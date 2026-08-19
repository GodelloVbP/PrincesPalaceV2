using System.Collections.Generic;
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
        MaxHealth,
        Attack,
        Defence,
        Speed,
        ManaRegen,
        PhysicalResistance,
        MagicalResistance,

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
            SheetStat.Defence,
            SheetStat.Speed,
            SheetStat.ManaRegen,
            SheetStat.PhysicalResistance,
            SheetStat.MagicalResistance,
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
                case SheetStat.Defence: return UiStrings.StatDefence;
                case SheetStat.Speed: return UiStrings.StatSpeed;
                case SheetStat.ManaRegen: return UiStrings.StatManaRegen;
                case SheetStat.PhysicalResistance: return UiStrings.StatPhysicalResistance;
                case SheetStat.MaxMana: return UiStrings.StatMaxMana;
                case SheetStat.SignatureGain: return UiStrings.StatSignatureGain;
                default: return UiStrings.StatMagicalResistance;
            }
        }

        // WHICH ATTRIBUTES ACTUALLY FEED A ROW.
        //
        // This is the data behind the character sheet's one real mechanic --
        // hover an attribute, see what it is doing for you -- so it has to be
        // TRUE rather than plausible. It is read off AbilityDerivation, which
        // is the only thing that actually converts a score into a stat:
        //
        //   Strength     -> Attack          Constitution -> Health
        //   Dexterity    -> Speed           Wisdom       -> Max Mana
        //   Charisma     -> signature gain
        //
        // Three rows are fed by NOTHING and that is not an omission: Defence,
        // both resistances and mana regen come from gear and talents only. A
        // handover that assigned Physical DEF to Constitution was describing a
        // game where armour is a stat you roll, and lighting that row from CON
        // would be a lie the player could not check.
        //
        // INTELLIGENCE FEEDS NO ROW AT ALL. It is not derived into anything --
        // it rides weapon and skill scaling through ScalingProfile instead, so
        // it changes what a WEAPON does rather than what the character is.
        // Hovering it highlights nothing, which is honest and also a gap worth
        // closing with a scaling row later.
        public static AbilityScore[] FedBy(SheetStat stat)
        {
            switch (stat)
            {
                case SheetStat.MaxHealth: return new[] { AbilityScore.Constitution };
                case SheetStat.Attack: return new[] { AbilityScore.Strength };
                case SheetStat.Speed: return new[] { AbilityScore.Dexterity };
                case SheetStat.MaxMana: return new[] { AbilityScore.Wisdom };
                case SheetStat.SignatureGain: return new[] { AbilityScore.Charisma };
                default: return System.Array.Empty<AbilityScore>();
            }
        }

        // The rows one attribute feeds, for the hover highlight.
        public static SheetStat[] Feeds(AbilityScore score)
        {
            var rows = new System.Collections.Generic.List<SheetStat>();
            foreach (var stat in Derived)
            {
                foreach (var source in FedBy(stat))
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
        public static string PerPointSummary(AbilityScoreBlock scores, AbilityScore score)
        {
            var bumped = scores.With(score, scores[score] + 1);
            var parts = new List<string>();

            foreach (var stat in Feeds(score))
            {
                int gain = DerivedBonus(stat, bumped) - DerivedBonus(stat, scores);
                if (gain != 0) parts.Add($"{(gain > 0 ? "+" : "")}{gain} {LabelFor(stat).Template}");
            }

            if (parts.Count == 0)
            {
                // True of Intelligence always, and of any score whose next point
                // lands mid-step on a rounded curve. Saying "nothing" is better
                // than printing "+0".
                return Feeds(score).Length == 0
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
                case SheetStat.Attack: return AbilityDerivation.AttackBonus(scores);
                case SheetStat.Speed: return AbilityDerivation.SpeedBonus(scores);
                case SheetStat.MaxHealth: return AbilityDerivation.MaxHealthBonus(scores);
                case SheetStat.MaxMana: return AbilityDerivation.MaxManaBonus(scores);
                case SheetStat.SignatureGain: return AbilityDerivation.SignatureGainBonus(scores);
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
                case SheetStat.Defence: return stats.defense;
                case SheetStat.Speed: return stats.speed;
                case SheetStat.ManaRegen: return stats.manaRegen;
                case SheetStat.PhysicalResistance: return stats.physicalResistance;

                // Neither lives on StatBlock -- max mana and the signature are
                // resolved per character in Core -- so the caller supplies them
                // and this returns 0 rather than inventing a number.
                case SheetStat.MaxMana: return 0;
                case SheetStat.SignatureGain: return 0;
                default: return stats.magicalResistance;
            }
        }
    }
}
