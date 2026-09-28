using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using UnityEngine;
using UnityEngine.TestTools;

namespace PrincesPalace.PlayModeTests
{
    // THE REAL bell_in_the_fog THROUGH THE REAL RUN
    // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.4, M7a's "done when"): every
    // page walked by RunOrchestrator.ChooseEventOption, the Bellwether fight
    // built through the seam the screen and the bot share
    // (RunOrchestrator.BuildFight), played to each of its three ends
    // (Endure, Break, Fall) and settled; the room cleared by the event's own
    // Leave; the Bellwether kept out of room rolls; and the balance bot's
    // first-available walk through the Bell ending with the room cleared.
    //
    // The page graph and every field are pinned on the fast host
    // (BellInTheFogEventTests); this file pins what only the run can show.
    // Fights end by playing plain swings (Drive), with one side made
    // unkillable or one-hit so the ending is the one under test rather than a
    // coin toss over the Bellwether's first-guess stats (M8a tunes those).
    //
    // NEEDS BUILT CONTENT: bell_in_the_fog and bellwether must be in
    // Resources/Content.
    public class BellInTheFogRunTests
    {
        private const string Bell = "bell_in_the_fog";

        // Row positions (BellInTheFogEventTests pins the texts in this order).
        private const int Touch = 0;
        private const int Ask = 1;
        private const int WalkAway = 2;
        private const int ListenAgain = 0;
        private const int LeaveRow = 0;

        private const int BellExp = 80;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-bell-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            SaveData.TestSquadOfThreeEnabled = true;
        }

        [TearDown]
        public void Restore()
        {
            SaveData.TestSquadOfThreeEnabled = null;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            ContentDatabase.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;
        private static RunSnapshot Run => RunManager.Run;

        // ---- fixture ----------------------------------------------------------------

        private static void OpenTheBell(params (string id, int hp)[] health)
        {
            RunManager.StartRun(29UL);
            CollectionAssert.AreEquivalent(new[] { "sheep", "bear", "owl" }, Save.ActiveSquadIds(),
                "fixture: the squad of three is Shawn, Bjorn and Odette");
            Run.relicIds.Clear();
            foreach (var (id, hp) in health) SetHp(id, hp);

            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(Bell), "fixture: bell_in_the_fog is not in the built content");
            Assert.AreEqual("bell", Run.eventPageId);
        }

        private static void SetHp(string id, int hp)
        {
            Run.currentHealth ??= new List<RunHealthEntry>();
            Run.currentHealth.RemoveAll(e => e.characterId == id);
            Run.currentHealth.Add(new RunHealthEntry { characterId = id, hp = hp });
        }

        private static int HpOf(string id) =>
            Run.currentHealth.FirstOrDefault(e => e.characterId == id)?.hp ?? -1;

        private static Character Member(string id) => Save.ActiveSquad().First(c => c.definitionId == id);

        // Shawn parked at level 10 with no exp banked, where 80 exp cannot
        // buy a level, so "Shawn got 80" reads straight off his exp. A fresh
        // level-1 Shawn levels up on it and banks the remainder, which would
        // make the check restate the level curve.
        private static void ParkShawnAtLevelTen()
        {
            Member("sheep").level = 10;
            Member("sheep").exp = 0;
        }

        private static void AssertShawnGotTheBellsExp()
        {
            Assert.AreEqual(10, Member("sheep").level, "fixture: 80 exp bought a level after all");
            Assert.AreEqual(BellExp, Member("sheep").exp);
        }

        private static EventChoiceResult Pick(int index)
        {
            var picked = EventPicks.OnCurrentPage(index);
            Assert.AreEqual(EventChoiceOutcome.Ok, picked.Outcome, $"the pick was refused: {picked.Reason}");
            return picked;
        }

        private static FightSession TouchTheBell()
        {
            Pick(Touch);
            Assert.IsTrue(RunOrchestrator.EventFightPending, "touching the bell left no fight pending");

            var built = RunOrchestrator.BuildFight();
            Assert.IsNotNull(built, "the Bellwether fight did not build");
            CollectionAssert.AreEqual(new[] { "sheep" }, built.PartyIds, "Shawn fights the Bellwether alone");
            CollectionAssert.AreEqual(new[] { "bellwether" },
                built.Session.Encounter.Enemies.Select(e => built.Session.SourceFor(e).Source.Id).ToArray());
            Assert.AreEqual(10, built.Session.RoundLimit);
            Assert.AreEqual(0, built.Session.SecondLifeCharges, "a wake fight fields no second lives");
            return built.Session;
        }

        // Plain swings at the Bellwether until the fight ends.
        private static void Drive(FightSession session)
        {
            session.Begin();
            session.DrainBeats();
            for (int i = 0; i < 400 && !session.IsOver; i++)
            {
                Assert.IsTrue(session.IsPlayerTurn, "fixture: control is back with Shawn between commands");
                session.ExecuteAttack(session.Encounter.FrontEnemy);
                session.DrainBeats();
            }

            Assert.IsTrue(session.IsOver, "fixture: the fight did not end in 400 swings");
        }

