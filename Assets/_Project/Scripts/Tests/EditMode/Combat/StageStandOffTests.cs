using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // WHERE AN ATTACKER STOPS, pinned with literal expected values worked out
    // by hand from the real art rather than by re-running TravelTo's own
    // arithmetic (CLAUDE.md gotcha 5). The numbers below are the ones the
    // owner's two complaints are actually about, so a regression that
    // re-introduces either of them fails here rather than in a screenshot
    // somebody has to look at.
    //
    // The art, measured off the committed PNGs (opaque box about each canvas's
    // own centre, in canvas pixels):
    //
    //   Characters/bear  idle  -157..155   slam  -209..237   rush -249..206
    //   Enemies/beetle   idle  -130..339,  drawn mirrored -> -339..130
    //   Enemies/beetle   turtle_up  -46..212,  drawn mirrored -> -212..46
    //
    // The stage, at three slots (FightStageAnchors / StageLayout):
    //
    //   enemy slot 0   x  300   scale 0.7144
    //   party slot 0   x -320   scale 0.7144
    //   party slot 1   x -565   scale 0.6688
    //   party slot 2   x -810   scale 0.6232
    //
    // The three party marks widened from -360/-540/-720 on 2026-09-09 (see
    // FightStageAnchors.PartyNearX). Only the DISTANCES below moved with
    // them: the arrival point is derived from the target and the attacker's
    // own reach, so it is the same three numbers it was, which is the
    // property TheArrivalPointBarelyMovesWithTheSlotYouStartedIn exists to
    // state and the reason this re-pin was mechanical.
    public class StageStandOffTests
    {
        private static readonly OpaqueSpan BearSlam = new OpaqueSpan(-209f, 237f);
        private static readonly OpaqueSpan BeetleIdleDrawn = new OpaqueSpan(-339f, 130f);
        private static readonly OpaqueSpan BeetleTurtleUpDrawn = new OpaqueSpan(-212f, 46f);
        private static readonly OpaqueSpan BearIdle = new OpaqueSpan(-157f, 155f);

        // 300 + (-339 * 0.7144) - 12 - (237 * 0.7144)
        //   = 300 - 242.1816 - 12 - 169.3128
        //   = -123.4944, so from -320 the travel is 196.5056.
        [Test]
        public void BjornSlamsFromTheFrontSlot_AndStopsShortOfTheBeetle()
        {
            var travel = StageStandOff.TravelTo(
                new UiVec(-320f, -218f), BearSlam, 0.7144f,
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f,
                StageStandOff.Gap);

            Assert.AreEqual(196.5056f, travel.X, 0.001f);
            Assert.AreEqual(0f, travel.Y, 0.001f, "same ground line, so no climb");
        }

        // THE OWNER'S SECOND COMPLAINT, as arithmetic. The old rule travelled
        // CloseFraction (0.78) of the 620px gap = 483.6, putting Bjorn's mark
        // at 163.6 and his hammer's leading edge at 163.6 + 237*0.7144 =
        // 332.9 -- while the beetle's near edge sits at 300 - 339*0.7144 =
        // 57.8. Two hundred and seventy-five pixels inside the thing he was
        // hitting. (266 until the front party mark moved -360 -> -320: a
        // fraction of a gap gets worse as the gap grows, which is the whole
        // complaint.)
        [Test]
        public void TheStandOffLeavesExactlyTheAuthoredGap_WhereTheOldFractionLeftMinus266()
        {
            var travel = StageStandOff.TravelTo(
                new UiVec(-320f, -218f), BearSlam, 0.7144f,
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f,
                StageStandOff.Gap);

            float hammerEdge = -320f + travel.X + 237f * 0.7144f;
            float beetleNearEdge = 300f + -339f * 0.7144f;

            Assert.AreEqual(12f, beetleNearEdge - hammerEdge, 0.001f);
            Assert.AreEqual(-275.1f, beetleNearEdge - (-320f + 483.6f + 237f * 0.7144f), 0.1f,
                "the fraction this replaced, restated so the regression is legible");
        }

        // THE OWNER'S FIRST COMPLAINT: "when attacking from the middle and
        // backline ... you stop earlier and not in front of the enemy". The
        // stand-off POINT is the same wherever the attacker started -- only
        // the distance travelled changes -- which is the whole property the
        // fractions did not have.
        //
        // Slot 1: -565, scale 0.6688. 300 - 242.1816 - 12 - 237*0.6688
        //   = -112.6872, travel 452.3128.
        // Slot 2: -810, scale 0.6232. 300 - 242.1816 - 12 - 237*0.6232
        //   = -101.8800, travel 708.1200.
        // The two arrivals differ only because a smaller depth scale draws a
        // shorter hammer, which is the correct reason to differ.
        [Test]
        public void TheArrivalPointBarelyMovesWithTheSlotYouStartedIn()
        {
            var front = StageStandOff.TravelTo(new UiVec(-320f, -218f), BearSlam, 0.7144f,
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f, StageStandOff.Gap);
            var middle = StageStandOff.TravelTo(new UiVec(-565f, -141f), BearSlam, 0.6688f,
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f, StageStandOff.Gap);
            var back = StageStandOff.TravelTo(new UiVec(-810f, -64f), BearSlam, 0.6232f,
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f, StageStandOff.Gap);

            Assert.AreEqual(196.5056f, front.X, 0.001f);
            Assert.AreEqual(452.3128f, middle.X, 0.001f);
            Assert.AreEqual(708.1200f, back.X, 0.001f);

            Assert.AreEqual(-123.4944f, -320f + front.X, 0.001f);
            Assert.AreEqual(-112.6872f, -565f + middle.X, 0.001f);
            Assert.AreEqual(-101.8800f, -810f + back.X, 0.001f);
        }

        // AND IT CLIMBS. A back-rank attacker used to swing from its own row,
        // level with the target's head; the stand-off lands on the target's
        // ground line.
        [Test]
        public void AttackingOutOfTheBackRankStepsDownToTheTargetsGroundLine()
        {
            var travel = StageStandOff.TravelTo(new UiVec(-810f, -64f), BearSlam, 0.6232f,
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f, StageStandOff.Gap);

            Assert.AreEqual(-154f, travel.Y, 0.001f, "-218 minus -64");
        }

        // A CHARGE ENDS AGAINST WHAT IT HIT: 12px further in than the swing,
        // which is exactly the gap the swing leaves.
        [Test]
        public void AChargeClosesTheGapCompletely()
        {
            var swing = StageStandOff.TravelTo(new UiVec(-320f, -218f), BearSlam, 0.7144f,
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f, StageStandOff.Gap);
            var charge = StageStandOff.TravelTo(new UiVec(-320f, -218f), BearSlam, 0.7144f,
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f, StageStandOff.ChargeGap);

            Assert.AreEqual(208.5056f, charge.X, 0.001f);
            Assert.AreEqual(12f, charge.X - swing.X, 0.001f);
        }

        // THE OTHER DIRECTION. An enemy swinging leftward at the party reads
        // the mirror image of the same rule, and nothing in TravelTo is
        // written per side.
        //
        // Beetle at 300 attacking Bjorn at -320: its own forward (screen-left)
        // reach is -339 * 0.7144 = -242.1816, Bjorn's near (screen-right) edge
        // is -320 + 155*0.7144 = -209.2680, and the gap is added back the
        // other way. -209.2680 + 12 + 242.1816 = 44.9136, travel -255.0864.
        [Test]
        public void AnEnemySwingingLeftwardUsesTheSameRuleMirrored()
        {
            var travel = StageStandOff.TravelTo(
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f,
                new UiVec(-320f, -218f), BearIdle, 0.7144f,
                StageStandOff.Gap);

            Assert.AreEqual(-255.0864f, travel.X, 0.001f);
        }

        // THE BEETLE'S BARREL ROLL, AND THE DAYLIGHT THAT USED TO BE IN IT.
        //
        // A Charge is contract-bound to end AGAINST what it hit
        // (StageStandOff.ChargeGap = 0), and the beetle's stopped 85.5px short
        // -- photographed by MeleeStandOffCaptureTests, not inferred. The
        // arithmetic below is the whole of the cause: the reach handed in was
        // the beetle's IDLE, 127 canvas pixels wider than the "turtle_up" it
        // is actually wearing when it arrives.
        //
        // Beetle at 300 rolling leftward into Bjorn at -320, both at 0.7144.
        // Bjorn's near (screen-right) edge: -320 + 155*0.7144 = -209.2680.
        // Wearing turtle_up, forward (screen-left) reach -212*0.7144 =
        //   -151.4528, so it stands at -209.2680 + 151.4528 = -57.8152 and
        //   travels -357.8152.
        // Measured as the idle instead, -339*0.7144 = -242.1816, so it stops
        //   at -209.2680 + 242.1816 = 32.9136 and travels -267.0864 -- and
        //   then draws turtle_up there, whose forward edge lands at
        //   32.9136 - 151.4528 = -118.5392, exactly 90.7288px short of Bjorn.
        [Test]
        public void AChargeMeasuredAgainstTheDrawingItWears_EndsAgainstItsTarget()
        {
            var worn = StageStandOff.TravelTo(
                new UiVec(300f, -218f), BeetleTurtleUpDrawn, 0.7144f,
                new UiVec(-320f, -218f), BearIdle, 0.7144f,
                StageStandOff.ChargeGap);

            Assert.AreEqual(-357.8152f, worn.X, 0.001f);

            float forwardEdge = 300f + worn.X - 212f * 0.7144f;
            Assert.AreEqual(-209.2680f, forwardEdge, 0.001f, "flush against Bjorn's near edge");

            // The counterfactual, restated so the regression is legible --
            // the same call with the drawing the actor does NOT wear.
            var asIdle = StageStandOff.TravelTo(
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f,
                new UiVec(-320f, -218f), BearIdle, 0.7144f,
                StageStandOff.ChargeGap);

            Assert.AreEqual(-267.0864f, asIdle.X, 0.001f);
            Assert.AreEqual(90.7288f, (300f + asIdle.X - 212f * 0.7144f) - (-209.2680f), 0.001f,
                "daylight left by measuring a pose the beetle never wears");
        }

        // WHICH DRAWINGS THE STAND-OFF IS ENTITLED TO MEASURE, which is the
        // half of the fix above that is not arithmetic. A Charge with a strike
        // and no phase poses wears exactly one drawing; the list must not
        // carry the null that OpenStanceFor/ArrivalStanceFor use for "keep
        // wearing what you have", because the measurement seam answers a null
        // stance with the idle.
        [Test]
        public void ACharge_WearsOnlyItsStrike_AndTheStandOffIsToldSo()
        {
            var worn = CombatBeat.StandOffStancesFor(StageApproach.Charge, "turtle_up", null, null);

            CollectionAssert.AreEqual(new[] { "turtle_up" }, worn.ToArray());
        }

        // AND BJORN'S SLAM STILL GETS ALL THREE, which is the property the
        // reach union was introduced for in the first place -- his rush
        // reaches 206 past his canvas centre, his overhead 165, his slam 237,
        // and a distance set by any one of them lets the other two clip.
        [Test]
        public void ACloseWearingThreePoses_HandsAllThreeToTheStandOff()
        {
            var worn = CombatBeat.StandOffStancesFor(StageApproach.Close, "slam", "rush", "overhead");

            CollectionAssert.AreEqual(new[] { "rush", "overhead", "slam" }, worn.ToArray());
        }

        // A BEAT THAT AUTHORS NOTHING measures nothing by name: the actor
        // crosses in whatever it already has on, and the empty list is how the
        // caller is told to ask for that rather than being handed a null it
        // would silently resolve to the idle.
        [Test]
        public void ABeatWithNoStrikeAtAll_NamesNoDrawingToMeasure()
        {
            CollectionAssert.IsEmpty(CombatBeat.StandOffStancesFor(StageApproach.Lunge, null, "rush", "overhead"));
            CollectionAssert.IsEmpty(CombatBeat.StandOffStancesFor(StageApproach.Close, "  ", null, null));
        }

        // THE ONE CAP LEFT. Two figures already inside each other's reach do
        // not shuffle backwards to make the gap; they stay put.
        [Test]
        public void AnAttackerAlreadyOnTopOfItsTargetDoesNotStepAway()
        {
            var travel = StageStandOff.TravelTo(
                new UiVec(0f, -218f), new OpaqueSpan(-400f, 400f), 1f,
                new UiVec(100f, -218f), new OpaqueSpan(-400f, 400f), 1f,
                StageStandOff.Gap);

            Assert.AreEqual(0f, travel.X, 0.001f);
            Assert.AreEqual(0f, travel.Y, 0.001f);
        }

        // The mirror is applied to the SPAN, once, where the drawing is
        // measured -- so everything downstream reads Left as screen-left.
        [Test]
        public void MirroringASpanFlipsItAboutTheCanvasCentre()
        {
            var span = new OpaqueSpan(-130f, 339f).Mirrored(-1f);

            Assert.AreEqual(-339f, span.Left, 0.001f);
            Assert.AreEqual(130f, span.Right, 0.001f);

            var unflipped = new OpaqueSpan(-130f, 339f).Mirrored(1f);
            Assert.AreEqual(-130f, unflipped.Left, 0.001f);
            Assert.AreEqual(339f, unflipped.Right, 0.001f);
        }
    }
}
