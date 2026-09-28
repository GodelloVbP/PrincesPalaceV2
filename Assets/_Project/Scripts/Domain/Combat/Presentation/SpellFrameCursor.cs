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

        public SpellFrameSample(bool visible, int index, int next, float blend, float alpha,
            float scale = 1f)
        {
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

        public static SpellFrameSample SampleOf(SpellLayerInstance instance, int frameCount, float seconds)
        {
            if (instance == null || frameCount <= 0) return SpellFrameSample.Hidden;

            var layer = instance.Layer;
            float age = seconds - instance.StartSeconds;
            if (age < 0f) return SpellFrameSample.Hidden;

            float lifetime = instance.EndSeconds - instance.StartSeconds;
            float alpha = AlphaAt(instance, age, lifetime);
            if (alpha <= 0f) return SpellFrameSample.Hidden;

            float scale = PunchAt(layer, age, lifetime);

            // A STILL IS ONE FRAME OF A FOLDER, held. Its path names the folder
            // and not the file, which is mechanical rather than tidy: the drift
            // tests match a played path's last segment against recipe filenames
            // and require it to be a directory on disk, so `..._wake/f0` would
            // present `f0` as its provenance and fail both.
            int first = FirstIndex(layer, frameCount);
            if (instance.RenderKind == SpellRender.Still)
            {
                return new SpellFrameSample(true, first, -1, 0f, alpha, scale);
            }

            int playable = frameCount - first;
            if (playable <= 0) return new SpellFrameSample(true, frameCount - 1, -1, 0f, alpha, scale);

            float perFrame = PerFrame(layer, lifetime, playable);
            if (perFrame <= 0f) return new SpellFrameSample(true, first, -1, 0f, alpha, scale);

            float at = age / perFrame;

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

            return new SpellFrameSample(true, index, next, blend, alpha, scale);
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

            float remaining = 1f - age / window;
            return 1f + layer.punch * remaining * remaining;
        }

        // THE FADE-OUT, TIMES THE LAYER'S AUTHORED OPACITY. Opacity scales the
        // whole curve rather than capping it, so a half-opaque ripple still
        // fades from its own half to nothing over the same `fade` seconds.
        private static float AlphaAt(SpellLayerInstance instance, float age, float lifetime)
        {
            float opacity = OpacityOf(instance.Layer);
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
