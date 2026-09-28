namespace PrincesPalace.Domain.Progression
{
    // How one node of the reward track reads, once the player is taken into
    // account.
    //
    // FOUR STATES OUT OF TWO INTEGERS -- `level` and `claimedTrackLevel` --
    // and nothing else, because deriving state from `level` alone cannot
    // say the thing the track most needs to say: that a reward has been
    // REACHED but not yet COLLECTED. That distinction is already in the
    // data (the watermark has always trailed the level); making collection
    // a thing the player does makes it the difference between a node that
    // wants pressing and one that does not.
    public enum TrackNodeState
    {
        // Above the player's level. Dark disc, gold mark, dim caption.
        ToCome,

        // Reached and unclaimed: the one state that asks for a click.
        Waiting,

        // Reached and paid. Carries the seal pip.
        Collected,

        // The player's own level. WINS OVER Waiting -- see RewardTrack.StateOf.
        Here,
    }
}
