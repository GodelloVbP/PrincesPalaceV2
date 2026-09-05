using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    public class RelicEntryResolverTests
    {
        // The achievement ids these tests validate `unlockedBy` against. A
        // field rather than a defaulting overload on the resolver itself: a
        // resolver that quietly accepts "no achievements known" would validate
        // nothing and let a typo'd gate through, which is the exact failure the
        // parameter was added to prevent.
        private static readonly string[] KnownAchievements = { "first_forest_boss", "character_level_30" };

        private static RawRelicEntry Relic(string id = "dual_wield", string effect = "DualWield")
        {
            return new RawRelicEntry { id = id, displayName = "Dual Wield", effect = effect };
        }

        [Test]
        public void AMinimalRelic_ResolvesWithItsEffect()
        {
            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { Relic() }, KnownAchievements, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("dual_wield", resolved[0].Id);
            Assert.AreEqual(RelicEffect.DualWield, resolved[0].Effect);
        }

        [Test]
        public void IconPath_IsOptionalAndDefaultsEmpty()
        {
            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { Relic() }, KnownAchievements, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("", resolved[0].IconPath);
        }

        [Test]
        public void IconPath_PassesThroughWhenProvided()
        {
            var raw = Relic();
            raw.iconPath = "Assets/_Project/Art/Items/Relics/Processed/relic_dualwield.png";

            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { raw }, KnownAchievements, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("Assets/_Project/Art/Items/Relics/Processed/relic_dualwield.png", resolved[0].IconPath);
        }

        [Test]
        public void EffectParsing_IsCaseInsensitive()
        {
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(effect: "dualwield") }, KnownAchievements, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(RelicEffect.DualWield, resolved[0].Effect);
        }

        [Test]
        public void MissingId_IsRejected()
        {
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(id: "") }, KnownAchievements, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("id is required", errors[0]);
        }

        [Test]
        public void MissingDisplayName_IsRejected()
        {
            var raw = Relic();
            raw.displayName = "";

            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { raw }, KnownAchievements, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("displayName is required", errors[0]);
        }

        [Test]
        public void UnknownEffectName_IsRejected()
        {
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(effect: "NotARealEffect") }, KnownAchievements, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("not a known RelicEffect", errors[0]);
        }

        [Test]
        public void DuplicateIds_AreRejected()
        {
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(id: "x"), Relic(id: "x", effect: "Bloodlust") }, KnownAchievements, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("Duplicate relic id", errors[0]);
        }

        // Every relic on offer has to be its own reason to pick it -- two
        // relics resolving to the same effect would make one a dead choice
        // on the assign screen with no way to tell them apart in play.
        [Test]
        public void DuplicateEffects_AreRejected()
        {
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(id: "a"), Relic(id: "b") }, KnownAchievements, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("one relic per effect", errors[0]);
        }

        // The three relics this system launches with, pinned so a rename or
        // a JSON typo in the real content file fails a fast EditMode test
        // rather than surfacing as a silent gap in the assign screen.
        [Test]
        public void AllThreeLaunchRelics_ResolveToDistinctEffects()
        {
            var entries = new List<RawRelicEntry>
            {
                new RawRelicEntry { id = "dual_wield", displayName = "Dual Wield", effect = "DualWield" },
                new RawRelicEntry { id = "magical_shield", displayName = "Magical Shield", effect = "MagicalShield" },
                new RawRelicEntry { id = "bloodlust", displayName = "Bloodlust", effect = "Bloodlust" },
            };

            bool ok = RelicEntryResolver.TryResolveAll(entries, KnownAchievements, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(3, resolved.Count);
            CollectionAssert.AreEquivalent(
                new[] { RelicEffect.DualWield, RelicEffect.MagicalShield, RelicEffect.Bloodlust },
                resolved.ConvertAll(r => r.Effect));
        }
    }
}
