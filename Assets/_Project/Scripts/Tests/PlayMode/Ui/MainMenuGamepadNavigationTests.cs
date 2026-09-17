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
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private IEnumerator LoadMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the main menu scene's EventSystem is not running NavigationInputModule");
            // Set BEFORE letting any frame run, not after -- a previously-
            // loaded scene's own EventSystem can still be EventSystem.current
            // (CancelOpensSystemMenuTests' own hazard note) for at least one
            // frame after this scene's own load, and EventSystem.Update only
            // drives its OWN input module while it is the current instance:
            // reassigning this late would mean the freshly loaded module's
            // Process() never ran during the frames this method itself waits
            // out below, and the entry it should have selected would still
            // read null afterward.
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            // Start() runs one frame after activation; a second frame lets
            // the dispatcher's own post-Process reselection land on the
            // entry Start()'s RegisterNavContext just declared.
            yield return null;
            yield return null;

            _menu = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the main menu scene has no MainMenuController");
        }

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
            yield return LoadMenu();

            Assert.AreEqual(Find("PlayButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "with no save to continue, Play is the first (and topmost) enabled button");
        }

        [UnityTest]
        public IEnumerator EntryIsContinueButton_WhenASaveExists()
        {
            SeedASave(0);
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

        // A SEPARATE test, freshly loaded, rather than a second press
        // chained onto the one above -- StandaloneInputModule's own move
        // debounce tracks a real-time timestamp across consecutive moves on
        // one module instance (SystemMenuGamepadNavigationTests' own header
        // makes the identical call for the identical reason), so a second
        // press moments later by test-clock time risks being read as a
        // repeat still inside the framework's own repeat delay.
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
    }
}
