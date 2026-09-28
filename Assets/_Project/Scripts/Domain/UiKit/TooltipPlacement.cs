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
    // AND IT NEVER LANDS ON ITS OWN SUBJECT. The
    // rule is: beside if a side has room, flipped if the preferred one
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
        // THE ANCHOR'S HEIGHT IS TAKEN AS WELL AS ITS WIDTH. It is only read by the
        // never-overlap fallback below, and that fallback is the reason the
        // parameter exists at all: a box cannot be placed clear of a rect
        // whose height it was never told.
        public static UiVec Beside(
            float anchorX, float anchorY, float anchorWidth, float anchorHeight,
            float tooltipWidth, float tooltipHeight,
            float interiorLeft, float interiorRight,
            float interiorBottom, float interiorTop,
            float gap = DefaultGap,
            System.Collections.Generic.IReadOnlyList<UiRect> keepOut = null)
        {
            float halfW = tooltipWidth * 0.5f;
            float halfH = tooltipHeight * 0.5f;
            float reach = gap + anchorWidth * 0.5f + halfW;

            var anchor = new UiRect(new UiVec(anchorX, anchorY), new UiVec(anchorWidth, anchorHeight));
            var size = new UiVec(tooltipWidth, tooltipHeight);

            // Right by preference, because the eye is already travelling that
            // way and the leftmost anchor is the one most likely to have room.
            // Level with the anchor, so the box reads as belonging to it,
            // pushed just far enough to stay inside.
            float beside = Clamp(anchorY, interiorBottom + halfH, interiorTop - halfH);

            // NEITHER SIDE HAS ROOM: before any clamp, go UNDER the anchor,
            // then OVER it. Under first because that is where a tooltip
            // conventionally sits and because the thing above an anchor is
            // usually what named it.
            float x = Clamp(anchorX, interiorLeft + halfW, interiorRight - halfW);
            float reachY = gap + anchorHeight * 0.5f + halfH;

            var preferred = new[]
            {
                new UiVec(anchorX + reach, beside),
                new UiVec(anchorX - reach, beside),
                new UiVec(x, anchorY - reachY),
                new UiVec(x, anchorY + reachY),
            };

            foreach (var at in preferred)
            {
                if (Clear(at, size, anchor, interiorLeft, interiorRight, interiorBottom, interiorTop, keepOut))
                    return at;
            }

            // NONE OF THE FOUR IS CLEAR -- usually because a keep-out rect
            // (the dossier's mannequin and slots) sits where the
            // box wanted to go. Look further out for the clear position
            // NEAREST the anchor rather than giving up: a box one column
            // further away is better than a box over what it must not hide.
            var found = NearestClear(anchor, size, gap, interiorLeft, interiorRight, interiorBottom, interiorTop, keepOut);
            if (found.HasValue) return found.Value;

            // Nothing clears it anywhere -- an interior with no room for the
            // box outside the anchor and the keep-outs. Clamped inside and
            // overlapping, kept as the stated last resort: a box half off the
            // panel is not better than a box over the anchor, and something
            // has to be returned. TooltipPlacementTests walks both shipped
            // screens against their real geometry to prove neither reaches it.
            return new UiVec(x, Clamp(anchorY, interiorBottom + halfH, interiorTop - halfH));
        }

        // THE CANDIDATES ARE EDGES, not a grid. A box that has to dodge
        // axis-aligned rects is nearest its anchor either level with the
        // anchor or standing `gap` off one of the obstacles' edges (or
        // against a wall), on each axis independently -- so those x's and
        // y's, crossed, are every position worth trying, and the answer is
        // exact rather than rounded to a pitch. Ranked by the gap between the
        // box's edge and the anchor's (a wide box beside a narrow anchor is
        // near when its EDGE is), then by how far its centre sits from the
        // anchor's, then right over left -- the same right-first preference
        // the four fixed positions above have.
        private static UiVec? NearestClear(UiRect anchor, UiVec size, float gap,
            float interiorLeft, float interiorRight, float interiorBottom, float interiorTop,
            System.Collections.Generic.IReadOnlyList<UiRect> keepOut)
        {
            float halfW = size.X * 0.5f;
            float halfH = size.Y * 0.5f;
            float minX = interiorLeft + halfW;
            float maxX = interiorRight - halfW;
            float minY = interiorBottom + halfH;
            float maxY = interiorTop - halfH;
            if (minX > maxX || minY > maxY) return null;

            var xs = new System.Collections.Generic.List<float> { anchor.Centre.X, minX, maxX };
            var ys = new System.Collections.Generic.List<float> { anchor.Centre.Y, minY, maxY };

            void AddEdges(UiRect r)
            {
                xs.Add(r.Left - gap - halfW);
                xs.Add(r.Right + gap + halfW);
                ys.Add(r.Bottom - gap - halfH);
                ys.Add(r.Top + gap + halfH);
            }

            AddEdges(anchor);
            if (keepOut != null)
            {
                for (int i = 0; i < keepOut.Count; i++) AddEdges(keepOut[i]);
            }

            UiVec? best = null;
            float bestGap = float.MaxValue;
            float bestCentre = float.MaxValue;
            bool bestRight = false;

            foreach (float rawX in xs)
            {
                if (rawX < minX - 0.001f || rawX > maxX + 0.001f) continue;

                foreach (float rawY in ys)
                {
                    if (rawY < minY - 0.001f || rawY > maxY + 0.001f) continue;

                    var at = new UiVec(rawX, rawY);
                    if (!Clear(at, size, anchor, interiorLeft, interiorRight, interiorBottom, interiorTop, keepOut))
                        continue;

                    float dx = System.Math.Max(0f, System.Math.Max(anchor.Left - (rawX + halfW), (rawX - halfW) - anchor.Right));
                    float dy = System.Math.Max(0f, System.Math.Max(anchor.Bottom - (rawY + halfH), (rawY - halfH) - anchor.Top));
                    float edgeGap = dx * dx + dy * dy;

                    float cx = rawX - anchor.Centre.X;
                    float cy = rawY - anchor.Centre.Y;
                    float centre = cx * cx + cy * cy;
                    bool right = rawX >= anchor.Centre.X;

                    bool better = edgeGap < bestGap - 0.001f
                        || (System.Math.Abs(edgeGap - bestGap) <= 0.001f
                            && (centre < bestCentre - 0.001f
                                || (System.Math.Abs(centre - bestCentre) <= 0.001f && right && !bestRight)));

                    if (!better) continue;

                    best = at;
                    bestGap = edgeGap;
                    bestCentre = centre;
                    bestRight = right;
                }
            }

            return best;
        }

        // Inside the interior, off the anchor, off every keep-out rect.
        private static bool Clear(UiVec at, UiVec size, UiRect anchor,
            float interiorLeft, float interiorRight, float interiorBottom, float interiorTop,
            System.Collections.Generic.IReadOnlyList<UiRect> keepOut)
        {
            const float Slop = 0.01f;
            var box = new UiRect(at, size);
            if (box.Left < interiorLeft - Slop || box.Right > interiorRight + Slop) return false;
            if (box.Bottom < interiorBottom - Slop || box.Top > interiorTop + Slop) return false;
            if (box.Overlaps(anchor)) return false;

            if (keepOut != null)
            {
                for (int i = 0; i < keepOut.Count; i++)
                {
                    if (box.Overlaps(keepOut[i])) return false;
                }
            }

            return true;
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
