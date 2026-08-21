using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.UiKit
{
    // Where the Reckoning's offer cards sit, and how wide they are, for a row
    // of any width.
    //
    // ITS OWN TYPE BECAUSE THE ROW IS NO LONGER ONE WIDTH. The reward track
    // widens the offer at level 50, and a screen is emitted once at build time
    // for every save -- so the tree cannot be built at the width a particular
    // player has earned. The tree is built at the WIDEST the track can grant
    // and the controller narrows it, which means the same arithmetic runs in
    // two places and has to be one function or it is two answers.
    //
    // Pure, engine-free and EditMode-testable, the same posture MapLayout takes
    // for the descent map's tiles.
    //
    // THE CARDS SHRINK RATHER THAN THE ROW GROWING, and that is forced.
    // BuildOffer's own comment recorded the constraint before this existed:
    // "The three cards run to x 540 against a clip half-width of 540.3. That is
    // deliberate -- the offers are sized to use the full interior -- but it
    // means widening a card by even a pixel now fails A2." Four cards at the
    // three-card width need 1450px against a painted interior of 1080.6. So the
    // budget is fixed and the cards divide it.
    //
    // The happy consequence is that the card width this file introduces is not
    // a number it overrides the old one with -- ~340 FALLS OUT of the budget at
    // three cards, so a player who has not reached level 50 sees exactly the
    // row they saw before. What level 50 costs is that all four cards come out
    // at ~248 instead, about 27% narrower than the art was sized for.
    //
    // WHY THE TREE IS BUILT AT THE WIDEST: `UiAudit` re-solves the emitted tree
    // at four aspects and cannot see a runtime reposition, so the case it
    // checks should be the one that can fail. Every row spans the same budget,
    // and the four-card row divides it into the most pieces -- the one where a
    // label has least room and a rounding error has most places to land.
    public static class OfferRowLayout
    {
        // The painted interior the row has to live inside, edge to edge.
        //
        // DERIVED from the screen that paints the border rather than restated
        // as 1080, which is what it looks like and is not -- the real figure is
        // 1080.576, because the inset is 9.8% of a 1344 panel. A rounded copy
        // would put the three-card row 0.576px inside an interior it is
        // supposed to exactly fill, and the whole point of BuildOffer's
        // "run to x 540 against a clip half-width of 540.3" note is that this
        // row is measured to the fraction.
        //
        // MapLayout sets the precedent for reading a constant from the type
        // that owns it (DescentMapGenerator) rather than keeping a copy in
        // sync.
        public const float RowBudget = Screens.ReckoningScreen.ContentHalfWidth * 2f;

        public const float CardGap = 30f;

        // How much narrower a card's text is than the card. Two lines of item
        // name have to stop short of the card edge or they read as touching
        // the next card along.
        public const float LabelInset = 20f;

        // The widest row the reward track can ever ask for, and therefore how
        // many cards the tree emits.
        //
        // Read off the track rather than typed here, so "the track grants a
        // four-wide offer" and "the screen has four cards" cannot drift into
        // disagreement -- the failure that would produce is a fourth offer
        // rolled, chosen by the player, and painted onto a card that does not
        // exist.
        public static int MaxCards =>
            RewardTrack.UnlockedAmount(TrackReward.WiderOffer, RewardTrack.MaxLevel, ItemOfferTable.OfferCount);

        // How wide an offer a character at `level` is shown.
        public static int CardsFor(int level) =>
            RewardTrack.UnlockedAmount(TrackReward.WiderOffer, level, ItemOfferTable.OfferCount);

        // One card's width in a row of `visibleCount`. The budget divided, less
        // the gaps between them.
        public static float CardWidth(int visibleCount)
        {
            if (visibleCount <= 0) return 0f;

            return (RowBudget - (visibleCount - 1) * CardGap) / visibleCount;
        }

        // The name and meta labels' width in a row of `visibleCount`.
        public static float LabelWidth(int visibleCount)
        {
            float width = CardWidth(visibleCount) - LabelInset;
            return width < 0f ? 0f : width;
        }

        // The x of card `index` in a row showing `visibleCount` cards.
        //
        // Centred on zero: card i sits at its offset from the middle of the
        // row, so every width shares an axis and the heading above them does
        // not have to move.
        public static float CardX(int index, int visibleCount)
        {
            if (visibleCount <= 0) return 0f;

            return (index - (visibleCount - 1) * 0.5f) * (CardWidth(visibleCount) + CardGap);
        }

        // Total width of a row of `visibleCount` cards, edge to edge. Always
        // the budget, for any count that fits in it -- which is the property
        // that makes the audit's one solved layout stand for all of them.
        public static float RowWidth(int visibleCount)
        {
            if (visibleCount <= 0) return 0f;

            return visibleCount * CardWidth(visibleCount) + (visibleCount - 1) * CardGap;
        }
    }
}
