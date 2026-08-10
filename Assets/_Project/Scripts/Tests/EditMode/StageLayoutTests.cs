using System;
using NUnit.Framework;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.Tests
{
    public class StageLayoutTests
    {
        [Test]
        public void DepthForSlot_SpreadsSlotsFromNearToFar()
        {
            Assert.AreEqual(0f, StageLayout.DepthForSlot(0, 3), 1e-5f, "Slot 0 is always nearest");
            Assert.AreEqual(0.5f, StageLayout.DepthForSlot(1, 3), 1e-5f);
            Assert.AreEqual(1f, StageLayout.DepthForSlot(2, 3), 1e-5f, "The last slot is always farthest");
        }

        // A lone enemy (every Boss room) should command the front of the
        // stage, not sit at mid-depth as a naive index/(count-1) with a
        // divide-by-zero guard might leave it.
        [Test]
        public void DepthForSlot_WithASingleSlot_PlacesItFullyForward()
        {
            Assert.AreEqual(0f, StageLayout.DepthForSlot(0, 1), 1e-5f);
        }

        [Test]
        public void DepthForSlot_RejectsOutOfRangeInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StageLayout.DepthForSlot(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => StageLayout.DepthForSlot(-1, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => StageLayout.DepthForSlot(3, 3));
        }

        [Test]
        public void ScaleForDepth_ShrinksWithDistance()
        {
            Assert.AreEqual(StageLayout.NearScale, StageLayout.ScaleForDepth(0f), 1e-5f);
            Assert.AreEqual(StageLayout.FarScale, StageLayout.ScaleForDepth(1f), 1e-5f);
            Assert.Less(StageLayout.ScaleForDepth(0.5f), StageLayout.ScaleForDepth(0f));
            Assert.Greater(StageLayout.ScaleForDepth(0.5f), StageLayout.ScaleForDepth(1f));
        }

        [Test]
        public void ScaleForDepth_ClampsBeyondTheEnds()
        {
            Assert.AreEqual(StageLayout.NearScale, StageLayout.ScaleForDepth(-5f), 1e-5f);
            Assert.AreEqual(StageLayout.FarScale, StageLayout.ScaleForDepth(5f), 1e-5f);
        }

        [Test]
        public void PositionForDepth_InterpolatesBetweenTheSceneAnchors()
        {
            Assert.AreEqual(-30f, StageLayout.PositionForDepth(-30f, 200f, 0f), 1e-4f);
            Assert.AreEqual(200f, StageLayout.PositionForDepth(-30f, 200f, 1f), 1e-4f);
            Assert.AreEqual(85f, StageLayout.PositionForDepth(-30f, 200f, 0.5f), 1e-4f);
        }

        // The whole point of the fake depth: a nearer figure must paint over
        // a farther one. uGUI draws later siblings on top, so slot 0 (near)
        // needs the HIGHEST sibling index.
        [Test]
        public void SiblingIndexForSlot_PutsNearestLastSoItDrawsOnTop()
        {
            Assert.AreEqual(2, StageLayout.SiblingIndexForSlot(0, 3), "Nearest slot draws last (on top)");
            Assert.AreEqual(1, StageLayout.SiblingIndexForSlot(1, 3));
            Assert.AreEqual(0, StageLayout.SiblingIndexForSlot(2, 3), "Farthest slot draws first (behind)");
        }

        [Test]
        public void SiblingIndexForSlot_IsAPermutationOfEverySlot()
        {
            const int slots = 3;
            var seen = new bool[slots];
            for (int i = 0; i < slots; i++)
            {
                int sibling = StageLayout.SiblingIndexForSlot(i, slots);
                Assert.IsFalse(seen[sibling], $"Sibling index {sibling} was assigned twice — slots would fight over draw order");
                seen[sibling] = true;
            }

            CollectionAssert.DoesNotContain(seen, false, "Every sibling index must be used exactly once");
        }

        [Test]
        public void SiblingIndexForSlot_RejectsOutOfRangeInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StageLayout.SiblingIndexForSlot(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => StageLayout.SiblingIndexForSlot(3, 3));
        }

        // Guards the invariant the stage actually depends on, rather than
        // the specific constants: nearer must always be bigger, whatever the
        // tuning values become.
        [Test]
        public void ScaleAndDepth_AgreeAcrossEverySlotOfAFullStage()
        {
            const int slots = 3;
            float previousScale = float.MaxValue;
            for (int i = 0; i < slots; i++)
            {
                float scale = StageLayout.ScaleForDepth(StageLayout.DepthForSlot(i, slots));
                Assert.Less(scale, previousScale, $"Slot {i} should be smaller than the slot in front of it");
                previousScale = scale;
            }
        }
    }
}
