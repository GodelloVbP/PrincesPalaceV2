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
    // Defense, the single generic mitigation stat, does not exist. Physical
    // Defense and Magical Defense are the only defensive stats a
    // combatant carries. See DamagePipeline.AfterDefences
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
        MagicalDefense,

        // CRITICAL HITS. APPENDED, never
        // inserted: content stores these as ordinals. Both are BONUSES on top
        // of the party baseline in CritRules (5% chance, 150% damage), so a
        // block nobody authored still means "the baseline" and every existing
        // StatBlock keeps its meaning. Percent points, not ratings: +3 chance
        // is 8% total, +25 damage is a 175% crit. See CritRules for the whole
        // rule and DamagePipeline.AfterDefences for where it lands.
        CritChance,
        CritDamage
    }
}
