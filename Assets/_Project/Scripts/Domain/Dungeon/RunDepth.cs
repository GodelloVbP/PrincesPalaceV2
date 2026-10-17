namespace PrincesPalace.Domain.Dungeon
{
    // How deep a run is, in floors.
    //
    // ONE FORMULA for the floor: FloorFor derives it from the leg boundary,
    // and RunManager mirrors that same number into run.floor when a leg
    // advances. The stored field stays because the lintel and the save format
    // read it; nothing computes it a second way.
    public static class RunDepth
    {
        // A realm's spine is RealmMap.SpineLength steps, and legStartStep
        // moves by exactly that when the party descends, however many pocket
        // rooms the route took. Floors are 1-based because they are shown to
        // a player, not used as an index.
        public static int FloorFor(int legStartStep)
        {
            if (legStartStep <= 0) return 1;
            return legStartStep / RealmMap.SpineLength + 1;
        }
    }
}
