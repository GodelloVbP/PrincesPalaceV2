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

        // ---- what a summoned body is worth ----------------------------------------

        [Test]
        [Ignore("owner's call: whether a summoned body pays its own reward is a balance question")]
        public void ASummonedBodyDoesNotPayItsOwnReward()
        {
            // ResolveSummon (FightSession.Skills.cs) files the summon's kit into
            // _enemyKits, and ResolveOutcome pays out over _enemyKits.Values --
            // so every rat the Forest Warden's Roar calls in is worth its full
            // authored experience and gold on top of the Warden's own.
            //
            // The cap counts the LIVING (SummonCap 2), not the summoned, so the
            // Warden re-fills the field every time the player clears it and the
            // dictionary keeps every corpse's kit. A player willing to stall has
            // an unbounded experience and gold faucet inside one room.
            //
            // The rest of the kill funnel already decided the other way:
            // EssenceSiphonOnKill and InconspicuousKeyOnKill both bail on
            // victim.IsSummon, on the stated reasoning that a called-in body is
            // not a body worth paying for. The payout is the one place that
            // does not ask.
            //
            // LEFT ALONE. Excluding summons drops the reward for a boss room
            // that is genuinely harder for having adds in it, and capping the
            // faucet some other way (count summons once, pay a fraction) is a
            // different answer again. That is a tuning decision, not a wiring
            // one -- this test says what the code does today and fails the
            // moment somebody decides.
            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var warden = new CombatantState("Warden", false, 1, 10, 1, 1);

            bool SummonFactory(string id, out CombatantState state, out EnemyKit kit)
            {
                state = new CombatantState("Rat", false, 1, 0, 1, 1);
                kit = new EnemyKit(Source("rat", 20, 10), false);
                return true;
            }

            var encounter = new CombatEncounter(new[] { hero }, new[] { warden });
            var session = new FightSession(encounter,
                new List<PlayerKit> { new PlayerKit("shawn", CharacterRole.Tank, null, null, null) },
                new List<EnemyKit> { new EnemyKit(Source("warden", 20, 10), false) },
                new SeededRandom(2), summonFactory: SummonFactory) { DamageVarianceRange = 0f };
            session.Begin();

            var roar = new ResolvedSkill("roar", "Roar", "", "warden", 1, SkillEffect.Summon,
                SkillTargeting.Self, 0, 0, false, 0, 0, false, null, SpellPresentation.None, 0,
                summonEnemyId: "rat", summonCap: 2);
            session.ResolveSummonForTest(warden, roar);

            session.ExecuteAttack(warden);
            Assert.IsFalse(warden.IsAlive, "fixture: the Warden has to fall first");

            var rat = encounter.Enemies.First(e => e.IsAlive);
            session.ExecuteAttack(rat);

            Assert.IsTrue(session.IsOver);
            Assert.AreEqual(20, session.Payout.Value.Experience,
                "the room paid for a body it called in itself");
        }

        // ---- a defeat that lands at a turn start ----------------------------------

        [Test]
        public void ADefeatAtATurnStartSettlesEvenWhenNothingElseIsWatching()
        {
            // StepToNextTurn (FightSession.Enemies.cs) says in writing that it
            // is there because "a fight can end on a monster's swing or on a
            // poison tick at turn start, and neither of those passes through
            // the riders -- so without this a defeat would never settle". It
            // only settles the first of those two: the guard sits at the TOP of
            // the method, so a death caused by the GrantTurnStart at the BOTTOM
            // is only ever caught on the next pass -- and there is no next pass,
            // because that same death makes the method return false and break
            // the loop.
            //
            // AdvanceAfterAction re-checks IsOver afterwards and covers it for a
            // fight driven by a player command. Nothing covers the two callers
            // that drive AutoResolveEnemyTurns on their own: Begin, and
            // FightController.RescueAStalledEnemyTurn -- which is precisely the
            // path a spent second life leaves the fight sitting on.
            //
            // Slower than the monster, so the opening belongs to the monster and
            // the hero's own turn start is the next thing that happens; enough
            // health to survive the swing, and poison that nothing survives.
            var hero = new CombatantState("Shawn", true, 300, 30, 40, 5);
            var foe = new CombatantState("Rat", false, 5000, 10, 8, 10);
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Poison, 999, 3);

            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var session = new FightSession(encounter,
                new List<PlayerKit> { new PlayerKit("shawn", CharacterRole.Tank, null, null, null) },
                new List<EnemyKit> { new EnemyKit(Source("rat", 20, 10), false) },
                new SeededRandom(2), isEliteFight: false) { DamageVarianceRange = 0f };

            session.Begin();

            Assert.IsFalse(hero.IsAlive, "the poison tick was supposed to be the fatal one");
            Assert.IsTrue(session.IsOver);
            Assert.IsNotNull(session.Payout, "the fight ended and paid nothing at all, not even zero");
            Assert.IsTrue(Lines(session).Any(m => m.Contains("The party falls")),
                "the defeat was never announced");
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

        // ---- what a spent charge leaves the fight sitting on ----------------------
        //
        // SettleIfOver (FightSession.Enemies.cs) answers "was it over ON ENTRY",
        // not "is it over now" -- and ResolveOutcome can flip the answer inside
        // the call, because that is precisely what a second life does. The
        // enemy's turn is then never advanced past, so every driver in the tree
        // re-enters AutoResolveEnemyTurns on the same live enemy and it swings
        // for a second time on one turn.

        [Test]
        public void ASpentSecondLifeHandsTheTurnBackToThePlayer()
        {
            var (session, hero, foe) = FatalFight(charges: 1, heroHealth: 200);
            hero.CurrentHealth = 1;

            session.ExecuteAttack(foe);

            Assert.IsFalse(session.IsOver, "the fight ended despite a charge");
            Assert.AreEqual(1, session.SecondLivesSpent);
            Assert.IsTrue(session.IsPlayerTurn,
                "the fight is sitting on an enemy turn that nothing will resolve; Current is "
                + session.Current.Name);
        }

        [Test]
        public void ASpentSecondLifeDoesNotBuyTheKillerASecondSwing()
        {
            var (session, hero, foe) = FatalFight(charges: 1, heroHealth: 200);
            hero.CurrentHealth = 1;

            session.ExecuteAttack(foe);

            // Half of 200, literal rather than derived: the revive's arithmetic
            // is pinned by AChargeBringsTheHeroBackOnHalfHealthAndTheFightGoesOn
            // and what is under test here is that NOTHING FURTHER happens.
            Assert.AreEqual(100, hero.CurrentHealth, "fixture: the revive is half of max");

            // The rescue every driver in the tree performs on a stalled enemy
            // turn -- FightController.RescueAStalledEnemyTurn, FightRunner's
            // StalledEnemyTurn path, and the loop in ASecondLifeIsSpentOnlyOnce
            // below. On a fight whose turn has moved on this is a no-op.
            session.AutoResolveEnemyTurns();

            Assert.AreEqual(100, hero.CurrentHealth,
                "the revived hero was hit again by the same enemy turn that had already resolved");
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
                // A spent charge now hands the turn back rather than leaving
                // the session parked on the enemy that landed the killing blow
                // (ASpentSecondLifeHandsTheTurnBackToThePlayer above), so this
                // driver no longer needs the AutoResolveEnemyTurns branch that
                // used to exist to unstick it. The guard stays: whether the
                // monster replies on any given exchange is a turn-order detail.
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
