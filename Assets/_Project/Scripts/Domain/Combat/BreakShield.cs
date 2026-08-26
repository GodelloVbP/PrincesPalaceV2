using System;

namespace PrincesPalace.Domain.Combat
{
    // A monster's stagger meter: depletes on every hit, faster on a hit that
    // matches its elemental weakness, and hitting zero BREAKS it. A broken
    // combatant skips its next turn and — see CombatMath.BroadDefense —
    // its armour stops counting for as long as it stays broken, which is
    // exactly one turn: FightController resets the shield the moment that
    // turn is actually skipped, so the window is always the same length
    // regardless of how it was reached.
    //
    // Independent of health entirely, same as SignatureResource is: a fully
    // healthy monster can be broken and a nearly-dead one can still be
    // carrying a full shield. Nullable-by-reference on CombatantState for
    // the same reason Signature is — most combatants, everyone but a
    // handful of enemies, pay nothing for a mechanic they do not have.
    //
    // Engine-free and pure, same as SignatureResource — the rules about
    // who has one and how big live in Content (EnemyDefinition) and Core
    // (FightController), not here.
    public sealed class BreakShield
    {
        public int Current;
        public int Max;

        // True from the instant Current reaches zero until FightController
        // actually skips the broken turn and calls Reset. A hit that lands
        // while this is already true does nothing further to the shield —
        // see Deplete — so a multi-packet spell cannot "double break" it.
        public bool IsBroken;

        // Every hit costs at least this much shield, whether or not it
        // matches the weakness — see WeaknessDepletion for the reason a
        // squad with no elemental match still has to be ABLE to break it.
        public const int BaseDepletion = 1;

        // A weakness-matched hit costs this much instead. Twice the plain
        // rate: enough that hunting the matchup is a real, felt reward,
        // never so much that a squad without it cannot break the shield at
        // all through ordinary, patient play.
        public const int WeaknessDepletion = 2;

        public BreakShield(int max)
        {
            Max = Math.Max(1, max);
            Current = Max;
        }

        // How much one hit costs, from whether it landed on the weakness.
        public static int DepletionFor(bool wasWeaknessHit)
        {
            return wasWeaknessHit ? WeaknessDepletion : BaseDepletion;
        }

        // Returns whether THIS hit was the one that broke it, so the caller
        // can say so. A no-op, reporting false, once already broken — the
        // shield does not go further negative and does not re-trigger the
        // break message on every subsequent hit of the same broken window.
        public bool Deplete(int amount)
        {
            if (amount <= 0 || IsBroken)
            {
                return false;
            }

            Current = Math.Max(0, Current - amount);
            if (Current == 0)
            {
                IsBroken = true;
                return true;
            }

            return false;
        }

        // Refills to Max and clears the broken flag. Called exactly once,
        // the moment the broken turn is actually skipped — never on a timer
        // and never from Deplete itself, so the vulnerable window is always
        // "until this monster's next turn would have happened", regardless
        // of how many hits it took to reach zero.
        public void Reset()
        {
            Current = Max;
            IsBroken = false;
        }
    }
}
