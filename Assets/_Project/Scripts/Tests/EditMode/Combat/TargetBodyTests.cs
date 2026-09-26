using NUnit.Framework;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // THE TARGET-BOUNDS CONTRACT: where a struck body is on stage and what
    // `fit: target` sizes a layer by.
    //
    // WHY THIS EXISTS. Every per-target spell layer aimed at the slot and
    // sized off it, and a slot is the sprite's CANVAS -- padding for the
    // tallest pose included. Owner, 2026-09-23: Court of Whispers "does not
    // scale with the mob's height/width".
    //
    // EVERY EXPECTED NUMBER IS A LITERAL with its arithmetic in the comment,
    // never recomputed through the code under test (CLAUDE.md gotcha 5). The
    // inputs are the delivered idle drawings' own opaque boxes, measured off
    // Resources/Enemies/{rat,treant}/idle.png, and a lone front-rank slot's
    // scale: 0.94 (StageLayout.NearScale) x 0.76 (SpriteScale) = 0.7144.
    public class TargetBodyTests
    {
        private const float Tolerance = 1e-3f;
        private const float FrontRank = 0.7144f;

        // Rat idle: canvas 616 wide, ground line 8, opaque x 74..465,
        // y 8..278 up from the canvas bottom.
        private static StageBody RatLocal(float mirror) =>
            TargetBody.SlotLocal(74f, 8f, 465f, 278f, 616f, 8f, mirror);

        [Test]
        public void TheBodyIsMeasuredFromTheSlotsPivotNotTheCanvasCorner()
        {
            var rat = RatLocal(1f);

            // x: 74 - 616/2 = -234, 465 - 308 = 157. The rat sits 38.5px LEFT
            // of its own canvas centre, which a slot-centred aim missed by.
            Assert.AreEqual(-234f, rat.Left, Tolerance);
            Assert.AreEqual(157f, rat.Right, Tolerance);

            // y: 8 - 8 = 0 on the ground line, 278 - 8 = 270 at the head.
            Assert.AreEqual(0f, rat.Bottom, Tolerance);
            Assert.AreEqual(270f, rat.Top, Tolerance);
        }

        [Test]
        public void AMirroredSpriteSwapsWhichEdgeIsLeft()
        {
            var rat = RatLocal(-1f);

            Assert.AreEqual(-157f, rat.Left, Tolerance);
            Assert.AreEqual(234f, rat.Right, Tolerance);
            Assert.AreEqual(270f, rat.Top, Tolerance);
        }

        [Test]
        public void OnStageTheBodyIsTheSlotOriginPlusTheLocalBoxAtRestingScale()
        {
            // A lone rat at FightStageAnchors.Near (300, -218).
            var rat = TargetBody.OnStage(RatLocal(1f), FrontRank, new UiVec(300f, -218f));

            //   left   300 - 234 x 0.7144 = 132.8304
            //   right  300 + 157 x 0.7144 = 412.1608
            //   bottom -218
            //   top    -218 + 270 x 0.7144 = -25.112
            Assert.AreEqual(132.8304f, rat.Left, Tolerance);
            Assert.AreEqual(412.1608f, rat.Right, Tolerance);
            Assert.AreEqual(-218f, rat.Bottom, Tolerance);
            Assert.AreEqual(-25.112f, rat.Top, Tolerance);

            // centre (272.4956, -121.556)
            Assert.AreEqual(272.4956f, rat.Centre.X, Tolerance);
            Assert.AreEqual(-121.556f, rat.Centre.Y, Tolerance);
        }

        [Test]
        public void TheReferenceRatFitsAtVeryNearlyItsAuthoredSize()
        {
            var rat = TargetBody.OnStage(RatLocal(1f), FrontRank, new UiVec(300f, -218f));

            // Extent is the WIDTH for a rat: 279.3304 against a height of
            // 192.888. Over the 280 reference: 0.9976086.
            Assert.AreEqual(279.3304f, rat.Extent, Tolerance);
            Assert.AreEqual(0.9976086f, TargetBody.FitFactor(rat), 1e-5f);
        }

        [Test]
        public void ATallTargetFitsByItsHeightNotItsWidth()
        {
            // Elder Treant idle: canvas 646, ground line 8, opaque x 90..438,
            // y 9..450. stageScale 1.45, so 0.7144 x 1.45 = 1.03588.
            var local = TargetBody.SlotLocal(90f, 9f, 438f, 450f, 646f, 8f, 1f);
            var treant = TargetBody.OnStage(local, 1.03588f, new UiVec(300f, -218f));

            // width 348 x 1.03588 = 360.48624; height 441 x 1.03588 = 456.82308.
            Assert.AreEqual(456.82308f, treant.Extent, Tolerance);

            // 456.82308 / 280 = 1.631511 -- the ritual that reads as a patch on
            // a rat-sized box is 1.63x on the treant, where the old stageScale
            // multiplier gave 1.45 and a canvas measure gave neither.
            Assert.AreEqual(1.631511f, TargetBody.FitFactor(treant), 1e-5f);
        }

        [Test]
        public void ABodyWithNoExtentFitsAtItsAuthoredSizeRatherThanNothing()
        {
            Assert.AreEqual(1f, TargetBody.FitFactor(default), Tolerance);
        }

        // ---- a tail never replaces what it struck ------------------------------

        // The Elder Treant in the front rank (SpellFlightTests' literal):
        // 360.4862 wide, 456.82308 tall -> extent 456.82308.
        private static readonly StageBody Treant = new StageBody(58.64f, 419.1262f, -216.96412f, 239.85896f);

        [Test]
        public void ATailFittedPastThreeQuartersOfTheBodyIsDrawnAtThreeQuarters()
        {
            // Winter's Rebuke's impact, 320 x fit 1.6315 = 522.08 on the treant.
            // Cap 0.75 x 456.82308 = 342.61731; 342.61731 / 522.08 = 0.65626.
            float scale = TargetBody.TailScale(522.08f, Treant);
            Assert.AreEqual(0.65626f, scale, 1e-4f);
            Assert.AreEqual(342.6173f, 522.08f * scale, 1e-2f,
                "the drawn tail must not exceed three quarters of the struck body's larger extent");
        }

        [Test]
        public void ATailAlreadyInsideTheBodyIsLeftAlone()
        {
            // 300 < 342.61731.
            Assert.AreEqual(1f, TargetBody.TailScale(300f, Treant), Tolerance);
        }

        [Test]
        public void ATailOnABodyWithNoExtentKeepsItsAuthoredSize()
        {
            Assert.AreEqual(1f, TargetBody.TailScale(320f, default), Tolerance);
        }
    }
}
