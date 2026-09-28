using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rewards;
using UnityEngine.TestTools;

namespace PrincesPalace.PlayModeTests
{
    // M5 OF docs/PLAN_EVENTS_BELL_AND_CARAVAN.md, THROUGH THE REAL RUN: an
    // event's merchant shelf -- rolled once, kept with the event across a
    // reload, a Walk on, a room shop and a return at another node; revealed
    // by "Browse with Odette" and kept revealed; robbed through takeShelf.
    //
    // A FIXTURE EVENT, not the caravan: its content is M7b. The fixture rides
    // the loaded catalogue for one test (FixtureEvents) and TearDown reloads
    // it. Expected values are literals (CLAUDE.md gotcha 5): the formula
    // itself is pinned in MerchantShelfTests; here the only prices asserted
    // are the potions', 15 -> 11 at 70%.
    public class CaravanShelfRunTests
    {
        private const string Caravan = "m5_caravan";
        private const string Wares = "wares";

        // Choices on the `caravan` page, then on `after_browse`.
        private const int Browse = 0;
        private const int BrowseWithOdette = 1;
        private const int Rob = 2;
        private const int WalkOnFromStart = 3;
        private const int WalkOn = 0;
        private const int LookAgain = 1;
        private const int RobAfter = 2;

        private const int Rich = 100000;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-caravan-shelf-" + Guid.NewGuid().ToString("N"));
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

        // ---- the fixture ----------------------------------------------------------------

        private static RawEventEffect Shelf(bool reveal = false) =>
            new RawEventEffect { kind = "shelf", shelf = Wares, reveal = reveal };

        private static RawEventOutcome GoTo(string page, params RawEventEffect[] effects) =>
            new RawEventOutcome { goTo = page, effects = effects };

        private static RawEventChoice Choice(string text, RawEventOutcome outcome) =>
            new RawEventChoice { text = text, outcomes = new[] { outcome } };

        private static RawEventChoice RobHim() => Choice("Rob him", new RawEventOutcome
        {
            effects = new[] { new RawEventEffect { kind = "fight", fight = "rat_pack" } },
        });

        private static RawEventEntry CaravanFixture() => new RawEventEntry
        {
            id = Caravan,
            mayReturn = true,
            floors = new[] { 999 },
            pages = new[]
            {
                new RawEventPage
                {
                    id = "caravan", title = "Caravan", body = "B",
                    choices = new[]
                    {
                        Choice("Browse", GoTo("after_browse", Shelf())),
                        Choice("Browse with Odette", GoTo("after_browse", Shelf(reveal: true))),
                        RobHim(),
                        Choice("Walk on", GoTo("Leave")),
                    },
                },
                new RawEventPage
                {
                    id = "after_browse", title = "Caravan", body = "B",
                    choices = new[]
                    {
                        Choice("Walk on", GoTo("Leave")),
                        Choice("Look again", GoTo("after_browse", Shelf())),
                        RobHim(),
                    },
                },
                new RawEventPage
                {
                    id = "robbed", title = "Robbed", body = "B",
                    choices = new[] { Choice("Leave", GoTo("Leave")) },
                },
            },
            fights = new[]
            {
                new RawEventFight
                {
                    id = "rat_pack", enemies = new[] { "rat" }, elite = true,
                    onDefeated = GoTo("robbed",
                        new RawEventEffect { kind = "takeShelf", shelf = Wares, amount = 1 },
                        new RawEventEffect { kind = "finish" }),
                },
            },
            shelves = new[]
            {
                new RawEventShelf
                {
                    id = Wares, priceFactorPercent = 70, fakeShare = 3, sections = new[] { "gear" }, consumableCount = 2,
                },
            },
        };

        private static void StartWithTheCaravanHere(ulong seed = 21UL)
        {
            FixtureEvents.Append(CaravanFixture());
            RunManager.StartRun(seed);
            Run.gold = Rich;
            OpenTheCaravanHere();
        }

        private static void OpenTheCaravanHere()
        {
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(Caravan), "fixture: the caravan did not open");
        }

        private static void Pick(int choice)
        {
            var picked = EventPicks.OnCurrentPage(choice);
            Assert.AreEqual(EventChoiceOutcome.Ok, picked.Outcome, $"the pick was refused: {picked.Reason}");
        }

        private static SaveData ReloadFromDisk()
        {
            SaveSlotManager.Forget();
            return SaveSlotManager.CurrentSave;
        }

