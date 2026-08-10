namespace PrincesPalace.Domain.Stats
{
    // The stats every combatant has. Adding a new one means: add it here,
    // add a matching field to StatBlock, and extend StatBlock's indexer and
    // ForStat — the compiler and StatBlockTests.Indexer_CoversEveryStatType
    // will point at anything left unwired.
    //
    // These names are also the AUTHORING vocabulary: item sets name their
    // stats as strings in JSON and resolve them against this enum, so adding
    // a member here is the whole of what it takes to make a new stat
    // authorable. That is why the enum is worth keeping in step with
    // StatBlock rather than letting the struct be the only list.
    public enum StatType
    {
        MaxHealth,
        Speed,
        Attack,
        Defense,

        // Mana returned at the start of each of this combatant's turns.
        ManaRegen,

        // Damage reduction against Physical, and against every other damage
        // type respectively. Points rather than percent — CombatMath turns
        // them into a reduction on a curve that cannot reach immunity.
        PhysicalResistance,
        MagicalResistance
    }
}
