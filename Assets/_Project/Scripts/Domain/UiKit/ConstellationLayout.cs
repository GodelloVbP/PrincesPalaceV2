namespace PrincesPalace.Domain.UiKit
{
    // Where a talent sits when its tree is drawn as a constellation, and how
    // paging between trees moves.
    //
    // Pure arithmetic, in Domain for the same reason MapLayout and
    // FightSubmenuLayout are: the SCREEN TREE places the star pool at build time
    // and the CONTROLLER re-anchors it per tree at runtime, and those two have to
    // be the same function or the sky drifts from what it claims.
    public static class ConstellationLayout
    {
        // The sky the stars are placed in. Wider than tall, because a tree
        // climbs and a constellation is read across.
        public const float SkyWidth = 1180f;
        public const float SkyHeight = 720f;

        public const float StarSize = 56f;

        // A talent's slot is (depth, dx) in TalentSkeleton's own vocabulary:
        // depth climbs toward the capstone, dx spreads the 3-way branch. Those
        // are grid coordinates; this turns them into sky coordinates.
        //
        // DEPTH RUNS UP THE SCREEN. The capstone is the thing at the top of the
        // climb, and a constellation that grew downward would read as falling.
        public static float StarY(int depth, int depthCount)
        {
            if (depthCount <= 1) return 0f;
            return -SkyHeight * 0.5f + SkyHeight * depth / (depthCount - 1);
        }

        // dx is signed and centred: -1 is the left branch, 0 the spine, +1 the
        // right. Multiplied out rather than indexed, so a future four-wide tier
        // needs no new case.
        public static float StarX(int dx, int maxAbsDx)
        {
            if (maxAbsDx <= 0) return 0f;
            return SkyWidth * 0.5f * dx / maxAbsDx;
        }

        // ---- paging ------------------------------------------------------------

        // Where a tree sits horizontally while the sky slides between them.
        //
        // A full screen width apart, so exactly one is ever centred and the
        // neighbours are genuinely off-stage rather than peeking. The slide is
        // what makes three trees read as three PLACES rather than three tabs.
        public const float PageStride = 1920f;

        public static float PageX(int index, int current) => (index - current) * PageStride;

        // How far through a slide, 0..1, eased.
        //
        // A pure static seam like every other animation curve in this project:
        // the shape can be pinned by an EditMode test with no scene, no
        // coroutine and no frame.
        public const float SlideSeconds = 0.42f;

        // No divide-by-zero guard here, deliberately. SlideSeconds is a const,
        // so the compiler folded that branch away and warned it was
        // unreachable (CS0162) -- a guard that cannot fire is not protection,
        // it is noise that hides the next real unreachable-code warning.
        //
        // What it was guarding against is real, though: at SlideSeconds 0 and
        // elapsed 0 the division is NaN, every comparison below is false, and
        // this returns NaN rather than a progress. So the guarantee moved to
        // where it can actually hold -- ConstellationLayoutTests asserts the
        // constant is positive, which fails at test time instead of producing
        // a NaN slide at play time.
        public static float SlideProgress(float elapsed)
        {
            float t = elapsed / SlideSeconds;
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            // Ease in-out. A slide that starts and stops abruptly reads as a cut
            // with extra steps; the whole point of moving is that the player
            // keeps their bearings.
            return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
        }

        // The sky's own offset partway through a slide from `from` to `to`.
        public static float SlideOffset(int from, int to, float progress) =>
            -PageStride * (from + (to - from) * progress);

        // ---- which tree ----------------------------------------------------------

        // Paging is a CLAMPED line, not a loop.
        //
        // Wrapping from the last tree back to the first would make the arrows
        // lie about where the ends are, and a player who has paged three times
        // to the right should be able to tell they are at the edge without
        // counting. The arrow simply stops being offered.
        public static int Step(int current, int direction, int count)
        {
            if (count <= 0) return 0;

            int next = current + (direction < 0 ? -1 : direction > 0 ? 1 : 0);
            if (next < 0) return 0;
            if (next >= count) return count - 1;
            return next;
        }

        public static bool CanStep(int current, int direction, int count) =>
            Step(current, direction, count) != current;
    }
}
