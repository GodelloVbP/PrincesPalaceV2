using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // How a fight ends, and what it pays.
    //
    // VictoryRewards already pins the arithmetic. What was missing is that
    // anything CALLS it -- a fight could be won and settle nothing, which is
    // exactly the state the screen was in.
    public class FightOutcomeTests
    {
        private static ResolvedEnemy Source(string id, int exp, int gold) =>
            new ResolvedEnemy(id, id, new StatBlock(), exp, gold, false,
                DamageType.Physical, DamageType.Physical, 0);

        private static (FightSession session, CombatantState hero, CombatantState foe) Fight(
            int foeHealth = 1, int heroHealth = 300, bool isElite = false, int depth = 0)
        {
            var hero = new CombatantState("Shawn", true, heroHealth, 30, 40, 0, 10);
            // Speed 9 against the hero's 10: fast enough that the monster
            // genuinely replies to every action. At speed 1 it barely ever acts,
            // which quietly made the defeat cases untestable -- the hero simply
            // never got hit.
            var foe = new CombatantState("Rat", false, foeHealth, 10, 8, 0, 9);

            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null);
            var enemyKit = new EnemyKit(Source("rat", 20, 10), isElite);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(2), isEliteFight: isElite)
            {
                DamageVarianceRange = 0f,
                DepthStep = depth,
            };
            session.Begin();
            return (session, hero, foe);
        }

        private static IEnumerable<string> Lines(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages);

        [Test]
        public void NothingIsPaidWhileTheFightIsStillRunning()
        {
            // Null rather than a zeroed struct: "not settled yet" and "settled at
            // nothing" are different answers and a caller has to be able to tell.
            var (session, _, _) = Fight(foeHealth: 5000);

            Assert.IsNull(session.Payout);
        }

        [Test]
        public void WinningSettlesAPayout()
        {
            var (session, _, foe) = Fight();

            session.ExecuteAttack(foe);

            Assert.IsTrue(session.IsOver);
            Assert.IsTrue(session.PlayerWon);
            Assert.IsNotNull(session.Payout);
            Assert.AreEqual(20, session.Payout.Value.Experience);
            Assert.AreEqual(10, session.Payout.Value.Gold);
        }

        [Test]
        public void ThePayoutIsSaidOutLoud()
        {
            var (session, _, foe) = Fight();

            session.ExecuteAttack(foe);

            var lines = Lines(session).ToList();
            Assert.IsTrue(lines.Any(m => m.Contains("Victory!")));
            Assert.IsTrue(lines.Any(m => m.Contains("20 experience")));
        }

        [Test]
        public void AnEliteRoomPaysMore()
        {
            var (plain, _, plainFoe) = Fight();
            plain.ExecuteAttack(plainFoe);

            var (elite, _, eliteFoe) = Fight(isElite: true);
            elite.ExecuteAttack(eliteFoe);

            Assert.Greater(elite.Payout.Value.Experience, plain.Payout.Value.Experience,
                "an elite fights scaled up, so it has to pay scaled up too");
        }

        [Test]
        public void DepthScalesThePayoutToo()
        {
            // Without this, a step-40 fight costs five times the effort of a
            // step-1 one and pays exactly the same -- so the optimal way to play
            // an endless dungeon would be never to descend.
            var (shallow, _, shallowFoe) = Fight(depth: 0);
            shallow.ExecuteAttack(shallowFoe);

            var (deep, _, deepFoe) = Fight(depth: 20);
            deep.ExecuteAttack(deepFoe);

            Assert.Greater(deep.Payout.Value.Experience, shallow.Payout.Value.Experience);
        }

        [Test]
        public void TheDepthIsTheROOMS_NotWhereverTheRunHasSinceMovedTo()
        {
            // DepthStep rides the session precisely so a run advancing between
            // the killing blow and the payout cannot change what was earned.
            var (session, _, foe) = Fight(depth: 10);

            session.ExecuteAttack(foe);
            int settled = session.Payout.Value.Experience;

            session.DepthStep = 99;

            Assert.AreEqual(settled, session.Payout.Value.Experience, "a settled payout is settled");
        }

        [Test]
        public void LosingPaysNothing_AndSaysSo()
        {
            // A reduced consolation payout would be a number the player has to
            // work out is meaningless.
            var (session, hero, foe) = Fight(foeHealth: 5000, heroHealth: 1);

            // The monster's reply lands on a hero who cannot survive it.
            session.ExecuteAttack(foe);

            Assert.IsTrue(session.IsOver);
            Assert.IsFalse(session.PlayerWon);
            Assert.IsNotNull(session.Payout);
            Assert.AreEqual(0, session.Payout.Value.Experience);
            Assert.AreEqual(0, session.Payout.Value.Gold);
            Assert.IsTrue(Lines(session).Any(m => m.Contains("The party falls")));
        }

        [Test]
        public void ADefeatSettlesEvenThoughNoRiderEverRuns()
        {
            // The reason the enemy loop needs its own call: a fight that ends on
            // a monster's swing never reaches AdvanceAfterAction's exit, so
            // before this the loss settled nothing at all.
            var (session, _, foe) = Fight(foeHealth: 5000, heroHealth: 1);

            session.ExecuteAttack(foe);

            Assert.IsNotNull(session.Payout, "the defeat never settled");
        }

        [Test]
        public void TheSurvivorsCelebrateExactlyOnce()
        {
            var (session, _, foe) = Fight();

            session.ExecuteAttack(foe);

            CollectionAssert.AreEqual(new[] { "shawn" }, session.VictoryVoiceIds.ToArray());
        }

        [Test]
        public void APayoutIsNeverSettledTwice()
        {
            // A round resolves in one pass and more than one path is entitled to
            // notice the fight is over.
            var (session, _, foe) = Fight();

            session.ExecuteAttack(foe);
            int once = session.Payout.Value.Gold;

            // Every subsequent query is a no-op; nothing accumulates.
            Assert.AreEqual(once, session.Payout.Value.Gold);
            Assert.AreEqual(1, session.VictoryVoiceIds.Count);
        }
    }
}