        private static void Pad(CombatantState combatant, int health)
        {
            combatant.MaxHealth = health;
            combatant.CurrentHealth = health;
        }

        private static CombatantState Shawn(FightSession session) => session.Encounter.PlayerParty.Single();

        private static CombatantState Bellwether(FightSession session) => session.Encounter.Enemies.Single();

        // ---- the pages --------------------------------------------------------------

        [Test]
        public void EveryAskPage_IsReachedByWhoIsStanding_AndListensAgainBackToTheBell()
        {
            OpenTheBell();

            var routes = new (string page, (string id, int hp)[] health)[]
            {
                ("ask_both", new (string, int)[0]),
                ("ask_bjorn", new[] { ("owl", 0) }),
                ("ask_odette", new[] { ("owl", 50), ("bear", 0) }),
                ("ask_none", new[] { ("owl", 0), ("bear", 0) }),
            };

            foreach (var (page, health) in routes)
            {
                foreach (var (id, hp) in health) SetHp(id, hp);

                Assert.IsFalse(Pick(Ask).Closed);
                Assert.AreEqual(page, Run.eventPageId);
                Assert.IsTrue(RunOrchestrator.CurrentEvent().Lines.Count > 0, $"{page}: plays no lines");

                Assert.IsFalse(Pick(ListenAgain).Closed);
                Assert.AreEqual("bell", Run.eventPageId, $"{page}: Listen again did not return to the bell");
            }

            CollectionAssert.IsEmpty(Run.relicIds, "asking grants nothing");
            CollectionAssert.DoesNotContain(Run.eventsSeen, Bell, "asking does not finish the event");
        }

        [Test]
        public void WalkingAway_ClearsTheRoom_AndLeavesTheBellToReturn()
        {
            OpenTheBell();
            int node = Run.currentNodeId;

            Pick(WalkAway);
            var view = RunOrchestrator.CurrentEvent();
            Assert.IsNotNull(view, "Walk away has a result to read before Leave");
            Assert.IsTrue(view.Concluded);

            RunOrchestrator.LeaveEvent();

            CollectionAssert.Contains(Run.clearedNodeIds, node);
            CollectionAssert.DoesNotContain(Run.eventsSeen, Bell, "Walk away must leave the Bell eligible to return");
            Assert.IsFalse(RunOrchestrator.EventIsOpen);
        }

        // ---- the three ends ---------------------------------------------------------

        [Test]
        public void Endure_TenTollsSurvived_GrantsTheToll_AndExp_ThenTheOthersSayASecondPassed()
        {
            OpenTheBell(("bear", 17));
            int node = Run.currentNodeId;
            ParkShawnAtLevelTen();
            int bearExp = Member("bear").exp;

            var session = TouchTheBell();
            Pad(Shawn(session), 1_000_000);
            Pad(Bellwether(session), 1_000_000);
            Drive(session);
            Assert.AreEqual(FightEndReason.Survived, session.EndReason);

            var settled = RunOrchestrator.SettleFight(session, session.PlayerWon);

            Assert.IsNull(settled.Reward, "pays: false opens no Reckoning");
            Assert.AreEqual("endure", Run.eventPageId);
            CollectionAssert.AreEqual(new[] { "toll_of_the_flock" }, Run.relicIds);
            AssertShawnGotTheBellsExp();
            Assert.AreEqual(bearExp, Member("bear").exp, "the exp is Shawn's alone");
            Assert.AreEqual(17, HpOf("bear"), "Bjorn sat out untouched");
            CollectionAssert.Contains(Run.eventsSeen, Bell, "Endure finishes the Bell");
            CollectionAssert.DoesNotContain(Run.clearedNodeIds, node, "the fight does not clear the room");

            // Bjorn is standing (17 HP) and Odette full: both speak.
            Pick(LeaveRow);
            var view = RunOrchestrator.CurrentEvent();
            Assert.IsTrue(view.Concluded);
            StringAssert.Contains("Bjorn", view.ResultText);
            StringAssert.Contains("Odette", view.ResultText);

            RunOrchestrator.LeaveEvent();
            CollectionAssert.Contains(Run.clearedNodeIds, node, "the event's Leave clears the room");
        }

