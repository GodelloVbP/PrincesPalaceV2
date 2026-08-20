using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Talents;

namespace PrincesPalace.Domain.Tests
{
    // THE SKELETON'S FOUR TABLES, PINNED AS LITERALS.
    //
    // Depth, DxSlot, Kind and Parents were four hand-aligned arrays of 21, and
    // the shape they describe is the same shape talents.json is authored
    // against -- 294 talents addressed by (column, row). Getting one entry out
    // of step with the others would not fail to compile and would not look
    // wrong; it would quietly move an orb, or unlock a node from the wrong
    // parent, or price it as the wrong kind.
    //
    // These literals are the tables as they stood BEFORE they were derived from
    // the rows they are actually made of. They exist so that derivation is
    // provably a no-op rather than a rewrite anyone has to eyeball -- and they
    // stay afterwards, because they are also the thing that catches a change to
    // the row table that nobody meant to make.
    public class TalentSkeletonShapeTests
    {
        private static readonly int[] ExpectedDepth =
        {
            0,
            1, 1, 1,
            2, 2, 2,
            3, 3, 3,
            4,
            5, 5, 5,
            6, 6, 6,
            7, 7, 7,
            8,
        };

        private static readonly int[] ExpectedDx =
        {
            0,
            -1, 0, 1,
            -1, 0, 1,
            -1, 0, 1,
            0,
            -1, 0, 1,
            -1, 0, 1,
            -1, 0, 1,
            0,
        };

        private static readonly string[] ExpectedKind =
        {
            "normal",
            "normal", "normal", "normal",
            "normal", "normal", "normal",
            "normal", "normal", "normal",
            "merge",
            "normal", "normal", "normal",
            "normal", "normal", "normal",
            "normal", "normal", "normal",
            "cap",
        };

        private static readonly int[][] ExpectedParents =
        {
            new int[0],
            new[] { 0 }, new[] { 0 }, new[] { 0 },
            new[] { 1 }, new[] { 2 }, new[] { 3 },
            new[] { 4 }, new[] { 5 }, new[] { 6 },
            new[] { 7, 8, 9 },
            new[] { 10 }, new[] { 10 }, new[] { 10 },
            new[] { 11 }, new[] { 12 }, new[] { 13 },
            new[] { 14 }, new[] { 15 }, new[] { 16 },
            new[] { 17, 18, 19 },
        };

        [Test]
        public void TheSlotCountIsTheShapesOwn()
        {
            Assert.AreEqual(ExpectedDepth.Length, TalentSkeleton.SlotCount);
        }

        [Test]
        public void TheDepthTiersAreUnchanged()
        {
            CollectionAssert.AreEqual(ExpectedDepth, TalentSkeleton.Depth);
        }

        [Test]
        public void TheHorizontalSlotsAreUnchanged()
        {
            CollectionAssert.AreEqual(ExpectedDx, TalentSkeleton.DxSlot);
        }

        [Test]
        public void TheRolesAreUnchanged()
        {
            CollectionAssert.AreEqual(ExpectedKind, TalentSkeleton.Kind);
        }

        [Test]
        public void TheParentGraphIsUnchanged()
        {
            Assert.AreEqual(ExpectedParents.Length, TalentSkeleton.Parents.Length,
                "the skeleton has a different number of slots than the pinned shape");

            for (int slot = 0; slot < ExpectedParents.Length; slot++)
            {
                CollectionAssert.AreEqual(ExpectedParents[slot], TalentSkeleton.Parents[slot],
                    $"slot {slot} is reached from a different parent than the pinned shape says");
            }
        }

        [Test]
        public void TheEdgeCountIsTheSumOfTheParents()
        {
            Assert.AreEqual(ExpectedParents.Sum(p => p.Length), TalentSkeleton.EdgesPerPath);
        }

        // The four tables have to agree with each other, whatever they say --
        // which is the property that made them dangerous to hand-align and is
        // free once they come from one description.
        [Test]
        public void EveryTableCoversEverySlot()
        {
            Assert.AreEqual(TalentSkeleton.SlotCount, TalentSkeleton.Depth.Length);
            Assert.AreEqual(TalentSkeleton.SlotCount, TalentSkeleton.DxSlot.Length);
            Assert.AreEqual(TalentSkeleton.SlotCount, TalentSkeleton.Kind.Length);
            Assert.AreEqual(TalentSkeleton.SlotCount, TalentSkeleton.Parents.Length);
        }

        // A parent must sit strictly below its child, or the tree has a cycle
        // and TalentPage's frontier walk would never terminate honestly.
        [Test]
        public void EveryParentIsShallowerThanItsChild()
        {
            for (int slot = 0; slot < TalentSkeleton.SlotCount; slot++)
            {
                foreach (int parent in TalentSkeleton.Parents[slot])
                {
                    Assert.Less(TalentSkeleton.Depth[parent], TalentSkeleton.Depth[slot],
                        $"slot {slot} is reached from slot {parent}, which is not below it");
                }
            }
        }
    }
}
