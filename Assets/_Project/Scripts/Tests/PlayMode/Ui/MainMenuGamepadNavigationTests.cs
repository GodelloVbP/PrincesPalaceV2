using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Stats;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PrincesPalace.PlayModeTests
{
    // Phase 3a of docs/GAMEPAD_NAVIGATION_PLAN.md, screen 2:
    // MainMenuController owns ONE NavContext shared with SaveSlotController's
    // own buttons, reconfigured (never re-pushed) whenever the Load
    // list opens or closes -- see MainMenuController.RefreshNavigation's own
    // header. Driven through the REAL production dispatcher (scripted
    // BaseInput via inputOverride, `yield return null`, assert resulting
    // state), the same shape every other file in this family uses.
    public class MainMenuGamepadNavigationTests
    {
        private string _root;
        private MainMenuController _menu;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-mainmenu-nav-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            NavSceneReuse.AfterTest(_input);
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // THE MAIN MENU IS SHARED ACROSS THIS FIXTURE (SharedScene). What a
        // test can leave on it is one of two stacked panels open and a
        // Continue button computed for the previous test's save root; both
        // are put back here through the menu's own close buttons and its
        // public RefreshContinue, topmost panel first (HandleCancel's order).
        // The Load list needs nothing: it re-reads the saves in its own
        // OnEnable. NavSceneReuse puts back the input and the focus
        // memory.
        private IEnumerator LoadMenu()
        {
            yield return SharedScene.Ensure("MainMenu");

            _menu = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the main menu scene has no MainMenuController");

            if (NavSceneReuse.Reused)
            {
                if (IsOpen("ResetConfirmPanel")) Find("ResetConfirmNoButton").onClick.Invoke();
                if (IsOpen("SettingsPanel")) Find("CloseSettingsButton").onClick.Invoke();
                if (IsOpen("SaveSlotPanel")) Find("CloseSaveSlotButton").onClick.Invoke();
                _menu.RefreshContinue();
            }

            _input = NavSceneReuse.TakeOverInput();
            NavSceneReuse.ForgetFocusMemory();

            // Two frames either way: on a fresh load, Start() has run inside
            // Ensure but the dispatcher has not yet read the scripted input;
            // on reuse, the first Process() reselects the entry out of the
            // null selection ForgetFocusMemory left.
            yield return null;
            yield return null;
        }

        private static bool IsOpen(string panel) => Resources.FindObjectsOfTypeAll<Transform>()
            .Any(t => t.name == panel && t.gameObject.scene.IsValid() && t.gameObject.activeSelf);

        // A save at the given slot, present on disk before the scene loads --
        // SaveSlotFlowTests' own pattern (CurrentSlot, Forget, read CurrentSave,
        // SaveCurrent), reused rather than re-invented.
        private static void SeedASave(int slot)
        {
            SaveSlotManager.CurrentSlot = slot;
            SaveSlotManager.Forget();
            var save = SaveSlotManager.CurrentSave;
            Assert.IsNotNull(save, "CurrentSave should never be null");
            SaveSlotManager.SaveCurrent();
            SaveSlotManager.Forget();
        }

        private static Button Find(string name) =>
            Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b => b.name == name && b.gameObject.scene.IsValid());

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator Move(float vertical)
        {
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Vertical = 0f;
        }

        [UnityTest]
        public IEnumerator EntryIsNewRunButton_WhenNoSaveExists()
        {
            SharedScene.MarkDirty("asserts the entry Start() picks on a freshly loaded menu, not one a reset put back");
            yield return LoadMenu();

            Assert.AreEqual(Find("NewRunButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "with no save to continue, New Run is the first (and topmost) enabled button");
        }

        [UnityTest]
        public IEnumerator EntryIsContinueButton_WhenASaveExists()
        {
            SeedASave(0);
            SharedScene.MarkDirty("asserts the entry Start() picks from a save present before the load, not one RefreshContinue put back");
            yield return LoadMenu();

            Assert.AreEqual(Find("ContinueButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "Continue sits above New Run/Load/Quit on screen and should be the entry when a save exists to continue");
        }

        [UnityTest]
        public IEnumerator Down_FromContinue_ReachesNewRun()
        {
            SeedASave(0);
            yield return LoadMenu();

            EventSystem.current.SetSelectedGameObject(Find("ContinueButton").gameObject);
            yield return null;

            yield return Move(-1f);
            Assert.AreEqual(Find("NewRunButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "Down from Continue should reach New Run");
        }

        [UnityTest]
        public IEnumerator Down_FromNewRun_ReachesLoad()
        {
            yield return LoadMenu();

            EventSystem.current.SetSelectedGameObject(Find("NewRunButton").gameObject);
            yield return null;

            yield return Move(-1f);
            Assert.AreEqual(Find("LoadButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "the column is Continue, New Run, Load, Settings, Quit");
        }

        // C4. Each of these leaves a save behind or navigates, so the shared
        // menu is dirtied; the hub load is stubbed through Navigation.LoadOverride.
        private IEnumerator PressNewRun(System.Collections.Generic.List<string> navigated)
        {
            yield return LoadMenu();
            Navigation.LoadOverride = scene => navigated.Add(scene);

            Find("NewRunButton").onClick.Invoke();
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewRun_WithNoSaves_EntersSlotZeroAndGoesToTheHub()
        {
            SharedScene.MarkDirty("New Run writes a save and navigates");
            var navigated = new System.Collections.Generic.List<string>();
            yield return PressNewRun(navigated);

            Assert.AreEqual(0, SaveSlotManager.CurrentSlot);
            Assert.IsTrue(SaveSystem.SlotExists(0), "an empty slot becomes a new save");
            CollectionAssert.AreEqual(new[] { Navigation.Hub }, navigated);
        }

        [UnityTest]
        public IEnumerator NewRun_WithSlotsZeroAndOneFilled_EntersSlotTwo()
        {
            SeedASave(0);
            SeedASave(1);
            SharedScene.MarkDirty("New Run writes a save and navigates");
            var navigated = new System.Collections.Generic.List<string>();
            yield return PressNewRun(navigated);

            Assert.AreEqual(2, SaveSlotManager.CurrentSlot);
            Assert.IsTrue(SaveSystem.SlotExists(2));
            CollectionAssert.AreEqual(new[] { Navigation.Hub }, navigated);
        }

        [UnityTest]
        public IEnumerator NewRun_WithEverySlotFull_OpensTheListAndOverwritesNothing()
        {
            for (int slot = 0; slot < SaveSystem.SlotCount; slot++) SeedASave(slot);
            SharedScene.MarkDirty("seeded a full roster");
            var navigated = new System.Collections.Generic.List<string>();
            yield return PressNewRun(navigated);

            Assert.IsTrue(IsOpen("SaveSlotPanel"), "all five full: New Run hands over to the Load list");
            Assert.IsEmpty(navigated, "it never enters a slot on its own");
        }

        [UnityTest]
        public IEnumerator Continue_ShowsTheSavesCharacterFloorAndPlaytime_OnASecondLine()
        {
            SeedASave(0);
            SharedScene.MarkDirty("asserts the subline Start() wrote from a save present before the load");
            yield return LoadMenu();

            var subline = Resources.FindObjectsOfTypeAll<TMPro.TMP_Text>()
                .First(t => t.name == "ContinueSubline" && t.gameObject.scene.IsValid());
            var facts = SaveSlotDetail.For(0);

            Assert.AreEqual(facts.CharacterName + " - Floor " + facts.Floor + " - " +
                PlaytimeFormat.Describe(facts.PlaySeconds), subline.text);
            Assert.IsNotEmpty(facts.CharacterName, "a fresh save names its lead character");
        }

        [UnityTest]
        public IEnumerator WithoutASave_ContinueIsHiddenAndNewRunTakesItsPlace()
        {
            yield return LoadMenu();

            var continueRect = (RectTransform)Find("ContinueButton").transform;
            var newRunRect = (RectTransform)Find("NewRunButton").transform;
            Assert.IsFalse(continueRect.gameObject.activeSelf);

            var column = (RectTransform)newRunRect.parent;
            Assert.AreEqual(190f, column.anchoredPosition.y + newRunRect.anchoredPosition.y +
                newRunRect.rect.height * 0.5f, 0.5f,
                "with Continue hidden the column starts at New Run: its top edge sits where Continue's did");
        }

        // A separate test from the one above only because each claim reads
        // better alone -- chaining the two presses would work too (LoadMenu
        // lifts uGUI's 0.1s re-press gate; JourneyFixture.Move has the
        // measurement).
        [UnityTest]
        public IEnumerator Down_FromLoad_ReachesSettings()
        {
            yield return LoadMenu();

            EventSystem.current.SetSelectedGameObject(Find("LoadButton").gameObject);
            yield return null;

            yield return Move(-1f);
            Assert.AreEqual(Find("SettingsButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "Down from Load should reach Settings");
        }

        [UnityTest]
        public IEnumerator Down_FromSettings_ReachesExit()
        {
            yield return LoadMenu();

            EventSystem.current.SetSelectedGameObject(Find("SettingsButton").gameObject);
            yield return null;

            yield return Move(-1f);
            Assert.AreEqual(Find("ExitButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "Down from Settings should reach Quit");
        }

        // C7/C8. Settings is the Options pane in a modal: the same controller
        // and GameSettings persistence as the system menu's tab.
        private IEnumerator OpenSettings()
        {
            yield return LoadMenu();

            EventSystem.current.SetSelectedGameObject(Find("SettingsButton").gameObject);
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();
            yield return null; // reselection onto the first row
        }

        [UnityTest]
        public IEnumerator Submit_OnSettings_OpensThePanel_WithTheFirstRowSelected()
        {
            yield return OpenSettings();

            Assert.IsTrue(IsOpen("SettingsPanel"), "Submit on Settings opens the Options modal");
            Assert.AreEqual("OptionsRowsound", Selected.name, "entry is the first Options row");
        }

        [UnityTest]
        public IEnumerator ChangingASettingInTheModal_PersistsThroughGameSettings()
        {
            float saved = GameSettings.SoundVolume;
            try
            {
                GameSettings.SetSoundVolume(0.5f);
                yield return OpenSettings();

                yield return Step(1f, 0f);

                Assert.AreEqual(0.55f, GameSettings.SoundVolume, 0.001f, "Right on the Sound row raises it by 5%");
                Assert.AreEqual(0.55f, PlayerPrefs.GetFloat("settings.soundVolume"), 0.001f,
                    "and the change is written to PlayerPrefs, which is what the next boot loads");
            }
            finally
            {
                GameSettings.SetSoundVolume(saved);
            }
        }

        [UnityTest]
        public IEnumerator CancelInsideSettings_ClosesIt_AndFocusReturnsToTheSettingsItem()
        {
            yield return OpenSettings();

            _input.CancelDown = true;
            yield return DriveFrame();
            yield return null;

            Assert.IsFalse(IsOpen("SettingsPanel"), "Cancel closes the Settings modal");
            Assert.AreEqual(Find("SettingsButton").gameObject, Selected,
                "focus goes back to the item that opened it");
        }

        [UnityTest]
        public IEnumerator DownThroughTheRows_ReachesRestoreDefaults_ThenClose()
        {
            yield return OpenSettings();

            int rowCount = Object.FindObjectsByType<OptionRow>(FindObjectsInactive.Include)
                .Count(r => r.gameObject.scene.IsValid());
            Assert.Greater(rowCount, 1, "fixture: the pane has rows to walk");

            for (int i = 0; i < rowCount - 1; i++) yield return Step(0f, -1f);
            Assert.AreEqual("OptionsRowbattlespeed", Selected.name, "fixture: the walk ends on the last row");

            yield return Step(0f, -1f);
            Assert.AreEqual("OptionsRestoreDefaults", Selected.name, "the last row's Down leads to Restore defaults");

            yield return Step(0f, -1f);
            Assert.AreEqual("CloseSettingsButton", Selected.name, "and Down again to Close");
        }

        [UnityTest]
        public IEnumerator Up_FromNewRun_ClampsRatherThanWraps_WhenNoSaveExists()
        {
            yield return LoadMenu();

            var play = Find("NewRunButton");
            EventSystem.current.SetSelectedGameObject(play.gameObject);
            yield return null;

            yield return Move(1f);

            Assert.AreEqual(play.gameObject, EventSystem.current.currentSelectedGameObject,
                "List is clamp by the owner default -- with no Continue shown, New Run is first and Up should not wrap to Quit");
        }

        // The required action (docs/GAMEPAD_NAVIGATION_PLAN.md phase 3's own
        // per-screen test list): pressing Continue, with the resulting
        // Navigation.Go stubbed through Navigation.LoadOverride (SaveSlotFlowTests'
        // own pattern) rather than actually loading the Hub scene.
        [UnityTest]
        public IEnumerator Submit_OnContinueButton_EntersTheSeededSlot_ExactlyOnce()
        {
            SeedASave(2);
            yield return LoadMenu();

            var navigated = new System.Collections.Generic.List<string>();
            Navigation.LoadOverride = scene => navigated.Add(scene);

            EventSystem.current.SetSelectedGameObject(Find("ContinueButton").gameObject);
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(2, SaveSlotManager.CurrentSlot, "Continue should enter the slot it advertised");
            Assert.AreEqual(1, navigated.Count, "Continue should navigate to the hub exactly once");
            Assert.AreEqual(Navigation.Hub, navigated[0]);
        }

        [UnityTest]
        public IEnumerator Submit_OnLoadButton_OpensTheSaveSlotPanel_AndEntryBecomesTheFirstSlot()
        {
            yield return LoadMenu();

            EventSystem.current.SetSelectedGameObject(Find("LoadButton").gameObject);
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();

            var saveSlotPanel = GameObject.Find("SaveSlotPanel") ?? Resources.FindObjectsOfTypeAll<Transform>()
                .Select(t => t.gameObject).FirstOrDefault(go => go.name == "SaveSlotPanel" && go.scene.IsValid());
            Assert.IsNotNull(saveSlotPanel, "no SaveSlotPanel in the scene");
            Assert.IsTrue(saveSlotPanel.activeSelf, "Submit on Load should open the save-slot panel, same as a click");

            // The reselection is NEXT frame (NavigationInputModule.
            // ReselectIfOutsideDeclaredSet, run once per Process() after
            // Reconfigure has already changed the declared set) -- the same
            // one-frame-later resolution every other file in this family
            // relies on rather than forcing selection on an ordinary Reconfigure.
            yield return null;

            Assert.AreEqual(Find("Slot0Button").gameObject, EventSystem.current.currentSelectedGameObject,
                "opening the save-slot panel should move the entry onto its first slot");
        }

        [UnityTest]
        public IEnumerator CancelOnTheBaseMenu_IsADeliberateNoOp()
        {
            yield return LoadMenu();

            var play = Find("NewRunButton");
            EventSystem.current.SetSelectedGameObject(play.gameObject);
            yield return null;

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.AreEqual(play.gameObject, EventSystem.current.currentSelectedGameObject,
                "there is nowhere to go back to from the base menu -- Cancel should do nothing");
        }

        [UnityTest]
        public IEnumerator CancelOnTheBaseMenu_PlaysNoCloseCue_ButWithTheLoadPanelOpenPlaysItOnce()
        {
            yield return LoadMenu();
            var played = new System.Collections.Generic.List<Sound>();
            SoundController.Observer = s => played.Add(s);
            try
            {
                EventSystem.current.SetSelectedGameObject(Find("NewRunButton").gameObject);
                yield return null;

                _input.CancelDown = true;
                yield return DriveFrame();
                CollectionAssert.IsEmpty(played, "Cancel at the base menu closes nothing, so it makes no close sound");

                EventSystem.current.SetSelectedGameObject(Find("LoadButton").gameObject);
                yield return null;
                _input.SubmitDown = true;
                yield return DriveFrame();
                yield return null;
                played.Clear();

                _input.CancelDown = true;
                yield return DriveFrame();
                CollectionAssert.AreEqual(new[] { Sound.MenuClose }, played);
            }
            finally
            {
                SoundController.Observer = null;
            }
        }

        [UnityTest]
        public IEnumerator CancelInsideTheSaveSlotPanel_ClosesItAndRestoresTheMenuEntry()
        {
            yield return LoadMenu();

            EventSystem.current.SetSelectedGameObject(Find("LoadButton").gameObject);
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();
            yield return null; // let the reselection onto Slot0Button land first

            var saveSlotPanel = Resources.FindObjectsOfTypeAll<Transform>()
                .Select(t => t.gameObject).FirstOrDefault(go => go.name == "SaveSlotPanel" && go.scene.IsValid());
            Assert.IsTrue(saveSlotPanel.activeSelf, "the panel should be open before this test presses Cancel");

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(saveSlotPanel.activeSelf, "Cancel inside the save-slot panel should close it, same as CloseSaveSlotButton");

            yield return null; // the reselection back onto the base menu is next frame

            Assert.AreEqual(Find("NewRunButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "closing the panel should restore the base menu's own entry (the first shown item)");
        }

        // ---- the Load list: cards, per-card Delete, confirm dialog (C5) ------
        //
        // Same shared NavContext as the tests above, reconfigured across the
        // three surfaces MainMenuController.RefreshNavigation's own header
        // names: base menu, Load list, confirm dialog.

        private IEnumerator OpenSaveSlotPanel()
        {
            yield return LoadMenu();

            EventSystem.current.SetSelectedGameObject(Find("LoadButton").gameObject);
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();
            yield return null; // reselection onto Slot0Button
        }

        private IEnumerator Step(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;

            // One neutral frame: the dispatcher's flick gate re-arms only
            // once the stick has returned below MoveThreshold.
            yield return null;
        }

        private static GameObject Selected => EventSystem.current.currentSelectedGameObject;

        [UnityTest]
        public IEnumerator Right_FromAFilledCard_SelectsItsDelete_AndLeftComesBack()
        {
            SeedASave(0);
            yield return OpenSaveSlotPanel();

            EventSystem.current.SetSelectedGameObject(Find("Slot0Button").gameObject);
            yield return null;

            yield return Step(1f, 0f);
            Assert.AreEqual(Find("Slot0DeleteButton").gameObject, Selected, "Right from a filled card reaches its Delete");

            yield return Step(-1f, 0f);
            Assert.AreEqual(Find("Slot0Button").gameObject, Selected, "Left from the Delete returns to its card");
        }

        [UnityTest]
        public IEnumerator Right_FromAnEmptyCard_GoesNowhere()
        {
            yield return OpenSaveSlotPanel();

            EventSystem.current.SetSelectedGameObject(Find("Slot0Button").gameObject);
            yield return null;

            yield return Step(1f, 0f);
            Assert.AreEqual(Find("Slot0Button").gameObject, Selected, "an empty slot has no Delete to step onto");
        }

        [UnityTest]
        public IEnumerator UpAndDown_FromADelete_MoveToTheNeighbouringCards()
        {
            SeedASave(0);
            SeedASave(1);
            yield return OpenSaveSlotPanel();

            EventSystem.current.SetSelectedGameObject(Find("Slot0DeleteButton").gameObject);
            yield return null;
            yield return Step(0f, -1f);
            Assert.AreEqual(Find("Slot1Button").gameObject, Selected, "Down from the first Delete reaches the second card");

            EventSystem.current.SetSelectedGameObject(Find("Slot1DeleteButton").gameObject);
            yield return null;
            yield return Step(0f, 1f);
            Assert.AreEqual(Find("Slot0Button").gameObject, Selected, "Up from the second Delete reaches the first card");
        }

        [UnityTest]
        public IEnumerator Down_FromTheLastCard_ReachesCancel()
        {
            yield return OpenSaveSlotPanel();

            var last = Find($"Slot{SaveSystem.SlotCount - 1}Button");
            EventSystem.current.SetSelectedGameObject(last.gameObject);
            yield return null;

            yield return Step(0f, -1f);
            Assert.AreEqual(Find("CloseSaveSlotButton").gameObject, Selected,
                "Down from the last card should reach the footer's Cancel");
        }

        private IEnumerator OpenTheConfirmDialogOnSlotZero()
        {
            SeedASave(0);
            yield return OpenSaveSlotPanel();

            EventSystem.current.SetSelectedGameObject(Find("Slot0DeleteButton").gameObject);
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();
            yield return null; // reselection onto the dialog's entry
        }

        [UnityTest]
        public IEnumerator Submit_OnADeleteButton_OpensTheConfirmDialog_WithNoAsEntry()
        {
            yield return OpenTheConfirmDialogOnSlotZero();

            Assert.IsTrue(IsOpen("ResetConfirmPanel"), "Submit on a Delete should open the confirm dialog, same as a click");
            Assert.AreEqual(Find("ResetConfirmNoButton").gameObject, Selected,
                "the confirm dialog's entry should be No, not the destructive hold button");
        }

        [UnityTest]
        public IEnumerator CancelInsideTheConfirmDialog_DismissesIt_AndFocusReturnsToTheDelete()
        {
            yield return OpenTheConfirmDialogOnSlotZero();

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(IsOpen("ResetConfirmPanel"), "Cancel inside the dialog should dismiss it, same as No");
            Assert.IsTrue(SaveSystem.SlotExists(0), "dismissing the dialog through Cancel must not delete the slot");
            Assert.IsTrue(IsOpen("SaveSlotPanel"), "the Load list stays open underneath");

            yield return null;

            Assert.AreEqual(Find("Slot0DeleteButton").gameObject, Selected,
                "focus goes back to the Delete that opened the dialog");
        }

        [UnityTest]
        public IEnumerator HoldingSubmitOnYesForTheFullHold_DeletesTheSlot_AndContinueGoesAway()
        {
            yield return OpenTheConfirmDialogOnSlotZero();
            Assert.IsTrue(Find("ContinueButton").gameObject.activeSelf, "fixture: the only save should be offered by Continue");

            var yes = Find("ResetConfirmYesButton");
            var hold = yes.GetComponent<HoldToConfirm>();
            EventSystem.current.SetSelectedGameObject(yes.gameObject);

            var submit = false;
            HoldToConfirm.SubmitLevelOverride = () => submit;
            hold.Tick(0f);
            submit = true;
            hold.Tick(0f);
            hold.Tick(1.2f);

            Assert.IsFalse(SaveSystem.SlotExists(0), "a full pad hold deletes the slot");
            Assert.IsFalse(IsOpen("ResetConfirmPanel"), "the dialog closes once the slot is gone");
            Assert.IsFalse(Find("ContinueButton").gameObject.activeSelf, "with its save gone, Continue has nothing to offer");
            Assert.IsFalse(Find("Slot0DeleteButton").gameObject.activeSelf, "an emptied slot loses its Delete");

            yield return null;
            Assert.AreEqual(Find("Slot0Button").gameObject, Selected, "focus lands on that slot's card");
        }

        [UnityTest]
        public IEnumerator ReleasingSubmitBeforeTheHoldCompletes_DeletesNothing()
        {
            yield return OpenTheConfirmDialogOnSlotZero();

            var yes = Find("ResetConfirmYesButton");
            var hold = yes.GetComponent<HoldToConfirm>();
            EventSystem.current.SetSelectedGameObject(yes.gameObject);

            var submit = false;
            HoldToConfirm.SubmitLevelOverride = () => submit;
            hold.Tick(0f);
            submit = true;
            hold.Tick(0f);
            hold.Tick(1.1f);
            submit = false;
            hold.Tick(0f);

            Assert.IsTrue(SaveSystem.SlotExists(0), "letting go at 1.1 s of a 1.2 s hold must keep the save");
            Assert.IsTrue(IsOpen("ResetConfirmPanel"), "and the dialog stays for another try");
        }
    }
}
