using System;

namespace PrincesPalace.Domain.Content
{
    // ONE DRAWING A SPELL MAKES, and a spell is an ordered array of them.
    //
    // A layer answers three independent questions -- what renders, where it
    // belongs, when it runs -- and answering them separately is the whole
    // point. The shape this replaces had four named blocks (flight, trail,
    // contact, particles), one of each, which is four hardcoded slots: a
    // caster-side wind-up, a second burst or a shared ground layer that is not
    // Cinderfault's each needed a FIFTH block and the code to read it. Here
    // they are five entries in one array and cost a content edit.
    //
    // EVERY DISCRIMINATOR IS A STRING, PARSED, never the enum itself, for the
    // reason SpellPresentation.anchor already gives: JsonUtility writes an enum
    // as its ORDINAL, so the file would read "render": 2 and reordering the
    // enum would silently repoint every spell. The word is what an author types
    // and what an error message can name.
    //
    // A BLANK discriminator is the documented default rather than an error --
    // SpellAnchorNames.IsKnown already returns true for blank precisely so an
    // unauthored word means the default. Blank `render` and blank `place` are
    // the two exceptions, refused by SpellLayerRules, because there is no safe
    // default for WHAT draws or WHERE.
    [Serializable]
    public class SpellLayer
    {
        [ContentDoc("Stable name for this layer; required only when another layer references it through place 'layer:<id>'.")]
        public string id = "";

        [ContentDoc("What draws: sprite (an animated folder), still (one frame of a folder) or emitter (ballistic particles). No default -- a blank is refused.")]
        public string render = "";

        [ContentDoc("Where it belongs: caster, caster-centre, target, target-centre, formation, or 'layer:<id>' to ride another layer. No default -- a blank is refused.")]
        public string place = "";

        // FOLLOW versus SAMPLE-ONCE, and the difference is visible the moment
        // anything moves. A splash sampled once stays where the target stood
        // when the cast opened, which is right for a target that dies mid-flight
        // -- the blow landed where the body was. A wake riding a projectile has
        // to re-read every tick or it is not a wake.
        [ContentDoc("Re-read the anchor every tick (true) rather than sampling its position once when the layer opens (false).")]
        public bool follow;

        [ContentDoc("When it starts: release (the beat opens), arrival (the cast's projectile lands) or hit (the authoritative impact cue). Blank means release.")]
        public string at = "";

        [ContentDoc("Seconds added to `at`.")]
        public float offset;

        [ContentDoc("Resources-relative FOLDER of frames, for sprite and still alike; a still draws that folder's startFrame.")]
        public string path = "";

        [ContentDoc("Total playback length in seconds; 0 derives it from fps and the folder's frame count.")]
        public float seconds;

        [ContentDoc("Frames per second; 0 means fit the whole folder into `seconds`, which is what every pre-layer block becomes.")]
        public float fps;

        [ContentDoc("Which frame the layer starts on, counting from 1; 0 means frame 1.")]
        public int startFrame;

        [ContentDoc("End policy: once, loop or hold. Blank means once. A travelling layer ends at its arrival whatever this says.")]
        public string until = "";

        [ContentDoc("Seconds of alpha ramp-out after the layer's end; 0 means cut. Capped at SpellLayerRules.MaxFadeSeconds.")]
        public float fade;

        [ContentDoc("Local offset from the anchor, in reference-frame units.")]
        public float dx;
        [ContentDoc("Local offset from the anchor, in reference-frame units.")]
        public float dy;

        [ContentDoc("Uniform scale applied on top of the fitted box.")]
        public float scale = 1f;

        [ContentDoc("The square box the art is fitted into; 0 means SpellPresentation.DefaultSize. Ignored by place 'formation', which measures its own span.")]
        public float size;

        [ContentDoc("What `size` scales against: none (the authored number, verbatim) or target (multiplied by the struck target's own stage footprint -- its rank depth times its authored stageScale). Blank means none. Legal only where a single struck target is resolved: place target or target-centre.")]
        public string fit = "";

        [ContentDoc("Mirroring: auto (take the cast's facing), none (never mirror) or reverse. Blank means auto.")]
        public string facing = "";

        [ContentDoc("Draw band: ground (behind the racks) or effects (over the HUD, under the damage numbers). Blank means effects.")]
        public string sort = "";

        // WHICH WAY A FORMATION LAYER LIES ON THE RANK IT SPANS. Only
        // `place: formation` has two points to draw a line between, so
        // SpellLayerRules refuses this word anywhere else rather than letting
        // it sit there doing nothing.
        [ContentDoc("How a formation-placed layer lies on the rank: level (axis-aligned, the default) or span (rotated along the line from the leftmost struck body to the rightmost). Refused on any other placement.")]
        public string align = "";

