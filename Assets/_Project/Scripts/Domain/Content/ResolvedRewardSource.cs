using System;

namespace PrincesPalace.Domain.Content
{
    // Which settlement page a reward source fills (docs/PLAN_ITEMS.md 1.2).
    public enum RewardPageKind
    {
        Gear,
        Relic,
    }

    // What one choice left unused on leaving a page pays.
    public enum UnusedChoicePays
    {
        // +1 honing on the first shown card's slot (gear page).
        Hone,

        // 30% of the cheapest shown card's price (relic page).
        CheapestSale,
    }

    // One validated rewards.json row, and the shape RewardSourceDefinition
    // stores. A plain mutable class written once by the resolver
    // (.claude/rules/content.md: a readonly record round-trips as default).
    [Serializable]
    public sealed class ResolvedRewardSource
    {
        public string Id = "";
        public RewardPageKind Kind;
        public int ExtraCards;
        public int TierOffset;
        public float TierStep;
        public float LadderStep;
        public Quality QualityFloor;
        public bool WeaponGuarantee;
        public Quality WeaponFloor;

        // Indexed by (int)RelicRarity; always RelicRarityCount long.
        public int[] RelicWeights = new int[RelicRarityCount];
        public RelicRarity RelicRarityMin;
        public UnusedChoicePays UnusedPays;
        public uint Stream;
        public int SortOrder;

        public const int RelicRarityCount = 6;

        public int RelicWeightOf(RelicRarity rarity)
        {
            int i = (int)rarity;
            return RelicWeights != null && i >= 0 && i < RelicWeights.Length ? RelicWeights[i] : 0;
        }
    }
}
