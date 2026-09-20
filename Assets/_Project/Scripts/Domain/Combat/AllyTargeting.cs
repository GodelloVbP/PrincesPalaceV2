namespace PrincesPalace.Domain.Combat
{
    // WHO, ON THE CASTER'S OWN SIDE, A GIVEN SkillEffect MAY BE AIMED AT.
    //
    // ONE PREDICATE PER EFFECT, AND IT LIVES IN DOMAIN. The click handler,
    // the plate dimming, the refusal inside CastSkill and the bot's legal
    // menu all have to agree about which allies are candidates; the moment
    // any one of them computes that itself, a plate the UI offers and a cast
    // the session refuses become two different answers to one question --
    // which is the shape of every targeting bug this project has already had
    // on the enemy side (see FightSession.CanReachEnemy's own header).
    //
    // WARD INCLUDES THE CASTER AND A GIFT DOES NOT, and both are design
    // rules rather than accidents: wrapping himself is the Lamb's basic
    // defensive turn, while a gift is wool he is handing over -- giving it
    // to himself would be a cast that spends the resource to return it.
    //
    // GIFT: MANA CARRIES ONE MORE CONDITION than the other two. It is a
    // fixed percentage of the recipient's own maximum poured into their
    // primary pool, and CombatMath.CanRestoreMana is the SAME predicate the
    // potion, the relic top-up, Lucky Deck and the bot's missing-mana
    // accounting all read -- a pool that refuses mana effects would take the
    // wool and the turn and move nothing. Fury and Haste say nothing about a
    // resource (one applies Empowered, the other moves a turn up the order),
    // so every living ally is a valid recipient for both.
    public static class AllyTargeting
    {
        // True when `candidate` is a legal ally target for `effect` cast by
        // `caster`. Says nothing about SIDE or about being alive -- that is
        // FightSession.CanReachAlly's half, asked first, so this stays a pure
        // question about the effect and needs no encounter to answer.
        public static bool Accepts(SkillEffect effect, CombatantState caster, CombatantState candidate)
        {
            if (candidate == null) return false;

            switch (effect)
            {
                case SkillEffect.GiftMana:
                    return !ReferenceEquals(candidate, caster) && CombatMath.CanRestoreMana(candidate);

                case SkillEffect.GiftFury:
                case SkillEffect.GiftHaste:
                // BORROWED MOMENT JOINS THE TWO GIFTS, and the plan named it
                // as joining them (2.7: "the caster is excluded, the same
                // rule the three Gifts already use"). The reason is stronger
                // here than there, though: forecast index 0 IS the action the
                // caster is taking right now, so advancing yourself has no
                // destination that exists -- it is not merely a waste, it is
                // undefined (1.9 rule 2).
                //
                // WHETHER THERE IS ANYWHERE FOR THIS ALLY TO GO is a
                // different question and deliberately not asked here: it
                // needs the schedule, which is board state, and this class
                // answers only the effect's own shape. FightSession.
                // EligibleAllies asks the board half, the same way it already
                // asks CanReachAlly.
                case SkillEffect.Hasten:
                    return !ReferenceEquals(candidate, caster);

                // PALACE PASSAGE TAKES ANYBODY ON THE CASTER'S SIDE,
                // themselves included -- swapping into a squadmate's place is
                // the most obvious use of it, and "the caster may not be one
                // of the two" would make a two-person party unable to cast it
                // at all. It is the default arm's answer, written out because
                // a reader arriving from the Gift cases above will expect the
                // exclusion and should be told it does not apply.
                //
                // THE SECOND PICK NOT BEING THE FIRST is not this predicate's
                // job either: it is a fact about a cast in progress, not
                // about who the effect accepts, and it lives in the picker
                // and in the session's commit-time refusal.
                case SkillEffect.SwapAllies:
                    return true;

                // Ward, and whatever single-ally effect is authored next --
                // a heal aimed at one squadmate is the obvious one, and
                // "anybody on my side, myself included" is the right default
                // for it. An effect that needs a narrower rule adds its own
                // case here rather than a flag anywhere else.
                default:
                    return true;
            }
        }
    }
}
