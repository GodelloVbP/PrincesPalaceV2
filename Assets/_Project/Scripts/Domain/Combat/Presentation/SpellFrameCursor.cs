using System;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Presentation
{
    // WHAT A SPRITE LAYER LOOKS LIKE AT ONE INSTANT: which frame, which frame
    // is fading up over it, how far, and how visible the whole thing is.
    public readonly struct SpellFrameSample
    {
        // False before the layer opens and after its fade has run out. The one
        // question a renderer asks before anything else.
        public readonly bool Visible;

        public readonly int Index;

        // The frame dissolving in over Index, or -1 when there is none -- the
        // last frame of a sequence that does not loop, or a still.
        public readonly int Next;

        // 0 while Index is shown on its own, rising to 1 as Next takes over.
        public readonly float Blend;

        // The layer's own fade-out, 1 until its lifetime ends.
        public readonly float Alpha;

        // A MULTIPLIER ON THE PLACED BOX, 1 for every layer that authors no
        // punch. On the sample rather than on the instance because it is a
        // property of the INSTANT and not of the placement: Core measures the
        // box once at Begin and never touches it again, so a scale that varies
        // over the layer's life has to arrive with the frame.
        public readonly float Scale;

        // HOW FAR THE BOX IS TURNED NOW, counter-clockwise in WORLD degrees (the
        // layer's `rotate`, mirrored with the cast), and the pixel displacement of
        // the box's centre that turning it about the authored pivot costs. Both
        // zero for every layer that authors no rotate. Core adds Degrees to the
        // instance's own and the shift to the placement.
        public readonly float Degrees;
        public readonly float ShiftX;
        public readonly float ShiftY;

        // The glow boost now: the layer's `glow` moved by its glowCurve. Equal to
        // `glow` for every layer that authors no curve.
        public readonly float Glow;

        // The brightness above which that boost applies (SpellGlowCurve.knee).
        public readonly float Knee;

        public SpellFrameSample(bool visible, int index, int next, float blend, float alpha,
            float scale = 1f, float degrees = 0f, float shiftX = 0f, float shiftY = 0f, float glow = 0f,
            float knee = SpellGlowCurve.DefaultKnee)
        {
            Knee = knee;
            Glow = glow;
            Degrees = degrees;
            ShiftX = shiftX;
            ShiftY = shiftY;
            Visible = visible;
            Index = index;
            Next = next;
            Blend = blend;
            Alpha = alpha;
            Scale = scale;
        }

        public static SpellFrameSample Hidden => new SpellFrameSample(false, 0, -1, 0f, 0f);
    }

    // WHICH DRAWING IS ON SCREEN AT TIME t, as arithmetic rather than as a
    // coroutine's accumulated position.
    //
    // A COROUTINE CANNOT ANSWER THIS AT AN ARBITRARY t. It answers "what is on
    // screen now", where `now` is however many times it happened to resume --
    // so a long frame, a scene load or a test at 60x gets a different answer
    // for the same clock, and a test asserting what is drawn at the impact
    // instant is asserting how fast the machine is. A pure function of the
    // instant has one answer, and a held clock makes it observable.
    public static class SpellFrameCursor
    {
        // HOW MUCH OF EACH FRAME'S TIME IS SPENT DISSOLVING, and the number
        // matters. A frame that cross-fades from the moment it appears is never
        // itself: at 0.5 through, two drawings are on screen at half strength
        // each and neither is legible, which for a lightning bolt means two
        // bolts. Holding for the first 55% and dissolving over the last 45%
        // keeps every frame readable and removes the step between them.
        public const float DissolveFraction = 0.45f;

        // HOW MUCH OF A LAYER'S LIFETIME ITS OVERSHOOT TAKES TO SETTLE.
        //
        // A fifth, and the reason it is derived rather than authored is that
        // "how long does the punch last" has one sensible answer and content
        // asking it twice would let the two drift: long enough to be seen at
        // all -- at Cinderfault's new 0.39s that is 78ms, three frames at 40fps
        // -- and short enough that the layer is at its true size well before
        // its own peak. The rupture frame of a nine-frame sheet fitted into
        // `seconds` lands at 5/9 of the way through, comfortably past a fifth.
        public const float PunchFraction = 0.2f;

        // Float slack, in frames, added to a layer's age quotient (see SampleOf).
        private const float BoundarySnap = 1e-3f;

        // Seconds. A frame opening this close before the cue counts as opening on it.
        private const float CueTolerance = 1e-3f;

        public static SpellFrameSample SampleOf(SpellLayerInstance instance, int frameCount, float seconds)
        {
            if (instance == null || frameCount <= 0) return SpellFrameSample.Hidden;

            var layer = instance.Layer;

            // OPENED WHEN THE SCHEDULE SAYS SO, NOT WHEN THE RAW SUBTRACTION
            // DOES. SpellSchedule.Crossed opens a layer at the first tick that
            // Passed its start; a cursor a float hair under the start (18 ticks
            // of 1/60 s read 0.29998 against an authored 0.3) opens the layer
            // there and would then find age < 0 here, so the layer that
            // replaces a closing one drew nothing for one frame.
            if (!SpellSchedule.Passed(instance.StartSeconds, seconds)) return SpellFrameSample.Hidden;
            float age = Math.Max(0f, seconds - instance.StartSeconds);

            float lifetime = instance.EndSeconds - instance.StartSeconds;
            float alpha = AlphaAt(instance, age, lifetime);
            if (alpha <= 0f) return SpellFrameSample.Hidden;

            float phase = BreathePhaseOf(instance);
            float breathe = layer.breathe != null && layer.breathe.IsAuthored
                ? layer.breathe.ScaleMultiplierAt(age, phase)
                : 1f;
            if (layer.breathe != null && layer.breathe.IsAuthored)
            {
                alpha *= layer.breathe.AlphaMultiplierAt(age, phase);
            }

            float scale = PunchAt(layer, age, lifetime) * breathe;
            var pose = PoseOf(instance, age, scale);
            float glow = GlowOf(layer, age, lifetime);
            float knee = layer.glowCurve != null ? layer.glowCurve.KneeOrDefault : SpellGlowCurve.DefaultKnee;

            // A STILL IS ONE FRAME OF A FOLDER, held. Its path names the folder
            // and not the file, which is mechanical rather than tidy: the drift
            // tests match a played path's last segment against recipe filenames
            // and require it to be a directory on disk, so `..._wake/f0` would
            // present `f0` as its provenance and fail both.
            int first = FirstIndex(layer, frameCount);
            if (instance.RenderKind == SpellRender.Still)
            {
                return new SpellFrameSample(true, first, -1, 0f, alpha, scale, pose.degrees, pose.x, pose.y, glow, knee);
            }

            int playable = frameCount - first;

            float perFrame = PerFrame(layer, lifetime, playable);
            if (perFrame <= 0f) return new SpellFrameSample(true, first, -1, 0f, alpha, scale, pose.degrees, pose.x, pose.y, glow, knee);

            // A hair past the exact quotient, so a frame boundary authored on the
            // cue (0.30 s over 0.06 s frames is 4.9999995 in floats) opens on
            // the cue tick and not one tick after it.
            float at = age / perFrame + BoundarySnap;

            // WRAPPED FOR A LOOP, CLAMPED OTHERWISE. `once` and `hold` select
            // the same frame -- the last one -- and differ only in intent,
            // because a layer's LIFETIME is what ends it either way. That is
            // deliberate: making `once` stop drawing at the end of its sequence
            // would cut a travelling sheet short at its arrival, and today such
            // a sheet keeps playing its remaining frames at the target until
            // the sequence runs out.
            int index;
            float within;
            bool loops = instance.UntilKind == SpellEnd.Loop;

            if (loops)
            {
                float wrapped = at % playable;
                index = first + (int)wrapped;
                within = wrapped - (int)wrapped;
            }
            else
            {
                float clamped = at < playable - 1 ? at : playable - 1;
                index = first + (int)clamped;
                within = clamped - (int)clamped;
            }

            if (index >= frameCount) index = frameCount - 1;

            int next = NextIndex(index, first, frameCount, loops);
            float blend = next < 0 ? 0f : BlendAt(within);

            // NOTHING OF A POST-CUE FRAME SHOWS BEFORE THE CUE. The dissolve
            // into a frame starts DissolveFraction of a frame early, so a sheet
            // whose payoff frame opens on the cue (Thorn Tithe's sap burst)
            // was already half-drawn 1-2 frames ahead of the number, sound and
            // badge. A frame that opens at or after the cue cuts in on it.
            if (blend > 0f && instance.CueSeconds >= 0f && seconds < instance.CueSeconds)
            {
                float nextOpens = instance.StartSeconds + (index + 1 - first) * perFrame;
                if (nextOpens >= instance.CueSeconds - CueTolerance) blend = 0f;
            }

            return new SpellFrameSample(true, index, next, blend, alpha, scale, pose.degrees, pose.x, pose.y, glow, knee);
        }

        // THE LAYER'S GLOW BOOST at `age`: its `glow`, moved by its glowCurve.
        private static float GlowOf(SpellLayer layer, float age, float lifetime) =>
            layer.glowCurve != null && layer.glowCurve.IsAuthored
                ? layer.glowCurve.At(layer.glow, age, lifetime)
                : layer.glow;

        // WHERE THE LAYER'S BOX CENTRE IS DRAWN for a sample taken at `seconds`:
        // its placement and slide, its authored offset (mirrored with the cast)
        // and the shift its turn costs.
        public static UiVec CentreOf(SpellLayerInstance instance, in SpellFrameSample sample, float seconds) =>
            SpellPerformance.PositionOf(instance, seconds) +
            new UiVec(instance.Layer.dx * instance.DrawFacing, instance.Layer.dy) +
            new UiVec(sample.ShiftX, sample.ShiftY);

        // HOW MUCH THIS LAYER LIFTS THE COAL GLOW OF THE BODY IT IS ON at
        // `seconds`: its `stoke`, times its own alpha as a share of the authored
        // opacity (so its fade-in, fade-out and breathing all show), and zero
        // before it opens or after it clears.
        public static float StokeOf(SpellLayerInstance instance, float seconds)
        {
            var layer = instance?.Layer;
            if (layer == null || !(layer.stoke > 0f)) return 0f;
            if (!SpellSchedule.Passed(instance.StartSeconds, seconds)) return 0f;

            float age = Math.Max(0f, seconds - instance.StartSeconds);
            float lifetime = instance.EndSeconds - instance.StartSeconds;
            float alpha = AlphaAt(instance, age, lifetime);
            if (alpha <= 0f) return 0f;

            if (layer.breathe != null && layer.breathe.IsAuthored)
            {
                alpha *= layer.breathe.AlphaMultiplierAt(age, BreathePhaseOf(instance));
            }

            float opacity = OpacityOf(layer);
            return opacity <= 0f ? 0f : layer.stoke * Math.Min(1f, alpha / opacity);
        }

        // Seconds on the Ember curve that this layer's breathing starts at: a
        // fixed spread per layer index, so two breathing layers of one spell
        // are not on the same beat, and the same cast repeats exactly.
        private const float BreathePhaseSpread = 1.7f;

        private static float BreathePhaseOf(SpellLayerInstance instance) =>
            instance.LayerIndex * BreathePhaseSpread;

        // THE LAYER'S TURN AT `age`, in world degrees, and where turning it about
        // its pivot moves its centre. The box rotates about its own centre in
        // the renderer, so a pivot elsewhere is the same turn plus a shift of
        // p - R(theta) p, p being the pivot's offset from the centre in the
        // drawn (scaled, mirrored) box.
        private static (float degrees, float x, float y) PoseOf(SpellLayerInstance instance, float age, float scale)
        {
            var rotate = instance.Layer.rotate;
            if (rotate == null || !rotate.IsAuthored) return (0f, 0f, 0f);

            float facing = instance.DrawFacing;
            float degrees = rotate.AngleAt(age) * facing;

            double radians = degrees * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            double px = (rotate.pivotX - 0.5) * instance.Box.X * scale * facing;
            double py = (rotate.pivotY - 0.5) * instance.Box.Y * scale;

            float x = (float)(px - (cos * px - sin * py));
            float y = (float)(py - (sin * px + cos * py));
            return (degrees, x, y);
        }

        // WHERE A POINT FIXED ON A TURNING LAYER'S ART IS, as a displacement from
        // where the same point sits on the unturned box, `age` seconds into the
        // layer. `offset` is from the box centre in the cast's frame, already
        // fitted and mirrored; the layer's own turn is applied about its pivot.
        // An emitter riding a swinging layer is born here, so smoke leaves the
        // censer's lid wherever the swing has carried it. Zero when the layer
        // authors no rotate.
        public static UiVec PointOn(SpellLayerInstance instance, float age, UiVec offset)
        {
            var rotate = instance?.Layer?.rotate;
            if (rotate == null || !rotate.IsAuthored) return UiVec.Zero;

            var pose = PoseOf(instance, Math.Max(0f, age), 1f);
            double radians = pose.degrees * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            float x = pose.x + (float)(cos * offset.X - sin * offset.Y) - offset.X;
            float y = pose.y + (float)(sin * offset.X + cos * offset.Y) - offset.Y;
            return new UiVec(x, y);
        }

        // The 1-based startFrame as an index; 0 means the first frame.
        public static int FirstIndex(SpellLayer layer, int frameCount)
        {
            int first = layer.startFrame > 1 ? layer.startFrame - 1 : 0;
            return first >= frameCount ? frameCount - 1 : first;
        }

        // AN AUTHORED RATE, OR THE FOLDER FITTED INTO THE LIFETIME. `fps: 0` is
        // what every pre-layer block becomes, and it reproduces
        // `perFrame = total / frames.Length` exactly -- which matters because a
        // pre-layer block's frame rate IS its duration over its frame count and
        // was never authored.
        private static float PerFrame(SpellLayer layer, float lifetime, int playable)
        {
            if (layer.fps > 0f) return 1f / layer.fps;
            return lifetime > 0f ? lifetime / playable : 0f;
        }

        // THE LAYER'S SLIDE (SpellLayer.drift) at `seconds`, in fractions of its
        // own box; (0, 0) for a layer that authors none. Over the layer's
        // lifetime, not including its fade.
        //
        // A LAYER THAT TRAVELS SLIDES OVER ITS FLIGHT, not over its life: its
        // life includes the wind-up it is held at the caster for, and a slide
        // that ran through the hold lifted Mud Burst's glob in Shawn's hand.
        // The slide's clock is the flight's own fraction (the same ease the
        // position uses), held at its start before departure and at its end
        // after arrival, so an arc peaks where the body is, not at mid-time.
        public static (float x, float y) DriftAt(SpellLayerInstance instance, float seconds)
        {
            var drift = instance?.Layer?.drift;
            if (drift == null || !drift.IsAuthored) return (0f, 0f);

            if (instance.Layer.travelSeconds > 0f)
            {
                float flown = SpellPerformance.FractionOf(instance, seconds);
                return drift.At(flown < 1f ? flown : 1f, 1f);
            }

            return drift.At(seconds - instance.StartSeconds, instance.EndSeconds - instance.StartSeconds);
        }

        // THE OVERSHOOT, EASED OUT. The layer opens at 1 + punch and settles
        // to 1 over PunchFraction of its lifetime, on a quadratic ease-out
        // (1-t)^2 -- most of the overshoot is gone in the first third of the
        // window, which is what makes it read as a snap rather than as a
        // shrink.
        //
        // EASE-OUT RATHER THAN EASE-IN-OUT, deliberately. An accent's whole
        // job is to put its energy at the front; a symmetric curve spends half
        // the window still arriving, and at 78ms there is no half of a window
        // to spend.
        //
        // OFF BY DEFAULT AND EXACTLY 1, not 1-ish: `punch` unauthored returns
        // the literal, so every layer that shipped before this multiplies its
        // box by a number that cannot round it.
        private static float PunchAt(SpellLayer layer, float age, float lifetime)
        {
            if (layer == null || layer.punch <= 0f || lifetime <= 0f) return 1f;

            float window = lifetime * PunchFraction;
            if (window <= 0f || age >= window) return 1f;

            return 1f + layer.punch * SpellEase.Settle(age / window);
        }

        // THE FADE-OUT, TIMES THE LAYER'S AUTHORED OPACITY. Opacity scales the
        // whole curve rather than capping it, so a half-opaque ripple still
        // fades from its own half to nothing over the same `fade` seconds.
        private static float AlphaAt(SpellLayerInstance instance, float age, float lifetime)
        {
            float opacity = OpacityOf(instance.Layer);
            float fadeIn = instance.Layer.fadeIn;
            if (fadeIn > 0f && age < fadeIn) opacity *= age / fadeIn;
            if (age <= lifetime) return opacity;
            if (instance.FadeSeconds <= 0f) return 0f;

            float into = (age - lifetime) / instance.FadeSeconds;
            return into >= 1f ? 0f : (1f - into) * opacity;
        }

        // The authored opacity, clamped to what can be drawn. Content refuses
        // anything outside (0, 1] (SpellLayerRules); the clamp is for a layer
        // built in code, and a null layer draws as painted. Public so the
        // emitter path multiplies its particles by the same number.
        public static float OpacityOf(SpellLayer layer)
        {
            if (layer == null) return 1f;
            float opacity = layer.opacity;
            return opacity < 0f ? 0f : opacity > 1f ? 1f : opacity;
        }

        private static int NextIndex(int index, int first, int frameCount, bool loops)
        {
            if (index + 1 < frameCount) return index + 1;
            return loops ? first : -1;
        }

        private static float BlendAt(float within)
        {
            float held = 1f - DissolveFraction;
            return within <= held ? 0f : (within - held) / DissolveFraction;
        }
    }
}
