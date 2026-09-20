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

        // ONE ALLY THE PLAYER PICKS, healed -- Odette's Mend (progression v2
        // phase 4). APPENDED, never inserted: the generated content assets
        // carry this enum as its ordinal, so slotting it beside HealSelf and
        // HealParty where it belongs alphabetically would renumber Provoke
        // and everything after it, and a rebuilt Resources/Content would
        // disagree with an unrebuilt one about what "5" means. Same
        // convention SkillTargeting.SingleAlly already follows and for the
        // same reason.
        //
        // A THIRD MEMBER RATHER THAN HealParty WITH A TARGET. The three heals
        // differ in who they land on, which is the one thing SkillTargeting
        // already says -- but they also differ in what a caller must supply:
        // HealSelf and HealParty need no pick and cannot be refused for
        // aiming at nobody, while this one is refused exactly the way a
        // Gift is. Folding it into HealParty would have meant a targeting
        // field that changes an effect's arity, which is the shape
        // FightSession's resolution switch exists to avoid.
        //
        // IT IS THE ONE HEAL THAT SCALES WITH ATTACK. "20 plus her spell
        // attack" is the authored spec, and it rides the same
        // CombatMath.ScaledAttack on the axis the skill names that spell
        // DAMAGE does -- see SkillResolution.Amount. HealSelf and HealParty
        // are deliberately left alone: giving them an attack term would be a
        // balance change to Woolgathering and the beetle's Shell Up riding
        // in on a new member.
        HealSingle,

        // ONE ENEMY, CONSUMES THE NAMED STATUS, DEALS ITS WORTH SPLIT ACROSS
        // AUTHORED TYPES -- Ashen Reckoning (plan D10, milestone B), and the
        // first of five appended together across the spell-expansion
        // milestones (Afflict, Hasten, SwapAllies, Enthrall are the other
        // four, each landing with the milestone that first needs it).
        //
        // A MEMBER RATHER THAN A DamageSingle VARIANT because its packets do
        // not exist until the consumption has happened: HasFixedDamage is
        // false and the authored damageInstances list is empty, so the
        // packets are built at resolution from a total ResolveReclaim reads
        // off StatusCombos.SpendPoisonIfMatched and ConsumedTotalSplit, not
        // from anything skills.json carries directly.
        //
        // APPENDED, never inserted -- see this enum's own header on why
        // (SkillDefinition stores this as a raw int).
        Reclaim,

        // ONE OTHER ALLY MOVED EARLIER IN THE ORDER -- Borrowed Moment (plan
        // D10, 2.7, milestone C). No damage, no status, nothing left on the
        // board: the whole cast is a position.
        //
        // A MEMBER RATHER THAN A FIELD ON A GIFT. Gift: Haste is the closest
        // thing that already exists and it is a different promise -- it hands
        // an ally the very next action, unconditionally, and is priced as a
        // Wool talent. This buys a stated number of PLACES, can be refused
        // for having none to buy (1.9 rule 5), and previews where it would
        // land before it is committed. Expressing that as "GiftHaste with a
        // slots field" would make one member mean two schedules.
        //
        // APPENDED, never inserted -- see this enum's own header.
        Hasten,

        // TWO ALLIES TRADE FIELD PLACES, as the caster's one free action --
        // Palace Passage (plan D10, 2.9, 1.12, milestone C).
        //
        // THE ONLY EFFECT IN THE GAME THAT NEEDS TWO PICKS, and the pick
        // COUNT is read off the effect here (SkillEffects.PicksRequired)
        // rather than authored on the row: one more content number would let
        // a row ask for three picks that no resolution can use.
        //
        // A MEMBER RATHER THAN AN EXTENSION OF THE MOVE VERB. Move is "the
        // current actor trades with the nearest living ally in one
        // direction" and costs the turn; this is "any two allies trade" and
        // costs a cast. They share their whole mechanism -- SwapPartySlots,
        // the Rooted rule read off both sides, the NoteDeliberateMove pair --
        // and differ in who chooses and what it costs, which is exactly the
        // split between a verb and a skill.
        //
        // APPENDED, never inserted -- see this enum's own header.
        SwapAllies,

        // ONE ENEMY, THIS SKILL'S AUTHORED STATUS, NO DAMAGE -- Velvet
        // Shackles (plan D10, 2.10, milestone D), with Censer of Embers and
        // Thorn Tithe joining it in milestone E.
        //
        // A MEMBER RATHER THAN A DamageSingle THAT HAPPENS TO DEAL NOTHING,
        // and the difference is visible to the player. A damage cast enters
        // AfterDefences and can therefore be DODGED; an Afflict has no damage
        // instance, never enters it, and so lands or is refused -- a two-turn
        // root that silently misses a fifth of the time is a different spell
        // from the one the card describes. It is also the difference between
        // "0 damage" appearing over the target and nothing appearing at all.
        //
        // WHAT IT LANDS is entirely the row's appliesStatus / statusMagnitude
        // / statusDuration, through the ApplyStatusTo seam (plan D6), so a
        // second afflicting spell is a content row and no code.
        //
        // APPENDED, never inserted -- see this enum's own header. Plan D10
        // listed it FIRST of the five, before Reclaim; Reclaim, Hasten and
        // SwapAllies landed in milestones B and C while this one waited for D,
        // so it appends behind them. The plan's ORDER was a convenience; its
        // rule ("nothing is inserted or reordered") is the part that protects
        // the generated assets, and that is what is kept.
        Afflict,
    }

    // ONE PLACE FOR "IS THIS A DAMAGE EFFECT", so the pipeline the pool-tier
    // multiplier, the defense-ignoring flag and ResolvedSkill.IsDamaging all
    // mean by "damaging" cannot list DamageSingle/DamageAll separately and
    // quietly disagree the next time a third damage effect is added.
    public static class SkillEffects
    {
        public static bool IsDamagePipeline(SkillEffect effect) =>
            effect == SkillEffect.DamageSingle || effect == SkillEffect.DamageAll;

        // HOW MANY TARGETS A CAST OF THIS EFFECT HAS TO CARRY, and the one
        // place that answer lives (plan 1.12).
        //
        // A PROPERTY OF THE EFFECT, NOT OF THE CONTENT ROW. Palace Passage
        // needs two because a swap needs two ends; nothing an author could
        // write would make a third pick resolvable, so there is no field to
        // author it with and no way for a row to disagree with the
        // resolution about how many clicks it takes.
        //
        // READ BY THREE: the menu (how many picks before it commits), the
        // session (how many it validates and refuses on), and the bot's legal
        // menu (which arity of command it can build). One rule, three
        // readers -- the shape this file's own IsDamagePipeline exists for.
        public static int PicksRequired(SkillEffect effect) =>
            effect == SkillEffect.SwapAllies ? 2 : 1;
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

        // ONE ALLY THE PLAYER PICKS. Appended rather than slotted in beside
        // Self, because the generated content assets carry this as its
        // ordinal -- inserting a member renumbers every row below it, and a
        // rebuilt Resources/Content would disagree with an unrebuilt one
        // about what "2" means.
        //
        // The side is the only thing that differs from SingleEnemy: same
        // Target depth, same plate click, same cancel. What does NOT carry
        // across is reach -- rank masks and the front-rank rule are about
        // fighting past a bodyguard, and nothing stands between a caster and
        // his own squad (docs/handoffs/archive/battle_ui/README.md:230). See
        // FightSession.CanReachAlly.
        SingleAlly,
    }
}
