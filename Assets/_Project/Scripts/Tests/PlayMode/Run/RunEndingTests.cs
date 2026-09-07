using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // Ending a run closes its books, whichever way it ends.
    //
    // There are two ways out of a descent -- dying in it, and abandoning it
    // from the map -- and settling used to be the CALLER'S job. Only the defeat
    // path did it, so walking away threw away every ember the bosses in that
    // run had earned and every room it had cleared. Nothing reported it,
    // because a discarded snapshot looks identical whether or not anything read
    // it first.
    //
    // These drive RunManager.EndRun directly, which is now the single door both
    // paths go through.
    public class RunEndingTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-endrun-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;

        // ---- what does not survive the run --------------------------------
        //
        // Inventory never did: it lives on RunSnapshot, which EndRun replaces.
        // GEAR DID, and that was the hole -- equipment lives on the character,
        // which is profile-scoped, so wearing a run's loot was the one way to
        // carry it out. Dying fully kitted kept the kit; dying holding the same
        // items lost them.

        [Test]
        public void EndRun_TakesTheGearWithIt()
        {
            GiveTheSaveARunWorthSettling();

            var character = Save.roster.FirstOrDefault();
            Assert.IsNotNull(character, "fixture: the save has nobody to equip");

            character.equipment.Set(Domain.Equipment.EquipmentSlot.Weapon1, "health_potion", plus: 3);
            Assert.IsFalse(character.equipment.IsEmpty(Domain.Equipment.EquipmentSlot.Weapon1),
                "fixture check: the item should be worn before the run ends");

            RunManager.EndRun();

            Assert.IsTrue(character.equipment.IsEmpty(Domain.Equipment.EquipmentSlot.Weapon1),
                "the run ended and its gear stayed on the character, so wearing an item is still a way " +
                "to launder run loot into the profile");
        }

        [Test]
        public void EndRun_TakesTheInventoryWithIt()
        {
            // THE REAL INVENTORY, which is stockpiledItems.
            //
            // The first version of this test added an entry to
            // activeRun.inventory and asserted the replaced snapshot's list was
            // empty. It passed, and it proved nothing: RunSnapshot.inventory is
            // vestigial -- one stats readout reads it and no loot is ever put
            // there. The list the player actually sees is stockpiledItems, which
            // the pack draws, the fight satchel is built from, and the Reckoning
            // drops loot into, and it was surviving every run.
            //
            // Third tautology of the day and the only one that hid a live bug,
            // which is the argument for asking of every test: what does this
            // fail on?
            GiveTheSaveARunWorthSettling();

            Save.stockpiledItems.Add(new InventoryEntry("health_potion", 2));
            Assert.IsNotEmpty(Save.stockpiledItems, "fixture check: the pack should have something in it");

            RunManager.EndRun();

            CollectionAssert.IsEmpty(Save.stockpiledItems,
                "the run's haul outlived the run -- this is the list the pack draws");
        }

        // A run that got somewhere: a boss down, rooms cleared, damage dealt.
        private static void GiveTheSaveARunWorthSettling()
        {
            var run = Save.activeRun;
            run.hasRun = true;
            run.roomsCleared = 6;
            run.deepestStep = 11;
            run.gold = 180;
            run.bossesKilled.Add("forest_warden");
            run.ledger.Add(new RunLedgerEntry { characterId = "shawn", physicalDealt = 4200 });
        }

        [Test]
        public void EndingARunPaysTheEmbersItsBossesEarned()
        {
            GiveTheSaveARunWorthSettling();
            int before = Save.ActiveSquad().First().embers;

            var settlement = RunManager.EndRun();

            Assert.AreEqual(1, settlement.EmbersEarned);
            Assert.AreEqual(before + 1, Save.ActiveSquad().First().embers);
            CollectionAssert.Contains(Save.defeatedBossIds, "forest_warden");
        }

        [Test]
        public void EndingARunFoldsItsTotalsIntoTheLifetimeCounters()
        {
            // Achievements read these. Losing them on one of the two exits made
            // "clear a hundred rooms" quietly unreachable for anyone who ever
            // abandoned a descent.
            GiveTheSaveARunWorthSettling();

            RunManager.EndRun();

            Assert.AreEqual(6, Save.lifetimeRoomsCleared);
            Assert.AreEqual(11, Save.lifetimeDeepestStep);
            Assert.AreEqual(4200, Save.lifetimeDamageDealt);
            Assert.AreEqual(1, Save.lifetimeRunsEnded);
        }

        [Test]
        public void EndingARunActuallyEndsIt()
        {
            GiveTheSaveARunWorthSettling();

            RunManager.EndRun();

            Assert.IsFalse(RunManager.HasRun);
            Assert.IsFalse(Save.activeRun.hasRun);
        }

        [Test]
        public void TheRunsUnbankedGoldIsForfeited()
        {
            GiveTheSaveARunWorthSettling();
            int banked = Save.wallet.gold;

            var settlement = RunManager.EndRun();

            Assert.AreEqual(180, settlement.GoldLost);
            Assert.AreEqual(banked, Save.wallet.gold, "forfeited gold reached the banked wallet");
        }

        [Test]
        public void EndingTwiceDoesNotPayTwice()
        {
            // The second call has an empty snapshot to settle. If it paid
            // again, closing the books would be a money printer rather than a
            // settlement.
            GiveTheSaveARunWorthSettling();

            RunManager.EndRun();
            int afterFirst = Save.ActiveSquad().First().embers;
            int roomsAfterFirst = Save.lifetimeRoomsCleared;

            var second = RunManager.EndRun();

            Assert.AreEqual(0, second.EmbersEarned);
            Assert.AreEqual(afterFirst, Save.ActiveSquad().First().embers);
            Assert.AreEqual(roomsAfterFirst, Save.lifetimeRoomsCleared);
        }

        [Test]
        public void EndingARunThatDidNothingIsHarmless()
        {
            var settlement = RunManager.EndRun();

            Assert.AreEqual(0, settlement.EmbersEarned);
            Assert.AreEqual(0, settlement.GoldLost);
            Assert.IsFalse(RunManager.HasRun);
        }

        [Test]
        public void WhatWasSettledSurvivesBeingWrittenToDisk()
        {
            GiveTheSaveARunWorthSettling();

            RunManager.EndRun();
            SaveSlotManager.Forget();

            Assert.AreEqual(6, Save.lifetimeRoomsCleared, "the fold never reached the file");
            CollectionAssert.Contains(Save.defeatedBossIds, "forest_warden");
        }

        // ---- abandoning starts the NEXT run from scratch -----------------------

        // "I keep popping in at floor 1 path 3": abandoning a descent and
        // starting another put the party somewhere down the map instead of at
        // its entry, and skipped the relic draft.
        //
        // Driven through RunManager, which is the door both the abandon button
        // and the hub gate go through, so a pass here means the MODEL is clean
        // and anything still wrong is the view.
        [Test]
        public void AbandoningAndStartingAgainBeginsAtTheEntryWithNothingCarriedOver()
        {
            RunManager.StartRun(12345);
            var first = RunManager.Run;

            // Get the run somewhere: move along the map, bank gold, draft a
            // relic, clear a room.
            var choices = RunManager.Choices();
            Assert.IsNotEmpty(choices, "a fresh run should offer somewhere to go");
            RunManager.MoveTo(choices[0].Id);
            RunManager.ClearCurrentRoom();
            RunManager.BankPayout(140);
            first.relicDrafted = true;
            first.relicIds.Add("dual_wield");
            int wanderedTo = first.currentNodeId;
            int wanderedStep = first.step;

            RunManager.EndRun();
            Assert.IsFalse(RunManager.HasRun, "abandoning must leave no run behind");

            RunManager.StartRun(67890);
            var second = RunManager.Run;

            Assert.AreEqual(0, second.step, "a new descent starts at step 0");
            Assert.AreEqual(0, second.legStartStep, "a new descent starts on the first leg");
            Assert.AreEqual(1, second.floor, "a new descent starts on floor 1");
            Assert.AreEqual(RunManager.Map.Entry.Id, second.currentNodeId,
                "the party must stand at the map's ENTRY, not wherever the last run wandered to");
            Assert.AreNotEqual(wanderedTo, second.currentNodeId,
                "the new run inherited the abandoned run's position");
            Assert.IsFalse(second.relicDrafted,
                "a new descent drafts a new relic - carrying the flag over skips the draft entirely");
            CollectionAssert.IsEmpty(second.relicIds, "relics do not survive an abandoned run");
            Assert.AreEqual(0, second.gold, "unbanked gold is forfeited, not carried into the next run");
            CollectionAssert.IsEmpty(second.clearedNodeIds, "the new map starts unexplored");
        }


        // The same thing again, but ACROSS A RELOAD.
        //
        // The test above drives the in-memory save, which is not the path the
        // game takes: abandoning returns to the hub through a scene load, and
        // the hub gate reads whatever survived. RunSnapshot round-trips through
        // JsonUtility, and hasRun is an in-band flag precisely because a null
        // activeRun is never null again after one trip -- so "did the abandon
        // actually stick" is a question only a reload can answer.
        [Test]
        public void AnAbandonedRunIsStillAbandonedAfterAReload()
        {
            RunManager.StartRun(4242);
            var choices = RunManager.Choices();
            RunManager.MoveTo(choices[0].Id);
            RunManager.Run.relicDrafted = true;
            int wanderedStep = RunManager.Run.step;
            Assert.Greater(wanderedStep, 0, "the fixture needs the run to have moved off the entry");

            RunManager.EndRun();

            // Drop the cache so the next read comes off DISK, which is what a
            // scene load does.
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            Assert.IsFalse(RunManager.HasRun,
                "the abandoned run came back after a reload - the hub gate would RESUME it, which " +
                "skips the relic draft and drops the party wherever the last run stopped");

            RunManager.StartRun(9999);
            Assert.AreEqual(0, RunManager.Run.step, "the run after a reload must still start at step 0");
            Assert.AreEqual(RunManager.Map.Entry.Id, RunManager.Run.currentNodeId);
            Assert.IsFalse(RunManager.Run.relicDrafted, "a new descent drafts again");
        }


        // ---- ending nothing --------------------------------------------------
        //
        // EndRun is wired to five buttons, and only two of them are on a path
        // that guarantees a descent: the map's abandon and the defeat screen.
        // The main menu's Exit, the hub's Main Menu button and the two in
        // ExitsController all call it flat -- and the hub between runs is
        // exactly where a player equips what they just bought.
        //
        // Nothing inside EndRun asked whether there WAS a run. RunSettlement
        // guards on `run == null`, which is never true (RunSnapshot's own
        // header explains why hasRun is an in-band flag rather than a null
        // check), so the whole settlement ran against an empty snapshot and
        // the two clears after it ran unconditionally: every roster
        // character's equipment, the whole stockpile, and lifetimeRunsEnded
        // counted one more run that never happened.
        //
        // "Gear does not survive a run" cannot apply where there is no run to
        // not survive it.

        [Test]
        public void EndingWithNoRunAtAll_LeavesTheGearAndThePackAlone()
        {
            Assert.IsFalse(RunManager.HasRun,
                "fixture: this test is about the no-run path, and the save has a run");

            var character = Save.roster.FirstOrDefault();
            Assert.IsNotNull(character, "fixture: the save has nobody to equip");

            character.equipment.Set(Domain.Equipment.EquipmentSlot.Weapon1, "health_potion", plus: 3);

            Save.stockpiledItems.Clear();
            Save.stockpiledItems.Add(new InventoryEntry("health_potion", 2));

            int endedBefore = Save.lifetimeRunsEnded;

            RunManager.EndRun();

            Assert.IsFalse(character.equipment.IsEmpty(Domain.Equipment.EquipmentSlot.Weapon1),
                "the hub stripped what the player was wearing on the way to the main menu");
            Assert.AreEqual(1, Save.stockpiledItems.Count,
                "the pack was emptied by ending a run that was never started");
            Assert.AreEqual(endedBefore, Save.lifetimeRunsEnded,
                "a run that never happened was counted as one that ended");
        }
    }
}
