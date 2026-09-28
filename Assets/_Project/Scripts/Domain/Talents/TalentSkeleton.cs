// MOVED from Core to Domain (engine-free) so TalentLayout's EditMode tests can
// reach it. Without the move those tests would have to duplicate these tables,
// which is exactly the drift this construction layer exists to kill -- and this
// is the cheapest moment it will ever be, with a single reference in the tree.
using System.Collections.Generic;

namespace PrincesPalace.Domain.Talents
{
    // The FIXED per-path node graph every talent path shares: a single
    // root, a 3x3 grid climbing to one
    // convergence, then a 3-way branch climbing to the capstone. Slot index
    // (0-20) is what talents.json's `row` field addresses -- this table is
    // the ONLY place the shape itself is defined. The screen uses it to
    // position orbs and edges at build time; TalentController uses the
    // identical slot order to know, at runtime, which edge index is which
    // (parent, child) pair without either file duplicating the graph.
    //
    // DESCRIBED AS THE ROWS IT IS MADE OF, and derived from there.
    //
    // Depth, DxSlot, Kind and Parents are derived from Rows rather than
    // stored as four separate hand-aligned arrays: nothing would tie four
    // arrays together, so a slot moved in one and not the others would not
    // fail to compile and would not look wrong -- it would
    // quietly draw an orb in the wrong place, or unlock a node from the wrong
    // parent, or price it as the wrong role. And the same shape is what
    // talents.json is authored against, 294 talents addressed by (column, row),
    // so a slip here desynchronises content that has no idea it depends on this
    // file.
    //
    // Changing the tree is now editing Rows. Adding a grid tier is one line
    // rather than four aligned edits across 84 literals, and every table below
    // follows on its own.
    public static class TalentSkeleton
    {
        // A row is either one node or three abreast. That is the whole
        // vocabulary the shape needs -- the root, the convergence and the
        // capstone are the singles; the grid and branch tiers are the triples.
        private enum Band
        {
            Single,
            Triple,
        }

        private readonly struct Row
        {
            public readonly Band Band;
            public readonly string Kind;

            public Row(Band band, string kind)
            {
                Band = band;
                Kind = kind;
            }

            public int Width => Band == Band.Triple ? 3 : 1;
        }

        // THE SHAPE. Everything else in this file is arithmetic over it.
        private static readonly Row[] Rows =
        {
            new Row(Band.Single, "normal"),   // the root
            new Row(Band.Triple, "normal"),   // the 3x3 grid, climbing
            new Row(Band.Triple, "normal"),
            new Row(Band.Triple, "normal"),
            new Row(Band.Single, "merge"),    // the convergence
            new Row(Band.Triple, "normal"),   // the 3-way branch, climbing
            new Row(Band.Triple, "normal"),
            new Row(Band.Triple, "normal"),
            new Row(Band.Single, "cap"),      // the capstone
        };

        public static readonly int SlotCount;

        // Depth tier, 0 (root) to 8 (capstone). Three slots share a depth
        // wherever the tree is 3-wide -- this is a tier, not a unique row.
        public static readonly int[] Depth;

        // Horizontal slot within a depth tier: -1 left, 0 centre, 1 right.
        // A single sits at 0 by definition.
        public static readonly int[] DxSlot;

        // "cap" and "merge" render larger -- sizing marks ROLE, not point cost.
        // Everything else, including the root, is "normal".
        public static readonly string[] Kind;

        // Parent slot indices per slot. Empty for the root, one entry for every
        // chain node, and three where a single sits above a triple -- the
        // convergence and the capstone, where ANY ONE invested parent unlocks
        // it.
        //
        // Derived from how the rows meet rather than listed, and the three
        // cases are the only ones the vocabulary allows:
        //
        //   triple above a single   each of the three hangs off that single
        //   triple above a triple   each hangs off the one directly below it
        //   single above a triple   it gathers all three
        //
        // Which is why the convergence and the capstone need no special case:
        // they are simply the rows where a single follows a triple.
        public static readonly int[][] Parents;

        static TalentSkeleton()
        {
            SlotCount = 0;
            foreach (var row in Rows) SlotCount += row.Width;

            Depth = new int[SlotCount];
            DxSlot = new int[SlotCount];
            Kind = new string[SlotCount];
            Parents = new int[SlotCount][];

            int slot = 0;
            var previousRow = new List<int>();

            for (int r = 0; r < Rows.Length; r++)
            {
                var row = Rows[r];
                var thisRow = new List<int>(row.Width);

                for (int i = 0; i < row.Width; i++)
                {
                    Depth[slot] = r;
                    DxSlot[slot] = row.Band == Band.Triple ? i - 1 : 0;
                    Kind[slot] = row.Kind;

                    if (r == 0)
                    {
                        Parents[slot] = new int[0];
                    }
                    else if (row.Band == Band.Single)
                    {
                        // Gathers everything under it -- one parent if the row
                        // below was a single too, three if it was a triple.
                        Parents[slot] = previousRow.ToArray();
                    }
                    else if (previousRow.Count == 1)
                    {
                        Parents[slot] = new[] { previousRow[0] };
                    }
                    else
                    {
                        // Directly below, so a strand climbs in a straight line
                        // and the left branch is reached only through the left.
                        Parents[slot] = new[] { previousRow[i] };
                    }

                    thisRow.Add(slot);
                    slot++;
                }

                previousRow = thisRow;
            }
        }

        // Total (parent, child) edges in one path's skeleton -- 24 for the
        // shape above: one for every chain slot plus three each for the two
        // convergences. Three paths share this identically.
        public static int EdgesPerPath
        {
            get
            {
                int total = 0;
                for (int i = 0; i < SlotCount; i++) total += Parents[i].Length;
                return total;
            }
        }
    }
}
