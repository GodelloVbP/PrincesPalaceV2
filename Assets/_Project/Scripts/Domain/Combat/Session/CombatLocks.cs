using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Combat.Session
{
    // LOCKS: a once-per-X gate, keyed by an arbitrary string so any number
    // of independent relics/skills can each own a key without colliding.
    //
    // Two boundaries, because "once" means different things to different
    // callers: OncePerTurn resets whenever a fresh turn starts for whoever
    // holds the lock (World Ender's Crown re-arming above 30% HP is a
    // THRESHOLD reset, not a turn reset, and does not use this at all --
    // this file is for the "once per turn" / "once per combat" shape
    // specifically). OncePerCombat never resets on its own; only a fresh
    // CombatLocks instance (a new fight) clears it.
    //
    // A plain class rather than a static dictionary on CombatantState: a
    // lock set belongs to the SESSION, not to a combatant that might be
    // reused across fights in a test, and every other per-fight-only
    // bookkeeping in this codebase (FightSession's own dictionaries) is
    // already scoped that way.
    public sealed class CombatLocks
    {
        private readonly HashSet<string> _perCombat = new HashSet<string>();
        private readonly HashSet<string> _perTurn = new HashSet<string>();

        // Returns true and marks the key spent, or returns false if it was
        // already spent this combat. The check-and-set are ONE call
        // deliberately -- a caller that asked "is this free" and then set it
        // in two separate steps could race against itself the same way an
        // unguarded double-fire bug always looks: two effects both reading
        // "free" before either marks it spent.
        public bool OncePerCombat(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _perCombat.Add(key);
        }

        public bool OncePerTurn(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _perTurn.Add(key);
        }

        // Called once per turn boundary (a fresh actor's GrantTurnStart) --
        // ONLY that actor's per-turn locks clear. Every *LockKeyFor helper in
        // FightTuning suffixes the key with the owning combatant's ledger id
        // (see that file's header), so a key ending ":<ownerId>" belongs to
        // this owner and nobody else's.
        //
        // Clearing the whole set here (the bug this replaced) meant any
        // combatant's turn re-armed every OTHER combatant's once-per-turn
        // lock too: two enemies each hitting a Berserker's Vest wearer in
        // one round shaved a cooldown twice, because the second enemy's own
        // turn start wiped the first enemy's hit out from under the wearer.
        public void ResetTurn(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId)) return;

            string suffix = ":" + ownerId;
            _perTurn.RemoveWhere(key => key.EndsWith(suffix, StringComparison.Ordinal));
        }
    }
}
