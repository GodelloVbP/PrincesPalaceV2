using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // upgrades.json's rules, which did not exist before the file did.
    //
    // Upgrades were two CreateUpgrade(...) calls in ContentBuilder and nothing
    // checked anything about them; the only check in the whole project was a
    // negative-cost sweep in ContentDatabase.ValidateContent, which ran against
    // the already-generated asset. These are the same rules, earlier, plus the
    // ones the other seven resolvers have always had.
    public class UpgradeEntryResolverTests
    {
        private static RawUpgradeEntry Upgrade(string id = "test_upgrade", int cost = 50, int gold = 0) =>
            new RawUpgradeEntry
            {
                id = id,
                displayName = "Test Upgrade",
                description = "Does a thing.",
                cost = cost,
                startingGoldBonus = gold,
            };

        private static bool Resolve(out List<ResolvedUpgrade> resolved, out List<string> errors,
            params RawUpgradeEntry[] entries) =>
            UpgradeEntryResolver.TryResolveAll(new List<RawUpgradeEntry>(entries), out resolved, out errors);

        [Test]
        public void AValidEntryResolvesWithEveryFieldIntact()
        {
            Assert.IsTrue(Resolve(out var resolved, out var errors, Upgrade("gold_boost", cost: 30, gold: 50)),
                string.Join("; ", errors ?? new List<string>()));

            Assert.AreEqual("gold_boost", resolved[0].Id);
            Assert.AreEqual("Test Upgrade", resolved[0].DisplayName);
            Assert.AreEqual("Does a thing.", resolved[0].Description);
            Assert.AreEqual(30, resolved[0].Cost);
            Assert.AreEqual(50, resolved[0].StartingGoldBonus);
        }

        // FILE ORDER IS SHOP ORDER. Resources.LoadAll returns filename order,
        // so an upgrade whose id sorts early would jump the queue if the
        // authored position were not stamped on the record here.
        [Test]
        public void SortOrderComesFromPositionInTheFile()
        {
            Assert.IsTrue(Resolve(out var resolved, out _, Upgrade("zeta"), Upgrade("alpha")));

            Assert.AreEqual(0, resolved[0].SortOrder);
            Assert.AreEqual(1, resolved[1].SortOrder);
            Assert.AreEqual("zeta", resolved[0].Id, "authored order, not alphabetical");
        }

        [Test]
        public void AnIdIsRequired()
        {
            var blank = Upgrade();
            blank.id = "   ";

            Assert.IsFalse(Resolve(out _, out var errors, blank));
            StringAssert.Contains("id is required", string.Join(" ", errors));

            // And the message names the ROW when there is no id to name.
            StringAssert.Contains("entry #1", string.Join(" ", errors));
        }

        [Test]
        public void ADisplayNameIsRequiredBecauseTheShopWouldShowABlankRow()
        {
            var nameless = Upgrade("nameless");
            nameless.displayName = "";

            Assert.IsFalse(Resolve(out _, out var errors, nameless));
            StringAssert.Contains("displayName", string.Join(" ", errors));
        }

        // The check that used to live in ContentDatabase.ValidateContent, moved
        // to where the bad row never becomes an asset at all.
        [Test]
        public void ANegativeCostIsRefused()
        {
            Assert.IsFalse(Resolve(out _, out var errors, Upgrade("free_money", cost: -1)));
            StringAssert.Contains("cost", string.Join(" ", errors));
        }

        // Zero is not the same mistake: an upgrade can legitimately be free.
        [Test]
        public void AFreeUpgradeIsLegal()
        {
            Assert.IsTrue(Resolve(out var resolved, out var errors, Upgrade("freebie", cost: 0)),
                string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(0, resolved[0].Cost);
        }

        [Test]
        public void ANegativeStartingGoldBonusIsRefused()
        {
            Assert.IsFalse(Resolve(out _, out var errors, Upgrade("tax", gold: -25)));
            StringAssert.Contains("startingGoldBonus", string.Join(" ", errors));
        }

        // These strings end up in save data as purchases. Two rows sharing an
        // id would be indistinguishable forever, and buying one would grant the
        // other.
        [Test]
        public void ADuplicateIdIsRefusedByName()
        {
            Assert.IsFalse(Resolve(out _, out var errors, Upgrade("twin"), Upgrade("twin")));
            StringAssert.Contains("twin", string.Join(" ", errors));
            StringAssert.Contains("Duplicate", string.Join(" ", errors));
        }

        // NOTHING IS WRITTEN when any entry fails, which is why the resolver
        // hands back null rather than the rows that happened to be fine: a
        // partial catalogue looks like content that merely lost a row, and that
        // is the quietest failure available.
        [Test]
        public void OneBadRowTakesTheWholeFileWithIt()
        {
            Assert.IsFalse(Resolve(out var resolved, out _, Upgrade("good"), Upgrade("bad", cost: -5)));
            Assert.IsNull(resolved);
        }

        [Test]
        public void AnEmptyFileIsNotAnError()
        {
            Assert.IsTrue(UpgradeEntryResolver.TryResolveAll(null, out var resolved, out var errors));
            Assert.IsEmpty(resolved);
            Assert.IsEmpty(errors);
        }
    }
}
