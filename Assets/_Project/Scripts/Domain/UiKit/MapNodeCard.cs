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

        public MapNodeFacts(RoomType type, IReadOnlyList<string> enemyNames = null, int experience = 0,
                            int gold = 0, int stashGold = 0)
        {
            Type = type;
            EnemyNames = enemyNames ?? new string[0];
            Experience = experience;
            Gold = gold;
            StashGold = stashGold;
        }
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

        // Fights name their enemies and the base payout; a treasure room names
        // its stash. Shop and event pay out of rolls made on arrival, so their
        // lines say so instead of guessing.
        public static string Body(MapNodeFacts facts)
        {
            switch (facts.Type)
            {
                case RoomType.Fight:
                case RoomType.EliteFight:
                case RoomType.Boss:
                    if (facts.EnemyNames.Count == 0) return string.Empty;
                    return UiStrings.MapPreviewEnemies.Format(EnemyLine(facts.EnemyNames)) + "\n"
                        + UiStrings.MapPreviewReward.Format(facts.Gold, facts.Experience);
                case RoomType.Treasure: return UiStrings.MapPreviewTreasure.Format(facts.StashGold);
                case RoomType.Rest: return UiStrings.MapPreviewRest.Format();
                case RoomType.Shop: return UiStrings.MapPreviewShop.Format();
                case RoomType.Event: return UiStrings.MapPreviewEvent.Format();
                case RoomType.Entry: return UiStrings.MapPreviewStart.Format();
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
