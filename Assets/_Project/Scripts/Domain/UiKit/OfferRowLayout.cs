namespace PrincesPalace.Domain.UiKit
{
    // Where the Reckoning's offer cards sit, and how wide they are, for a row
    // of any width.
    //
    // ITS OWN TYPE BECAUSE THE ROW IS NOT ALWAYS FULL. The tree is built at
    // ItemOfferTable.OfferCount cards, but a thin content pool can still roll
    // fewer than that -- and the controller re-centres and resizes the row for
    // whatever count is actually on screen (ReckoningController.LayOutOfferRow),
    // from the same arithmetic the tree was built with. One function shared by
    // both rather than the same numbers written out twice.
    //
    // Pure, engine-free and EditMode-testable, the same posture MapLayout takes
    // for the descent map's tiles.
    //
    // BuildOffer's own comment records the constraint this arithmetic answers
    // to: "The three cards run to x 540 against a clip half-width of 540.3.
    // That is deliberate -- the offers are sized to use the full interior --
    // but it means widening a card by even a pixel now fails A2." So the
    // budget is fixed and the cards divide it, at any count.
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

        // How much narrower the ITEM ART is than its card, so a big icon does
        // not run to the card edge and read as touching the next one along.
        public const float IconInset = 20f;

        // The item art's box in a row of `visibleCount`.
        //
        // WIDTH IS THE CONSTRAINT, not height, and that is the whole reason
        // this exists. ItemIcons.Apply preserves aspect, so an icon box taller
        // than it is wide simply letterboxes, wasting the extra height. Tie
        // the box to the card instead and a three-card row gets art half again
        // as large, while a thinner one -- fewer offers than the pool could
        // fill -- still fits inside the same budget.
        public static float IconWidth(int visibleCount)
        {
            float width = CardWidth(visibleCount) - IconInset;
            return width < 0f ? 0f : width;
        }

        // The rarity glow behind the art, and the rayed burst behind that.
        //
        // Sized off the CARD rather than off the icon, which is the constraint
        // that actually matters now the icon is large: a glow scaled to the art
        // would reach into the neighbouring card, and rarity colour bleeding
        // from one offer onto the next is worse than a glow that is merely
        // subtle. The burst is the wider of the two because its rays are the
        // part that has to clear the art at all -- see BuildOffer.
        public static float HaloDiameter(int visibleCount) => CardWidth(visibleCount);

        public static float BurstDiameter(int visibleCount) => CardWidth(visibleCount) + 10f;

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
