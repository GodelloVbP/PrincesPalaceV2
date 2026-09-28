namespace PrincesPalace.Domain.Dungeon
{
    // What a player finds in a room.
    //
    // Adding one is deliberately cheap: put it here, give it a weight in
    // DescentMapGenerator.MiddleRooms (if it should generate at all), give it
    // a glyph/label/colour/description in MapController, and give it a case
    // in GameplayManager.ResolveRoom, which is where a room's content runs
    // the moment it is picked on the map.
    //
    // ItemSpawn currently has no weight, so nothing generates it. That is a
    // gap rather than a decision: no sweep asserts every value is reachable.
    public enum RoomType
    {
        // Column 0 of every floor, and ONLY column 0 — never generated as a
        // middle room, so an entry room simply cannot show up anywhere but
        // where it belongs. See DescentMapGenerator.Generate.
        Entry,

        Fight,
        EliteFight,
        Treasure,
        Rest,
        Boss,

        // Placeholders with no bespoke behaviour yet. They generate, they
        // render, they can be entered and cleared — the content behind them
        // lands later. Present now so the map does not have to change shape
        // when it does.
        Event,
        Shop,
        ItemSpawn,

        // Unscouted: the map shows a "?" and nothing else until the room is
        // actually entered. Retired from generation (DescentMapGenerator no
        // longer rolls it — its weight moved to Fight), kept for
        // compatibility so a saved `currentNodeId` from before that change
        // still resolves, renders and clears the same as it always did.
        Unknown,
    }
}
