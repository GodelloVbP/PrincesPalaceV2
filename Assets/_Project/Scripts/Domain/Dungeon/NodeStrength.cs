using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Dungeon
{
    // One fighter reduced to the two numbers strength compares: scaled max HP
    // and attack, exactly as the fight would field them.
    public readonly struct FighterBody
    {
        public readonly int MaxHealth;
        public readonly int Attack;

        public FighterBody(int maxHealth, int attack)
        {
            MaxHealth = maxHealth;
            Attack = attack;
        }

        // Double, not long: deep enemies scale toward int.MaxValue on both
        // axes and three of them would overflow a long product sum.
        public double Weight => (double)MaxHealth * Attack;
    }

    // How hard a fight node is against the party, 1 to 3 (docs/PLAN_RUN_MAP.md
    // section 2, Strength). Derived in Core at realm open from each node's
    // preview roll and frozen in the save, so levelling mid-realm never
    // re-labels a node the player already judged.
    //
    // r = threat / party, with threat and party each the sum of max HP x
    // attack. T1 and T2 are the P0 EV sheet's literals
    // (docs/handoffs/run_map/EV_SHEET.md section 2): the 40th and 80th
    // percentile of r on floors 1-10 against the reference party
    // ReferencePartyWeight. They hold while the real party at realm open is
    // near that figure; RealmOpening logs the real one so the sheet can be
    // redone against it.
    public static class NodeStrength
    {
        public const double T1 = 0.34;
        public const double T2 = 0.61;

        // Shawn, Bjorn and Odette unarmed: 200x7 + 260x10 + 140x4.
        public const double ReferencePartyWeight = 4560;

        public const int Min = 1;
        public const int Max = 3;

        public static double Weight(IEnumerable<FighterBody> fighters)
        {
            double total = 0;
            if (fighters == null) return total;
            foreach (var fighter in fighters) total += fighter.Weight;
            return total;
        }

        // A party that weighs nothing (a wipe, no content) cannot be compared
        // against; every node reads as the hardest rather than as free.
        public static int Of(double threat, double party)
        {
            if (party <= 0) return Max;
            double ratio = threat / party;
            if (ratio < T1) return 1;
            if (ratio < T2) return 2;
            return Max;
        }
    }

    // One node's frozen strength, as a list entry because JsonUtility cannot
    // write a dictionary (RunSnapshot.nodeStrength).
    [Serializable]
    public struct NodeStrengthEntry
    {
        public int nodeId;
        public int strength;

        public NodeStrengthEntry(int nodeId, int strength)
        {
            this.nodeId = nodeId;
            this.strength = strength;
        }

        // 0 when the node has no entry: not a fight, or a realm opened before
        // strength existed. Callers treat 0 as "no strength", never as weak.
        public static int Find(IReadOnlyList<NodeStrengthEntry> entries, int nodeId)
        {
            if (entries == null) return 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].nodeId == nodeId) return entries[i].strength;
            }

            return 0;
        }
    }
}
