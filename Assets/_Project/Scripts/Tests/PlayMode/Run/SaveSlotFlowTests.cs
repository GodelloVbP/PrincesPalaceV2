using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // Save slots, driven through the real scene.
    //
    // Every one of these writes files, so they run against a throwaway root
    // rather than the player's actual saves -- SaveSystem.RootOverride exists
    // for exactly this. A test suite that can delete someone's progress is not
    // a test suite anyone will run.
    public class SaveSlotFlowTests
    {
        private string _root;

        // Where a click WOULD have gone. Recorded rather than followed: picking
        // a slot now enters the hub, and actually loading it mid-test destroys
        // the very objects every assertion below is about.
        private readonly List<string> _navigated = new List<string>();

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _navigated.Clear();
            Navigation.LoadOverride = scene => _navigated.Add(scene);

            _root = Path.Combine(Path.GetTempPath(), "pp-slot-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
        }

        [TearDown]
        public void RestoreSaveRoot()
        {
            SharedScene.AfterTest();
            Navigation.Reset();

            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // THE MENU IS SHARED ACROSS THIS FIXTURE (SharedScene). What a test
        // opened is closed again here through the buttons a hand would press,
        // topmost first, so every test starts on the bare root the way a fresh
        // load does. The slot lists need nothing: both re-read the disk in
        // OnEnable, and [SetUp] has already pointed it at a fresh root.
        //
        // Continue does need something, and gets no reset here: it is read
        // once at Start, so the tests about it mark the scene dirty instead of
        // letting a helper recompute it and hide a Start that stopped doing so.
        private static IEnumerator LoadMenu()
        {
            yield return SharedScene.Ensure("MainMenu");

            if (IsOpen("ResetConfirmPanel")) Click("ResetConfirmNoButton");
            if (IsOpen("ManageSavesPanel")) Click("CloseManageSavesButton");
            if (IsOpen("SaveSlotPanel")) Click("CloseSaveSlotButton");
        }

        private static bool IsOpen(string name)
        {
            var go = Named(name);
            return go != null && go.activeSelf;
        }

        private static GameObject Named(string name) =>
            Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(go => go.name == name && go.scene.IsValid());

        private static void Click(string name) => Named(name).GetComponent<Button>().onClick.Invoke();

        // Delete is a HOLD now, not a click -- see ResetProgressController's
        // own header on why onClick fires on release regardless of what the
        // press was for. Driven the same way SystemMenuExitsTests already
        // drives ExitsScreen's identical abandon-hold: through the component's
        // own public Begin/Advance rather than simulated pointer events, which
        // is still "pressing what a player presses" and not reaching into
        // controller state -- HoldToConfirm IS the press, from the button's
        // point of view.
        private static void HoldToDelete(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}' in the scene");

            var hold = go.GetComponent<HoldToConfirm>();
            Assert.IsNotNull(hold, $"'{name}' has no hold behaviour attached");

            hold.Begin();
            hold.Advance(ResetProgressController.HoldSeconds);
        }

        // A slot's card is several TMP_Text nodes now (Number/Top/Detail/Gold,
        // see MainMenuScreen.AddCardContent), not one label on the button --
        // so these read one named field each rather than the first TMP_Text
        // GetComponentInChildren happens to find.
        private static string SlotTop(int slot) =>
            Named($"Slot{slot}Top").GetComponent<TMP_Text>().text;

        private static string SlotGold(int slot) =>
            Named($"Slot{slot}Gold").GetComponent<TMP_Text>().text;

        private static string ResetTop(int slot) =>
            Named($"ResetSlot{slot}Top").GetComponent<TMP_Text>().text;

        [UnityTest]
        public IEnumerator AnUntouchedSlotReadsAsEmpty()
        {
            yield return LoadMenu();
            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (.claude/rules/tests.md "Writing tests").
            yield return null;
            yield return null;

            Assert.AreEqual("New Descent", SlotTop(0));
        }

        [UnityTest]
        public IEnumerator ChoosingASlot_CreatesItAndSetsTheCurrentSlot()
        {
            yield return LoadMenu();
            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (.claude/rules/tests.md "Writing tests").
            yield return null;
            yield return null;

            Assert.IsFalse(SaveSystem.SlotExists(2));
            Click("Slot2Button");

            Assert.IsTrue(SaveSystem.SlotExists(2), "picking an empty slot should write it immediately");
            Assert.AreEqual(2, SaveSlotManager.CurrentSlot);
            Assert.AreNotEqual("New Descent", SlotTop(2), "the top line should stop inviting a new run");
            StringAssert.Contains("GOLD", SlotGold(2), "the gold figure should show once the slot is filled");
        }

        [UnityTest]
        public IEnumerator BothScreensDescribeTheSameSlotIdentically()
        {
            // v1's actual bug: the Play screen and the Options screen formatted
            // this independently and disagreed, and one of them printed an `exp`
            // field nothing ever wrote. One manifest entry serves both now.
            yield return LoadMenu();
            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (.claude/rules/tests.md "Writing tests").
            yield return null;
            yield return null;
            Click("Slot1Button");
            Click("ManageSavesButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (.claude/rules/tests.md "Writing tests").
            yield return null;
            yield return null;

            Assert.AreEqual(SlotTop(1), ResetTop(1));
        }

        [UnityTest]
        public IEnumerator DeleteAsksFirst_AndDoesNotDeleteUntilConfirmed()
        {
            yield return LoadMenu();
            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (.claude/rules/tests.md "Writing tests").
            yield return null;
            yield return null;
            Click("Slot0Button");
            Click("ManageSavesButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (.claude/rules/tests.md "Writing tests").
            yield return null;
            yield return null;

            var confirm = Named("ResetConfirmPanel");
            Assert.IsFalse(confirm.activeSelf, "the confirmation should start closed");

            Click("ResetSlot0DeleteButton");

            Assert.IsTrue(confirm.activeSelf, "Delete should open the confirmation, not delete");
            Assert.IsTrue(SaveSystem.SlotExists(0), "the slot must survive until the deletion is confirmed");
        }

        // THE WHOLE POINT of a hold over a click: letting go early must cost
        // nothing, and two separate partial holds must not add up to a whole
        // one. Same claim SystemMenuExitsTests.LettingGoEarlyLosesEverything
        // already pins for ExitsScreen's identical gesture.
        [UnityTest]
        public IEnumerator ReleasingTheHoldEarly_DeletesNothing()
        {
            yield return LoadMenu();
            Click("PlayButton");
            yield return null;
            yield return null;
            Click("Slot0Button");
            Click("ManageSavesButton");
            yield return null;
            yield return null;

            Click("ResetSlot0DeleteButton");

            var hold = Named("ResetConfirmYesButton").GetComponent<HoldToConfirm>();
            Assert.IsNotNull(hold, "the confirm button has no hold behaviour attached");

            hold.Begin();
            hold.Advance(ResetProgressController.HoldSeconds * 0.9f);
            hold.Cancel();

            Assert.IsTrue(SaveSystem.SlotExists(0), "a released hold must not delete anything");

            // AND A FRESH HOLD STARTS FROM ZERO, not from where the last one
            // was let go -- two 90% holds must never sum to one whole one.
            hold.Begin();
            hold.Advance(ResetProgressController.HoldSeconds * 0.9f);

            Assert.IsTrue(SaveSystem.SlotExists(0),
                "a released hold carried its progress into the next one, so two partial holds deleted the slot");
        }

        [UnityTest]
        public IEnumerator ConfirmingActuallyDeletes_AndCancellingDoesNot()
        {
            yield return LoadMenu();
            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (.claude/rules/tests.md "Writing tests").
            yield return null;
            yield return null;
            Click("Slot0Button");
            Click("Slot1Button");
            Click("ManageSavesButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (.claude/rules/tests.md "Writing tests").
            yield return null;
            yield return null;

            Click("ResetSlot0DeleteButton");
            Click("ResetConfirmNoButton");
            Assert.IsTrue(SaveSystem.SlotExists(0), "Cancel must leave the slot intact");
            Assert.IsFalse(Named("ResetConfirmPanel").activeSelf, "Cancel should close the confirmation");

            Click("ResetSlot1DeleteButton");
            HoldToDelete("ResetConfirmYesButton");
            Assert.IsFalse(SaveSystem.SlotExists(1), "Delete should actually delete once confirmed");
            Assert.AreEqual("Empty", ResetTop(1), "and the row should say so");
        }

        [UnityTest]
        public IEnumerator DeletingAnAlreadyEmptySlot_DoesNotOpenAPointlessConfirmation()
        {
            // Confirmations that appear when nothing is at stake are how people
            // learn to click through the ones that matter.
            yield return LoadMenu();
            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (.claude/rules/tests.md "Writing tests").
            yield return null;
            yield return null;
            Click("ManageSavesButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (.claude/rules/tests.md "Writing tests").
            yield return null;
            yield return null;

            Click("ResetSlot3DeleteButton");

            Assert.IsFalse(Named("ResetConfirmPanel").activeSelf);
        }

        [UnityTest]
        public IEnumerator ReopeningPlayAfterADeletion_ShowsTheSlotAsEmptyAgain()
        {
            // No cross-panel event wires these together - the Play list refreshes
            // in OnEnable, and Manage Saves' own Back button is what triggers
            // it now: Back re-shows SaveSlotPanel directly rather than handing
            // the player back to the main menu root to reopen Play themselves.
            yield return LoadMenu();
            Click("PlayButton");
            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (.claude/rules/tests.md "Writing tests").
            yield return null;
            yield return null;
            Click("Slot4Button");
            Click("CloseSaveSlotButton");

            Click("PlayButton");
            Click("ManageSavesButton");
            // First-ever activation of Manage Saves in this test, so its own
            // listeners need the same one-frame grace Play's did above.
            yield return null;
            yield return null;
            Click("ResetSlot4DeleteButton");
            HoldToDelete("ResetConfirmYesButton");
            Click("CloseManageSavesButton");

            Assert.AreEqual("New Descent", SlotTop(4));
        }
    
        [UnityTest]
        public IEnumerator ChoosingASlotEntersTheHub()
        {
            // The other half of the wiring: the slot is written first, so a
            // player who picks one and immediately quits still finds it there.
            yield return LoadMenu();

            Click("PlayButton");

            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (.claude/rules/tests.md "Writing tests").
            yield return null;
            yield return null;

            Click("Slot0Button");

            CollectionAssert.Contains(_navigated, Navigation.Hub);
            Assert.IsTrue(SaveSystem.SlotExists(0), "the slot must be written BEFORE the scene changes");
        }

        private static GameObject FindGo(string name) =>
            Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(g => g.name == name && g.scene.IsValid());

        private static void ClickByName(string name)
        {
            var go = FindGo(name);
            Assert.IsNotNull(go, $"no object named '{name}' in the scene");

            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, $"'{name}' has no Button to press");
            button.onClick.Invoke();
        }

        // ---- deleting a slot actually deletes it ---------------------------

        [UnityTest]
        public IEnumerator DeletingTheSlotYouAreIn_DoesNotComeBackOnTheNextSave()
        {
            // DRIVEN THROUGH THE BUTTONS, not by calling what the controller
            // calls. The first version of this test ran SaveSlotManager.Forget
            // itself with a comment saying "what ConfirmDelete now does" -- so
            // it passed whether or not ConfirmDelete did anything: a test doing
            // for production what production must do for itself.
            //
            // The bug: SaveSlotManager holds ONE SaveData for the current slot
            // and SaveCurrent writes it wherever CurrentSlot points. Deleting
            // the file left that copy alone, so the next Persist -- starting a
            // run, buying an upgrade, anything -- wrote the whole save back with
            // every item and every worn piece of gear on it. It looked like the
            // delete had worked, because the slot list refreshes off the disk it
            // had just been removed from.
            const int Slot = 2;

            SaveSlotManager.CurrentSlot = Slot;
            SaveSlotManager.Forget();
            var save = SaveSlotManager.CurrentSave;
            save.roster.FirstOrDefault()?.equipment
                .Set(Domain.Equipment.EquipmentSlot.Weapon1, "health_potion", plus: 2);
            SaveSlotManager.SaveCurrent();
            Assert.IsTrue(SaveSystem.SlotExists(Slot), "fixture check: the slot should exist before deletion");

            yield return LoadMenu();

            // BY NAME, because the controller's fields are `internal` and
            // InternalsVisibleTo is granted to the Editor assembly only -- a
            // PlayMode test is deliberately kept from reaching into controller
            // state, so it has to press what a player presses.
            // OPEN PLAY THEN MANAGE SAVES, because that is where the reset UI
            // lives and both panels start inactive -- so
            // ResetProgressController.Start has not run and none of its
            // listeners are wired until a player reaches it. Found by this
            // test failing on the assertion below rather than on the one it
            // was written for, which is why that assertion is there.
            ClickByName("PlayButton");
            yield return null;
            yield return null;
            ClickByName("ManageSavesButton");
            yield return null;
            yield return null;

            ClickByName($"ResetSlot{Slot}DeleteButton");
            yield return null;

            // PROVES THE CLICK LANDED. Start() wires these listeners a frame
            // after activation, so a test that clicks too early presses a button
            // with nothing attached -- and then the assertion at the bottom
            // fails for a reason that has nothing to do with what it is testing.
            var confirm = FindGo("ResetConfirmPanel");
            Assert.IsNotNull(confirm, "the reset screen has no ResetConfirmPanel");
            Assert.IsTrue(confirm.activeInHierarchy,
                "the delete button did not open the confirmation, so its listener was not wired yet " +
                "and nothing below this line is testing what it claims to");

            HoldToDelete("ResetConfirmYesButton");

            // The write that used to resurrect it.
            SaveSlotManager.SaveCurrent();

            Assert.IsFalse(SaveSystem.SlotExists(Slot),
                "the deleted slot was written straight back from the in-memory copy, so the save and " +
                "everything on it survived being deleted");
        }

        // ---- Manage Saves lives inside the slot flow now, not on the root --

        [UnityTest]
        public IEnumerator BackFromManageSaves_ReturnsToTheSlotList()
        {
            yield return LoadMenu();
            Click("PlayButton");
            yield return null;
            yield return null;
            Click("ManageSavesButton");
            yield return null;
            yield return null;

            Click("CloseManageSavesButton");

            Assert.IsFalse(Named("ManageSavesPanel").activeSelf);
            Assert.IsTrue(Named("SaveSlotPanel").activeSelf,
                "Back should return to the list it was opened from, not to the main menu root");
        }

        // ---- Continue --------------------------------------------------------

        [UnityTest]
        public IEnumerator WithNoSaveAtAll_ContinueDoesNotShow()
        {
            SharedScene.MarkDirty("asserts Continue as Start computed it, with nothing on disk");
            yield return LoadMenu();

            var continueGo = Named("ContinueButton");
            Assert.IsNotNull(continueGo, "the button is built unconditionally and toggled at runtime");
            Assert.IsFalse(continueGo.activeSelf, "nothing has ever been saved, so there is nothing to continue");
        }

        [UnityTest]
        public IEnumerator WithASave_ContinueNamesItsSlot()
        {
            SaveSystem.Save(SaveData.CreateNew(), 2);

            SharedScene.MarkDirty("asserts Continue as Start computed it from the save written above");
            yield return LoadMenu();

            var continueGo = Named("ContinueButton");
            Assert.IsTrue(continueGo.activeSelf);
            Assert.AreEqual("Continue - Slot 3", continueGo.GetComponentInChildren<TMP_Text>(true).text);
        }

        [UnityTest]
        public IEnumerator ContinueEntersTheSameSlotItNames()
        {
            SaveSystem.Save(SaveData.CreateNew(), 1);
            SharedScene.MarkDirty("clicks the slot Start read for Continue from the save written above");
            yield return LoadMenu();

            Click("ContinueButton");

            Assert.AreEqual(1, SaveSlotManager.CurrentSlot);
            CollectionAssert.Contains(_navigated, Navigation.Hub);
        }

        [UnityTest]
        public IEnumerator DeletingTheContinueSlot_HidesContinueOnceItIsGone()
        {
            // The one path that makes RefreshContinue's existence worth
            // asserting: Continue is computed once at Start, and the only
            // thing that can make it wrong INSIDE one visit to the menu is
            // deleting the very slot it is offering.
            SaveSystem.Save(SaveData.CreateNew(), 0);
            SharedScene.MarkDirty("needs Continue visible from Start's read of the save written above");
            yield return LoadMenu();

            var continueGo = Named("ContinueButton");
            Assert.IsTrue(continueGo.activeSelf, "fixture: Continue should start visible");

            Click("PlayButton");
            yield return null;
            yield return null;
            Click("ManageSavesButton");
            yield return null;
            yield return null;

            Click("ResetSlot0DeleteButton");
            HoldToDelete("ResetConfirmYesButton");

            Assert.IsFalse(continueGo.activeSelf,
                "the only save just got deleted, so Continue has nothing left to offer");
        }

        // ---- nothing may write a slot before the player has picked one ------

        // AUDIT #117. Continue is "which slot did I play last", and SaveSystem
        // answers it off the FILE'S OWN mtime -- so anything that writes a slot
        // before the player has chosen one answers the question wrongly, and
        // slot 0 is the only slot anything at boot can reach.
        //
        // Driven by REFLECTION over RunManager's own [RuntimeInitializeOnLoad
        // Method] members rather than by naming the method that used to do it.
        // The rule is "nothing RunManager runs at boot may write a save", not
        // "SettleAnyRunAPreviousSessionLeftBehind in particular must go": a
        // second boot hook added later under any name is caught here without
        // this test being edited, and the day there are none the loop simply
        // has nothing to invoke and the assertion still stands.
        [Test]
        public void SettlingARunLeftInSlotZeroDoesNotStealContinueFromTheSlotLastPlayed()
        {
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            // Slot 0 carries a descent a previous session walked into and never
            // came out of -- an alt-F4 mid-run, which is the only way a run
            // reaches the disk at all (RunManager's own header).
            RunManager.StartRun(20260911UL);
            RunManager.Run.roomsCleared = 1;
            SaveSlotManager.SaveCurrent();
            Assert.IsTrue(RunManager.HasRun, "fixture: slot 0 should be holding a run");

            // ...and slot 2 is the one the player actually played last.
            SaveSystem.Save(SaveData.CreateNew(), 2);

            // STATED, not waited for. Two writes inside one system-clock tick
            // carry the SAME mtime on Windows (~15ms granularity), and
            // MostRecentSlot breaks a tie toward the lower slot -- which would
            // redden this test for a reason that has nothing to do with #117.
            AgeSlotFile(0, TimeSpan.FromHours(1));
            Assert.AreEqual(2, SaveSystem.MostRecentSlot(),
                "fixture: slot 2 must be the newest file before the boot check runs");

            SaveSlotManager.Forget();
            SaveSlotManager.CurrentSlot = 0;
            RunManager.ResetForTests();
            InvokeRunManagersBootHooks();

            Assert.AreEqual(2, SaveSystem.MostRecentSlot(),
                "something RunManager runs at boot wrote slot 0, so the main menu now offers " +
                "Continue on the slot the player never chose rather than on slot 2");
        }

        // The other half of #117's answer: dropping the boot check must not
        // leave slot 0 as the one slot a run CAN survive into. Opening it is
        // what settles it, exactly as opening any other slot does.
        [Test]
        public void OpeningSlotZeroStillSettlesTheRunAPreviousSessionLeftInIt()
        {
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            RunManager.StartRun(20260911UL);
            RunManager.Run.roomsCleared = 1;
            RunManager.Run.bossesKilled.Add("boss_a");
            SaveSlotManager.SaveCurrent();

            // A fresh process: nothing cached, nothing opened yet.
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            SaveSlotManager.EnterSlot(0);

            Assert.IsFalse(RunManager.HasRun,
                "the run left in slot 0 survived being opened, so slot 0 is the one slot a descent " +
                "outlives the process in");
        }

        // save_slot_<n>.json is SaveSystem.PathForSlot's own name and it is
        // private, so this matches on the suffix rather than rebuilding the
        // path -- and asserts it found exactly one file, which is what makes a
        // renamed save file fail here loudly instead of silently skipping.
        private void AgeSlotFile(int slot, TimeSpan by)
        {
            var matches = Directory.GetFiles(_root, $"*_{slot}.json");
            Assert.AreEqual(1, matches.Length,
                $"expected exactly one save file for slot {slot} under the throwaway root");
            File.SetLastWriteTimeUtc(matches[0], DateTime.UtcNow - by);
        }

        private static void InvokeRunManagersBootHooks()
        {
            var hooks = typeof(RunManager)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length > 0)
                .ToList();

            foreach (var hook in hooks) hook.Invoke(null, null);
        }
}
}
