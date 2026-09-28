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

        // The Bell as authored: 0.35 to 0.6 in nine equal steps of 0.0277778.
        [Test]
        public void TheBellsFlockStepsFrom035To06()
        {
            var bell = Bell(from: 0.35f, to: 0.6f);

            Assert.AreEqual(0.35f, bell.OverlayScaleFor(1), 1e-5f);
            Assert.AreEqual(0.4611111f, bell.OverlayScaleFor(5), 1e-5f);
            Assert.AreEqual(0.6f, bell.OverlayScaleFor(10), 1e-5f);
        }

        // ---- where the overlay stands ------------------------------------------------

        private static FightRoundPresentation Anchored(float pivotX, float pivotY, float anchorX, float anchorY) =>
            new FightRoundPresentation(roundLimit: 10, overlayKey: "k", overlayFromScale: 0.35f, overlayToScale: 0.8f,
                overlayPivotX: pivotX, overlayPivotY: pivotY, overlayAnchorX: anchorX, overlayAnchorY: anchorY);

        [Test]
        public void TheDefaultsAreTheCentreScaledFullFrame()
        {
            var place = new FightRoundPresentation(roundLimit: 10, overlayKey: "k").PlaceOverlay(1920f, 1080f, 16f / 9f);

            Assert.AreEqual(0.5f, place.PivotX, 1e-5f);
            Assert.AreEqual(0.5f, place.PivotY, 1e-5f);
            Assert.AreEqual(0f, place.OffsetX, 1e-3f);
            Assert.AreEqual(0f, place.OffsetY, 1e-3f);
        }

        // The Bell at 16:9: the flock's feet, 15% up the image (162 of 1080),
        // lifted 378 onto the fog line half-way up the frame (540).
        [Test]
        public void TheBellsFlockStandsOnTheFogLine_At16x9()
        {
            var place = Anchored(0.5f, 0.15f, 0.5f, 0.5f).PlaceOverlay(1920f, 1080f, 16f / 9f);

            Assert.AreEqual(0.5f, place.PivotX, 1e-5f);
            Assert.AreEqual(0.15f, place.PivotY, 1e-5f);
            Assert.AreEqual(0f, place.OffsetX, 1e-3f);
            Assert.AreEqual(378f, place.OffsetY, 1e-3f);
        }

        // At 4:3 the frame is 1920x1440 and the 16:9 image is fitted with
        // 180 above and below: its feet are at 180 + 162 = 342 of 1440, and
        // the fog line (the backdrop fills the frame) is at 720.
        [Test]
        public void TheBellsFlockStandsOnTheFogLine_At4x3()
        {
            var place = Anchored(0.5f, 0.15f, 0.5f, 0.5f).PlaceOverlay(1920f, 1440f, 16f / 9f);

            Assert.AreEqual(0.5f, place.PivotX, 1e-5f);
            Assert.AreEqual(0.2375f, place.PivotY, 1e-5f);
            Assert.AreEqual(0f, place.OffsetX, 1e-3f);
            Assert.AreEqual(378f, place.OffsetY, 1e-3f);
        }

        // A frame wider than the image fits by height: 1080 high, 1920 wide
        // inside a 2560 frame, 320 either side. Pivot 0.25 of the image is at
        // 320 + 480 = 800; anchor 0.75 of 2560 is 1920.
        [Test]
        public void AWideFrameFitsTheImageByHeight()
        {
            var place = Anchored(0.25f, 0.5f, 0.75f, 0.5f).PlaceOverlay(2560f, 1080f, 16f / 9f);

            Assert.AreEqual(0.3125f, place.PivotX, 1e-5f);
            Assert.AreEqual(0.5f, place.PivotY, 1e-5f);
            Assert.AreEqual(1120f, place.OffsetX, 1e-3f);
            Assert.AreEqual(0f, place.OffsetY, 1e-3f);
        }

        [Test]
        public void ADegenerateFrameFallsBackToTheCentre()
        {
            var place = Anchored(0.5f, 0.15f, 0.5f, 0.5f).PlaceOverlay(0f, 0f, 16f / 9f);

            Assert.AreEqual(0.5f, place.PivotX, 1e-5f);
            Assert.AreEqual(0.5f, place.PivotY, 1e-5f);
            Assert.AreEqual(0f, place.OffsetY, 1e-3f);
        }

        [Test]
        public void ALimitOfOneShowsToScale()
        {
            var one = new FightRoundPresentation(roundLimit: 1, overlayKey: "k", overlayFromScale: 1f, overlayToScale: 3f);

            Assert.AreEqual(3f, one.OverlayScaleFor(1), 1e-5f);
        }
    }
}
