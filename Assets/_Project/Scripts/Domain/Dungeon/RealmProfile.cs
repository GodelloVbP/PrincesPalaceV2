using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Content;

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
    // the slots the shape has. A designed realm authors one of these in
    // realms.json (RealmEntryResolver).
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
        // Treasure, Rest and Cache. Each role filters the table to what it may
        // hold (RealmMapGenerator), so Treasure only rolls in pockets, Rest
        // only on a fork sibling, once, and Cache only in a pocket, once.
        // Shop and anchors are placed, not rolled.
        public List<RoomWeight> waypointWeights = new List<RoomWeight>();

        // The chip a Fight node gets. All zero gives None.
        public List<PrizeWeight> prizeWeights = new List<PrizeWeight>();

        // The chip an anchor gets. All zero means an anchor pays exp and gold
        // only (docs/PLAN_ITEMS.md section 5, amendment 2).
        public List<PrizeWeight> anchorPrizeWeights = new List<PrizeWeight>();

        // Enemy ids the boss is drawn from at realm open. Empty means every
        // boss in content. Read by Core, never by the map generator.
        public List<string> bossPool = new List<string>();

        // Rest on the spine node at offset 7 (reward-track level 30).
        public bool restBeforeBoss;

        // A copy that shares no list with this one. A designed realm's profile
        // lives in a content asset; the run saves and mutates its own copy.
        public RealmProfile Clone() => new RealmProfile
        {
            pocketCount = pocketCount,
            pocketLengthMin = pocketLengthMin,
            pocketLengthMax = pocketLengthMax,
            forkCount = forkCount,
            anchorCount = anchorCount,
            shopChance = shopChance,
            waypointWeights = new List<RoomWeight>(waypointWeights),
            prizeWeights = new List<PrizeWeight>(prizeWeights),
            anchorPrizeWeights = new List<PrizeWeight>(anchorPrizeWeights),
            bossPool = new List<string>(bossPool),
            restBeforeBoss = restBeforeBoss,
        };
    }

    // The profile for every floor without a designed realm, floor 100
    // included (docs/PLAN_RUN_MAP.md section 4, "The default profile").
    // Shape is bounded by the spine; depth shows through what grows below it.
    public static class RealmProfiles
    {
        public const int MaxDefaultForks = 3;
        public const int MaxDefaultAnchors = 6;
        public const int MaxDefaultPocketLength = 4;

        // What the map header calls a realm that has no designed name.
        public const string DefaultName = "The Descent";

        // The profile a realm opening at `legStartStep` resolves to when no
        // realm is designed there.
        public static RealmProfile ForRealm(int legStartStep, bool restBeforeBoss = false) =>
            Default(RunDepth.FloorFor(legStartStep), restBeforeBoss);

        // The profile and name a realm opening at `legStartStep` resolves to:
        // the designed realm of that floor, else the default table and
        // DefaultName. RealmOpening calls this once per realm and saves both.
        // The result owns its profile: the run's own restBeforeBoss flag only
        // ever turns the rest on, and never writes back into content.
        public static ResolvedRealm Resolve(int legStartStep, bool restBeforeBoss,
            IReadOnlyList<ResolvedRealm> designed)
        {
            int floor = RunDepth.FloorFor(legStartStep);

            if (designed != null)
            {
                for (int i = 0; i < designed.Count; i++)
                {
                    var realm = designed[i];
                    if (realm == null || realm.Floor != floor || realm.Profile == null) continue;

                    var profile = realm.Profile.Clone();
                    profile.restBeforeBoss |= restBeforeBoss;
                    return new ResolvedRealm(floor, realm.Name, profile);
                }
            }

            return new ResolvedRealm(floor, DefaultName, Default(floor, restBeforeBoss));
        }

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

            // Prize weights are item phase I3's starting mix, set by hand and
            // left to I5's bot batches: a plain fight pays gold, a consumable,
            // Gear (a page) or a Book, never a Relic; an anchor pays Gear or
            // Relic. The waypoint weights are P1's and no sheet has set them yet.
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
                    new RoomWeight(RoomType.Cache, 6),
                },
                prizeWeights = new List<PrizeWeight>
                {
                    new PrizeWeight(PrizeKind.Gold, 40),
                    new PrizeWeight(PrizeKind.Consumable, 30),
                    new PrizeWeight(PrizeKind.Item, 20),
                    new PrizeWeight(PrizeKind.Book, 10),
                    new PrizeWeight(PrizeKind.Relic, 0),
                },
                anchorPrizeWeights = new List<PrizeWeight>
                {
                    new PrizeWeight(PrizeKind.Gold, 0),
                    new PrizeWeight(PrizeKind.Consumable, 0),
                    new PrizeWeight(PrizeKind.Item, 70),
                    new PrizeWeight(PrizeKind.Book, 0),
                    new PrizeWeight(PrizeKind.Relic, 30),
                },
                restBeforeBoss = restBeforeBoss,
            };
        }
    }
}
