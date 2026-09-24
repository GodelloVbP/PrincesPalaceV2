using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TMPro;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // Step B's behavioural gate (docs/GAMEPAD_NAVIGATION_PLAN.md phase 2):
    // SystemMenu as a nested modal context, driven through the REAL
    // production dispatcher on the REAL Hub scene -- a scripted BaseInput
    // via inputOverride, yield return null, assert resulting state. Never a
    // direct HandleEscape/Open call standing in for a press: Navigation
    // DispatcherTests' own header draws exactly this distinction, and this
    // file is that same proof against production wiring instead of a
    // fixture.
    public class SystemMenuGamepadNavigationTests
    {
        private HubController _hub;
        private SystemMenuController _menu;
        private ScriptedBaseInput _input;

        [TearDown]
        public void AfterEach() => NavSceneReuse.AfterTest(_input);

        // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene). The one thing
        // a test here opens is the system menu itself, closed through its own
        // Close() (which also resumes the pause Open() took); NavSceneReuse
        // puts back the input and the focus memory, so the hub's own entry
        // is reselected exactly as on a fresh load. GameSettings values are
        // set by each test that reads them, as before.
        private IEnumerator LoadHub()
        {
            yield return SharedScene.Ensure("Hub");

            _hub = Object.FindAnyObjectByType<HubController>(FindObjectsInactive.Include);
            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_hub, "the hub scene has no HubController");
            Assert.IsNotNull(_menu, "the hub scene has no SystemMenuController");

            NavSceneReuse.CloseHubModals();

            _input = NavSceneReuse.TakeOverInput();
            NavSceneReuse.ForgetFocusMemory();
            yield return null;
            yield return null;
        }

        // One engine frame: EventSystem.Update() calls Process() exactly
        // once during it (plan section 2), matching NavigationDispatcherTests'
        // own DriveFrame.
        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        [UnityTest]
        public IEnumerator StartOpensTheMenuFromTheHub_StartAgainClosesItAndRestoresTheGate()
        {
            SharedScene.MarkDirty("asserts the gate is selected as soon as the scene loads, not after a reset");
            yield return LoadHub();

            var gate = _hub.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .First(b => b.name == "StartRunGate");

            Assert.IsFalse(_menu.IsOpen, "the menu should start closed");

            // Phase 3's rollout (docs/GAMEPAD_NAVIGATION_PLAN.md phase 3a,
            // HubController.WireNavigation): the hub now declares a real
            // selectable set and an entry, so this is no longer the "nothing
            // is selected yet" case an earlier draft of this test pinned --
            // the gate is the screen's own stated primary action.
            Assert.AreEqual(gate.gameObject, EventSystem.current.currentSelectedGameObject,
                "the hub's entry (the gate) should be selected as soon as the scene loads");

            _input.SystemMenuDown = true;
            yield return DriveFrame();

            Assert.IsTrue(_menu.IsOpen,
                "Start should open the system menu -- HubController.HandleEscape, now the hub's own " +
                "NavContext systemMenu handler rather than its Cancel (the owner's 2026-09-19 call)");
            Assert.IsNotNull(EventSystem.current.currentSelectedGameObject,
                "opening the menu should select its own tab bar");

            _input.SystemMenuDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_menu.IsOpen, "a second Start should close the menu it just opened");

            // Hub's remembered node IS restored now, provably: closing the
            // menu must land back on the hub's own entry (the gate), not the
            // menu's last-selected tab and not nothing -- the failure mode
            // SystemMenuController.PopNavContext leaving a stale selection
            // behind would look like.
            Assert.AreEqual(gate.gameObject, EventSystem.current.currentSelectedGameObject,
                "closing the menu should restore the hub's own entry, not leak the menu's last tab or leave nothing selected");
        }

        [UnityTest]
        public IEnumerator CancelInsideOptions_PopsExactlyOneLayer()
        {
            yield return LoadHub();

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);
            yield return null;
            Assert.IsTrue(_menu.IsOpen);

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_menu.IsOpen,
                "Cancel from inside the Options pane should close the whole menu -- Options has no nested " +
                "context of its own, so there is only ever one layer to pop");

            // Exactly ONE layer, proven rather than assumed: if Cancel had
            // somehow popped the hub's own base context too, this press would
            // find an empty stack and do nothing. START rather than a second
            // Cancel, because the hub is a ROOT now -- its Cancel is
            // deliberately nothing, and Start is the only way back into the
            // menu from it.
            _input.SystemMenuDown = true;
            yield return DriveFrame();

            Assert.IsTrue(_menu.IsOpen,
                "the hub's own context should still be on top and functioning after the menu's one pop -- " +
                "Start re-opening the menu is what proves that, rather than the stack having lost its own " +
                "base context along with the menu's");
        }

        // Pinned to literal expected values (docs/CODE_STANDARDS.md section
        // 8) -- never recomputed from the production formula under test.
        //
        // One press per test because each test pins one row and one
        // direction, not because two presses could not be chained: the 0.1s
        // uGUI re-press gate that made chaining look unsafe is lifted by
        // NavSceneReuse.TakeOverInput (JourneyFixture.Move has the
        // measurement).
        [UnityTest]
        public IEnumerator SliderRow_RightPress_RaisesItByExactlyOneStep()
        {
            yield return LoadHub();
            GameSettings.SetSoundVolume(0.5f);

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);
            yield return null;

            var soundRow = _menu.GetComponentsInChildren<OptionRow>(includeInactive: true)
                .First(r => r.name == "OptionsRowsound");
            EventSystem.current.SetSelectedGameObject(soundRow.gameObject);
            yield return null;

            _input.Horizontal = 1f;
            yield return DriveFrame();

            Assert.AreEqual(0.55f, GameSettings.SoundVolume, 0.0001f,
                "one Right press on a slider row should raise it by exactly OptionsController.SliderStep (0.05)");
        }

        [UnityTest]
        public IEnumerator SliderRow_LeftPress_LowersItByExactlyOneStep()
        {
            yield return LoadHub();
            GameSettings.SetSoundVolume(0.5f);

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);
            yield return null;

            var soundRow = _menu.GetComponentsInChildren<OptionRow>(includeInactive: true)
                .First(r => r.name == "OptionsRowsound");
            EventSystem.current.SetSelectedGameObject(soundRow.gameObject);
            yield return null;

            _input.Horizontal = -1f;
            yield return DriveFrame();

            Assert.AreEqual(0.45f, GameSettings.SoundVolume, 0.0001f,
                "one Left press on a slider row should lower it by exactly OptionsController.SliderStep (0.05)");
        }

        [UnityTest]
        public IEnumerator StepperRow_RightPress_StepsItUpByExactlyOneIndex()
        {
            yield return LoadHub();
            GameSettings.SetResolutionIndex(0);

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);
            yield return null;

            var resolutionRow = _menu.GetComponentsInChildren<OptionRow>(includeInactive: true)
                .First(r => r.name == "OptionsRowresolution");
            EventSystem.current.SetSelectedGameObject(resolutionRow.gameObject);
            yield return null;

            _input.Horizontal = 1f;
            yield return DriveFrame();

            Assert.AreEqual(1, GameSettings.ResolutionIndex,
                "one Right press on a stepper row should step its index up by exactly one");
        }

        [UnityTest]
        public IEnumerator StepperRow_LeftPress_StepsItDownByExactlyOneIndex()
        {
            yield return LoadHub();
            GameSettings.SetResolutionIndex(1);

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);
            yield return null;

            var resolutionRow = _menu.GetComponentsInChildren<OptionRow>(includeInactive: true)
                .First(r => r.name == "OptionsRowresolution");
            EventSystem.current.SetSelectedGameObject(resolutionRow.gameObject);
            yield return null;

            _input.Horizontal = -1f;
            yield return DriveFrame();

            Assert.AreEqual(0, GameSettings.ResolutionIndex,
                "one Left press on a stepper row should step its index down by exactly one");
        }

        // Structural half of plan section 7's stepper claim: a stepper
        // button must never be a navigation target, and clicking it must
        // still fire its own onClick. NavigationDispatcherTests' fixture
        // (a hand-built ScreenSpaceOverlay canvas) is where the full
        // click-vs-selection interaction through a real scripted raycast is
        // proven -- this scene's canvas is ScreenSpaceCamera
        // (SceneBuilder.CreateCanvas), where a RectTransform's world
        // position is not simply a screen coordinate, so a click simulated
        // here would be testing arithmetic this file has no business
        // getting wrong rather than the row's own behaviour.
        [UnityTest]
        public IEnumerator StepperButtons_AreNeverANavigationTarget_AndStillClick()
        {
            yield return LoadHub();
            GameSettings.SetResolutionIndex(0);

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);
            yield return null;

            var prevButton = _menu.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .First(b => b.name == "OptionsRowresolutionPrev");
            var nextButton = _menu.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .First(b => b.name == "OptionsRowresolutionNext");

            Assert.AreEqual(UnityEngine.UI.Navigation.Mode.None, prevButton.navigation.mode,
                "the stepper's Prev button must never be a navigation target");
            Assert.AreEqual(UnityEngine.UI.Navigation.Mode.None, nextButton.navigation.mode,
                "the stepper's Next button must never be a navigation target");

            nextButton.onClick.Invoke();
            Assert.AreEqual(1, GameSettings.ResolutionIndex,
                "Navigation.Mode.None must not also suppress the button's own click");
        }

        // ---- the trigger shortcut (docs/GAMEPAD_NAVIGATION_PLAN.md phase 3, item 2) ----
        //
        // TriggerLeft/TriggerRight (ProjectSettings/InputManager.asset's own
        // two trigger axes) are offered to whichever context is top the same
        // way Cancel is (NavigationInputModule.Process()), and only a
        // context that implements INavTabStrip acts on them --
        // SystemMenuController.StepTab is the only implementor, and it steps
        // through the SAME Select(index) each tab's own Button.onClick
        // calls, so a trigger pull and a click can never disagree about
        // which tab is next. TabPrev/TabNext (LB/RB) were reassigned to
        // section/character paging by the owner's 2026-09-19 hardware-round
        // call (see the shoulder tests further down this file).

        private static Transform PaneNamed(SystemMenuController menu, string key) =>
            menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .First(t => t.name == "SystemPane" + key);

        [UnityTest]
        public IEnumerator TabNext_FromTheDefaultTab_SwitchesToPartyAndSelectsItsPaneEntry()
        {
            yield return LoadHub();

            _menu.Open();
            yield return null;

            // Fixture check: DefaultFor(inRun: false) is CharacterInventory,
            // out of the hub's own four visible tabs (CharacterInventory,
            // Party, Options, MainMenu -- FloorMap/RunStats are RunOnly).
            Assert.AreEqual(SystemMenuTabs.IndexOf(SystemMenuTab.CharacterInventory), _menu.SelectedIndex,
                "fixture: the menu should open on CharacterInventory outside a run");

            _input.TriggerRight = 1f;
            yield return DriveFrame();
            _input.TriggerRight = 0f;

            Assert.AreEqual(SystemMenuTabs.IndexOf(SystemMenuTab.Party), _menu.SelectedIndex,
                "one TabNext press should step the tab strip to Party, the next VISIBLE tab");

            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.IsNotNull(selected, "the trigger shortcut should leave something selected, not nothing");
            Assert.IsTrue(selected.transform.IsChildOf(PaneNamed(_menu, "Party")),
                "the trigger shortcut should land INSIDE the new pane (its own entry), the same place a " +
                "Move-to-tab + Submit + Move-down dance would eventually reach in three presses instead of one");
        }

        [UnityTest]
        public IEnumerator TabPrev_FromTheDefaultTab_WrapsToTheLastVisibleTab()
        {
            yield return LoadHub();

            _menu.Open();
            yield return null;

            _input.TriggerLeft = 1f;
            yield return DriveFrame();
            _input.TriggerLeft = 0f;

            Assert.AreEqual(SystemMenuTabs.IndexOf(SystemMenuTab.MainMenu), _menu.SelectedIndex,
                "TabPrev from the first visible tab should wrap to the last one (MainMenu, out of the hub's " +
                "own four visible tabs) -- the owner default every other group in this project wraps by");
        }

        [UnityTest]
        public IEnumerator ShoulderNext_OnTheDossierPagesCharactersWithoutChangingTabs()
        {
            yield return LoadHub();
            _menu.Open();
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the character tab has no dossier controller");
            var name = dossier.GetComponentsInChildren<TMP_Text>(includeInactive: true)
                .First(t => t.name == "DossierName");
            string before = name.text;
            int tabBefore = _menu.SelectedIndex;

            _input.TabNextDown = true;
            yield return DriveFrame();

            Assert.AreNotEqual(before, name.text, "RB should page to the next dossier character");
            Assert.AreEqual(tabBefore, _menu.SelectedIndex, "RB pages characters; it must not switch menu tabs");
        }

        [UnityTest]
        public IEnumerator ShoulderPress_WithNoTabStripContextOnTop_DoesNothing()
        {
            yield return LoadHub();

            var gate = _hub.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .First(b => b.name == "StartRunGate");
            yield return null;

            Assert.IsFalse(_menu.IsOpen, "fixture: the menu should start closed, so the hub's own " +
                "(non-tab-strip) context is top");

            _input.TabNextDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_menu.IsOpen, "a shoulder press should not open the menu on its own");
            Assert.AreEqual(gate.gameObject, EventSystem.current.currentSelectedGameObject,
                "a shoulder press with no INavSectionStrip context on top should be silently absorbed " +
                "(NavContext.RaiseSectionStep's own no-op), not move selection off the hub's entry");
        }
    }
}
