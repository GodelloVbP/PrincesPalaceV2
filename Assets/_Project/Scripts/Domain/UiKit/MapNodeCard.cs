using System.Collections.Generic;
using System.Linq;
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

        public MapNodeFacts(RoomType type, IReadOnlyList<string> enemyNames = null, int experience = 0,
                            int gold = 0, int stashGold = 0)
            : this(type, enemyNames, experience, gold, stashGold, 0, default, 0, false)
        {
        }

        private MapNodeFacts(RoomType type, IReadOnlyList<string> enemyNames, int experience, int gold,
                             int stashGold, int strength, MapNodeChip chip, int skipHealPercent, bool guardsHeart)
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
        }

        public MapNodeFacts WithRealm(int strength, MapNodeChip chip, int skipHealPercent, bool guardsHeart) =>
            new MapNodeFacts(Type, EnemyNames, Experience, Gold, StashGold, strength, chip, skipHealPercent,
                guardsHeart);
    }

    // THE WORDS ON THE MAP'S ROOMS: the caption under each tile, the detail
    // shown for a hovered or focused one, and the reason a refused press gives.
    // One home so the controller, the tests and the build read the same text.
    public static class MapNodeCard
    {
        public static string Caption(RoomType type)
        {
            switch (type)
            {
                case RoomType.Entry: return "START";
                case RoomType.Fight: return "FIGHT";
                case RoomType.EliteFight: return "ELITE";
                case RoomType.Boss: return "BOSS";
                case RoomType.Treasure: return "TREASURE";
                case RoomType.Rest: return "REST";
                case RoomType.Shop: return "SHOP";
                case RoomType.Event: return "EVENT";
                default: return type.ToString().ToUpperInvariant();
            }
        }

        // The card's heading: the caption, and a fight's strength beside it.
        public static string Title(MapNodeFacts facts) =>
            facts.Strength > 0
                ? UiStrings.MapPreviewStrength.Format(Caption(facts.Type), facts.Strength)
                : Caption(facts.Type);

        // Fights name their enemies, the base payout and the chip; a treasure
        // room names its stash. Shop and event pay out of rolls made on
        // arrival, so their lines say so instead of guessing. Last, what the
        // choice itself means: an anchor's promise, or the skip heal.
        public static string Body(MapNodeFacts facts)
        {
            var lines = new List<string>();
            switch (facts.Type)
            {
                case RoomType.Fight:
                case RoomType.EliteFight:
                case RoomType.Boss:
                    if (facts.EnemyNames.Count == 0) break;
                    lines.Add(UiStrings.MapPreviewEnemies.Format(EnemyLine(facts.EnemyNames)));
                    string pay = UiStrings.MapPreviewReward.Format(facts.Gold, facts.Experience);
                    string chip = ChipLine(facts.Chip);
                    lines.Add(chip.Length == 0 ? pay : pay + "  ·  " + chip);
                    break;
                case RoomType.Treasure: lines.Add(UiStrings.MapPreviewTreasure.Format(facts.StashGold)); break;
                case RoomType.Rest: lines.Add(UiStrings.MapPreviewRest.Format()); break;
                case RoomType.Shop: lines.Add(UiStrings.MapPreviewShop.Format()); break;
                case RoomType.Event: lines.Add(UiStrings.MapPreviewEvent.Format()); break;
                case RoomType.Entry: lines.Add(UiStrings.MapPreviewStart.Format()); break;
            }

            if (facts.GuardsHeart) lines.Add(UiStrings.MapPreviewAnchor.Format());
            if (facts.SkipHealPercent > 0) lines.Add(UiStrings.MapPreviewSkipHeal.Format(facts.SkipHealPercent));
            return string.Join("\n", lines);
        }

        public static string ChipLine(MapNodeChip chip)
        {
            switch (chip.Kind)
            {
                case PrizeKind.Gold: return UiStrings.MapPreviewPrizeGold.Format(chip.Amount);
                case PrizeKind.Consumable:
                    return chip.Amount == 1
                        ? UiStrings.MapPreviewPrizePotion.Format()
                        : UiStrings.MapPreviewPrizePotions.Format(chip.Amount);
                case PrizeKind.Item: return UiStrings.MapPreviewPrizeGear.Format();
                case PrizeKind.Book: return UiStrings.MapPreviewPrizeBook.Format();
                case PrizeKind.Relic: return UiStrings.MapPreviewPrizeRelic.Format();
                default: return string.Empty;
            }
        }

        // "Giant Rat ×2, Wolf": first-seen order, repeats counted.
        public static string EnemyLine(IReadOnlyList<string> names) =>
            string.Join(", ", names
                .GroupBy(n => n)
                .Select(g => g.Count() > 1 ? g.Key + " ×" + g.Count() : g.Key));

        // What a press on a room that cannot be entered says. Open is the one
        // state that is enterable, so it has no refusal of its own; it gets the
        // walking reason, the only way an open room's press is ignored.
        public static string Refusal(MapTileState state)
        {
            switch (state)
            {
                case MapTileState.Current: return UiStrings.MapYouAreHere.Format();
                case MapTileState.Walked: return UiStrings.MapRefuseCleared.Format();
                case MapTileState.Ahead: return UiStrings.MapRefuseAhead.Format();
                case MapTileState.Closed: return UiStrings.MapRefuseClosed.Format();
                default: return UiStrings.MapRefuseWalking.Format();
            }
        }
    }
}
