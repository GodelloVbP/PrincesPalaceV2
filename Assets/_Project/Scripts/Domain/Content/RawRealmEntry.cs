using System;

namespace PrincesPalace.Domain.Content
{
    // One designed realm as typed into realms.json: the floor it replaces the
    // default profile on, the name the map header shows, and the RealmProfile
    // the generator shapes it with (docs/PLAN_RUN_MAP.md section 4). A floor
    // with no row here resolves to RealmProfiles.Default.
    //
    // Enum-valued fields are strings for the reason RawSkillEntry.approach
    // gives: JsonUtility writes an enum as its ordinal.
    [Serializable]
    public class RawRealmEntry
    {
        [ContentDoc("The floor this realm is. One row per floor; a floor with no row gets the default profile.")]
        public int floor;

        [ContentDoc("The name the map header shows for this realm.")]
        public string name = "";

        [ContentDoc("Pockets (side branches) on the spine, 1-6.")]
        public int pocketCount = 1;

        [ContentDoc("Fewest rooms in a pocket; at least 1.")]
        public int pocketLengthMin = 1;

        [ContentDoc("Most rooms in a pocket; at least pocketLengthMin, at most 4 (the map layout is sized for it).")]
        public int pocketLengthMax = 2;

        [ContentDoc("Forks on the spine, 0-3 (the map layout is sized for it).")]
        public int forkCount;

        [ContentDoc("Anchors (heart-guarding side fights), 0-6 (the map layout is sized for it).")]
        public int anchorCount = 1;

        [ContentDoc("Chance 0-1 that the realm has a shop.")]
        public float shopChance = 0.75f;

        [ContentDoc("Relative weights of the rooms the generator rolls. kind is Fight, Event, Treasure, Rest or Cache, once each; at least one weight above 0.")]
        public RawRealmWeight[] waypointWeights = Array.Empty<RawRealmWeight>();

        [ContentDoc("Relative weights of the chip a Fight node gets. kind is Gold, Consumable, Item, Book or Relic, once each; all 0 or empty gives no chip.")]
        public RawRealmWeight[] prizeWeights = Array.Empty<RawRealmWeight>();

        [ContentDoc("Relative weights of the chip an anchor gets; same kinds as prizeWeights. All 0 or empty pays exp and gold only.")]
        public RawRealmWeight[] anchorPrizeWeights = Array.Empty<RawRealmWeight>();

        [ContentDoc("Enemy ids the boss is drawn from. Empty means every boss.")]
        public string[] bossPool = Array.Empty<string>();

        [ContentDoc("A Rest room on the spine node before the boss.")]
        public bool restBeforeBoss;
    }

    // One {kind, weight} pair of a weight table. kind is parsed against
    // RoomType for waypointWeights and PrizeKind for the two prize tables.
    [Serializable]
    public class RawRealmWeight
    {
        [ContentDoc("The room type or chip kind this weight is for.")]
        public string kind = "";

        [ContentDoc("Relative weight, 0 or more.")]
        public int weight;
    }

    // JsonUtility can't deserialize a bare top-level JSON array, so
    // realms.json is one object with a "realms" array inside it.
    [Serializable]
    public class RawRealmFile
    {
        [ContentDoc("This file's rows; see RawRealmEntry.")]
        public RawRealmEntry[] realms = Array.Empty<RawRealmEntry>();
    }
}
