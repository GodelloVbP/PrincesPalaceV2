using System;
using System.Collections.Generic;
using UnityEngine;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Content;

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

    // WHO OWNS WHAT WHILE A SPELL IS DRAWING, and when it stops.
    //
    // OWNERSHIP IS THE CAST, not the stage slot. What this replaces handed out
    // pool members by POSITION IN THE STRUCK WALK, so every cast started again
    // at member 0 and a second cast on a live member restarted it -- there is a
    // comment in the renderer defending that restart, and it is the behaviour
    // the brief asks to remove. Here a cast records every renderer it obtained
    // against its own handle and the free list only ever hands out members
    // nothing owns, so two casts on one target coexist.
    //
    // THE MODULE OWNS ITS OWN CLOCK and ticks from its own Update, rather than
    // borrowing the fight controller's frame. The controller gains no
    // responsibility for something it has no opinion about, and Tick(float)
    // stays public so a test can drive it directly.
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
        }

        private readonly List<Cast> _casts = new List<Cast>();
        private readonly List<SpellEvent> _crossed = new List<SpellEvent>();

        // Owner per pool member, as a cast slot index; -1 is free. Not a bool:
        // "who has it" is what makes a release able to refuse to hand back a
        // member a later cast already took.
        private int[] _effectOwners;
        private int[] _groundOwners;

        // How many casts drew nothing because their band was full. Read by the
        // test that proves the hit cue still fires when the pictures cannot be
        // obtained -- an overflow is a missing picture and nothing else.
        public int DroppedLayers { get; private set; }

        // ---- the interface combat calls ------------------------------------------

        public CastHandle Begin(SpellPerformance performance)
        {
            if (performance == null || performance.Instances == null) return CastHandle.None;

            int slot = FreeCastSlot();
            var cast = _casts[slot];

            cast.Generation++;
            if (cast.Generation <= 0) cast.Generation = 1;
            cast.Live = true;
            cast.Performance = performance;
            cast.StartedAt = SpellVfxPlayer.Now();
            cast.Cursor = SpellSchedule.BeforeAnything;
            cast.Members = new int[performance.Instances.Count];
            for (int i = 0; i < cast.Members.Length; i++) cast.Members[i] = -1;

            // THE OPENING WINDOW IS DELIVERED HERE, not on the next Update.
            // What this replaces drew on the frame the beat opened, and a beat
            // whose art appeared one frame later would be a retiming of every
            // spell in the game for no stated reason.
            Advance(slot, 0f);

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
                Advance(slot, FightBeatPlayer.Unscaled(now - _casts[slot].StartedAt));
            }
        }

        // A VISUAL-ONLY STOP. The cast's gameplay resolved before any of this
        // ran, so there is no combat action here to cancel and no method that
        // could be mistaken for one.
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

        // ---- the tick ------------------------------------------------------------

        private void Update()
        {
            Tick(SpellVfxPlayer.Now());
        }

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
                    // the module owes is that the instant exists in one place,
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
                cast.Live = false;
                cast.Performance = null;
            }
        }

        private void Open(Cast cast, int slot, int index)
        {
            var instance = cast.Performance.Instances[index];
            var layer = instance.Layer;

            // An emitter has no sprite renderer to obtain. Its particles are
            // M4's; until then it owns nothing and costs nothing, which is the
            // right shape for a layer kind whose whole ending is derived.
            if (layer.Render == SpellRender.Emitter) return;

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
                                 $"renderer for layer '{layer.id}' of this cast; it draws nothing. " +
                                 "The hit cue is unaffected.");
                return;
            }

            owners[member] = slot;
            cast.Members[index] = member;
            Draw(pool[member], instance);
        }

        private void Close(Cast cast, int index)
        {
            int member = cast.Members[index];
            if (member < 0) return;

            var instance = cast.Performance.Instances[index];
            bool ground = instance.Layer.Sort == SpellSort.Ground;
            var owners = ground ? _groundOwners : _effectOwners;
            var pool = ground ? groundRenderers : effectRenderers;

            cast.Members[index] = -1;
            if (member >= owners.Length) return;

            owners[member] = -1;
            if (pool[member] != null) pool[member].StopImmediately();
        }

        private void Release(int slot)
        {
            var cast = _casts[slot];
            if (!cast.Live) return;

            for (int i = 0; i < cast.Members.Length; i++) Close(cast, i);

            cast.Live = false;
            cast.Performance = null;
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
            _casts.Add(new Cast { Members = Array.Empty<int>() });
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
        }

        private static int Length(SpellVfxPlayer[] pool) => pool == null ? 0 : pool.Length;

        private static int[] Free(int count)
        {
            var owners = new int[count];
            for (int i = 0; i < count; i++) owners[i] = -1;
            return owners;
        }

        // ---- handing an instance to the renderer ---------------------------------

        // THE ONE TRANSLATION BACK INTO THE RENDERER'S FRAME-INDEX VOCABULARY.
        //
        // The renderer still owns frame playback, and its flight is expressed
        // as a departure index and an arrival index into the sequence rather
        // than as two durations. Both are recoverable exactly: a sequence's
        // per-frame time is its length over its frame count, which is the same
        // arithmetic the adapter inverted to produce travelDelay and
        // travelSeconds -- so a pre-layer block hands back the identical pair
        // of indices it was authored with, and a shipped spell does not
        // retime by a frame.
        private void Draw(SpellVfxPlayer renderer, SpellLayerInstance instance)
        {
            var layer = instance.Layer;

            renderer.SetFacing(layer.Facing == SpellFacing.None ? 1f : instance.Facing);

            var box = new Vector2(instance.Box.X, instance.Box.Y);
            var from = new Vector2(instance.From.X, instance.From.Y);
            var to = new Vector2(instance.To.X, instance.To.Y);

            float length = instance.EndSeconds - instance.StartSeconds;
            if (length <= 0f) return;

            if (!layer.Travels)
            {
                renderer.PlayAt(layer.path, length, to, box);
                return;
            }

            int frames = renderer.Frames(layer.path)?.Length ?? 0;
            if (frames <= 0) return;

            float perFrame = length / frames;
            if (perFrame <= 0f) return;

            int depart = Mathf.RoundToInt(layer.travelDelay / perFrame);
            int arrive = Mathf.RoundToInt((layer.travelDelay + layer.travelSeconds) / perFrame);

            renderer.PlayFrom(layer.path, length, from, to, box, depart, arrive);
        }
    }
}
