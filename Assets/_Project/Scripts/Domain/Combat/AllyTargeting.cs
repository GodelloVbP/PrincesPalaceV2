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
                    return !ReferenceEquals(candidate, caster);

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