        // THE OVERSHOOT, AND WHY IT IS ONE NUMBER RATHER THAN TWO. A punch is
        // an amount and a settle time, and the settle time has exactly one
        // sensible value -- short enough to read as an impact rather than as a
        // zoom. Authoring it would be a second number with no second answer,
        // so the window is derived (SpellFrameCursor.PunchFraction of the
        // layer's own lifetime) and only the amount is authored.
        [ContentDoc("Extra scale at the layer's opening instant, eased out to nothing over the first fifth of its lifetime; 0 means no punch. 0.2 opens it 20% oversized.")]
        public float punch;

        // BRIGHTNESS ABOVE 1, WHICH IS THE ONLY WAY ANYTHING IN THIS GAME CAN
        // BLOOM. The Volume profile's Bloom threshold is 1.05 (PipelineBuilder)
        // and a Canvas Image cannot exceed 1.0 on its own: sprite textures top
        // out at white and the vertex colour a CanvasRenderer carries is a
        // Color32, so `image.color` clamps. Without a boost applied in the
        // shader, no UI pixel in the game has ever crossed that threshold.
        //
        // APPLIED TO THE HOT CORE ONLY, not to the whole drawing -- see
        // Resources/Shaders/UISpellGlow.shader. A flat multiply would push the
        // midtones past 1 as well, and with no tonemapper in the profile
        // everything above 1 clips to white: the "pop" would be a white blob.
        [ContentDoc("How far above 1 this layer's brightest pixels are pushed so the Bloom override can see them; 0 means draw it flat, as every layer did before. 1.2 roughly doubles the hot core.")]
        public float glow;

        [ContentDoc("Where the blow lands inside this layer's frames, as a fraction from the left edge; -1 means unauthored.")]
        public float impactX = SpellPresentation.Unauthored;
        [ContentDoc("Where the blow lands inside this layer's frames, as a fraction from the bottom edge; -1 means unauthored.")]
        public float impactY = SpellPresentation.Unauthored;

        [ContentDoc("Width-over-height of the box; 0 means take the sheet's own frame aspect.")]
        public float aspect;

        // TRAVEL IS A BEHAVIOUR, NOT A KIND OF THING. A layer with
        // travelSeconds > 0 is a projectile; one without is not. That keeps
        // "flies across the stage" where the brief puts it -- something that
        // happens between release and impact -- rather than making it a
        // placement word and a parallel code path.
        [ContentDoc("Seconds this layer takes to cross from its anchor to the target, departure to arrival; 0 means it does not travel.")]
        public float travelSeconds;

        // THE HOLD BEFORE THE FLIGHT, and it has to be its own number rather
        // than a schedule offset. A travelling sheet today is DRAWN and
        // ANIMATING on the caster through its wind-up and only then leaves;
        // delaying the layer's start instead would delay the drawing too, which
        // is a different animation and is the one
        // SpellVfxTests.ATravellingEffectHoldsAtTheCasterUntilItsChargeIsDone
        // reads the image's position to refuse.
        [ContentDoc("Seconds after this layer starts before its motion begins; the wind-up held at the caster.")]
        public float travelDelay;

        [ContentDoc("Ballistic particle settings; inert unless render is 'emitter'.")]
        public SpellEmitter emitter = new SpellEmitter();

        // A DEEP COPY, because SpellPresentation.Copy is the only boundary
        // between the catalogue and a combat beat and a shallow array copy
        // would alias the catalogue's own layer objects into every beat -- the
        // exact bug Copy()'s header exists to prevent, and worse here than for
        // the scalar fields because a layer is a mutable object a view could
        // write through.
        public SpellLayer Copy() => new SpellLayer
        {
            id = id,
            render = render,
            place = place,
            follow = follow,
            at = at,
            offset = offset,
            path = path,
            seconds = seconds,
            fps = fps,
            startFrame = startFrame,
            until = until,
            fade = fade,
            dx = dx,
            dy = dy,
            scale = scale,
            size = size,
            fit = fit,
            facing = facing,
            sort = sort,
            align = align,
            punch = punch,
            glow = glow,
            impactX = impactX,
            impactY = impactY,
            aspect = aspect,
            travelSeconds = travelSeconds,
            travelDelay = travelDelay,
            emitter = emitter == null ? new SpellEmitter() : emitter.Copy(),
        };

        public SpellRender Render => SpellRenderNames.Parse(render);
        public SpellPlace Place => SpellPlaceNames.Parse(place);
        public SpellCue At => SpellCueNames.Parse(at);
        public SpellEnd Until => SpellEndNames.Parse(until);
        public SpellFacing Facing => SpellFacingNames.Parse(facing);
        public SpellSort Sort => SpellSortNames.Parse(sort);
        public SpellAlign Align => SpellAlignNames.Parse(align);
        public SpellFit Fit => SpellFitNames.Parse(fit);

