namespace PrincesPalace.Domain.Dungeon
{
    // How deep a run is, in floors.
    //
    // ONE FORMULA, because there were nearly two. The step counter is the thing
    // everything else is really keyed on -- the difficulty curve compounds per
    // step, the rng streams are seeded by it -- but "floor" is what the player
    // is told and what the enemy bands are written in, and a run carries a
    // `floor` field of its own that was set to 1 when the run began and never
    // incremented again.
    //
    // So this derives the floor from the leg boundary, and RunManager writes
    // that same number back into the save when a leg advances. The stored field
    // stays because the lintel and the save format read it; what changed is
    // that nothing computes it a second way.
    public static class RunDepth
    {
        // A leg is DescentMapGenerator.DefaultLegLength rooms, and legStartStep
        // moves by exactly that when the party descends. Floors are 1-based
        // because they are shown to a player, not used as an index.
        public static int FloorFor(int legStartStep)
        {
            if (legStartStep <= 0) return 1;
            return legStartStep / DescentMapGenerator.DefaultLegLength + 1;
        }
    }
}
