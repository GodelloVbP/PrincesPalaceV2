using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Dungeon
{
    [Serializable]
    public struct RoomWeight
    {
        public RoomType type;
        public int weight;

        public RoomWeight(RoomType type, int weight)
        {
            this.type = type;
            this.weight = weight;
        }
    }

    [Serializable]
    public struct PrizeWeight
    {
        public PrizeKind kind;
        public int weight;

        public PrizeWeight(PrizeKind kind, int weight)
        {
            this.kind = kind;
            this.weight = weight;
        }
    }

    // The shape one realm is generated into. The generator reads no content:
    // the profile is resolved once when the realm opens, saved beside
    // bossEnemyId, and RealmMap reads it back from the save. Lists of
    // {kind, weight} structs because JsonUtility cannot write dictionaries
    // (docs/PLAN_RUN_MAP.md section 4).
    //
    // The generator clamps what it is given rather than trusting it: pockets
    // to 1..RealmMap.MaxPockets, lengths to at least 1, forks and anchors to
    // the slots the shape has. A designed realm (P8) authors one of these.
    [Serializable]
    public sealed class RealmProfile
    {
        public int pocketCount = 1;
        public int pocketLengthMin = 1;
        public int pocketLengthMax = 2;
        public int forkCount;
        public int anchorCount = 1;

        // Chance 0..1 that the realm has a shop. The shop sits in a pocket
        // room no anchor took, so a realm with no such room has none.
        public float shopChance = 0.75f;

        // Relative weights for the rooms the generator rolls: Fight, Event,
        // Treasure and Rest. Each role filters the table to what it may hold
        // (RealmMapGenerator), so Treasure only rolls in pockets and Rest only
        // on a fork sibling, once. Shop and anchors are placed, not rolled.
        // Cache joins here with its RoomType in P7.
        public List<RoomWeight> waypointWeights = new List<RoomWeight>();

        // The chip a Fight node gets. All zero gives None.
        public List<PrizeWeight> prizeWeights = new List<PrizeWeight>();

        // The chip an anchor gets. All zero (the default until item phase I3)
        // means an anchor pays exp and gold only (docs/PLAN_ITEMS.md section 5,
        // amendment 2).
        public List<PrizeWeight> anchorPrizeWeights = new List<PrizeWeight>();

        // Enemy ids the boss is drawn from at realm open. Empty means every
        // boss in content. Read by Core, never by the map generator.
        public List<string> bossPool = new List<string>();

        // Rest on the spine node at offset 7 (reward-track level 30).
        public bool restBeforeBoss;
    }

    // The profile for every floor without a designed realm, floor 100
    // included (docs/PLAN_RUN_MAP.md section 4, "The default profile").
    // Shape is bounded by the spine; depth shows through what grows below it.
    public static class RealmProfiles
    {
        public const int MaxDefaultForks = 3;
        public const int MaxDefaultAnchors = 6;
        public const int MaxDefaultPocketLength = 4;

        public static RealmProfile Default(int floor, bool restBeforeBoss = false)
        {
            if (floor < 1) floor = 1;

            int pocketLengthMax = 2 + (floor > 2 ? (floor - 2) / 6 : 0);
            if (pocketLengthMax > MaxDefaultPocketLength) pocketLengthMax = MaxDefaultPocketLength;

            int pockets = 1 + floor / 2;
            if (pockets > RealmMap.MaxPockets) pockets = RealmMap.MaxPockets;

            int forks = floor / 4;
            if (forks > MaxDefaultForks) forks = MaxDefaultForks;

            int anchors = 1 + floor / 3;
            if (anchors > MaxDefaultAnchors) anchors = MaxDefaultAnchors;

            // Placeholder literals until the P0 EV sheet sets them.
            return new RealmProfile
            {
                pocketCount = pockets,
                pocketLengthMin = 1,
                pocketLengthMax = pocketLengthMax,
                forkCount = forks,
                anchorCount = anchors,
                shopChance = 0.75f,
                waypointWeights = new List<RoomWeight>
                {
                    new RoomWeight(RoomType.Fight, 60),
                    new RoomWeight(RoomType.Event, 20),
                    new RoomWeight(RoomType.Treasure, 12),
                    new RoomWeight(RoomType.Rest, 8),
                },
                prizeWeights = new List<PrizeWeight>
                {
                    new PrizeWeight(PrizeKind.Gold, 60),
                    new PrizeWeight(PrizeKind.Consumable, 40),
                    new PrizeWeight(PrizeKind.Item, 0),
                    new PrizeWeight(PrizeKind.Book, 0),
                    new PrizeWeight(PrizeKind.Relic, 0),
                },
                anchorPrizeWeights = new List<PrizeWeight>
                {
                    new PrizeWeight(PrizeKind.Gold, 0),
                    new PrizeWeight(PrizeKind.Consumable, 0),
                    new PrizeWeight(PrizeKind.Item, 0),
                    new PrizeWeight(PrizeKind.Book, 0),
                    new PrizeWeight(PrizeKind.Relic, 0),
                },
                restBeforeBoss = restBeforeBoss,
            };
        }
    }
}
