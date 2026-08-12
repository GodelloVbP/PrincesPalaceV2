using NUnit.Framework;
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.Domain.Tests
{
    // Pins for the combat stage's pixel anchors.
    //
    // Every expected value is a LITERAL, never a re-derivation of the formula
    // being tested (CLAUDE.md gotcha 5). That matters more than usual here:
    // these numbers were tuned against the real art, so a test that recomputed
    // StageLayout's own lerp would happily agree with a stage that had silently
    // moved.
    //
    // NO LONGER v1-PARITY on Y, and the class used to say it was. The inherited
    // -300/-140 were v1's numbers from BEFORE v1 fixed them, and they put every
    // front-row figure's feet behind the HUD. The Y pins below are v2's own
    // derivation; X, the scales and the sizes are still v1's.
    public class FightStageAnchorsTests
    {
        [Test]
        public void TheAnchorsThemselvesAreWhereTheDerivationPutThem()
        {
            Assert.AreEqual(470f, FightStageAnchors.Near.X, 0.001f);
            Assert.AreEqual(-228f, FightStageAnchors.Near.Y, 0.001f);
            Assert.AreEqual(250f, FightStageAnchors.Far.X, 0.001f);
            Assert.AreEqual(-68f, FightStageAnchors.Far.Y, 0.001f);
            Assert.AreEqual(0.78f, FightStageAnchors.SpriteScale, 0.001f);
            Assert.AreEqual(-34f, FightStageAnchors.NameplateOffset, 0.001f);
            Assert.AreEqual(1000f, FightStageAnchors.StageSize.X, 0.001f);
            Assert.AreEqual(600f, FightStageAnchors.StageSize.Y, 0.001f);
        }

        [Test]
        public void ThreeSlots_LandOnTheNearAnchor_TheMidpoint_AndTheFarAnchor()
        {
            // Depths 0, 0.5, 1 across three slots; x lerps 470 -> 250 and
            // y lerps -228 -> -68.
            var near = FightStageAnchors.SlotOffset(0, 3, mirrored: false);
            Assert.AreEqual(470f, near.X, 0.001f);
            Assert.AreEqual(-228f, near.Y, 0.001f);

            var mid = FightStageAnchors.SlotOffset(1, 3, mirrored: false);
            Assert.AreEqual(360f, mid.X, 0.001f);
            Assert.AreEqual(-148f, mid.Y, 0.001f);

            var far = FightStageAnchors.SlotOffset(2, 3, mirrored: false);
            Assert.AreEqual(250f, far.X, 0.001f);
            Assert.AreEqual(-68f, far.Y, 0.001f);
        }

        [Test]
        public void MirroringFlipsXOnly()
        {
            var right = FightStageAnchors.SlotOffset(1, 3, mirrored: false);
            var left = FightStageAnchors.SlotOffset(1, 3, mirrored: true);

            Assert.AreEqual(-right.X, left.X, 0.001f);
            Assert.AreEqual(right.Y, left.Y, 0.001f, "the two sides share a ground line");
        }

        [Test]
        public void SlotScale_ComposesTheDepthCurveWithTheGlobalShrink()
        {
            // ScaleForDepth lerps 1 -> 0.58, then everything is shrunk by 0.78.
            Assert.AreEqual(0.78f, FightStageAnchors.SlotScale(0, 3), 0.0001f);
            Assert.AreEqual(0.6162f, FightStageAnchors.SlotScale(1, 3), 0.0001f);
            Assert.AreEqual(0.4524f, FightStageAnchors.SlotScale(2, 3), 0.0001f);
        }

        [Test]
        public void ALoneCombatantSitsFullyForward()
        {
            // A single-slot row does not split the difference - a lone boss
            // should dominate the stage rather than hover at mid-depth.
            var only = FightStageAnchors.SlotOffset(0, 1, mirrored: false);
            Assert.AreEqual(470f, only.X, 0.001f);
            Assert.AreEqual(-228f, only.Y, 0.001f);
            Assert.AreEqual(0.78f, FightStageAnchors.SlotScale(0, 1), 0.0001f);
        }

        [Test]
        public void EveryStageSlotFitsInsideTheStageRect()
        {
            // The anchors and the stage size were tuned separately in v1. If a
            // future anchor tweak pushes a slot outside the 1000x600 rect, the
            // screen tree's containment audit would fail at build time - this
            // says so a step earlier, in Domain, where it costs a second.
            float halfW = FightStageAnchors.StageSize.X / 2f;
            float halfH = FightStageAnchors.StageSize.Y / 2f;

            for (int i = 0; i < 3; i++)
            {
                var offset = FightStageAnchors.SlotOffset(i, 3, mirrored: false);
                Assert.LessOrEqual(System.Math.Abs(offset.X), halfW, $"slot {i} x");
                Assert.LessOrEqual(System.Math.Abs(offset.Y), halfH, $"slot {i} y");
            }
        }
    }
}
