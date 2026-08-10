using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    public class SpellTierEntryResolverTests
    {
        private static RawSpellTierEntry Tier(int level, string name = "Spark", int manaCost = 10, float power = 1.5f)
        {
            return new RawSpellTierEntry { level = level, displayName = name, manaCost = manaCost, powerMultiplier = power };
        }

        [Test]
        public void ValidEntry_ResolvesSuccessfully()
        {
            bool ok = SpellTierEntryResolver.TryResolveAll(new List<RawSpellTierEntry> { Tier(1) }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(1, resolved.Count);
            Assert.AreEqual(1, resolved[0].Level);
            Assert.AreEqual("Spark", resolved[0].DisplayName);
            Assert.AreEqual(10, resolved[0].ManaCost);
            Assert.AreEqual(1.5f, resolved[0].PowerMultiplier);
        }

        [Test]
        public void Entries_AreSortedByLevel_RegardlessOfFileOrder()
        {
            var entries = new List<RawSpellTierEntry> { Tier(3), Tier(1), Tier(2) };

            SpellTierEntryResolver.TryResolveAll(entries, out var resolved, out _);

            Assert.AreEqual(1, resolved[0].Level);
            Assert.AreEqual(2, resolved[1].Level);
            Assert.AreEqual(3, resolved[2].Level);
        }

        [Test]
        public void LevelBelowOne_ProducesAClearError()
        {
            bool ok = SpellTierEntryResolver.TryResolveAll(new List<RawSpellTierEntry> { Tier(0) }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("level must be 1 or higher", errors[0]);
        }

        [Test]
        public void MissingDisplayName_ProducesAClearError()
        {
            var entry = Tier(1);
            entry.displayName = "";

            bool ok = SpellTierEntryResolver.TryResolveAll(new List<RawSpellTierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("displayName is required", errors[0]);
        }

        [Test]
        public void ZeroOrNegativeManaCost_ProducesAClearError()
        {
            var entry = Tier(1, manaCost: 0);

            bool ok = SpellTierEntryResolver.TryResolveAll(new List<RawSpellTierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("manaCost must be a positive number", errors[0]);
        }

        [Test]
        public void ZeroOrNegativePowerMultiplier_ProducesAClearError()
        {
            var entry = Tier(1, power: 0f);

            bool ok = SpellTierEntryResolver.TryResolveAll(new List<RawSpellTierEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("powerMultiplier must be a positive number", errors[0]);
        }

        [Test]
        public void DuplicateLevels_ProduceAClearError()
        {
            var entries = new List<RawSpellTierEntry> { Tier(1), Tier(1) };

            bool ok = SpellTierEntryResolver.TryResolveAll(entries, out var resolved, out var errors);

            Assert.IsFalse(ok);
            Assert.IsNull(resolved);
            StringAssert.Contains("Duplicate spell tier for level 1", errors[0]);
        }

        [Test]
        public void MultipleInvalidEntries_ReportEveryErrorTogether()
        {
            var entries = new List<RawSpellTierEntry> { Tier(0), Tier(2, manaCost: -1) };

            bool ok = SpellTierEntryResolver.TryResolveAll(entries, out _, out var errors);

            Assert.IsFalse(ok);
            Assert.AreEqual(2, errors.Count);
        }

        [Test]
        public void TierForLevel_ReturnsTheHighestTierNotExceedingTheGivenLevel()
        {
            var entries = new List<RawSpellTierEntry> { Tier(1, "Spark"), Tier(5, "Current"), Tier(9, "Ascendance") };
            SpellTierEntryResolver.TryResolveAll(entries, out var resolved, out _);

            Assert.AreEqual("Spark", SpellTierEntryResolver.TierForLevel(resolved, 1)?.DisplayName);
            Assert.AreEqual("Spark", SpellTierEntryResolver.TierForLevel(resolved, 4)?.DisplayName, "Level 4 has no tier of its own — should use the highest one it qualifies for (level 1)");
            Assert.AreEqual("Current", SpellTierEntryResolver.TierForLevel(resolved, 5)?.DisplayName);
            Assert.AreEqual("Current", SpellTierEntryResolver.TierForLevel(resolved, 8)?.DisplayName);
            Assert.AreEqual("Ascendance", SpellTierEntryResolver.TierForLevel(resolved, 9)?.DisplayName);
            Assert.AreEqual("Ascendance", SpellTierEntryResolver.TierForLevel(resolved, 99)?.DisplayName, "A level far above the highest tier should still fall back to that highest tier, not null");
        }

        [Test]
        public void TierForLevel_ReturnsNull_WhenNoTierQualifies()
        {
            var entries = new List<RawSpellTierEntry> { Tier(5) };
            SpellTierEntryResolver.TryResolveAll(entries, out var resolved, out _);

            Assert.IsNull(SpellTierEntryResolver.TierForLevel(resolved, 1));
        }
    }
}
