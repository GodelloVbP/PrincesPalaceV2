using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // WALKING INTO AN EVENT ROOM, CHOOSING, LEAVING, AND COMING BACK.
    //
    // ShopArrivalTests' fixture: a real map, real content and a throwaway
    // save root, so "on disk" means the file and not the cached SaveData.
    //
    // The event under test is demo_wishing_well (ContentData/events.json),
    // the phase-1 fixture built to exercise every feature. Its page and
    // choice ids are named here as literals; the choice INDICES are looked
    // up from the definition by what the choice does, so reordering the
    // demo's buttons does not silently retarget a test.
    public class EventArrivalTests
    {
        private const string DemoEvent = "demo_wishing_well";
        private const string DemoCounter = "wishing_well_tosses";

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-event-arrival-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
        }

        [TearDown]
        public void RestoreSaveRoot()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RoomResolver.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // Found by generating maps directly: the generator is pure, so the
        // search costs no save writes. Through GenerateLegFor -- the call
        // RunManager.Map makes -- and not GenerateLeg(new SeededRandom(seed)),
        // which is a different map for the same seed (GenerateLegFor opens
        // RngStreams.Leg on the seed first).
        private static ulong SeedWithAnEventInTheFirstColumn()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = DescentMapGenerator.GenerateLegFor(seed, 0);
                if (map.AtDepth(1).Any(n => n.Type == RoomType.Event)) return seed;
            }

            throw new AssertionException("No seed under 4000 generated an event in the first column.");
        }

        private static DescentNode ArriveAtTheFirstEvent()
        {
            RunManager.StartRun(SeedWithAnEventInTheFirstColumn());
            var node = RunManager.Map.AtDepth(1).First(n => n.Type == RoomType.Event);

            // Every other authored event (petting_zoo, ...) counts as seen
            // this run, so the pool is the demo alone and the roll must pick it.
            foreach (var definition in ContentDatabase.Events)
            {
                if (definition.id != DemoEvent) RunManager.Run.eventsSeen.Add(definition.id);
            }

            Assert.AreEqual(RunOrchestrator.Arrival.Event, RunOrchestrator.ArriveAt(node),
                "fixture: the demo event is eligible on floor 1 and the run has seen nothing else");
            Assert.AreEqual(DemoEvent, RunManager.Run.eventId,
                "fixture: every other event is marked seen, so the pool of one must pick the demo");
            return node;
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;

        private static int ChoiceIndex(string pageId, Func<ResolvedEventChoice, bool> match)
        {
            var page = ContentDatabase.Events.First(e => e.id == DemoEvent).Data.PageById(pageId);
            for (int i = 0; i < page.Choices.Length; i++)
            {
                if (match(page.Choices[i])) return i;
            }

            throw new AssertionException($"fixture: page '{pageId}' has no such choice");
        }

        private static int TossIndex() =>
            ChoiceIndex("well", c => c.Effects.Any(e => e.Kind == EventEffectKind.Counter));

        private static int PlainLeaveIndex() =>
            ChoiceIndex("well", c => c.Requires.Length == 0 && c.Effects.Length == 0);

        private static int LevelGatedIndex() =>
            ChoiceIndex("well", c => c.Requires.Any(r => r.Kind == EventRequirementKind.MemberLevel));

        private static int ShawnIndex() =>
            ChoiceIndex("well", c => c.Requires.Any(r => r.Kind == EventRequirementKind.InParty));

        // ---- arriving -----------------------------------------------------------------

        // Contract 1: the pick is on disk BEFORE ArriveAt returns, which is
        // before MapController.Walk.Arrive can call OpenEvent. Read back off
        // the file, since the cache is the thing that would hide a missing
        // write.
        [Test]
        public void ThePickIsPersistedBeforeThePanelWouldShowAndTheRoomIsNotCleared()
        {
            var node = ArriveAtTheFirstEvent();

            CollectionAssert.DoesNotContain(RunManager.Run.clearedNodeIds, node.Id);

            var onDisk = SaveSystem.Load(SaveSlotManager.CurrentSlot).activeRun;
            Assert.AreEqual(DemoEvent, onDisk.eventId);
            Assert.AreEqual(node.Id, onDisk.eventNodeId);
            Assert.AreEqual("well", onDisk.eventPageId);
            CollectionAssert.Contains(onDisk.eventsSeen, DemoEvent);
        }

        // Contract 2: the same event on the same page after a reload -- and
        // mid-event, not just at arrival: the tenth toss moved it to its own
        // page, and the result line comes back with it.
        [Test]
        public void AReloadYieldsTheSameEventAndPage()
        {
            ArriveAtTheFirstEvent();
            RunManager.Run.gold = 100;
            Save.SetEventCounter(DemoCounter, 9);

            var tenth = EventPicks.OnCurrentPage(TossIndex());
            Assert.AreEqual(EventChoiceOutcome.Ok, tenth.Outcome);

            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            Assert.IsTrue(RunOrchestrator.EventIsOpen);
            var view = RunOrchestrator.CurrentEvent();
            Assert.AreEqual(DemoEvent, view.EventId);
            Assert.AreEqual("wish_granted", view.PageId);
            Assert.AreEqual("On the tenth coin the well glimmers gold -- your wish is granted.", view.ResultText);
            Assert.AreEqual("-5 gold  ·  +50 XP", view.EffectsLine);
        }

        // Contract 8 through the real path: picks 9, 10 and 11 of the counter.
        // The counter ticks BEFORE the outcome reads it, so the pick that
        // makes it 10 is the one that branches.
        [TestCase(8, 9, "well")]
        [TestCase(9, 10, "wish_granted")]
        [TestCase(10, 11, "well")]
        public void TheTenthTossAndOnlyTheTenthGoesToItsOwnPage(int before, int after, string expectedPage)
        {
            ArriveAtTheFirstEvent();
            RunManager.Run.gold = 100;
            Save.SetEventCounter(DemoCounter, before);

            EventPicks.OnCurrentPage(TossIndex());

            Assert.AreEqual(after, Save.EventCounter(DemoCounter));
            Assert.AreEqual(expectedPage, RunManager.Run.eventPageId);
        }

        // A spend is not an earning: run.gold moves, goldEarned does not.
        [Test]
        public void ASpendLeavesGoldEarnedAlone()
        {
            ArriveAtTheFirstEvent();
            RunManager.Run.gold = 100;
            RunManager.Run.goldEarned = 40;

            EventPicks.OnCurrentPage(TossIndex());

            Assert.AreEqual(95, RunManager.Run.gold);
            Assert.AreEqual(40, RunManager.Run.goldEarned);
        }

        // A gain goes through BankPayout's rule: held AND earned.
        [Test]
        public void AGainIsBankedLikeAnyPayout()
        {
            ArriveAtTheFirstEvent();
            Save.selectedCharacterIds = new System.Collections.Generic.List<string> { "sheep" };
            RunManager.Run.gold = 0;
            RunManager.Run.goldEarned = 0;

            var result = EventPicks.OnCurrentPage(ShawnIndex());

            Assert.AreEqual(EventChoiceOutcome.Ok, result.Outcome);
            Assert.AreEqual(10, RunManager.Run.gold);
            Assert.AreEqual(10, RunManager.Run.goldEarned);
            Assert.AreEqual("+10 gold", result.EffectsLine);

            // A Leave outcome WITH a result shows it and waits: concluded.
            Assert.IsFalse(result.Closed);
            var view = RunOrchestrator.CurrentEvent();
            Assert.IsTrue(view.Concluded);
            Assert.IsEmpty(view.Choices);
        }

        // ---- locked choices -----------------------------------------------------------

        [Test]
        public void ALockedChoiceIsRefusedAndNothingMoves()
        {
            var node = ArriveAtTheFirstEvent();
            foreach (var character in Save.roster) character.level = 14;
            RunManager.Run.gold = 3;
            SaveSlotManager.SaveCurrent();

            int gated = LevelGatedIndex();
            var shown = RunOrchestrator.CurrentEvent().Choices[gated];
            Assert.IsFalse(shown.Enabled);
            Assert.AreEqual("Requires a level 15 party member", shown.LockReason);

            var refused = EventPicks.OnCurrentPage(gated);
            Assert.AreEqual(EventChoiceOutcome.Refused, refused.Outcome);
            Assert.AreEqual(EventRefusal.Locked, refused.Reason);

            // And the implied gate: 3 gold cannot toss a 5-gold coin.
            var toss = EventPicks.OnCurrentPage(TossIndex());
            Assert.AreEqual(EventRefusal.Locked, toss.Reason);
            Assert.AreEqual("Requires 5 gold", RunOrchestrator.CurrentEvent().Choices[TossIndex()].LockReason);

            Assert.AreEqual(3, RunManager.Run.gold);
            Assert.AreEqual(0, Save.EventCounter(DemoCounter));
            Assert.AreEqual("well", RunManager.Run.eventPageId);
            CollectionAssert.DoesNotContain(RunManager.Run.clearedNodeIds, node.Id);
        }

        [Test]
        public void AnUnlockedGateAppliesItsEffectToTheSquad()
        {
            ArriveAtTheFirstEvent();
            var member = Save.ActiveSquad()[0];
            member.level = 15;
            int max = ContentDatabase.EffectiveStats(member).maxHealth;
            RunManager.Run.currentHealth.RemoveAll(e => e.characterId == member.definitionId);
            RunManager.Run.currentHealth.Add(new RunHealthEntry { characterId = member.definitionId, hp = 1 });

            var result = EventPicks.OnCurrentPage(LevelGatedIndex());

            Assert.AreEqual(EventChoiceOutcome.Ok, result.Outcome);
            Assert.AreEqual("Party healed 25%", result.EffectsLine);

            // The arithmetic is pinned with literals in EventRoomRulesTests;
            // this is the wiring -- the effect reached the run's own entry.
            int hp = RunManager.Run.currentHealth.First(e => e.characterId == member.definitionId).hp;
            Assert.AreEqual(EventHealth.Healed(1, max, 25), hp);
            Assert.Greater(hp, 1);
        }

        // ---- leaving ------------------------------------------------------------------

        [Test]
        public void LeaveEventClearsTheRoom()
        {
            var node = ArriveAtTheFirstEvent();

            RunOrchestrator.LeaveEvent();

            CollectionAssert.Contains(RunManager.Run.clearedNodeIds, node.Id);
            Assert.AreEqual("", RunManager.Run.eventId);
            Assert.IsFalse(RunOrchestrator.EventIsOpen);
            CollectionAssert.Contains(SaveSystem.Load(SaveSlotManager.CurrentSlot).activeRun.clearedNodeIds, node.Id);
        }

        // The plain Leave button: nothing to say, so it closes at once.
        [Test]
        public void ThePlainLeaveChoiceClosesTheEventAndClearsTheRoom()
        {
            var node = ArriveAtTheFirstEvent();

            var result = EventPicks.OnCurrentPage(PlainLeaveIndex());

            Assert.IsTrue(result.Closed);
            CollectionAssert.Contains(RunManager.Run.clearedNodeIds, node.Id);
            Assert.IsNull(RunOrchestrator.CurrentEvent());
            CollectionAssert.Contains(RunManager.Run.eventsSeen, DemoEvent, "left is still seen");
        }

        // ---- counters -------------------------------------------------------------------

        [Test]
        public void ACounterSurvivesEndRun()
        {
            ArriveAtTheFirstEvent();
            RunManager.Run.gold = 100;

            EventPicks.OnCurrentPage(TossIndex());
            Assert.AreEqual(1, Save.EventCounter(DemoCounter));

            RunManager.EndRun();
            SaveSlotManager.Forget();

            Assert.IsFalse(RunManager.HasRun, "fixture: the run really ended");
            Assert.AreEqual(1, Save.EventCounter(DemoCounter));
        }

        // ---- old saves and stale state --------------------------------------------------

        // Contract 15. A save written before these fields existed has
        // currentNodeId 0 and no eventNodeId at all -- if the node were the
        // discriminator, a missing int reading 0 would open an event on node
        // 0. eventId is, and a missing string is no event.
        [Test]
        public void AnOldSaveWithoutTheNewFieldsLoadsWithNoOpenEvent()
        {
            string json = "{\"version\":" + SaveData.CurrentVersion +
                          ",\"activeRun\":{\"hasRun\":true,\"runSeed\":12345,\"floor\":1,\"currentNodeId\":0}}";
            File.WriteAllText(Path.Combine(_root, "save_slot_0.json"), json);

            Assert.IsTrue(RunManager.HasRun, "fixture: the old save carries a live run");
            Assert.IsFalse(RunOrchestrator.EventIsOpen);
            Assert.IsNull(RunOrchestrator.CurrentEvent());
            Assert.AreEqual("", RunManager.Run.eventId);
            Assert.IsNotNull(RunManager.Run.eventsSeen);
            Assert.IsNotNull(Save.eventCounters);
            Assert.IsEmpty(Save.eventCounters);
            Assert.AreEqual(0, Save.EventCounter(DemoCounter));
        }

        // An event not under the party is closed on load, like a shelf that
        // is not under the party (ReconcileShopStock).
        [Test]
        public void AnEventOpenOnAnotherNodeIsClosedOnLoad()
        {
            ArriveAtTheFirstEvent();
            RunManager.Run.eventNodeId = RunManager.Run.currentNodeId + 1;
            SaveSlotManager.SaveCurrent();

            SaveSlotManager.Forget();

            Assert.AreEqual("", RunManager.Run.eventId);
            Assert.IsFalse(RunOrchestrator.EventIsOpen);
        }

        // ---- the empty pool ---------------------------------------------------------------

        // Contract 3: nothing eligible means the old room, not a throw and
        // not an empty panel.
        [Test]
        public void AnEmptyPoolFallsBackToTheOldMessage()
        {
            RunManager.StartRun(SeedWithAnEventInTheFirstColumn());
            foreach (var definition in ContentDatabase.Events) RunManager.Run.eventsSeen.Add(definition.id);
            var node = RunManager.Map.AtDepth(1).First(n => n.Type == RoomType.Event);

            var arrival = RunOrchestrator.ArriveAt(node);

            Assert.AreEqual(RunOrchestrator.Arrival.Resolved, arrival);
            Assert.AreEqual(RoomResolution.Kind.EventNotBuilt, RoomResolver.Last.Result);
            CollectionAssert.Contains(RunManager.Run.clearedNodeIds, node.Id);
            Assert.AreEqual("", RunManager.Run.eventId);
        }

        // ---- the Core context ----------------------------------------------------------------

        [Test]
        public void TheRunContextReadsTheLiveSaveAndRun()
        {
            RunManager.StartRun(SeedWithAnEventInTheFirstColumn());
            var member = Save.ActiveSquad()[0];
            var context = new RunEventContext(Save, RunManager.Run);

            member.level = 7;
            RunManager.Run.gold = 42;
            Save.SetEventCounter("some_counter", 3);
            int charismaBefore = context.EffectiveAbilityScore(member.definitionId, AbilityScore.Charisma);
            member.investedAbilityScores = member.investedAbilityScores.With(AbilityScore.Charisma,
                member.investedAbilityScores[AbilityScore.Charisma] + 5);

            CollectionAssert.AreEqual(Save.ActiveSquadIds(), context.SquadIds);
            Assert.AreEqual(7, context.LevelOf(member.definitionId));
            Assert.AreEqual(42, context.Gold);
            Assert.AreEqual(3, context.CounterValue("some_counter"));
            Assert.AreEqual(0, context.CounterValue("never_touched"));
            Assert.AreEqual(5, context.EffectiveAbilityScore(member.definitionId, AbilityScore.Charisma) - charismaBefore,
                "effective, so five invested points are five more");
            Assert.AreEqual(0, context.LevelOf("not_a_character"));
        }

        // ---- the bot ------------------------------------------------------------------------

        // THE BOT'S OWN LOOP, not a seam into it: whole runs, shallow, until
        // one walks into an event. The archetype picks the node, so a map
        // search alone cannot promise the bot enters the event -- and a map
        // column is never all one type (DescentMapGenerator rule (c)), so
        // there is no seed where every choice is an event. The walk is
        // deterministic per seed, so the first hit is the same seed every
        // run; the bound only exists so content that removed events fails
        // here loudly rather than looping.
        [Test]
        public void TheBotCompletesAnEventRoom()
        {
            const int DepthCap = 4;

            for (ulong seed = 1; seed <= 200UL; seed++)
            {
                var result = BotRunDriver.PlayRun(seed, "RandomLegal", ProfilePresets.Fresh, DepthCap);
                if (!result.Trace.Rooms.Any(r => r.RoomType == "Event")) continue;

                // The event arm records a hit for a trapped event, a refused
                // choice, the choice cap, and a room left uncleared -- so no
                // hits IS "completed". The run went on past it, too.
                // StalledEnemyTurn is the fight fault BalanceBotSmokeTests
                // already counts rather than fails on; it is not this room's.
                var hits = result.Hits.Where(h => h.Name != "StalledEnemyTurn").ToList();
                Assert.IsEmpty(hits, $"seed {seed}: " + string.Join(" | ", hits.Select(h => h.ToString())));
                Assert.IsTrue(result.Trace.Capped || result.Trace.DeathStep > 0, $"seed {seed} did not terminate cleanly");
                return;
            }

            Assert.Fail("No bot run in seeds 1-200 walked into an event room within " + DepthCap + " steps.");
        }
    }
}
