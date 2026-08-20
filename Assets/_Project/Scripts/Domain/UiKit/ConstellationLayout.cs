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

        // ---- the orb layout, migrated from v1 -----------------------------------
        //
        // Everything below is v1's talent-tree-v2 geometry, brought over with
        // its numbers intact. v2 had replaced it with a stretch -- one orb size
        // for every slot, and depth and dx normalised across the whole sky --
        // which lost the two things the design was actually saying.
        //
        // SIZING MARKS ROLE, NOT POINT COST. That is the design's own rule and
        // v2 had no way to express it: every orb was StarSize, so the capstone
        // at the top of an eight-tier climb looked exactly like the first chain
        // node above the root. The convergence and the capstone are the two
        // slots whose SHAPE is their meaning, and they are the two that render
        // larger.
        public const float OrbNormal = 60f;
        public const float OrbMerge = 70f;
        public const float OrbCap = 82f;

        // Kept as the old name so nothing that only wants "about an orb wide"
        // has to care which kind it is asking about.
        public const float StarSize = OrbNormal;

        public static float OrbSize(string kind)
        {
            switch (kind)
            {
                case "cap": return OrbCap;
                case "merge": return OrbMerge;
                default: return OrbNormal;
            }
        }

        // THE GRID AND THE BRANCH SPREAD DIFFERENTLY, which a single normalised
        // StarX cannot say at all. The 3x3 grid below the convergence is the
        // narrow half of the tree and the three-way branch above it is the wide
        // half -- the shape opens out as it climbs, and that widening is how the
        // convergence reads as a waist rather than as one more row.
        //
        // v1's own comment: the design draws these as 150 and 200 on a 1920
        // reference canvas, scaled down slightly for this game's column pitch.
        public const float GridDx = 130f;
        public const float BranchDx = 170f;

        // The convergence sits at depth 4; everything above it is the branch.
        // Derived from the skeleton rather than written as 4, so a tier added
        // below the waist moves the waist instead of silently widening the grid.
        public static int BranchStartDepth => MergeDepth + 1;

        public static int MergeDepth
        {
            get
            {
                for (int slot = 0; slot < Talents.TalentSkeleton.SlotCount; slot++)
                {
                    if (Talents.TalentSkeleton.Kind[slot] == "merge") return Talents.TalentSkeleton.Depth[slot];
                }

                return 0;
            }
        }

        // A FIXED PITCH, not a stretch to fill the sky. Normalising depth across
        // the sky's height means adding a tier silently squeezes every existing
        // one, so a tree's spacing would depend on how many tiers it happened to
        // have -- and the orbs are a constant size, so the gap between them would
        // shrink toward nothing while the orbs stayed put.
        public const float DepthGap = 75f;

        // Chosen in v1 so the capstone clears the path titles above it and the
        // root clears the info panel below it.
        public const float RootY = -250f;

        // DEPTH RUNS UP THE SCREEN. The capstone is the thing at the top of the
        // climb, and a constellation that grew downward would read as falling.
        public static float StarY(int depth) => RootY + depth * DepthGap;

        // dx is signed and centred: -1 is the left branch, 0 the spine, +1 the
        // right. Multiplied out rather than indexed, so a future four-wide tier
        // needs no new case.
        public static float StarX(int dx, int depth) =>
            dx * (depth >= BranchStartDepth ? BranchDx : GridDx);

        // How wide and tall one path's tree actually is, orbs included. Used by
        // the screen to place three of them side by side, and by the tests to
        // say the thing fits what it is drawn in.
        public static float TreeWidth => BranchDx * 2f + OrbCap;

        public static float TreeHeight => (Talents.TalentPage.DepthCount - 1) * DepthGap + OrbCap;

        // ---- edges ---------------------------------------------------------------
        //
        // TEN, not three. v2 drew the connections as 3px hairlines, which read
        // as a wiring diagram; v1's are limbs with a lit crack down them, and
        // the width is what makes an edge look grown rather than drawn.
        public const float EdgeWidth = 10f;

        // The lit layers, as multiples of the base so they cannot drift from
        // it. v1's numbers: a wide soft halo, a thin bright core down its
        // middle.
        public const float EdgeGlowWidth = EdgeWidth * 2.6f;
        public const float EdgeCoreWidth = EdgeWidth * 0.45f;

        // The travelling dot. Wider than the limb it runs along, so it reads
        // as something moving THROUGH the line rather than as a bright patch
        // of it.
        public const float EdgeSparkSize = EdgeWidth * 1.8f;

        // ---- paging ------------------------------------------------------------

        // Where a tree sits horizontally while the sky slides between them.
        //
        // A full screen width apart, so exactly one is ever centred and the
        // neighbours are genuinely off-stage rather than peeking. The slide is
        // what makes three trees read as three PLACES rather than three tabs.
        // ONE SCREEN WIDE, which is what makes a page change read as the sky
        // sliding rather than as the tree jumping. Read from UiFrames: a stride
        // that stopped matching the stage would leave part of the next path
        // visible beside the current one.
        public static float PageStride => UiFrames.Reference.X;

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