        // The id this layer rides, or empty when it does not ride one. Read
        // rather than re-split at each of the four call sites that need it.
        public string FollowsLayerId => SpellPlaceNames.LayerReference(place);

        public bool Travels => travelSeconds > 0f;

        // Both halves of the point, never one -- the same rule
        // SpellPresentation.HasImpactPoint states, for the same reason: a sheet
        // corrected on one axis and left on the other lands somewhere neither
        // rule intended and reads as a third bug.
        public bool HasImpactPoint => IsUnitFraction(impactX) && IsUnitFraction(impactY);

        public bool HasImpactY => impactY >= 0f && impactY <= 1f;

        // THE [0,1] BOUND impactX/impactY ARE AUTHORED IN, stated once for
        // this type, SpellPresentation.HasImpactPoint and SpellLayerRules.
        // CheckPlacement to share -- a fraction of the frame, not a pixel
        // coordinate.
        public static bool IsUnitFraction(float value) => value >= 0f && value <= 1f;

        // WHETHER layers[index]'S ART LANDS PER STRUCK TARGET -- its own
        // placement (Travels or a per-target Place), or inherited by riding a
        // layer that does. SpellPerformance.FanOut's per-instance scope and
        // SpellPresentation.HasPerTargetArt's content-time "does this block
        // put anything on a target" question are the same walk over the same
        // array, so this is the one home for it -- the second copy is what
        // let HasPerTargetArt check only a layer's own placement and miss a
        // follower chain that lands on a per-target source.
        //
        // `idOf` resolves an authored id to its index in `layers`, however
        // the caller wants to look it up -- a prebuilt dictionary for
        // FanOut's hot path, a linear scan for HasPerTargetArt's one-shot
        // content check. `depth` bounds the walk: content that cycles is
        // refused at build time by SpellLayerRules, but a reader of
        // unvalidated content must not hang on one that does anyway.
        public static bool IsPerTarget(SpellLayer[] layers, int index, Func<string, int> idOf, int depth = 0)
        {
            if (layers == null || index < 0 || index >= layers.Length) return false;

            var layer = layers[index];
            if (layer == null) return false;
            if (layer.Travels || SpellPlaceNames.PerTarget(layer.Place)) return true;

            string source = layer.FollowsLayerId;
            if (string.IsNullOrWhiteSpace(source) || depth > layers.Length) return false;

            int at = idOf(source);
            return at >= 0 && IsPerTarget(layers, at, idOf, depth + 1);
        }
    }

    // WHAT A BALLISTIC EMITTER NEEDS, and nothing else. The brief's minimum
    // list, one field each: explicitly no collision, no sub-emitters, no colour
    // curves, no per-particle scripting.
    //
    // Every field is read by SpellEmitterSim as a closed form over
    // (spec, seed, index, age), never integrated, which is what makes a large
    // clock step correct by construction and a seeded preview repeat exactly.
    [Serializable]
    public class SpellEmitter
    {
        [ContentDoc("Resources-relative folder of particle stills; each particle picks one by hash, so a folder of one selects that one.")]
        public string path = "";

        [ContentDoc("Particles emitted per second while the window is open.")]
        public float rate;

        [ContentDoc("Particles emitted the instant the window opens.")]
        public int burst;

        [ContentDoc("Seconds of emission; 0 means burst only.")]
        public float window;

        [ContentDoc("Offset from the source anchor that particles are born at, in reference-frame units.")]
        public float sourceDx;
        [ContentDoc("Offset from the source anchor that particles are born at, in reference-frame units.")]
        public float sourceDy;

        [ContentDoc("Full cone width of the emission spread, in degrees.")]
        public float spreadDegrees;

        [ContentDoc("Centre of the emission cone, in degrees; 0 means along the cast's facing.")]
        public float aimDegrees;

        [ContentDoc("Slowest initial speed, in reference-frame units per second.")]
        public float speedMin;
        [ContentDoc("Fastest initial speed, in reference-frame units per second.")]
        public float speedMax;

        [ContentDoc("Fraction of the source's forward velocity a particle carries away, 0..1.")]
        public float inherit;

        [ContentDoc("Linear drag per second.")]
        public float drag;

        [ContentDoc("Acceleration in reference-frame units per second squared; negative falls.")]
        public float gravity;

        [ContentDoc("Shortest particle lifetime, in seconds.")]
        public float lifeMin;
        [ContentDoc("Longest particle lifetime, in seconds; also what an emitter layer's own lifetime adds to its window.")]
        public float lifeMax;

        [ContentDoc("Smallest particle scale.")]
        public float sizeMin = 1f;
        [ContentDoc("Largest particle scale.")]
        public float sizeMax = 1f;

        [ContentDoc("Slowest spin, in degrees per second; may be negative.")]
        public float spinMin;
        [ContentDoc("Fastest spin, in degrees per second.")]
        public float spinMax;

