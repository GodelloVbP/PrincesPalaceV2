namespace PrincesPalace.Domain.Dungeon
{
    // What a player finds in a room.
    //
    // Adding one is deliberately cheap: put it here, give it a weight in
    // DescentMapGenerator.MiddleRooms (if it should generate at all), give it
    // a glyph/label/colour/description in DescentMapView, and give it a case
    // in GameplayManager.ResolveRoom, which is where a room's content runs
    // the moment it is picked on the map.
    //
    // ItemSpawn currently has no weight, so nothing generates it. That is a
    // gap rather than a decision: the sweep that used to assert every value
    // was reachable belonged to MapGeneratorTests and went with the grid map.
    public enum RoomType
    {
        // Column 0 of every floor, and ONLY column 0 — never generated as a
        // middle room, so unlike the old workaround (typing the entrance as
        // Rest and special-casing its label) an entry room simply cannot show
        // up anywhere but where it belongs. See DescentMapGenerator.Generate.
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
        // actually entered. Same placeholder treatment as Event/Shop/
        // ItemSpawn otherwise — it generates, renders, clears — the mystery
        // is presentation, not a hidden second type underneath. Keeping it
        // that way (rather than secretly pre-rolling a real type and
        // revealing it) is what keeps this deterministic from the floor seed
        // like everything else, with nothing extra to reproduce.
        Unknown,
    }
}
