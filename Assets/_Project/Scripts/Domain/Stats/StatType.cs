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
    //
    // Defense (the old single generic mitigation stat) is GONE. Physical
    // Defense and Magical Defense — formerly PhysicalResistance and
    // MagicalResistance, renamed rather than re-added — are now the only
    // defensive stats a combatant carries. See DamagePipeline.AfterDefences
    // for the one place they are read.
    public enum StatType
    {
        MaxHealth,
        Speed,
        Attack,

        // Mana returned at the start of each of this combatant's turns.
        ManaRegen,

        // Damage reduction against Physical, and against every other damage
        // type respectively. Points rather than percent — CombatMath turns
        // them into a reduction on a curve that cannot reach immunity. See
        // DamagePipeline's canonical mitigation equation.
        PhysicalDefense,
        MagicalDefense
    }
}
