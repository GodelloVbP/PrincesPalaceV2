using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // A LAYER'S TINT (docs/PLAN_SPELL_REWORK.md decision 10): a #RRGGBB the
    // sheet is multiplied by. Mud Burst is its first user, a gold sheet drawn
    // as matte brown.
    public class SpellTintTests
    {
        [Test]
        public void AHexColourParsesToUnitChannels()
        {
            Assert.IsTrue(SpellTint.TryParse("#FF8000", out float r, out float g, out float b));
            Assert.AreEqual(1f, r, 1e-6f);
            Assert.AreEqual(128f / 255f, g, 1e-6f);
            Assert.AreEqual(0f, b, 1e-6f);
            Assert.IsTrue(SpellTint.TryParse("#6b4a30", out r, out g, out b), "lowercase is a colour too");
            Assert.AreEqual(107f / 255f, r, 1e-6f);
        }

        [Test]
        public void BlankIsNoTintAndLeavesTheChannelsAtOne()
        {
            Assert.IsFalse(SpellTint.TryParse("", out float r, out float g, out float b));
            Assert.AreEqual(1f, r);
            Assert.AreEqual(1f, g);
            Assert.AreEqual(1f, b);
            Assert.IsFalse(SpellTint.TryParse(null, out _, out _, out _));
        }

        [TestCase("6B4A30")]
        [TestCase("#6B4A3")]
        [TestCase("#6B4A3G")]
        [TestCase("brown")]
        public void ANonColourIsRefused(string text)
        {
            Assert.IsFalse(SpellTint.TryParse(text, out _, out _, out _));
        }

        private static SpellPresentation With(SpellLayer layer) =>
            new SpellPresentation { layerFormat = 1, layers = new[] { layer } };

        [Test]
        public void TheRulesRefuseABadTintAndATintOnAnEmitter()
        {
            var bad = new SpellLayer { id = "core", render = "sprite", place = "target", path = "Spells/x", seconds = 0.5f, tint = "brown" };
            var problems = SpellLayerRules.Check("skill 't'", With(bad));
            Assert.AreEqual(1, problems.Count, string.Join(" | ", problems));
            StringAssert.Contains("vfx.layers[0].tint \"brown\" is not a #RRGGBB colour", problems[0]);

            var good = new SpellLayer { id = "core", render = "sprite", place = "target", path = "Spells/x", seconds = 0.5f, tint = "#6B4A30" };
            Assert.IsEmpty(SpellLayerRules.Check("skill 't'", With(good)));

            var emitter = new SpellLayer
            {
                id = "drops", render = "emitter", place = "target", tint = "#6B4A30",
                emitter = new SpellEmitter { path = "Spells/x", burst = 4, lifeMin = 0.2f, lifeMax = 0.3f, speedMin = 10f, speedMax = 20f },
            };
            var refused = SpellLayerRules.Check("skill 't'", With(emitter));
            Assert.IsTrue(refused.Any(p => p.Contains("tint is not drawn on an emitter")), string.Join(" | ", refused));
        }
    }
}
