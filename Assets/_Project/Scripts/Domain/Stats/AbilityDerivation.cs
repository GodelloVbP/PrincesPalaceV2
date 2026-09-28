namespace PrincesPalace.Domain.Stats
{
    // Turns the six ability scores into things combat actually reads.
    //
    // Every derivation below is a single straight line through zero at score
    // 10, for every score at every value: a flat rate stays honest because
    // gear's ability-score budget is bounded (see GearScaling's own header),
    // so nothing here needs a piecewise curve to avoid going supernova on a
    // heavily-geared character.
    //
    // The design rule, and the reason this can ship without rebalancing
    // anything else: every derivation is a function of (score - 10) and
    // returns an exact zero at 10. A character nobody has authored scores
    // for derives nothing at all.
    //
    // Engine-free and pure, so the whole layer is unit-testable in EditMode
    // with no scene and no ScriptableObject -- which is the point of Domain.
    //
    // Strength and Intelligence derive nothing here, and that is deliberate,
    // not a gap this file forgot to fill: weapon/spell GRADES already answer
    // "how hard do I hit", via ScalingProfile reading STR/INT off the caster
    // at the moment of the swing/cast (see CombatMath.ScaledAttack /
    // ComputeSkillDamage, and FightEncounterAdapter.ToCombatant /
    // WeaponPower.DisplayDamage for where that reaches an enemy). A second,
    // uncoordinated answer to the same question here would double-dip
    // against weapon scaling. STR and INT are real ability scores that do
    // real things -- they just do them at the weapon/spell layer, not here.
    // Raising STR or INT still moves nothing this file returns, and that
    // stays permanent rather than a placeholder: SheetStats.FedBy leaves
    // both rows unlit for the same reason, on purpose, not as a gap to
    // close later.
    public static class AbilityDerivation
    {
        // The score at which every derivation is neutral.
        public const int NeutralScore = 10;

        // One point of Constitution: health and physical mitigation both.
        // PUBLIC rather than private -- ItemStatLines' tooltip annotation
        // ("+2 WIS (+4 Mana, +4 Mag Def)") reads these directly rather than
        // hand-copying the rate, so a retuned point value cannot drift
        // between what a stat does and what a tooltip claims it does.
        public const int HealthPerPoint = 20;
        public const int PhysicalDefensePerPoint = 2;

        // One point of Wisdom: mana and magical mitigation both.
        public const int ManaPerPoint = 2;
        public const int MagicalDefensePerPoint = 2;

        // Dexterity buys Speed at one point per two, Charisma buys signature
        // gain at one point per four -- see SpeedBonus/SignatureGainBonus for
        // the division-direction note that makes these divisors matter below
        // neutral. PUBLIC for the same reason as the four constants above --
        // GlossaryEntries.Mechanics() reads these rather than retyping "2"
        // and "4" into prose that could then disagree with the code.
        public const int SpeedDivisor = 2;
        public const int SignatureGainDivisor = 4;

        // Division convention pinned here because it is a real decision, not
        // an accident of C#'s `/` operator.
        //
        // "+1 Speed per 2 points" (and "+1 signature gain per 4") has to mean
        // something specific below neutral, where the division no longer
        // lands on a whole number. Two conventions disagree there:
        //
        //   mathematical floor (round toward -infinity): (-3) / 2 = -2
        //   truncation toward zero:                      (-3) / 2 = -1
        //
        // The plan's own worked example settles it: Shawn's DEX 7 (d = -3)
        // is specified to derive -1 Speed, not -2. That is truncation toward
        // zero -- which is also exactly what C#'s built-in integer `/`
        // already does, so the code below is plain `d / divisor` with no
        // helper method standing in for it.
        //
        // Worth being honest about the one place this bites: it makes the
        // penalty side of these two derivations very slightly shallower than
        // the bonus side would mirror (score 9, d=-1, truncates to 0 instead
        // of flooring to -1 -- a below-neutral point can cost nothing right
        // at the boundary where an above-neutral point already pays). That
        // is accepted here because it is what the plan's pinned example
        // requires, not an oversight; AbilityDerivationTests' boundary table
        // pins the exact behaviour at every score this matters for.

        // Constitution: max health, +20 a point, signed.
        public static int MaxHealthBonus(AbilityScoreBlock scores)
        {
            return (scores.constitution - NeutralScore) * HealthPerPoint;
        }

        // Constitution: physical mitigation, +2 a point, signed.
        public static int PhysicalDefenseBonus(AbilityScoreBlock scores)
        {
            return (scores.constitution - NeutralScore) * PhysicalDefensePerPoint;
        }

        // Wisdom: max mana, +2 a point, signed. Read by EffectiveMaxMana
        // rather than folded into DerivedStats -- StatBlock carries no mana
        // field, mana lives beside it in ContentDatabase.Effective.cs.
        public static int MaxManaBonus(AbilityScoreBlock scores)
        {
            return (scores.wisdom - NeutralScore) * ManaPerPoint;
        }

        // Wisdom: magical mitigation, +2 a point, signed.
        public static int MagicalDefenseBonus(AbilityScoreBlock scores)
        {
            return (scores.wisdom - NeutralScore) * MagicalDefensePerPoint;
        }

        // Wisdom: standard per-turn mana regen. DELIBERATELY NOT the
        // (score - NeutralScore) convention every other derivation in this
        // file uses -- this is not a bonus/penalty measured against a
        // neutral 10, it is the character's whole baseline regen rate, floor
        // divided straight off the raw score. floor(WIS / 4): WIS 14 -> 3,
        // WIS 3 -> 0, WIS 20 -> 5. Plain integer division already floors for
        // every non-negative WIS a character can field, so there is no
        // helper call here the way SpeedBonus/SignatureGainBonus need one
        // for their signed, below-neutral case.
        public const int ManaRegenPerPoints = 4;

        public static int ManaRegenBonus(AbilityScoreBlock scores)
        {
            return scores.wisdom / ManaRegenPerPoints;
        }

        // Dexterity: Speed, +1 per 2 points, signed -- see the convention
        // note above for what "per 2" means below neutral.
        public static int SpeedBonus(AbilityScoreBlock scores)
        {
            return (scores.dexterity - NeutralScore) / SpeedDivisor;
        }

        // A character's Speed from their content row alone -- the row's own
        // speed plus the Dexterity term, before relics. Named once because two
        // readers must agree on it: the tooling party build
        // (FightEncounterAdapter.ToCombatant) and the spell preview's squad
        // pick (PreviewStage.Squad), which has to know who opens the player
        // side before the fight it is planning exists.
        public static int BaseSpeed(StatBlock stats, AbilityScoreBlock scores)
        {
            return stats.speed + SpeedBonus(scores);
        }

        // Charisma: how fast a character's own signature resource fills each
        // turn, +1 per 4 points, signed. Read by BuildSignatureResource
        // rather than folded into DerivedStats, for the same reason mana
        // is separate -- StatBlock has no signature-gain field, and a
        // character with no signature resource simply never calls this.
        public static int SignatureGainBonus(AbilityScoreBlock scores)
        {
            return (scores.charisma - NeutralScore) / SignatureGainDivisor;
        }

        // The StatBlock-shaped contributions as one block, so callers add
        // once rather than remembering which of the six scores land here.
        //
        // Attack is deliberately 0: Strength does not derive it (see this
        // file's header) -- weapon power reaches Attack at the
        // FightEncounterAdapter seam instead. A character's basic Attack is
        // whatever their base stats and gear say, unmoved by Strength.
        //
        // MaxMana and SignatureGain are NOT here even though Wisdom and
        // Charisma derive them -- see MaxManaBonus/SignatureGainBonus above
        // for why: StatBlock has no field for either, so their callers read
        // those two methods directly instead of through this block.
        public static StatBlock DerivedStats(AbilityScoreBlock scores)
        {
            return new StatBlock(
                MaxHealthBonus(scores),
                SpeedBonus(scores),
                attack: 0,
                manaRegen: ManaRegenBonus(scores),
                physicalDefense: PhysicalDefenseBonus(scores),
                magicalDefense: MagicalDefenseBonus(scores));
        }
    }
}
