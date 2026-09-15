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
    // The art, measured off the committed PNGs, about each canvas's own
    // centre, in canvas pixels. TIGHT is the outermost opaque pixel (Unity's
    // own trim, which is where the production path reads it from); MASS is the
    // outermost column carrying at least OpaqueSpan.MassFraction of the
    // tallest column's opaque rows, counted at the same 0.02 alpha floor
    // StageActorAnimator uses. The two are allowed to disagree by a pixel on
    // the tight pair, because Unity trims at the importer's own alpha
    // tolerance and nothing here depends on the tight pair any more:
    //
    //                              tight          mass
    //   Characters/bear  idle     -157..155     -120..135
    //   Characters/bear  slam     -209..237     -172..200
    //   Characters/bear  rush     -249..206     -201..165
    //   Enemies/beetle   idle     -130..339      -83..280
    //   Enemies/beetle   turtle_up  -46..212     -37..204
    //
    // Drawn mirrored (every enemy is), those last two become -339..130 /
    // -280..83 and -212..46 / -204..37.
    //
    // THE ARITHMETIC BELOW READS THE MASS PAIR, which is the 2026-09-15
    // change: the beetle's horn trails 58 canvas pixels past its body and
    // Bjorn used to stop 12px short of the tip of it. StageStandOff's own
    // header argues the rule; OpaqueSpan.MassFraction argues the threshold.
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
        private static readonly OpaqueSpan BearSlam = new OpaqueSpan(-209f, 237f, -172f, 200f);
        private static readonly OpaqueSpan BeetleIdleDrawn = new OpaqueSpan(-339f, 130f, -280f, 83f);
        private static readonly OpaqueSpan BeetleTurtleUpDrawn = new OpaqueSpan(-212f, 46f, -204f, 37f);
        private static readonly OpaqueSpan BearIdle = new OpaqueSpan(-157f, 155f, -120f, 135f);

        // 300 + (-280 * 0.7144) - 12 - (200 * 0.7144)
        //   = 300 - 200.032 - 12 - 142.88
        //   = -54.912, so from -320 the travel is 265.088.
        [Test]
        public void BjornSlamsFromTheFrontSlot_AndStopsShortOfTheBeetle()
        {
            var travel = StageStandOff.TravelTo(
                new UiVec(-320f, -218f), BearSlam, 0.7144f,
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f,
                StageStandOff.Gap);

            Assert.AreEqual(265.088f, travel.X, 0.001f);
            Assert.AreEqual(0f, travel.Y, 0.001f, "same ground line, so no climb");
        }

        // AND HIS HAMMER IS NOW INSIDE THE BEETLE, on purpose. The tight edges
        // are what the eye sees overlap, and the whole point of closing to the
        // bodies instead is that the drawings interpenetrate at impact.
        //
        // Hammer tip: -320 + 265.088 + 237*0.7144 = 114.4008.
        // The beetle faces left, so its outermost pixel is the horn tip at
        // 300 - 339*0.7144 = 57.8184 and its BODY starts further right, at
        // 300 - 280*0.7144 = 99.968. The hammer tip lands past both: 56.58px
        // past the horn it used to stop in front of, and 14.43px inside the
        // body -- which is 37 canvas pixels of hammer beyond the hammer's own
        // mass edge, less the 12px the two bodies keep between them.
        [Test]
        public void TheHammerOverlapsTheBeetleAtImpact_WhichIsWhatTheMassEdgesBuy()
        {
            var travel = StageStandOff.TravelTo(
                new UiVec(-320f, -218f), BearSlam, 0.7144f,
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f,
                StageStandOff.Gap);

            float hammerTip = -320f + travel.X + 237f * 0.7144f;
            Assert.AreEqual(56.5824f, hammerTip - (300f - 339f * 0.7144f), 0.001f,
                "past the horn tip, which used to be where he stopped");
            Assert.AreEqual(14.4328f, hammerTip - (300f - 280f * 0.7144f), 0.001f,
                "past the body edge the stand-off aimed at");
        }

        // THE OWNER'S SECOND COMPLAINT, as arithmetic. The old rule travelled
        // CloseFraction (0.78) of the 620px gap = 483.6, putting Bjorn's mark
        // at 163.6 and his hammer's leading edge at 163.6 + 237*0.7144 =
        // 332.9 -- while the beetle's outermost pixel sits at
        // 300 - 339*0.7144 = 57.8. Two hundred and seventy-five pixels inside
        // the thing he was hitting. (266 until the front party mark moved
        // -360 -> -320: a fraction of a gap gets worse as the gap grows, which
        // is the whole complaint.)
        //
        // The 12px the rule DOES leave is between the two bodies:
        //   Bjorn's body edge  -320 + 265.088 + 200*0.7144 =  87.968
        //   beetle's body edge  300 - 280*0.7144           =  99.968
        [Test]
        public void TheStandOffLeavesExactlyTheAuthoredGap_WhereTheOldFractionLeftMinus266()
        {
            var travel = StageStandOff.TravelTo(
                new UiVec(-320f, -218f), BearSlam, 0.7144f,
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f,
                StageStandOff.Gap);

            float bjornBodyEdge = -320f + travel.X + 200f * 0.7144f;
            float beetleBodyEdge = 300f + -280f * 0.7144f;

            Assert.AreEqual(12f, beetleBodyEdge - bjornBodyEdge, 0.001f);
            Assert.AreEqual(-275.1f, (300f + -339f * 0.7144f) - (-320f + 483.6f + 237f * 0.7144f), 0.1f,
                "the fraction this replaced, restated so the regression is legible");
        }

        // THE OWNER'S FIRST COMPLAINT: "when attacking from the middle and
        // backline ... you stop earlier and not in front of the enemy". The
        // stand-off POINT is the same wherever the attacker started -- only
        // the distance travelled changes -- which is the whole property the
        // fractions did not have.
        //
        // Slot 1: -565, scale 0.6688. 300 - 200.032 - 12 - 200*0.6688
        //   = -45.7920, travel 519.2080.
        // Slot 2: -810, scale 0.6232. 300 - 200.032 - 12 - 200*0.6232
        //   = -36.6720, travel 773.3280.
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

            Assert.AreEqual(265.0880f, front.X, 0.001f);
            Assert.AreEqual(519.2080f, middle.X, 0.001f);
            Assert.AreEqual(773.3280f, back.X, 0.001f);

            Assert.AreEqual(-54.9120f, -320f + front.X, 0.001f);
            Assert.AreEqual(-45.7920f, -565f + middle.X, 0.001f);
            Assert.AreEqual(-36.6720f, -810f + back.X, 0.001f);
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

            Assert.AreEqual(277.0880f, charge.X, 0.001f);
            Assert.AreEqual(12f, charge.X - swing.X, 0.001f);
        }

        // THE OTHER DIRECTION. An enemy swinging leftward at the party reads
        // the mirror image of the same rule, and nothing in TravelTo is
        // written per side.
        //
        // Beetle at 300 attacking Bjorn at -320: its own forward (screen-left)
        // body edge is -280 * 0.7144 = -200.032, Bjorn's near (screen-right)
        // body edge is -320 + 135*0.7144 = -223.5560, and the gap is added
        // back the other way. -223.5560 + 12 + 200.032 = -11.5240, travel
        // -311.5240.
        [Test]
        public void AnEnemySwingingLeftwardUsesTheSameRuleMirrored()
        {
            var travel = StageStandOff.TravelTo(
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f,
                new UiVec(-320f, -218f), BearIdle, 0.7144f,
                StageStandOff.Gap);

            Assert.AreEqual(-311.5240f, travel.X, 0.001f);
        }

        // THE BEETLE'S BARREL ROLL, AND THE DAYLIGHT THAT USED TO BE IN IT.
        //
        // A Charge is contract-bound to end AGAINST what it hit
        // (StageStandOff.ChargeGap = 0), and the beetle's stopped 85.5px short
        // -- photographed by MeleeStandOffCaptureTests, not inferred. The
        // arithmetic below is the whole of the cause: the reach handed in was
        // the beetle's IDLE, whose body edge is 76 canvas pixels wider than
        // the "turtle_up" it is actually wearing when it arrives.
        //
        // Beetle at 300 rolling leftward into Bjorn at -320, both at 0.7144.
        // Bjorn's near (screen-right) body edge: -320 + 135*0.7144 = -223.5560.
        // Wearing turtle_up, forward (screen-left) body edge -204*0.7144 =
        //   -145.7376, so it stands at -223.5560 + 145.7376 = -77.8184 and
        //   travels -377.8184.
        // Measured as the idle instead, -280*0.7144 = -200.032, so it stops
        //   at -223.5560 + 200.032 = -23.5240 and travels -323.5240 -- and
        //   then draws turtle_up there, whose body edge lands at
        //   -23.5240 - 145.7376 = -169.2616, exactly 54.2944px short of Bjorn.
        [Test]
        public void AChargeMeasuredAgainstTheDrawingItWears_EndsAgainstItsTarget()
        {
            var worn = StageStandOff.TravelTo(
                new UiVec(300f, -218f), BeetleTurtleUpDrawn, 0.7144f,
                new UiVec(-320f, -218f), BearIdle, 0.7144f,
                StageStandOff.ChargeGap);

            Assert.AreEqual(-377.8184f, worn.X, 0.001f);

            float forwardEdge = 300f + worn.X - 204f * 0.7144f;
            Assert.AreEqual(-223.5560f, forwardEdge, 0.001f, "flush against Bjorn's near body edge");

            // The counterfactual, restated so the regression is legible --
            // the same call with the drawing the actor does NOT wear.
            var asIdle = StageStandOff.TravelTo(
                new UiVec(300f, -218f), BeetleIdleDrawn, 0.7144f,
                new UiVec(-320f, -218f), BearIdle, 0.7144f,
                StageStandOff.ChargeGap);

            Assert.AreEqual(-323.5240f, asIdle.X, 0.001f);
            Assert.AreEqual(54.2944f, (300f + asIdle.X - 204f * 0.7144f) - (-223.5560f), 0.001f,
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
        // measured -- so everything downstream reads Left as screen-left. Both
        // pairs turn around together, and the beetle is the case that matters:
        // its horn is on the art's right and on the screen's left.
        [Test]
        public void MirroringASpanFlipsItAboutTheCanvasCentre()
        {
            var span = new OpaqueSpan(-130f, 339f, -83f, 280f).Mirrored(-1f);

            Assert.AreEqual(-339f, span.Left, 0.001f);
            Assert.AreEqual(130f, span.Right, 0.001f);
            Assert.AreEqual(-280f, span.MassLeft, 0.001f, "the horn now faces screen-left");
            Assert.AreEqual(83f, span.MassRight, 0.001f);

            var unflipped = new OpaqueSpan(-130f, 339f, -83f, 280f).Mirrored(1f);
            Assert.AreEqual(-130f, unflipped.Left, 0.001f);
            Assert.AreEqual(339f, unflipped.Right, 0.001f);
            Assert.AreEqual(-83f, unflipped.MassLeft, 0.001f);
            Assert.AreEqual(280f, unflipped.MassRight, 0.001f);
        }

        // A SPAN THAT COULD NOT BE MEASURED FOR MASS is its own tight box, so
        // an unreadable texture stands a figure further off rather than
        // putting its mass edges at zero -- which, on a drawing sitting well
        // off its own canvas centre, would be an arrival point inside the
        // target rather than in front of it.
        [Test]
        public void ASpanWithNoMassMeasurement_FallsBackToItsTightEdges()
        {
            var span = new OpaqueSpan(-130f, 339f);

            Assert.AreEqual(-130f, span.MassLeft, 0.001f);
            Assert.AreEqual(339f, span.MassRight, 0.001f);

            Assert.AreEqual(0f, OpaqueSpan.None.MassLeft, 0.001f);
            Assert.AreEqual(0f, OpaqueSpan.None.MassRight, 0.001f);
        }
    }
}
