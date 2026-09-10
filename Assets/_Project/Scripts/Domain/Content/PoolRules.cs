namespace PrincesPalace.Domain.Content
{
    // THE THREE CLOSED VOCABULARIES A POOL IS AUTHORED IN.
    //
    // One file rather than three, and not for brevity: they are only
    // meaningful together. A pool row states what its capacity IS, what it
    // opens a fight AT, and what counts as having done something this turn --
    // and reading one of those without the other two tells you nothing about
    // the pool. Declared under Domain/Content/, which ContentInputHash hashes
    // wholesale, so adding them needs no ContentInputHash.Sources line.
    //
    // Parsed case-insensitively by PoolEntryResolver, the way CharacterRole
    // and DamageType are by CharacterEntryResolver -- so the valid names
    // print themselves into docs/CONTENT_SCHEMA.md off Enum.GetNames and
    // cannot go stale.

    // Where a pool's capacity number comes from.
    //
    // WisdomDerived is mana as it has always worked: the authored number is a
    // BASE, and every Max Mana source in the game (AbilityDerivation's Wisdom
    // bonus, talents, relics, the reward track, FlatMaxManaBonus) adds to it.
    // Fixed is the opposite promise, and it is the whole reason this is an
    // enum rather than a bool on a number: a Fixed pool is EXACTLY its
    // authored capacity from every source, which is what lets a resource say
    // "this bar is 0..100, always" and have that be literally true rather
    // than true until somebody equips a relic. See the plan's P3 and attack
    // point 3. Nothing consumes this yet -- phase B is what teaches
    // ContentDatabase's capacity chain to respect it.
    public enum PoolCapacityRule
    {
        WisdomDerived,
        Fixed,
    }

    // What a pool holds at the start of a fight.
    //
    // Full is mana: a budget you spend down. Zero is a rage bar: an arc that
    // starts empty and is earned. Value exists because the two obvious
    // answers are not the only defensible ones -- a resource that opens at a
    // third of capacity is a different feel from either -- and adding it
    // costs one arm and one authored int rather than a second start concept
    // later.
    public enum PoolStartRule
    {
        Full,
        Zero,
        Value,
    }

    // What stops a turn counting as idle, and therefore what stops
    // decayPerIdleTurn from firing.
    //
    // Damage is the narrow reading and today's only shipped one: only damage
    // DEALT or damage TAKEN keeps the pool; a turn spent on Provoke, an item
    // or a Move is idle and decays. AnyAction is the wider reading, authored
    // rather than coded so the owner can widen it without a code change if
    // the narrow one plays badly. See the plan's P4 and attack point 5.
    public enum PoolDecayTrigger
    {
        Damage,
        AnyAction,
    }
}
