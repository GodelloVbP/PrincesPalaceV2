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
    // are knowable from the map; so is a treasure stash. A shop's stock and an
    // event's page are rolled on arrival (RunOrchestrator.ArriveAt) and stay out
    // of the facts, which MapNodeCard words as "learn it when you step in".
    // Nothing here draws from a stream the real room will not open again.
    public static class MapNodePreview
    {
        public static MapNodeFacts For(RunSnapshot run, DescentNode node)
        {
            if (run == null || node == null) return default;

            switch (node.Type)
            {
                case RoomType.Fight:
                case RoomType.EliteFight:
                case RoomType.Boss:
                    return FightFacts(run, node);

                case RoomType.Treasure:
                    int stash = RoomResolution.Resolve(node.Type,
                        RoomResolver.TreasureStream(run, run.legStartStep + node.Depth, node.Id)).Gold;
                    return new MapNodeFacts(node.Type, stashGold: stash);

                default:
                    return new MapNodeFacts(node.Type);
            }
        }

        private static MapNodeFacts FightFacts(RunSnapshot run, DescentNode node)
        {
            var roll = RunEncounter.Preview(run, node);

            var enemies = roll.EnemyIds
                .Select(id => ContentDatabase.GetEnemy(id)?.Data)
                .Where(data => data != null)
                .ToList();

            var payout = VictoryRewards.ForEnemies(enemies, roll.IsElite, run.legStartStep + node.Depth);

            var names = enemies
                .Select(data => string.IsNullOrEmpty(data.DisplayName) ? data.Id : data.DisplayName)
                .ToList();

            return new MapNodeFacts(node.Type, names, payout.Experience, payout.Gold);
        }
    }
}
