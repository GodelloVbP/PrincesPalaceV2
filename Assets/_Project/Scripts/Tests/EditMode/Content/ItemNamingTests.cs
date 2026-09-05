using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    public class ItemNamingTests
    {
        // The three-part weapon shape and the two-part armour shape, which is
        // the whole reason this is shared rather than duplicated per resolver.
        [Test]
        public void AWeaponName_IsModifierThenAdjectiveThenNoun()
        {
            Assert.AreEqual("Nimble Keen Sword", ItemNaming.Compose("Nimble", "Keen", "Sword"));
        }

        [Test]
        public void AnArmourName_IsAdjectiveThenPiece()
        {
            Assert.AreEqual("Hardened Leather Coif", ItemNaming.Compose("Hardened", "Leather Coif"));
        }

        // Any part may be missing. A double space or a leading space in an
        // item name is the kind of thing that ships, because nothing throws.
        [TestCase(null, "Keen", "Sword", "Keen Sword")]
        [TestCase("Nimble", null, "Sword", "Nimble Sword")]
        [TestCase("", "", "Sword", "Sword")]
        [TestCase("  Nimble  ", "  Keen  ", "  Sword  ", "Nimble Keen Sword")]
        public void MissingOrPaddedParts_StillProduceACleanName(string prefix, string adjective, string noun, string expected)
        {
            Assert.AreEqual(expected, ItemNaming.Compose(prefix, adjective, noun));
        }

        // Plus is opt-in and absent by default, because the generators bake
        // the definition's name and a definition has no plus.
        [Test]
        public void PlusIsAbsentUnlessAskedFor()
        {
            Assert.AreEqual("Hardened Leather Coif", ItemNaming.Compose("Hardened", "Leather Coif"));
            Assert.AreEqual("Hardened Leather Coif +3", ItemNaming.Compose("Hardened", "Leather Coif", 3));
        }

        [TestCase(0, "Keen Sword")]
        [TestCase(-2, "Keen Sword")]
        [TestCase(1, "Keen Sword +1")]
        [TestCase(10, "Keen Sword +10")]
        public void WithPlus_AppendsOnlyAPositiveOne(int plus, string expected)
        {
            Assert.AreEqual(expected, ItemNaming.WithPlus("Keen Sword", plus));
        }

        // ---- the tier adjective ladder ----------------------------------

        [TestCase(0, "Ragged")]
        [TestCase(2, "Supple")]
        [TestCase(4, "Hardened")]
        public void AdjectiveAt_ReadsTheLadderLowestFirst(int tier, string expected)
        {
            var ladder = new[] { "Ragged", "Worn", "Supple", "Cured", "Hardened" };
            Assert.AreEqual(expected, ItemNaming.AdjectiveAt(ladder, tier, "Plain"));
        }

        // A short ladder runs out gracefully rather than throwing, so raising
        // maxTier later cannot break naming before the words catch up.
        [Test]
        public void AShortLadder_RepeatsItsLastEntryRatherThanFailing()
        {
            var ladder = new[] { "Worn", "Fine" };

            Assert.AreEqual("Fine", ItemNaming.AdjectiveAt(ladder, 9, "Plain"));
            Assert.AreEqual("Fine", ItemNaming.AdjectiveAt(ladder, 50, "Plain"));
        }

        [Test]
        public void ANegativeTier_ReadsTheBottomOfTheLadder()
        {
            Assert.AreEqual("Worn", ItemNaming.AdjectiveAt(new[] { "Worn", "Fine" }, -3, "Plain"));
        }

        [Test]
        public void AnAbsentOrBlankEntry_FallsBack()
        {
            Assert.AreEqual("Plain", ItemNaming.AdjectiveAt(null, 3, "Plain"));
            Assert.AreEqual("Plain", ItemNaming.AdjectiveAt(new string[0], 3, "Plain"));
            Assert.AreEqual("Plain", ItemNaming.AdjectiveAt(new[] { "Worn", "   " }, 1, "Plain"));
        }

        // Armour's fallback is deliberately empty, so a set with no ladder
        // reads as "Leather Coif" rather than as "Plain Leather Coif".
        [Test]
        public void AnEmptyFallback_LeavesTheNameUnadorned()
        {
            string adjective = ItemNaming.AdjectiveAt(null, 3, "");
            Assert.AreEqual("Leather Coif", ItemNaming.Compose(adjective, "Leather Coif"));
        }
    }
}
