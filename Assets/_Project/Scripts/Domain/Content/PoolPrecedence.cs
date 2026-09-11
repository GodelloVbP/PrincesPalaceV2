using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Content
{
    // WHICH SOURCES A POOL'S CAPACITY AND INCOME ACTUALLY LISTEN TO, and the
    // only place the two capacity rules differ.
    //
    // The plan's P3 puts this statement in ContentDatabase.BuildPrimaryPool,
    // and that is still where the CHAIN is assembled -- talents, the reward
    // track and a character's live loadout are Core's to read. What lives
    // here is the RULE those sums are fed into, for one reason: a rule that
    // can only be exercised through a content catalogue can only be tested
    // against the rows that catalogue ships, and PoolCapacityRule.Fixed's
    // promise is now exercised for real -- pools.json's `fury` row is
    // authored Fixed, and this is what stops every Max Mana source from
    // adding to it.
    //
    //   WisdomDerived: the authored number is a BASE. Every Max Mana source
    //   in the game adds to it -- the Wisdom bonus, talents, relics, the
    //   reward track, a gear modifier. That is mana as it has always worked.
    //
    //   Fixed: the authored number is the WHOLE number, from every source. A
    //   bar that says "0..100" is 0..100 after a relic, after a talent,
    //   after anything. The consequence, priced in rather than hidden: a Max
    //   Mana talent or relic is a dead pick for its owner, and the character
    //   sheet says so by dropping the Wisdom link (SheetStats.FedBy).
    //
    // Engine-free and pure, like everything else in this folder.
    public static class PoolPrecedence
    {
        // `derivedBonuses` is every additive Max Mana term the caller has
        // already summed; `flatBonus` is the gear modifier applied AFTER
        // relics, because RelicModifiers.Apply reads RelicModifier and never
        // ModifierEffect, so the two cannot be summed before the multiply.
        // That ordering is not new -- it is what FightEncounterAdapter did
        // inline before the pool model, preserved here rather than tidied.
        public static int Capacity(ResolvedPool definition, int derivedBonuses,
                                   IReadOnlyList<RelicModifier> modifiers, int flatBonus)
        {
            if (definition == null) return 0;

            if (definition.CapacityRule != PoolCapacityRule.WisdomDerived)
            {
                return Math.Max(0, definition.Capacity);
            }

            int total = Math.Max(0, definition.Capacity + derivedBonuses);
            total = RelicModifiers.Apply(total, RelicStat.MaxMana, modifiers);
            return Math.Max(0, total + flatBonus);
        }

        // THE SAME SHAPE FOR INCOME, deliberately: a pool whose capacity
        // ignores every outside source and whose regen did not would be two
        // rules wearing one word. Mana authors a base of 0 and derives all
        // of its regen, which is why the row reading 0 does not mean mana
        // stopped regenerating.
        public static int GainPerTurn(ResolvedPool definition, int derivedBonuses)
        {
            if (definition == null) return 0;

            return definition.CapacityRule == PoolCapacityRule.WisdomDerived
                ? Math.Max(0, definition.GainPerTurn + derivedBonuses)
                : Math.Max(0, definition.GainPerTurn);
        }
    }
}
