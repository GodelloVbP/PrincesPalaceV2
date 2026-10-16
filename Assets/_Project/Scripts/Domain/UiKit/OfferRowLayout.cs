namespace PrincesPalace.Domain.UiKit
{
    // Where a Reckoning page's cards sit, and how wide they are, for a row of
    // any width.
    //
    // ITS OWN TYPE BECAUSE THE ROW IS NOT ALWAYS THE SAME. A page shows k + 2
    // cards for k choices (docs/PLAN_ITEMS.md 1.2), so a row is three wide for
    // the common one-choice page and MaxCards wide for a boss and two anchors.
    // The tree is built at MaxCards and ReckoningController.LayOutOfferRow
    // re-centres and resizes it for whatever count is on screen, from the same
    // arithmetic the tree was built with. One function shared by both rather
    // than the same numbers written out twice.
    //
    // Pure, engine-free and EditMode-testable, the same posture MapLayout takes
    // for the descent map's tiles.
    //
    // BuildOffer's own comment records the constraint this arithmetic answers
    // to: the row runs to the clip's half-width, so widening a card by even a
    // pixel fails the containment audit. The budget is fixed and the cards
    // divide it, at any count.
    public static class OfferRowLayout
    {
        // The most cards one page shows: k + 2 for k = 3, a boss and two
        // anchors settled together. A fourth chip would not fit; I3 revisits
        // the width when it lets chips pile up.
        public const int MaxCards = 5;

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
        // that owns it rather than keeping a copy in sync.
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

        // ---- the card face's parts (docs/PLAN_REWARD_SCREENS.md C5) -------------
        //
        // x offsets are from the card's centre. Every one is a function of the
        // card count so the tree (built at MaxCards) and the controller (laying
        // out k + 2) share one set of numbers.

        // The plinth's width: a step-1 flagstone is the narrowest, a step-4 gold
        // cloth fills the card. Width is a quality channel that survives greyscale.
        public static float PlinthWidth(int visibleCount, int step) =>
            CardWidth(visibleCount) * (0.6f + 0.1f * System.Math.Max(1, System.Math.Min(4, step)));

        public const float PlinthHeight = 54f;

        // The slot band is a fixed fraction of the card, not of the plinth, so
        // the numeral and the +N stay where the eye looks for them.
        public static float BandWidth(int visibleCount) => CardWidth(visibleCount) * 0.8f;

        public static float BandLabelX(int visibleCount) => BandWidth(visibleCount) * 0.5f - 26f;

        // The worn item's ghost: 55% of the new item's box, tucked to the left.
        public const float GhostScale = 0.55f;
        public const float GhostAlpha = 0.45f;

        public static float GhostWidth(int visibleCount) => IconWidth(visibleCount) * GhostScale;

        public static float GhostX(int visibleCount) =>
            -(CardWidth(visibleCount) - GhostWidth(visibleCount)) * 0.5f + 6f;

        public const float LostBadgeSize = 22f;

        // The lost-affix mark sits on the ghost's top-left corner.
        public static float LostBadgeX(int visibleCount) =>
            GhostX(visibleCount) - GhostWidth(visibleCount) * 0.5f + LostBadgeSize * 0.5f;

        public const float MedallionSize = 44f;

        public static float MedallionX(int visibleCount) =>
            -CardWidth(visibleCount) * 0.5f + MedallionSize * 0.5f + 4f;

        // Delta line: the number takes the left, the chevron the right edge.
        public const float ChevronSize = 28f;

        public static float DeltaLabelWidth(int visibleCount) => LabelWidth(visibleCount) - ChevronSize - 8f;

        public static float DeltaLabelX(int visibleCount) => -(ChevronSize + 8f) * 0.5f;

        public static float ChevronX(int visibleCount) => LabelWidth(visibleCount) * 0.5f - ChevronSize * 0.5f;

        // From four cards up the card is narrow: the affix line waits for focus
        // and the name truncates.
        public static bool IsNarrow(int visibleCount) => visibleCount >= 4;

        // ---- row order -----------------------------------------------------------
        //
        // Row position -> page index. Cards group by wearer in squad order,
        // stable within a wearer; a card whose wearer is not in the squad (a
        // relic's empty id included) keeps its page position after the squad's
        // groups, wearers in first-seen order. The layout, the rail, the links
        // and the flip order all iterate this; the buttons, the test names and
        // the pages stay page-indexed.
        public static int[] RowOrder(System.Collections.Generic.IReadOnlyList<string> wearers,
            System.Collections.Generic.IReadOnlyList<string> squad)
        {
            int count = wearers == null ? 0 : wearers.Count;
            var order = new int[count];
            var rank = new int[count];
            var seen = new System.Collections.Generic.List<string>();
            int squadSize = squad == null ? 0 : squad.Count;

            for (int i = 0; i < count; i++)
            {
                string wearer = wearers[i] ?? "";
                int slot = -1;
                for (int s = 0; s < squadSize && slot < 0; s++)
                {
                    if (squad[s] == wearer && wearer.Length > 0) slot = s;
                }

                if (slot < 0)
                {
                    int found = seen.IndexOf(wearer);
                    if (found < 0)
                    {
                        found = seen.Count;
                        seen.Add(wearer);
                    }

                    slot = squadSize + found;
                }

                rank[i] = slot;
                order[i] = i;
            }

            // Insertion sort: stable, and the lists are at most MaxCards long.
            for (int i = 1; i < count; i++)
            {
                int page = order[i];
                int j = i - 1;
                while (j >= 0 && rank[order[j]] > rank[page])
                {
                    order[j + 1] = order[j];
                    j--;
                }

                order[j + 1] = page;
            }

            return order;
        }

        // The inverse: page index -> row position.
        public static int[] RowOf(int[] rowOrder)
        {
            var rowOf = new int[rowOrder.Length];
            for (int row = 0; row < rowOrder.Length; row++) rowOf[rowOrder[row]] = row;
            return rowOf;
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
