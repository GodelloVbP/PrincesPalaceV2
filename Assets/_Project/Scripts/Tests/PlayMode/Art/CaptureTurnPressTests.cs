using NUnit.Framework;

namespace PrincesPalace.PlayModeTests
{
    // A series' forced press belongs to one TURN: the same squadmate's next turn
    // (after a stunned teammate's skipped turn played its beat) needs a press of
    // its own. Keyed on the actor alone it was "already armed" and stalled the
    // five-cast Gilded Aegis film at cast 4.
    public class CaptureTurnPressTests
    {
        [Test]
        public void APressHoldsForItsTurnAndAgesByFrames()
        {
            var odette = new object();
            var press = new TurnPress();
            Assert.IsFalse(press.Holds(odette));

            press.Take(odette, 10);
            Assert.IsTrue(press.Holds(odette));
            Assert.AreEqual(25, press.Age(35));
            Assert.IsFalse(press.Holds(new object()), "another actor's turn is not covered");
        }

        [Test]
        public void ABeatBetweenTurnsReleasesItSoTheNextTurnIsPressedAgain()
        {
            var odette = new object();
            var press = new TurnPress();
            press.Take(odette, 0);

            press.Release();

            Assert.IsFalse(press.Holds(odette), "Odette's next turn is a new one");
            press.Take(odette, 90);
            Assert.AreEqual(0, press.Age(90), "and its stall clock starts over");
        }

        [Test]
        public void NothingHoldsANullActor()
        {
            var press = new TurnPress();
            Assert.IsFalse(press.Holds(null));
        }
    }
}