        // What a card IS, the whole of it: what it sells, its roll, its price,
        // whether it is fake, which physical copy, and whether it sold.
        private static List<string> Snapshot() =>
            RunOrchestrator.CurrentShopStock
                .Select(e => $"{e.section}/{e.index} {e.kind} {e.contentId} +{e.plus} r{e.riftTier} " +
                             $"[{string.Join(",", e.modifiers)}] {e.price}g fake={e.fake} lot={e.lot} sold={e.sold} " +
                             $"none={e.noOffer}")
                .ToList();

        // A node the party can walk to next.
        private static void WalkToTheNextNode()
        {
            var next = RunManager.Map.ReachableFrom(Run.currentNodeId).First();
            Assert.IsTrue(RunManager.MoveTo(next), "fixture: could not move on");
        }

        private static void LeaveTheShelfAndWalkOn()
        {
            RunOrchestrator.LeaveShelf();
            var walked = EventPicks.OnCurrentPage(WalkOn);
            Assert.IsTrue(walked.Closed, "Walk on did not close the event");
        }

        // ---- what Browse opens ----------------------------------------------------------

        [Test]
        public void Browse_PutsTheShelfInFront_FourGearTwoPotions_TwoFake_EachWithItsOwnLot()
        {
            StartWithTheCaravanHere();
            Pick(Browse);

            Assert.IsTrue(RunOrchestrator.EventShelfPending);
            Assert.IsTrue(RunOrchestrator.ShelfInFrontIsMerchant);
            Assert.IsFalse(RunOrchestrator.ShelfInFrontIsRevealed, "a plain Browse revealed the fakes");
            Assert.AreEqual("after_browse", Run.eventPageId, "the pick did not move the event on");

            var stock = RunOrchestrator.CurrentShopStock;
            Assert.AreEqual(6, stock.Count);
            Assert.AreEqual(4, stock.Count(e => e.kind == ShopEntryKind.Gear && e.section == ShopStock.GearSection));
            Assert.AreEqual(2, stock.Count(e => e.fake));

            var potions = stock.Where(e => e.kind == ShopEntryKind.Consumable).ToList();
            CollectionAssert.AreEquivalent(new[] { "health_potion", "mana_potion" }, potions.Select(e => e.contentId));
            CollectionAssert.AreEqual(new[] { 11, 11 }, potions.Select(e => e.price).ToArray(), "15 at 70% is 11");

            CollectionAssert.AllItemsAreUnique(stock.Select(e => e.lot));
            Assert.IsTrue(stock.All(e => e.lot.StartsWith("m5_caravan:wares:", StringComparison.Ordinal)),
                string.Join(", ", stock.Select(e => e.lot)));
        }

        [Test]
        public void LeavingTheShelf_ReturnsToTheEvent_AndNeverClearsTheRoom()
        {
            StartWithTheCaravanHere();
            int node = Run.currentNodeId;
            Pick(Browse);

            RunOrchestrator.LeaveShelf();

            Assert.IsFalse(RunOrchestrator.EventShelfPending);
            Assert.IsTrue(RunOrchestrator.EventIsOpen);
            Assert.AreEqual("after_browse", Run.eventPageId);
            CollectionAssert.DoesNotContain(Run.clearedNodeIds, node);
            Assert.AreEqual(0, RunOrchestrator.CurrentShopStock.Count, "the shelf is still in front after leaving it");
            Assert.AreEqual(1, Run.shelves.Count, "leaving the shelf took its stock with it");
        }

        [Test]
        public void APickIsRefusedWhileTheShelfIsInFront()
        {
            StartWithTheCaravanHere();
            Pick(Browse);

            var again = EventPicks.OnCurrentPage(WalkOn);

            Assert.AreEqual(EventRefusal.ShelfOpen, again.Reason);
            Assert.AreEqual(Wares, Run.pendingShelf);
        }

        // ---- the stock stays --------------------------------------------------------------

        [Test]
        public void TheStock_IsTheSameAfterAReload_WithTheShelfStillInFront()
        {
            StartWithTheCaravanHere();
            Pick(Browse);
            var before = Snapshot();

            ReloadFromDisk();

            Assert.IsTrue(RunOrchestrator.EventShelfPending, "the shelf in front did not survive the reload");
            CollectionAssert.AreEqual(before, Snapshot());
        }

        [Test]
        public void WalkOn_AndAReturnAtAnotherNode_ShowTheSameStock_MinusWhatSold()
        {
            StartWithTheCaravanHere();
            Pick(Browse);
            Assert.AreEqual(ShopOutcome.Ok, RunOrchestrator.BuyConsumable(0).Outcome);
            var before = Snapshot();
            int firstNode = Run.currentNodeId;

            LeaveTheShelfAndWalkOn();
            CollectionAssert.Contains(Run.clearedNodeIds, firstNode, "Walk on did not clear the room");
            Assert.AreEqual(1, Run.shelves.Count, "Walk on ended the stock");

            WalkToTheNextNode();
            Assert.AreNotEqual(firstNode, Run.currentNodeId);
            OpenTheCaravanHere();
            Pick(Browse);

            CollectionAssert.AreEqual(before, Snapshot());
            Assert.IsTrue(RunOrchestrator.CurrentShopStock.Single(e => e.kind == ShopEntryKind.Consumable && e.index == 0).sold);
            Assert.AreEqual(1, Run.shelves.Count, "the return rolled a second stock");
        }

