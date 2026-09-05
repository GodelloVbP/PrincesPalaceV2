using System;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // What an item modifier ("Rift affix" — see the item-modifier plan's "The
    // model at a glance") DOES to combat, when what it does is a rule the
    // pipeline reads rather than a number folded into a stat by
    // ContentDatabase.EffectiveStats.
    //
    // MIRRORS TalentEffectType exactly, deliberately: a talent is a rule a
    // character's TREE grants for the fight, and a modifier is the identical
    // shape of rule granted by the character's GEAR instead. Two engines
    // reading two closed enums through two structurally identical bags
    // (TalentEffectSet / ModifierEffectSet) is cheaper than one shared enum
    // straddling two very different authoring surfaces — talents.json's
    // fixed 21-slot skeleton vs modifiers.json's flat id list, rolled 0-3
    // per item — which is exactly why the plan calls TalentEffectSet one of
    // "the two templates the codebase already hands us" rather than asking
    // for a new invention.
    //
    // PHASE C fills this out to the real modifier table (Fiery/Frosty/
    // Sylvan/Venomous/Astral/Hardened/Vampiric/Stalwart/Explosive/
    // Fortunate/Runic — see the plan's Phase C section and
    // ContentData/modifiers.json). Phase A2 stubbed five representative
    // shapes to prove the bag could be assembled, attached to a combatant
    // via CombatantState.ModifierEffects, and queried; every member below is
    // now read by a real combat hook, and every hook is exercised by real,
    // content-authored, droppable modifiers rather than test fixtures.
    //
    // Deliberately a CLOSED enum for the same reason TalentEffectType is one
    // — see that enum's own header. A modifier that needs a genuinely new
    // combat hook needs a line here AND the hook, which keeps the cost of a
    // new rule visible instead of letting it hide inside modifiers.json.
    //
    // MAGNITUDE IS PRE-SCALED before it ever reaches this struct. Every
    // scalar member's Magnitude, as read by a combat hook, is already
    // `base x TierMultiplier(itemTier) x RiftMultiplier(riftTier)` — see
    // ModifierMagnitude and ContentDatabase.ModifierEffects, the one
    // assembly point that performs this multiplication. A modifier
    // DEFINITION (ModifierDefinition.effects, what modifiers.json authors)
    // carries the UNSCALED base instead; ResolvedModifier/ModifierEffect is
    // the shared shape for both raw and scaled, which is why the scaling
    // happens exactly once, at the seam between them, rather than being
    // re-derived by every combat hook that reads a Magnitude.
    //
    // Threshold is percent of max health for any health-gated member — none
    // of today's members are health-gated, so Threshold is unused by all of
    // them; see ModifierEffectSet's own header for why BestBelowHealth is
    // not ported over yet either.
    public enum ModifierEffectType
    {
        // Never authored. Lets the resolver reject a mistyped effect name by
        // NAME instead of silently landing on whichever member is listed
        // first — same reasoning as TalentEffectType.None and
        // RelicModifierType.None.
        None = 0,

        // Bonus damage of ONE type (see the struct's own Against field below
        // — always a specific DamageType, never the "magical" shorthand
        // TypedResistanceFlat accepts: a hit is dealt AS an element, not
        // merely resisted as one of several) landed alongside a normal hit,
        // as a percent of the attacker's own Attack. Reuses the on-hit-rider
        // shape this codebase already has for typed damage — the
        // Poison-on-hit template at FightSession.Enemies.cs — mirrored onto
        // the PLAYER's own hit resolution in FightSession.ApplyFinalDamage,
        // which both a plain swing and a skill cast funnel through, so this
        // fires from either.
        //
        // REPLACES Phase A2's FireDamageOnHitPercent, which named only Fire.
        // The elemental family needs the identical rider for five other
        // types (Frosty/Sylvan/Venomous/Astral/Hardened), and
        // TypedResistanceFlat right below already solved "one member, five
        // possible targets" via the Against selector — generalising this
        // member the same way beats authoring five near-identical members
        // that would only ever differ by which DamageType they close over.
        ElementalDamageOnHitPercent,

        // Flat resistance to ONE damage type — see the struct's own
        // Against/AgainstMagical fields below for which one. "magical" is
        // accepted at authoring time as shorthand for every type that is not
        // Physical, the exact shorthand RelicModifierType.ResistanceFlat
        // already reads (see RelicModifier.cs) — mirrored rather than
        // reinvented because it is the same problem: one flat number, five
        // possible targets, no percent twin (resistance already rides the
        // R/(R+100) curve, so "+20% resistance" would be a percentage of a
        // percentage). Read wherever CombatantState.TypedResistance is
        // assembled (ContentDatabase.Effective/FightEncounterAdapter),
        // summed alongside whatever a relic already grants the same way.
        TypedResistanceFlat,

        // +Magnitude flat Speed while this modifier's item is worn. Kept as
        // real, working infrastructure from Phase A2 — no family in Phase
        // C's table spends it (Swift is a Phase D dodge-family concept), so
        // no modifiers.json entry authors it today; it is ready the moment
        // one does.
        FlatSpeedBonus,

        // Heals the wearer for Magnitude percent of the damage they just
        // dealt with an attack. Vampiric's whole effect — an on-hit rider in
        // FightSession.ApplyFinalDamage, reusing CombatMath.Heal.
        LifestealPercent,

        // The wearer always acts first in turn order, regardless of Speed.
        // Magnitude is ignored — a FLAG, the same shape as TalentEffectType's
        // own flag-shaped members (ProvokeHitsEveryEnemy,
        // CheatDeathOncePerFight, ...): present or absent, no number
        // attached, documented here rather than left for a reader to guess.
        // Kept as real infrastructure from Phase A2; unclaimed by Phase C's
        // table (Fortunate's own effect is FortunateFavorBonusFlat below, not
        // this), ready for a future modifier.
        GuaranteedFirstAction,

        // Stalwart's first half: a FLAT subtraction from PHYSICAL damage the
        // wearer takes, applied as the last step of
        // DamagePipeline.AfterDefences — AFTER the R/(R+100) mitigation
        // curve runs, not folded into it, so it reads as armour plating
        // rather than more of the same percentage curve PhysicalDefense
        // already rides. Floored at the game's usual damage floor of 1, same
        // as every other damage step.
        FlatPhysicalDamageReduction,

        // Stalwart's second half: incoming BreakShield depletion against the
        // wearer is reduced by Magnitude percent before BreakShield.Deplete
        // ever sees the amount (FightSession.DepleteBreakShield). Only
        // meaningful on a combatant that HAS a BreakShield — today that is
        // bosses/elites, not the player, so this is forward-looking
        // infrastructure for whichever side of a fight next carries one; it
        // does nothing (safely) on a wearer with BreakShield == null.
        BreakShieldDepletionResistPercent,

        // Explosive's effect: on a kill BY the wearer, splash Magnitude
        // percent of the wearer's own Attack onto the victim's neighbouring
        // enemy slots. Reuses FightSession.SplashOntoNeighbours — the exact
        // mechanism Trample's ApplyKillSplash already uses — through a
        // SEPARATE modifier-driven call (ApplyModifierKillSplash) alongside
        // the talent-driven one, rather than folding into ApplyKillSplash
        // itself: talent splash and modifier splash are two independently
        // magnituded sources, and one method reading two effect bags and
        // silently summing them would make a talent+modifier combo splash
        // harder to reason about than either source alone.
        OnKillSplashPercent,

        // Hardened's distinguishing extra effect (see modifiers.json):
        // Magnitude percent chance, on a landed hit, to knock the target
        // back FightTuning.ModifierPushBackSlots places in turn order —
        // reuses TurnOrder.PushBack via CombatEncounter.PushBack, fired from
        // the same on-hit rider block ElementalDamageOnHitPercent/
        // LifestealPercent read. CombatEncounter.PullToFront exists on the
        // same queue for a future "haste an ally on hit" modifier; nothing
        // in today's table authors one, so it stays unwired rather than
        // exercised by content that doesn't need it.
        PushBackOnHitChancePercent,

        // Runic's mana pool: flat +Magnitude Max Mana, applied at the same
        // FightEncounterAdapter seam a relic's MaxManaFlat already reaches
        // CombatantState.MaxMana through, summed alongside it.
        FlatMaxManaBonus,

        // Runic's mana income: flat +Magnitude ManaRegen per turn, same seam
        // as FlatMaxManaBonus above.
        FlatManaRegenBonus,

        // Runic's tempo rider: after the wearer lands a PLAIN swing (not a
        // skill), their very next skill cast costs Magnitude percent less
        // mana. One-shot — armed by the swing
        // (CombatantState.PendingManaDiscountPercent), consumed and cleared
        // the next time mana is charged for a skill
        // (FightSession.ChargeSkillMana), whether that cast happens the same
        // turn, three turns later, or never.
        NextSkillManaDiscountPercent,

        // Runic's mana->Ward conversion: at the start of the wearer's own
        // turn, unspent mana becomes a Shielded (Ward) status — reuses
        // StatusEffectType.Shielded + StatusEffects.Apply, the exact status
        // the Magical Shield relic already grants. A FLAG, like
        // GuaranteedFirstAction above: Magnitude is ignored, and the
        // conversion RATE is a fixed, deliberately conservative constant
        // (FightTuning.RunicWardConversionRate) rather than an authored
        // number — see that constant's own comment for why. Matches the
        // model-at-a-glance table's own rule for binary effects: their
        // "power" is being rare, not a bigger number.
        ManaToWardOnTurnStartPercent,

        // Fortunate's whole effect: a flat +Magnitude Prince's Favor bonus,
        // read LIVE off whatever the character currently has equipped, every
        // time Favor is queried for the loot roll (ItemOfferRoll.FavorOf,
        // via ContentDatabase.ModifierEffects(character).Best(...)). NO
        // stored state anywhere — unequip Fortunate and the bonus is gone on
        // the very next query, because ModifierEffects only ever reflects
        // the currently-equipped, currently-live loadout (ActiveLoadout).
        //
        // REPLACES an earlier shape (FortunateFavorOnWin) that wrote a
        // one-time chunk of favour to RunSnapshot.runFavor on every fight
        // WON — an accumulating currency that only ever grew across a run,
        // which was not the design: Fortunate is meant to be a passive
        // bonus that is only "on" while worn, not a chest of favour that
        // outlives the item. See git history for the retired shape.
        FortunateFavorBonusFlat,

        // PHASE D1. Swift's whole effect: a dodge RATING for the WEARER, as
        // the TARGET of an incoming swing or cast, to dodge it outright —
        // zero damage, and every on-hit rider (elemental procs, lifesteal,
        // status application, splash/kill-splash triggers) never fires,
        // because none of them run at all once the swing that would have
        // triggered them never landed. Read by DamagePipeline.RollDodge,
        // baked into the top of BOTH DamagePipeline.AfterDefences
        // overloads — the one funnel every real damage path shares —
        // rather than as a separate call site each path has to remember,
        // which is the actual fix for the class of bug a prior
        // balance-redesign phase hit when a multiplier removal reached two
        // of three real damage entry points and silently missed the third.
        // See DamagePipeline's own header for the full "why here" argument.
        //
        // NOT A DIRECT PERCENT, despite Magnitude otherwise reading as one
        // for every other *Percent-suffixed member in this enum — RENAMED
        // from DodgeChancePercent (2026-08-26) specifically to stop that
        // misreading. Magnitude here is a RATING run through
        // CombatMath.DodgePercentFrom's curve (100 * R / (R + 100), the
        // designer's own spec: "100 dodge = 50% chance to dodge") — the
        // IDENTICAL R/(R+100) shape CombatMath.AfterResistance already uses
        // for armour mitigation, just read as a probability instead of a
        // damage multiplier. Same reasoning as that curve's own header:
        // every point of rating helps, later points help less, and no
        // amount of stacking ever reaches a guaranteed dodge. Before this
        // curve existed, Magnitude WAS read as a direct percent, and a
        // maxed-out Swift item (base 12 x up to ~18.6x combined
        // tier/rift scaling) could already exceed 100 — a guaranteed,
        // unavoidable dodge, which is exactly the balance problem this
        // curve (and the rename) fixes. A content author authoring a new
        // *Rating member against this curve should expect "50" to mean
        // 33% dodge, not 50% — see CombatMath.DodgePercentFrom for the
        // exact numbers at a few landmark ratings.
        //
        // DELIBERATELY NOT read by splash/AoE-adjacent damage (Trample's
        // kill splash, Explosive's OnKillSplashPercent, the Black Ram's
        // transform splash, Shatter, the Lucky Deck relic's splash) or by a
        // status DoT tick (Poison) — none of those five call
        // DamagePipeline.AfterDefences at all today (see FightSession.
        // Talents.SplashOntoNeighbours/ResolveShatter and FightSession.
        // Relics.LuckyDeckSplash, all of which apply raw damage directly
        // through DealDamage on purpose — "the blow that splashes already
        // paid its own armour tax"; StatusEffects.Tick likewise applies
        // Poison straight through CombatMath.ApplyDamage). Baking the roll
        // into the shared funnel rather than at each individual DealDamage
        // call site means these five stay correctly undodgeable simply by
        // never routing through the funnel dodge lives in — no extra code
        // had to say so, and no future splash source can accidentally
        // acquire a dodge check by copy-pasting a damage call that never
        // had one. The alternative reading (splash IS a swing the target
        // could sidestep) was considered and rejected: splash is not
        // something the target reacted to in the moment the way a swing or
        // a cast aimed AT them is — it is collateral off someone ELSE's
        // hit, or off the DOT tick's own prior, already-resolved
        // application (which was itself dodgeable, if it came from a
        // landed hit — see StatusEffects.Apply's callers).
        DodgeRating,

        // PHASE D2. Frosty's distinguishing extra effect (see
        // modifiers.json): Magnitude percent CHANCE, on a landed hit, to
        // chill the target — StatusEffectType.Chilled, applied through
        // FightSession.SpeedBuffs.ApplyChilled exactly the way Lucky Deck's
        // relic-driven slow already does (that migration is what Chilled's
        // own read hook proves correct first — see StatusEffects.cs's own
        // header). The CHANCE is authored here, on the modifier; the
        // resulting chill's own speed-reduction magnitude and duration are
        // NOT authored per-modifier — they are fixed tuning constants
        // (FightTuning.ChilledOnHitSpeedPercent/ChilledOnHitTurns), the
        // identical shape PushBackOnHitChancePercent already uses for
        // Hardened's push distance (chance is content-tunable per modifier;
        // the mechanical effect's own size is one game-wide constant, not
        // a second per-modifier number to author and re-balance).
        //
        // Fired from the SAME on-hit rider block ElementalDamageOnHitPercent/
        // LifestealPercent/PushBackOnHitChancePercent read
        // (FightSession.ApplyModifierOnHitRiders) — one more rider in the
        // list a landed hit already walks, not a new call site.
        ChilledOnHitChancePercent,

        // PHASE D3. Sylvan's distinguishing extra effect (see
        // modifiers.json): Magnitude percent CHANCE, on a landed hit, to
        // root the TARGET — StatusEffectType.Rooted, applied via
        // StatusEffects.Apply straight off the same on-hit rider block
        // ChilledOnHitChancePercent/PushBackOnHitChancePercent already read
        // (FightSession.ApplyModifierOnHitRiders), the identical "chance is
        // authored per-modifier, the resulting status's own duration is a
        // fixed tuning constant" split those two already use
        // (FightTuning.RootOnHitTurns; Rooted carries no magnitude of its
        // own to author, so there is no sibling *Percent/*SpeedPercent
        // constant the way ChilledOnHitSpeedPercent is Chilled's).
        //
        // ONE DIRECTION ONLY, by construction: the wearer is the PLAYER (a
        // weapon modifier), so this only ever roots an ENEMY the wearer
        // hits. Nothing in this pass authors an enemy ability that grants
        // Rooted to a player, so the enemy-only gate this status is read by
        // (FightSession.Enemies.EffectivePoolFor) never has a player-side
        // case to handle — matching CanMeleeReach, the existing rule this
        // whole status builds on, which is exactly as one-directional.
        RootChancePercent,
    }

    // One rule an item modifier grants. Immutable and engine-free; the
    // combat pipeline reads these through ModifierEffectSet, never one at a
    // time — see that class' own header.
    // [Serializable] with public fields because ResolvedModifier is what the
    // ModifierDefinition asset now stores. The asset used to carry a parallel
    // ModifierEffectEntry for exactly this reason -- "Unity will not serialise
    // a readonly struct's fields" -- with a Select in each direction. Telling
    // System.Serializable about the struct is cheaper than maintaining its
    // mirror, and System.Serializable is BCL, so Domain stays engine-free.
    [Serializable]
    public struct ModifierEffect
    {
        // Authored as the enum MEMBER NAME in modifiers.json
        // ("ElementalDamageOnHitPercent"), not as an integer, for the
        // identical reason TalentEffect.Type is: JsonUtility cannot
        // deserialise an enum from a string, so the raw shape carries the
        // name and ModifierEntryResolver parses it, matching
        // TalentEntryResolver's own Enum.TryParse<TalentEffectType> pattern.
        public ModifierEffectType Type;

        // How much. Percent for every *Percent member, otherwise a flat
        // count (Speed points, Max Mana points, Favor). Ignored outright by
        // the flag-shaped members (GuaranteedFirstAction,
        // ManaToWardOnTurnStartPercent), which say so in their own comments.
        //
        // AS READ BY A COMBAT HOOK, this is already the FINAL, SCALED number
        // — see this file's own header on where the
        // `base x TierMultiplier x RiftMultiplier` multiplication happens.
        // A ModifierDefinition's own authored effects (before that seam)
        // still carry the raw, unscaled base.
        public int Magnitude;

        // The rule's health-gate number, percent of max health — unused
        // (always 0) by every member today; see this file's own header.
        // Kept as a real field rather than added later so ModifierEffect's
        // constructor shape never has to change out from under
        // ModifierEntryResolver once a health-gated member does land.
        public int Threshold;

        // TypedResistanceFlat's and ElementalDamageOnHitPercent's target
        // only. Null/false for every other member.
        //
        // WHY THIS LIVES ON THE STRUCT rather than a separate small carrier
        // type: TalentEffect's own header argues a rule needing a third
        // number "wants splitting in two, not... a fourth field" — but that
        // rule is about a SECOND scalar competing with Threshold's one
        // meaning. Against/AgainstMagical are not a second scalar, they are
        // a SELECTOR — "which of six elements" — the identical shape
        // RelicModifierType.ResistanceFlat already solved with
        // RelicModifier.Against/AgainstMagical (RelicModifier.cs). Giving
        // these members their own carrier type would mean ModifierEffectSet,
        // the resolver and every future consumer switching between "the
        // plain kind" and "the targeted kind" of effect — real complexity —
        // to avoid two nullable-shaped fields that sit unused on every other
        // member. Mirroring the existing precedent costs two idle fields;
        // inventing a second shape costs a parallel type hierarchy for two
        // rules.
        // THE FLAG IS NOT DECORATION: Nullable<DamageType> does not survive a
        // serializer, and Physical is DamageType's own zero, so a dropped
        // nullable reads back as "resists Physical" rather than "resists
        // nothing" -- the same pair RelicModifier keeps for the same reason.
        public DamageType AgainstType;
        public bool HasAgainst;
        public bool AgainstMagical;

        // The nullable reading every consumer already asks in.
        public DamageType? Against => HasAgainst ? (DamageType?)AgainstType : null;

        public ModifierEffect(ModifierEffectType type, int magnitude, int threshold = 0,
            DamageType? against = null, bool againstMagical = false)
        {
            Type = type;
            Magnitude = magnitude;
            Threshold = threshold;
            AgainstType = against ?? default;
            HasAgainst = against.HasValue;
            AgainstMagical = againstMagical;
        }
    }
}
