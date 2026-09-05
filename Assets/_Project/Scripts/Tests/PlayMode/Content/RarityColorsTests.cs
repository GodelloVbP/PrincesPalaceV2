using NUnit.Framework;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // The Unity-facing face of RarityBands.
    //
    // PlayMode only because Color and ColorUtility are engine types -- there is
    // no scene here and nothing loads. The bands themselves are pinned in
    // Domain by RarityBandsTests; what these check is that the parse into a
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
        public void EveryBandParsesToTheColourDomainPublishes()
        {
            // Walked from the enum rather than listed, so a seventh band cannot
            // be added without this failing.
            foreach (Rarity rarity in System.Enum.GetValues(typeof(Rarity)))
            {
                Assert.AreEqual(Expected(RarityBands.HexColor(rarity)), RarityColors.For(rarity),
                    $"{rarity} disagrees with its Domain hex");
            }
        }

        [Test]
        public void NoTwoBandsShareAColour()
        {
            // Rarity is read at a glance from the cell edge; two bands the same
            // colour is two ranks the player cannot tell apart.
            var seen = new System.Collections.Generic.HashSet<Color>();

            foreach (Rarity rarity in System.Enum.GetValues(typeof(Rarity)))
            {
                Assert.IsTrue(seen.Add(RarityColors.For(rarity)), $"{rarity} duplicates another band");
            }
        }

        [Test]
        public void ANullItemIsNeutralRatherThanBlack()
        {
            // Reachable: a save naming an id that content no longer has. Black
            // would read as a real rarity; white reads as "no information".
            Assert.AreEqual(Color.white, RarityColors.For((Content.ItemDefinition)null));
            Assert.AreEqual("", RarityColors.NameOf(null));
            Assert.AreEqual("", RarityColors.Wrap(null));
        }

        [Test]
        public void AnOutOfRangeRarityFallsBackRatherThanThrowing()
        {
            // Casting an int past the enum is what a corrupted or
            // forward-versioned save produces.
            Assert.AreEqual(Color.white, RarityColors.For((Rarity)99));
        }

        [Test]
        public void WrapProducesOneRichTextTagAroundTheName()
        {
            // Used where the name sits inside a longer line, so tinting the
            // whole label would colour the sentence too.
            var item = ScriptableObject.CreateInstance<Content.ItemDefinition>();
            item.displayName = "Cuirass";
            item.tier = 0;

            string wrapped = RarityColors.Wrap(item);

            StringAssert.StartsWith("<color=" + RarityBands.HexColorForTier(0) + ">", wrapped);
            StringAssert.EndsWith("</color>", wrapped);
            StringAssert.Contains("Cuirass", wrapped);

            Object.DestroyImmediate(item);
        }

        [Test]
        public void APlusRidesTheNameButNotTheColour()
        {
            // The plus is an instance fact; the colour is the item's tier. They
            // are separate axes and the name is where they meet.
            var item = ScriptableObject.CreateInstance<Content.ItemDefinition>();
            item.displayName = "Cuirass";
            item.tier = 4;

            Assert.AreEqual("Cuirass", RarityColors.NameOf(item, 0), "a plain copy carries no suffix");
            StringAssert.Contains("+5", RarityColors.NameOf(item, 5));
            Assert.AreEqual(RarityColors.For(item), RarityColors.For(Rarity.Rare),
                "tier 4 is Rare whatever the plus");

            Object.DestroyImmediate(item);
        }
    }
}
