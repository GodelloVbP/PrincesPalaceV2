using System;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // The two rarity ladders and the colours they are drawn in: a gear copy's
    // Quality and a relic's RelicRarity. Hexes are PINNED literals (CLAUDE.md
    // gotcha 5).
    public class RarityPaletteTests
    {
        [TestCase(Quality.Common, "#9A93A8")]
        [TestCase(Quality.Uncommon, "#6FBF73")]
        [TestCase(Quality.Rare, "#5AA6E8")]
        [TestCase(Quality.Legendary, "#F0913D")]
        public void EveryQuality_HasItsAuthoredColour(Quality quality, string expected)
        {
            Assert.AreEqual(expected, ItemQuality.HexColor(quality));
        }

        [TestCase(RelicRarity.Common, "#9A93A8")]
        [TestCase(RelicRarity.Uncommon, "#6FBF73")]
        [TestCase(RelicRarity.Rare, "#5AA6E8")]
        [TestCase(RelicRarity.UltraRare, "#B48CFF")]
        [TestCase(RelicRarity.Mythic, "#F0913D")]
        [TestCase(RelicRarity.Godlike, "#FFD76B")]
        public void EveryRelicRarity_HasItsAuthoredColour(RelicRarity rarity, string expected)
        {
            Assert.AreEqual(expected, RelicRarityNames.HexColor(rarity));
        }

        // Bands sharing a colour would make the colour decorative rather than
        // informative, which is the entire reason it exists.
        [Test]
        public void NoTwoBands_OfOneLadder_ShareAColour()
        {
            CollectionAssert.AllItemsAreUnique(
                Enum.GetValues(typeof(Quality)).Cast<Quality>().Select(ItemQuality.HexColor).ToList());
            CollectionAssert.AllItemsAreUnique(
                Enum.GetValues(typeof(RelicRarity)).Cast<RelicRarity>().Select(RelicRarityNames.HexColor).ToList());
        }

        // Parsed by ColorUtility.TryParseHtmlString on the Unity side, which
        // silently fails on a malformed string.
        [Test]
        public void EveryColour_IsAWellFormedSixDigitHex()
        {
            var hexes = Enum.GetValues(typeof(Quality)).Cast<Quality>().Select(ItemQuality.HexColor)
                .Concat(Enum.GetValues(typeof(RelicRarity)).Cast<RelicRarity>().Select(RelicRarityNames.HexColor));

            foreach (string hex in hexes)
            {
                Assert.AreEqual(7, hex.Length, $"'{hex}' is not #RRGGBB");
                Assert.AreEqual('#', hex[0], $"'{hex}' does not start with #");
                Assert.IsTrue(hex.Skip(1).All(Uri.IsHexDigit), $"'{hex}' has a non-hex digit in it");
            }
        }
    }
}
