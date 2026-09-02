using System.Collections.Generic;

namespace PrincesPalace.Domain.Combat
{
    // FALLING-OFF STACKS: a stacking status where every stack carries its
    // OWN expiry rather than the whole pile sharing one duration -- the
    // shape Cursed Idol needs ("each hit adds a stack, each stack falls off
    // on its own N turns later") and the ordinary ActiveStatus/StatusEffects
    // machinery cannot express, because Magnitude/TurnsRemaining there is
    // one number per status TYPE, not one per stack.
    //
    // Keyed by an arbitrary string per caller (e.g. a relic id) rather than
    // by a StatusEffectType, so two unrelated stacking effects on the same
    // combatant never collide and this file never has to grow an enum.
    // Storage lives on CombatantState.StackTimers -- a plain
    // Dictionary<string, List<int>>, one list of remaining-turn counts per
    // key, empty for every combatant that carries no stacking effect at all
    // (which is nearly everyone).
    //
    // ADD DOES NOT TICK. A stack's clock only moves on an explicit Tick
    // call, which callers make once per the HOLDER's own turn (the same
    // "holder's own turns" convention ActiveStatus already documents) --
    // never inside AddStack itself, so a caller applying several stacks in
    // the same beat (Lucky Deck's own splash, a sweep hitting one enemy
    // twice) does not accidentally age anything.
    public static class FallingOffStacks
    {
        // Adds one stack at full duration, unless the holder is already at
        // the cap -- a relic that stacks "up to 5 times" simply stops
        // adding on the 6th hit rather than refreshing an existing stack's
        // clock, which would let spamming hits hold the top stack open
        // forever instead of it aging out on schedule.
        public static int AddStack(CombatantState target, string key, int durationTurns, int maxStacks)
        {
            var list = ListFor(target, key);
            if (list == null) return 0;

            if (list.Count < maxStacks && durationTurns > 0)
            {
                list.Add(durationTurns);
            }

            return list.Count;
        }

        public static int Count(CombatantState target, string key)
        {
            return target != null && target.StackTimers.TryGetValue(key, out var list) ? list.Count : 0;
        }

        // Total magnitude = stacks x per-stack value, capped -- the other
        // half of the mechanic's own spec, alongside individual expiry.
        public static int Magnitude(CombatantState target, string key, int perStackValue, int cap)
        {
            int total = Count(target, key) * perStackValue;
            return cap > 0 && total > cap ? cap : total;
        }

        // Advances every stack under `key` by one of the holder's own turns
        // and drops whichever ones just ran out. Returns the count that
        // survives.
        public static int Tick(CombatantState target, string key)
        {
            if (target == null || !target.StackTimers.TryGetValue(key, out var list)) return 0;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                list[i]--;
                if (list[i] <= 0) list.RemoveAt(i);
            }

            return list.Count;
        }

        // Advances every KEY this combatant carries, in one call -- what a
        // turn-start hook (FightSession.Riders.TickStatuses) actually wants:
        // it does not know in advance which relics' keys a given combatant
        // is carrying stacks under.
        public static void TickAll(CombatantState target)
        {
            if (target == null || target.StackTimers.Count == 0) return;

            foreach (var list in target.StackTimers.Values)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    list[i]--;
                    if (list[i] <= 0) list.RemoveAt(i);
                }
            }
        }

        private static List<int> ListFor(CombatantState target, string key)
        {
            if (target == null || string.IsNullOrEmpty(key)) return null;

            if (!target.StackTimers.TryGetValue(key, out var list))
            {
                list = new List<int>();
                target.StackTimers[key] = list;
            }

            return list;
        }
    }
}