        // A seed whose first column offers a shop (ShopArrivalTests' search).
        private static ulong SeedWithAShopInTheFirstColumn()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = DescentMapGenerator.GenerateLegFor(seed, 0);
                if (map.AtDepth(1).Any(n => n.Type == RoomType.Shop)) return seed;
            }

            throw new AssertionException("No seed under 4000 generated a shop in the first column.");
        }

        [Test]
        public void ARoomShopInBetween_LeavesTheCaravanStockAlone()
        {
            StartWithTheCaravanHere(SeedWithAShopInTheFirstColumn());
            Pick(Browse);
            var before = Snapshot();
            LeaveTheShelfAndWalkOn();

            var shop = RunManager.Map.AtDepth(1).First(n => n.Type == RoomType.Shop);
            Assert.AreEqual(RunOrchestrator.Arrival.Shop, RunOrchestrator.ArriveAt(shop));
            Assert.IsFalse(RunOrchestrator.ShelfInFrontIsMerchant, "the room shop read as a merchant shelf");
            Assert.AreEqual(ShopStock.GearCount + ShopStock.BookCount + ShopStock.RelicCount,
                RunOrchestrator.CurrentShopStock.Count, "the room shop's own stock is not in front");
            Assert.IsTrue(RunOrchestrator.CurrentShopStock.All(e => e.lot == "" && !e.fake),
                "the room shop's cards carry provenance");
            Assert.AreEqual(ShopRefusal.BadIndex, RunOrchestrator.BuyConsumable(0).Reason,
                "the room shop sold a consumable");
            RunOrchestrator.LeaveShop();

            WalkToTheNextNode();
            OpenTheCaravanHere();
            Pick(Browse);

            CollectionAssert.AreEqual(before, Snapshot());
        }

        // ---- the reveal -----------------------------------------------------------------

        [Test]
        public void TheReveal_SurvivesLookAgain_AndAReload()
        {
            StartWithTheCaravanHere();
            Pick(BrowseWithOdette);
            Assert.IsTrue(RunOrchestrator.ShelfInFrontIsRevealed, "Browse with Odette revealed nothing");

            RunOrchestrator.LeaveShelf();
            Pick(LookAgain);
            Assert.IsTrue(RunOrchestrator.ShelfInFrontIsRevealed, "Look again hid the fakes");

            ReloadFromDisk();
            Assert.IsTrue(RunOrchestrator.ShelfInFrontIsRevealed, "the reload hid the fakes");
        }

        // ---- buying -----------------------------------------------------------------------

        // The first seed whose shelf, just browsed, holds a card like this --
        // so a test does not depend on which two cards one seed makes fake.
        private static ShopStockEntry BrowseUntilTheShelfHas(Func<ShopStockEntry, bool> wanted, string what)
        {
            FixtureEvents.Append(CaravanFixture());
            for (ulong seed = 1; seed < 60UL; seed++)
            {
                SaveSlotManager.Forget();
                RunManager.ResetForTests();
                RunManager.StartRun(seed);
                Run.gold = Rich;
                OpenTheCaravanHere();
                Pick(Browse);

                var card = RunOrchestrator.CurrentShopStock.FirstOrDefault(wanted);
                if (card != null) return card;
            }

            throw new AssertionException($"No seed under 60 stocked {what}.");
        }

        [Test]
        public void ABoughtGenuinePotion_NeverMergesWithAnOrdinaryOne()
        {
            var card = BrowseUntilTheShelfHas(
                e => e.kind == ShopEntryKind.Consumable && e.contentId == "health_potion" && !e.fake,
                "a genuine health potion");
            InventoryOps.Add(Save.stockpiledItems, "health_potion", 2);
            int rowsBefore = Save.stockpiledItems.Count;

            Assert.AreEqual(ShopOutcome.Ok, RunOrchestrator.BuyConsumable(card.index).Outcome);

            Assert.AreEqual(rowsBefore + 1, Save.stockpiledItems.Count, "the caravan potion stacked");
            var bought = Save.stockpiledItems.Single(e => e.Instance.HasLot);
            Assert.AreEqual(1, bought.count);
            Assert.AreEqual(card.lot, bought.Instance.Lot);
            Assert.IsFalse(bought.Instance.IsFake);
            Assert.AreEqual(Rich - 11, Run.gold, "the potion did not cost 11");
        }

        [Test]
        public void ABoughtFakeGearCard_IsAFakeInTheBag_WithItsCountdown()
        {
            var fake = BrowseUntilTheShelfHas(e => e.kind == ShopEntryKind.Gear && e.fake, "a fake gear card");

            Assert.AreEqual(ShopOutcome.Ok, RunOrchestrator.BuyGear(fake.index).Outcome);

            var bought = Save.stockpiledItems.Single(e => e.Instance.Lot == fake.lot);
            Assert.IsTrue(bought.Instance.IsFake);
            Assert.AreEqual(3, bought.Instance.FightsLeft);
        }

        // ---- the robbery ------------------------------------------------------------------

        private static void RobAndWin(int choice)
        {
            Pick(choice);
            Assert.IsTrue(RunOrchestrator.EventFightPending, "Rob him started no fight");
            var built = RunOrchestrator.BuildFight();
            Assert.IsNotNull(built, "the robbery's fight did not build");
            RunOrchestrator.SettleFight(built.Session, won: true);
        }

        [Test]
        public void Rob_GrantsTheUnsoldCardsMinusOne_FakesStayingFake_AndEndsTheStock()
        {
            StartWithTheCaravanHere();
            Pick(Browse);
            Assert.AreEqual(ShopOutcome.Ok, RunOrchestrator.BuyGear(0).Outcome);
            var shelf = Run.shelves.Single().entries.ToList();
            var unsoldLots = shelf.Where(e => !e.sold).Select(e => e.lot).ToList();
            var fakeLots = shelf.Where(e => e.fake).Select(e => e.lot).ToList();
            RunOrchestrator.LeaveShelf();

            RobAndWin(RobAfter);

            var grantedLots = Save.stockpiledItems.Where(e => e.Instance.HasLot).Select(e => e.Instance.Lot)
                .Where(unsoldLots.Contains).ToList();
            Assert.AreEqual(4, grantedLots.Count, "six cards, one bought, one lost: four handed over");

            foreach (var entry in Save.stockpiledItems.Where(e => grantedLots.Contains(e.Instance.Lot)))
            {
                Assert.AreEqual(fakeLots.Contains(entry.Instance.Lot), entry.Instance.IsFake,
                    $"{entry.Instance}: its fake flag changed hands");
            }

            var loss = Run.eventResultEffects.Single(e => e.Kind == EventEffectKind.TakeShelf);
            string lostLot = unsoldLots.Single(l => !grantedLots.Contains(l));
            Assert.AreEqual(shelf.Single(e => e.lot == lostLot).contentId, loss.Item, "the line names another card");

            Assert.AreEqual("robbed", Run.eventPageId);
            Assert.AreEqual(0, Run.shelves.Count, "finish did not end the stock");
            CollectionAssert.Contains(Run.eventsSeen, Caravan);
        }

        [Test]
        public void Rob_WithNothingLeft_LosesNothing_AndTheLineSaysSo()
        {
            StartWithTheCaravanHere();
            Pick(Browse);
            for (int i = 0; i < ShopStock.GearCount; i++) RunOrchestrator.BuyGear(i);
            RunOrchestrator.BuyConsumable(0);
            RunOrchestrator.BuyConsumable(1);
            Assert.IsTrue(RunOrchestrator.CurrentShopStock.All(e => e.sold), "fixture: the shelf is not empty");
            int rows = Save.stockpiledItems.Count;
            RunOrchestrator.LeaveShelf();

            RobAndWin(RobAfter);

            Assert.AreEqual(rows, Save.stockpiledItems.Count, "the robbery handed something over");
            var loss = Run.eventResultEffects.Single(e => e.Kind == EventEffectKind.TakeShelf);
            Assert.AreEqual("", loss.Item);
            StringAssert.Contains("Nothing left to lose in the scuffle",
                EventEffectSummary.Describe(Run.eventResultEffects, id => id));
        }

        [Test]
        public void RobbingBeforeBrowsing_RollsTheStockThere_AndHandsOverFiveOfSix()
        {
            StartWithTheCaravanHere();
            int rows = Save.stockpiledItems.Count;

            RobAndWin(Rob);

            Assert.AreEqual(rows + 5, Save.stockpiledItems.Count, "six cards, one lost: five new bag rows");
            Assert.AreEqual(5, Run.eventResultEffects.Count(e => e.Kind == EventEffectKind.Item));
            Assert.AreEqual(0, Run.shelves.Count);
        }
    }
}
