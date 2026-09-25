using System;
using System.Collections.Generic;
using UnityEngine;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // A LIVE CAST, addressable without being able to address a dead one.
    //
    // An int alone would let a handle kept past the end of its cast reach
    // whatever took its slot next -- a cancel aimed at a spell that finished
    // three beats ago tearing down the one drawing now. The generation counter
    // is what makes a stale handle simply not match.
    public readonly struct CastHandle
    {
        internal readonly int Slot;
        internal readonly int Generation;

        internal CastHandle(int slot, int generation)
        {
            Slot = slot;
            Generation = generation;
        }

        // A handle that never addressed anything. Zero is not a generation any
        // cast is given, so `default` is safely nothing.
        public bool IsLive => Generation > 0;

        public static CastHandle None => default;
    }

    // WHO OWNS WHAT WHILE A SPELL IS DRAWING, WHEN IT STOPS, AND WHAT CLOCK
    // BOTH ANSWERS ARE MEASURED AGAINST.
    //
    // OWNERSHIP IS THE CAST, not the stage slot. What this replaces handed out
    // pool members by POSITION IN THE STRUCK WALK, so every cast started again
    // at member 0 and a second cast on a live member restarted it -- there is a
    // comment in the old renderer defending that restart, and it is the
    // behaviour the brief asks to remove. A cast records every renderer and
    // every particle it obtains against its own handle, and the free lists only
    // ever hand out members nothing owns.
    //
    // ONE CLOCK, AND IT IS Time.time. The renderer used to read
    // realtimeSinceStartup while the beat waited on WaitForSeconds, which is
    // scaled engine time -- so opening the system menu mid-cast froze the beat
    // and FAST-FORWARDED the spell, and a captured recording sampled the spell
    // at wall speed while frames advanced at a fixed step. Time.time is stopped
    // by timeScale and advanced by captureFramerate, so the beat and the spell
    // freeze and resume on one mechanism.
    //
    // ONE CONVERSION, AND IT IS FightBeatPlayer.Scaled. A schedule holds
    // AUTHORED seconds and the clock holds engine ones; the multiplier behind
    // that division is read in exactly one place, so a spell and its beat
    // cannot be 60x apart under a test speed-up.
    //
    // WHAT IT DOES NOT DO, and the line is deliberate: it never applies damage,
    // never decides who was hit, and exposes no method that could be mistaken
    // for cancelling a combat action. Cancel is a VISUAL-ONLY stop -- by the
    // time anything here runs, the whole round has already resolved and the
    // beat is a recording being replayed.
    public class SpellPerformancePlayer : MonoBehaviour
    {
        // The two draw bands, both already built. `ground` sits behind every
        // figure because a fault opening in the floor has to be an earlier
        // sibling than the figures standing on it; `effects` sits over the HUD
        // and under the damage numbers.
        [SerializeField] internal SpellVfxPlayer[] effectRenderers;
        [SerializeField] internal SpellVfxPlayer[] groundRenderers;
        [SerializeField] internal SpellParticleRenderer particleRenderer;

        // THE CLOCK PLAYBACK IS MEASURED AGAINST, and the only reason it is a
        // seam at all: null in the game, where it reads Time.time.
        //
        // A cast's whole budget is FightBeatPlayer.Scaled(seconds), and a test
        // runs the fight at BeatSpeedMultiplier 60 -- so a 0.78s cinderfault is
        // 13 MILLISECONDS end to end. A batchmode frame right after a scene load
        // was measured at 24ms and 57ms. A test that casts, waits one frame and
        // then counts the layers on screen is therefore counting AFTER the
        // effect tore itself down, and which of them it still catches depends on
        // how long that one frame happened to take. That is AUDIT.md #61: three
        // runs on an unchanged tree, a different test failing each time.
        //
        // Holding this still turns "what is drawn at the impact instant" from a
        // race into a question with an answer -- and advancing it past the end
        // makes the cleanup half assertable, which waiting on real time never
        // could at 13ms of resolution.
        //
        // Public rather than internal because InternalsVisibleTo names the
        // EDITOR assembly only; a PlayMode test reaches this or reaches nothing.
        // Reset by TestGlobals.ResetAll and policed by GlobalStateLintTests like
        // every other global a test can flip.
        public static Func<float> ClockOverride;

        internal static float Now() => ClockOverride != null ? ClockOverride() : Time.time;

        // T4's test seam (docs/archive/PLAN_BATTLE_SPEED.md), not in the plan's own
        // stated Seams line ranges (98-105,152-164,196-208) because it
        // touches Advance instead, which sits outside them -- recorded as a
        // deviation in the plan's own Deviations section. HitCue is "the one
        // authoritative impact cue, delivered exactly once per cast"
        // (SpellEventKind.HitCue's own header), but Advance's switch below
        // has never acted on it: the beat player owns the impact block
        // through its OWN independent wait, entirely disconnected from this
        // module's schedule crossing. Proving that the crossing THIS module
        // computes happens exactly once, and only inside the right window,
        // needs a hook nothing else reads. Public rather than internal for
        // the same reason ClockOverride above is.
        public Action<CastHandle> HitCueCrossedForTest;

        // A drop born by an emitter: which particle-pool member it drew (or
        // -1, when the pool was exhausted) plus its two closed-form birth
        // inputs. Position-at-birth and velocity-at-birth are pure functions
        // of (spec, seed, source, birth) -- they never change once the drop
        // exists, so PaintEmitter computes them once, here, at TakeDrop time
        // rather than re-deriving them (a follower-chain PositionOf read
        // among them) for every live drop on every frame of its life.
        private struct Drop
        {
            internal int Member;
            internal UiVec P0;
            internal UiVec V0;
        }

        private sealed class Cast
        {
            internal int Generation;
            internal bool Live;
            internal SpellPerformance Performance;

            // Engine seconds, from the module's own clock.
            internal float StartedAt;

            // Contract 3: FightBeatPlayer.Pace AT THE INSTANT this cast
            // began, captured once and never re-read. A live cast keeps
            // ageing on the pace it was born at even if the player steps the
            // preset mid-cast in either direction -- reading the live Pace
            // in Tick instead would make an in-flight cast's age jump the
            // moment the setting changed, tearing its cue windows out from
            // under whichever ones had not fired yet.
            internal float PaceAtStart;

            // Authored seconds already delivered. Starts below zero so a cue at
            // zero -- which every melee beat has -- lands in the first window.
            internal float Cursor;

            // Where SpellSchedule.Crossed left off scanning its own events
            // array, so the NEXT call resumes there instead of rescanning
            // from 0 -- safe because Cursor above only ever grows across a
            // cast's life. Reset to 0 at Begin, alongside Cursor itself.
            internal int ScheduleCursor;

            // Per instance: which member of its band this instance holds, or
            // -1. An array rather than a dictionary because a cast's instance
            // count is known the moment it begins and never changes.
            internal int[] Members;

            // Per instance: the ultimate root of its SourceInstance chain --
            // itself, for an instance that follows nothing. Resolved once
            // here rather than walked per frame per call; see the local
            // PositionOf below.
            internal int[] RootSource;

            // Per instance: the emitter's own seed, and the particle members it
            // holds. Null for every instance that is not an emitter, which is
            // most of them.
            internal int[] Seeds;
            internal List<Drop>[] Drops;
        }

        private readonly List<Cast> _casts = new List<Cast>();
        private readonly List<SpellEvent> _crossed = new List<SpellEvent>();

        // Owner per pool member, as a cast slot index; -1 is free. Not a bool:
        // "who has it" is what lets a release refuse to hand back a member a
        // later cast already took.
        private int[] _effectOwners;
        private int[] _groundOwners;
        private int[] _dropOwners;

        // Which particle index of which instance a drop member is drawing, so a
        // tick can advance it without searching. Parallel to _dropOwners
        // because they are written and cleared together in the same two places.
        private int[] _dropInstance;
        private int[] _dropIndex;

        // Counts up whenever a layer or a drop could not be obtained. Read by
        // the test that proves the hit cue still fires when the pictures cannot
        // be: an overflow is a missing picture and nothing else.
        public int DroppedLayers { get; private set; }
        public int DroppedParticles { get; private set; }

        // A cast's generation counter, so an emitter that authored no seed still
        // differs between two casts in one fight -- right for playing, and wrong
        // for a picture, which is why the field exists.
        private int _castsBegun;

        // ---- the interface combat calls ------------------------------------------

        // THE MOST RECENTLY BEGUN CAST'S OWN HANDLE. ForceFirstAction drives a
        // cast through the normal beat flow, and PlaySpellVfx's return value
        // is swallowed by the beat player long before any test sees it -- so
        // a PlayMode test recording THAT cast, rather than one it began
        // itself through PlaySpellVfxForTest, has no other way to name which
        // cast is its own. Public for the reason every seam here is:
        // InternalsVisibleTo names the EDITOR assembly only, so a PlayMode
        // test reaches this or reaches nothing.
        public CastHandle LastBegunForTest { get; private set; }

        public CastHandle Begin(SpellPerformance performance)
        {
            // NOTHING TO PLAY IS NOT A CAST. Refused here rather than by each
            // caller, which is where it used to live: two of the three callers
            // pre-filtered an empty performance and PlayContactFx did not, and
            // the one that did not was one presentation edit away from Paint
            // dereferencing the null Performance that Advance's own Release
            // leaves behind.
            if (performance?.Instances == null || performance.Instances.Count == 0) return CastHandle.None;

            // Contract 3: refused and logged, the same posture missing art
            // gets. A cast born at pace <= 0 has no honest age to convert --
            // Tick's (now - StartedAt) * PaceAtStart would sit at zero for
            // its entire life, so every cue would either fire on the same
            // frame or never, depending on where BeforeAnything happened to
            // land.
            float pace = FightBeatPlayer.Pace;
            if (pace <= 0f)
            {
                Debug.LogWarning($"[SpellPerformancePlayer] Refused to begin a cast: FightBeatPlayer.Pace " +
                                  $"was {pace}, and a cast cannot age against a non-positive pace.");
                return CastHandle.None;
            }

            int slot = FreeCastSlot();
            var cast = _casts[slot];

            _castsBegun++;
            cast.Generation++;
            if (cast.Generation <= 0) cast.Generation = 1;
            cast.Live = true;
            cast.Performance = performance;
            cast.StartedAt = Now();
            cast.PaceAtStart = pace;
            cast.Cursor = SpellSchedule.BeforeAnything;
            cast.ScheduleCursor = 0;

            int count = performance.Instances.Count;
            cast.Members = new int[count];
            cast.RootSource = new int[count];
            cast.Seeds = new int[count];
            cast.Drops = new List<Drop>[count];

            for (int i = 0; i < count; i++)
            {
                cast.Members[i] = -1;

                // THE SAME WALK THE LOCAL PositionOf USED TO MAKE PER FRAME,
                // paid once per instance here instead: an instance's ultimate
                // SourceInstance root, itself when it follows nothing. Bounded
                // by `count` the same way that walk was, so a resolver run on
                // content the build-time validator never saw cannot hang on a
                // cycle either.
                int root = i;
                int guard = 0;
                while (performance.Instances[root].SourceInstance >= 0 && guard++ <= count)
                {
                    root = performance.Instances[root].SourceInstance;
                }

                cast.RootSource[i] = root;

                var emitter = performance.Instances[i].Layer.emitter;
                cast.Seeds[i] = emitter != null && emitter.seed != 0
                    ? emitter.seed
                    : _castsBegun * 7919 + i;
            }

            // THE OPENING WINDOW IS DELIVERED HERE, not on the next Update.
            // What this replaces drew on the frame the beat opened, and a beat
            // whose art appeared one frame later would be a retiming of every
            // spell in the game for no stated reason.
            //
            // GUARDED THE WAY Tick IS: Advance releases a cast that has nothing
            // left to draw or fire (every instance unplaced, a zero-length
            // schedule), and a released cast has no Performance to paint. It
            // also has no handle worth returning -- IsLive would already say
            // false, and None says so without a slot a later cast may reuse.
            Advance(slot, 0f);
            if (!cast.Live) return CastHandle.None;
            Paint(slot, 0f);

            var handle = new CastHandle(slot, cast.Generation);
            LastBegunForTest = handle;
            return handle;
        }

        // Ticked from Update with the module's own clock, and from a test with
        // whatever clock it wants to hold.
        public void Tick(float now)
        {
            for (int slot = 0; slot < _casts.Count; slot++)
            {
                if (!_casts[slot].Live) continue;

                // The schedule is in AUTHORED seconds and the clock is in
                // engine ones, so the window is converted rather than the
                // deadlines -- but through the CAST'S OWN PaceAtStart, not
                // FightBeatPlayer's live Pace product (contract 3): this
                // cast must keep ageing on the pace it was born at even if
                // the player steps the preset while it is still drawing.
                float seconds = (now - _casts[slot].StartedAt) * _casts[slot].PaceAtStart;
                Advance(slot, seconds);
                if (_casts[slot].Live) Paint(slot, seconds);
            }
        }

        // A VISUAL-ONLY STOP. The cast's gameplay resolved before any of this
        // ran, so there is no combat action here to cancel and no method that
        // could be mistaken for one. Living particles go with it, because a
        // visual-only stop means stop.
        public void Cancel(CastHandle handle)
        {
            if (!handle.IsLive || handle.Slot < 0 || handle.Slot >= _casts.Count) return;
            if (_casts[handle.Slot].Generation != handle.Generation) return;

            Release(handle.Slot);
        }

        public void CancelAll()
        {
            for (int slot = 0; slot < _casts.Count; slot++) Release(slot);
        }

        // The one number the beat player reads off a presentation. A method on
        // the module rather than a field read at the call site, so the day a
        // cue stops being a plain number the beat player does not have to learn
        // about it.
        public float HitCueSeconds(SpellPerformance performance) =>
            performance == null ? 0f : performance.HitCueSeconds;

        // Whether this handle still owns anything. What a test asks instead of
        // reaching into the pool.
        public bool IsLive(CastHandle handle) =>
            handle.IsLive && handle.Slot >= 0 && handle.Slot < _casts.Count &&
            _casts[handle.Slot].Generation == handle.Generation && _casts[handle.Slot].Live;

        // WHETHER THIS CAST'S OWN CLOCK -- Cursor, in authored seconds -- has
        // delivered `seconds` of playback. A cast that already released has
        // necessarily reached every value there is: Advance only releases a
        // cast once Cursor has crossed its performance's ClearedSeconds and
        // nothing is still drawing, so "not live" reads as "reached", not as
        // "unknown".
        //
        // WHY A CALLER CANNOT WATCH CAPTURE FRAMES INSTEAD OF ASKING THIS.
        // Cursor ages on Now() (Time.time, paced), and a beat's own hit-stop
        // hold freezes it by dropping timeScale to sell an impact --
        // Update, and with it a capture frame, keeps running every tick
        // regardless of timeScale. A recording driven off a frame count
        // computed from ClearedSeconds is measuring the wrong clock the
        // moment a beat holds; this is the clock a PlayMode capture polls
        // instead (see SpellRuntimeCaptureTests).
        public bool HasReached(CastHandle handle, float seconds) =>
            !IsLive(handle) || _casts[handle.Slot].Cursor >= seconds;

        // Which member of which band an instance of a live cast holds, or -1.
        // The seam the overlap tests read: "these two casts hold different
        // members" is only answerable from outside if the answer is askable.
        public int MemberFor(CastHandle handle, int instance)
        {
            if (!IsLive(handle)) return -1;

            var cast = _casts[handle.Slot];
            return instance < 0 || instance >= cast.Members.Length ? -1 : cast.Members[instance];
        }

        // WHICH BAND AN INSTANCE DREW IN, because a member index only means
        // something inside one. Ground member 0 and effects member 0 are
        // different renderers that share an integer, so "these two casts hold
        // different members" is a question that has to be asked per band or it
        // reports a collision that does not exist.
        public bool DrawsInGroundBand(CastHandle handle, int instance)
        {
            if (!IsLive(handle)) return false;

            var cast = _casts[handle.Slot];
            if (instance < 0 || instance >= cast.Performance.Instances.Count) return false;

            return cast.Performance.Instances[instance].SortKind == SpellSort.Ground;
        }

        // How many particles this cast currently owns. What proves a tail is
        // still alive when the next cast opens.
        public int ParticlesOwnedBy(CastHandle handle)
        {
            if (!IsLive(handle)) return 0;

            int held = 0;
            for (int i = 0; i < _dropOwners.Length; i++)
            {
                if (_dropOwners[i] == handle.Slot) held++;
            }

            return held;
        }

        // Every free member of a band, for the exhaustion test that has to fill
        // one before it can prove the cue survives it.
        public int FreeEffectRenderers => FreeIn(_effectOwners);
        public int FreeGroundRenderers => FreeIn(_groundOwners);

        // ---- the tick ------------------------------------------------------------

        private void Update() => Tick(Now());

        private void OnDisable()
        {
            // An abandoned fight must not leave a spell frozen mid-frame over
            // an empty stage. The rule the renderer and the beat player's own
            // teardown already follow.
            CancelAll();
        }

        private void Advance(int slot, float seconds)
        {
            var cast = _casts[slot];
            var schedule = cast.Performance.Schedule;

            schedule.Crossed(cast.Cursor, seconds, _crossed, ref cast.ScheduleCursor);
            if (seconds > cast.Cursor) cast.Cursor = seconds;

            for (int i = 0; i < _crossed.Count; i++)
            {
                var crossed = _crossed[i];
                switch (crossed.Kind)
                {
                    case SpellEventKind.LayerStart:
                        Open(cast, slot, crossed.Instance);
                        break;

                    case SpellEventKind.LayerEnd:
                        Close(cast, crossed.Instance);
                        break;

                    // THE CUE IS DISPATCHED AND NOTHING HERE ACTS ON IT
                    // PRODUCTION-WISE. The beat player owns the impact block
                    // and always has; what the module owes is that the
                    // instant exists in ONE place, so a layer scheduled
                    // `at: hit` and the blow itself cannot drift apart. A
                    // renderer that could not be obtained removes a picture
                    // and never the cue. HitCueCrossedForTest is the one
                    // exception, and it is test-only.
                    case SpellEventKind.HitCue:
                        HitCueCrossedForTest?.Invoke(new CastHandle(slot, cast.Generation));
                        break;

                    default:
                        break;
                }
            }

            // Nothing left to draw and nothing left to fire.
            if (cast.Cursor >= cast.Performance.ClearedSeconds && !HoldsAnything(cast))
            {
                Release(slot);
            }
        }

        private void Open(Cast cast, int slot, int index)
        {
            var instance = cast.Performance.Instances[index];
            var layer = instance.Layer;

            // NOWHERE TO DRAW IT. Core's PlaceOne already tried and failed
            // (a cast-level layer on a beat that struck no target) and left
            // Box/To/From at zero -- taking a member here would draw a box at
            // the stage origin for the layer's whole lifetime and leave one
            // fewer renderer for whatever the cast's NEXT layer needs.
            if (!instance.Placed) return;

            // An emitter obtains no sprite renderer. Its drops are taken one at
            // a time as they are born and given back as they die, which is what
            // lets emission stop while the particles it already threw finish.
            if (layer.Render == SpellRender.Emitter)
            {
                // REFUSED HERE, NOT IN PaintEmitter's PER-FRAME PATH. A path
                // that resolves to no frames used to reach
                // `frames[drop.Frame]` unguarded once a drop went alive by
                // age alone, throwing out of Tick's loop and starving every
                // later slot in the same tick -- the graceful-degradation
                // posture this project takes on missing art everywhere else
                // (ItemIcons, CharacterPortraits, SceneBuilder.LoadSpriteByKey).
                // Leaving cast.Drops[index] null makes PaintEmitter's
                // existing `if (drops == null) return;` the only check the
                // hot path needs -- nothing new added there.
                var frames = ParticleFrames(layer.emitter);
                if (frames == null || frames.Length == 0)
                {
                    Debug.LogWarning($"SpellPerformancePlayer: emitter layer '{layer.id}' resolved no " +
                                     "frames; it draws nothing. The hit cue is unaffected.");
                    return;
                }

                cast.Drops[index] = cast.Drops[index] ?? new List<Drop>();
                return;
            }

            bool ground = layer.Sort == SpellSort.Ground;
            var owners = ground ? _groundOwners : _effectOwners;
            var pool = ground ? groundRenderers : effectRenderers;
            if (pool == null || owners == null) return;

            int member = -1;
            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i] != -1 || pool[i] == null) continue;
                member = i;
                break;
            }

            // DROPPED, NOT QUEUED, and never allocated for. The house's own
            // precedent is the damage popup pool's fallback, and the priority
            // is stated: the cue is dispatched by the schedule and is never
            // affected, so an exhausted pool costs a picture.
            if (member < 0)
            {
                DroppedLayers++;
                Debug.LogWarning($"SpellPerformancePlayer: no free {(ground ? "ground" : "effects")} " +
                                 $"renderer for layer '{layer.id}'; it draws nothing. The hit cue is " +
                                 "unaffected.");
                return;
            }

            owners[member] = slot;
            cast.Members[index] = member;
        }

        private void Close(Cast cast, int index)
        {
            int member = cast.Members[index];
            cast.Members[index] = -1;

            // An emitter's own end closes the WINDOW; its living drops are
            // already accounted for by the layer's lifetime being
            // window + lifeMax, so there is nothing to hand back here.
            if (member < 0) return;

            var instance = cast.Performance.Instances[index];
            bool ground = instance.SortKind == SpellSort.Ground;
            var owners = ground ? _groundOwners : _effectOwners;
            var pool = ground ? groundRenderers : effectRenderers;

            if (member >= owners.Length) return;

            owners[member] = -1;
            if (pool[member] != null) pool[member].StopImmediately();
        }

        // ---- drawing -------------------------------------------------------------

        private void Paint(int slot, float seconds)
        {
            var cast = _casts[slot];
            var instances = cast.Performance.Instances;

            for (int i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];

                if (instance.RenderKind == SpellRender.Emitter)
                {
                    PaintEmitter(cast, slot, i, instance, seconds);
                    continue;
                }

                int member = cast.Members[i];
                if (member < 0) continue;

                var pool = instance.SortKind == SpellSort.Ground ? groundRenderers : effectRenderers;
                var renderer = pool[member];
                if (renderer == null) continue;

                var frames = renderer.Frames(instance.Layer.path);
                int frameCount = frames?.Length ?? 0;

                var sample = SpellFrameCursor.SampleOf(instance, frameCount, seconds);
                if (!sample.Visible)
                {
                    renderer.StopImmediately();
                    continue;
                }

                renderer.SetFacing(instance.DrawFacing);

                var next = sample.Next >= 0 && sample.Next < frameCount ? frames[sample.Next] : null;
                var at = PositionOf(cast, i, instance, seconds);

                // THE PUNCH MULTIPLIES THE BOX HERE, not in the placement.
                // Core measures the box once at Begin and a scale that varies
                // over the layer's life is a property of the instant, so it
                // arrives with the frame (SpellFrameSample.Scale) and is
                // applied at the one place the box reaches a renderer.
                renderer.Show(frames[sample.Index], next, sample.Blend, sample.Alpha,
                    new Vector2(at.X, at.Y),
                    new Vector2(instance.Box.X * sample.Scale, instance.Box.Y * sample.Scale),
                    instance.Degrees, instance.Layer.glow);
            }
        }

        // WHERE A LAYER IS NOW, which for a follower is wherever its source is.
        // The one evaluation of an eased flight in the program is
        // SpellPerformance.PositionOf, and a follower reading it for its source
        // rather than keeping a path of its own is what keeps the wake glued to
        // the core through a retune of either.
        //
        // `index` READS Cast.RootSource RATHER THAN WALKING THE CHAIN HERE --
        // Begin resolved every instance's ultimate follower-chain root once,
        // so this is one array read per call instead of the same walk paid
        // again on every frame of the layer's life.
        private static UiVec PositionOf(Cast cast, int index, SpellLayerInstance instance, float seconds)
        {
            var source = cast.Performance.Instances[cast.RootSource[index]];

            // dx IS LOCAL, so it mirrors with the cast -- a wake authored 118
            // units behind a rightward core has to sit 118 units behind a
            // leftward one, not 118 units in front of it. Its ContentDoc has
            // always said "local offset from the anchor"; the sign was the half
            // that was missing, and the emitter's own sourceDx already did it.
            var at = SpellPerformance.PositionOf(source, seconds);
            return at + new UiVec(instance.Layer.dx * instance.DrawFacing, instance.Layer.dy);
        }

        private void PaintEmitter(Cast cast, int slot, int index, SpellLayerInstance instance, float seconds)
        {
            var drops = cast.Drops[index];
            if (drops == null || particleRenderer == null) return;

            var spec = instance.Layer.emitter;
            var frames = ParticleFrames(spec);
            int frameCount = frames?.Length ?? 0;
            int seed = cast.Seeds[index];
            int total = SpellEmitterSim.CountOf(spec);

            // BORN AT A TIME, NOT ON A TICK. Particle i's birth is
            // windowStart + i / rate, so a tick spanning six spawn intervals
            // births six particles at six DIFFERENT times, each sampled where
            // the source was at its own instant. That back-dating is what makes
            // a shed look like emission during flight rather than six drops
            // stacked where the step ended.
            //
            // P0/V0 COMPUTED ONCE, HERE, AT BIRTH -- both are pure functions
            // of (spec, seed, source, birth), so a drop's whole life reads
            // the same two values every frame rather than re-deriving them
            // (a follower-chain PositionOf/VelocityOf read among them) on
            // every one of them. See the Drop struct's own header.
            // FITTED TO THE BODY, the whole burst scales about its source:
            // the offset it is born at, how far each drop flies and how big it
            // is drawn. The sim is linear in launch speed and gravity, so
            // scaling the displacement it returns is the same as scaling both
            // -- done here, on the way out, so SpellEmitterSim stays a closed
            // form over authored numbers. 1 for every unfitted emitter.
            float fit = instance.Fit > 0f ? instance.Fit : 1f;

            var source = SourceOf(cast, instance);
            while (drops.Count < total)
            {
                int particle = drops.Count;
                float birth = SpellEmitterSim.BirthOf(spec, particle, instance.StartSeconds);
                if (birth > seconds) break;

                var p0 = SpellPerformance.PositionOf(source, birth)
                         + new UiVec(spec.sourceDx * instance.DrawFacing, spec.sourceDy) * fit;
                var v0 = SpellEmitterSim.LaunchOf(spec, seed, particle, instance.DrawFacing)
                         + SpellPerformance.VelocityOf(source, birth) * spec.inherit;

                drops.Add(new Drop { Member = TakeDrop(slot, index, particle), P0 = p0, V0 = v0 });
            }

            for (int i = 0; i < drops.Count; i++)
            {
                var held = drops[i];
                float birth = SpellEmitterSim.BirthOf(spec, i, instance.StartSeconds);
                var drop = SpellEmitterSim.At(spec, seed, i, held.P0, held.V0, seconds - birth, frameCount);

                int member = held.Member;
                if (member < 0) continue;

                if (!drop.Alive)
                {
                    GiveBackDrop(member);
                    held.Member = -1;
                    drops[i] = held;
                    continue;
                }

                var at = held.P0 + (drop.Position - held.P0) * fit;
                particleRenderer.Show(member, frames[drop.Frame],
                    new Vector2(at.X, at.Y),
                    ParticleSize * drop.Scale * fit, drop.Alpha, drop.Rotation);
            }
        }

        // THE SOURCE A DROP IS BORN OFF. `place: layer:<id>` names it; anything
        // else is its own anchor, which has travelSeconds 0 and therefore a
        // position that is its anchor and a velocity of zero -- so a
        // target-centre burst goes through the identical path rather than
        // through a second branch.
        private static SpellLayerInstance SourceOf(Cast cast, SpellLayerInstance instance)
        {
            if (instance.SourceInstance < 0) return instance;
            return cast.Performance.Instances[instance.SourceInstance];
        }

        // THE BOX ONE DROP IS DRAWN IN, before its own size variation. A
        // reservation rather than a measurement: the atlas's own cells are a
        // few dozen pixels and the emitter's sizeMin/sizeMax are authored
        // against this, so it is the number a spell tunes against.
        private const float ParticleSize = 64f;

        private Sprite[] ParticleFrames(SpellEmitter spec)
        {
            var any = effectRenderers != null && effectRenderers.Length > 0 ? effectRenderers[0] : null;
            return any == null || spec == null ? null : any.Frames(spec.path);
        }

        private int TakeDrop(int slot, int instance, int particle)
        {
            if (_dropOwners == null) return -1;

            for (int i = 0; i < _dropOwners.Length; i++)
            {
                if (_dropOwners[i] != -1) continue;

                _dropOwners[i] = slot;
                _dropInstance[i] = instance;
                _dropIndex[i] = particle;
                return i;
            }

            // AN EMITTER THAT CANNOT GET A PARTICLE DROPS IT AND KEEPS
            // EMITTING, silently. Droplets thin out under pressure before a
            // crown disappears, which is the priority order the overflow policy
            // states -- and neither ever touches the cue.
            DroppedParticles++;
            return -1;
        }

        private void GiveBackDrop(int member)
        {
            if (member < 0 || _dropOwners == null || member >= _dropOwners.Length) return;

            _dropOwners[member] = -1;
            _dropInstance[member] = -1;
            _dropIndex[member] = -1;
            particleRenderer?.Release(member);
        }

        private void Release(int slot)
        {
            var cast = _casts[slot];
            if (!cast.Live) return;

            for (int i = 0; i < cast.Members.Length; i++) Close(cast, i);

            if (_dropOwners != null)
            {
                for (int i = 0; i < _dropOwners.Length; i++)
                {
                    if (_dropOwners[i] == slot) GiveBackDrop(i);
                }
            }

            cast.Live = false;
            cast.Performance = null;
            cast.Drops = null;
        }

        private bool HoldsAnything(Cast cast)
        {
            for (int i = 0; i < cast.Members.Length; i++)
            {
                if (cast.Members[i] >= 0) return true;
            }

            return false;
        }

        private int FreeCastSlot()
        {
            EnsureOwners();

            for (int i = 0; i < _casts.Count; i++)
            {
                if (!_casts[i].Live) return i;
            }

            // GROWN RATHER THAN CAPPED. A cast record is a handful of ints; the
            // reservation that actually bounds anything is the RENDERER pool,
            // and a cast that cannot obtain one still has to exist so its cue
            // and its lifetime are accounted for. Capping here would drop a
            // whole cast to save an allocation the size of its instance list.
            _casts.Add(new Cast
            {
                Members = Array.Empty<int>(),
                RootSource = Array.Empty<int>(),
                Seeds = Array.Empty<int>(),
                Drops = Array.Empty<List<Drop>>(),
            });
            return _casts.Count - 1;
        }

        private void EnsureOwners()
        {
            if (_effectOwners == null || _effectOwners.Length != Length(effectRenderers))
            {
                _effectOwners = Free(Length(effectRenderers));
            }

            if (_groundOwners == null || _groundOwners.Length != Length(groundRenderers))
            {
                _groundOwners = Free(Length(groundRenderers));
            }

            int drops = particleRenderer == null ? 0 : particleRenderer.Capacity;
            if (_dropOwners == null || _dropOwners.Length != drops)
            {
                _dropOwners = Free(drops);
                _dropInstance = Free(drops);
                _dropIndex = Free(drops);
            }
        }

        private static int FreeIn(int[] owners)
        {
            if (owners == null) return 0;

            int free = 0;
            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i] == -1) free++;
            }

            return free;
        }

        private static int Length(SpellVfxPlayer[] pool) => pool == null ? 0 : pool.Length;

        private static int[] Free(int count)
        {
            var owners = new int[count];
            for (int i = 0; i < count; i++) owners[i] = -1;
            return owners;
        }
    }
}
