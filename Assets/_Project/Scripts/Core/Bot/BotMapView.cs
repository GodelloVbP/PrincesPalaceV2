using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace
{
    // The realm map as the party standing on the current node sees it: the
    // frozen strengths and the chips the cards show, which is all a player
    // reads off the map screen.
    public static class BotMapView
    {
        public static MapView Current()
        {
            var run = RunManager.Run;
            var map = RunManager.Map;
            if (run == null || map == null) return null;

            return MapView.Build(map, RunManager.CurrentNode,
                node => RealmOpening.StrengthOf(run, node.Id),
                node => RealmPrizes.ChipOf(run, node).Kind);
        }
    }
}
