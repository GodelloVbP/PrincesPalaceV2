using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rewards;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // THE REAL rat_caravan THROUGH THE REAL RUN
    // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.5, M7b's "done when"): every
    // page walked by EventPicks -- Browse, Browse with Odette, Look again,
    // Walk on -- the shelf opened and left on each, the rat pack built through
    // the seam the screen and the bot share (RunOrchestrator.BuildFight), won
    // and settled into the robbery; and the balance bot's first-available walk
    // through the caravan terminating with the room cleared.
    //
    // The page graph and every field are pinned on the fast host
    // (RatCaravanEventTests); the shelf's own rules on a fixture
    // (CaravanShelfRunTests). This file pins what only the real content
    // through the run can show. Literal expected values throughout.
    //
    // NEEDS BUILT CONTENT: rat_caravan must be in Resources/Content.
    public class RatCaravanRunTests
    {
        private const string Caravan = "rat_caravan";

        // Row positions (RatCaravanEventTests pins the texts in this order).
        private const int Browse = 0;
        private const int BrowseWithOdette = 1;
        private const int Rob = 2;
        private const int WalkOnFromStart = 3;
        private const int WalkOn = 0;
        private const int LookAgain = 1;
        private const int LeaveRow = 0;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-rat-caravan-" + Guid.NewGuid().ToString("N"));
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

        private static RunSnapshot Run => RunManager.Run;
        private static SaveData Save => SaveSlotManager.CurrentSave;

        // ---- fixture ----------------------------------------------------------------

        private static void OpenTheCaravan()
        {
            RunManager.StartRun(21UL);
            CollectionAssert.AreEquivalent(new[] { "sheep", "bear", "owl" }, Save.ActiveSquadIds(),
                "fixture: the squad of three is Shawn, Bjorn and Odette");
            Run.gold = 100000;
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(Caravan), "fixture: rat_caravan is not in the built content");
            Assert.AreEqual("caravan", Run.eventPageId);
        }

        private static EventChoiceResult Pick(int index)
        {
            var picked = EventPicks.OnCurrentPage(index);
            Assert.AreEqual(EventChoiceOutcome.Ok, picked.Outcome, $"the pick was refused: {picked.Reason}");
            return picked;
        }

        // The shelf is in front, with the merchant's name on it; leaving it
        // lands on `page`, which plays its lines.
        private static void AssertTheShelfIsInFront(bool revealed)
        {
            Assert.IsTrue(RunOrchestrator.EventShelfPending, "the pick did not put the shelf in front");
            Assert.AreEqual(revealed, RunOrchestrator.ShelfInFrontIsRevealed);
            Assert.AreEqual(6, RunOrchestrator.CurrentShopStock.Count, "four gear cards and two consumables");
            Assert.AreEqual(2, RunOrchestrator.CurrentShopStock.Count(e => e.fake), "a third of six is fake");

            var front = RunOrchestrator.ShelfInFront;
            Assert.IsNotNull(front);
            Assert.AreEqual("The Rat Caravan", front.Title);
            Assert.AreEqual("Mister Pockets", front.KeeperName);
            Assert.AreEqual("Dealer in Nearly Everything", front.KeeperEpithet);
            Assert.AreEqual("Portraits/Dialogue/rat_merchant", front.KeeperBustFolder);
            Assert.AreEqual("neutral", front.KeeperExpression);
            Assert.IsTrue(front.HasFakes);
            Assert.AreEqual(revealed, front.Revealed);
        }

        private static void LeaveTheShelfOnto(string page)
        {
            RunOrchestrator.LeaveShelf();
            Assert.IsFalse(RunOrchestrator.EventShelfPending);
            Assert.IsNull(RunOrchestrator.ShelfInFront, "the room shop has no merchant");
            Assert.AreEqual(page, Run.eventPageId);
            Assert.IsTrue(RunOrchestrator.CurrentEvent().Lines.Count > 0, $"{page}: plays no lines");
        }

        private static void WalkOnAndLeave(int row)
        {
            int node = Run.currentNodeId;
            Pick(row);
            var view = RunOrchestrator.CurrentEvent();
            Assert.IsTrue(view.Concluded, "Walk on has the merchant's parting line to read");
            Assert.AreNotEqual("", view.ResultText);

            RunOrchestrator.LeaveEvent();

            CollectionAssert.Contains(Run.clearedNodeIds, node, "the event's Leave clears the room");
            CollectionAssert.DoesNotContain(Run.eventsSeen, Caravan, "Walk on must leave the caravan to return");
            Assert.AreEqual(1, Run.shelves.Count, "Walk on kept the stock for the next visit");
        }

        // ---- the pages --------------------------------------------------------------

        [Test]
        public void Browse_ThenLookAgain_ThenWalkOn_KeepsTheStockAndTheCaravan()
        {
            OpenTheCaravan();

            Pick(Browse);
            AssertTheShelfIsInFront(revealed: false);
            LeaveTheShelfOnto("after_browse");

            Pick(LookAgain);
            AssertTheShelfIsInFront(revealed: false);
            LeaveTheShelfOnto("after_browse");

            WalkOnAndLeave(WalkOn);
        }

        [Test]
        public void BrowseWithOdette_MarksTheFakes_HerPageFollows_AndLookAgainKeepsTheMarks()
        {
            OpenTheCaravan();

            Pick(BrowseWithOdette);
            AssertTheShelfIsInFront(revealed: true);
            LeaveTheShelfOnto("after_odette");

            Pick(LookAgain);
            AssertTheShelfIsInFront(revealed: true);
            LeaveTheShelfOnto("after_browse");

            WalkOnAndLeave(WalkOn);
        }

        [Test]
        public void WalkingOnAtOnce_RollsNoStock()
        {
            OpenTheCaravan();
            int node = Run.currentNodeId;

            Pick(WalkOnFromStart);
            RunOrchestrator.LeaveEvent();

            CollectionAssert.Contains(Run.clearedNodeIds, node);
            CollectionAssert.DoesNotContain(Run.eventsSeen, Caravan);
            Assert.AreEqual(0, Run.shelves?.Count ?? 0, "nobody looked, so nothing was rolled");
        }

        // ---- the robbery ------------------------------------------------------------

        // Three rats at 1 HP against a padded squad: the win is the ending
        // under test, not a coin toss over the rats (M8b tunes those).
        [Test]
        public void RobbingHim_ThreeEliteRats_Won_PaysAReckoning_HandsOverFiveOfSix_AndFinishes()
        {
            OpenTheCaravan();
            int node = Run.currentNodeId;
            int bagBefore = Save.stockpiledItems?.Count ?? 0;

            Pick(Rob);
            Assert.IsTrue(RunOrchestrator.EventFightPending, "Rob him left no fight pending");

            var built = RunOrchestrator.BuildFight();
            Assert.IsNotNull(built, "the rat pack did not build");
            var session = built.Session;
            CollectionAssert.AreEquivalent(new[] { "sheep", "bear", "owl" }, built.PartyIds, "the normal squad fights");
            CollectionAssert.AreEqual(new[] { "rat", "rat", "rat" },
                session.Encounter.Enemies.Select(e => session.SourceFor(e).Source.Id).ToArray());
            Assert.AreEqual(0, session.RoundLimit);

            foreach (var member in session.Encounter.PlayerParty)
            {
                member.MaxHealth = 1_000_000;
                member.CurrentHealth = 1_000_000;
            }

            foreach (var rat in session.Encounter.Enemies) rat.CurrentHealth = 1;

            session.Begin();
            session.DrainBeats();
            for (int i = 0; i < 400 && !session.IsOver; i++)
            {
                if (session.IsPlayerTurn) session.ExecuteAttack(session.Encounter.FrontEnemy);
                session.DrainBeats();
            }

            Assert.IsTrue(session.IsOver, "fixture: the fight did not end in 400 swings");
            Assert.AreEqual(FightEndReason.Defeated, session.EndReason);

            var settled = RunOrchestrator.SettleFight(session, session.PlayerWon);

            Assert.IsNotNull(settled.Reward, "pays: true opens the Reckoning");
            Assert.AreEqual("robbed", Run.eventPageId);
            CollectionAssert.Contains(Run.eventsSeen, Caravan, "the robbery finishes the caravan");
            Assert.AreEqual(0, Run.shelves?.Count ?? 0, "finish ends the stock");
            CollectionAssert.DoesNotContain(Run.clearedNodeIds, node, "the fight does not clear the room");
            Assert.AreEqual(bagBefore + 5, Save.stockpiledItems.Count, "six unsold cards, one lost, each its own lot");

            var view = RunOrchestrator.CurrentEvent();
            Assert.AreNotEqual("", view.ResultText);
            StringAssert.Contains("Lost in the scuffle: ", view.EffectsLine, "the result names the lost piece");
            Assert.IsTrue(view.Lines.Any(l => l.SpeakerId == "merchant"), "the merchant has his say");

            Assert.IsTrue(Pick(LeaveRow).Closed, "robbed's Leave is silent and closes at once");
            CollectionAssert.Contains(Run.clearedNodeIds, node);
        }

        // ---- the bot ----------------------------------------------------------------

        // THE BOT'S OWN LOOP, whole shallow runs, with the caravan the only
        // event, so every Event room is the caravan. Its first-available path
        // is Browse (the first row), the shop's buying loop on the shelf, then
        // Walk on (after_browse's first row), so the caravan comes back at
        // every later Event room. VisitEvent records a hit for a trapped
        // event, a refused choice, the choice cap and a room left uncleared,
        // so no hits IS "terminated with the room cleared".
        [Test]
        public void TheBotsFirstAvailablePath_ThroughTheCaravan_EndsWithTheRoomCleared()
        {
            const int DepthCap = 6;
            var events = (List<EventDefinition>)ContentDatabase.Events;
            events.RemoveAll(e => e.Data.Id != Caravan);
            Assert.AreEqual(1, events.Count, "fixture: the caravan is not in the built content");

            int runs = 0;
            for (ulong seed = 1; seed <= 200UL && runs < 3; seed++)
            {
                var result = BotRunDriver.PlayRun(seed, "RandomLegal", ProfilePresets.Fresh, DepthCap);
                var rooms = result.Trace.Rooms.Where(r => r.EventId == Caravan).ToList();
                if (rooms.Count == 0) continue;

                runs++;
                var hits = result.Hits.Where(h => h.Name != "StalledEnemyTurn").ToList();
                Assert.IsEmpty(hits, $"seed {seed}: " + string.Join(" | ", hits.Select(h => h.ToString())));
                Assert.IsTrue(result.Trace.Capped || result.Trace.DeathStep > 0, $"seed {seed} did not terminate cleanly");
                Assert.IsFalse(result.Trace.Fights.Any(f => f.EventId == Caravan), $"seed {seed}: first available robbed him");
            }

            Assert.AreEqual(3, runs, $"only {runs} run(s) over seeds 1-200 reached the caravan within {DepthCap} steps");
        }

        // -EventChoice "Rob him" (M8b's probe): the forced caravan's rob is
        // played as an elite fight of three rats and traced under its id.
        [Test]
        public void EventChoice_RobHim_PlaysTheRatPack_AndTracesIt()
        {
            var probe = new BotRunDriver.BotProbe { ForceEventId = Caravan, ForceEventFloor = 1, EventChoiceText = "Rob him" };

            int robbed = 0;
            for (ulong seed = 1; seed <= 60UL && robbed < 3; seed++)
            {
                var result = BotRunDriver.PlayRun(seed, "GreedyAggressive", ProfilePresets.Fresh, 8,
                    ShopNodeMode.WhenOffered, probe);
                if (!result.Trace.Rooms.Any(r => r.EventForced)) continue;

                robbed++;
                var fight = result.Trace.Fights.SingleOrDefault(f => f.EventId == Caravan);
                Assert.IsNotNull(fight, $"seed {seed}: the forced caravan played no rob");
                CollectionAssert.AreEqual(new[] { "rat", "rat", "rat" }, fight.EnemyIds, $"seed {seed}");
                CollectionAssert.Contains(new[] { "Defeated", "Fell" }, fight.EndReason, $"seed {seed}");
                Assert.IsFalse(result.Hits.Any(h => h.Name == "EventRoomNotCleared"), $"seed {seed}");
            }

            Assert.AreEqual(3, robbed, $"only {robbed} run(s) over seeds 1-60 reached the forced caravan");
        }
    }
}
