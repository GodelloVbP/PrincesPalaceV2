using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    public class RarityBandsTests
    {
        // PINNED LITERALS, every tier spelled out. This is a pure lookup with
        // no arithmetic worth trusting to a loop — recomputing the boundaries
        // here would make the test agree with itself rather than with the
        // design (CLAUDE.md gotcha 5, and AUDIT.md #18 on tautological
        // tests).
        [TestCase(0, Rarity.Common)]
        [TestCase(1, Rarity.Common)]
        [TestCase(2, Rarity.Uncommon)]
        [TestCase(3, Rarity.Uncommon)]
        [TestCase(4, Rarity.Rare)]
        [TestCase(5, Rarity.Rare)]
        [TestCase(6, Rarity.Epic)]
        [TestCase(7, Rarity.Epic)]
        [TestCase(8, Rarity.Legendary)]
        [TestCase(9, Rarity.Legendary)]
        [TestCase(10, Rarity.Mythic)]
        public void EveryTier_LandsInItsAuthoredBand(int tier, Rarity expected)
        {
            Assert.AreEqual(expected, RarityBands.For(tier));
        }

        // Tier arrives from save data and from reward rolls; neither is
        // trusted to stay in range.
        [Test]
        public void OutOfRangeTiers_ClampToTheEnds()
        {
            Assert.AreEqual(Rarity.Common, RarityBands.For(-4), "A negative tier is the bottom of the ladder, not a crash");
            Assert.AreEqual(Rarity.Mythic, RarityBands.For(99), "Past the top is still the top");
        }

        // Mythic being one tier wide is the whole point of the top of the
        // ladder: if tier 9 and tier 10 shared a word, the last rung — the
        // reason a player is still descending — would be invisible.
        [Test]
        public void MythicIsTheTopTierAlone()
        {
            Assert.AreNotEqual(RarityBands.For(9), RarityBands.For(10));
            Assert.AreEqual(Rarity.Legendary, RarityBands.For(9));
        }

        [Test]
        public void FloorTierOf_IsTheInverseOfFor()
        {
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
            {
                int floor = RarityBands.FloorTierOf(rarity);
                Assert.AreEqual(rarity, RarityBands.For(floor),
                    $"{rarity}'s floor tier of {floor} should land back in {rarity}");
            }
        }

        // The floor is the LOWEST tier in the band, not merely some tier in
        // it — the reward tables turn "a boss never drops below Uncommon"
        // straight into this number.
        [Test]
        public void FloorTierOf_IsTheLowestTierInTheBand()
        {
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
            {
                int floor = RarityBands.FloorTierOf(rarity);
                if (floor > 0)
                {
                    Assert.AreNotEqual(rarity, RarityBands.For(floor - 1),
                        $"tier {floor - 1} should already be a weaker band than {rarity}");
                }
            }
        }

        [TestCase(Rarity.Common, "#9A93A8")]
        [TestCase(Rarity.Uncommon, "#6FBF73")]
        [TestCase(Rarity.Rare, "#5AA6E8")]
        [TestCase(Rarity.Epic, "#B48CFF")]
        [TestCase(Rarity.Legendary, "#F0913D")]
        [TestCase(Rarity.Mythic, "#FFD76B")]
        public void EveryBand_HasItsAuthoredColour(Rarity rarity, string expected)
        {
            Assert.AreEqual(expected, RarityBands.HexColor(rarity));
        }

        // Six bands sharing a colour would make the colour decorative rather
        // than informative, which is the entire reason this exists.
        [Test]
        public void NoTwoBands_ShareAColour()
        {
            var colours = Enum.GetValues(typeof(Rarity))
                .Cast<Rarity>()
                .Select(RarityBands.HexColor)
                .ToList();

            CollectionAssert.AllItemsAreUnique(colours);
        }

        // Parsed by ColorUtility.TryParseHtmlString on the Unity side, which
        // silently fails on a malformed string and leaves the label black.
        [Test]
        public void EveryColour_IsAWellFormedSixDigitHex()
        {
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
            {
                string hex = RarityBands.HexColor(rarity);
                Assert.AreEqual(7, hex.Length, $"{rarity}: '{hex}' is not #RRGGBB");
                Assert.AreEqual('#', hex[0], $"{rarity}: '{hex}' does not start with #");
                Assert.IsTrue(hex.Skip(1).All(Uri.IsHexDigit), $"{rarity}: '{hex}' has a non-hex digit in it");
            }
        }

        [Test]
        public void HexColorForTier_AgreesWithTheBandItFallsIn()
        {
            for (int tier = 0; tier <= 10; tier++)
            {
                Assert.AreEqual(RarityBands.HexColor(RarityBands.For(tier)), RarityBands.HexColorForTier(tier));
            }
        }
    }
}
