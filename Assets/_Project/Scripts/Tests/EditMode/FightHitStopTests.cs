using NUnit.Framework;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // The freeze at contact, and how hard a blow has to be to earn one.
    //
    // THE ARITHMETIC IS THE RISK, not the effect. A pause that reads well and
    // quietly lengthens every beat desynchronises the whole round -- and it
    // would do it invisibly, because a beat running 60ms long looks like
    // nothing until twelve of them have run and the log is ahead of the stage.
    // FightBeatPlayer's own SettleAfter comment records that exact line going
    // to zero once with nothing able to see it; the budget half of this rule is
    // pinned in FightBeatPacingTests, where SettleAfter itself lives.
    //
    // What is here is the part that decides how hard everything hit -- the one
    // number the hit-stop, the stage shake and the target's squash all key off,
    // so getting it wrong misfires three effects at once.
    public class FightHitStopTests
    {
        private static float Weight(int amount, int maxHealth, bool healing = false) =>
            HitStop.Weight(amount, maxHealth, healing);

        private static float Stop(int amount, int maxHealth, bool healing = false) =>
            HitStop.SecondsFor(Weight(amount, maxHealth, healing));

        // ---- what counts as a hard hit ----------------------------------------

        [Test]
        public void AChipHitWeighsAlmostNothingAndAQuarterBarWeighsEverything()
        {
            Assert.AreEqual(0f, Weight(0, 400), 0.001f, "nothing landed");
            Assert.AreEqual(0.1f, Weight(10, 400), 0.001f, "10 against a 100-point quarter");
            Assert.AreEqual(1f, Weight(100, 400), 0.001f, "a quarter of the bar is the top of the scale");
        }

        // Past the ceiling everything is the same size, on purpose: the range
        // is better spent separating a scratch from a real blow than separating
        // enormous from enormous.
        [Test]
        public void AnOverwhelmingHitDoesNotClimbPastTheCeiling()
        {
            Assert.AreEqual(1f, Weight(9999, 400), 0.001f);
        }

        // AGAINST MAX HEALTH, NOT REMAINING -- asserted structurally, because
        // there is no parameter through which remaining health could reach this
        // rule. A later change that started dividing by what is left would have
        // to break this signature to do it, which is a louder failure than a
        // number moving.
        [Test]
        public void TheSameBlowWeighsTheSameWhateverIsLeftOnTheBar()
        {
            Assert.AreEqual(0.5f, Weight(50, 400), 0.001f);
            Assert.AreEqual(Weight(50, 400), Weight(50, 400), 0.001f);
        }

        // A heal is not a small impact, it is a different event. Shaking the
        // stage for one would teach the player that the shake means nothing.
        [Test]
        public void AHealNeverStopsTheGame()
        {
            Assert.AreEqual(0f, Weight(80, 400, healing: true), 0.001f);
            Assert.AreEqual(0f, Stop(80, 400, healing: true), 0.001f);
        }

        [Test]
        public void NothingToWeighAgainstIsNoWeightRatherThanADivideByZero()
        {
            Assert.AreEqual(0f, Weight(50, 0), 0.001f);
            Assert.AreEqual(0f, Weight(-5, 400), 0.001f);
        }

        // ---- the stop itself ---------------------------------------------------

        // Every landed blow gets SOME stop: the effect is punctuation, and
        // punctuation that only appears on long sentences reads as an accident
        // rather than as emphasis.
        [Test]
        public void EveryLandedBlowStopsAtLeastALittleAndNoneStopsTooLong()
        {
            // A HAIR ABOVE THE FLOOR RATHER THAN ON IT, since the response
            // curve went in: 1 into a 4000-point bar is 0.001 of the ceiling
            // and the square root lifts that to 0.03, which is the curve doing
            // exactly what it exists for. The floor is still the floor.
            Assert.GreaterOrEqual(Stop(1, 4000), HitStop.MinSeconds);
            Assert.Less(Stop(1, 4000), HitStop.MinSeconds + 0.01f);
            Assert.AreEqual(HitStop.MaxSeconds, Stop(9999, 400), 0.001f);
        }

        // THE CURVE THE THREE EFFECTS SHARE, and the reason amplitude alone
        // did not fix "hits do not feel like they land": an ordinary exchange
        // on this roster sits at 0.1 to 0.35 of the weight ceiling, so a linear
        // mapping spent the whole tuned range on the rare enormous blow.
        //
        // Pinned at both ends and in the middle. The ends must not move -- a
        // curve that changed what "nothing" or "as hard as it gets" means would
        // be a different scale, not a reshaped one.
        [Test]
        public void TheResponseCurveLiftsAnOrdinaryHitWithoutMovingEitherEnd()
        {
            Assert.AreEqual(0f, HitStop.Response(0f), 0.0001f);
            Assert.AreEqual(1f, HitStop.Response(1f), 0.0001f);
            Assert.AreEqual(1f, HitStop.Response(4f), 0.0001f, "past the ceiling is still the ceiling");

            Assert.AreEqual(0.387f, HitStop.Response(0.15f), 0.002f, "a typical rat swing");
            Assert.AreEqual(0.707f, HitStop.Response(0.5f), 0.002f);
        }

        // Monotonic, or a harder hit could shake the stage less than a softer
        // one -- the same rule the stop itself is held to below, on the input
        // all three effects now share.
        [Test]
        public void TheResponseCurveNeverFallsAsTheBlowGetsHarder()
        {
            float previous = -1f;
            for (int step = 0; step <= 100; step++)
            {
                float here = HitStop.Response(step / 100f);
                Assert.GreaterOrEqual(here, previous, $"weight {step / 100f} responds weaker than the one below it");
                previous = here;
            }
        }

        [Test]
        public void NothingLandedIsNoStopAtAll()
        {
            Assert.AreEqual(0f, Stop(0, 400), 0.001f);
        }

        // Where a freeze stops reading as weight and starts reading as the game
        // hitching. Pinned as a literal because it is a judgement about
        // perception rather than a derivation -- if somebody raises it, that
        // should be a decision rather than a drift.
        //
        // RAISED FROM A TENTH TO A FIFTH, on request, after a tenth went in and
        // the blows still did not land. The literal moved because somebody
        // decided it should, which is the test working rather than the test
        // being wrong.
        [Test]
        public void TheLongestStopStaysUnderAFifthOfASecond()
        {
            Assert.LessOrEqual(HitStop.MaxSeconds, 0.2f);
            Assert.Less(HitStop.MinSeconds, HitStop.MaxSeconds);
        }

        // The scale has to be monotonic or a harder hit could stop for less
        // time than a softer one, which is the one thing that would make the
        // effect read as random rather than as weight.
        [Test]
        public void AHarderHitNeverStopsForLessTimeThanASofterOne()
        {
            float previous = 0f;

            for (int amount = 0; amount <= 200; amount += 5)
            {
                float stop = Stop(amount, 400);
                Assert.GreaterOrEqual(stop, previous, $"{amount} damage stopped for less than the hit below it");
                previous = stop;
            }
        }
    }
}
