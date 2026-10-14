namespace PrincesPalace.Domain.Dungeon
{
    // What a player finds in a room.
    //
    // Adding one: put it here, give it a role in RealmMapGenerator.TypeRooms
    // and a weight in RealmProfile.waypointWeights (if it should generate at
    // all), an icon in MapScreen and a caption in MapNodeCard, and a case in
    // RunOrchestrator.ArriveAt, which is where a room's content runs the
    // moment the party reaches it.
    //
    // ItemSpawn currently has no weight, so nothing generates it. That is a
    // gap rather than a decision: no sweep asserts every value is reachable.
    public enum RoomType
    {
        // Offset 0 of every realm, and only there: RealmMapGenerator places
        // it and never rolls it.
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
        // actually entered. Never generated (the map shows every room); kept
        // because RoomResolution and the map icons still handle the value.
        Unknown,

        // Appended, never inserted: RealmProfile.waypointWeights serializes
        // this enum as an int. At most one per realm, in a pocket. Entering
        // it opens the cache event (RunOrchestrator.EnsureCache), where gold
        // is banked out of death's reach (RunSnapshot.bankedGold).
        Cache,
    }
}
