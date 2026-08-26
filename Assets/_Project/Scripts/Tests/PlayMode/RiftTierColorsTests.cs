using NUnit.Framework;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // The Unity-facing face of RiftTierBands -- the RiftTier twin of
    // RarityColorsTests, and the regression guard for the item-modifier
    // plan's own Phase E requirement: an item that rolled nothing (RiftTier
    // .Ordinary, the vast majority of items) must draw EXACTLY as it did
    // before this system existed.
    public class RiftTierColorsTests
    {
        private static Color Expected(string hex)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out var parsed), $"{hex} is not parseable");
            return parsed;
        }

        [Test]
        public void EveryTierParsesToTheColourDomainPublishes()
        {
            foreach (RiftTier tier in System.Enum.GetValues(typeof(RiftTier)))
            {
                Assert.AreEqual(Expected(RiftTierBands.HexColor(tier)), RiftTierColors.For(tier),
                    $"{tier} disagrees with its Domain hex");
            }
        }

        [Test]
        public void NoTwoGlowingTiersShareAColour()
        {
            // Ordinary is excluded on purpose -- it never draws (see
            // ShouldGlow below), so its hex existing at all is only ever a
            // "no gap in the table" placeholder, never a colour a player
            // sees. The three tiers that DO glow must stay distinguishable
            // from one another.
            var seen = new System.Collections.Generic.HashSet<Color>();
            foreach (RiftTier tier in new[] { RiftTier.RiftTouched, RiftTier.RiftForged, RiftTier.Convergent })
            {
                Assert.IsTrue(seen.Add(RiftTierColors.For(tier)), $"{tier} duplicates another glowing tier");
            }
        }

        [Test]
        public void OnlyOrdinaryNeverGlows()
        {
            // THE regression guard: every item that has never touched the
            // modifier system rolls RiftTier.Ordinary (default int 0 on an
            // old save), and every card-painting call site gates its glow
            // node on this exact call -- see CharacterDossierController.
            // RefreshSlots/BindPackWindow and ReckoningController.PaintOffers.
            Assert.IsFalse(RiftTierColors.ShouldGlow(RiftTier.Ordinary));
            Assert.IsTrue(RiftTierColors.ShouldGlow(RiftTier.RiftTouched));
            Assert.IsTrue(RiftTierColors.ShouldGlow(RiftTier.RiftForged));
            Assert.IsTrue(RiftTierColors.ShouldGlow(RiftTier.Convergent));
        }

        [Test]
        public void AnOutOfRangeRiftTierFallsBackRatherThanThrowing()
        {
            // Same defensive posture as ModifierMagnitude.RiftMultiplier for
            // a corrupted/hand-edited save.
            Assert.AreEqual(Color.white, RiftTierColors.For((RiftTier)99));
        }

        [Test]
        public void DisplayNamesReadAsWordsRatherThanEnumIdentifiers()
        {
            Assert.AreEqual("Ordinary", RiftTierColors.DisplayName(RiftTier.Ordinary));
            Assert.AreEqual("Rift-Touched", RiftTierColors.DisplayName(RiftTier.RiftTouched));
            Assert.AreEqual("Rift-Forged", RiftTierColors.DisplayName(RiftTier.RiftForged));
            Assert.AreEqual("Convergent", RiftTierColors.DisplayName(RiftTier.Convergent));
        }
    }
}
