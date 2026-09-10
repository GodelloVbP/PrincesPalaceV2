using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // THE SHOP'S MUTATIONS, AND WHAT THEY PROMISE WHEN THEY FAIL.
    //
    // PlayMode rather than EditMode because every one of these reads
    // ContentDatabase and writes a save file, both Core. No scene is loaded.
    //
    // The shop is opened directly through EnsureShopStock rather than by
    // walking to a generated Shop node: what these tests are about is the
    // three-part shape of a mutation (validate / apply / persist once), and
    // which node the party happens to be standing on is not part of it.
    // ArriveAt's own branch is covered by ShopArrivalTests below.
    public class ShopMutationTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-shop-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            RunOrchestrator.RefuseShopMutationForTest = null;
        }

        [TearDown]
        public void RestoreSaveRoot()
        {
            RunOrchestrator.RefuseShopMutationForTest = null;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // A run standing somewhere with a shelf in front of it and enough
        // gold to buy anything on it.
        private static void OpenAShop(int gold = 5000)
        {
            RunManager.StartRun(4242UL);
            RunManager.Run.gold = gold;
            RunOrchestrator.EnsureShopStock();
        }

        private static ShopStockEntry FirstBuyable(int section) =>
            RunOrchestrator.CurrentShopStock.FirstOrDefault(e => e.section == section && !e.noOffer && !e.sold);

        private static int BagCount(string itemId) =>
            InventoryOps.Count(SaveSlotManager.CurrentSave.stockpiledItems, itemId);

        // ---- rolling and restoring -------------------------------------------

        [Test]
        public void ArrivingRollsAShelfAndComingBackFindsTheSameOne()
        {
            OpenAShop();

            var first = RunOrchestrator.CurrentShopStock.Select(e => e.contentId + ":" + e.price).ToList();
            Assert.AreEqual(RunManager.Run.currentNodeId, RunManager.Run.shopNodeId);
            Assert.AreEqual(ShopStock.StockVersion, RunManager.Run.shopStockVersion);
            Assert.AreEqual(ShopStock.SectionCount, RunManager.Run.shopRerollsUsed.Length);

            // Same node, same stock, forever -- otherwise quitting mid-visit
            // is a free reroll.
            RunOrchestrator.EnsureShopStock();

            CollectionAssert.AreEqual(first,
                RunOrchestrator.CurrentShopStock.Select(e => e.contentId + ":" + e.price).ToList());
        }

        // THE RULE, PINNED TO THE THING THAT ACTUALLY KEEPS IT.
        //
        // "An open shop from an older build is left exactly as it was rolled
        // and the new generator applies at the next node: a shop that
        // reshuffles itself because the game updated is a free reroll granted
        // by a patch note." That rule holds, and run.shopStockVersion is not
        // why -- the field is written in three places and compared in none. It
        // holds because the shelf is STORED and EnsureShopStock returns early
        // while one is open, whatever version rolled it.
        //
        // So the rule gets a test of its own rather than resting on an unread
        // field. Passes today; it is here so that a future ReconcileShopStock
        // that decides to re-roll a stale shelf fails rather than reads the
        // field's header as "already handled".
        [Test]
        public void AShelfRolledByAnOlderGeneratorIsLeftAsItWasRolled()
        {
            OpenAShop();
            var asRolled = RunOrchestrator.CurrentShopStock
                .Select(e => e.section + "/" + e.index + ":" + e.contentId + ":" + e.price)
                .ToList();

            RunManager.Run.shopStockVersion = ShopStock.StockVersion - 1;

            SaveSlotManager.CurrentSave.Reconcile();
            RunOrchestrator.EnsureShopStock();

            CollectionAssert.AreEqual(asRolled,
                RunOrchestrator.CurrentShopStock
                    .Select(e => e.section + "/" + e.index + ":" + e.contentId + ":" + e.price)
                    .ToList(),
                "a shelf reshuffled itself because the build moved on, which is a free reroll");
        }

        [Test]
        public void TheShelfIsEveryCountTheSectionsDeclare()
        {
            OpenAShop();

            Assert.AreEqual(ShopStock.GearCount,
                RunOrchestrator.CurrentShopStock.Count(e => e.section == ShopStock.GearSection));
            Assert.AreEqual(ShopStock.BookCount,
                RunOrchestrator.CurrentShopStock.Count(e => e.section == ShopStock.BookSection));
            Assert.AreEqual(ShopStock.RelicCount,
                RunOrchestrator.CurrentShopStock.Count(e => e.section == ShopStock.RelicSection));
        }

        [Test]
        public void LeavingClearsTheShelfAndTheRoom()
        {
            OpenAShop();
            RunOrchestrator.LeaveShop();

            Assert.IsEmpty(RunManager.Run.shopStock);
            Assert.AreEqual(-1, RunManager.Run.shopNodeId);
            Assert.IsTrue(RunManager.Run.shopRerollsUsed.All(n => n == 0));
            Assert.IsEmpty(RunOrchestrator.CurrentShopStock);
            CollectionAssert.Contains(RunManager.Run.clearedNodeIds, RunManager.Run.currentNodeId);
        }

        // ---- buying ------------------------------------------------------------

        [Test]
        public void BuyingGearSpendsGoldMarksTheCardAndFillsTheBag()
        {
            OpenAShop();
            var card = FirstBuyable(ShopStock.GearSection);
            Assert.IsNotNull(card, "The gear shelf had nothing on it, so nothing below means anything.");

            int goldBefore = RunManager.Run.gold;
            int heldBefore = BagCount(card.contentId);

            var result = RunOrchestrator.BuyGear(card.index);

            Assert.AreEqual(ShopOutcome.Ok, result.Outcome);
            Assert.AreEqual(-card.price, result.GoldDelta);
            Assert.AreEqual(goldBefore - card.price, RunManager.Run.gold);
            Assert.IsTrue(card.sold);
            Assert.AreEqual(heldBefore + 1, BagCount(card.contentId));
        }

        // A purchase deliberately does NOT auto-equip, unlike TakeOffer: the
        // player already decided, with money, against the other cards.
        [Test]
        public void BuyingGearDoesNotEquipIt()
        {
            OpenAShop();
            var card = FirstBuyable(ShopStock.GearSection);
            var save = SaveSlotManager.CurrentSave;
            var wornBefore = save.ActiveSquad().SelectMany(c => c.equipment.EquippedItemIds()).ToList();

            RunOrchestrator.BuyGear(card.index);

            var wornAfter = save.ActiveSquad().SelectMany(c => c.equipment.EquippedItemIds()).ToList();
            CollectionAssert.AreEqual(wornBefore, wornAfter);
            Assert.AreEqual(1, InventoryOps.CountAt(save.stockpiledItems, card.contentId, card.plus,
                card.modifiers, card.riftTier));
        }

        [Test]
        public void ACardCanOnlyBeBoughtOnce()
        {
            OpenAShop();
            var card = FirstBuyable(ShopStock.GearSection);
            RunOrchestrator.BuyGear(card.index);

            int goldBefore = RunManager.Run.gold;
            var again = RunOrchestrator.BuyGear(card.index);

            Assert.AreEqual(ShopOutcome.Refused, again.Outcome);
            Assert.AreEqual(ShopRefusal.NothingToBuy, again.Reason);
            Assert.AreEqual(goldBefore, RunManager.Run.gold);
        }

        [Test]
        public void AnEmptyPurseRefusesAndChangesNothing()
        {
            OpenAShop();
            var card = FirstBuyable(ShopStock.GearSection);
            RunManager.Run.gold = card.price - 1;
            int bagBefore = BagCount(card.contentId);

            var result = RunOrchestrator.BuyGear(card.index);

            Assert.AreEqual(ShopRefusal.NotEnoughGold, result.Reason);
            Assert.AreEqual(card.price - 1, RunManager.Run.gold);
            Assert.IsFalse(card.sold);
            Assert.AreEqual(bagBefore, BagCount(card.contentId));
        }

        [Test]
        public void AnIndexOutsideTheSectionIsRefused()
        {
            OpenAShop();

            Assert.AreEqual(ShopRefusal.BadIndex, RunOrchestrator.BuyGear(-1).Reason);
            Assert.AreEqual(ShopRefusal.BadIndex, RunOrchestrator.BuyGear(ShopStock.GearCount).Reason);
            Assert.AreEqual(ShopRefusal.BadIndex, RunOrchestrator.BuyRelic(ShopStock.RelicCount).Reason);
            Assert.AreEqual(ShopRefusal.BadIndex, RunOrchestrator.RerollSection(ShopStock.SectionCount).Reason);
        }

        [Test]
        public void BuyingARelicAppendsItToTheRunWithNoSlotToFill()
        {
            OpenAShop();
            var card = FirstBuyable(ShopStock.RelicSection);
            Assert.IsNotNull(card);

            int goldBefore = RunManager.Run.gold;
            int heldBefore = RunManager.Run.relicIds.Count;

            var result = RunOrchestrator.BuyRelic(card.index);

            Assert.AreEqual(ShopOutcome.Ok, result.Outcome);
            Assert.AreEqual(goldBefore - card.price, RunManager.Run.gold);
            Assert.AreEqual(heldBefore + 1, RunManager.Run.relicIds.Count);
            CollectionAssert.Contains(RunManager.Run.relicIds, card.contentId);
        }

        // Gate 3 gives the book shelf real content (the five bookTier > 0
        // spells) -- the shelf is the same BookCount width either way, but
        // it is no longer all NO OFFER by construction.
        [Test]
        public void TheBookShelfOffersRealBooksInGate3()
        {
            OpenAShop();

            var books = RunOrchestrator.CurrentShopStock
                .Where(e => e.section == ShopStock.BookSection).ToList();

            Assert.AreEqual(ShopStock.BookCount, books.Count);
            Assert.IsTrue(books.Any(e => !e.noOffer), "the five book-eligible spells should fill at least one card");
        }

        [Test]
        public void BuyingABookAppendsToUnassignedSpellBooksAndChargesGold()
        {
            OpenAShop();
            var run = RunManager.Run;
            int goldBefore = run.gold;

            var entry = FirstBuyable(ShopStock.BookSection);
            Assert.IsNotNull(entry, "gate 3's book shelf should offer at least one real card");

            var result = RunOrchestrator.BuyBook(entry.index);

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(goldBefore - entry.price, run.gold);
            CollectionAssert.Contains(run.unassignedSpellBooks, entry.contentId);
            Assert.IsTrue(RunOrchestrator.CurrentShopStock.First(e => e.section == ShopStock.BookSection
                && e.index == entry.index).sold);
        }

        // ---- selling -------------------------------------------------------------

        [Test]
        public void SellingRemovesFromTheBagAndCreditsTheRun()
        {
            OpenAShop();
            var save = SaveSlotManager.CurrentSave;
            save.stockpiledItems.Clear();

            var sellable = Content.ContentDatabase.Offerable.First();
            InventoryOps.Add(save.stockpiledItems, sellable.id, 3, plus: 1);

            int expected = ShopPricing.SellPrice(ShopPricing.GearPrice(sellable.tier, 1, 0));
            int goldBefore = RunManager.Run.gold;

            var result = RunOrchestrator.Sell(0, 2);

            Assert.AreEqual(ShopOutcome.Ok, result.Outcome);
            Assert.AreEqual(expected * 2, result.GoldDelta);
            Assert.AreEqual(goldBefore + expected * 2, RunManager.Run.gold);
            Assert.AreEqual(1, InventoryOps.CountAt(save.stockpiledItems, sellable.id, 1));
        }

        // A SALE IS GOLD THE RUN EARNED, and it was the one credit in the game
        // that never went through RunManager.BankPayout -- `run.gold += paid`,
        // straight onto the field. goldEarned was credited from a settled
        // fight's ledger call, so a bag sold off in a shop moved "Gold held"
        // and left "Gold earned" behind, which is exactly the comparison Run
        // statistics puts side by side.
        [Test]
        public void ASaleIsGoldTheRunEarned()
        {
            OpenAShop();
            var save = SaveSlotManager.CurrentSave;
            save.stockpiledItems.Clear();

            var sellable = Content.ContentDatabase.Offerable.First();
            InventoryOps.Add(save.stockpiledItems, sellable.id, 1, plus: 1);

            int expected = ShopPricing.SellPrice(ShopPricing.GearPrice(sellable.tier, 1, 0));
            Assert.Greater(expected, 0, "fixture: the item has to be worth something to sell");

            int heldBefore = RunManager.Run.gold;
            int earnedBefore = RunManager.Run.goldEarned;

            var result = RunOrchestrator.Sell(0, 1);

            Assert.AreEqual(ShopOutcome.Ok, result.Outcome);
            Assert.AreEqual(heldBefore + expected, RunManager.Run.gold);
            Assert.AreEqual(earnedBefore + expected, RunManager.Run.goldEarned,
                "the sale credited the purse without the run admitting it earned anything");

            // Held is NOT asserted to stay under earned here: OpenAShop hands
            // the run 5000 gold as a fixture rather than earning it, so this
            // run's held already exceeds anything it could have earned. The
            // invariant that matters -- every credit moves both -- is the two
            // deltas above, and RunManagerTests pins the treasure half.
        }

        [Test]
        public void SellingMoreThanTheBagHoldsIsRefusedAndCreditsNothing()
        {
            OpenAShop();
            var save = SaveSlotManager.CurrentSave;
            save.stockpiledItems.Clear();

            var sellable = Content.ContentDatabase.Offerable.First();
            InventoryOps.Add(save.stockpiledItems, sellable.id, 1);

            int goldBefore = RunManager.Run.gold;
            var result = RunOrchestrator.Sell(0, 2);

            Assert.AreEqual(ShopRefusal.NotInBag, result.Reason);
            Assert.AreEqual(goldBefore, RunManager.Run.gold);
            Assert.AreEqual(1, InventoryOps.Count(save.stockpiledItems, sellable.id));
        }

        // Two copies at different plus are different objects and must be
        // priced separately -- InventoryOps stacks on the full
        // (itemId, plus, modifierIds, riftTier) key, and so does the sell.
        [Test]
        public void TwoCopiesAtDifferentPlusSellForDifferentMoney()
        {
            OpenAShop();
            var save = SaveSlotManager.CurrentSave;
            save.stockpiledItems.Clear();

            var sellable = Content.ContentDatabase.Offerable.First();
            InventoryOps.Add(save.stockpiledItems, sellable.id, 1, plus: 0);
            InventoryOps.Add(save.stockpiledItems, sellable.id, 1, plus: 4);

            int plain = RunOrchestrator.SellPriceOf(save.stockpiledItems[0]);
            int honed = RunOrchestrator.SellPriceOf(save.stockpiledItems[1]);

            Assert.Greater(honed, plain);
        }

        // ---- rerolling -------------------------------------------------------------

        [Test]
        public void RerollingOneSectionMovesThatSectionAndNoOther()
        {
            OpenAShop();

            var relicsBefore = RunOrchestrator.CurrentShopStock
                .Where(e => e.section == ShopStock.RelicSection)
                .Select(e => e.contentId).ToList();

            int goldBefore = RunManager.Run.gold;
            var result = RunOrchestrator.RerollSection(ShopStock.GearSection);

            Assert.AreEqual(ShopOutcome.Ok, result.Outcome);
            Assert.AreEqual(goldBefore - ShopPricing.RerollPrice(0), RunManager.Run.gold);
            Assert.AreEqual(1, RunManager.Run.shopRerollsUsed[ShopStock.GearSection]);
            Assert.AreEqual(0, RunManager.Run.shopRerollsUsed[ShopStock.RelicSection]);

            CollectionAssert.AreEqual(relicsBefore, RunOrchestrator.CurrentShopStock
                .Where(e => e.section == ShopStock.RelicSection)
                .Select(e => e.contentId).ToList(),
                "Rerolling the gear shelf moved the relics beside it.");
        }

        [Test]
        public void TheRerollPriceDoublesPerSectionIndependently()
        {
            OpenAShop();

            Assert.AreEqual(15, RunOrchestrator.RerollPriceFor(ShopStock.GearSection));

            RunOrchestrator.RerollSection(ShopStock.GearSection);

            Assert.AreEqual(30, RunOrchestrator.RerollPriceFor(ShopStock.GearSection));
            Assert.AreEqual(15, RunOrchestrator.RerollPriceFor(ShopStock.RelicSection),
                "One section's rerolls priced another section's.");
        }

        [Test]
        public void ARerollTheRunCannotAffordIsRefusedAndRollsNothing()
        {
            OpenAShop(gold: 5);
            var before = RunOrchestrator.CurrentShopStock.Select(e => e.contentId).ToList();

            var result = RunOrchestrator.RerollSection(ShopStock.GearSection);

            Assert.AreEqual(ShopRefusal.NotEnoughGold, result.Reason);
            Assert.AreEqual(5, RunManager.Run.gold);
            Assert.AreEqual(0, RunManager.Run.shopRerollsUsed[ShopStock.GearSection]);
            CollectionAssert.AreEqual(before, RunOrchestrator.CurrentShopStock.Select(e => e.contentId).ToList());
        }

        // ---- atomicity -------------------------------------------------------------

        // THE PRE-STATE END. A refusal at the end of step 1 must leave
        // everything exactly as it was: gold, the card, the bag, the reroll
        // counter. Every refusal in a shop mutation is decided before
        // anything is touched, and this is the test that says so for all four
        // at once.
        [Test]
        public void ARefusalAtTheEndOfValidationTouchesNothing()
        {
            OpenAShop();
            var save = SaveSlotManager.CurrentSave;
            save.stockpiledItems.Clear();
            var sellable = Content.ContentDatabase.Offerable.First();
            InventoryOps.Add(save.stockpiledItems, sellable.id, 2);

            int gold = RunManager.Run.gold;
            int relics = RunManager.Run.relicIds.Count;
            int bag = InventoryOps.Count(save.stockpiledItems, sellable.id);
            var shelf = RunOrchestrator.CurrentShopStock.Select(e => e.contentId + ":" + e.sold).ToList();

            // Only the cards that could actually be bought -- a NO OFFER
            // card refuses in step 1 for its own reason, which is a
            // different test.
            var gearCard = FirstBuyable(ShopStock.GearSection);
            var relicCard = FirstBuyable(ShopStock.RelicSection);

            RunOrchestrator.RefuseShopMutationForTest = () => true;

            var results = new List<ShopResult>
            {
                RunOrchestrator.Sell(0, 1),
                RunOrchestrator.RerollSection(ShopStock.GearSection),
            };
            if (gearCard != null) results.Add(RunOrchestrator.BuyGear(gearCard.index));
            if (relicCard != null) results.Add(RunOrchestrator.BuyRelic(relicCard.index));

            foreach (var result in results)
            {
                Assert.AreEqual(ShopOutcome.Refused, result.Outcome);
                Assert.AreEqual(ShopRefusal.Injected, result.Reason);
                Assert.AreEqual(0, result.GoldDelta);
            }

            Assert.AreEqual(gold, RunManager.Run.gold);
            Assert.AreEqual(relics, RunManager.Run.relicIds.Count);
            Assert.AreEqual(bag, InventoryOps.Count(save.stockpiledItems, sellable.id));
            Assert.IsTrue(RunManager.Run.shopRerollsUsed.All(n => n == 0));
            CollectionAssert.AreEqual(shelf,
                RunOrchestrator.CurrentShopStock.Select(e => e.contentId + ":" + e.sold).ToList());
        }

        // THE OTHER END. An unwritable save root cannot be caught -- Save
        // swallows its own exception on purpose, because a torn save is a
        // worse answer than a logged one. What it can now do is SAY SO, and
        // the mutation still stands whole in memory.
        [Test]
        public void AFailedWriteLeavesTheMutationWholeInMemoryAndSaysSo()
        {
            OpenAShop();
            var card = FirstBuyable(ShopStock.GearSection);
            int goldBefore = RunManager.Run.gold;

            // A FILE where the save root should be, so Save's write throws
            // inside its own try and answers false.
            string blocked = Path.Combine(_root, "not-a-directory");
            File.WriteAllText(blocked, "");
            SaveSystem.RootOverride = blocked;

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("could not be written"));

            var result = RunOrchestrator.BuyGear(card.index);

            Assert.AreEqual(ShopOutcome.AppliedNotPersisted, result.Outcome);
            Assert.AreEqual(goldBefore - card.price, RunManager.Run.gold, "The mutation must be whole in memory.");
            Assert.IsTrue(card.sold);
            Assert.AreEqual(1, InventoryOps.CountAt(SaveSlotManager.CurrentSave.stockpiledItems,
                card.contentId, card.plus, card.modifiers, card.riftTier));

            SaveSystem.RootOverride = _root;
        }

        // ---- reconciliation ------------------------------------------------------------

        [Test]
        public void StockBelongingToAnotherNodeIsDroppedOnLoad()
        {
            OpenAShop();
            var save = SaveSlotManager.CurrentSave;
            save.activeRun.shopNodeId = save.activeRun.currentNodeId + 99;

            save.Reconcile();

            Assert.IsEmpty(save.activeRun.shopStock);
            Assert.AreEqual(-1, save.activeRun.shopNodeId);
            Assert.IsTrue(save.activeRun.shopRerollsUsed.All(n => n == 0));
        }

        [Test]
        public void ACardWhoseContentVanishedBecomesNoOfferInPlace()
        {
            OpenAShop();
            var save = SaveSlotManager.CurrentSave;
            var card = save.activeRun.shopStock.First(e => e.section == ShopStock.GearSection && !e.noOffer);
            int index = card.index;
            card.contentId = "an_item_that_content_no_longer_has";

            save.Reconcile();

            var after = save.activeRun.shopStock.First(e => e.section == ShopStock.GearSection && e.index == index);
            Assert.IsTrue(after.noOffer, "A card that cannot resolve must become NO OFFER rather than disappear.");
            Assert.AreEqual(ShopStock.GearCount,
                save.activeRun.shopStock.Count(e => e.section == ShopStock.GearSection),
                "Removing the entry would renumber every card the screen binds by index.");
        }

        [Test]
        public void ASoldCardStaysSoldEvenIfItsContentVanished()
        {
            OpenAShop();
            var save = SaveSlotManager.CurrentSave;
            var card = save.activeRun.shopStock.First(e => e.section == ShopStock.GearSection && !e.noOffer);
            RunOrchestrator.BuyGear(card.index);
            card.contentId = "an_item_that_content_no_longer_has";

            save.Reconcile();

            Assert.IsTrue(card.sold, "The gold was spent; the card is not a refund.");
            Assert.IsFalse(card.noOffer);
        }

        [Test]
        public void ARerollArrayOfTheWrongLengthIsNormalisedRatherThanTrusted()
        {
            OpenAShop();
            var save = SaveSlotManager.CurrentSave;
            save.activeRun.shopRerollsUsed = new[] { 4 };

            save.Reconcile();

            Assert.AreEqual(ShopStock.SectionCount, save.activeRun.shopRerollsUsed.Length);
            Assert.AreEqual(4, save.activeRun.shopRerollsUsed[0], "What was there was carried, not discarded.");

            save.activeRun.shopRerollsUsed = null;
            save.Reconcile();
            Assert.AreEqual(ShopStock.SectionCount, save.activeRun.shopRerollsUsed.Length);
        }

        // A save written before the shop existed has no shop fields at all,
        // and "no shop" is the correct reading of it -- not a crash, and not
        // a phantom shelf.
        [Test]
        public void ASaveWithNoShopFieldsLoadsAsNoShop()
        {
            var save = SaveData.CreateNew();
            save.activeRun = new RunSnapshot { hasRun = true, currentNodeId = 3 };

            Assert.DoesNotThrow(() => save.Reconcile());
            Assert.IsEmpty(save.activeRun.shopStock);
            Assert.AreEqual(-1, save.activeRun.shopNodeId);
            Assert.AreEqual(ShopStock.SectionCount, save.activeRun.shopRerollsUsed.Length);
        }

        // ---- the whole catalogue --------------------------------------------------------

        // §2b's "selling is not supposed to make you rich", over every item
        // the game can actually offer rather than over the three depths §2c
        // happened to tabulate.
        [Test]
        public void SellingIsWorseThanBuyingForEveryOfferableItemAtEveryRoll()
        {
            foreach (var item in Content.ContentDatabase.Offerable)
            {
                for (int plus = 0; plus <= 5; plus++)
                {
                    for (int rift = 0; rift <= 3; rift++)
                    {
                        int buy = ShopPricing.GearPrice(item.tier, plus, rift);
                        Assert.Less(ShopPricing.SellPrice(buy), buy,
                            $"{item.id} at tier {item.tier}, +{plus}, rift {rift} sold for at least its price.");
                    }
                }
            }
        }
    }
}
