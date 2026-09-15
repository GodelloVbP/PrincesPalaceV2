using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // PHASE 3 package 3: the placeholder-skill mechanism (docs/handoffs/
    // progression_v2/PLAN_PROGRESSION_V2.md §7 phase 3). A separate file
    // from SkillEntryResolverTests rather than an addition to it, so a
    // concurrent edit to that file (phase 2 is landing in its own worktree
    // at the same time) has nothing here to conflict with.
    public class SkillEntryResolverPlaceholderTests
    {
        private static RawSkillEntry Placeholder(string id = "placeholder_shawn_capstone", string owner = "sheep",
            string note = "Owner has not designed this yet.") =>
            new RawSkillEntry
            {
                id = id,
                displayName = "Shawn's Fourth Ability",
                characterId = owner,
                placeholder = true,
                placeholderNote = note,
            };

        [Test]
        public void AMinimalPlaceholder_Resolves_NeverPlayerSelectable()
        {
            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { Placeholder() },
                out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.IsTrue(resolved[0].Placeholder);
            Assert.AreEqual("Owner has not designed this yet.", resolved[0].PlaceholderNote);
            Assert.IsFalse(resolved[0].PlayerSelectable, "a placeholder must never be player-selectable");
        }

        [Test]
        public void APlaceholderAuthoredPlayerSelectableTrue_IsForcedFalseAnyway()
        {
            var entry = Placeholder();
            entry.playerSelectable = true;

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.IsFalse(resolved[0].PlayerSelectable);
        }

        [Test]
        public void APlaceholderWithNoNote_IsRejected()
        {
            var entry = Placeholder(note: "");

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("placeholderNote", string.Join("; ", errors));
        }

        [Test]
        public void APlaceholderThatAuthorsManaCost_IsRejected()
        {
            var entry = Placeholder();
            entry.manaCost = 5;

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("any effect field", string.Join("; ", errors));
        }

        [Test]
        public void APlaceholderThatAuthorsAnEffect_IsRejected()
        {
            var entry = Placeholder();
            entry.effect = "HealSelf";

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("any effect field", string.Join("; ", errors));
        }

        [Test]
        public void APlaceholderThatAuthorsDamageInstances_IsRejected()
        {
            var entry = Placeholder();
            entry.damageInstances = new[] { new RawDamageInstance { type = "Fire", amount = 10 } };

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("any effect field", string.Join("; ", errors));
        }
    }
}
