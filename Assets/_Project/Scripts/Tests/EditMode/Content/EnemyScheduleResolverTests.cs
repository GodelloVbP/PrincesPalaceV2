using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // An enemy's `schedule` (docs/PLAN_BELLWETHER_KIT.md 3.7, M4): the shape
    // M5 authors resolves, and one named refusal per way an author can write
    // a schedule that would be read by nothing or read two ways.
    public class EnemyScheduleResolverTests
    {
        private static RawEnemyEntry Bell(params RawEnemyScheduleEntry[] schedule) =>
            new RawEnemyEntry
            {
                id = "bell", displayName = "Bell", maxHealth = 150,
                attackWeight = 0f,
                abilities = new[]
                {
                    new RawEnemyAbility { skillId = "scratch", weight = 1f },
                    new RawEnemyAbility { skillId = "chains", weight = 0f },
                    new RawEnemyAbility { skillId = "knell", weight = 0f },
                },
                schedule = schedule,
            };

        private static RawEnemyScheduleEntry Entry(int[] onTurns, params string[] skills) =>
            new RawEnemyScheduleEntry { onTurns = onTurns, skills = skills };

        private static ResolvedEnemy Resolves(RawEnemyEntry entry)
        {
            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            return resolved[0];
        }

        private static void Refuses(RawEnemyEntry entry, string fragment)
        {
            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out _, out var errors);
            Assert.IsFalse(ok, "the row resolved");
            StringAssert.Contains(fragment, errors[0]);
        }

        [Test]
        public void TheBellwethersPairOnTurnsOneAndFiveResolves()
        {
            var enemy = Resolves(Bell(Entry(new[] { 1, 5 }, "chains", "knell")));

            Assert.AreEqual(1, enemy.Schedule.Length);
            CollectionAssert.AreEqual(new[] { 1, 5 }, enemy.Schedule[0].OnTurns);
            CollectionAssert.AreEqual(new[] { "chains", "knell" }, enemy.Schedule[0].Skills);
        }

        [Test]
        public void NoScheduleResolvesToAnEmptyOne()
        {
            Assert.AreEqual(0, Resolves(Bell()).Schedule.Length);
        }

        [Test]
        public void TwoSequencesThatTouchButDoNotOverlapResolve()
        {
            // [1,2] and [3]: adjacent, not shared.
            var enemy = Resolves(Bell(Entry(new[] { 1 }, "chains", "knell"), Entry(new[] { 3 }, "scratch")));
            Assert.AreEqual(2, enemy.Schedule.Length);
        }

        [Test]
        public void AScheduleWithNoTurnsIsRefused() =>
            Refuses(Bell(Entry(new int[0], "chains")), "has no onTurns");

        [Test]
        public void AScheduleWithNoSkillsIsRefused() =>
            Refuses(Bell(Entry(new[] { 1 })), "has no skills");

        [Test]
        public void ABlankScheduledSkillIsRefused() =>
            Refuses(Bell(Entry(new[] { 1 }, "chains", " ")), "skills[1] is blank");

        // Also the unknown-id refusal: an id in `abilities` that no skill
        // matches is refused by CatalogueCrossChecks, so an id must be in
        // both lists or one of the two refuses it.
        [Test]
        public void AScheduledSkillNotInAbilitiesIsRefused() =>
            Refuses(Bell(Entry(new[] { 1 }, "chains", "toll")), "plays 'toll', which is not in this monster's abilities");

        [Test]
        public void ATurnBeforeTheFirstIsRefused() =>
            Refuses(Bell(Entry(new[] { 0 }, "chains")), "starts on acting turn 0");

        [Test]
        public void ATurnListedTwiceIsRefused() =>
            Refuses(Bell(Entry(new[] { 5, 5 }, "chains")), "lists acting turn 5 twice");

        [Test]
        public void TwoSequencesSharingATurnAreRefused() =>
            Refuses(Bell(Entry(new[] { 1 }, "chains", "knell"), Entry(new[] { 2 }, "scratch")),
                "overlaps schedule[0]'s turns 1-2");

        [Test]
        public void OneSequenceStartingInsideItselfIsRefused() =>
            Refuses(Bell(Entry(new[] { 1, 2 }, "chains", "knell")), "overlaps schedule[0]'s turns 1-2");
    }
}
