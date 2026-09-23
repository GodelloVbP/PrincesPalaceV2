using NUnit.Framework;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // THE AIR A CALLED STRIKE AMASSES IN, AND THE TURN THAT POINTS IT AT ITS
    // TARGET. Owner, 2026-09-23: Winter's Rebuke "should form and amass in the
    // air around the middle, then fire itself diagonally at the mob".
    //
    // Literals with their arithmetic spelled out (CLAUDE.md gotcha 5). The rat
    // body is TargetBodyTests' lone front-rank rat: left 132.8304, right
    // 412.1608, bottom -218, top -25.112, centre x 272.4956.
    public class SpellFlightTests
    {
        private const float Tolerance = 1e-3f;

        private static readonly StageBody Rat = new StageBody(132.8304f, 412.1608f, -218f, -25.112f);

        [Test]
        public void TheAirPointIsHalfwayAcrossAndAboveTheTallerBody()
        {
            // A caster leaving from (-400, -100) whose head is at 7.
            var sky = SpellFlight.SkyPoint(new UiVec(-400f, -100f), 7f, Rat);

            // x: (-400 + 272.4956) / 2 = -63.7522
            // y: max(7, -25.112) + 110 = 117 -- the caster is the taller.
            Assert.AreEqual(-63.7522f, sky.X, Tolerance);
            Assert.AreEqual(117f, sky.Y, Tolerance);
        }

        [Test]
        public void ATallTargetLiftsTheAirPointOnlyAsFarAsTheCeiling()
        {
            // A treant-height body, head at 239.86: 239.86 + 110 = 349.86,
            // capped at 240.
            var treant = new StageBody(58.64f, 419.1262f, -216.96412f, 239.85896f);
            var sky = SpellFlight.SkyPoint(new UiVec(-400f, -100f), 7f, treant);

            Assert.AreEqual(240f, sky.Y, Tolerance);
        }

        [Test]
        public void TheFlightFromTheAirToTheRatRunsDownAndAcross()
        {
            // (-63.7522, 117) -> (272.4956, -121.556):
            // atan2(-238.556, 336.2478) = -35.3544 degrees.
            float degrees = SpellFlight.DegreesOf(new UiVec(-63.7522f, 117f), new UiVec(272.4956f, -121.556f));

            Assert.AreEqual(-35.3544f, degrees, 1e-2f);
        }

        [Test]
        public void ASpearPaintedPointingUpIsTurnedDownOntoItsFlight()
        {
            // Winter's Rebuke is painted pointing 26 degrees up. Flown along
            // -35.3544 it turns -61.3544.
            Assert.AreEqual(-61.3544f, SpellFlight.Turn(-35.3544f, 26f, 1f), Tolerance);
        }

        [Test]
        public void AMirroredSheetTurnsFromItsMirroredDirection()
        {
            // Mirrored, a sheet painted at 26 points at 180 - 26 = 154. Flown
            // leftward-and-down at -144.6456 (the mirror image of -35.3544) it
            // turns -144.6456 - 154 = -298.6456, normalised +61.3544 -- the
            // mirror image of the unmirrored turn, which is what a mirrored
            // cast has to be.
            Assert.AreEqual(61.3544f, SpellFlight.Turn(-144.6456f, 26f, -1f), Tolerance);
        }

        [Test]
        public void ASheetAlreadyPaintedAlongItsFlightIsNotTurned()
        {
            Assert.AreEqual(0f, SpellFlight.Turn(-45f, -45f, 1f), Tolerance);
        }

        [Test]
        public void TheImpactPointTurnsWithTheBox()
        {
            // A 100 x 100 box, impact on its right edge's middle: local (50, 0).
            // Turned a quarter anticlockwise it is straight above the centre.
            var up = SpellFlight.ImpactOffset(1f, 0.5f, new UiVec(100f, 100f), 1f, 90f);
            Assert.AreEqual(0f, up.X, Tolerance);
            Assert.AreEqual(50f, up.Y, Tolerance);

            // Mirrored, the right edge is drawn on the left: local (-50, 0),
            // turned a quarter it is straight below.
            var down = SpellFlight.ImpactOffset(1f, 0.5f, new UiVec(100f, 100f), -1f, 90f);
            Assert.AreEqual(0f, down.X, Tolerance);
            Assert.AreEqual(-50f, down.Y, Tolerance);
        }

        [Test]
        public void AnUnturnedImpactOffsetIsTheSheetFractionOfTheArt()
        {
            // Winter's Rebuke's tip, (0.88, 0.625) of a 250 square:
            // (0.38 x 250, 0.125 x 250) = (95, 31.25).
            var tip = SpellFlight.ImpactOffset(0.88f, 0.625f, new UiVec(250f, 250f), 1f, 0f);
            Assert.AreEqual(95f, tip.X, Tolerance);
            Assert.AreEqual(31.25f, tip.Y, Tolerance);
        }
    }
}
