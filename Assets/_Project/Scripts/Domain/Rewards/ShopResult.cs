namespace PrincesPalace.Domain.Rewards
{
    // WHY A SHOP MUTATION DID NOTHING.
    //
    // A reason enum rather than a bool, because the four callers that will
    // exist by gate 3 -- the screen, the bot policy, the atomicity tests and
    // the trace -- each need a different amount of it, and "false" is the
    // answer none of them can act on.
    public enum ShopRefusal
    {
        None,

        // No run, no shop stock, or the party is not standing where the
        // stock says it is. Not a player-facing refusal; a call arriving out
        // of order.
        NoShop,

        // The index is outside the section, or the section is not one.
        BadIndex,

        // The card is a NO OFFER placeholder, or has already been bought.
        NothingToBuy,

        // The purse. The one refusal a player causes on purpose.
        NotEnoughGold,

        // Sell only: the bag does not hold that many of exactly that copy
        // (itemId, plus, modifierIds, riftTier).
        NotInBag,

        // LearnSpell/ReplaceSpell only: the character already has this
        // skillId in one of their three slots. A no-op, not an error --
        // content can change under a run -- but not a silent success either
        // (docs/PLAN_SHOP.md §1d).
        AlreadyKnown,

        // LearnSpell/ReplaceSpell only: the skillId named is not sitting in
        // run.unassignedSpellBooks -- nothing to place.
        NotOwned,

        // LearnSpell only: CanLearn(characterId) answered -1. The caller is
        // expected to have checked this and offered ReplaceSpell instead;
        // reaching here means it didn't.
        NoFreeSlot,

        // The test-only refusal seam fired. Never reachable in a build.
        Injected,
    }

    public enum ShopOutcome
    {
        // Applied and persisted.
        Ok,

        // Nothing happened, and the pre-state is intact. Every refusal is
        // decided before anything is touched (docs/PLAN_SHOP.md §2f step 1),
        // which is what lets a caller retry or report without repairing
        // anything.
        Refused,

        // APPLIED IN MEMORY, NOT WRITTEN TO DISK. The half state that can
        // actually happen: SaveSystem.Save catches its own exception and
        // answers false, so the mutation stands in the live SaveData and the
        // previous save on disk is untouched. The screen can say so; before
        // SaveSystem.Save returned a bool, only the console knew.
        AppliedNotPersisted,
    }

    public readonly struct ShopResult
    {
        public readonly ShopOutcome Outcome;
        public readonly ShopRefusal Reason;

        // What the mutation cost or paid, in gold, signed the way the purse
        // moved: negative for a purchase, positive for a sale, zero for a
        // refusal. On the result rather than recomputed by the caller,
        // because the trace wants it and the caller would have to re-derive
        // a price the mutation already resolved.
        public readonly int GoldDelta;

        private ShopResult(ShopOutcome outcome, ShopRefusal reason, int goldDelta)
        {
            Outcome = outcome;
            Reason = reason;
            GoldDelta = goldDelta;
        }

        public static ShopResult Ok(int goldDelta) =>
            new ShopResult(ShopOutcome.Ok, ShopRefusal.None, goldDelta);

        public static ShopResult AppliedNotPersisted(int goldDelta) =>
            new ShopResult(ShopOutcome.AppliedNotPersisted, ShopRefusal.None, goldDelta);

        public static ShopResult Refused(ShopRefusal reason) =>
            new ShopResult(ShopOutcome.Refused, reason, 0);

        // True for both outcomes that CHANGED something. A caller repainting
        // a shelf cares about this; a caller reporting to the player cares
        // about the difference between them.
        public bool Applied => Outcome != ShopOutcome.Refused;
    }
}
