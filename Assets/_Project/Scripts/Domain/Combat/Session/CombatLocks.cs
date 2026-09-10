using System.Collections.Generic;

namespace PrincesPalace.Domain.Combat.Session
{
    // LOCKS: a once-per-X gate, OWNED BY A COMBATANT and named by a key, so any
    // number of independent relics/skills can each own a gate per holder
    // without colliding.
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
    //
    // THE OWNER IS THE COMBATANT ITSELF, not a string spelled into the key.
    // This used to be one flat set of strings, every key suffixed ":" + the
    // owner's LEDGER id, with ResetTurn clearing by EndsWith. Two things were
    // wrong with that shape and only one of them was ever visible:
    //
    //   * A LEDGER ID IS NOT AN IDENTITY. It deliberately groups every
    //     combatant of one enemy type under one row -- three rats are all
    //     "rat", because a fight's damage table wants one Rat row and not
    //     three. Used as a lock owner it made them one owner: the first rat to
    //     claim a once-per-turn gate spent it for its littermates, and any one
    //     rat's turn boundary re-armed it for all of them. Unreachable against
    //     today's content (the only enemy-side lock is the primary pool's
    //     attack gain and every shipped enemy authors 0 for it), which is why
    //     nothing caught it, and one authored number away from being live.
    //   * SUFFIX MATCHING IS A CONVENTION, and a convention is something a
    //     future key can forget. An owner id ending in another owner's id, or
    //     containing the separator, aliased two owners with no way to notice.
    //
    // Both go away by making the owner a real field of the entry. The ledger's
    // grouping is untouched: it is right for the ledger and wrong for this,
    // which is why these are two different questions and not one id.
    //
    // Combatants are compared by REFERENCE (CombatantState overrides neither
    // Equals nor GetHashCode), which is exactly what "this combatant" means
    // and the same comparison FightSession's own per-combatant dictionaries
    // already use.
    public sealed class CombatLocks
    {
        private readonly HashSet<(CombatantState Owner, string Key)> _perCombat =
            new HashSet<(CombatantState, string)>();

        private readonly HashSet<(CombatantState Owner, string Key)> _perTurn =
            new HashSet<(CombatantState, string)>();

        // Returns true and marks the key spent, or returns false if it was
        // already spent this combat. The check-and-set are ONE call
        // deliberately -- a caller that asked "is this free" and then set it
        // in two separate steps could race against itself the same way an
        // unguarded double-fire bug always looks: two effects both reading
        // "free" before either marks it spent.
        public bool OncePerCombat(CombatantState owner, string key)
        {
            if (owner == null || string.IsNullOrEmpty(key)) return false;
            return _perCombat.Add((owner, key));
        }

        public bool OncePerTurn(CombatantState owner, string key)
        {
            if (owner == null || string.IsNullOrEmpty(key)) return false;
            return _perTurn.Add((owner, key));
        }

        // Called once per turn boundary (a fresh actor's GrantTurnStart) --
        // ONLY that combatant's per-turn locks clear.
        //
        // Clearing the whole set here (the bug this replaced) meant any
        // combatant's turn re-armed every OTHER combatant's once-per-turn
        // lock too: two enemies each hitting a Berserker's Vest wearer in
        // one round shaved a cooldown twice, because the second enemy's own
        // turn start wiped the first enemy's hit out from under the wearer.
        public void ResetTurn(CombatantState owner)
        {
            if (owner == null) return;

            _perTurn.RemoveWhere(entry => ReferenceEquals(entry.Owner, owner));
        }
    }
}
