using System.Collections.Generic;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.UiKit
{
    // Every number the character sheet shows, in the order it shows them.
    //
    // One home for the row list, its labels and how each value is read, so the
    // tree that lays out thirteen rows and the controller that fills them
    // cannot disagree about which row is which. Same idiom as
    // EquipmentSlots.All: declaration order IS display order, and there is no
    // lookup table to drift.
    //
    // The sheet previously showed no numbers at all. A screen for deciding what
    // to wear that never says what wearing it does is the one screen that most
    // needs them -- and now that equipment actually reaches combat, these are
    // the same figures the fight uses rather than a second opinion.
    public enum SheetStat
    {
        // The six a player spends points on.
        Strength,
        Dexterity,
        Constitution,
        Wisdom,
        Intelligence,
        Charisma,

        // What those turn into, plus whatever gear and talents added.
        //
        // Defence (the old single generic mitigation stat) is GONE, not
        // renamed to either PhysicalDefense or MagicalDefense — see
        // StatType's own header.
        MaxHealth,
        Attack,
        Speed,
        ManaRegen,
        PhysicalDefense,
        MagicalDefense,

        // ADDED so the attribute-to-stat link is not dead for half the sheet.
        // Wisdom and Charisma both feed something real -- the mana pool and the
        // signature resource -- and neither had a row, so hovering either lit
        // nothing and the feature read as broken rather than as empty.
        MaxMana,
        SignatureGain,
    }

    public static class SheetStats
    {
        // The ability half and the derived half are laid out as two columns, so
        // they are kept as two lists rather than one list sliced by index -- a
        // slice point is exactly the kind of constant that survives a reorder
        // and quietly puts Charisma at the top of the combat column.
        public static readonly SheetStat[] Abilities =
        {
            SheetStat.Strength,
            SheetStat.Dexterity,
            SheetStat.Constitution,
            SheetStat.Wisdom,
            SheetStat.Intelligence,
            SheetStat.Charisma,
        };

        public static readonly SheetStat[] Derived =
        {
            SheetStat.MaxHealth,
            SheetStat.Attack,
            SheetStat.Speed,
            SheetStat.ManaRegen,
            SheetStat.PhysicalDefense,
            SheetStat.MagicalDefense,
            SheetStat.MaxMana,
            SheetStat.SignatureGain,
        };

        // Both columns end to end, which is the order the controller fills its
        // one flat array of labels in. Built from the two arrays above rather
        // than restated, so adding a row in one place is enough.
        public static readonly SheetStat[] All = Concat(Abilities, Derived);

        private static SheetStat[] Concat(SheetStat[] left, SheetStat[] right)
        {
            var all = new SheetStat[left.Length + right.Length];
            left.CopyTo(all, 0);
            right.CopyTo(all, left.Length);
            return all;
        }

        public static UiString LabelFor(SheetStat stat)
        {
            switch (stat)
            {
                case SheetStat.Strength: return UiStrings.StatStrength;
                case SheetStat.Dexterity: return UiStrings.StatDexterity;
                case SheetStat.Constitution: return UiStrings.StatConstitution;
                case SheetStat.Wisdom: return UiStrings.StatWisdom;
                case SheetStat.Intelligence: return UiStrings.StatIntelligence;
                case SheetStat.Charisma: return UiStrings.StatCharisma;
                case SheetStat.MaxHealth: return UiStrings.StatMaxHealth;
                case SheetStat.Attack: return UiStrings.StatAttack;
                case SheetStat.Speed: return UiStrings.StatSpeed;
                case SheetStat.ManaRegen: return UiStrings.StatManaRegen;
                case SheetStat.PhysicalDefense: return UiStrings.StatPhysicalDefense;
                case SheetStat.MagicalDefense: return UiStrings.StatMagicalDefense;
                case SheetStat.MaxMana: return UiStrings.StatMaxMana;
                case SheetStat.SignatureGain: return UiStrings.StatSignatureGain;
                default: throw new System.ArgumentOutOfRangeException(nameof(stat), stat,
                    "SheetStat has no label -- add a case above.");
            }
        }

        // WHICH ATTRIBUTES ACTUALLY FEED A ROW.
        //
        // This is the data behind the character sheet's one real mechanic --
        // hover an attribute, see what it is doing for you -- so it has to be
        // TRUE rather than plausible. It is read off AbilityDerivation, which
        // is the only thing that actually converts a score into a stat:
        //
        //   Constitution -> Health, Physical Defense
        //   Wisdom       -> Max Mana, Magical Defense
        //   Dexterity    -> Speed
        //   Charisma     -> signature gain
        //
        // PHASE 2 OF THE BALANCE REDESIGN moved Physical/Magical Defense off
        // "fed by nothing" and onto Constitution/Wisdom respectively (see
        // AbilityDerivation.PhysicalDefenseBonus/MagicalDefenseBonus) -- armour
        // is no longer the only source of either, ability scores are too.
        //
        // ATTACK IS FED BY NOTHING, PERMANENTLY -- not a placeholder pending a
        // later phase. AbilityDerivation.AttackBonus was deleted for good:
        // Strength no longer derives a flat Attack bonus, because offense
        // lives at weapon grades instead (D3, Phase 3, landed -- see
        // WeaponPower.DisplayDamage and FightEncounterAdapter.ToCombatant) to
        // kill the double-dip where both a stat AND a weapon rewarded the
        // same score. Hovering Strength on the sheet lights no row, and that
        // stays true: a stat row is "what does this DERIVE", and STR derives
        // nothing -- it multiplies a weapon's own number instead, which is a
        // different question with a different, honest answer (see an item's
        // own tooltip -- ItemStatLines.BonusParts -- for where that answer
        // actually lives: "weapon scaling", not a row here). Same reasoning
        // applies to Intelligence below.
        //
        // INTELLIGENCE FEEDS NO ROW AT ALL, for the same permanent reason. It
        // is not derived into anything -- it rides weapon and skill scaling
        // through ScalingProfile instead, so it changes what a WEAPON or
        // SPELL does rather than what the character is. Hovering it
        // highlights nothing, and PerPointSummary says so explicitly rather
        // than printing a misleading "+0".

        // THE PRIMARY POOL'S CAPACITY RULE DECIDES WHETHER WISDOM FEEDS THE
        // POOL ROW AT ALL, and it has to: a Fixed pool is exactly its
        // authored capacity from every source (PoolCapacityRule.Fixed), so
        // for its owner Wisdom moves that number by nothing. Lighting the row
        // anyway would be the precise lie this table exists to prevent -- the
        // row highlights and the figure never changes.
        //
        // A RULE ENUM RATHER THAN THE POOL. Domain/UiKit cannot see
        // ContentDatabase, and the sheet needs exactly one bit of the
        // definition here. The default is WisdomDerived because that is what
        // mana is and what every character shipped today carries; the
        // dossier passes the real answer (CharacterDossierController), and a
        // caller with no character in hand -- the feed test, the screen tree
        // -- gets the shipped truth rather than a guess.
        public static AbilityScore[] FedBy(SheetStat stat, PoolCapacityRule primaryPoolRule = PoolCapacityRule.WisdomDerived)
        {
            switch (stat)
            {
                case SheetStat.MaxHealth: return new[] { AbilityScore.Constitution };
                case SheetStat.PhysicalDefense: return new[] { AbilityScore.Constitution };
                case SheetStat.Speed: return new[] { AbilityScore.Dexterity };
                case SheetStat.MaxMana:
                    return primaryPoolRule == PoolCapacityRule.WisdomDerived
                        ? new[] { AbilityScore.Wisdom }
                        : System.Array.Empty<AbilityScore>();
                case SheetStat.MagicalDefense: return new[] { AbilityScore.Wisdom };
                case SheetStat.SignatureGain: return new[] { AbilityScore.Charisma };
                default: return System.Array.Empty<AbilityScore>();
            }
        }

        // The rows one attribute feeds, for the hover highlight.
        public static SheetStat[] Feeds(AbilityScore score, PoolCapacityRule primaryPoolRule = PoolCapacityRule.WisdomDerived)
        {
            var rows = new System.Collections.Generic.List<SheetStat>();
            foreach (var stat in Derived)
            {
                foreach (var source in FedBy(stat, primaryPoolRule))
                {
                    if (source == score) { rows.Add(stat); break; }
                }
            }

            return rows.ToArray();
        }

        // WHAT ONE MORE POINT WOULD BUY, measured rather than authored.
        //
        // The handover writes this as a fixed string per attribute ("+18
        // Health, +0.5 Physical DEF"). It cannot be fixed: every derivation in
        // this game is a CURVE -- pools grow quadratically, rates by a root --
        // so the marginal point is worth more at 30 than at 11, and a constant
        // would be wrong everywhere except the one score it was written at.
        //
        // Derived by bumping the score by one and re-reading, so it is exact at
        // whatever the character actually has and cannot drift from the
        // formula.
        public static string PerPointSummary(AbilityScoreBlock scores, AbilityScore score,
                                             PoolCapacityRule primaryPoolRule = PoolCapacityRule.WisdomDerived)
        {
            var bumped = scores.With(score, scores[score] + 1);
            var parts = new List<string>();

            foreach (var stat in Feeds(score, primaryPoolRule))
            {
                int gain = DerivedBonus(stat, bumped) - DerivedBonus(stat, scores);
                if (gain != 0) parts.Add($"{(gain > 0 ? "+" : "")}{gain} {LabelFor(stat).Template}");
            }

            if (parts.Count == 0)
            {
                // True of Intelligence always, and of any score whose next point
                // lands mid-step on a rounded curve. Saying "nothing" is better
                // than printing "+0".
                //
                // AND OF WISDOM, for a character whose primary pool is Fixed:
                // Feeds drops the pool row for them, so the summary says
                // Magical Defense alone rather than promising a capacity that
                // will not move.
                return Feeds(score, primaryPoolRule).Length == 0
                    ? "Feeds no stat directly - it scales weapons and skills."
                    : "The next point does not move a stat.";
            }

            return "One more point: " + string.Join(", ", parts);
        }

        // The attribute-derived HALF of a stat, which is the only half a score
        // can move. Gear is deliberately absent: this answers "what does my CON
        // do", not "what is my Health".
        private static int DerivedBonus(SheetStat stat, AbilityScoreBlock scores)
        {
            switch (stat)
            {
                case SheetStat.Speed: return AbilityDerivation.SpeedBonus(scores);
                case SheetStat.MaxHealth: return AbilityDerivation.MaxHealthBonus(scores);
                case SheetStat.PhysicalDefense: return AbilityDerivation.PhysicalDefenseBonus(scores);
                case SheetStat.MaxMana: return AbilityDerivation.MaxManaBonus(scores);
                case SheetStat.MagicalDefense: return AbilityDerivation.MagicalDefenseBonus(scores);
                case SheetStat.SignatureGain: return AbilityDerivation.SignatureGainBonus(scores);

                // Attack: no case, falls to 0 below -- AttackBonus is deleted
                // for good, and Strength derives nothing here, permanently.
                // See FedBy's header.
                default: return 0;
            }
        }

        // The value, read out of figures the caller has already resolved.
        //
        // Takes the blocks rather than a Character on purpose: this is Domain
        // and the effective figures come from ContentDatabase in Core. Keeping
        // the extraction here means the row order and the row VALUES are
        // decided in the same file, and an EditMode test can cover both without
        // a save.
        public static int ValueOf(SheetStat stat, StatBlock stats, AbilityScoreBlock scores)
        {
            switch (stat)
            {
                case SheetStat.Strength: return scores.strength;
                case SheetStat.Dexterity: return scores.dexterity;
                case SheetStat.Constitution: return scores.constitution;
                case SheetStat.Wisdom: return scores.wisdom;
                case SheetStat.Intelligence: return scores.intelligence;
                case SheetStat.Charisma: return scores.charisma;
                case SheetStat.MaxHealth: return stats.maxHealth;
                case SheetStat.Attack: return stats.attack;
                case SheetStat.Speed: return stats.speed;
                case SheetStat.ManaRegen: return stats.manaRegen;
                case SheetStat.PhysicalDefense: return stats.physicalDefense;
                case SheetStat.MagicalDefense: return stats.magicalDefense;

                // Neither lives on StatBlock -- max mana and the signature are
                // resolved per character in Core -- so the caller supplies them
                // and this returns 0 rather than inventing a number.
                case SheetStat.MaxMana: return 0;
                case SheetStat.SignatureGain: return 0;
                default: throw new System.ArgumentOutOfRangeException(nameof(stat), stat,
                    "SheetStat has no value mapping -- add a case above.");
            }
        }

        // The value AS SHOWN on the sheet, not just its raw number.
        //
        // BALANCE REDESIGN PHASE 6 (D7.3): Physical/Magical Defense mean
        // nothing on their own -- nobody outside this codebase knows what
        // "50" buys -- so their row carries the same read of the mitigation
        // curve every real hit runs on. `value` is whatever the caller
        // already resolved (ValueOf for most rows, Core's own path for the
        // two rows StatBlock does not carry), so this never re-derives a
        // number it was just handed, only decides how to WORD it.
        //
        // "50 (33%)", not a full "33% less physical damage" sentence --
        // DEDUCED, not a style choice: this row is CharacterDossierScreen's
        // DossierStatValue{i} label, a single line 76px wide at font 15 (see
        // its own declaration), and that box was never widened for this
        // phase (every spare pixel in that row is already spent on the name
        // and preview columns beside it, and widening any of the three
        // collides with its neighbour at every one of the four audited
        // canvas aspects). Built off ItemStatLines.DamageReductionPercent --
        // the one shared formula for the whole curve -- so a retuned
        // mitigation equation cannot leave this row disagreeing with the
        // glossary's own worked examples (GlossaryEntries.Mechanics()).
        public static string DisplayText(SheetStat stat, int value)
        {
            if (stat == SheetStat.PhysicalDefense || stat == SheetStat.MagicalDefense)
            {
                return $"{value} ({ItemStatLines.DamageReductionPercent(value)}%)";
            }

            return value.ToString();
        }
    }
}
