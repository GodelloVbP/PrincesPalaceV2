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
                default: return UiStrings.StatMagicalResistance;
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
                default: return stats.magicalResistance;
            }
        }
    }
}
