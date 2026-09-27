using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The round-start hook, the round limit and the enemy rally
    // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 3.2, 2.1-2.2, M3). Literal values
    // throughout: a speed-10 combatant charges at rate 1, the same rate a round
    // is measured in (SpeedScale.BaselineSpeed), so with everyone at speed 10
    // each side acts once a round.
    public class EventFightRoundsTests
    {
        private const string Shawn = "sheep";

        private static ResolvedEnemy Source(string id, int rallyPercent = 0, int rallyMax = 0,
                                            int exp = 0, int gold = 0) =>
            new ResolvedEnemy(id, id, new StatBlock(), exp, gold, false, ElementalAffinity.Neutral, 0)
            {
                RallyAttackPercentPerStack = rallyPercent,
                RallyMaxStacks = rallyMax,
            };

        private static CombatantState Foe(string name, int health = 999999, int attack = 0, int speed = 10) =>
            new CombatantState(name, false, health, 0, attack, speed) { PhysicalDefense = 0, MagicalDefense = 0 };

        private static CombatantState Hero(int health = 100000, int attack = 10, int speed = 10) =>
            new CombatantState("Shawn", true, health, 0, attack, speed) { PhysicalDefense = 0, MagicalDefense = 0 };

        // Built but NOT begun, so a test can subscribe to RoundStarted first.
        private static FightSession Build(CombatantState hero, int roundLimit,
                                          params (CombatantState state, ResolvedEnemy source)[] foes)
        {
            var encounter = new CombatEncounter(new[] { hero }, foes.Select(f => f.state));
            var kits = new List<PlayerKit> { new PlayerKit(Shawn, CharacterRole.Tank, null, null, null) };
            return new FightSession(encounter, kits, foes.Select(f => new EnemyKit(f.source, false)).ToList(),
                new SeededRandom(5))
            {
                DamageVarianceRange = 0f,
                RoundLimit = roundLimit,
            };
        }

        // Plain swings at the front enemy until `until` holds or the fight ends.
        // Every drained beat is kept, in order.
        private static List<CombatBeat> Drive(FightSession session, Func<bool> until, int cap = 400)
        {
            var beats = new List<CombatBeat>(session.DrainBeats());
            for (int i = 0; i < cap && !session.IsOver && !until(); i++)
            {
                Assert.IsTrue(session.IsPlayerTurn, "fixture: control is back with the hero between commands");
                session.ExecuteAttack(session.Encounter.FrontEnemy);
                beats.AddRange(session.DrainBeats());
            }

            return beats;
        }

        // ---- rounds ----------------------------------------------------------------

        [Test]
        public void RoundOneStartsAtBeginAndEachRoundStartsOnceInOrder()
        {
            var session = Build(Hero(), 0, (Foe("Rat"), Source("rat")));
            var started = new List<int>();
            session.RoundStarted += started.Add;

            session.Begin();
            Assert.AreEqual(1, session.Round);
            CollectionAssert.AreEqual(new[] { 1 }, started);

            Drive(session, () => session.Round >= 6);

            Assert.AreEqual(6, session.Round);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, started);
            Assert.AreEqual(FightEndReason.None, session.EndReason, "no limit, nobody down: still running");
        }

        // Speed 1 charges at 0.35, so a turn takes ~2.9 rounds and the schedule
        // crosses two rounds between some pairs of turns. Each still starts.
        [Test]
        public void SkippedRoundsEachStartInOrder()
        {
            var session = Build(Hero(speed: 1), 0, (Foe("Rat", speed: 1), Source("rat")));
            var started = new List<int>();
            var openedAtStart = new List<int>();
            var hero = session.Encounter.PlayerParty[0];
            var rat = session.Encounter.Enemies[0];
            session.RoundStarted += round =>
            {
                started.Add(round);
                openedAtStart.Add(session.OpenedTurnsOf(hero) + session.OpenedTurnsOf(rat));
            };

            session.Begin();
            Drive(session, () => session.Round >= 12);

            CollectionAssert.AreEqual(Enumerable.Range(1, session.Round).ToArray(), started,
                "every round from 1 to the current one started, once each, oldest first");

            bool caughtUp = false;
            for (int i = 1; i < openedAtStart.Count; i++)
            {
                if (openedAtStart[i] == openedAtStart[i - 1]) caughtUp = true;
            }
            Assert.IsTrue(caughtUp, "fixture: at least two rounds started with no turn opened between them");
        }

        // Plan 2.1: (a) the limit, (b) the enemy round effects, (c) the next
        // actor's turn opens. The event is raised between (b) and (c).
        [Test]
        public void TheRallyLandsBeforeTheRoundIsAnnouncedAndTheTurnOpensAfter()
        {
            var session = Build(Hero(), 0, (Foe("Bell", attack: 50), Source("bell", 8, 10)));
            var rat = session.Encounter.Enemies[0];
            var seen = new List<(int round, int stacks, CombatantState next, int openedBefore)>();
            session.RoundStarted += round =>
                seen.Add((round, session.RallyStacksOf(rat), session.Current, session.OpenedTurnsOf(session.Current)));

            session.Begin();
            Drive(session, () => session.Round >= 4);

            Assert.AreEqual(4, seen.Count);
            foreach (var s in seen)
            {
                Assert.AreEqual(s.round, s.stacks, $"round {s.round}: its stack is already on");
            }

            Assert.AreEqual(0, seen[0].openedBefore, "round 1 is announced before anyone's first turn opens");
            Assert.GreaterOrEqual(session.OpenedTurnsOf(seen[0].next), 1, "and that turn opened after it");
        }

        // ---- the round limit ---------------------------------------------------------

        [Test]
        public void TenRoundsAreSurvivedAsRoundElevenStarts()
        {
            var session = Build(Hero(), 10, (Foe("Bell"), Source("bell")));
            var started = new List<int>();
            session.RoundStarted += started.Add;
            session.Begin();

            Drive(session, () => false);

            Assert.IsTrue(session.IsOver);
            Assert.AreEqual(11, session.Round, "the fight ran into round 11 ...");
            CollectionAssert.AreEqual(Enumerable.Range(1, 10).ToArray(), started, "... and round 11 never started");
            Assert.AreEqual(FightEndReason.Survived, session.EndReason);
            Assert.IsTrue(session.PlayerWon, "surviving is a win");
            Assert.IsTrue(session.Encounter.Enemies[0].IsAlive, "with the enemy standing");
        }

        [Test]
        public void TheLimitIsCheckedBeforeTheRallySoNoEleventhStack()
        {
            var session = Build(Hero(), 3, (Foe("Bell"), Source("bell", 8, 10)));
            session.Begin();

            Drive(session, () => false);

            Assert.AreEqual(FightEndReason.Survived, session.EndReason);
            Assert.AreEqual(4, session.Round);
            Assert.AreEqual(3, session.RallyStacksOf(session.Encounter.Enemies[0]), "rounds 1-3 rallied, round 4 did not");
        }

        [Test]
        public void AKillInTheLastRoundIsDefeated()
        {
            var session = Build(Hero(), 10, (Foe("Bell"), Source("bell")));
            session.Begin();
            var bell = session.Encounter.Enemies[0];

            Drive(session, () => session.Round >= 10);
            Assert.AreEqual(10, session.Round, "fixture: the hero has a turn in round 10");
            Assert.IsFalse(session.IsOver, "fixture: round 10 is inside the limit");

            bell.CurrentHealth = 1;
            session.ExecuteAttack(bell);

            Assert.IsTrue(session.IsOver);
            Assert.AreEqual(10, session.Round);
            Assert.AreEqual(FightEndReason.Defeated, session.EndReason);
        }

        [Test]
        public void NoLimitMeansNoSurvival()
        {
            var session = Build(Hero(), 0, (Foe("Rat"), Source("rat")));
            session.Begin();

            Drive(session, () => session.Round >= 30);

            Assert.AreEqual(30, session.Round);
            Assert.IsFalse(session.IsOver);
            Assert.AreEqual(FightEndReason.None, session.EndReason);
        }

        [Test]
        public void APartyWipeIsFell()
        {
            var session = Build(Hero(health: 40), 10, (Foe("Bell", attack: 50), Source("bell")));
            session.Begin();

            Drive(session, () => false);

            Assert.AreEqual(FightEndReason.Fell, session.EndReason);
            Assert.IsFalse(session.PlayerWon);
        }

        // Plan risk R3: a win with an enemy still standing must tear down like
        // any other win -- poses, voice ids, the ledger, the payout -- and pay
        // only for what actually fell.
        [Test]
        public void ASurvivedFightWithAnEnemyStandingTearsDownCleanly()
        {
            var session = Build(Hero(), 3,
                (Foe("Pup", health: 1), Source("pup", exp: 10, gold: 4)),
                (Foe("Bell"), Source("bell", exp: 100, gold: 50)));
            var hero = session.Encounter.PlayerParty[0];
            var pup = session.Encounter.Enemies[0];
            var bell = session.Encounter.Enemies[1];
            session.Begin();

            int commands = 0;
            var beats = Drive(session, () => { commands++; return false; });

            Assert.AreEqual(FightEndReason.Survived, session.EndReason);
            Assert.IsFalse(pup.IsAlive, "fixture: the first swing killed the pup");
            Assert.IsTrue(bell.IsAlive);

            Assert.IsTrue(session.Payout.HasValue);
            Assert.AreEqual(10, session.Payout.Value.Experience, "the pup's 10, not the standing Bellwether's 100");
            Assert.AreEqual(4, session.Payout.Value.Gold);

            CollectionAssert.AreEqual(new[] { Shawn }, session.VictoryVoiceIds);
            Assert.AreEqual(FightSession.Stances.Victory, beats.Last().Stances[hero]);
            Assert.IsFalse(beats.Any(b => b.Stances.TryGetValue(bell, out var s) && s == FightSession.Stances.Defeated),
                "the standing enemy is never drawn dead");

            var messages = beats.SelectMany(b => b.Messages).ToList();
            Assert.AreEqual(1, messages.Count(m => m == "Victory!"));
            Assert.IsFalse(messages.Contains("The party falls."));

            Assert.AreEqual(1, session.Ledger.For(Shawn).Kills);
            Assert.AreEqual(0, session.Ledger.For("bell").TimesDowned);

            CollectionAssert.IsEmpty(FightInvariants.Check(session, commands));
        }

        // ---- the rally -------------------------------------------------------------------

        [Test]
        public void TheRallyAddsEightPercentPerStackAtZeroOneAndTenStacks()
        {
            var session = Build(Hero(), 0, (Foe("Bell", attack: 50), Source("bell", 8, 10)));
            var hero = session.Encounter.PlayerParty[0];
            var bell = session.Encounter.Enemies[0];

            Assert.AreEqual(0, session.RallyStacksOf(bell));
            Assert.AreEqual(50, CombatMath.ComputeAttackDamage(bell, hero), "0 stacks: the authored 50");

            session.Begin();
            Assert.AreEqual(1, session.RallyStacksOf(bell));
            Assert.AreEqual(8, bell.BonusAttackPercent);
            Assert.AreEqual(54, CombatMath.ComputeAttackDamage(bell, hero), "1 stack: 50 x 1.08");

            Drive(session, () => session.Round >= 10);
            Assert.AreEqual(10, session.Round);
            Assert.AreEqual(10, session.RallyStacksOf(bell));
            Assert.AreEqual(80, bell.BonusAttackPercent);
            Assert.AreEqual(90, CombatMath.ComputeAttackDamage(bell, hero), "10 stacks: 50 x 1.80");
        }

        [Test]
        public void TheRallyStopsAtItsCapAndTheSwingLandsWithIt()
        {
            var session = Build(Hero(), 0, (Foe("Bell", attack: 50), Source("bell", 8, 4)));
            var bell = session.Encounter.Enemies[0];
            session.Begin();

            var beats = Drive(session, () => session.Round >= 9);

            Assert.AreEqual(4, session.RallyStacksOf(bell), "capped at 4 through 9 rounds");
            Assert.AreEqual(32, bell.BonusAttackPercent);

            var swings = beats.Where(b => b.IsAction && b.Actor == bell).Select(b => b.Amount).ToList();
            CollectionAssert.AreEqual(new[] { 54, 58, 62, 66, 66, 66, 66, 66 }, swings,
                "one swing a round, each carrying its round's stacks: 50 x 1.08 .. 1.32, then held at the cap");
        }

        [Test]
        public void AMonsterWithNoRallySwingsExactlyAsBefore()
        {
            var session = Build(Hero(), 0, (Foe("Rat", attack: 30), Source("rat")));
            var rat = session.Encounter.Enemies[0];
            session.Begin();

            var beats = Drive(session, () => session.Round >= 12);

            var swings = beats.Where(b => b.IsAction && b.Actor == rat).Select(b => b.Amount).ToList();
            Assert.GreaterOrEqual(swings.Count, 10, "fixture: the rat swung every round");
            CollectionAssert.AreEqual(Enumerable.Repeat(30, swings.Count), swings);
            Assert.AreEqual(0, rat.BonusAttackPercent);
            Assert.AreEqual(0, session.RallyStacksOf(rat));
        }
    }
}
