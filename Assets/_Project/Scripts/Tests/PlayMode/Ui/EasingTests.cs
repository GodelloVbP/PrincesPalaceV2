using NUnit.Framework;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // Pins the one thing the four copies Easing.cs replaced did not agree on:
    // whether the curve clamps outside [0,1]. Both must here, so a caller
    // that forgets to pre-normalise gets a curve that finishes rather than
    // one that reverses past t=1.
    //
    // Interior values are literal, not recomputed from the formula -- a test
    // that calls t*t*(3-2t) to check t*t*(3-2t) proves nothing (CLAUDE.md
    // gotcha 5).
    public class EasingTests
    {
        [Test]
        public void SmoothStep_ClampsBelowZero()
        {
            Assert.AreEqual(0f, Easing.SmoothStep(-0.5f));
        }

        [Test]
        public void SmoothStep_ClampsAboveOne()
        {
            Assert.AreEqual(1f, Easing.SmoothStep(1.5f));
        }

        [Test]
        public void SmoothStep_InteriorValue()
        {
            Assert.AreEqual(0.5f, Easing.SmoothStep(0.5f), 0.0001f);
        }

        [Test]
        public void OutCubic_ClampsBelowZero()
        {
            Assert.AreEqual(0f, Easing.OutCubic(-0.5f));
        }

        [Test]
        public void OutCubic_ClampsAboveOne()
        {
            Assert.AreEqual(1f, Easing.OutCubic(2f));
        }

        [Test]
        public void OutCubic_InteriorValue()
        {
            Assert.AreEqual(0.875f, Easing.OutCubic(0.5f), 0.0001f);
        }
    }
}