        [ContentDoc("Fraction of life at which alpha starts falling, 0..1.")]
        public float fadeFrom = 1f;

        [ContentDoc("Scale at the end of life, relative to the particle's own.")]
        public float endScale = 1f;

        // THE REPEATABILITY CONTRACT. A preview or a test that sets this gets
        // the identical droplet field every run, because the simulation is a
        // pure function of (spec, seed, index, age) and never of accumulated
        // state. Zero derives from the cast's own generation counter, so two
        // casts in one fight differ and neither repeats -- right for playing,
        // wrong for a picture, which is why the field exists at all.
        [ContentDoc("Random seed; 0 derives one from the cast so two casts differ, non-zero repeats exactly.")]
        public int seed;

        // PER-CELL CHANCE, not per-particle behaviour. One float per still in
        // emitter.path's folder, in the folder's own file order -- the same
        // order SpellEmitterSim.At already draws frame indices in. Absent or
        // empty means uniform, which is the existing hash-modulus rule
        // (AUDIT #108's whole compatibility promise: a shipped emitter with no
        // weights authored plays the identical field it always did).
        //
        // VALIDATED IN TWO PLACES FOR TWO DIFFERENT REASONS. SpellLayerRules
        // refuses a negative, all-zero or non-finite entry because those are
        // questions about the numbers alone, answerable with no disk access.
        // Whether the array's LENGTH matches the folder's frame count is not
        // -- Domain cannot see the disk -- so that check lives beside the
        // identical one for startFrame, in
        // SpellVfxRecipeDriftTests.NoSkillTimesABeatToAFrameItsFolderDoesNotHave.
        [ContentDoc("Per-cell pick weight, one per still in emitter.path's folder in file order; blank means uniform. Each entry must be finite and >= 0, and at least one must be > 0. Length must equal the folder's own frame count.")]
        public float[] weights;

        public SpellEmitter Copy() => new SpellEmitter
        {
            path = path,
            rate = rate,
            burst = burst,
            window = window,
            sourceDx = sourceDx,
            sourceDy = sourceDy,
            spreadDegrees = spreadDegrees,
            aimDegrees = aimDegrees,
            speedMin = speedMin,
            speedMax = speedMax,
            inherit = inherit,
            drag = drag,
            gravity = gravity,
            lifeMin = lifeMin,
            lifeMax = lifeMax,
            sizeMin = sizeMin,
            sizeMax = sizeMax,
            spinMin = spinMin,
            spinMax = spinMax,
            fadeFrom = fadeFrom,
            endScale = endScale,
            seed = seed,
            // A DEEP COPY, for the same reason SpellLayer.Copy() takes one of
            // this whole object: a shallow assignment would alias the
            // catalogue's own array into every combat beat, and a beat that
            // mutated it (it does not today, but Copy()'s whole point is not
            // to depend on that staying true) would corrupt the next cast.
            weights = weights == null ? null : (float[])weights.Clone(),
        };

        // Whether anything on this block was authored, measured against a
        // DEFAULT-CONSTRUCTED one rather than against zero. sizeMin, sizeMax
        // and fadeFrom all initialise to 1f, so a "non-zero" test would report
        // every sprite layer in the game as carrying inert emitter settings and
        // refuse the whole Water pilot.
        //
        // BY REFLECTION rather than as a hand-written field list, and the
        // reason is measured: SpellPresentation.Copy() is a hand-written list
        // and AUDIT #60 records the identical shape dropping fields twice.
        // A list that has to be extended by hand for every new emitter knob
        // would report "inert" for the one field somebody forgot, which is a
        // validation rule that fails by going quiet. Runs once per layer at
        // content build, never in a fight.
        public bool IsAuthored => !FieldsEqual(this, new SpellEmitter());

        // ARRAY-AWARE, because `Equals` on two `float[]` references is
        // identity comparison -- `weights` has no initialiser (null is
        // "uniform"), but the moment any field on this class defaulted to a
        // freshly-allocated array, a bare `Equals` would compare that default
        // against A DIFFERENT freshly-allocated array on the other side of
        // every IsAuthored check and report every sprite layer in the game as
        // authoring emitter settings -- refusing the whole Water pilot on a
        // field that is not even weights. Comparing element-by-element is
        // what keeps that failure mode impossible rather than merely avoided
        // by today's choice of default.
        private static bool FieldsEqual(SpellEmitter a, SpellEmitter b)
        {
            foreach (var field in typeof(SpellEmitter).GetFields(
                         System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                object va = field.GetValue(a);
                object vb = field.GetValue(b);

                if (va is float[] || vb is float[])
                {
                    if (!ArraysEqual(va as float[], vb as float[])) return false;
                    continue;
                }

                if (!Equals(va, vb)) return false;
            }

            return true;
        }

        private static bool ArraysEqual(float[] a, float[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }
    }
}
