using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The fight HUD's round counter and overlay, as rules
    // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.4, M6). Literal values
    // throughout: the Bell's own numbers are "Toll", a limit of 10, and an
    // overlay that closes in from one scale to another in nine steps.
    public class FightRoundPresentationTests
    {
        private static FightRoundPresentation Bell(float from = 1f, float to = 1.9f, string label = "Toll") =>
            new FightRoundPresentation(roundLimit: 10, label: label, roundSfxPath: "Audio/Sfx/toll",
                overlayKey: "Assets/_Project/Art/Events/bell_in_the_fog/flock.png",
                overlayFromScale: from, overlayToScale: to, ambiencePath: "Audio/Ambience/fog");

        // ---- visibility ------------------------------------------------------------

        [Test]
        public void ARoomFightShowsNoCounterNoOverlayAndNoBed()
        {
            var none = FightRoundPresentation.None;

            Assert.IsFalse(none.ShowsCounter);
            Assert.IsFalse(none.HasOverlay);
            Assert.AreEqual("", none.CounterText(3));
            Assert.AreEqual("", none.AmbiencePath);
            Assert.AreEqual("", none.BackdropKey);
        }

        [Test]
        public void AFightWithARoundLimitShowsTheCounter()
        {
            Assert.IsTrue(Bell().ShowsCounter);
            Assert.IsTrue(Bell().HasOverlay);
        }

        // An overlay path only means something across a limit (the content
        // build refuses it without one); a hand-built presentation that has
        // one anyway still shows nothing.
        [Test]
        public void AnOverlayWithoutARoundLimitIsNotShown()
        {
            var noLimit = new FightRoundPresentation(overlayKey: "Assets/_Project/Art/Events/x/flock.png");

            Assert.IsFalse(noLimit.ShowsCounter);
            Assert.IsFalse(noLimit.HasOverlay);
        }

        // ---- text ------------------------------------------------------------------

        [Test]
        public void TheCounterReadsTheLabelAndTheRound()
        {
            Assert.AreEqual("Toll 1", Bell().CounterText(1));
            Assert.AreEqual("Toll 3", Bell().CounterText(3));
            Assert.AreEqual("Toll 10", Bell().CounterText(10));
        }

        // Round 11 is the one that ends the fight Survived and never starts;
        // the counter never reads past the limit, nor below 1.
        [Test]
        public void TheCounterNeverReadsPastTheLimitOrBelowOne()
        {
            Assert.AreEqual("Toll 10", Bell().CounterText(11));
            Assert.AreEqual("Toll 1", Bell().CounterText(0));
        }

        [Test]
        public void AnUnlabelledLimitReadsRound()
        {
            Assert.AreEqual("Round 4", Bell(label: "").CounterText(4));
            Assert.AreEqual("Round 4", Bell(label: "   ").CounterText(4));
        }

        // The counter's box is audited against this sample at every aspect, so
        // the sample must be exactly as long as the longest label content can
        // author, plus the three-digit round.
        [Test]
        public void TheAuditSampleIsALabelAtTheContentCapPlusAThreeDigitRound()
        {
            Assert.AreEqual(12, EventEntryResolver.MaxRoundLabelLength);
            Assert.AreEqual("Wolfsong Hou 999", UiStrings.FightRoundCounter.AuditSample);
        }

        // ---- the overlay -----------------------------------------------------------

        [Test]
        public void TheOverlayStepsFromFromScaleToToScaleAcrossTheLimit()
        {
            var bell = Bell(from: 1f, to: 1.9f);

            Assert.AreEqual(1.0f, bell.OverlayScaleFor(1), 1e-5f);
            Assert.AreEqual(1.1f, bell.OverlayScaleFor(2), 1e-5f);
            Assert.AreEqual(1.5f, bell.OverlayScaleFor(6), 1e-5f);
            Assert.AreEqual(1.9f, bell.OverlayScaleFor(10), 1e-5f);
        }

        [Test]
        public void TheOverlayHoldsItsEndsOutsideTheLimit()
        {
            var bell = Bell(from: 1f, to: 1.9f);

            Assert.AreEqual(1.0f, bell.OverlayScaleFor(0), 1e-5f);
            Assert.AreEqual(1.9f, bell.OverlayScaleFor(11), 1e-5f);
        }

        // Closing in is the Bell's use; stepping the other way is the same rule.
        [Test]
        public void TheOverlayCanStepDownAsWellAsUp()
        {
            var receding = Bell(from: 2f, to: 1f);

            Assert.AreEqual(2.0f, receding.OverlayScaleFor(1), 1e-5f);
            Assert.AreEqual(1.0f, receding.OverlayScaleFor(10), 1e-5f);
        }

        [Test]
        public void ALimitOfOneShowsToScale()
        {
            var one = new FightRoundPresentation(roundLimit: 1, overlayKey: "k", overlayFromScale: 1f, overlayToScale: 3f);

            Assert.AreEqual(3f, one.OverlayScaleFor(1), 1e-5f);
        }
    }
}
