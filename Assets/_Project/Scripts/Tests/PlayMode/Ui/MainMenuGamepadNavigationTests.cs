using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PrincesPalace.PlayModeTests
{
    // Phase 3a of docs/GAMEPAD_NAVIGATION_PLAN.md, screen 2:
    // MainMenuController owns ONE NavContext shared with SaveSlotController's
    // own buttons, reconfigured (never re-pushed) whenever the save-slot
    // modal opens or closes -- see MainMenuController.RefreshNavigation's own
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
        // test can leave on it is one of three stacked panels open and a
        // Continue button computed for the previous test's save root; both
        // are put back here through the menu's own close buttons and its
        // public RefreshContinue, topmost panel first (HandleCancel's order).
        // The slot and manage lists need nothing: each re-reads the saves in
        // its own OnEnable. NavSceneReuse puts back the input and the focus
        // memory.
        private IEnumerator LoadMenu()
        {
            yield return SharedScene.Ensure("MainMenu");

            _menu = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the main menu scene has no MainMenuController");

            if (NavSceneReuse.Reused)
            {
                if (IsOpen("ResetConfirmPanel")) Find("ResetConfirmNoButton").onClick.Invoke();
                if (IsOpen("ManageSavesPanel")) Find("CloseManageSavesButton").onClick.Invoke();
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
        public IEnumerator EntryIsPlayButton_WhenNoSaveExists()
        {
            SharedScene.MarkDirty("asserts the entry Start() picks on a freshly loaded menu, not one a reset put back");
            yield return LoadMenu();

            Assert.AreEqual(Find("PlayButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "with no save to continue, Play is the first (and topmost) enabled button");
        }

        [UnityTest]
        public IEnumerator EntryIsContinueButton_WhenASaveExists()
        {
            SeedASave(0);
            SharedScene.MarkDirty("asserts the entry Start() picks from a save present before the load, not one RefreshContinue put back");
            yield return LoadMenu();

            Assert.AreEqual(Find("ContinueButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "Continue sits above Play/Exit on screen and should be the entry when a save exists to continue");
        }

        [UnityTest]
        public IEnumerator Down_FromContinue_ReachesPlay()
        {
            SeedASave(0);
            yield return LoadMenu();

            EventSystem.current.SetSelectedGameObject(Find("ContinueButton").gameObject);
            yield return null;

            yield return Move(-1f);
            Assert.AreEqual(Find("PlayButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "Down from Continue should reach Play");
        }

        // A separate test from the one above only because each claim reads
        // better alone -- chaining the two presses would work too (LoadMenu
        // lifts uGUI's 0.1s re-press gate; JourneyFixture.Move has the
        // measurement).
        [UnityTest]
        public IEnumerator Down_FromPlay_ReachesExit()
        {
            yield return LoadMenu();

            EventSystem.current.SetSelectedGameObject(Find("PlayButton").gameObject);
            yield return null;

            yield return Move(-1f);
            Assert.AreEqual(Find("ExitButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "Down from Play should reach Exit");
        }

        [UnityTest]
        public IEnumerator Up_FromPlay_ClampsRatherThanWraps_WhenNoSaveExists()
        {
            yield return LoadMenu();

            var play = Find("PlayButton");
            EventSystem.current.SetSelectedGameObject(play.gameObject);
            yield return null;

            yield return Move(1f);

            Assert.AreEqual(play.gameObject, EventSystem.current.currentSelectedGameObject,
                "List is clamp by the owner default -- with no Continue shown, Play is first and Up should not wrap to Exit");
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
        public IEnumerator Submit_OnPlayButton_OpensTheSaveSlotPanel_AndEntryBecomesTheFirstSlot()
        {
            yield return LoadMenu();

            EventSystem.current.SetSelectedGameObject(Find("PlayButton").gameObject);
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();

            var saveSlotPanel = GameObject.Find("SaveSlotPanel") ?? Resources.FindObjectsOfTypeAll<Transform>()
                .Select(t => t.gameObject).FirstOrDefault(go => go.name == "SaveSlotPanel" && go.scene.IsValid());
            Assert.IsNotNull(saveSlotPanel, "no SaveSlotPanel in the scene");
            Assert.IsTrue(saveSlotPanel.activeSelf, "Submit on Play should open the save-slot panel, same as a click");

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

            var play = Find("PlayButton");
            EventSystem.current.SetSelectedGameObject(play.gameObject);
            yield return null;

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.AreEqual(play.gameObject, EventSystem.current.currentSelectedGameObject,
                "there is nowhere to go back to from the base menu -- Cancel should do nothing");
        }

        [UnityTest]
        public IEnumerator CancelInsideTheSaveSlotPanel_ClosesItAndRestoresPlayButton()
        {
            yield return LoadMenu();

            EventSystem.current.SetSelectedGameObject(Find("PlayButton").gameObject);
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

            Assert.AreEqual(Find("PlayButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "closing the panel should restore the base menu's own entry");
        }

        // ---- Manage Saves / reset-confirm (AUDIT.md #159) -------------------
        //
        // Same shared NavContext as the tests above, reconfigured across two
        // more surfaces -- MainMenuController.RefreshNavigation's own header
        // states the four-state shape this exercises.

        private IEnumerator OpenSaveSlotPanel()
        {
            yield return LoadMenu();

            EventSystem.current.SetSelectedGameObject(Find("PlayButton").gameObject);
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();
            yield return null; // reselection onto Slot0Button
        }

        private IEnumerator OpenManageSaves()
        {
            yield return OpenSaveSlotPanel();
            EventSystem.current.SetSelectedGameObject(Find("ManageSavesButton").gameObject);
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();
            yield return null; // reselection onto ResetSlot0DeleteButton
        }

        [UnityTest]
        public IEnumerator EntryIsFirstDeleteButton_WhenManageSavesOpens()
        {
            yield return OpenManageSaves();

            Assert.AreEqual(Find("ResetSlot0DeleteButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "opening Manage Saves should move the entry onto its first delete row");
        }

        [UnityTest]
        public IEnumerator Down_FromLastDeleteButton_ReachesBack()
        {
            yield return OpenManageSaves();

            int lastSlot = SaveSystem.SlotCount - 1;
            var lastDelete = Find($"ResetSlot{lastSlot}DeleteButton");
            EventSystem.current.SetSelectedGameObject(lastDelete.gameObject);
            yield return null;

            yield return Move(-1f);
            Assert.AreEqual(Find("CloseManageSavesButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "Down from the last delete row should reach Back, the explicit inter-group link");
        }

        [UnityTest]
        public IEnumerator Submit_OnADeleteButton_OpensTheConfirmDialog_WithNoAsEntry()
        {
            SeedASave(0);
            yield return OpenManageSaves();

            EventSystem.current.SetSelectedGameObject(Find("ResetSlot0DeleteButton").gameObject);
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();

            var confirmPanel = Resources.FindObjectsOfTypeAll<Transform>()
                .Select(t => t.gameObject).FirstOrDefault(go => go.name == "ResetConfirmPanel" && go.scene.IsValid());
            Assert.IsNotNull(confirmPanel, "no ResetConfirmPanel in the scene");
            Assert.IsTrue(confirmPanel.activeSelf, "Submit on a delete row should open the confirm dialog, same as a click");

            yield return null; // reselection onto the confirm dialog's entry

            Assert.AreEqual(Find("ResetConfirmNoButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "the confirm dialog's entry should be No, not the destructive Yes/hold button");
        }

        [UnityTest]
        public IEnumerator CancelInsideTheConfirmDialog_DismissesIt_WithoutDeleting()
        {
            SeedASave(0);
            yield return OpenManageSaves();

            EventSystem.current.SetSelectedGameObject(Find("ResetSlot0DeleteButton").gameObject);
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();
            yield return null; // reselection onto ResetConfirmNoButton

            var confirmPanel = Resources.FindObjectsOfTypeAll<Transform>()
                .Select(t => t.gameObject).FirstOrDefault(go => go.name == "ResetConfirmPanel" && go.scene.IsValid());

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(confirmPanel.activeSelf, "Cancel inside the confirm dialog should dismiss it, same as ResetConfirmNoButton");
            Assert.IsTrue(SaveSystem.SlotExists(0), "dismissing the confirm dialog through Cancel must not delete the slot");

            yield return null; // reselection back onto the manage-saves list

            Assert.AreEqual(Find("ResetSlot0DeleteButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "dismissing the confirm dialog should restore the manage-saves list's own entry");
        }

        [UnityTest]
        public IEnumerator CancelInsideManageSaves_ReturnsToTheSaveSlotList()
        {
            yield return OpenManageSaves();

            _input.CancelDown = true;
            yield return DriveFrame();

            var managePanel = Resources.FindObjectsOfTypeAll<Transform>()
                .Select(t => t.gameObject).FirstOrDefault(go => go.name == "ManageSavesPanel" && go.scene.IsValid());
            var saveSlotPanel = Resources.FindObjectsOfTypeAll<Transform>()
                .Select(t => t.gameObject).FirstOrDefault(go => go.name == "SaveSlotPanel" && go.scene.IsValid());

            Assert.IsFalse(managePanel.activeSelf, "Cancel inside Manage Saves should close it, same as CloseManageSavesButton");
            Assert.IsTrue(saveSlotPanel.activeSelf, "Cancel inside Manage Saves should return to the save-slot list underneath it");

            yield return null; // reselection back onto the save-slot list

            Assert.AreEqual(Find("Slot0Button").gameObject, EventSystem.current.currentSelectedGameObject,
                "returning from Manage Saves should restore the save-slot list's own entry");
        }
    }
}
