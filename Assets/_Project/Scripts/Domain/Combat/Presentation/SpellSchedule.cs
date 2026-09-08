using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Combat.Presentation
{
    // What happens, and when. Every time is seconds from the beat opening.
    public enum SpellEventKind
    {
        // The one authoritative impact cue. Delivered exactly once per cast,
        // whatever the art did or failed to do -- a renderer that could not be
        // obtained removes a picture and nothing else.
        HitCue,

        // A layer instance opens: the tick it starts being drawn on.
        LayerStart,

        // A layer instance's own lifetime is over. Its fade, if it authored
        // one, runs from here.
        LayerEnd,
    }

    public readonly struct SpellEvent
    {
        public readonly SpellEventKind Kind;

        // Which instance this is about; -1 for the hit cue, which belongs to
        // the cast rather than to any layer. A layer could not own it, or two
        // layers could disagree about when the blow landed.
        public readonly int Instance;

        public readonly float Seconds;

        public SpellEvent(SpellEventKind kind, int instance, float seconds)
        {
            Kind = kind;
            Instance = instance;
            Seconds = seconds;
        }

        public override string ToString() => $"{Kind}#{Instance}@{Seconds:0.###}";
    }

    // EVERY CUE A CAST WILL EVER FIRE, SORTED ONCE, READ AS A HALF-OPEN WINDOW.
    //
    // A FUNCTION OF (previous, now] RATHER THAN OF "is now past t", and that is
    // the whole design. A coroutine per layer cannot state what happens when
    // one tick advances the clock past several times at once -- a long frame, a
    // scene load, a test running the fight at 60x -- so it delivers whichever
    // cues its own resume order happened to reach. AUDIT #61 is what that costs:
    // three runs on an unchanged tree, a different test failing each time.
    //
    // Half-open at BOTH ends is what makes "exactly once" true rather than
    // nearly true. A cue at exactly `to` belongs to this window and not to the
    // next one, which opens at `to`; a cue at exactly `from` belonged to the
    // previous window and is not repeated.
    //
    // The consequence for the first tick: a cue at 0 -- which is legal and
    // common, since every melee beat's cue is at 0 -- would never fire if the
    // first window were (0, now]. So a player starts its cursor at
    // BeforeAnything rather than at zero, and the sentinel is stated here
    // beside the rule that needs it rather than typed at the call site.
    public sealed class SpellSchedule
    {
        // Below every time a schedule can hold, since a cue is never negative.
        public const float BeforeAnything = -1f;

        private readonly SpellEvent[] _events;

        public SpellSchedule(IEnumerable<SpellEvent> events)
        {
            var list = new List<SpellEvent>(events ?? Array.Empty<SpellEvent>());

            // ORDERED BY TIME, THEN BY THE ORDER THEY WERE BUILT IN. List.Sort
            // is not stable, so ties would come out in whatever order the
            // partition happened to leave them -- and the ties here are exactly
            // the interesting case: every layer of a Cinderfault opens at
            // release, and "each cue delivered in schedule order" has to mean
            // something on that tick. Sorting on (time, original index) makes
            // it authored order, which is also draw order.
            var indexed = new List<(SpellEvent Event, int Index)>(list.Count);
            for (int i = 0; i < list.Count; i++) indexed.Add((list[i], i));

            indexed.Sort((a, b) =>
            {
                int byTime = a.Event.Seconds.CompareTo(b.Event.Seconds);
                return byTime != 0 ? byTime : a.Index.CompareTo(b.Index);
            });

            _events = new SpellEvent[indexed.Count];
            for (int i = 0; i < indexed.Count; i++) _events[i] = indexed[i].Event;
        }

        public int Count => _events.Length;

        public SpellEvent this[int index] => _events[index];

        // The last thing this cast will do. What a player reads to know when a
        // handle may be released without asking every renderer.
        public float LastSeconds => _events.Length == 0 ? 0f : _events[_events.Length - 1].Seconds;

        // FILLS A CALLER'S BUFFER RATHER THAN RETURNING A COLLECTION. The tick
        // path must allocate nothing -- that is the assertion the pooling in
        // this design exists to earn -- and a method returning a List or an
        // iterator allocates one per frame per cast whatever the caller does
        // with it.
        //
        // SCANS FROM 0 EVERY CALL. Correct for any (from, to), including a
        // caller with no cursor of its own to carry between calls -- see the
        // cursor overload below for the one caller that has one.
        public void Crossed(float from, float to, List<SpellEvent> into)
        {
            int cursor = 0;
            Crossed(from, to, into, ref cursor);
        }

        // THE SAME WINDOW, RESUMED FROM WHERE THE CALLER LEFT OFF. A live
        // cast's own `from` is monotone -- SpellPerformancePlayer.Advance
        // only ever grows cast.Cursor, never rewinds it -- so an event this
        // call skips as `Seconds <= from` can never fall inside a LATER
        // call's window either. `cursor` is the caller's own state (one int
        // per cast, reset to 0 at Begin): advancing it here rather than
        // restarting at index 0 turns a cast's whole tick lifetime from
        // O(events) rescanned every frame into O(events) total.
        public void Crossed(float from, float to, List<SpellEvent> into, ref int cursor)
        {
            if (into == null) return;
            into.Clear();
            if (to <= from) return;

            if (cursor < 0) cursor = 0;
            while (cursor < _events.Length && _events[cursor].Seconds <= from) cursor++;

            for (int i = cursor; i < _events.Length; i++)
            {
                float at = _events[i].Seconds;

                // Sorted, so the first event past the window ends the walk.
                if (at > to) break;

                into.Add(_events[i]);
            }
        }
    }
}
