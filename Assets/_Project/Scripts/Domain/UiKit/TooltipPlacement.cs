namespace PrincesPalace.Domain.UiKit
{
    // Where a hover tooltip sits when it belongs to the thing being hovered.
    //
    // ONE COPY OF THE ARITHMETIC. The dossier's pack worked this out first --
    // beside the cell if there is room on that side, flipped over if there is
    // not, clamped so it never leaves the panel -- and the Reckoning needs
    // exactly the same answer for its offer cards. A second hand-written copy
    // is the duplication this project keeps writing rules against, and it would
    // have been a copy with a real difference: the dossier's version does not
    // clamp x after flipping, so a tooltip too wide for either side would have
    // left the panel rather than being pushed back inside.
    //
    // Pure, engine-free and EditMode-testable, the same posture OfferRowLayout
    // takes for the offer row itself. The controllers own "which rect is being
    // hovered"; this owns "and therefore where does the box go".
    public static class TooltipPlacement
    {
        // Clear air between the anchor's edge and the tooltip's.
        public const float DefaultGap = 14f;

        // Where to put a tooltip's CENTRE, in the same local space as the
        // anchor's centre.
        //
        // `interior` is the box the tooltip must stay inside, already inset by
        // whatever margin the caller wants -- expressed as edges rather than a
        // half-size because the Reckoning's painted interior is not vertically
        // symmetric (its crest is deeper than its bottom ornament) and a
        // half-height would have to lie about one edge or the other.
        public static UiVec Beside(
            float anchorX, float anchorY, float anchorWidth,
            float tooltipWidth, float tooltipHeight,
            float interiorLeft, float interiorRight,
            float interiorBottom, float interiorTop,
            float gap = DefaultGap)
        {
            float halfW = tooltipWidth * 0.5f;
            float halfH = tooltipHeight * 0.5f;
            float reach = gap + anchorWidth * 0.5f + halfW;

            // Right by preference, because the eye is already travelling that
            // way and the leftmost anchor is the one most likely to have room.
            float x = anchorX + reach;
            if (x + halfW > interiorRight) x = anchorX - reach;

            // AND THEN CLAMPED ANYWAY. The flip answers "this side has no
            // room"; it does not answer "neither side does", which is a real
            // case for a wide tooltip on a middle anchor. Overlapping the
            // anchor is the lesser evil against hanging off the panel.
            x = Clamp(x, interiorLeft + halfW, interiorRight - halfW);

            // Level with the anchor, so the box reads as belonging to it,
            // pushed just far enough to stay inside.
            float y = Clamp(anchorY, interiorBottom + halfH, interiorTop - halfH);

            return new UiVec(x, y);
        }

        // A tooltip taller or wider than the box it must fit in makes the two
        // clamp bounds cross, and min/max in that order would put it at the
        // wrong edge. Centring is the only sensible answer to "it does not
        // fit", and it is the answer that degrades rather than jumps.
        private static float Clamp(float value, float min, float max)
        {
            if (min > max) return (min + max) * 0.5f;
            if (value < min) return min;
            return value > max ? max : value;
        }
    }
}
