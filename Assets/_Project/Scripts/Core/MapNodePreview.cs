using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // What the run has already decided about a room the party has not entered.
    //
    // Fights are keyed to (seed, step, node), so the enemies and the base payout
    // are knowable from the map; so is a treasure stash, a fight's frozen
    // strength and its chip. A shop's stock is rolled on arrival
    // (RunOrchestrator.ArriveAt) and stays out of the facts, which MapNodeCard
    // words as "learn it when you step in". An event's TITLE is the same roll
    // arrival makes (RunOrchestrator.DecideEvent), so the card names it. Nothing here
    // draws from a stream the real room will not open again.
    public static class MapNodePreview
    {
        public static MapNodeFacts For(RunSnapshot run, RealmNode node)
        {
            if (run == null || node == null) return default;

            var map = RunManager.Map;
            var current = RunManager.CurrentNode;
            int skipHeal = map != null && current != null && map.PocketSkippedBy(current.Id, node.Id) != null
                ? RealmMap.SkipHealPercent
                : 0;
            int strength = RealmOpening.StrengthOf(run, node.Id);
            var chip = RealmPrizes.ChipOf(run, node);

            return Base(run, node).WithRealm(strength, chip, skipHeal, guardsHeart: node.IsAnchor);
        }

        private static MapNodeFacts Base(RunSnapshot run, RealmNode node)
        {
            switch (node.Type)
            {
                case RoomType.Fight:
                case RoomType.EliteFight:
                case RoomType.Boss:
                    return FightFacts(run, node);

                case RoomType.Treasure:
                    int stash = RoomResolution.Resolve(node.Type,
                        RoomResolver.TreasureStream(run, run.legStartStep + node.Offset, node.Id)).Gold;
                    return new MapNodeFacts(node.Type, stashGold: stash);

                case RoomType.Event:
                    return new MapNodeFacts(node.Type).WithEventTitle(RunOrchestrator.PreviewEventTitle(run, node));

                default:
                    return new MapNodeFacts(node.Type);
            }
        }

        private static MapNodeFacts FightFacts(RunSnapshot run, RealmNode node)
        {
            var roll = RunEncounter.Preview(run, node);

            var enemies = roll.EnemyIds
                .Select(id => ContentDatabase.GetEnemy(id)?.Data)
                .Where(data => data != null)
                .ToList();

            var payout = VictoryRewards.ForEnemies(enemies, roll.IsElite, run.legStartStep + node.Offset);

            var names = enemies
                .Select(data => string.IsNullOrEmpty(data.DisplayName) ? data.Id : data.DisplayName)
                .ToList();

            return new MapNodeFacts(node.Type, names, payout.Experience, payout.Gold);
        }
    }
}
