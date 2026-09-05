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
            var hero = new CombatantState("Shawn", true, heroHealth, 30, 40, 10);
            // Speed 9 against the hero's 10: fast enough that the monster
            // genuinely replies to every action. At speed 1 it barely ever acts,
            // which quietly made the defeat cases untestable -- the hero simply
            // never got hit.
            var foe = new CombatantState("Rat", false, foeHealth, 10, 8, 9);

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
        public void TheVictoryIsSaidOutLoudButThePayoutIsNot()
        {
            // The bark used to read "20 experience, 14 gold." and the Reckoning
            // now expands seconds later saying exactly that, larger, with a
            // bar -- while the bark sits ABOVE the panel, where it is the first
            // thing the eye lands on. Two readouts of one fact, smaller one
            // first.
            //
            // What the log still owes the player is that they WON, which the
            // Reckoning never says. And the payout itself still has to be
            // computed and carried, which is what the second half asserts --
            // deleting the line must not quietly delete the number.
            var (session, _, foe) = Fight();

            session.ExecuteAttack(foe);

            var lines = Lines(session).ToList();
            Assert.IsTrue(lines.Any(m => m.Contains("Victory!")));
            Assert.IsFalse(lines.Any(m => m.Contains("experience")),
                "the bark is duplicating the Reckoning again");

            Assert.IsTrue(session.Payout.HasValue, "the payout still has to exist for the screen to show");
            Assert.AreEqual(20, session.Payout.Value.Experience);
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

        // ---- second life, level 90 of the reward track ----------------------------
        //
        // The whole mechanic is that the fight REFUSES TO END. There is no
        // re-entry, no branch in the defeat path and nothing in teardown
        // changes -- which is what makes an in-fight revive safe where a
        // run-level "continue" would not have been.

        private static (FightSession session, CombatantState hero, CombatantState foe) FatalFight(
            int charges, int heroHealth = 200)
        {
            var built = Fight(foeHealth: 5000, heroHealth: heroHealth);
            built.session.SecondLifeCharges = charges;
            return built;
        }

        [Test]
        public void WithoutACharge_TheFightEndsAsItAlwaysDid()
        {
            var (session, hero, foe) = FatalFight(charges: 0, heroHealth: 1);

            session.ExecuteAttack(foe);

            Assert.IsTrue(session.IsOver, "a fight with no second life did not end");
            Assert.IsFalse(hero.IsAlive);
            Assert.AreEqual(0, session.SecondLivesSpent);
        }

        [Test]
        public void AChargeBringsTheHeroBackOnHalfHealthAndTheFightGoesOn()
        {
            var (session, hero, foe) = FatalFight(charges: 1, heroHealth: 200);
            hero.CurrentHealth = 1;

            session.ExecuteAttack(foe);

            Assert.IsFalse(session.IsOver, "the fight ended despite a second life being available");
            Assert.IsTrue(hero.IsAlive, "the hero was not raised");
            Assert.AreEqual(100, hero.CurrentHealth, "a second life should return HALF of max health");
            Assert.AreEqual(1, session.SecondLivesSpent);
            Assert.IsNull(session.Payout, "the fight settled a payout even though it is still running");
        }

        [Test]
        public void ASecondLifeIsSpentOnlyOnce()
        {
            var (session, hero, foe) = FatalFight(charges: 1, heroHealth: 200);
            hero.CurrentHealth = 1;

            session.ExecuteAttack(foe);
            Assert.IsFalse(session.IsOver);
            Assert.AreEqual(1, session.SecondLivesSpent, "the first death did not spend the charge");

            // Down again, with nothing left to spend. Driven in a loop rather
            // than with one more swing: whether the monster gets a reply on any
            // given exchange is a turn-order detail, and what is under test is
            // that ONE charge buys exactly ONE refusal however many exchanges
            // it takes to get there.
            int guard = 0;
            while (!session.IsOver && guard++ < 20)
            {
                hero.CurrentHealth = 1;
                session.ExecuteAttack(foe);
            }

            Assert.Less(guard, 20, "the fight never ended, so a charge is being spent repeatedly");
            Assert.IsTrue(session.IsOver, "the fight refused to end a second time on one charge");
            Assert.AreEqual(1, session.SecondLivesSpent);
            Assert.IsFalse(session.PlayerWon);
        }

        // A charge must not be consumable by a fight that is over for another
        // reason -- winning, most obviously. Nobody is down, so there is
        // nothing to raise and nothing to spend.
        [Test]
        public void WinningDoesNotSpendASecondLife()
        {
            var (session, _, foe) = Fight();
            session.SecondLifeCharges = 1;

            session.ExecuteAttack(foe);

            Assert.IsTrue(session.PlayerWon);
            Assert.AreEqual(0, session.SecondLivesSpent, "a won fight spent a second life");
        }

        // Half of max, never zero. A character whose max health is 1 would come
        // back on nothing and die again immediately, spending the charge for
        // an outcome identical to not having it.
        [Test]
        public void AVeryFrailHeroStillComesBackAlive()
        {
            var (session, hero, foe) = FatalFight(charges: 1, heroHealth: 1);

            session.ExecuteAttack(foe);

            Assert.IsTrue(hero.IsAlive, "half of 1 rounded to 0 and the revive raised a corpse");
            Assert.GreaterOrEqual(hero.CurrentHealth, 1);
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
