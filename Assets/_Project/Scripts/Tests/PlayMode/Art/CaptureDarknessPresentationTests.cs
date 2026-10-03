using NUnit.Framework;

namespace PrincesPalace.PlayModeTests
{
    // The darkness a capture scenario binds (CaptureScenarioRunner.Darkness):
    // an opacity held for every round, none when 0.
    public class CaptureDarknessPresentationTests
    {
        [Test]
        public void ZeroBindsNoPresentation()
        {
            Assert.IsNull(CaptureScenarioRunner.DarknessPresentation(0f));
        }

        [Test]
        public void AnOpacityIsHeldAtEveryRound()
        {
            var presentation = CaptureScenarioRunner.DarknessPresentation(0.9f);

            Assert.IsTrue(presentation.HasDarkness);
            Assert.AreEqual(0.9f, presentation.DarknessAlphaFor(1), 1e-6f);
            Assert.AreEqual(0.9f, presentation.DarknessAlphaFor(7), 1e-6f);
        }
    }
}
