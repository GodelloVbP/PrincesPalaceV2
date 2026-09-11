using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;

namespace PrincesPalace.PlayModeTests
{
    // The two panels that can destroy a save, opened and closed repeatedly.
    //
    // Both wire in Start and only REFRESH in OnEnable, which is the safe shape
    // -- and the shape a later change could quietly move into OnEnable without
    // anything noticing, because everything these panels do looks identical
    // when it happens once and when it happens three times. Except the two
    // things that do not: entering a slot NAVIGATES, and a delete hold carries
    // progress that must not survive the panel going away.
    //
    // scenarios B10, B11 (docs/hunt/SCENARIOS.md). D6 is already fully asserted
    // by SaveSlotFlowTests (DeletingTheSlotYouAreIn_DoesNotComeBackOnTheNextSave
    // and DeletingTheContinueSlot_HidesContinueOnceItIsGone) and is not
    // rewritten here.
    public class SaveSlotLifecycleTests
    {
        private const int Cycles = 3;

        private string _root;
        private readonly List<string> _navigated = new List<string>();

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _navigated.Clear();
            Navigation.LoadOverride = scene => _navigated.Add(scene);

            _root = Path.Combine(Path.GetTempPath(), "pp-slot-life-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.Forget();
        }

        [TearDown]
        public void Restore()
        {
            // Any hold still running is cancelled first -- AUDIT #52: nothing
            // disables these between tests, so a hold left advancing on the
            // shared player loop fires its Completed into whoever runs next,
            // and this one's Completed DELETES A SAVE.
            foreach (var hold in Object.FindObjectsByType<HoldToConfirm>(FindObjectsInactive.Include))
            {
                hold.Cancel();
            }

            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- B10: the slot list ------------------------------------------------------

        // ENTERING A SLOT NAVIGATES, which is the one effect on this panel that
        // counts rather than settles: a listener added once per opening would
        // send three Navigation.Go(Hub) for one press. The recorder makes that
        // visible; the save write underneath it would not, because writing the
        // same slot three times looks exactly like writing it once.
        [UnityTest]
        public IEnumerator ThreeOpeningsOfTheSlotListStillEnterOneSlotPerPress()
        {
            yield return OpenTheSlotList();

            var panel = Object.FindAnyObjectByType<SaveSlotController>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "the main menu has no SaveSlotController");

            yield return Cycle(panel.gameObject);

            _navigated.Clear();
            Click("Slot0Button");
            yield return null;

            CollectionAssert.AreEqual(new[] { Navigation.Hub }, _navigated,
                "one press on a slot navigated more than once, so the slot list's listeners were " +
                "added again every time the panel was opened");
            Assert.AreEqual(0, SaveSlotManager.CurrentSlot, "the press did not enter the slot it named");
        }

        // ---- B11: the manage-saves panel and its hold ----------------------------------

        // A HALF-FINISHED DELETE MUST NOT SURVIVE ANYTHING, and this panel
        // states the claim twice: HoldToConfirm cancels itself in OnDisable,
        // and ResetProgressController calls Cancel again on the way IN rather
        // than inheriting it -- "the hold is not half-pressed the moment you
        // open it" is a claim this panel makes, so it says so.
        //
        // ONE CORRECTION TO SCENARIOS.md's B11 ROW, found while writing this
        // and worth stating because the row's wording -- "the confirm hold
        // cancelled by the disable" of the PANEL -- does not hold.
        // ResetConfirmPanel is a SIBLING of the manage-saves modal under
        // MainMenuPanel, not a child of it (MainMenuScreen: `Ui.Panel(
        // "MainMenuPanel", ..., saveSlotModal, manageSavesModal,
        // confirmModal)`), so closing the manage-saves panel disables neither
        // the confirmation nor its hold. What makes that safe is the
        // confirmation's own full-screen scrim covering the Back button
        // underneath it: containment by scrim rather than by hierarchy. The
        // disables asserted below are therefore the ones that actually happen.
        [UnityTest]
        public IEnumerator AHalfFinishedDeleteIsForgottenByEveryWayOutOfTheConfirmation()
        {
            yield return OpenTheSlotList();

            Click("Slot0Button");
            yield return null;
            yield return OpenTheSlotList();

            Click("ManageSavesButton");
            yield return null;
            yield return null;

            var reset = Object.FindAnyObjectByType<ResetProgressController>(FindObjectsInactive.Include);
            Assert.IsNotNull(reset, "the main menu has no ResetProgressController");
            Assert.IsTrue(SaveSystem.SlotExists(0), "fixture: there is no slot to delete");

            Click("ResetSlot0DeleteButton");

            var confirmation = Named("ResetConfirmPanel");
            var hold = Named("ResetConfirmYesButton").GetComponent<HoldToConfirm>();
            Assert.IsNotNull(hold, "the confirm button has no hold behaviour attached");
            Assert.IsTrue(confirmation.activeSelf, "fixture: the confirmation never opened");

            hold.Begin();
            hold.Advance(ResetProgressController.HoldSeconds * 0.9f);
            Assert.IsTrue(hold.Holding, "fixture: nothing was being held");

            // The way out a player takes: Cancel.
            Click("ResetConfirmNoButton");
            yield return null;

            Assert.IsFalse(hold.Holding, "the delete hold kept running after the confirmation closed");
            Assert.IsTrue(SaveSystem.SlotExists(0), "cancelling completed the delete");

            // And asking again arrives fresh, which is the claim the panel
            // makes explicitly rather than inheriting.
            Click("ResetSlot0DeleteButton");
            yield return null;

            Assert.AreEqual(0f, hold.Progress01, 0.0001f,
                "the confirmation reopened with its hold already nine tenths done, so one short " +
                "press finishes a delete the player only just backed out of");

            // The component's own defence, driven directly: the confirmation
            // disabled while a hold is live.
            hold.Begin();
            hold.Advance(ResetProgressController.HoldSeconds * 0.9f);
            confirmation.SetActive(false);
            yield return null;

            Assert.IsFalse(hold.Holding,
                "HoldToConfirm did not cancel itself when the confirmation was disabled, so a hold " +
                "left half-done keeps advancing on the player loop and fires into whatever is up next");
            Assert.IsTrue(SaveSystem.SlotExists(0), "the delete completed on a disabled confirmation");

            // AND THE PANEL IS STILL WIRED ONCE after a few open/close cycles:
            // the delete button opens the confirmation rather than deleting.
            yield return Cycle(reset.gameObject);

            Click("ResetSlot0DeleteButton");
            yield return null;

            Assert.IsTrue(SaveSystem.SlotExists(0),
                "pressing delete after a close/open cycle deleted the slot outright, so the panel " +
                "wired something a second time");
            Assert.IsTrue(Named("ResetConfirmPanel").activeSelf,
                "the delete press no longer opens the confirmation");
        }

        // ---- fixture ---------------------------------------------------------------------

        private static IEnumerator OpenTheSlotList()
        {
            yield return SceneManager.LoadSceneAsync(Navigation.MainMenu, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Click("PlayButton");

            // Start() runs one frame AFTER SetActive, not synchronously, so the
            // panel's listeners do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;
        }

        private static IEnumerator Cycle(GameObject panel)
        {
            for (int i = 0; i < Cycles; i++)
            {
                panel.SetActive(false);
                yield return null;
                panel.SetActive(true);
                yield return null;
            }
        }

        private static GameObject Named(string name) =>
            Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(go => go.name == name && go.scene.IsValid());

        private static void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}' in the scene");
            go.GetComponent<Button>().onClick.Invoke();
        }
    }
}
