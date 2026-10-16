using System.Collections.Generic;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.UiKit
{
    // Where a room stands relative to the party. The only thing a tile's tint
    // says, and the only thing a refused press needs to explain itself.
    //
    // Five rather than three, so "cleared" and "never reachable" do not render
    // identically dim, which would make a leg's history unreadable.
    public enum MapTileState { Current, Walked, Open, Ahead, Closed }

    // What a fight node's chip pays, as the card shows it: the kind after an
    // empty Book or Relic pool turned it to gold (RealmPrize.Effective), and
    // the gold or the potion count. Gear, Book and Relic pay a pick, Amount 0.
    public readonly struct MapNodeChip
    {
        public readonly PrizeKind Kind;
        public readonly int Amount;

        public MapNodeChip(PrizeKind kind, int amount)
        {
            Kind = kind;
            Amount = amount;
        }
    }

    // What the run has already decided about a room, handed to the card as
    // plain data so the wording is pure and pinnable.
    public readonly struct MapNodeFacts
    {
        public readonly RoomType Type;
        public readonly IReadOnlyList<string> EnemyNames;

        // Base payout before relics; what VictoryRewards.For says for these
        // enemies at the room's own step.
        public readonly int Experience;
        public readonly int Gold;

        // The treasure stream's draw for this node.
        public readonly int StashGold;

        // The realm's own facts (docs/PLAN_RUN_MAP.md sections 2-3): the
        // frozen strength (0 for a room with none), the chip, the skip heal a
        // spine room pays for walking past a pocket, and whether an anchor
        // left alive will guard the boss.
        public readonly int Strength;
        public readonly MapNodeChip Chip;
        public readonly int SkipHealPercent;
        public readonly bool GuardsHeart;

        // How many rooms stepping here shuts off for good; 0 for most choices.
        public readonly int ClosesRooms;

        // An event room's title, as the run's own roll will open it; "" for
        // any other room, or when no event is eligible.
        public readonly string EventTitle;

        public MapNodeFacts(RoomType type, IReadOnlyList<string> enemyNames = null, int experience = 0,
                            int gold = 0, int stashGold = 0)
            : this(type, enemyNames, experience, gold, stashGold, 0, default, 0, false, 0, "")
        {
        }

        private MapNodeFacts(RoomType type, IReadOnlyList<string> enemyNames, int experience, int gold,
                             int stashGold, int strength, MapNodeChip chip, int skipHealPercent, bool guardsHeart,
                             int closesRooms, string eventTitle)
        {
            Type = type;
            EnemyNames = enemyNames ?? new string[0];
            Experience = experience;
            Gold = gold;
            StashGold = stashGold;
            Strength = strength;
            Chip = chip;
            SkipHealPercent = skipHealPercent;
            GuardsHeart = guardsHeart;
            ClosesRooms = closesRooms;
            EventTitle = eventTitle ?? "";
        }

        public MapNodeFacts WithRealm(int strength, MapNodeChip chip, int skipHealPercent, bool guardsHeart) =>
            new MapNodeFacts(Type, EnemyNames, Experience, Gold, StashGold, strength, chip, skipHealPercent,
                guardsHeart, ClosesRooms, EventTitle);

        public MapNodeFacts WithCloses(int rooms) =>
            new MapNodeFacts(Type, EnemyNames, Experience, Gold, StashGold, Strength, Chip, SkipHealPercent,
                GuardsHeart, rooms, EventTitle);

        public MapNodeFacts WithEventTitle(string title) =>
            new MapNodeFacts(Type, EnemyNames, Experience, Gold, StashGold, Strength, Chip, SkipHealPercent,
                GuardsHeart, ClosesRooms, title);
    }

    // THE ONE LINE OF MAP COPY THAT NAMES A KEY: the overview hint, and the
    // banner that replaces it while the overview is up. One home so the
    // controller, the tests and the build read the same text.
    public static class MapNodeCard
    {
        // The key that toggles the overview on this device, in capitals. Empty
        // when the device has none, which hides the hint instead of naming a
        // key the player cannot press.
        public static string OverviewText(bool inOverview, PromptDevice device)
        {
            var glyph = PromptGlyphs.For(device, PromptButton.Overview);
            if (!glyph.IsSet) return string.Empty;

            string key = glyph.Text.ToUpperInvariant();
            return inOverview ? UiStrings.MapOverview.Format(key) : UiStrings.MapOverviewHint.Format(key);
        }
    }
}
