using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Tests
{
    // Reads Assets/_Project/ContentData/level_curve.json from disk through
    // the REAL LevelCurveEntryResolver -- the same path ContentBuilder takes
    // -- and pins the authored ladder against
    // docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §3.
    //
    // TWO PARSERS, ONE ASSERTION, same pattern as RewardTrackContentPinTests:
    // no UnityEngine dependency, so this is a [D] class and runs under plain
    // `dotnet test`, which has no JsonUtility. ParseFile/DataPath live on
    // Shared/ContentDataFiles.
    //
    // WHAT THIS PINS THAT THE RESOLVER DOES NOT: the resolver refuses a table
    // that breaks a rule, but it has no opinion about which numbers are
    // right. The cumulative figures below are the plan's own, and they are
    // the ones a career is priced against -- a retune that silently makes
    // level 30 cheaper is a change to how long the game is, and should have
    // to be typed here too.
    public class LevelCurveContentPinTests
    {
        private static List<ResolvedLevelCost> Rows()
        {
            var raw = ContentDataFiles.ParseFile<RawLevelCurveFile>(
                ContentDataFiles.DataPath("level_curve.json")).levels;

            bool ok = LevelCurveEntryResolver.TryResolveAll(raw, out var resolved, out var errors);
            Assert.IsTrue(ok, "level_curve.json does not resolve: " + string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }

        private static IReadOnlyList<int> Costs() => LevelCurveEntryResolver.CostsOf(Rows());

        // ---- the shipped table ------------------------------------------------

        [Test]
        public void TheAuthoredTableCoversExactlyTheTrack()
        {
            var rows = Rows();

            Assert.AreEqual(RewardTrack.MaxLevel - LevelCurve.FirstPaidLevel + 1, rows.Count,
                "one row per level from 2 to the cap");
            Assert.AreEqual(LevelCurve.FirstPaidLevel, rows.First().Level);
            Assert.AreEqual(RewardTrack.MaxLevel, rows.Last().Level);
            Assert.AreEqual(RewardTrack.MaxLevel, LevelCurve.MaxLevel(Costs()));
        }

        // THE PLAN'S §3 TABLE, level by level. Written out rather than
        // generated, because generating it here would be a second copy of the
        // authoring decision and would agree with a mistyped JSON file.
        [TestCase(2, 75)]
        [TestCase(3, 85)]
        [TestCase(4, 150)]
        [TestCase(5, 250)]
        [TestCase(10, 500)]
        [TestCase(11, 1200)]
        [TestCase(15, 3000)]
        [TestCase(17, 4000)]
        [TestCase(18, 4000)]
        [TestCase(20, 5000)]
        [TestCase(21, 6000)]
        [TestCase(29, 10000)]
        [TestCase(30, 10000)]
        [TestCase(40, 10000)]
        public void ALevelCostsWhatThePlanSays(int level, int expected)
        {
            Assert.AreEqual(expected, LevelCurve.ExpToNextLevel(Costs(), level - 1),
                $"level {level}'s cost");
        }

        // THE CUMULATIVE ANCHORS, which are what the career table in §3 is
        // actually built on. Every one of these is a literal from the plan.
        [TestCase(3, 160)]
        [TestCase(5, 560)]
        [TestCase(10, 2560)]
        [TestCase(15, 12760)]
        [TestCase(20, 33760)]
        [TestCase(30, 115760)]
        [TestCase(40, 215760)]
        public void ReachingALevelCostsThisMuchInTotal(int level, int expected)
        {
            Assert.AreEqual(expected, LevelCurve.CumulativeCost(Costs(), level));
        }

        // CONTRACT 2, measured against the income it is stated in terms of:
        // no level costs more than one deep run's pay (10,153 at 25 permille)
        // and none costs less than two typical room-0 fights (58). The
        // resolver enforces this on every table; this says the shipped one
        // sits inside it with room, rather than on either edge.
        [Test]
        public void EveryAuthoredCostIsInsideContractTwo()
        {
            var costs = Costs();

            Assert.AreEqual(75, costs.Min(), "the cheapest level, well clear of the 58 floor");
            Assert.AreEqual(10000, costs.Max(), "the dearest level, just under the 10,153 ceiling");

            foreach (int cost in costs)
            {
                Assert.GreaterOrEqual(cost, LevelCurveEntryResolver.MinCost);
                Assert.LessOrEqual(cost, LevelCurveEntryResolver.MaxCost);
            }
        }

        [Test]
        public void TheAuthoredTableNeverGetsCheaper()
        {
            var costs = Costs();

            for (int i = 1; i < costs.Count; i++)
            {
                Assert.GreaterOrEqual(costs[i], costs[i - 1],
                    $"level {i + LevelCurve.FirstPaidLevel} costs less than the level before it");
            }
        }

        // ---- the rules, proved by breaking them ------------------------------
        //
        // A validation nobody has watched refuse anything is a validation that
        // might be scanning nothing -- CODE_STANDARDS §8's vacuity guard. Each
        // of these mutates a COPY of the real file and asserts the refusal.

        private static RawLevelCurveEntry[] Authored() =>
            ContentDataFiles.ParseFile<RawLevelCurveFile>(
                ContentDataFiles.DataPath("level_curve.json")).levels
                .Select(row => new RawLevelCurveEntry { level = row.level, cost = row.cost })
                .ToArray();

        [Test]
        public void AGapInTheLadderIsRefused()
        {
            var rows = Authored().Where(r => r.level != 7).ToArray();

            Assert.IsFalse(LevelCurveEntryResolver.TryResolveAll(rows, out _, out var errors));
            Assert.IsTrue(errors.Any(e => e.Contains("missing 7")), string.Join("; ", errors));
        }

        [Test]
        public void ALadderThatOverrunsTheCapIsRefused()
        {
            var rows = Authored().ToList();
            rows.Add(new RawLevelCurveEntry { level = RewardTrack.MaxLevel + 1, cost = 10000 });

            Assert.IsFalse(LevelCurveEntryResolver.TryResolveAll(rows, out _, out var errors));
            Assert.IsTrue(errors.Any(e => e.Contains("not on the track")), string.Join("; ", errors));
        }

        [Test]
        public void ACostThatFallsIsRefused()
        {
            var rows = Authored();
            rows.Single(r => r.level == 12).cost = 100;

            Assert.IsFalse(LevelCurveEntryResolver.TryResolveAll(rows, out _, out var errors));
            Assert.IsTrue(errors.Any(e => e.Contains("must never fall")), string.Join("; ", errors));
        }

        [TestCase(57)]
        [TestCase(10154)]
        public void ACostOutsideContractTwoIsRefused(int cost)
        {
            var rows = Authored();
            rows.Single(r => r.level == 20).cost = cost;

            Assert.IsFalse(LevelCurveEntryResolver.TryResolveAll(rows, out _, out var errors));
            Assert.IsTrue(errors.Any(e => e.Contains("outside 58-10153")), string.Join("; ", errors));
        }
    }
}
