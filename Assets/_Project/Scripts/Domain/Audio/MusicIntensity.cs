namespace PrincesPalace.Domain.Audio
{
    // How loud the arrangement should be right now, by MEANING rather than by
    // stem count — the same reasoning `MusicTrack` and `Sound` already follow.
    //
    // FOUR named tiers, not nine independent switches. Nine toggles is more
    // control than will ever be used and turns every new song into a
    // nine-dimensional authoring problem instead of a four-state one; a
    // composer can hold "what does the elite mix sound like" in their head and
    // cannot hold 512 combinations.
    //
    // Ordered weakest to strongest. A set must declare ALL FOUR — see
    // MusicLayerResolver, which refuses a set that omits one. Inheriting a
    // missing tier from the one below was the alternative, and it hides the
    // hole: a song with no `elite` entry would play its ordinary fight mix in
    // an elite room and sound like nothing was wrong. Four mixes is also
    // exactly what the authoring model asks a composer to decide anyway.
    public enum MusicIntensity
    {
        // Hub, run map, every non-fight overlay. A minimal bed.
        Ambient,

        // An ordinary fight. The rhythm section joins.
        Fight,

        // An elite. Most of the arrangement.
        Elite,

        // A boss. Everything.
        Boss,
    }
}
