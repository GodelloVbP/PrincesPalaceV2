namespace PrincesPalace.Domain.Combat
{
    // A HEALTH COST IS A PAYMENT, NOT DAMAGE (plan 1.2). It is validated
    // alongside mana and the signature resource, in the same refusal, and
    // paid by writing CurrentHealth directly -- never through
    // CombatMath.ApplyDamage or FightSession.DealDamage, both of which run
    // riders a payment must not trigger (a ward would soak it, Wool's
    // SignaturePool.Absorb would drain for it, Phoenix Egg would consider
    // hatching, Last Stand's spike cap would clip it, and a lethal payment
    // would settle a death this is defined never to be able to do).
    //
    // ONE SMALL CLASS rather than three call sites each doing the same
    // ceiling-and-floor arithmetic: Blackglass Spear is the only author
    // today, but the shape (percent of max health, rounded up, refused
    // rather than allowed to kill) is a payment rule, not a spell-specific
    // one, and the next skill that wants blood for power reads this rather
    // than re-deriving it.
    public static class HealthCost
    {
        // ROUNDS UP -- the owner's rule, and deliberately NOT
        // Rounding.AwayFromZero, which rounds to the NEAREST integer (ties
        // away from zero) rather than always up: 5% of 201 max health is
        // 10.05, which AwayFromZero would round to 10 and a ceiling rounds
        // to 11. HealthCostTests.FivePercentOf201MaxHealth_Costs11_Not10
        // pins the difference.
        public static int AmountFor(int maxHealth, int healthCostPercent)
        {
            if (maxHealth <= 0 || healthCostPercent <= 0) return 0;

            return (int)System.Math.Ceiling(maxHealth * healthCostPercent / 100.0);
        }

        // Refused unless the payment would leave at least 1 HP -- a health
        // cost can never be the thing that kills, by construction, so there
        // is no SettleDeath to reach and no KillCredit to name for this path.
        public static bool CanPay(CombatantState actor, int healthCostPercent)
        {
            if (actor == null) return healthCostPercent <= 0;

            int amount = AmountFor(actor.MaxHealth, healthCostPercent);
            return amount <= 0 || actor.CurrentHealth - amount >= 1;
        }

        // A DIRECT WRITE, not a funnel call -- see this class's own header
        // for the riders that funnel would run and must not.
        public static void Pay(CombatantState actor, int healthCostPercent)
        {
            if (actor == null) return;

            int amount = AmountFor(actor.MaxHealth, healthCostPercent);
            if (amount <= 0) return;

            actor.CurrentHealth -= amount;
        }
    }
}
