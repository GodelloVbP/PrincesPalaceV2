namespace PrincesPalace.Domain.Combat
{
    // BLOOD PRICE (docs/PLAN_BJORN_CONSTELLATIONS.md section 4, Phase 4 row
    // 4d): a caster whose CombatantState.ShortfallHealthPermille is above 0
    // may pay the part of a skill's PRIMARY-pool cost they cannot cover in
    // health instead, at that many thousandths of max health per point.
    // Blood Price T1 is 5 (1 Fury = 0.5% max HP).
    //
    // A PAYMENT, EXACTLY LIKE HealthCost -- written straight to
    // CurrentHealth, never through CombatMath.ApplyDamage or DealDamage. That
    // is what makes "self-payment never triggers cheat death" true by
    // construction: CombatantState.CheatDeathSpent and the
    // CheatDeathOncePerFight talent are only consulted inside
    // CombatMath.ApplyDamageDetailed, which this never reaches. No ward, no
    // Ignore Pain deferral, no ledger Took row, no Fury for "damage taken".
    //
    // NEVER BELOW 1 HP. SkillResolution.CanAfford adds this figure to any
    // authored HealthCost and refuses the cast unless at least 1 HP would be
    // left -- refused, not clamped, the same rule HealthCost follows, so a
    // skill is either fully paid or not cast (a clamp would sell a 60-Fury
    // cast for whatever health happened to be left).
    //
    // ONE PREDICATE: CanAfford is what the fight menu (ResolvedSkillOption
    // .Affordable), every bot policy reading that flag, CanCastToSeat and the
    // cast refusal in CastCore all ask, so none of them can disagree about a
    // blood-paid cast.
    //
    // ROUNDING: ceil(shortfall * maxHealth * permille / 1000), in long
    // arithmetic. Rounded UP like HealthCost.AmountFor -- a payment never
    // rounds in the payer's favour to zero. 10 Fury on 400 max HP is exactly
    // 20; 7 Fury on 450 max HP is 15.75 -> 16.
    public static class BloodPrice
    {
        // What the primary pool cannot cover of `primaryCost`, or 0 when the
        // caster has no Blood Price or can pay in full.
        public static int ShortfallOf(CombatantState actor, int primaryCost)
        {
            if (actor == null || actor.ShortfallHealthPermille <= 0 || primaryCost <= 0) return 0;

            int available = actor.PrimaryPool?.Current ?? 0;
            return primaryCost > available ? primaryCost - available : 0;
        }

        public static int HealthFor(CombatantState actor, int shortfall)
        {
            if (actor == null || shortfall <= 0 || actor.ShortfallHealthPermille <= 0 || actor.MaxHealth <= 0)
            {
                return 0;
            }

            long thousandths = (long)shortfall * actor.MaxHealth * actor.ShortfallHealthPermille;
            return (int)((thousandths + 999) / 1000);
        }

        // A direct write, see the header. Returns what was paid.
        public static int Pay(CombatantState actor, int shortfall)
        {
            int health = HealthFor(actor, shortfall);
            if (health <= 0) return 0;

            actor.CurrentHealth -= health;
            return health;
        }
    }
}
