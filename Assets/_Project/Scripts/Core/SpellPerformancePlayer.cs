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

        private sealed class Cast
        {
            internal int Generation;
            internal bool Live;
            internal SpellPerformance Performance;

            // Engine seconds, from the module's own clock.
            internal float StartedAt;

            // Authored seconds already delivered. Starts below zero so a cue at
            // zero -- which every melee beat has -- lands in the first window.
            internal float Cursor;

            // Per instance: which member of its band this instance holds, or
            // -1. An array rather than a dictionary because a cast's instance
            // count is known the moment it begins and never changes.
            internal int[] Members;

            // Per instance: the emitter's own seed, and the particle members it
            // holds. Null for every instance that is not an emitter, which is
            // most of them.
            internal int[] Seeds;
            internal List<int>[] Drops;
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

        public CastHandle Begin(SpellPerformance performance)
        {
            if (performance == null || performance.Instances == null) return CastHandle.None;

            int slot = FreeCastSlot();
            var cast = _casts[slot];

            _castsBegun++;
            cast.Generation++;
            if (cast.Generation <= 0) cast.Generation = 1;
            cast.Live = true;
            cast.Performance = performance;
            cast.StartedAt = Now();
            cast.Cursor = SpellSchedule.BeforeAnything;

            int count = performance.Instances.Count;
            cast.Members = new int[count];
            cast.Seeds = new int[count];
            cast.Drops = new List<int>[count];

            for (int i = 0; i < count; i++)
            {
                cast.Members[i] = -1;
                var emitter = performance.Instances[i].Layer.emitter;
                cast.Seeds[i] = emitter != null && emitter.seed != 0
                    ? emitter.seed
                    : _castsBegun * 7919 + i;
            }

            // THE OPENING WINDOW IS DELIVERED HERE, not on the next Update.
            // What this replaces drew on the frame the beat opened, and a beat
            // whose art appeared one frame later would be a retiming of every
            // spell in the game for no stated reason.
            Advance(slot, 0f);
            Paint(slot, 0f);

            return new CastHandle(slot, cast.Generation);
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
                // deadlines -- through FightBeatPlayer's own inverse, so the
                // beat's speed multiplier keeps having exactly one reader.
                float seconds = FightBeatPlayer.Unscaled(now - _casts[slot].StartedAt);
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

            return cast.Performance.Instances[instance].Layer.Sort == SpellSort.Ground;
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

            schedule.Crossed(cast.Cursor, seconds, _crossed);
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

                    // THE CUE IS DISPATCHED AND NOTHING HERE ACTS ON IT. The
                    // beat player owns the impact block and always has; what
                    // the module owes is that the instant exists in ONE place,
                    // so a layer scheduled `at: hit` and the blow itself cannot
                    // drift apart. A renderer that could not be obtained
                    // removes a picture and never the cue.
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

            // An emitter obtains no sprite renderer. Its drops are taken one at
            // a time as they are born and given back as they die, which is what
            // lets emission stop while the particles it already threw finish.
            if (layer.Render == SpellRender.Emitter)
            {
                cast.Drops[index] = cast.Drops[index] ?? new List<int>();
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
            bool ground = instance.Layer.Sort == SpellSort.Ground;
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

                if (instance.Layer.Render == SpellRender.Emitter)
                {
                    PaintEmitter(cast, slot, i, instance, seconds);
                    continue;
                }

                int member = cast.Members[i];
                if (member < 0) continue;

                var pool = instance.Layer.Sort == SpellSort.Ground ? groundRenderers : effectRenderers;
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

                renderer.SetFacing(instance.Layer.Facing == SpellFacing.None ? 1f : instance.Facing);

                var next = sample.Next >= 0 && sample.Next < frameCount ? frames[sample.Next] : null;
                var at = PositionOf(cast, instance, seconds);

                renderer.Show(frames[sample.Index], next, sample.Blend, sample.Alpha,
                    new Vector2(at.X, at.Y), new Vector2(instance.Box.X, instance.Box.Y));
            }
        }

        // WHERE A LAYER IS NOW, which for a follower is wherever its source is.
        // The one evaluation of an eased flight in the program is
        // SpellPerformance.PositionOf, and a follower reading it for its source
        // rather than keeping a path of its own is what keeps the wake glued to
        // the core through a retune of either.
        private static UiVec PositionOf(Cast cast, SpellLayerInstance instance, float seconds)
        {
            var source = instance;
            int guard = 0;

            while (source.SourceInstance >= 0 && guard++ <= cast.Performance.Instances.Count)
            {
                source = cast.Performance.Instances[source.SourceInstance];
            }

            var at = SpellPerformance.PositionOf(source, seconds);
            return at + new UiVec(instance.Layer.dx, instance.Layer.dy);
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
            while (drops.Count < total)
            {
                int particle = drops.Count;
                float birth = SpellEmitterSim.BirthOf(spec, particle, instance.StartSeconds);
                if (birth > seconds) break;

                drops.Add(TakeDrop(slot, index, particle));
            }

            for (int i = 0; i < drops.Count; i++)
            {
                float birth = SpellEmitterSim.BirthOf(spec, i, instance.StartSeconds);
                var source = SourceOf(cast, instance);

                var p0 = SpellPerformance.PositionOf(source, birth)
                         + new UiVec(spec.sourceDx * SignOf(instance), spec.sourceDy);
                var v0 = SpellEmitterSim.LaunchOf(spec, seed, i, instance.Facing)
                         + SpellPerformance.VelocityOf(source, birth) * spec.inherit;

                var drop = SpellEmitterSim.At(spec, seed, i, p0, v0, seconds - birth, frameCount);

                int member = drops[i];
                if (member < 0) continue;

                if (!drop.Alive)
                {
                    GiveBackDrop(member);
                    drops[i] = -1;
                    continue;
                }

                particleRenderer.Show(member, frames[drop.Frame],
                    new Vector2(drop.Position.X, drop.Position.Y),
                    ParticleSize * drop.Scale, drop.Alpha, drop.Rotation);
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

        private static float SignOf(SpellLayerInstance instance) => instance.Facing < 0f ? -1f : 1f;

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
                Seeds = Array.Empty<int>(),
                Drops = Array.Empty<List<int>>(),
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
