using System;

namespace PrincesPalace.Domain.Content
{
    // One reward source as typed into rewards.json (docs/PLAN_ITEMS.md 2.4):
    // the dials one settlement page of this class rolls with. A new source
    // on existing kinds is one row here and no code.
    [Serializable]
    public class RawRewardSourceEntry
    {
        [ContentDoc("Unique id. Named <class>_<kind>: normal_gear, anchor_gear, boss_gear, anchor_relic, boss_relic (RewardSources.IdFor).")]
        public string id;

        [ContentDoc("Which page this source fills.")]
        public string kind;

        [ContentDoc("Cards shown beyond one per chip: a page of k chips shows k + extraCards cards and grants k choices.")]
        public int extraCards = 2;

        [ContentDoc("Gear: added to RarityTable.FloorTier(step) to centre the tier roll (boss +1).")]
        public int tierOffset;

        [ContentDoc("Gear: per-rung chance of the tier climb at Favor 0, 0-1 (normal 0.22, anchor 0.30, boss 0.38).")]
        public float tierStep;

        [ContentDoc("Gear: per-rung chance of the quality climb at Favor 0, 0-1 (normal 0.35, anchor 0.42, boss 0.45).")]
        public float ladderStep;

        [ContentDoc("Gear: the quality every card starts at; the climb has the rungs left above it.")]
        public string qualityFloor = "Common";

        [ContentDoc("Gear: one card on the page is a weapon card (step 2).")]
        public bool weaponGuarantee;

        [ContentDoc("Gear: the least quality the guaranteed weapon card shows (boss Rare). Common means no floor.")]
        public string weaponFloor = "Common";

        [ContentDoc("Relic: draw weight per RelicRarity, six ints in enum order, Common first.")]
        public int[] relicWeights = Array.Empty<int>();

        [ContentDoc("Relic: the lowest rarity this page may show.")]
        public string relicRarityMin = "Common";

        [ContentDoc("What a choice left unused on leaving the page pays.")]
        public string unusedPays;

        [ContentDoc("The RngStreams constant the page rolls on, by name (Reward).")]
        public string stream = "Reward";

        [ContentDoc("Display and load order.")]
        public int sortOrder;
    }

    // JsonUtility can't deserialize a bare top-level JSON array.
    [Serializable]
    public class RawRewardSourceFile
    {
        [ContentDoc("This file's rows; see RawRewardSourceEntry.")]
        public RawRewardSourceEntry[] sources = Array.Empty<RawRewardSourceEntry>();
    }
}
