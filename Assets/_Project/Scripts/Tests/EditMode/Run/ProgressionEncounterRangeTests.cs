using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // THE SAME CAREER, ROLLED RATHER THAN ASSUMED.
    //
    // ProgressionCareerTests feeds the plan's EXPECTED per-leg pay through the
    // real level-up loop. This one generates two hundred real descents with
    // the real map generator and the real encounter roll, pays each room with
    // the real VictoryRewards, and reports the SPREAD -- because the
    // expectation is an average over six rolled rooms a leg and a player only
    // ever plays one draw of it.
    //
    // WHAT IS REAL: DescentMapGenerator.GenerateLegFor (so the forced elite at
    // room 4 and boss at room 8 come from the generator, not from a
    // restatement of them), EncounterRoll.Roll against the pool enemies.json
    // actually resolves to, banded by RunDepth.FloorFor exactly as
    // RunEncounter does, and VictoryRewards.For at the room's own absolute
    // step. The rng streams are opened the way production opens them:
    // RngStreams.Fight keyed to (runSeed, step, nodeId), which is what makes a
    // room's draw a function of where it is rather than of how many fights
    // came before it.
    //
    // WHAT IS NOT: which branch the player takes. That is a decision a person
    // makes on the map screen, so it is drawn uniformly from the open
    // neighbours on its own stream. A policy that preferred fights would push
    // every figure below up; one that avoided them would push it down. The
    // spread reported here is the neutral case and should be read as such.
    //
    // ASSERTIONS ARE DELIBERATELY LOOSE -- the median inside 15% of the plan's
    // figure, and nothing about min or max. A seeded simulation pinned tightly
    // is a test that fails on an unrelated generator retune while saying
    // nothing about whether the game got better or worse. The numbers that
    // matter are PRINTED, and the ones current when this landed are in the
    // commit message.
    public class ProgressionEncounterRangeTests
    {
        private const int Seeds = 200;

        // xp_model.md Part B at 25 permille -- the same figures
        // ProgressionCareerTests uses as its inputs.
        private const int PlanLeg2 = 710;
        private const int PlanLeg5 = 2_642;
        private const int PlanLeg10 = 10_153;

        private const double Tolerance = 0.15;

        // ---- content ----------------------------------------------------------

        private static List<ResolvedEnemy> Enemies()
        {
            var raw = ContentDataFiles.ParseFile<RawEnemyFile>(ContentDataFiles.DataPath("enemies.json")).enemies;
            bool ok = EnemyEntryResolver.TryResolveAll(raw, out var resolved, out var errors);
            Assert.IsTrue(ok, "enemies.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));

            // ContentBuilder writes no asset for a benched row, so
            // ContentDatabase never sees one and it cannot spawn. Mirrored
            // rather than assumed, the same way ContentStampIdsTests mirrors
            // it.
            return resolved.Where(e => e.Active).ToList();
        }

        private static IReadOnlyList<int> Costs()
        {
            var raw = ContentDataFiles.ParseFile<RawLevelCurveFile>(
                ContentDataFiles.DataPath("level_curve.json")).levels;
            bool ok = LevelCurveEntryResolver.TryResolveAll(raw, out var resolved, out var errors);
            Assert.IsTrue(ok, "level_curve.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));
            return LevelCurveEntryResolver.CostsOf(resolved);
        }

        // ---- one run ------------------------------------------------------------

        // Walks `legs` legs of the descent generated from `runSeed`, choosing
        // a branch at every column, and returns what the whole run paid in
        // experience.
        private static int RunExperience(ulong runSeed, int legs,
            IReadOnlyList<ResolvedEnemy> enemies, IReadOnlyList<EnemyCandidate> pool)
        {
            var byId = enemies.ToDictionary(e => e.Id);

            // A stream of its own for the road, so adding or removing a branch
            // choice cannot shift the enemy draws.
            var road = new SeededRandom(runSeed ^ 0x9E3779B97F4A7C15UL);

            int total = 0;

            for (int leg = 0; leg < legs; leg++)
            {
                int legStartStep = leg * DescentMapGenerator.DefaultLegLength;
                int floor = RunDepth.FloorFor(legStartStep);

                // restBeforeBoss false: assumption A1 of the model -- the
                // forced rest before a boss only exists once the track's
                // level-30 node is collected, and a run being modelled from
                // level 1 is below it.
                var map = DescentMapGenerator.GenerateLegFor(runSeed, legStartStep);

                var node = map.Entry;
                Assert.IsNotNull(node, $"leg {leg + 1} generated no entry column");

                for (int depth = 1; depth <= DescentMapGenerator.DefaultLegLength; depth++)
                {
                    var open = map.ReachableFrom(node.Id);
                    Assert.IsNotEmpty(open, $"leg {leg + 1} column {depth} is unreachable");

                    node = map.Node(open[road.NextInt(0, open.Count)]);
                    int step = legStartStep + depth;

                    if (node.Type != RoomType.Fight && node.Type != RoomType.EliteFight
                        && node.Type != RoomType.Boss)
                    {
                        continue;
                    }

                    var rng = RngStreams.Open(runSeed, RngStreams.Fight, step, node.Id);
                    var roll = EncounterRoll.Roll(node.Type, pool, rng, declaredBossId: null, floor: floor);

                    var kits = roll.EnemyIds
                        .Where(byId.ContainsKey)
                        .Select(id => new EnemyKit(byId[id], roll.IsElite))
                        .ToList();

                    total += VictoryRewards.For(kits, roll.IsElite, step).Experience;
                }
            }

            return total;
        }

        private sealed class Spread
        {
            public int Min;
            public int Median;
            public int Max;
            public int MinLevel;
            public int MedianLevel;
            public int MaxLevel;
        }

        private static Spread Sample(int legs)
        {
            var enemies = Enemies();
            var pool = enemies
                .Select(e => new EnemyCandidate(e.Id, e.IsBoss, e.AvoidsFrontSlot, e.MinFloor, e.SlotSpan))
                .ToList();
            var costs = Costs();

            var totals = new List<int>(Seeds);
            for (int seed = 1; seed <= Seeds; seed++)
            {
                totals.Add(RunExperience((ulong)seed, legs, enemies, pool));
            }

            totals.Sort();

            int LevelFor(int xp) => LevelCurve.AddExperience(costs, RewardTrack.StartingLevel, 0, xp).Level;

            return new Spread
            {
                Min = totals[0],
                Median = totals[totals.Count / 2],
                Max = totals[totals.Count - 1],
                MinLevel = LevelFor(totals[0]),
                MedianLevel = LevelFor(totals[totals.Count / 2]),
                MaxLevel = LevelFor(totals[totals.Count - 1]),
            };
        }

        [TestCase(2, PlanLeg2)]
        [TestCase(5, PlanLeg5)]
        [TestCase(10, PlanLeg10)]
        public void TheRolledSpreadBracketsThePlansFigure(int legs, int planTotal)
        {
            var spread = Sample(legs);

            TestContext.WriteLine(
                $"leg-{legs} run over {Seeds} seeds: " +
                $"min {spread.Min:N0} (level {spread.MinLevel}), " +
                $"median {spread.Median:N0} (level {spread.MedianLevel}), " +
                $"max {spread.Max:N0} (level {spread.MaxLevel}); " +
                $"plan says {planTotal:N0}");

            double drift = System.Math.Abs(spread.Median - planTotal) / (double)planTotal;

            Assert.Less(drift, Tolerance,
                $"the median leg-{legs} run pays {spread.Median:N0} against the plan's {planTotal:N0}, " +
                $"a drift of {drift:P1} -- the model and the game have diverged");
        }

        // A vacuity guard, not a balance claim. A simulation that generated
        // zero fights would report a stable, confident zero and every drift
        // check above would still be comparing something to something.
        [Test]
        public void EverySeedActuallyFoughtSomething()
        {
            var spread = Sample(2);

            Assert.Greater(spread.Min, 0, "some seed walked two whole legs and never had a fight");
            Assert.Greater(spread.MinLevel, RewardTrack.StartingLevel,
                "the worst seed of two legs did not earn a single level");
        }
    }
}
