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
    //
    // AND IT NEVER LANDS ON ITS OWN SUBJECT (gamepad phase 3b, job 1). The
    // rule is now: beside if a side has room, flipped if the preferred one
    // does not, under or over the anchor if neither does, and only then
    // clamped-and-overlapping as a stated last resort. Both the pointer and
    // the selection path go through this one method, so a box does not move
    // depending on which input opened it.
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
        //
        // THE ANCHOR'S HEIGHT IS TAKEN AS WELL AS ITS WIDTH, since the
        // gamepad pass (phase 3b, job 1). It is only read by the
        // never-overlap fallback below, and that fallback is the reason the
        // parameter exists at all: a box cannot be placed clear of a rect
        // whose height it was never told.
        public static UiVec Beside(
            float anchorX, float anchorY, float anchorWidth, float anchorHeight,
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
            // Level with the anchor, so the box reads as belonging to it,
            // pushed just far enough to stay inside.
            float beside = Clamp(anchorY, interiorBottom + halfH, interiorTop - halfH);

            float right = anchorX + reach;
            if (right + halfW <= interiorRight) return new UiVec(right, beside);

            float left = anchorX - reach;
            if (left - halfW >= interiorLeft) return new UiVec(left, beside);

            // NEITHER SIDE HAS ROOM, and this is where the rule changed for
            // the gamepad pass. It used to clamp x back inside and accept
            // landing ON the anchor as "the lesser evil against hanging off
            // the panel" -- which is the exact failure the pack's own
            // placement comment records ("the box answering the question was
            // covering the evidence"), only reached by a narrower path. A
            // focus-driven tooltip makes that path ordinary rather than
            // exotic: there is no cursor to move off the box, so a tooltip
            // that covers its own subject covers it until the player
            // navigates away.
            //
            // So before any clamp: go UNDER the anchor, then OVER it. Under
            // first because that is where a tooltip conventionally sits and
            // because the thing above an anchor is usually what named it.
            float x = Clamp(anchorX, interiorLeft + halfW, interiorRight - halfW);
            float reachY = gap + anchorHeight * 0.5f + halfH;

            float below = anchorY - reachY;
            if (below - halfH >= interiorBottom) return new UiVec(x, below);

            float above = anchorY + reachY;
            if (above + halfH <= interiorTop) return new UiVec(x, above);

            // Nothing clears it on either axis -- an interior with no room
            // for the box anywhere outside the anchor. Clamped inside and
            // overlapping, the old behaviour, kept as the last resort rather
            // than as the second one: a box half off the panel is not better
            // than a box over the anchor, and something has to be returned.
            // Neither shipped screen can reach this (TooltipPlacementTests
            // walks both against their real geometry); it is here so the
            // degradation is stated instead of being an accident.
            return new UiVec(x, Clamp(anchorY, interiorBottom + halfH, interiorTop - halfH));
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
