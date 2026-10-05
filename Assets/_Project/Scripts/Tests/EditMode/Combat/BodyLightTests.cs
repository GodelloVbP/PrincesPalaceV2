using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // WHERE LIGHT SITS ON A BODY (BodyLight): the burn glow gathers on the outline
    // and the feet with a faint tint inside, and every hit flashes the
    // outline. Literals; the shader files must state the same constants.
    public class BodyLightTests
    {
        [TestCase(1f, 0f)]
        [TestCase(0.75f, 0.5f)]
        [TestCase(0.5f, 1f)]
        [TestCase(0f, 1f)]
        public void TheEdgeIsHowMuchOfTheNeighbourhoodIsClear(float meanAlpha, float edge)
        {
            Assert.AreEqual(edge, BodyLight.EdgeOf(meanAlpha), 1e-6f);
        }

        [Test]
        public void TheGlowIsFaintInsideAndStrongOnTheOutlineAndAtTheFeet()
        {
            // Interior at the feet: 0.16 * 1; at the crown: 0.16 * 0.35.
            Assert.AreEqual(0.16f, BodyLight.BurnWeight(1f, 0f, 0f), 1e-6f);
            Assert.AreEqual(0.056f, BodyLight.BurnWeight(1f, 0f, 0.4f), 1e-6f);
            Assert.AreEqual(0.056f, BodyLight.BurnWeight(1f, 0f, 1f), 1e-6f);
            // Halfway up the feet's reach the smoothstep is 0.5: 0.16 * (0.35 + 0.65 * 0.5).
            Assert.AreEqual(0.108f, BodyLight.BurnWeight(1f, 0f, 0.2f), 1e-6f);
            // The outline: full at the ground, half (plus the interior) at the crown.
            Assert.AreEqual(1f, BodyLight.BurnWeight(1f, 1f, 0f), 1e-6f);
            Assert.AreEqual(0.556f, BodyLight.BurnWeight(1f, 1f, 1f), 1e-6f);
            // Nothing where the sprite is clear.
            Assert.AreEqual(0f, BodyLight.BurnWeight(0f, 1f, 0f), 0f);
        }

        [Test]
        public void TheRimFlashLightsTheOutlineFullyAndTheInsideLightly()
        {
            Assert.AreEqual(0.22f, BodyLight.FlashWeight(1f, 0f), 1e-6f);
            Assert.AreEqual(1f, BodyLight.FlashWeight(1f, 1f), 1e-6f);
            Assert.AreEqual(0.61f, BodyLight.FlashWeight(1f, 0.5f), 1e-6f);
            Assert.AreEqual(0.5f, BodyLight.FlashWeight(0.5f, 1f), 1e-6f);
            Assert.AreEqual(2, BodyLight.FlashHoldFrames);
            Assert.AreEqual(0.08f, BodyLight.FlashFadeSeconds, 1e-6f);
        }

        // The shaders restate the constants as property defaults.
        [TestCase("UIBurnGlow", "_Interior", 0.16f)]
        [TestCase("UIBurnGlow", "_RimWidth", 0.02f)]
        [TestCase("UIBurnGlow", "_FeetReach", 0.4f)]
        [TestCase("UIRimFlash", "_Interior", 0.22f)]
        [TestCase("UIRimFlash", "_RimWidth", 0.022f)]
        public void TheShaderDefaultsMatchTheCsharpConstants(string shader, string property, float expected)
        {
            string text = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project", "Resources",
                "Shaders", shader + ".shader"));
            var match = Regex.Match(text, property + @"\s*\(""[^""]*"",\s*Range\([^)]*\)\)\s*=\s*([0-9.]+)");
            Assert.IsTrue(match.Success, shader + " has no " + property);
            Assert.AreEqual(expected, float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1e-6f);
        }

        [Test]
        public void TheConstantsTheShadersRestateAreTheOnesTheseTestsPin()
        {
            Assert.AreEqual(0.16f, BodyLight.BurnInteriorShare, 0f);
            Assert.AreEqual(0.02f, BodyLight.BurnRimWidth, 0f);
            Assert.AreEqual(0.4f, BodyLight.BurnFeetReach, 0f);
            Assert.AreEqual(0.22f, BodyLight.FlashInteriorShare, 0f);
            Assert.AreEqual(0.022f, BodyLight.FlashRimWidth, 0f);
        }
    }
}
