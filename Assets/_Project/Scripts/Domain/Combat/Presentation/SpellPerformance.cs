using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Combat.Presentation
{
    // ONE DRAWING A CAST ACTUALLY MAKES: a layer, fanned out to a target, with
    // its times already absolute and its path already two points.
    //
    // A CLASS WITH MUTABLE POSITION FIELDS rather than a readonly struct,
    // because the two halves of "where" are owned by two layers of the program.
    // Domain resolves the schedule and the scope, which is pure arithmetic over
    // authored content and is where every test below reaches. WHERE a struck
    // slot actually is depends on the stage's depth scaling and on where the
    // slots landed, neither of which Domain can see -- so Core writes From and
    // To once, at Begin, and nothing writes them again.
    public sealed class SpellLayerInstance
    {
        public SpellLayer Layer;

        // Layer.Render/Place/At/Until/Facing/Sort/Align, PARSED ONCE HERE
        // rather than read as computed properties on the frame path -- content
        // is immutable after load, so Resolve (below) is the one place any of
        // the seven needs to touch a string. "Kind" suffixed because Facing
        // already names the per-instance CAST direction (below); these seven
        // are the content's own authored word, resolved to its enum.
        public SpellRender RenderKind;
        public SpellPlace PlaceKind;
        public SpellCue AtKind;
        public SpellEnd UntilKind;
        public SpellFacing FacingKind;
        public SpellSort SortKind;
        public SpellAlign AlignKind;

        // AUTHORED ARRAY ORDER, kept because it is draw order. A cast allocates
        // its renderers in this order from the lowest free member, so a spray
        // authored after its splash gets the higher index and draws over it --
        // with no z-order for content to author and nothing to keep in sync.
        public int LayerIndex;

        // Which struck target this instance belongs to, or -1 for a cast-level
        // layer. Not a combatant: Domain has no idea what one is, and the
        // caller walks its own struck list by this index.
        public int TargetIndex;

        // The instance this one rides, or -1. A follower's lifetime is its
        // source's, so this is read by the resolution below and by the renderer
        // that has to place it every tick.
        public int SourceInstance;

        // Seconds from the beat opening. Start is when it opens; End is when
        // its own lifetime is over and its fade begins; Cleared is when the
        // last of it is gone.
        public float StartSeconds;
        public float EndSeconds;
        public float FadeSeconds;

        public float ClearedSeconds => EndSeconds + FadeSeconds;

        // Stage-space, written by Core at Begin. Equal for a layer that does
        // not travel, which is what lets PositionOf be one function rather than
        // two: a non-travelling source's position is its anchor and its
        // velocity is zero, through the identical arithmetic.
        public UiVec From;
        public UiVec To;

        // The box the art is fitted into, and which way it points. Written by
        // Core beside From/To and for the same reason: both depend on the
        // stage's depth scaling and on the sheet's own transparent margins,
        // neither of which Domain can measure.
        //
        // ON THE INSTANCE rather than in a parallel array handed alongside the
        // performance. Two arrays keyed by the same index is the shape this
        // codebase already removed once -- `string[] iconIds` beside
        // `Sprite[] iconSprites` across five controllers -- and it fails the
        // same way: nothing makes the two lengths agree.
        public UiVec Box;

        // HOW FAR THE BOX IS TURNED, counter-clockwise, in degrees. Zero for
        // every layer that was ever drawn before `align: span` existed, which
        // is what keeps a renderer that reads this from having to ask whether
        // the layer rotates at all.
        //
        // WRITTEN BY CORE, beside Box/From/To and for the same reason: the
        // angle is the angle of the line the struck bodies ACTUALLY landed on,
        // which depends on the depth curve and on the stand-off and is
        // therefore not something Domain can measure. The arithmetic that
        // turns two measured points into an angle IS engine-free and lives in
        // Domain.Stage.FormationSpan, so the part worth pinning with literals
        // is pinned.
        public float Degrees;

        // FALSE WHEN CORE COULD NOT FIND ANYWHERE FOR THIS LAYER TO GO --
        // FightController.PlaceOne's early return, a cast-level layer whose
        // beat struck no target. Written before SpellPerformancePlayer.Begin
        // runs, so Open() can refuse the layer without ever taking a pooled
        // renderer for a box that is still all-zero: the alternative was an
        // instance that opened, held a member and drew nothing at the stage
        // origin for its whole lifetime, one renderer short for whatever the
        // cast's next layer needed. True is the default because every other
        // placement path (target, caster, formation) always succeeds.
        public bool Placed = true;

        // +1 as drawn, -1 mirrored. A property of the CAST rather than of a
        // target, so a projectile and the wake riding it cannot disagree about
        // which way they point.
        public float Facing = 1f;

        // THE CAST'S FACING AFTER THIS LAYER'S OWN POLICY, which is the number
        // every mirroring decision actually wants -- the sheet's scale, the
        // layer's dx, an emitter's sourceDx and the angle its cone opens at.
        //
        // ASKED RATHER THAN RECOMPUTED. The expression `Facing == None ? 1 :
        // instance.Facing` was written out at two call sites and a third read
        // `instance.Facing` raw, which is how `facing: none` came to mirror an
        // emitter's cone while un-mirroring the sheet it was thrown from. One
        // property, four readers, and a fifth reader cannot get it wrong.
        public float DrawFacing =>
            Layer != null && Layer.Facing == SpellFacing.None ? 1f : (Facing < 0f ? -1f : 1f);
    }

    // A PRESENTATION, RESOLVED: every layer fanned out, every time absolute,
    // every version-0 block already through the adapter.
    //
    // THE ONE PLACE layerFormat IS READ AT RUN TIME. A version-1 presentation
    // passes through with its authored layers; a version-0 one is put through
    // SpellPresentation.ToLayers here and nowhere else, so nothing downstream
    // -- not the scheduler, not a renderer, not the beat player -- ever asks
    // which format a spell was written in. That is what makes ONE orchestration
    // path rather than two, and it is the thing that disappears the day the
    // last pre-layer block is re-authored.
    //
    // IN DOMAIN RATHER THAN CORE, which the plan placed the other way. Every
    // line of it is arithmetic over authored content and a frame count, and the
    // frame count arrives as a function rather than as a folder read -- so
    // putting it here costs nothing and buys the whole of it an EditMode test,
    // including the departure/travel split, which is the part a wrong answer is
    // least visible in.
    public sealed class SpellPerformance
    {
        public IReadOnlyList<SpellLayerInstance> Instances { get; private set; }

        public SpellSchedule Schedule { get; private set; }

        // WHEN THE CAST'S PROJECTILE LANDS. One number for the whole cast, at N
        // places: three projectiles leave one caster and land on three slots at
        // the SAME instant, because travelSeconds is an authored number and not
        // a distance divided by a speed. Computed once, here, and read only by
        // the scheduling below -- a layer never asks "when did I arrive", it is
        // scheduled at a time this already resolved.
        //
        // 0 when nothing travels, which is release. Defined rather than
        // undefined so the adapter's output -- every legacy layer is at release
        // -- has no hole to fall into. Content that schedules `arrival` in a
        // cast with no traveller is refused at build time as a TYPO, not
        // because the value would be wrong.
        public float ArrivalSeconds { get; private set; }

        // THE ONE INSTANT THE BLOW LANDS, and the only number the beat player
        // reads off a presentation. A version-1 block authors it; a version-0
        // block derives it from impactFrame over the frame count, which is the
        // identical expression ImpactDelayFor used before this existed --
        // preserved deliberately, because changing it would retime five shipped
        // spells.
        public float HitCueSeconds { get; private set; }

        // How long until nothing of this cast is drawn. What a player reads to
        // release a handle without polling every renderer.
        public float ClearedSeconds { get; private set; }

        // `frameCountOf` answers how many frames a Resources folder holds. A
        // function rather than a folder read because Domain cannot see one, and
        // rather than a pre-computed number because a cast touches between one
        // and six folders and only Core knows which are loaded.
        public static SpellPerformance Resolve(SpellPresentation vfx, int targetCount,
            Func<string, int> frameCountOf)
        {
            var performance = new SpellPerformance();
            var presentation = vfx ?? new SpellPresentation();
            int targets = targetCount < 0 ? 0 : targetCount;

            var layers = presentation.HasLayers
                ? presentation.layers
                : presentation.ToLayers(FramesOf(frameCountOf, presentation.path));

            performance.HitCueSeconds = presentation.HasLayers
                ? Math.Max(0f, presentation.hitCueSeconds)
                : LegacyHitCue(presentation, frameCountOf);

            var instances = FanOut(layers, targets);
            performance.ArrivalSeconds = ArrivalOf(layers, performance.HitCueSeconds);

            AssignTimes(instances, performance, frameCountOf);

            performance.Instances = instances;
            performance.Schedule = BuildSchedule(instances, performance.HitCueSeconds);
            performance.ClearedSeconds = ClearedOf(instances, performance.HitCueSeconds);
            return performance;
        }

        // WHERE A LAYER INSTANCE IS AT TIME t, and the ONE evaluation of an
        // eased flight path in the program. The sprite renderer places its box
        // with this and the emitter simulation samples its source with it, so
        // the ease cannot have two homes -- which is what a time-driven
        // scheduler lerping linearly beside a renderer easing quadratically
        // would have produced: a silent reshaping of every travelling spell.
        //
        // THE EASE IS t*t AND IS NOT AUTHORED. It is what ships
        // (SpellVfxPlayer's flight lerp) and the comment there says why -- the
        // projectile leaves fast and hard rather than being dragged the whole
        // way. One house rule until a second spell wants a different one; then
        // it is a word on the layer, additive under the same format version.
        public static UiVec PositionOf(SpellLayerInstance instance, float seconds)
        {
            float u = Progress(instance, seconds);
            float eased = u * u;
            return instance.From + (instance.To - instance.From) * eased;
        }

        // The exact derivative of the line above, which is why it is here and
        // not measured by differencing two samples: a difference over a tick
        // would make inherited velocity a function of frame rate.
        public static UiVec VelocityOf(SpellLayerInstance instance, float seconds)
        {
            float travel = instance.Layer == null ? 0f : instance.Layer.travelSeconds;
            if (travel <= 0f) return UiVec.Zero;

            float u = Progress(instance, seconds);
            return (instance.To - instance.From) * (2f * u / travel);
        }

        private static float Progress(SpellLayerInstance instance, float seconds)
        {
            float travel = instance.Layer == null ? 0f : instance.Layer.travelSeconds;
            if (travel <= 0f) return 1f;

            float delay = instance.Layer.travelDelay;
            float u = (seconds - instance.StartSeconds - delay) / travel;
            return u < 0f ? 0f : u > 1f ? 1f : u;
        }

        // ---- fan-out -------------------------------------------------------------

        // ONE INSTANCE PER STRUCK TARGET, OR ONE FOR THE CAST, decided by the
        // placement word and by nothing else. That is the property that
        // reproduces Cinderfault's one fault plus N plumes with no spell id
        // anywhere: `formation` is cast-level and `target` is not.
        //
        // A PROJECTILE FANS OUT WHATEVER ITS PLACE SAYS, and it is the only
        // exception in the table. `caster` says where the box STARTS, and a
        // spell that travels to three enemies needs three boxes leaving the
        // same point -- which is exactly what the shipped code does today,
        // once per struck target.
        private static List<SpellLayerInstance> FanOut(SpellLayer[] layers, int targets)
        {
            var built = new List<SpellLayerInstance>();
            var byId = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer == null) continue;
                if (!string.IsNullOrWhiteSpace(layer.id)) byId[layer.id.Trim()] = i;
            }

            // ONE LOOKUP CLOSURE, built once rather than once per layer --
            // SpellLayer.IsPerTarget takes `idOf` this way so
            // SpellPresentation.HasPerTargetArt's one-shot content check can
            // hand it a plain array scan instead, without either caller
            // needing to know the other exists.
            int IdOf(string id) => byId.TryGetValue(id, out int at) ? at : -1;

            var perTarget = new bool[layers.Length];
            for (int i = 0; i < layers.Length; i++) perTarget[i] = SpellLayer.IsPerTarget(layers, i, IdOf);

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer == null) continue;

                int count = perTarget[i] ? targets : 1;
                for (int t = 0; t < count; t++)
                {
                    built.Add(new SpellLayerInstance
                    {
                        Layer = layer,
                        RenderKind = layer.Render,
                        PlaceKind = layer.Place,
                        AtKind = layer.At,
                        UntilKind = layer.Until,
                        FacingKind = layer.Facing,
                        SortKind = layer.Sort,
                        AlignKind = layer.Align,
                        LayerIndex = i,
                        TargetIndex = perTarget[i] ? t : -1,
                        SourceInstance = -1,
                    });
                }
            }

            LinkFollowers(built, byId);
            return built;
        }

        private static void LinkFollowers(List<SpellLayerInstance> built, Dictionary<string, int> byId)
        {
            foreach (var instance in built)
            {
                string source = instance.Layer.FollowsLayerId;
                if (string.IsNullOrWhiteSpace(source)) continue;
                if (!byId.TryGetValue(source, out int sourceLayer)) continue;

                for (int i = 0; i < built.Count; i++)
                {
                    if (built[i].LayerIndex != sourceLayer) continue;

                    // A per-target follower rides the source instance standing
                    // on its own target; a cast-level one rides the only source
                    // there is.
                    if (instance.TargetIndex >= 0 && built[i].TargetIndex != instance.TargetIndex) continue;

                    instance.SourceInstance = i;
                    break;
                }
            }
        }

        // ---- times ---------------------------------------------------------------

        private static void AssignTimes(List<SpellLayerInstance> instances, SpellPerformance performance,
            Func<string, int> frameCountOf)
        {
            foreach (var instance in instances)
            {
                instance.StartSeconds = StartOf(instance.Layer, performance);
                instance.FadeSeconds = Math.Min(Math.Max(0f, instance.Layer.fade),
                    SpellLayerRules.MaxFadeSeconds);
            }

            // ENDS AFTER STARTS, because a follower's end is its source's and
            // the source's own end needs its start first. One pass over the
            // list in instance order is enough: LinkFollowers points at an
            // EARLIER index only when the source layer was declared earlier,
            // which is not guaranteed -- so followers are resolved by walking
            // the chain rather than by relying on the order.
            foreach (var instance in instances) instance.EndSeconds = EndOf(instance, instances, frameCountOf, 0);
        }

        private static float StartOf(SpellLayer layer, SpellPerformance performance)
        {
            float at;
            switch (layer.At)
            {
                case SpellCue.Arrival: at = performance.ArrivalSeconds; break;
                case SpellCue.Hit: at = performance.HitCueSeconds; break;
                default: at = 0f; break;
            }

            float start = at + layer.offset;
            return start < 0f ? 0f : start;
        }

        // THE ONE PLACE THAT SAYS WHEN A LAYER STOPS. Six kinds of ending in
        // one function, because spreading them across `until`, `seconds`,
        // `fade` and a renderer is how two of the pilot's own five layers came
        // to read as drawing for the rest of the fight.
        private static float EndOf(SpellLayerInstance instance, List<SpellLayerInstance> all,
            Func<string, int> frameCountOf, int depth)
        {
            var layer = instance.Layer;

            // AN EMITTER LIVES window + lifeMax, NOT window. The brief asks for
            // exactly this -- emission stops while the particles it already
            // threw finish -- and it is the difference between a shed that
            // stops at the target and a shed whose last drops vanish in mid-air.
            // Finite by construction, which is why the unbounded-lifetime rule
            // does not have to police this kind.
            if (layer.Render == SpellRender.Emitter)
            {
                var emitter = layer.emitter ?? new SpellEmitter();
                return instance.StartSeconds + emitter.window + emitter.lifeMax;
            }

            // A FOLLOWER ENDS WITH THE LAYER IT FOLLOWS, AND NEVER AFTER IT.
            // Its own fade then runs from there, capped like every other, so
            // the wake cannot outlive the core by more than a tenth of a second
            // at its authored fade.
            if (instance.SourceInstance >= 0 && depth <= all.Count)
            {
                var source = all[instance.SourceInstance];
                return EndOf(source, all, frameCountOf, depth + 1);
            }

            float authored = layer.seconds;
            float derived = DerivedSeconds(layer, frameCountOf);
            float own = authored > 0f ? authored : derived;

            // A TRAVELLING LAYER ENDS AT ITS ARRIVAL, whatever `until` says --
            // `loop` describes what the sheet does WHILE it crosses the stage,
            // not how long it lives. An authored seconds may extend it past the
            // arrival, and that clause is not a nicety: it is what keeps five
            // shipped travelling spells from being cut off, because today a
            // travelling sheet keeps playing its remaining frames at the target
            // until the sequence runs out.
            if (layer.Travels)
            {
                float arrival = instance.StartSeconds + layer.travelDelay + layer.travelSeconds;
                return own > 0f ? Math.Max(arrival, instance.StartSeconds + own) : arrival;
            }

            return instance.StartSeconds + own;
        }

        // The length a folder plays at an authored rate, counting from the
        // layer's own start frame. `startFrame` is 1-based, so a 2 skips one
        // frame of nine and eight are left.
        private static float DerivedSeconds(SpellLayer layer, Func<string, int> frameCountOf)
        {
            if (layer.fps <= 0f) return 0f;

            int frames = FramesOf(frameCountOf, layer.path);
            int skipped = layer.startFrame > 1 ? layer.startFrame - 1 : 0;
            int playable = frames - skipped;
            return playable <= 0 ? 0f : playable / layer.fps;
        }

        // THE FIRST LAYER IN AUTHORED ORDER WITH travelSeconds > 0, and "first"
        // never has to break a tie content can produce: SpellLayerRules refuses
        // a second traveller in any cast that schedules `arrival`. A field
        // naming the arrival layer would be a field with one possible value.
        private static float ArrivalOf(SpellLayer[] layers, float hitCueSeconds)
        {
            foreach (var layer in layers)
            {
                if (layer == null || !layer.Travels) continue;

                // The traveller's own start, without consulting arrival --
                // which is what this is computing. A layer that both travels
                // and schedules off arrival would be defining itself, so it
                // reads as release, which is what an author who wrote that
                // meant.
                float start = layer.At == SpellCue.Hit ? hitCueSeconds : 0f;
                float arrival = start + layer.offset + layer.travelDelay + layer.travelSeconds;
                return arrival < 0f ? 0f : arrival;
            }

            return 0f;
        }

        private static float LegacyHitCue(SpellPresentation vfx, Func<string, int> frameCountOf)
        {
            if (!vfx.HasAnimation) return 0f;

            // The identical expression ImpactDelayFor used before this existed,
            // including its fallback: a folder that failed to load reports 0
            // frames and ImpactFraction answers 0.5, so the blow lands halfway
            // through an animation that is not playing. That is an accident
            // rather than a design -- and it is preserved on purpose here,
            // because removing it would retime a shipped spell whose art went
            // momentarily unreadable. A version-1 spell authors its cue in
            // seconds and has no such accident to inherit.
            int frames = FramesOf(frameCountOf, vfx.path);
            return vfx.seconds * Session.CombatBeat.ImpactFraction(vfx.impactFrame, frames);
        }

        private static float ClearedOf(List<SpellLayerInstance> instances, float hitCueSeconds)
        {
            float last = hitCueSeconds;
            foreach (var instance in instances)
            {
                if (instance.ClearedSeconds > last) last = instance.ClearedSeconds;
            }

            return last;
        }

        private static SpellSchedule BuildSchedule(List<SpellLayerInstance> instances, float hitCueSeconds)
        {
            var events = new List<SpellEvent>(instances.Count * 2 + 1)
            {
                new SpellEvent(SpellEventKind.HitCue, -1, hitCueSeconds),
            };

            for (int i = 0; i < instances.Count; i++)
            {
                events.Add(new SpellEvent(SpellEventKind.LayerStart, i, instances[i].StartSeconds));

                // CLEARED, NOT ENDED, and the difference is the whole of an
                // authored `fade`. LayerEnd is what hands the renderer back, so
                // firing it at EndSeconds reclaimed the member on the very tick
                // the alpha ramp was supposed to begin -- SpellFrameCursor's
                // fade branch was unreachable from a running fight, and the
                // pilot's wake cut instead of fading. The layer's LIFETIME is
                // still EndSeconds, which is what the cursor measures its ramp
                // from; this is only when the picture is finally taken away.
                //
                // ClearedSeconds is EndSeconds when nothing fades, so every
                // spell that authors no fade -- which is every pre-layer block
                // through the adapter -- is untouched to the float.
                events.Add(new SpellEvent(SpellEventKind.LayerEnd, i, instances[i].ClearedSeconds));
            }

            return new SpellSchedule(events);
        }

        private static int FramesOf(Func<string, int> frameCountOf, string path)
        {
            if (frameCountOf == null || string.IsNullOrWhiteSpace(path)) return 0;
            int count = frameCountOf(path);
            return count < 0 ? 0 : count;
        }
    }
}
