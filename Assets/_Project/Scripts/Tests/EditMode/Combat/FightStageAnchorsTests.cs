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
    // NOTHING HERE IS v1-PARITY ANY MORE, and this class has twice claimed to
    // be. First Y went, because the inherited -300/-140 were v1's numbers from
    // BEFORE v1 fixed them and they put every front row's feet behind the HUD.
    // Then X, the scales and the frame size went with the formation change:
    // depth now recedes OUTWARD rather than in, so the back rank is the one
    // furthest from the enemy instead of the one nearest it.
    public class FightStageAnchorsTests
    {
        [Test]
        public void TheAnchorsThemselvesAreWhereTheDerivationPutThem()
        {
            Assert.AreEqual(300f, FightStageAnchors.Near.X, 0.001f);
            Assert.AreEqual(-218f, FightStageAnchors.Near.Y, 0.001f);
            Assert.AreEqual(565f, FightStageAnchors.Far.X, 0.001f);
            Assert.AreEqual(-125f, FightStageAnchors.Far.Y, 0.001f);
            Assert.AreEqual(0.76f, FightStageAnchors.SpriteScale, 0.001f);
            Assert.AreEqual(-34f, FightStageAnchors.NameplateOffset, 0.001f);
            Assert.AreEqual(1200f, FightStageAnchors.StageSize.X, 0.001f);
            Assert.AreEqual(600f, FightStageAnchors.StageSize.Y, 0.001f);
        }

        [Test]
        public void ThreeSlots_LandOnTheNearAnchor_TheMidpoint_AndTheFarAnchor()
        {
            // Depths 0, 0.5, 1 across three slots; x lerps 300 -> 565, OUTWARD
            // with depth, and y lerps -218 -> -125.
            var near = FightStageAnchors.SlotOffset(0, 3, mirrored: false);
            Assert.AreEqual(300f, near.X, 0.001f);
            Assert.AreEqual(-218f, near.Y, 0.001f);

            var mid = FightStageAnchors.SlotOffset(1, 3, mirrored: false);
            Assert.AreEqual(432.5f, mid.X, 0.001f);
            Assert.AreEqual(-171.5f, mid.Y, 0.001f);

            var far = FightStageAnchors.SlotOffset(2, 3, mirrored: false);
            Assert.AreEqual(565f, far.X, 0.001f);
            Assert.AreEqual(-125f, far.Y, 0.001f);
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
            // ScaleForDepth lerps 1 -> 0.74, then everything is shrunk by 0.76.
            Assert.AreEqual(0.76f, FightStageAnchors.SlotScale(0, 3), 0.0001f);
            Assert.AreEqual(0.6612f, FightStageAnchors.SlotScale(1, 3), 0.0001f);
            Assert.AreEqual(0.5624f, FightStageAnchors.SlotScale(2, 3), 0.0001f);
        }

        [Test]
        public void ALoneCombatantSitsFullyForward()
        {
            // A single-slot row does not split the difference - a lone boss
            // should dominate the stage rather than hover at mid-depth.
            var only = FightStageAnchors.SlotOffset(0, 1, mirrored: false);
            Assert.AreEqual(300f, only.X, 0.001f);
            Assert.AreEqual(-218f, only.Y, 0.001f);
            Assert.AreEqual(0.76f, FightStageAnchors.SlotScale(0, 1), 0.0001f);
        }

        [Test]
        public void EveryStageSlotFitsInsideTheStageRect()
        {
            // The anchors and the stage size are tuned separately, so an anchor
            // tweak can push a slot outside the frame it is declared in — the
            // screen tree's containment audit would fail at build time, and
            // this says so a step earlier, in Domain, where it costs a second.
            //
            // It has already earned that: moving the far anchor out to 565 put
            // the outermost slot 65px beyond the old 1000-wide frame, and this
            // is the test that said so.
            float halfW = FightStageAnchors.StageSize.X / 2f;
            float halfH = FightStageAnchors.StageSize.Y / 2f;

            for (int i = 0; i < 3; i++)
            {
                var offset = FightStageAnchors.SlotOffset(i, 3, mirrored: false);
                Assert.LessOrEqual(System.Math.Abs(offset.X), halfW, $"slot {i} x");
                Assert.LessOrEqual(System.Math.Abs(offset.Y), halfH, $"slot {i} y");
            }
        }

        // ---- the formation adapts to how many actors are actually there ------

        // The shipped bug: a room fielding TWO monsters put them in slots 0 and
        // 1 of a three-slot formation and left slot 2 -- the widest position --
        // empty. They stood 132 apart against a 675px-wide rat sheet, so the
        // back one was 78% hidden and the pair read as a single monster.
        //
        // Literals, not a re-derivation: 300 and 565 are Near.X and Far.X, and
        // the point of the test is that a PAIR reaches both ends of that range.
        [Test]
        public void TwoActorsStandAtBothEndsOfTheRange_NotBunchedAtTheNearEnd()
        {
            var front = FightStageAnchors.SlotOffset(0, 2, mirrored: false);
            var back = FightStageAnchors.SlotOffset(1, 2, mirrored: false);

            Assert.AreEqual(300f, front.X, 0.01f, "the front of a pair should sit at the near anchor");
            Assert.AreEqual(565f, back.X, 0.01f, "the back of a pair should reach the FAR anchor, not the midpoint");
            Assert.AreEqual(265f, back.X - front.X, 0.01f,
                "a pair separated by less than the full range is the bunching this test exists to catch");
        }

        [Test]
        public void ASoloActorStandsAtTheFront()
        {
            var only = FightStageAnchors.SlotOffset(0, 1, mirrored: false);

            Assert.AreEqual(300f, only.X, 0.01f);
            Assert.AreEqual(-218f, only.Y, 0.01f, "a lone monster belongs at the near ground line, not floating mid-stage");
        }

        // Whatever the count, the outermost actor must still land at the far
        // anchor -- that is what "spread across the range" means, and it is the
        // property that stops a party of two bunching the same way.
        [TestCase(2)]
        [TestCase(3)]
        public void TheLastActorAlwaysReachesTheFarAnchor(int count)
        {
            var last = FightStageAnchors.SlotOffset(count - 1, count, mirrored: false);

            Assert.AreEqual(565f, last.X, 0.01f, $"with {count} actors the back one stops short of the far anchor");
        }

        // Mirroring is the party side, and it must mirror the SPREAD too rather
        // than only the endpoints.
        [Test]
        public void ThePartySideMirrorsTheSameSpread()
        {
            var front = FightStageAnchors.SlotOffset(0, 2, mirrored: true);
            var back = FightStageAnchors.SlotOffset(1, 2, mirrored: true);

            Assert.AreEqual(-300f, front.X, 0.01f);
            Assert.AreEqual(-565f, back.X, 0.01f);
        }
    }
}
