using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // WHAT A FIGHT PAYS IN EXPERIENCE, at the four depths the progression v2
    // model is written against.
    //
    // Phase 2 of docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §7: gold
    // keeps the health curve (75 permille a step) and experience moves to its
    // own, much flatter 25. The literals below are the model's own figures
    // (docs/handoffs/progression_v2/xp_model.md Part A/B, recomputed at 25
    // permille), written down rather than recomputed here -- CLAUDE.md gotcha
    // 5. Recomputing 1.025^step in this file would assert only that the
    // method is deterministic.
    //
    // THE THREE ROOM TYPES, and where their raw numbers come from
    // (Assets/_Project/ContentData/enemies.json, active rows only):
    //
    //   NORMAL   the floor-1 pool is rat 15 and beetle 24, mean raw 19.5, and
    //            EncounterRoll fields NextInt(1,3) of them -- 1 or 2 with
    //            equal odds, mean 1.5. So the model's average room-0 normal
    //            fight is 1.5 x 19.5 = 29.25, i.e. 29 as an integer payout.
    //            The concrete realisation pinned end-to-end below is one rat
    //            and one beetle, raw 39.
    //   ELITE    exactly 2 enemies (EncounterRoll.EliteEnemyCount) at
    //            VictoryRewards.EliteRewardMultiplier 1.56: 2 x 19.5 x 1.56
    //            = 60.84, rounded away from zero to 61 at room 0.
    //   BOSS     forest_warden, raw 100, and the elite multiplier does NOT
    //            apply to a boss room (EncounterRoll authors isBoss with
    //            isElite false) -- its authored reward does the work.
    public class ExperienceRateTests
    {
        // The four depths the plan's tables are stated at. Room 0 is the
        // start of a run, 8 the leg-1 boss, 40 the leg-5 boss and 80 the leg-
        // 10 boss of a full deep run.
        private const int Room0 = 0;
        private const int Room8 = 8;
        private const int Room40 = 40;
        private const int Room80 = 80;

        // ---- the rate itself ------------------------------------------------

        // Per-fight pay by room type, as raw integer payouts through the
        // curve. Literals from xp_model.md Part A/B at 25 permille.
        [TestCase(29, Room0, 29)]
        [TestCase(29, Room8, 35)]
        [TestCase(29, Room40, 77)]
        [TestCase(29, Room80, 209)]
        [TestCase(61, Room0, 61)]
        [TestCase(61, Room8, 74)]
        [TestCase(61, Room40, 163)]
        [TestCase(61, Room80, 439)]
        [TestCase(100, Room0, 100)]
        [TestCase(100, Room8, 121)]
        [TestCase(100, Room40, 268)]
        [TestCase(100, Room80, 720)]
        public void ExperienceScalesAtItsOwnRate(int raw, int step, int expected)
        {
            Assert.AreEqual(expected, DifficultyCurve.ScaleExperience(raw, step));
        }

        // The half that did NOT move. Same literals the health curve has
        // always produced, restated here so a future retune of the experience
        // rate cannot quietly take gold with it.
        [TestCase(20, Room0, 20)]
        [TestCase(20, Room8, 35)]
        [TestCase(20, Room40, 360)]
        [TestCase(20, Room80, 6511)]
        [TestCase(100, Room0, 100)]
        [TestCase(100, Room8, 178)]
        [TestCase(100, Room40, 1804)]
        [TestCase(100, Room80, 32559)]
        public void GoldStaysOnTheHealthCurve(int raw, int step, int expected)
        {
            Assert.AreEqual(expected, DifficultyCurve.ScaleReward(raw, step));
            Assert.AreEqual(DifficultyCurve.ScaleHealth(raw, step), DifficultyCurve.ScaleReward(raw, step));
        }

        // THE POINT OF THE SPLIT, in one number: a step-80 fight used to pay
        // 325x a step-0 one in experience and now pays 7.2x. The old figure
        // is what made a hundred-level track worth under four deep runs.
        [Test]
        public void DepthNoLongerDominatesTheExperienceTotal()
        {
            Assert.AreEqual(720, DifficultyCurve.ScaleExperience(100, Room80));
            Assert.AreEqual(32559, DifficultyCurve.ScaleReward(100, Room80));
        }

        // ---- the same pay, through the production path ----------------------

        // A NORMAL ROOM of one rat and one beetle -- the concrete two-enemy
        // draw the 29.25 average is the mean of.
        [TestCase(Room0, 39, 20)]
        [TestCase(Room8, 47, 35)]
        [TestCase(Room40, 104, 360)]
        [TestCase(Room80, 281, 6511)]
        public void ANormalRoomPaysThis(int step, int expectedExp, int expectedGold)
        {
            var payout = VictoryRewards.For(new[] { Rat(), Beetle() }, isElite: false, depthStep: step);

            Assert.AreEqual(expectedExp, payout.Experience);
            Assert.AreEqual(expectedGold, payout.Gold);
        }

        // AN ELITE ROOM, the same two enemies at x1.56. Raw 39 x 1.56 =
        // 60.84, away from zero to 61, then the depth curve -- which is the
        // order VictoryRewards applies them in and the order the room-0 row
        // proves.
        [TestCase(Room0, 61, 31)]
        [TestCase(Room8, 74, 55)]
        [TestCase(Room40, 163, 559)]
        [TestCase(Room80, 439, 10093)]
        public void AnEliteRoomPaysThis(int step, int expectedExp, int expectedGold)
        {
            var payout = VictoryRewards.For(new[] { Rat(), Beetle() }, isElite: true, depthStep: step);

            Assert.AreEqual(expectedExp, payout.Experience);
            Assert.AreEqual(expectedGold, payout.Gold);
        }

        // A BOSS ROOM. forest_warden is the only active boss; isElite is
        // false for a boss room, so nothing multiplies its authored 100.
        [TestCase(Room0, 100, 60)]
        [TestCase(Room8, 121, 107)]
        [TestCase(Room40, 268, 1082)]
        [TestCase(Room80, 720, 19535)]
        public void ABossRoomPaysThis(int step, int expectedExp, int expectedGold)
        {
            var payout = VictoryRewards.For(new[] { ForestWarden() }, isElite: false, depthStep: step);

            Assert.AreEqual(expectedExp, payout.Experience);
            Assert.AreEqual(expectedGold, payout.Gold);
        }

        // The raw rows of enemies.json, as kits. Written out rather than
        // loaded, because this fixture is pinning the CURVE and a content
        // edit to rat's expReward should fail the content pin
        // (EnemyContentPinTests), not this.
        private static EnemyKit Rat() => Kit("rat", 15, 8);
        private static EnemyKit Beetle() => Kit("beetle", 24, 12);
        private static EnemyKit ForestWarden() => Kit("forest_warden", 100, 60);

        private static EnemyKit Kit(string id, int exp, int gold) =>
            new EnemyKit(new ResolvedEnemy(id, id, new StatBlock(),
                exp, gold, false, DamageType.Physical, DamageType.Physical, 0), isElite: false);
    }
}
