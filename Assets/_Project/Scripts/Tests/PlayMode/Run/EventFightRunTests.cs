using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    // M2 OF docs/PLAN_EVENTS_BELL_AND_CARAVAN.md, THROUGH THE REAL RUN:
    // returning events, the encounter request seam, and how an event fight
    // settles (wake / endRun, pays, the result outcome, the room left
    // uncleared), plus rollable:false and save 7.
    //
    // FIXTURE EVENTS AND ENEMIES, not the Bell: its content is M7a. The
    // fixtures ride the loaded catalogue for one test (FixtureEvents; the
    // enemy clone below) and TearDown reloads it. Fights are built through
    // RunOrchestrator.BuildFight -- the seam both callers use -- and ended
    // either by playing plain swings (Drive) where the session's own ending
    // matters (Survived, a paid win), or by settling directly with `won`
    // where only the settlement's branch is under test. Expected values are
    // literals (CLAUDE.md gotcha 5), except the paid gold, which is read off
    // the session's own payout as FightSettlementTests does.
    public class EventFightRunTests
    {
        private const string Returning = "m2_returning";
        private const string Fights = "m2_fights";

        // Choice indices on the fight fixture's `gate` page.
        private const int Duel = 0;       // wake, override [sheep], 2 rounds, pays false
        private const int Brawl = 1;      // endRun, normal party, pays true
        private const int QuietBrawl = 2; // endRun, normal party, pays false

        private const int DuelExp = 7;
        private const int BenchedHp = 17;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-event-fight-" + Guid.NewGuid().ToString("N"));
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
            LogAssert.ignoreFailingMessages = false;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            ContentDatabase.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;
        private static RunSnapshot Run => RunManager.Run;

        // ---- fixtures -----------------------------------------------------------------

        private static RawEventOutcome Leave(string result = "", params RawEventEffect[] effects) =>
            new RawEventOutcome { goTo = "Leave", result = result, effects = effects };

        private static RawEventOutcome GoTo(string page, string result, params RawEventEffect[] effects) =>
            new RawEventOutcome { goTo = page, result = result, effects = effects };

        private static RawEventEffect Finish() => new RawEventEffect { kind = "finish" };

        private static RawEventChoice Starts(string text, string fightId) =>
            new RawEventChoice
            {
                text = text,
                outcomes = new[] { new RawEventOutcome { effects = new[] { new RawEventEffect { kind = "fight", fight = fightId } } } },
            };

        private static RawEventPage LeavePage(string id) =>
            new RawEventPage
            {
                id = id, title = id, body = "B",
                choices = new[] { new RawEventChoice { text = "Leave", outcomes = new[] { Leave() } } },
            };

        private static RawEventEntry ReturningFixture(int floor) =>
            new RawEventEntry
            {
                id = Returning,
                mayReturn = true,
                floors = new[] { floor },
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "start", title = "T", body = "B",
                        choices = new[]
                        {
                            new RawEventChoice { text = "Walk away", outcomes = new[] { Leave() } },
                            new RawEventChoice { text = "Be done", outcomes = new[] { Leave("", Finish()) } },
                        },
                    },
                },
            };

        private static RawEventEntry FightFixture() =>
            new RawEventEntry
            {
                id = Fights,
                mayReturn = true,
                floors = new[] { 999 },
                pages = new[]
                {
                    new RawEventPage
                    {
                        id = "gate", title = "Gate", body = "B",
                        choices = new[]
                        {
                            Starts("Duel", "duel"),
                            Starts("Brawl", "brawl"),
                            Starts("Quiet brawl", "quiet_brawl"),
                            new RawEventChoice { text = "Walk away", outcomes = new[] { Leave() } },
                        },
                    },
                    LeavePage("won"),
                    LeavePage("endured"),
                },
                fights = new[]
                {
                    new RawEventFight
                    {
                        id = "duel", enemies = new[] { "rat" }, party = new[] { "sheep" },
                        surviveRounds = 2, roundLabel = "Toll", onLoss = "wake", pays = false,
                        onDefeated = GoTo("won", "Broke it.",
                            new RawEventEffect { kind = "exp", amount = DuelExp, character = "sheep" }, Finish()),
                        onSurvived = GoTo("endured", "Endured.", Finish()),
                        onFell = Leave("Fell."),
                    },
                    new RawEventFight
                    {
                        id = "brawl", enemies = new[] { "rat" },
                        onDefeated = GoTo("won", "Won the brawl."),
                    },
                    new RawEventFight
                    {
                        id = "quiet_brawl", enemies = new[] { "rat" }, pays = false,
                        onDefeated = GoTo("won", "Won quietly."),
                    },
                },
            };

        // A run standing on its entry node with the fight fixture open there,
        // Bjorn carrying 17 HP and Odette full (no entry).
        private static void OpenFightFixture()
        {
            FixtureEvents.Append(FightFixture());
            RunManager.StartRun(21UL);

            var squad = Save.ActiveSquadIds();
            Assert.That(squad, Does.Contain("sheep").And.Contain("bear").And.Contain("owl"),
                "fixture: the squad of three is Shawn, Bjorn and Odette");

            Run.currentHealth ??= new List<RunHealthEntry>();
            Run.currentHealth.RemoveAll(e => e.characterId == "bear" || e.characterId == "owl");
            Run.currentHealth.Add(new RunHealthEntry { characterId = "bear", hp = BenchedHp });

            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(Fights), "fixture: the event did not open");
        }

        private static FightEncounterAdapter.BuiltFight StartAndBuild(int choice)
        {
            var picked = EventPicks.OnCurrentPage(choice);
            Assert.AreEqual(EventChoiceOutcome.Ok, picked.Outcome, $"the pick was refused: {picked.Reason}");
            Assert.IsTrue(RunOrchestrator.EventFightPending, "the pick left no fight pending");

            var built = RunOrchestrator.BuildFight();
            Assert.IsNotNull(built, "the event fight did not build");
            return built;
        }

        private static int HpOf(string id) =>
            Run.currentHealth.FirstOrDefault(e => e.characterId == id)?.hp ?? -1;

        private static Character Member(string id) => Save.ActiveSquad().First(c => c.definitionId == id);

        // Plain swings at the front enemy until the fight ends.
        private static void Drive(FightSession session)
        {
            session.Begin();
            session.DrainBeats();
            for (int i = 0; i < 400 && !session.IsOver; i++)
            {
                Assert.IsTrue(session.IsPlayerTurn, "fixture: control is back with the party between commands");
                session.ExecuteAttack(session.Encounter.FrontEnemy);
                session.DrainBeats();
            }

            Assert.IsTrue(session.IsOver, "fixture: the fight did not end in 400 swings");
        }

        private static void MakeTheHeroesUnkillable(FightSession session)
        {
            foreach (var hero in session.Encounter.PlayerParty)
            {
                hero.MaxHealth = 1_000_000;
                hero.CurrentHealth = 1_000_000;
            }
        }

        // ---- 1. returning events -----------------------------------------------------

        [Test]
        public void AReturningEvent_RollsAgainAfterWalkAway_ThreeTimesInARow_AndNeverAfterFinish()
        {
            RunManager.StartRun(33UL);
            FixtureEvents.Append(ReturningFixture(Run.floor));

            // Every authored event is already seen, so the fixture is the pool.
            Run.eventsSeen.AddRange(ContentDatabase.Events.Select(e => e.Data.Id).Where(id => id != Returning));

            for (int visit = 1; visit <= 3; visit++)
            {
                Assert.IsTrue(RunOrchestrator.EnsureEvent(), $"visit {visit}: no event rolled");
                Assert.AreEqual(Returning, Run.eventId, $"visit {visit}");
                CollectionAssert.DoesNotContain(Run.eventsSeen, Returning, $"visit {visit}: opening marked it seen");

                var walked = EventPicks.OnCurrentPage(0);
                Assert.IsTrue(walked.Closed, $"visit {visit}: Walk away did not close the event");
                CollectionAssert.DoesNotContain(Run.eventsSeen, Returning, $"visit {visit}: Walk away marked it seen");
            }

            Assert.IsTrue(RunOrchestrator.EnsureEvent());
            Assert.IsTrue(EventPicks.OnCurrentPage(1).Closed, "Be done did not close the event");
            CollectionAssert.Contains(Run.eventsSeen, Returning, "finish did not mark it seen");

            Assert.IsFalse(RunOrchestrator.EnsureEvent(), "a finished returning event rolled again");
            Assert.AreEqual("", Run.eventId);
        }

        // ---- 2. the request ---------------------------------------------------------

        [Test]
        public void TheOverrideFightFieldsOnlyItsParty_WithItsEnemies_RoundLimit_AndNoSecondLives()
        {
            OpenFightFixture();
            var built = StartAndBuild(Duel);

            var request = RunOrchestrator.CurrentEncounterRequest();
            Assert.IsTrue(request.IsEventFight);
            Assert.AreEqual("duel", request.EventFight.Id);

            CollectionAssert.AreEqual(new[] { "sheep" }, built.PartyIds);
            CollectionAssert.AreEqual(new[] { "rat" },
                built.Session.Encounter.Enemies.Select(e => built.Session.SourceFor(e).Source.Id).ToArray());
            Assert.AreEqual(2, built.Session.RoundLimit);
            Assert.AreEqual(0, built.Session.SecondLifeCharges, "a wake fight fields no second lives");
            Assert.AreEqual("gate", Run.eventPageId, "the event stays on the page that launched the fight");
        }

        [Test]
        public void AChoiceIsRefusedWhileItsFightIsPending()
        {
            OpenFightFixture();
            StartAndBuild(Duel);

            var again = EventPicks.OnCurrentPage(Brawl);
            Assert.AreEqual(EventRefusal.FightPending, again.Reason);
            Assert.AreEqual("duel", Run.pendingFight);
        }

        [Test]
        public void AFightWhoseOverrideHasNobodyStanding_IsRefusedAndStartsNothing()
        {
            OpenFightFixture();
            Run.currentHealth.RemoveAll(e => e.characterId == "sheep");
            Run.currentHealth.Add(new RunHealthEntry { characterId = "sheep", hp = 0 });

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("nobody in its party is standing"));
            var picked = EventPicks.OnCurrentPage(Duel);

            Assert.AreEqual(EventRefusal.NoFighters, picked.Reason);
            Assert.AreEqual("", Run.pendingFight);
            Assert.IsFalse(RunOrchestrator.EventFightPending);
        }

        // ---- 3. settling ------------------------------------------------------------

        [Test]
        public void AWonOverrideFight_PaysItsOutcome_LeavesTheBenchedUntouched_AndTheRoomUncleared()
        {
            OpenFightFixture();
            int bearExp = Member("bear").exp, owlExp = Member("owl").exp, sheepExp = Member("sheep").exp;
            int bearLevel = Member("bear").level, owlLevel = Member("owl").level;
            int gold = Run.gold, roomsCleared = Run.roomsCleared, step = Run.step, legStart = Run.legStartStep;
            int node = Run.currentNodeId;

            var built = StartAndBuild(Duel);
            var settled = RunOrchestrator.SettleFight(built.Session, won: true);

            Assert.IsNull(settled.Reward, "pays: false settled a reward, which would open a Reckoning");
            Assert.IsNull(settled.RunEnded);
            Assert.IsTrue(RunManager.HasRun);

            // The event moved on to onDefeated, applied through the effect code.
            Assert.AreEqual(Fights, Run.eventId, "the event closed");
            Assert.AreEqual("won", Run.eventPageId);
            Assert.AreEqual("Broke it.", Run.eventResult);
            Assert.AreEqual("", Run.pendingFight, "the request was not cleared");
            CollectionAssert.Contains(Run.eventsSeen, Fights, "the result's finish did not apply");
            Assert.AreEqual(sheepExp + DuelExp, Member("sheep").exp, "exp.character did not reach Shawn alone");

            // The benched: HP entry, absence of one, and XP untouched.
            Assert.AreEqual(BenchedHp, HpOf("bear"));
            Assert.AreEqual(-1, HpOf("owl"), "Odette gained a health entry from a fight she sat out");
            Assert.AreEqual(bearExp, Member("bear").exp);
            Assert.AreEqual(owlExp, Member("owl").exp);
            Assert.AreEqual(bearLevel, Member("bear").level);
            Assert.AreEqual(owlLevel, Member("owl").level);

            // No room cleared, no leg advanced, nothing paid.
            CollectionAssert.DoesNotContain(Run.clearedNodeIds, node);
            Assert.AreEqual(roomsCleared, Run.roomsCleared);
            Assert.AreEqual(step, Run.step);
            Assert.AreEqual(legStart, Run.legStartStep);
            Assert.AreEqual(gold, Run.gold);

            // Persisted: the result survives a reload.
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Assert.AreEqual("won", Run.eventPageId);
            Assert.AreEqual("", Run.pendingFight);
        }

        [Test]
        public void AWakeLoss_StandsTheFallenAtOneHp_AndTheRunContinuesOnOnFell()
        {
            OpenFightFixture();
            var built = StartAndBuild(Duel);
            foreach (var hero in built.Session.Encounter.PlayerParty) hero.CurrentHealth = 0;

            var settled = RunOrchestrator.SettleFight(built.Session, won: false);

            Assert.IsTrue(RunManager.HasRun, "a wake loss ended the run");
            Assert.IsNull(settled.RunEnded, "a wake loss published a settlement, which opens the defeat screen");
            Assert.IsNull(settled.Reward);
            Assert.AreEqual(1, HpOf("sheep"));
            Assert.AreEqual(BenchedHp, HpOf("bear"), "the wake touched someone who was not fielded");
            Assert.AreEqual(Fights, Run.eventId);
            Assert.AreEqual("", Run.eventPageId, "onFell leaves: the event concludes on its result");
            Assert.AreEqual("Fell.", Run.eventResult);
            CollectionAssert.DoesNotContain(Run.eventsSeen, Fights, "onFell carries no finish here");
        }

        [Test]
        public void AnEndRunLoss_EndsTheRun()
        {
            OpenFightFixture();
            var built = StartAndBuild(Brawl);

            var settled = RunOrchestrator.SettleFight(built.Session, won: false);

            Assert.IsFalse(RunManager.HasRun, "an endRun event fight left the run standing");
            Assert.IsNotNull(settled.RunEnded, "the defeat screen has nothing to report");
        }

        [Test]
        public void SurvivingTheRoundLimit_SettlesOnSurvived()
        {
            OpenFightFixture();
            var built = StartAndBuild(Duel);
            var session = built.Session;
            MakeTheHeroesUnkillable(session);
            foreach (var enemy in session.Encounter.Enemies)
            {
                enemy.MaxHealth = 1_000_000;
                enemy.CurrentHealth = 1_000_000;
            }

            Drive(session);
            Assert.AreEqual(FightEndReason.Survived, session.EndReason, "fixture: the fight did not end on its limit");

            var settled = RunOrchestrator.SettleFight(session, session.PlayerWon);

            Assert.IsNull(settled.Reward, "a Survived pays:false fight opened a Reckoning");
            Assert.AreEqual("endured", Run.eventPageId);
            Assert.AreEqual("Endured.", Run.eventResult);
        }

        [Test]
        public void AWonPayingEventFight_PaysLikeARoom_ButLeavesTheRoomAndLegAlone()
        {
            OpenFightFixture();
            int gold = Run.gold, roomsCleared = Run.roomsCleared, legStart = Run.legStartStep;
            int node = Run.currentNodeId;

            var built = StartAndBuild(Brawl);
            var session = built.Session;
            MakeTheHeroesUnkillable(session);
            foreach (var enemy in session.Encounter.Enemies) enemy.CurrentHealth = 1;

            Drive(session);
            Assert.AreEqual(FightEndReason.Defeated, session.EndReason);
            Assert.IsTrue(session.Payout.HasValue, "fixture: a won fight with no payout");

            var settled = RunOrchestrator.SettleFight(session, won: true);

            Assert.IsNotNull(settled.Reward, "pays: true settled no reward, so no Reckoning");
            Assert.AreEqual(gold + session.Payout.Value.Gold, Run.gold);
            Assert.AreEqual("won", Run.eventPageId);
            Assert.AreEqual("Won the brawl.", Run.eventResult);
            CollectionAssert.DoesNotContain(Run.clearedNodeIds, node);
            Assert.AreEqual(roomsCleared, Run.roomsCleared);
            Assert.AreEqual(legStart, Run.legStartStep);

            // The event's own Leave is what clears the room, as it always was.
            Assert.IsTrue(EventPicks.OnCurrentPage(0).Closed);
            CollectionAssert.Contains(Run.clearedNodeIds, node);
        }

        [Test]
        public void AWonNonPayingNormalPartyFight_SettlesNoReward()
        {
            OpenFightFixture();
            var built = StartAndBuild(QuietBrawl);
            var session = built.Session;
            MakeTheHeroesUnkillable(session);
            foreach (var enemy in session.Encounter.Enemies) enemy.CurrentHealth = 1;

            Drive(session);
            var settled = RunOrchestrator.SettleFight(session, won: true);

            Assert.IsNull(settled.Reward);
            Assert.AreEqual("Won quietly.", Run.eventResult);
        }

        // ---- 4. reload --------------------------------------------------------------

        [Test]
        public void AReloadMidFight_RelaunchesTheSameEnemiesOnTheSameStream()
        {
            OpenFightFixture();
            EventPicks.OnCurrentPage(Brawl);

            var before = RunEncounter.For(Save, Run, RunOrchestrator.CurrentEncounterRequest());
            var draws = Enumerable.Range(0, 5).Select(_ => before.Rng.NextInt(0, 1_000_000)).ToArray();

            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            var request = RunOrchestrator.CurrentEncounterRequest();
            Assert.IsTrue(request.IsEventFight, "the reload lost the pending fight");
            Assert.AreEqual("brawl", request.EventFight.Id);

            var after = RunEncounter.For(Save, Run, request);
            CollectionAssert.AreEqual(new[] { "rat" }, after.EnemyIds);
            CollectionAssert.AreEqual(before.EnemyIds, after.EnemyIds);
            CollectionAssert.AreEqual(draws, Enumerable.Range(0, 5).Select(_ => after.Rng.NextInt(0, 1_000_000)).ToArray(),
                "the relaunched fight is on a different stream");
        }

        [Test]
        public void Reconcile_DropsAPendingFightWhoseEventIsNotOpen()
        {
            OpenFightFixture();
            EventPicks.OnCurrentPage(Brawl);

            // The event closes under it (the fields a Leave clears, minus the
            // request, which is what a hand-edited or stale save could hold).
            Run.eventId = "";
            Run.eventNodeId = -1;
            Run.pendingFight = "brawl";

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("dropped pending event fight"));
            Save.Reconcile();

            Assert.AreEqual("", Run.pendingFight);
            Assert.IsFalse(RunOrchestrator.CurrentEncounterRequest().IsEventFight);
        }

        [Test]
        public void Reconcile_DropsAPendingFightItsEventNoLongerHas_AndKeepsTheEvent()
        {
            OpenFightFixture();
            Run.pendingFight = "no_such_fight";

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("dropped pending event fight"));
            Save.Reconcile();

            Assert.AreEqual("", Run.pendingFight);
            Assert.AreEqual(Fights, Run.eventId);
            Assert.AreEqual("gate", Run.eventPageId);
        }

        // ---- 5. rollable ------------------------------------------------------------

        [Test]
        public void ARollableFalseEnemy_NeverAppearsInRoomRolls()
        {
            RunManager.StartRun(47UL);
            AppendEnemyClone("rat", "m2_rollable", rollable: true);
            AppendEnemyClone("rat", "m2_unrollable", rollable: false);

            var pool = RunEncounter.Pool().Select(c => c.Id).ToList();
            CollectionAssert.Contains(pool, "m2_rollable");
            CollectionAssert.DoesNotContain(pool, "m2_unrollable");

            bool controlSeen = false;
            for (int i = 0; i < 200; i++)
            {
                Run.step = 1 + i % 7;
                Run.currentNodeId = i;
                var roster = RunEncounter.For(Save, Run, Domain.Dungeon.RoomType.Fight);
                CollectionAssert.DoesNotContain(roster.EnemyIds, "m2_unrollable", $"roll {i}");
                controlSeen |= roster.EnemyIds.Contains("m2_rollable");
            }

            Assert.IsTrue(controlSeen, "fixture: the rollable twin never rolled either, so the check proves nothing");
        }

        private static void AppendEnemyClone(string sourceId, string id, bool rollable)
        {
            var source = ContentDatabase.Enemies.First(e => e.id == sourceId).Data;
            var copy = JsonUtility.FromJson<ResolvedEnemy>(JsonUtility.ToJson(source));
            copy.Id = id;
            copy.Rollable = rollable;

            var asset = ScriptableObject.CreateInstance<EnemyDefinition>();
            var dataField = typeof(EnemyDefinition).GetField("data", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(dataField, "EnemyDefinition no longer has a private 'data' field to seed");
            dataField.SetValue(asset, copy);
            ((List<EnemyDefinition>)ContentDatabase.Enemies).Add(asset);
        }

        // ---- 6. save 7, and the 8 above it ------------------------------------------
        //
        // Stage B's M4 bumped the save to 8 (item provenance). The version pin
        // and the refusal of the next version up moved to FakeItemTests; what
        // stays here is Stage A's own step: a v6 save with a pending event
        // fight still arrives at the current version with the fight kept.

        [Test]
        public void AVersionSixSave_MigratesToEight_KeepingAnOpenEventsPendingFight()
        {
            OpenFightFixture();
            EventPicks.OnCurrentPage(Brawl);
            Save.version = 6;

            Assert.IsTrue(Save.Migrate(), "a version-6 save was refused");
            Assert.AreEqual(8, Save.version);
            Assert.AreEqual("brawl", Run.pendingFight);
        }
    }
}
