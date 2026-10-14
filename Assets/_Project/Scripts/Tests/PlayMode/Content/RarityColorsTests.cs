using NUnit.Framework;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // The Unity-facing face of the two rarity ladders (a copy's Quality, a
    // relic's RelicRarity).
    //
    // PlayMode only because Color and ColorUtility are engine types -- there is
    // no scene here and nothing loads. The hexes themselves are pinned in
    // Domain by RarityPaletteTests; what these check is that the parse into a
    // real Color agrees with the hex Domain publishes, and that the degenerate
    // inputs a content gap actually produces come back neutral instead of
    // black.
    public class RarityColorsTests
    {
        private static Color Expected(string hex)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out var parsed), $"{hex} is not parseable");
            return parsed;
        }

        [Test]
        public void EveryQualityParsesToTheColourDomainPublishes()
        {
            foreach (Quality quality in System.Enum.GetValues(typeof(Quality)))
            {
                Assert.AreEqual(Expected(ItemQuality.HexColor(quality)), RarityColors.For(quality),
                    $"{quality} disagrees with its Domain hex");
            }
        }

        [Test]
        public void EveryRelicRarityParsesToTheColourDomainPublishes()
        {
            foreach (RelicRarity rarity in System.Enum.GetValues(typeof(RelicRarity)))
            {
                Assert.AreEqual(Expected(RelicRarityNames.HexColor(rarity)), RarityColors.For(rarity),
                    $"{rarity} disagrees with its Domain hex");
            }
        }

        [Test]
        public void ANullItemIsNeutralRatherThanBlack()
        {
            // Reachable: a save naming an id that content no longer has. Black
            // would read as a real rarity; white reads as "no information".
            Assert.AreEqual(Color.white, RarityColors.For((ItemInstance)null));
            Assert.AreEqual("", RarityColors.NameOf(null));
            Assert.AreEqual("", RarityColors.Wrap(null));
        }

        [Test]
        public void AnOutOfRangeRarityFallsBackRatherThanThrowing()
        {
            // Casting an int past the enum is what a corrupted or
            // forward-versioned save produces.
            Assert.AreEqual(Color.white, RarityColors.For((Quality)99));
            Assert.AreEqual(Color.white, RarityColors.For((RelicRarity)99));
        }

        [Test]
        public void WrapProducesOneRichTextTagAroundTheName()
        {
            // Used where the name sits inside a longer line, so tinting the
            // whole label would colour the sentence too.
            var copy = ItemInstance.Of("brigandine_boots", 0);

            string wrapped = RarityColors.Wrap(copy);

            StringAssert.StartsWith("<color=" + ItemQuality.HexColor(Quality.Common) + ">", wrapped);
            StringAssert.EndsWith("</color>", wrapped);
            StringAssert.Contains("Patched Brigandine Boots", wrapped);
        }

        [Test]
        public void AnAffixedCopyIsDrawnInItsQualityColour()
        {
            // Quality is derived from the roll, not from the tier.
            var plain = ItemInstance.Of("brigandine_boots", 4);
            var uncommon = ItemInstance.Of("brigandine_boots", 4, new[] { "attuned" });

            Assert.AreEqual(RarityColors.For(Quality.Common), RarityColors.For(plain));
            Assert.AreEqual(RarityColors.For(Quality.Uncommon), RarityColors.For(uncommon));
        }

        [Test]
        public void AHoningRidesTheNameButNotTheColour()
        {
            // The honing is the slot's fact; the colour is the copy's quality.
            // They are separate axes and the name is where they meet.
            var copy = ItemInstance.Of("brigandine_boots", 4);

            Assert.AreEqual("Tempered Brigandine Boots", RarityColors.NameOf(copy, 0), "a plain copy carries no suffix");
            StringAssert.Contains("+5", RarityColors.NameOf(copy, 5));
            Assert.AreEqual(RarityColors.For(Quality.Common), RarityColors.For(copy));
        }
    }
}
