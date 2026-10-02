using NUnit.Framework;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // THE PART OF A PROJECTILE'S LINE THAT HAS BEEN CROSSED, which is what the
    // rift left by the Blackglass Spear is drawn into. Literals, with the
    // arithmetic spelled out (CLAUDE.md gotcha 5).
    //
    // The line is the air point (-63.7522, 117) to the rat's middle
    // (272.4956, -121.556), the flight SpellFlightTests pins at -35.3544
    // degrees. Its whole length is sqrt(336.2478^2 + 238.556^2) = 412.276.
    public class FlightSpanTests
    {
        private const float Tolerance = 1e-2f;

        private static readonly UiVec Air = new UiVec(-63.7522f, 117f);
        private static readonly UiVec Rat = new UiVec(272.4956f, -121.556f);

        [Test]
        public void TheWholeLineIsCentredOnItsMidpointAndTurnedOntoTheFlight()
        {
            var span = FlightSpan.Between(Air, Rat, 1f);

            // x: (-63.7522 + 272.4956) / 2 = 104.3717; y: (117 - 121.556) / 2 = -2.278
            Assert.AreEqual(104.3717f, span.Centre.X, Tolerance);
            Assert.AreEqual(-2.278f, span.Centre.Y, Tolerance);
            Assert.AreEqual(412.276f, span.Length, Tolerance);
            Assert.AreEqual(-35.3544f, span.Degrees, Tolerance);
        }

        [Test]
        public void ThePartCrossedGrowsOutOfTheLaunchPoint()
        {
            // A quarter of the way: 0.25 x 412.276 = 103.069 long, centred
            // an eighth of the way along: x -63.7522 + 336.2478 / 8 = -21.7212,
            // y 117 - 238.556 / 8 = 87.1805.
            var span = FlightSpan.Between(Air, Rat, 0.25f);

            Assert.AreEqual(-21.7212f, span.Centre.X, Tolerance);
            Assert.AreEqual(87.1805f, span.Centre.Y, Tolerance);
            Assert.AreEqual(103.069f, span.Length, Tolerance);
            Assert.AreEqual(-35.3544f, span.Degrees, Tolerance);
        }

        [Test]
        public void NothingIsCrossedBeforeTheLaunch_AndTheFractionIsClamped()
        {
            Assert.AreEqual(0f, FlightSpan.Between(Air, Rat, 0f).Length, Tolerance);
            Assert.AreEqual(0f, FlightSpan.Between(Air, Rat, -0.5f).Length, Tolerance);
            Assert.AreEqual(412.276f, FlightSpan.Between(Air, Rat, 1.7f).Length, Tolerance);
        }

        [Test]
        public void ALeftwardFlightIsTurnedRightWayUp()
        {
            // (200, 100) -> (0, 0): atan2(-100, -200) = -153.4349, which is
            // 26.5651 once turned into (-90, 90] -- the same line, drawn the
            // right way up.
            var span = FlightSpan.Between(new UiVec(200f, 100f), new UiVec(0f, 0f), 1f);

            Assert.AreEqual(26.5651f, span.Degrees, Tolerance);
            Assert.AreEqual(100f, span.Centre.X, Tolerance);
            Assert.AreEqual(50f, span.Centre.Y, Tolerance);
            Assert.AreEqual(223.6068f, span.Length, Tolerance);
        }

        [Test]
        public void ADegenerateFlightIsALevelNothing()
        {
            var span = FlightSpan.Between(Air, Air, 1f);

            Assert.AreEqual(0f, span.Length, Tolerance);
            Assert.AreEqual(0f, span.Degrees, Tolerance);
        }
    }
}
