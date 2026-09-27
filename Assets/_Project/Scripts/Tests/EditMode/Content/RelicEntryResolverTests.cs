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

        // The roster a `bearer` is validated against, for the same reason.
        private static readonly string[] KnownCharacters = { "sheep", "owl", "bear" };

        private static RawRelicEntry Relic(string id = "dual_wield", string effect = "DualWield")
        {
            return new RawRelicEntry { id = id, displayName = "Dual Wield", effect = effect };
        }

        [Test]
        public void AMinimalRelic_ResolvesWithItsEffect()
        {
            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { Relic() }, KnownAchievements, KnownCharacters, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("dual_wield", resolved[0].Id);
            Assert.AreEqual(RelicEffect.DualWield, resolved[0].Effect);
        }

        [Test]
        public void IconPath_IsOptionalAndDefaultsEmpty()
        {
            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { Relic() }, KnownAchievements, KnownCharacters, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("", resolved[0].IconPath);
        }

        [Test]
        public void IconPath_PassesThroughWhenProvided()
        {
            var raw = Relic();
            raw.iconPath = "Assets/_Project/Art/Items/Relics/Processed/relic_dualwield.png";

            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { raw }, KnownAchievements, KnownCharacters, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("Assets/_Project/Art/Items/Relics/Processed/relic_dualwield.png", resolved[0].IconPath);
        }

        [Test]
        public void EffectParsing_IsCaseInsensitive()
        {
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(effect: "dualwield") }, KnownAchievements, KnownCharacters, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(RelicEffect.DualWield, resolved[0].Effect);
        }

        [Test]
        public void MissingId_IsRejected()
        {
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(id: "") }, KnownAchievements, KnownCharacters, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("id is required", errors[0]);
        }

        [Test]
        public void MissingDisplayName_IsRejected()
        {
            var raw = Relic();
            raw.displayName = "";

            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { raw }, KnownAchievements, KnownCharacters, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("displayName is required", errors[0]);
        }

        [Test]
        public void UnknownEffectName_IsRejected()
        {
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(effect: "NotARealEffect") }, KnownAchievements, KnownCharacters, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("not a known RelicEffect", errors[0]);
        }

        [Test]
        public void DuplicateIds_AreRejected()
        {
            bool ok = RelicEntryResolver.TryResolveAll(
                new List<RawRelicEntry> { Relic(id: "x"), Relic(id: "x", effect: "Bloodlust") }, KnownAchievements, KnownCharacters, out _, out var errors);

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
                new List<RawRelicEntry> { Relic(id: "a"), Relic(id: "b") }, KnownAchievements, KnownCharacters, out _, out var errors);

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

            bool ok = RelicEntryResolver.TryResolveAll(entries, KnownAchievements, KnownCharacters, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(3, resolved.Count);
            CollectionAssert.AreEquivalent(
                new[] { RelicEffect.DualWield, RelicEffect.MagicalShield, RelicEffect.Bloodlust },
                resolved.ConvertAll(r => r.Effect));
        }

        // ---- bearer and draftable (Kinship, docs/PLAN_PETTING_ZOO.md) ------------

        [Test]
        public void BearerAndDraftable_DefaultToEveryoneAndOffered()
        {
            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { Relic() }, KnownAchievements, KnownCharacters, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("", resolved[0].Bearer);
            Assert.IsTrue(resolved[0].Draftable);
        }

        [Test]
        public void BearerAndDraftable_PassThroughWhenAuthored()
        {
            var raw = Relic("kinship", "Kinship");
            raw.bearer = "sheep";
            raw.draftable = false;

            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { raw }, KnownAchievements, KnownCharacters, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("sheep", resolved[0].Bearer);
            Assert.IsFalse(resolved[0].Draftable);
            Assert.IsTrue(resolved[0].ReachesCharacter("sheep"));
            Assert.IsFalse(resolved[0].ReachesCharacter("bear"));
        }

        [Test]
        public void AnUnknownBearer_IsRefusedByName()
        {
            var raw = Relic("kinship", "Kinship");
            raw.bearer = "shep";

            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { raw }, KnownAchievements, KnownCharacters, out _, out var errors);

            Assert.IsFalse(ok);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("bearer 'shep' is not a character id", errors[0]);
            StringAssert.Contains("sheep, owl, bear", errors[0]);
        }

        // The adapter flattens relic modifiers onto every party member, so a
        // bearer could not keep them to one character. Refused, not half-kept.
        [Test]
        public void ABearerOnARelicWithModifiers_IsRefused()
        {
            var raw = Relic("kinship", "Kinship");
            raw.bearer = "sheep";
            raw.modifiers = new[] { new RawRelicModifier { type = "AttackPercent", amount = 10 } };

            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { raw }, KnownAchievements, KnownCharacters, out _, out var errors);

            Assert.IsFalse(ok);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("bearer 'sheep'", errors[0]);
            StringAssert.Contains("every party member", errors[0]);
        }

        // ---- optional vfx (PLAN_EVENTS_BELL_AND_CARAVAN 3.5) ----------------

        [Test]
        public void Vfx_IsOptionalAndDefaultsToNothing()
        {
            RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { Relic() }, KnownAchievements, KnownCharacters,
                out var resolved, out _);

            Assert.AreEqual("", resolved[0].Vfx.path);
            Assert.AreEqual("", resolved[0].Vfx.sfxPath);
        }

        [Test]
        public void Vfx_PassesThroughWhenProvided()
        {
            var raw = Relic();
            raw.vfx = new SpellPresentation { path = "Vfx/ghost_flock", sfxPath = "Audio/Sfx/flock_charge" };

            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { raw }, KnownAchievements, KnownCharacters,
                out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("Vfx/ghost_flock", resolved[0].Vfx.path);
            Assert.AreEqual("Audio/Sfx/flock_charge", resolved[0].Vfx.sfxPath);
        }

        [Test]
        public void Refuses_AnAssetsRelativeVfxPath()
        {
            var raw = Relic();
            raw.vfx = new SpellPresentation { path = "Assets/_Project/Resources/Vfx/ghost_flock" };

            bool ok = RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { raw }, KnownAchievements, KnownCharacters,
                out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("RESOURCES-relative", string.Join(" | ", errors));
        }
    }
}
