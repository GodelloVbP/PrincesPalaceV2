using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Domain.Bot;

namespace PrincesPalace.PlayModeTests
{
    // BotRunDriver.PlayCareer -- PlayRun wipes the save and rebuilds the
    // profile from scratch on every call (docs/handoffs/progression_v2/
    // PHASE6_BOT_REPORT.md SS1), so it cannot play a career across runs.
    // PlayMode because a career plays through RunManager/SaveSystem/
    // RunOrchestrator, all Core, the same reason BalanceBotSmokeTests and
    // BotLevelUpCarriedHealthTests beside this file are PlayMode rather than
    // a [D] class: nothing under Core is noEngineReferences, so a genuine
    // zero-Unity-dependency test of this behaviour is not possible -- see
    // this phase's own report for the call made about that against the
    // brief's "[D] test" wording.
    public class BotCareerModeTests
    {
        [TearDown]
        public void LeaveNoStaticsBehind()
        {
            // PlayCareer restores all of these in its own finally, same as
            // PlayRun does -- belt and braces against a future edit that
            // throws before the try, same reasoning BalanceBotSmokeTests'
            // own teardown gives.
            BotRunDriver.InMemorySaves = true;
            SaveSystem.InMemory = false;
            SaveSystem.ClearMemory();
            Navigation.Reset();
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        // A CAREER OF TWO CARRIES LEVEL AND EXP FROM THE FIRST LIFE INTO THE
        // SECOND. GreedyAggressive on Fresh, depth cap 24, is virtually
        // certain to win at least its opening fight and gain SOME experience
        // before its first life ends (death or cap) -- Fresh starts every
        // fielded character at level 1 with 0 exp, so any level above 1 or
        // any exp above 0 at the end of run 0 is proof a fight was won, and
        // PlayCareer never rebuilds the profile for run index > 0 (see its
        // own header), so run 1 either carries that gain forward or this
        // test catches the regression: without the carry, run 1 would start
        // and end at the same Fresh baseline every single-life PlayRun call
        // already produces, and its own level/exp would read no higher than
        // run 0's despite two lives' worth of fighting.
        [Test]
        public void ATwoRunCareer_CarriesLevelAndExpFromTheFirstLifeIntoTheSecond()
        {
            var career = BotRunDriver.PlayCareer(
                seed: 1, archetype: "GreedyAggressive", profile: "Fresh",
                runsInCareer: 2, depthCapSteps: 24);

            Assert.AreEqual(2, career.Runs.Count, "both lives of the career should have played");

            var run0 = career.Runs[0];
            var run1 = career.Runs[1];

            Assert.IsEmpty(run0.Result.Hits.Where(h => h.Name != "StalledEnemyTurn"),
                "run 0 tripped an invariant: " + string.Join("; ", run0.Result.Hits.Select(h => h.ToString())));
            Assert.IsEmpty(run1.Result.Hits.Where(h => h.Name != "StalledEnemyTurn"),
                "run 1 tripped an invariant: " + string.Join("; ", run1.Result.Hits.Select(h => h.ToString())));

            bool run0GainedSomething = run0.LevelByCharacter.Values.Any(level => level > 1)
                || run0.ExpByCharacter.Values.Any(exp => exp > 0);
            Assert.IsTrue(run0GainedSomething,
                "run 0 (GreedyAggressive, Fresh, depth cap 24) ended at the Fresh baseline with no gain at " +
                "all -- the test's own premise that at least one fight was won did not hold for this seed");

            foreach (var characterId in run0.LevelByCharacter.Keys)
            {
                Assert.IsTrue(run1.LevelByCharacter.ContainsKey(characterId),
                    $"{characterId} was fielded in run 0 and should still be fielded in run 1");

                Assert.GreaterOrEqual(run1.LevelByCharacter[characterId], run0.LevelByCharacter[characterId],
                    $"{characterId}'s level fell from run 0 to run 1 -- the save was not carried");

                // EXP CAN LEGITIMATELY READ LOWER after a level-up spends the
                // bar (AddExperience's own rule, the same one
                // CheckRewardsDidNotGoBackwards guards within a single run),
                // so this only asserts the STRICT regression a reset to the
                // Fresh baseline would produce: exp reset all the way to 0
                // with the level unchanged is not a level-up, it is state
                // that did not carry.
                if (run1.LevelByCharacter[characterId] == run0.LevelByCharacter[characterId])
                {
                    Assert.GreaterOrEqual(run1.ExpByCharacter[characterId], run0.ExpByCharacter[characterId],
                        $"{characterId}'s level held steady but exp fell from run 0 to run 1 -- the save was not carried");
                }
            }
        }

        // THE SAME CAREER SEED REPRODUCES EXACTLY -- the plan's own
        // determinism guarantee (PLAN_BALANCE_BOT.md F3), extended from one
        // life to a whole sequence of them: replaying a career must
        // reproduce every life's trace hash, in order, not merely the
        // first one's.
        [Test]
        public void PlayingTheSameCareerTwice_ProducesIdenticalTraceHashesForEveryLife()
        {
            var first = BotRunDriver.PlayCareer(
                seed: 5, archetype: "RandomLegal", profile: "Fresh", runsInCareer: 3, depthCapSteps: 16);
            var second = BotRunDriver.PlayCareer(
                seed: 5, archetype: "RandomLegal", profile: "Fresh", runsInCareer: 3, depthCapSteps: 16);

            Assert.AreEqual(first.Runs.Count, second.Runs.Count);
            for (int i = 0; i < first.Runs.Count; i++)
            {
                Assert.AreEqual(first.Runs[i].Result.Trace.Hash(), second.Runs[i].Result.Trace.Hash(),
                    $"life {i} of the career did not replay identically");
            }
        }
    }
}
