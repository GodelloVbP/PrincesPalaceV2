using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Tests
{
    // A WHOLE CAREER, RUN BY RUN, THROUGH THE REAL LEVEL-UP CODE.
    //
    // docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §3's career table is
    // the phase-2 pin: the exact level AND remainder a player sits on after
    // each of a fixed sequence of runs, plus the two knockout trajectories and
    // the stuck-player one. A cost table can look reasonable row by row and
    // still put the first ability nine fights late or level 30 six runs early;
    // this is the only thing that reads the table the way a player does.
    //
    // WHAT IS REAL HERE AND WHAT IS AN INPUT.
    //
    //   REAL: the cost table (level_curve.json through
    //   LevelCurveEntryResolver, the same path ContentBuilder takes) and the
    //   level-up loop (LevelCurve.AddExperience, which IS
    //   Character.AddExperience -- Character is a Core type and this assembly
    //   cannot see it, which is exactly why the loop moved down to Domain).
    //
    //   AN INPUT: what a run PAYS. A run's total is an expectation over which
    //   of six rolled rooms turn out to be fights, so it is not deterministic
    //   and cannot be produced by running the generator once. The plan's own
    //   per-leg figures are used as literal inputs instead -- 710 for a leg-2
    //   death, 1,221 for leg 3, 2,642 for leg 5, 10,153 for a full deep run,
    //   all from xp_model.md Part B at 25 permille. The random version, with
    //   its spread, is ProgressionEncounterRangeTests.
    //
    // THREE OF THE PLAN'S OWN NUMBERS DO NOT COME OUT. They are kept below as
    // ignored tests carrying the plan's literal, beside the passing test
    // carrying the computed one, so the disagreement is in the file rather
    // than in a report nobody re-reads. Each says which it believes and why.
    public class ProgressionCareerTests
    {
        // ---- the inputs, from the plan ---------------------------------------

        private const int Leg2Death = 710;
        private const int Leg3Death = 1_221;
        private const int Leg5Death = 2_642;
        private const int DeepRun = 10_153;

        // BOSS PAY PER LEG, at 25 permille. A leg-N boss is at absolute step
        // 8N and is always forest_warden, raw 100, with no elite multiplier
        // (EncounterRoll authors a boss room isElite false). So the row is
        // floor(100 * 1.025^(8N)), worked out from xp_model.md's per-room
        // table rather than by calling the curve:
        //
        //   leg    1    2    3    4    5    6    7    8    9   10
        //   step   8   16   24   32   40   48   56   64   72   80
        //   pay  121  148  180  220  268  327  398  485  591  720
        //
        // What a DOWNED character collects is half of that, rounded up
        // (contract 4), so what they LOSE against a standing run is the
        // rounded-down half: 60, 74, 90, 110, 134, 163, 199, 242, 295, 360.
        private static readonly int[] BossPayByLeg =
            { 121, 148, 180, 220, 268, 327, 398, 485, 591, 720 };

        // ---- the machinery ----------------------------------------------------

        private static IReadOnlyList<int> Costs()
        {
            var raw = ContentDataFiles.ParseFile<RawLevelCurveFile>(
                ContentDataFiles.DataPath("level_curve.json")).levels;

            bool ok = LevelCurveEntryResolver.TryResolveAll(raw, out var resolved, out var errors);
            Assert.IsTrue(ok, "level_curve.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));
            return LevelCurveEntryResolver.CostsOf(resolved);
        }

        // Where a character stands after being paid `runs`, in order, through
        // the production loop. Not a reimplementation of it -- every level-up
        // in the sequence goes through LevelCurve.AddExperience exactly as a
        // victory would.
        private static (int Level, int Exp) After(IEnumerable<int> runs)
        {
            var costs = Costs();
            int level = RewardTrack.StartingLevel;
            int exp = 0;

            foreach (int pay in runs)
            {
                var after = LevelCurve.AddExperience(costs, level, exp, pay);
                level = after.Level;
                exp = after.Exp;
            }

            return (level, exp);
        }

        // THE PLAN'S FIXED SEQUENCE: run 1 dies on leg 2, run 2 on leg 3, run
        // 3 on leg 5, and every run from 4 on is a full deep run.
        private static IEnumerable<int> Career(int runs) =>
            new[] { Leg2Death, Leg3Death, Leg5Death }
                .Concat(Enumerable.Repeat(DeepRun, System.Math.Max(0, runs - 3)))
                .Take(runs);

        // ---- the boss table agrees with the curve -----------------------------

        // NOT the source of any expectation below -- the literals are. This
        // only says the table transcribed from xp_model.md and the rate
        // shipped in DifficultyCurve are describing the same game, which is
        // the one way a hand-copied table can be wrong without any of the
        // career rows noticing.
        [Test]
        public void TheBossPayTableMatchesTheShippedExperienceRate()
        {
            for (int leg = 1; leg <= BossPayByLeg.Length; leg++)
            {
                Assert.AreEqual(BossPayByLeg[leg - 1], DifficultyCurve.ScaleExperience(100, leg * 8),
                    $"leg {leg}'s boss");
            }
        }

        // ---- the standing career ----------------------------------------------

        [TestCase(1, 5, 150)]
        [TestCase(3, 11, 813)]
        [TestCase(4, 15, 1_966)]
        [TestCase(6, 20, 1_272)]
        [TestCase(10, 25, 6_884)]
        [TestCase(14, 30, 496)]
        public void AStandingCareerReachesThisLevelWithThisRemainder(int runs, int level, int remainder)
        {
            var (gotLevel, gotExp) = After(Career(runs));

            Assert.AreEqual(level, gotLevel, $"level after run {runs}");
            Assert.AreEqual(remainder, gotExp, $"remainder after run {runs}");
        }

        // RUN 2 IS THE ONE ROW THAT DISAGREES, and the level is not the part
        // that does -- see the ignored test below for the plan's own figure.
        // 1,931 total, less the 1,610 that levels 2 to 8 cost (75 + 85 + 150 +
        // 250 + 300 + 350 + 400), is 321.
        [Test]
        public void AfterTwoRunsTheRemainderIs321Not246()
        {
            var (level, exp) = After(Career(2));

            Assert.AreEqual(8, level);
            Assert.AreEqual(321, exp);
        }

        [Ignore("PLAN DISAGREES WITH ITS OWN COST TABLE. PLAN_PROGRESSION_V2.md section 3 gives run 2 as " +
                "'level 8, 246 of 450'. The level is right and the remainder is not: 710 + 1,221 = 1,931, " +
                "and levels 2-8 cost 75+85+150+250+300+350+400 = 1,610, leaving 321. 246 would need a " +
                "cumulative cost of 1,685, which is 75 more than the table in the same section adds up to -- " +
                "the level-2 row, counted twice or the sum started one row late. Every other row of that " +
                "table reproduces exactly. Owner's call whether the table or the row is wrong; the passing " +
                "pin beside this one asserts 321.")]
        [Test]
        public void ThePlansRunTwoRemainderOf246()
        {
            var (level, exp) = After(Career(2));

            Assert.AreEqual(8, level);
            Assert.AreEqual(246, exp);
        }

        // The end of the ladder. 217,786 against a cumulative 215,760, so the
        // cap is crossed on run 24 with 2,026 to spare -- and stays there,
        // because LevelCurve.AddExperience refuses to go past MaxLevel.
        [Test]
        public void TheCapIsReachedOnRunTwentyFour()
        {
            Assert.AreEqual(39, After(Career(23)).Level, "run 23 reached the cap a run early");

            var (level, exp) = After(Career(24));

            Assert.AreEqual(RewardTrack.MaxLevel, level);
            Assert.AreEqual(2_026, exp, "experience past the cap is kept, not discarded");
        }

        // ---- knocked out at every boss -----------------------------------------
        //
        // Same sequence, with the character downed at the largest room of each
        // leg. A leg-N run therefore pays its standing total less the
        // rounded-down half of every boss up to N (the table at the top).

        private static int KnockedOutAtEveryBoss(int legs, int standingTotal)
        {
            int lost = 0;
            for (int leg = 1; leg <= legs; leg++) lost += BossPayByLeg[leg - 1] / 2;
            return standingTotal - lost;
        }

        private static IEnumerable<int> KnockoutCareer(int runs)
        {
            // 710 - 60 - 74 = 576; 1,221 - 224 = 997; 2,642 - 468 = 2,174;
            // 10,153 - 1,727 = 8,426.
            int leg2 = KnockedOutAtEveryBoss(2, Leg2Death);
            int leg3 = KnockedOutAtEveryBoss(3, Leg3Death);
            int leg5 = KnockedOutAtEveryBoss(5, Leg5Death);
            int deep = KnockedOutAtEveryBoss(10, DeepRun);

            return new[] { leg2, leg3, leg5 }
                .Concat(Enumerable.Repeat(deep, System.Math.Max(0, runs - 3)))
                .Take(runs);
        }

        [Test]
        public void TheKnockoutRunTotalsAreThese()
        {
            // Stated so the trajectory below cannot drift silently on an
            // arithmetic slip in the subtraction.
            Assert.AreEqual(576, KnockedOutAtEveryBoss(2, Leg2Death));
            Assert.AreEqual(997, KnockedOutAtEveryBoss(3, Leg3Death));
            Assert.AreEqual(2_174, KnockedOutAtEveryBoss(5, Leg5Death));
            Assert.AreEqual(8_426, KnockedOutAtEveryBoss(10, DeepRun));
        }

        // STILL LEVEL 5 AFTER RUN 1, which is the claim that matters: contract
        // 3 says a run that dies on leg 2 earns at least three levels, and it
        // does even for a player who is knocked out at both bosses. 576
        // against the 560 that level 5 costs -- a margin of 16, one thin
        // fight, which is worth the owner knowing.
        [Test]
        public void KnockedOutAtEveryBossStillReachesLevelFiveOnRunOne()
        {
            var (level, exp) = After(KnockoutCareer(1));

            Assert.AreEqual(5, level);
            Assert.AreEqual(16, exp, "level 5 was cleared by 16 experience");
        }

        [Test]
        public void KnockedOutAtEveryBossReachesLevelFourteenAfterTheFirstDeepRun()
        {
            Assert.AreEqual(14, After(KnockoutCareer(4)).Level);
        }

        // LEVEL 30 LANDS ON RUN 17, NOT 15. See the ignored test below.
        [Test]
        public void KnockedOutAtEveryBossReachesLevelThirtyOnRunSeventeen()
        {
            Assert.AreEqual(28, After(KnockoutCareer(15)).Level, "run 15");
            Assert.AreEqual(29, After(KnockoutCareer(16)).Level, "run 16");
            Assert.AreEqual(30, After(KnockoutCareer(17)).Level, "run 17");
        }

        [Ignore("PLAN DISAGREES. PLAN_PROGRESSION_V2.md section 3 says the knockout trajectory 'reaches " +
                "level 30 on run 15'. It reaches 28 there and 30 on run 17. The plan's own leg-2 figure " +
                "for this trajectory (562) does not reproduce either -- it comes out 576, and 562 is not " +
                "reachable by rounding the halves either way -- so the paragraph looks computed against a " +
                "boss table other than the one in xp_model.md. The standing career and the stuck-player " +
                "trajectory in the same section both reproduce exactly, so this is that one paragraph " +
                "rather than the model. Owner's call; the passing pins beside this assert 28/29/30 on runs " +
                "15/16/17 and a leg-2 total of 576.")]
        [Test]
        public void ThePlansKnockoutLevelThirtyOnRunFifteen()
        {
            Assert.AreEqual(30, After(KnockoutCareer(15)).Level);
        }

        // ---- the stuck player ---------------------------------------------------
        //
        // Never past leg 3, so 1,221 a run forever. The track does not fix the
        // wall and the plan does not claim it does -- phase 6 evaluates it
        // with one named lever. What is pinned here is how slowly it goes, so
        // the lever is evaluated against a number rather than a feeling.

        private static IEnumerable<int> StuckCareer(int runs) => Enumerable.Repeat(Leg3Death, runs);

        [TestCase(3, 10)]
        [TestCase(11, 15)]
        [TestCase(28, 20)]
        public void AStuckPlayerReachesThisLevel(int runs, int level)
        {
            Assert.AreEqual(level, After(StuckCareer(runs)).Level, $"after {runs} runs of leg 3");
        }

        // And the run BEFORE each of those is still short, which is what makes
        // the three rows above landmarks rather than coincidences.
        [TestCase(2, 9)]
        [TestCase(10, 14)]
        [TestCase(27, 19)]
        public void AStuckPlayerHasNotReachedItYet(int runs, int level)
        {
            Assert.AreEqual(level, After(StuckCareer(runs)).Level, $"after {runs} runs of leg 3");
        }
    }
}
