using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The tint literals are pinned rather than recomputed from the paintings:
    // a repaint of one backdrop has to show up as a deliberate edit of its row
    // here, with the new mean-luma arithmetic in TalentBackdrops' header.
    public class TalentBackdropsTests
    {
        [TestCase("shield", "Sentinel.png", "#494467")]
        [TestCase("axe", "Einherjar.png", "#232132")]
        [TestCase("paw", "Juggernaut.png", "#504B72")]
        public void EachPlotNamesItsPaintingAndItsFittedTint(string plot, string file, string tint)
        {
            var row = TalentBackdrops.For(plot);

            Assert.IsNotNull(row);
            Assert.AreEqual("Assets/_Project/Art/Backgrounds/" + file, row.SpriteKey);
            Assert.AreEqual(tint, row.TintHex);
        }

        [Test]
        public void APlotWithNoPaintingHasNoRow()
        {
            Assert.IsNull(TalentBackdrops.For("ram"));
            Assert.IsNull(TalentBackdrops.For(""));
        }
    }
}
