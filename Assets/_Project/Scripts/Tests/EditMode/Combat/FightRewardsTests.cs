using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // What clearing a room pays out, and what the fight itself does when it is
    // won.
    //
    // The two scaling rules here decide whether descending is worth doing at
    // all, and in v1 both were inline arithmetic between a manager lookup and a
    // save write -- so the only way to check either was to play a fight and read
    // the results screen.
    public class FightRewardsTests
    {
        private static EnemyKit Kit(int exp, int gold, string name = "Foe", bool isElite = false) =>
            new EnemyKit(new ResolvedEnemy(name.ToLowerInvariant(), name, new StatBlock(),
                exp, gold, false, DamageType.Physical, DamageType.Physical, 0), isElite);

        // ---- the payout ---------------------------------------------------------

        [Test]
        public void ARoomPaysTheSumOfWhatItHeld()
        {
            var payout = VictoryRewards.For(new[] { Kit(10, 5), Kit(20, 7) }, isElite: false, depthStep: 0);

            Assert.AreEqual(30, payout.Experience);
            Assert.AreEqual(12, payout.Gold);
        }

        [Test]
        public void AnEmptyRoomPaysNothingRatherThanThrowing()
        {
            var payout = VictoryRewards.For(new EnemyKit[0], isElite: false, depthStep: 0);

            Assert.AreEqual(0, payout.Experience);
            Assert.AreEqual(0, payout.Gold);
        }

        [Test]
        public void ANullRosterPaysNothing()
        {
            var payout = VictoryRewards.For(null, isElite: false, depthStep: 0);
            Assert.AreEqual(0, payout.Experience);
        }

        [Test]
        public void AnEliteRoomPaysMoreThanAnOrdinaryOne()
        {
            // Elites fight scaled up, so a genuinely harder fight must not pay
            // out exactly the same as an ordinary one.
            var roster = new[] { Kit(100, 50) };

            var ordinary = VictoryRewards.For(roster, isElite: false, depthStep: 0);
            var elite = VictoryRewards.For(roster, isElite: true, depthStep: 0);

            Assert.Greater(elite.Experience, ordinary.Experience);
            Assert.Greater(elite.Gold, ordinary.Gold);
        }

        [Test]
        public void TheEliteMultiplierIsAppliedBeforeTheDepthCurve()
        {
            // Order matters because both are rounded. Pinned with literal
            // expected values rather than by recomputing the formula, which
            // would make the test a tautology.
            var roster = new[] { Kit(100, 50) };

            var elite = VictoryRewards.For(roster, isElite: true, depthStep: 0);

            Assert.AreEqual(156, elite.Experience, "100 * 1.56, then a depth curve that is flat at step 0");
            Assert.AreEqual(78, elite.Gold);
        }

        [Test]
        public void DeeperRoomsPayMore()
        {
            // Without this a step-40 fight would cost five times the effort of a
            // step-1 one and pay exactly the same, so the optimal way to play an
            // endless dungeon would be never to descend.
            var roster = new[] { Kit(100, 50) };

            var shallow = VictoryRewards.For(roster, isElite: false, depthStep: 1);
            var deep = VictoryRewards.For(roster, isElite: false, depthStep: 40);

            Assert.Greater(deep.Experience, shallow.Experience);
            Assert.Greater(deep.Gold, shallow.Gold);
        }

        [Test]
        public void GoldRidesTheSameCurveAsTheThreat()
        {
            // Not a separate rate, on purpose: gold is spent inside the run it
            // was earned in, at a shop whose prices climb with depth, so if it
            // lagged difficulty the deep game would be worse value per fight.
            var payout = VictoryRewards.For(new[] { Kit(100, 100) }, isElite: false, depthStep: 25);

            Assert.AreEqual(DifficultyCurve.ScaleReward(100, 25), payout.Gold);
        }

        [Test]
        public void ExperienceDoesNotRideTheThreatCurve()
        {
            // Progression v2 phase 2 gave experience its own, much flatter
            // rate -- the whole reason the two are separate methods now. The
            // literal pins live in ExperienceRateTests; this one only has to
            // catch the two being wired back together.
            var payout = VictoryRewards.For(new[] { Kit(100, 100) }, isElite: false, depthStep: 25);

            Assert.AreEqual(DifficultyCurve.ScaleExperience(100, 25), payout.Experience);
            Assert.Less(payout.Experience, payout.Gold,
                "experience and gold are on the same rate again");
        }

        [Test]
        public void ExperienceIsNotSplitAcrossTheParty()
        {
            // Every fielded character receives the FULL amount. A pre-existing
            // design decision, made visible rather than changed.
            var payout = VictoryRewards.For(new[] { Kit(60, 0) }, isElite: false, depthStep: 0);

            Assert.AreEqual(60, VictoryRewards.ExperienceFor(payout, isDowned: false));
        }

        // HALF, ROUNDED UP, and never zero -- progression v2's contract 4. It
        // used to be nothing at all, which compounded a bad run into a bad
        // career: the player losing fights is the one who most needs the
        // levels that would let them stop losing.
        //
        // The three literals are the plan's own worked cases plus the two
        // edges. 29 is an average room-0 normal fight and 100 the boss;
        // rounding UP is what keeps a 1-experience payout from paying nothing
        // and quietly restoring the old rule.
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(15, 8)]
        [TestCase(29, 15)]
        [TestCase(100, 50)]
        public void ADownedSquadMemberEarnsHalfRoundedUp(int raw, int expected)
        {
            var payout = VictoryRewards.For(new[] { Kit(raw, 0) }, isElite: false, depthStep: 0);

            Assert.AreEqual(expected, VictoryRewards.ExperienceFor(payout, isDowned: true));
            Assert.AreEqual(raw, VictoryRewards.ExperienceFor(payout, isDowned: false),
                "standing pay moved with downed pay");
        }

        // ---- drops ---------------------------------------------------------------

        [Test]
        public void EliteAndBossRoomsBothGuaranteeAWeapon()
        {
            // Boss alone made weapons effectively unequippable, because the boss
            // was the last room and clearing it ended the run.
            Assert.IsTrue(VictoryRewards.GuaranteesWeapon(isBossFight: true, isEliteFight: false));
            Assert.IsTrue(VictoryRewards.GuaranteesWeapon(isBossFight: false, isEliteFight: true));
            Assert.IsFalse(VictoryRewards.GuaranteesWeapon(isBossFight: false, isEliteFight: false));
        }

        [Test]
        public void EveryDropIsAttributedToTheEnemyThatLeftIt()
        {
            var drops = VictoryRewards.RollConsumableDrops(
                Enumerable.Range(0, 40).Select(i => Kit(0, 0, "Rat" + i)).ToList(),
                new[] { "potion", "elixir" },
                new SeededRandom(11));

            Assert.IsNotEmpty(drops, "40 rolls at 30% should produce something");
            foreach (var drop in drops)
            {
                Assert.IsTrue(drop.FromEnemyName.StartsWith("Rat"));
                CollectionAssert.Contains(new[] { "potion", "elixir" }, drop.ItemId);
            }
        }

        [Test]
        public void TheSameSeedDropsTheSameLoot()
        {
            var roster = Enumerable.Range(0, 20).Select(i => Kit(0, 0, "Rat" + i)).ToList();
            var pool = new[] { "potion", "elixir", "tonic" };

            var first = VictoryRewards.RollConsumableDrops(roster, pool, new SeededRandom(42));
            var second = VictoryRewards.RollConsumableDrops(roster, pool, new SeededRandom(42));

            CollectionAssert.AreEqual(
                first.Select(d => d.FromEnemyName + ":" + d.ItemId).ToList(),
                second.Select(d => d.FromEnemyName + ":" + d.ItemId).ToList());
        }

        [Test]
        public void AnEmptyConsumablePoolDropsNothing()
        {
            var drops = VictoryRewards.RollConsumableDrops(
                new[] { Kit(0, 0) }, new string[0], new SeededRandom(1));

            CollectionAssert.IsEmpty(drops);
        }

        [Test]
        public void DropsAreOneRollPerEnemy()
        {
            // Not one roll for the room: a room of six is six chances.
            var roster = Enumerable.Range(0, 200).Select(i => Kit(0, 0, "Rat" + i)).ToList();

            var drops = VictoryRewards.RollConsumableDrops(roster, new[] { "potion" }, new SeededRandom(7));

            Assert.LessOrEqual(drops.Count, roster.Count);
            Assert.Greater(drops.Count, roster.Count / 10, "roughly 30%, not one flat roll");
        }

        // ---- what the fight itself does on a win ----------------------------------

        private static (FightSession session, CombatantState hero, CombatEncounter encounter) WinnableFight(
            PlayerKit kit = null)
        {
            var hero = new CombatantState("Hero", true, 500, 10, 50, 10);
            var doomed = new CombatantState("Doomed", false, 1, 10, 5, 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { doomed });
            var session = new FightSession(encounter,
                kit == null ? null : new List<PlayerKit> { kit }, null, new SeededRandom(1))
            { DamageVarianceRange = 0f };
            return (session, hero, encounter);
        }

        [Test]
        public void EveryoneStillStandingCelebrates()
        {
            var (session, hero, encounter) = WinnableFight();

            session.ExecuteAttack(encounter.Enemies[0]);
            var beats = session.DrainBeats();

            Assert.IsTrue(session.PlayerWon);
            Assert.AreEqual(FightSession.Stances.Victory, beats[beats.Count - 1].Stances[hero]);
        }

        [Test]
        public void TheCelebrationLandsOnTheBlowThatEarnedIt()
        {
            // Decided after the killing beat was committed, so left as an
            // immediate message it would land OLDER than the blow that caused
            // it -- and the trim drops from the front.
            var (session, _, encounter) = WinnableFight();

            session.ExecuteAttack(encounter.Enemies[0]);
            var beats = session.DrainBeats();

            Assert.AreEqual(1, beats.Count, "no beat is invented for the ending");
            Assert.IsTrue(beats[0].Messages.Contains("Victory!"));
            CollectionAssert.IsEmpty(session.DrainImmediateMessages());
        }

        [Test]
        public void TheSquadDoesNotWhoopTwiceOverItself()
        {
            // A round resolves in a single pass and can reach the ending from
            // more than one path.
            var (session, _, encounter) = WinnableFight(
                new PlayerKit("shawn", CharacterRole.Tank, null, null, null));

            session.ExecuteAttack(encounter.Enemies[0]);

            CollectionAssert.AreEqual(new[] { "shawn" }, session.VictoryVoiceIds);
        }

        [Test]
        public void NobodyCelebratesAFightThatIsStillRunning()
        {
            var hero = new CombatantState("Hero", true, 500, 10, 20, 10);
            var tank = new CombatantState("Tank", false, 1000, 10, 5, 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { tank });
            var session = new FightSession(encounter,
                new List<PlayerKit> { new PlayerKit("shawn", CharacterRole.Tank, null, null, null) },
                null, new SeededRandom(1)) { DamageVarianceRange = 0f };

            session.ExecuteAttack(tank);

            CollectionAssert.IsEmpty(session.VictoryVoiceIds);
            Assert.IsFalse(session.DrainBeats().SelectMany(b => b.Messages).Any(m => m.Contains("Victory")));
        }

        [Test]
        public void ALostFightIsNotAVictory()
        {
            var doomedHero = new CombatantState("Hero", true, 1, 10, 1, 1);
            var killer = new CombatantState("Killer", false, 1000, 10, 500, 10);
            var encounter = new CombatEncounter(new[] { doomedHero }, new[] { killer });
            var session = new FightSession(encounter, null,
                new List<EnemyKit> { Kit(0, 0, "Killer") }, new SeededRandom(1))
            { DamageVarianceRange = 0f };

            session.Begin();

            Assert.IsTrue(session.IsOver);
            Assert.IsFalse(session.PlayerWon);
            CollectionAssert.IsEmpty(session.VictoryVoiceIds);
        }
    }
}
