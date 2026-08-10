// MOVED from Core to Domain (engine-free) so TalentLayout's EditMode tests can
// reach it. Without the move those tests would have to duplicate these tables,
// which is exactly the drift this construction layer exists to kill -- and this
// is the cheapest moment it will ever be, with a single reference in the tree.
namespace PrincesPalace.Domain.Talents
{
    // The FIXED per-path node graph every talent path shares (Talent Tree
    // v2, 2026-08-02): a single root, a 3x3 grid climbing to one
    // convergence, then a 3-way branch climbing to the capstone. Slot index
    // (0-20) is what talents.json's `row` field addresses -- this table is
    // the ONLY place the shape itself is defined. SceneBuilder.Talents.cs
    // uses it to position orbs/edges at build time; TalentController.cs
    // uses the identical slot order to know, at runtime, which edge index
    // is which (parent,child) pair without either file duplicating the
    // graph.
    public static class TalentSkeleton
    {
        public const int SlotCount = 21;

        // Depth tier, 0 (root) to 8 (capstone). Three slots share the same
        // depth wherever the tree is 3-wide (the grid rows and the branch
        // rows) -- this is a tier, not a unique row, so several slots can
        // and do repeat a value.
        public static readonly int[] Depth =
        {
            0,
            1, 1, 1,
            2, 2, 2,
            3, 3, 3,
            4,
            5, 5, 5,
            6, 6, 6,
            7, 7, 7,
            8
        };

        // Horizontal slot within a depth tier: -1 left, 0 centre, 1 right.
        public static readonly int[] DxSlot =
        {
            0,
            -1, 0, 1,
            -1, 0, 1,
            -1, 0, 1,
            0,
            -1, 0, 1,
            -1, 0, 1,
            -1, 0, 1,
            0
        };

        // "cap" (slot 20) and "merge" (slot 10, the first convergence)
        // render larger -- sizing marks role, not point cost. Every other
        // slot, including the root, is "normal".
        public static readonly string[] Kind =
        {
            "normal",
            "normal", "normal", "normal",
            "normal", "normal", "normal",
            "normal", "normal", "normal",
            "merge",
            "normal", "normal", "normal",
            "normal", "normal", "normal",
            "normal", "normal", "normal",
            "cap"
        };

        // Parent slot indices per slot -- empty for the root, one entry for
        // every chain node, three for the two convergence nodes (10 and
        // 20) where ANY ONE invested parent is enough to unlock it.
        public static readonly int[][] Parents =
        {
            new int[0],
            new[] { 0 }, new[] { 0 }, new[] { 0 },
            new[] { 1 }, new[] { 2 }, new[] { 3 },
            new[] { 4 }, new[] { 5 }, new[] { 6 },
            new[] { 7, 8, 9 },
            new[] { 10 }, new[] { 10 }, new[] { 10 },
            new[] { 11 }, new[] { 12 }, new[] { 13 },
            new[] { 14 }, new[] { 15 }, new[] { 16 },
            new[] { 17, 18, 19 }
        };

        // Total (parent,child) edges in one path's skeleton -- 24: one for
        // every chain slot (18 of them) plus 3 each for the two
        // convergences. Three paths share this identically, so the built
        // scene always has SlotCount-derived EdgesPerPath * 3 edges total.
        public static int EdgesPerPath
        {
            get
            {
                int total = 0;
                for (int i = 0; i < SlotCount; i++)
                {
                    total += Parents[i].Length;
                }

                return total;
            }
        }
    }
}
