namespace PrincesPalace.Domain.Combat
{
    // What a character skill does when it resolves.
    //
    // Lives in Domain and is used directly by the Unity-side
    // SkillDefinition, with no mirror enum and no name-mapping step — the
    // same precedent DamageType and EquipmentSlot already set. ItemKind
    // needs a mirror only because it is a Content-layer enum; this is not.
    public enum SkillEffect
    {
        // One enemy. The bread and butter.
        DamageSingle,

        // Every living enemy, resolved independently so each gets its own
        // weakness/resistance multiplier.
        DamageAll,

        HealSelf,
        HealParty,
        RestorePartyMana,

        // Forces the target to attack whoever cast this on their next turn
        // (StatusEffectType.Provoked). Deals no damage of its own — the whole
        // action is the redirection, plus whatever the caster's talents have
        // hung off being the one everything is aimed at.
        //
        // Hits every enemy instead of one when the caster has
        // TalentEffectType.ProvokeHitsEveryEnemy, which is why the targeting
        // for this effect is decided at cast time rather than authored: the
        // same skill is single-target or party-wide depending on the tree.
        Provoke,

        // Puts the caster under a timed transform — Shawn's Black Ram Mode.
        // The grants come from the skill's own TransformGrant block, so a
        // second transform is content rather than another case here.
        Transform,

        // ── The Fragile Lamb ─────────────────────────────────────────────
        //
        // Every number these read lives on the TALENT, not on the skill —
        // how strong a Ward is, how far it spreads, what a Shatter hits for.
        // The skill carries only what it costs. Same split the Black Ram's
        // Provoke already uses, and the reason a strand can deepen an ability
        // three times without three skills.

        // Wards the caster, and whoever else The Flock has widened it to.
        //
        // Targets SELF rather than a chosen ally, which is not a
        // simplification: the strand that spreads a ward is worded "also
        // applies to one ADDITIONAL ally", so the base ward was always the
        // caster's own. It is also the one the damage strand pays double for.
        Ward,

        // Detonates every Ward the caster has out, each for a share of their
        // Attack against a random enemy.
        Shatter,

        // Applies this skill's authored status to the whole living party and
        // does nothing else — the Lamb's Wail.
        //
        // Distinct from HealParty, which needs a heal amount to be worth
        // casting and would have meant authoring a fake one. "Everyone takes
        // half damage for two turns" is a buff with no healing in it.
        BuffParty,

        // The three Wool Gifts: mana, a stronger next swing, and a place at
        // the front of the queue. Separate members because they are three
        // different things happening to an ally, unlocked by three different
        // nodes at three different prices.
        GiftMana,
        GiftFury,
        GiftHaste,

        // Adds a fresh combatant to the CASTER'S OWN side, mid-fight. First
        // used by the Forest Warden's Roar, calling in a Giant Rat.
        //
        // Gated by SummonEnemyId/SummonCap rather than by a condition anyone
        // authors separately: the ability is simply drawn less often once the
        // field already holds enough of what it calls, via the same weighted
        // pool every other ability is drawn from -- see
        // FightSession.Enemies.EffectivePoolFor. A resolution that runs
        // anyway (an existing summon dying between the intent being
        // telegraphed and it landing, say) just tops back up to the cap
        // rather than refusing outright, so the boss is never shown winding
        // up for a call that then visibly does nothing.
        Summon,
    }

    // Who a skill is aimed at. Kept separate from the effect because the
    // targeting question the UI has to answer ("do I need the player to
    // click an enemy?") is not the same as the resolution question.
    public enum SkillTargeting
    {
        SingleEnemy,
        AllEnemies,
        Self,
        Party,
    }
}
