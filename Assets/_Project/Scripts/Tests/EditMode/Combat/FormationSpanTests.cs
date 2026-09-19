using NUnit.Framework;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // THE LINE A SPANNING LAYER LIES ON, pinned against the stage's own
    // endpoints.
    //
    // WHY THIS EXISTS. Cinderfault's fault was drawn as an axis-aligned box
    // across the enemy rank's horizontal extent, sitting on the rank's MEAN
    // ground line. The enemy rank is not level and has not been since the depth
    // line was re-derived: it runs (300, -218) -> (660, -125), so the fault
    // opened 46 units below the back body's feet and 46 above the front one's,
    // and read as a crack in a floor nobody was standing on. Owner, 2026-09-19:
    // the line "should follow the mobs, who stand in a diagonal line".
    //
    // EVERY EXPECTED NUMBER IS A LITERAL WITH ITS ARITHMETIC SPELLED OUT, never
    // recomputed by calling the thing under test (CLAUDE.md gotcha 5). The
    // INPUTS are FightStageAnchors.Near/Far, because those are the authored
    // constants this has to hold against -- if the rank moves, these numbers
    // are supposed to fail and be re-derived, not follow it silently.
    public class FormationSpanTests
    {
        // The enemy rank, end to end, as FightStageAnchors authors it.
        //
        //   run    = sqrt(360^2 + 93^2) = sqrt(138249) = 371.81850
        //   angle  = atan2(93, 360)                    =  14.48473 degrees
        private const float EnemyRun = 371.8185f;
        private const float EnemyDegrees = 14.484734f;

        private const float Tolerance = 1e-3f;

        [Test]
        public void TheEnemyRankSpansFromNearToFarOnItsOwnDiagonal()
        {
            var span = FormationSpan.Between(FightStageAnchors.Near, FightStageAnchors.Far, 0f, 0f);

            // NOT 360. The horizontal extent is 360 and the line is longer than
            // its own shadow -- which is the whole defect: a box 360 wide laid
            // level is 11.8 units short of the rank it claims to span, and
            // misses BOTH end bodies' feet by 46.5 vertically.
            Assert.AreEqual(EnemyRun, span.Length, Tolerance,
                "the span is the rank's own length, not its horizontal extent");

            Assert.AreEqual(EnemyDegrees, span.Degrees, Tolerance,
                "and it tilts by the rank's own angle, which is what 'follow the mobs' means");

            // The midpoint of (300, -218) and (660, -125), with no pads.
            Assert.AreEqual(480f, span.Centre.X, Tolerance);
            Assert.AreEqual(-171.5f, span.Centre.Y, Tolerance);
        }

        // THE CORRECTION AXIS, which is the half a rotation quietly breaks. A
        // sheet whose own ground line is not at its vertical centre is nudged
        // along the box's UP; once the box is tilted, that is no longer world
        // +Y, and correcting on +Y would slide the art off the line by the
        // correction times tan(14.48) -- 0.258 of it.
        [Test]
        public void UpIsPerpendicularToTheRankRatherThanToTheScreen()
        {
            var span = FormationSpan.Between(FightStageAnchors.Near, FightStageAnchors.Far, 0f, 0f);

            // (cos 14.48473, sin 14.48473) and that turned a quarter left.
            Assert.AreEqual(0.9682143f, span.Along.X, Tolerance);
            Assert.AreEqual(0.2501220f, span.Along.Y, Tolerance);
            Assert.AreEqual(-0.2501220f, span.Up.X, Tolerance);
            Assert.AreEqual(0.9682143f, span.Up.Y, Tolerance);

            // Perpendicular, stated as the dot product rather than implied by
            // the four numbers above.
            Assert.AreEqual(0f, span.Along.X * span.Up.X + span.Along.Y * span.Up.Y, Tolerance);
        }

        // TWO PADS, NOT ONE, because the two ends sit at different depths and
        // are therefore drawn at different scales -- so each end reaches past
        // its own body by its own half-width. 60 at the front and 40 at the
        // back is the shape of that difference, not a measurement of it.
        [Test]
        public void EachEndIsPushedOutByItsOwnPadAlongTheLine()
        {
            var span = FormationSpan.Between(FightStageAnchors.Near, FightStageAnchors.Far, 60f, 40f);

            // 371.81850 + 60 + 40.
            Assert.AreEqual(471.8185f, span.Length, Tolerance);

            // The pads move the ends ALONG the line, so the midpoint drifts on
            // both axes: 480 + (40 - 60)/2 x 0.9682143 = 470.31786, and
            // -171.5 + (40 - 60)/2 x 0.2501220 = -174.00122.
            Assert.AreEqual(470.31786f, span.Centre.X, Tolerance);
            Assert.AreEqual(-174.00122f, span.Centre.Y, Tolerance);

            // The angle is a property of the two bodies, not of how far the
            // drawing reaches past them.
            Assert.AreEqual(EnemyDegrees, span.Degrees, Tolerance);
        }

        // THE PARTY'S RANK LEANS HARDER, and it is in here because a model
        // validated against one formation is validated against nothing: the
        // party's own endpoints (320, -218) -> (810, -64) are a different
        // length AND a different angle, and the same call answers both.
        //
        //   run   = sqrt(490^2 + 154^2) = sqrt(263816) = 513.63022
        //   angle = atan2(154, 490)                    =  17.44719 degrees
        [Test]
        public void ThePartyRankGetsItsOwnAngleFromTheSameCall()
        {
            var party = FightStageAnchors.Party;
            var span = FormationSpan.Between(party.Near, party.Far, 0f, 0f);

            Assert.AreEqual(513.63022f, span.Length, Tolerance);
            Assert.AreEqual(17.447188f, span.Degrees, Tolerance,
                "three degrees steeper than the enemy's, because the HUD pushed its back rank up");
        }

        // ONE STRUCK BODY IS NOT A LINE. atan2(0, 0) is 0 on every platform
        // this runs on and is still not a number anyone should have to look
        // up, so the degenerate case is answered outright -- and it answers
        // with the level box a lone target always had.
        [Test]
        public void ALoneBodyLiesLevelRatherThanAtSomeUndefinedAngle()
        {
            var at = new UiVec(300f, -218f);
            var span = FormationSpan.Between(at, at, 55f, 55f);

            Assert.AreEqual(0f, span.Degrees, Tolerance);
            Assert.AreEqual(110f, span.Length, Tolerance, "the two pads and nothing between them");
            Assert.AreEqual(300f, span.Centre.X, Tolerance);
            Assert.AreEqual(-218f, span.Centre.Y, Tolerance);
            Assert.AreEqual(1f, span.Along.X, Tolerance);
            Assert.AreEqual(0f, span.Along.Y, Tolerance);
        }

        // THE CALLER ORDERS THE ENDPOINTS LEFT TO RIGHT, and this is what that
        // contract is worth: handed them the other way round the same rank
        // comes back 180 degrees out, which would render a crack in the floor
        // upside down. Asserted rather than commented, because the ordering
        // lives at the call site (FightController.PlaceAlongRank) where an
        // EditMode test cannot reach it -- so the cost of getting it wrong is
        // pinned here instead.
        [Test]
        public void ReversingTheEndpointsReversesTheAngle()
        {
            var span = FormationSpan.Between(FightStageAnchors.Far, FightStageAnchors.Near, 0f, 0f);

            Assert.AreEqual(EnemyDegrees - 180f, span.Degrees, Tolerance);
            Assert.AreEqual(EnemyRun, span.Length, Tolerance, "the length does not care about direction");
        }
    }
}
