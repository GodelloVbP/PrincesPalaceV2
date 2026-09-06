using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // The achievement table, and the things about it that fail silently.
    //
    // Every check here exists because the failure it catches is INVISIBLE at
    // runtime. An achievement nobody can earn does not throw, does not log, and
    // does not look wrong -- whatever it gates simply never appears, and the
    // only symptom is a player wondering why. The same is true of a threshold
    // of zero, which hands out a free unlock to every new profile and looks
    // exactly like the achievement working.
    public class AchievementTableTests
    {
        private static RawAchievementEntry Raw(string id = "a", string name = "A",
            string condition = "ReachCharacterLevel", int threshold = 10, string parameter = "") =>
            new RawAchievementEntry
            {
                id = id, displayName = name, condition = condition,
                threshold = threshold, parameter = parameter,
            };

        private static ResolvedAchievement Resolve(RawAchievementEntry raw)
        {
            Assert.IsTrue(AchievementEntryResolver.TryResolveAll(new[] { raw }, out var ok, out var errors),
                "fixture did not resolve: " + string.Join(" | ", errors));
            return ok[0];
        }

        // ---- every condition is actually implemented -----------------------------

        [Test]
        public void EveryConditionExceptNeverCanBeSatisfiedBySomeFacts()
        {
            // THE test this class exists for. Adding a value to
            // AchievementCondition without a case in AchievementProgress
            // produces an achievement that can never be earned -- and the
            // default-false in that switch is what makes it silent. Walked from
            // the enum, so the failure lands here rather than on a player.
            var facts = new AchievementFacts(
                defeatedBossIds: new[] { "a", "b", "c", "d" },
                highestCharacterLevel: 999,
                roomsCleared: 9999,
                deepestStep: 9999,
                totalDamageDealt: 9_999_999);

            foreach (AchievementCondition condition in Enum.GetValues(typeof(AchievementCondition)))
            {
                if (condition == AchievementCondition.Never) continue;

                var achievement = new ResolvedAchievement(
                    "x", "X", "", condition,
                    threshold: 1,
                    parameter: "a",
                    sortOrder: 0);

                Assert.IsTrue(AchievementProgress.IsEarned(achievement, facts),
                    $"{condition} has no case in AchievementProgress, so nothing gated on it can ever unlock");
            }
        }

        [Test]
        public void NeverIsNeverEarnedHoweverGoodTheFactsAre()
        {
            // The safe default. An achievement authored without a condition is
            // inert rather than earned on the first frame.
            var facts = new AchievementFacts(
                defeatedBossIds: new[] { "a" }, highestCharacterLevel: 99,
                roomsCleared: 99, deepestStep: 99, totalDamageDealt: 99_999);

            var never = new ResolvedAchievement("x", "X", "", AchievementCondition.Never, 0, "", 0);

            Assert.IsFalse(AchievementProgress.IsEarned(never, facts));
        }

        [Test]
        public void NothingIsEarnedByABrandNewProfile()
        {
            // The single most important property of the whole table: a fresh
            // save has earned nothing. A threshold that defaults to zero, or a
            // count comparison written with >= against an empty set, breaks
            // this and hands out every gated relic on the first launch.
            var fresh = new AchievementFacts();

            foreach (AchievementCondition condition in Enum.GetValues(typeof(AchievementCondition)))
            {
                var achievement = new ResolvedAchievement("x", "X", "", condition, 1, "some_boss", 0);

                Assert.IsFalse(AchievementProgress.IsEarned(achievement, fresh),
                    $"{condition} is already earned by a profile that has done nothing");
            }
        }

        // ---- what the resolver refuses to ship -------------------------------------

        [Test]
        public void ACountingConditionWithoutAThresholdIsRejected()
        {
            // Threshold 0 means "earned by everyone, immediately", and it looks
            // identical to the achievement working.
            Assert.IsFalse(AchievementEntryResolver.TryResolveAll(
                new[] { Raw(condition: "ClearRooms", threshold: 0) }, out _, out var errors));

            StringAssert.Contains("threshold", errors[0]);
        }

        [Test]
        public void ABossConditionWithoutABossNamedIsRejected()
        {
            Assert.IsFalse(AchievementEntryResolver.TryResolveAll(
                new[] { Raw(condition: "DefeatSpecificBoss", parameter: "") }, out _, out var errors));

            StringAssert.Contains("parameter", errors[0]);
        }

        [Test]
        public void AnUnknownConditionIsRejectedRatherThanDefaultingToNever()
        {
            // Defaulting would produce an achievement that is authored, listed,
            // shown in a glossary, and permanently unearnable.
            Assert.IsFalse(AchievementEntryResolver.TryResolveAll(
                new[] { Raw(condition: "DoATrickshot") }, out _, out var errors));

            StringAssert.Contains("not a known AchievementCondition", errors[0]);
        }

        [Test]
        public void NeverCannotBeAuthoredDirectly()
        {
            // It is the safe internal default, not something content may pick:
            // an achievement that is deliberately unearnable is a mistake, and
            // an accidental one is invisible.
            Assert.IsFalse(AchievementEntryResolver.TryResolveAll(
                new[] { Raw(condition: "Never") }, out _, out _));
        }

        [Test]
        public void DuplicateIdsAreRejected()
        {
            // Ids reach save data. Two accomplishments sharing one would be
            // indistinguishable forever.
            Assert.IsFalse(AchievementEntryResolver.TryResolveAll(
                new[] { Raw(id: "same"), Raw(id: "same") }, out _, out var errors));

            Assert.IsTrue(errors.Any(e => e.Contains("Duplicate")));
        }

        [Test]
        public void AMissingIdOrNameIsRejected()
        {
            Assert.IsFalse(AchievementEntryResolver.TryResolveAll(new[] { Raw(id: "") }, out _, out _));
            Assert.IsFalse(AchievementEntryResolver.TryResolveAll(new[] { Raw(name: "") }, out _, out _));
        }

        // ---- the conditions themselves ------------------------------------------------

        [Test]
        public void DefeatSpecificBossMatchesOnlyItsOwnBoss()
        {
            var achievement = Resolve(Raw(condition: "DefeatSpecificBoss", parameter: "forest_warden"));

            Assert.IsTrue(AchievementProgress.IsEarned(achievement,
                new AchievementFacts(defeatedBossIds: new[] { "forest_warden" })));
            Assert.IsFalse(AchievementProgress.IsEarned(achievement,
                new AchievementFacts(defeatedBossIds: new[] { "swamp_thing" })));
        }

        [Test]
        public void DefeatDistinctBossesCountsDistinctOnesOnly()
        {
            // Killing the same boss on four separate runs is one boss.
            var achievement = Resolve(Raw(condition: "DefeatDistinctBosses", threshold: 3));

            Assert.IsFalse(AchievementProgress.IsEarned(achievement,
                new AchievementFacts(defeatedBossIds: new[] { "a", "a", "a", "a" })));
            Assert.IsTrue(AchievementProgress.IsEarned(achievement,
                new AchievementFacts(defeatedBossIds: new[] { "a", "b", "c" })));
        }

        [Test]
        public void AThresholdIsMetExactlyAtItAndNotOneBelow()
        {
            var achievement = Resolve(Raw(condition: "ReachCharacterLevel", threshold: 30));

            Assert.IsFalse(AchievementProgress.IsEarned(achievement,
                new AchievementFacts(highestCharacterLevel: 29)));
            Assert.IsTrue(AchievementProgress.IsEarned(achievement,
                new AchievementFacts(highestCharacterLevel: 30)));
        }

        [Test]
        public void ADamageThresholdSurvivesNumbersBiggerThanAnInt()
        {
            // A million damage is authored today; a long run over a long
            // profile will pass two billion, and an int total would wrap into
            // a negative and silently un-earn the achievement.
            var achievement = Resolve(Raw(condition: "DealTotalDamage", threshold: 1_000_000));

            Assert.IsTrue(AchievementProgress.IsEarned(achievement,
                new AchievementFacts(totalDamageDealt: 5_000_000_000L)));
        }

        [Test]
        public void NullFactsCollectionsDegradeRatherThanThrow()
        {
            var achievement = Resolve(Raw(condition: "DefeatSpecificBoss", parameter: "x"));

            Assert.DoesNotThrow(() => AchievementProgress.IsEarned(achievement, new AchievementFacts()));
        }

        // ---- the whole-catalogue check no resolver can make -----------------------
        //
        // AchievementEntryResolver only sees achievements.json, so the checks
        // above stop at "is parameter non-blank". Whether that id names a real
        // enemy, and whether that enemy is a boss, needs the enemy catalogue too
        // -- ContentDatabase.ValidateContent is the only reader that has both,
        // and it delegates the actual comparison here so it stays testable with
        // literals instead of through Resources-loaded content. The real-content
        // path (does achievements.json actually only name real bosses today) is
        // covered separately, in PlayMode: AchievementBossValidationTests.

        [Test]
        public void DefeatSpecificBossValidationPassesARealBoss()
        {
            var enemies = new Dictionary<string, bool> { ["forest_warden"] = true };

            Assert.IsNull(AchievementProgress.ValidateDefeatSpecificBossParameter(
                "first_forest_boss", "forest_warden", enemies));
        }

        [Test]
        public void DefeatSpecificBossValidationRefusesAMissingEnemyId()
        {
            var enemies = new Dictionary<string, bool> { ["forest_warden"] = true };

            string error = AchievementProgress.ValidateDefeatSpecificBossParameter(
                "typo_achievement", "forest_wardn", enemies);

            Assert.IsNotNull(error, "a boss id absent from the enemy catalogue must be refused");
            StringAssert.Contains("unknown enemy id", error);
        }

        [Test]
        public void DefeatSpecificBossValidationRefusesANonBossEnemy()
        {
            var enemies = new Dictionary<string, bool> { ["rat"] = false };

            string error = AchievementProgress.ValidateDefeatSpecificBossParameter(
                "rat_achievement", "rat", enemies);

            Assert.IsNotNull(error, "an enemy that exists but is not a boss must be refused too");
            StringAssert.Contains("not a boss", error);
        }
    }
}