        [Test]
        public void Break_TheBellwetherFalls_GrantsBothRelics_AndTheSameExp()
        {
            OpenTheBell();
            int node = Run.currentNodeId;
            ParkShawnAtLevelTen();

            var session = TouchTheBell();
            Pad(Shawn(session), 1_000_000);
            Bellwether(session).CurrentHealth = 1;
            Drive(session);
            Assert.AreEqual(FightEndReason.Defeated, session.EndReason);

            var settled = RunOrchestrator.SettleFight(session, session.PlayerWon);

            Assert.IsNull(settled.Reward);
            Assert.AreEqual("break", Run.eventPageId);
            CollectionAssert.AreEqual(new[] { "toll_of_the_flock", "bellwethers_bell" }, Run.relicIds);
            AssertShawnGotTheBellsExp();
            CollectionAssert.Contains(Run.eventsSeen, Bell);

            Assert.IsTrue(Pick(LeaveRow).Closed, "the break page's Leave is silent and closes at once");
            CollectionAssert.Contains(Run.clearedNodeIds, node);
        }

        [Test]
        public void Fall_ShawnWakesAtOneHp_WithNothing_AndTheBellIsGone()
        {
            OpenTheBell();
            int node = Run.currentNodeId;
            int sheepExp = Member("sheep").exp;

            var session = TouchTheBell();
            Pad(Bellwether(session), 1_000_000);
            Shawn(session).CurrentHealth = 1;
            Drive(session);
            Assert.AreEqual(FightEndReason.Fell, session.EndReason);

            var settled = RunOrchestrator.SettleFight(session, session.PlayerWon);

            Assert.IsNull(settled.RunEnded, "a wake loss opens no defeat screen");
            Assert.IsTrue(RunManager.HasRun, "a wake loss ends nothing");
            Assert.AreEqual(1, HpOf("sheep"), "the wake stands Shawn up at 1 HP");
            CollectionAssert.IsEmpty(Run.relicIds);
            Assert.AreEqual(sheepExp, Member("sheep").exp);
            CollectionAssert.Contains(Run.eventsSeen, Bell, "Fall finishes the Bell too: the stump is empty");

            var view = RunOrchestrator.CurrentEvent();
            Assert.IsTrue(view.Concluded, "Fall leaves on its result");
            Assert.AreNotEqual("", view.ResultText);

            RunOrchestrator.LeaveEvent();
            CollectionAssert.Contains(Run.clearedNodeIds, node);
        }

        // ---- never rolled -----------------------------------------------------------

        [Test]
        public void TheBellwether_NeverAppearsInRoomRolls()
        {
            RunManager.StartRun(47UL);
            CollectionAssert.DoesNotContain(RunEncounter.Pool().Select(c => c.Id).ToList(), "bellwether");

            foreach (var type in new[] { Domain.Dungeon.RoomType.Fight, Domain.Dungeon.RoomType.EliteFight })
            {
                for (int i = 0; i < 200; i++)
                {
                    Run.step = 1 + i % 40;
                    Run.currentNodeId = i;
                    var roster = RunEncounter.For(Save, Run, type);
                    CollectionAssert.DoesNotContain(roster.EnemyIds, "bellwether", $"{type} roll {i}");
                }
            }
        }

        // ---- the bot ----------------------------------------------------------------

        // THE BOT'S OWN LOOP, whole shallow runs, with the Bell the only event
        // in the catalogue, so the first Event room of each run is the Bell
        // (Shawn starts standing). Its first-available path touches the bell
        // (the first row), plays the Bellwether through PlayTheFight, and
        // leaves on the result. VisitEvent records a hit for a trapped event,
        // a refused choice, the choice cap and a room left uncleared, so no
        // hits IS "terminated with the room cleared". Three runs that reach an
        // Event room, not one: a later Event room in the same run finds the
        // finished Bell gone and an empty pool, which must clear as well.
        [Test]
        public void TheBotsFirstAvailablePath_ThroughTheBell_EndsWithTheRoomCleared()
        {
            const int DepthCap = 6;
            var events = (List<EventDefinition>)ContentDatabase.Events;
            events.RemoveAll(e => e.Data.Id != Bell);
            Assert.AreEqual(1, events.Count, "fixture: the Bell is not in the built content");

            int runs = 0;
            for (ulong seed = 1; seed <= 200UL && runs < 3; seed++)
            {
                var result = BotRunDriver.PlayRun(seed, "RandomLegal", ProfilePresets.Fresh, DepthCap);
                int here = result.Trace.Rooms.Count(r => r.RoomType == "Event");
                if (here == 0) continue;

                runs++;
                var hits = result.Hits.Where(h => h.Name != "StalledEnemyTurn").ToList();
                Assert.IsEmpty(hits, $"seed {seed}: " + string.Join(" | ", hits.Select(h => h.ToString())));
                Assert.IsTrue(result.Trace.Capped || result.Trace.DeathStep > 0, $"seed {seed} did not terminate cleanly");
                Debug.Log($"[Bell bot] seed {seed}: {here} Event room(s); relics at end: " +
                          string.Join(",", result.RelicIdsAtEnd));
            }

            Assert.AreEqual(3, runs, $"only {runs} run(s) over seeds 1-200 reached an Event room within {DepthCap} steps");
        }
    }
}
