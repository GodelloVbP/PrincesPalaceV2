using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PrincesPalace.PlayModeTests
{
    // Phase 3a of docs/GAMEPAD_NAVIGATION_PLAN.md: the hub's real shape is
    // four staged buildings and a gate (HubAnchors), not a tab bar -- see
    // HubController.WireNavigation's own header for the RING and the two
    // columns this pins, and for the measured screen positions they were
    // authored from.
    //
    // The 2x2 Grid this file used to pin was REJECTED BY THE OWNER on real
    // hardware ("when I'm top right on relics and I go to the right with the
    // joystick, it doesn't take me to character sheet but to talents"), so
    // the tests that encoded it are adapted rather than deleted: each still
    // asks about the same pair of controls, against the answer the owner
    // actually wants.
    // Driven through the REAL production dispatcher (scripted BaseInput via
    // inputOverride, `yield return null`, assert resulting state), the same
    // shape SystemMenuGamepadNavigationTests already established for this
    // scene -- never a direct WireNavigation/HandleEscape call standing in
    // for a press.
    public class HubGamepadNavigationTests
    {
        private HubController _hub;
        private ScriptedBaseInput _input;

        private Button _talents;
        private Button _relics;
        private Button _principality;
        private Button _characterSheet;
        private Button _gate;
        private Button _mainMenu;

        private IEnumerator LoadHub()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            // Start() runs one frame after activation.
            yield return null;
            yield return null;

            _hub = Object.FindAnyObjectByType<HubController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_hub, "the hub scene has no HubController");

            var buttons = _hub.GetComponentsInChildren<Button>(includeInactive: true);
            _talents = buttons.First(b => b.name == "TalentsBuilding");
            _relics = buttons.First(b => b.name == "RelicsBuilding");
            _principality = buttons.First(b => b.name == "PrincipalityBuilding");
            _characterSheet = buttons.First(b => b.name == "CharacterSheetBuilding");
            _gate = buttons.First(b => b.name == "StartRunGate");
            _mainMenu = buttons.First(b => b.name == "MainMenuButton");

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the hub scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        // The real-time settle after release is JourneyFixture.Move's own,
        // and for its reason: StandaloneInputModule's repeat gate is wall
        // clock with no BaseInput seam, so a SECOND chained move arriving
        // inside moveRepeatDelay is silently dropped. Every test below but
        // the ring and the column walks is a single press and never needed
        // it; those two are nothing but chained presses.
        private IEnumerator Move(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;
            yield return new WaitForSecondsRealtime(0.6f);
            yield return DriveFrame();
        }

        private void AssertSelected(Button expected, string because) =>
            Assert.AreEqual(expected.gameObject, EventSystem.current.currentSelectedGameObject, because);

        [UnityTest]
        public IEnumerator EntryIsTheGate_SelectedAssoonAsTheSceneLoads()
        {
            yield return LoadHub();

            Assert.AreEqual(_gate.gameObject, EventSystem.current.currentSelectedGameObject,
                "the gate is the hub's stated primary action (HubScreen's own comment) and its declared entry");
        }

        // ---- the owner's own two expectations, pinned literally ------------
        //
        // "from the gate ('start descent'), Left goes to Principality, then
        // Left again to Talents"
        [UnityTest]
        public IEnumerator Left_FromTheGate_ReachesPrincipality_ThenTalents()
        {
            yield return LoadHub();
            AssertSelected(_gate, "the gate is the entry");

            yield return Move(-1f, 0f);
            AssertSelected(_principality, "Left off the gate should reach Principality");

            yield return Move(-1f, 0f);
            AssertSelected(_talents, "Left again should reach Talents");
        }

        // "when I'm top right on relics and I go to the right with the
        // joystick, it doesn't take me to character sheet but to talents" --
        // the Grid's own row wrap, which is what this replaces.
        [UnityTest]
        public IEnumerator Right_FromRelics_ReachesCharacterSheet_NotTalents()
        {
            yield return LoadHub();
            EventSystem.current.SetSelectedGameObject(_relics.gameObject);
            yield return null;

            yield return Move(1f, 0f);

            AssertSelected(_characterSheet,
                "Right off Relics should reach Character Sheet, the nearest thing to its right -- " +
                "under the 2x2 Grid it wrapped the whole way across the screen to Talents instead");
        }

        // ---- the whole ring, both ways -------------------------------------
        [UnityTest]
        public IEnumerator TheWholeRing_RightThenLeft()
        {
            yield return LoadHub();

            AssertSelected(_gate, "the gate is the entry");
            yield return Move(1f, 0f);
            AssertSelected(_relics, "Right off the gate reaches Relics");
            yield return Move(1f, 0f);
            AssertSelected(_characterSheet, "Right off Relics reaches Character Sheet");
            yield return Move(1f, 0f);
            AssertSelected(_talents, "the ring wraps off its right end to Talents");
            yield return Move(1f, 0f);
            AssertSelected(_principality, "Right off Talents reaches Principality");
            yield return Move(1f, 0f);
            AssertSelected(_gate, "Right off Principality closes the ring on the gate");

            yield return Move(-1f, 0f);
            AssertSelected(_principality, "Left off the gate reaches Principality");
            yield return Move(-1f, 0f);
            AssertSelected(_talents, "Left off Principality reaches Talents");
            yield return Move(-1f, 0f);
            AssertSelected(_characterSheet, "the ring wraps off its left end to Character Sheet");
            yield return Move(-1f, 0f);
            AssertSelected(_relics, "Left off Character Sheet reaches Relics");
            yield return Move(-1f, 0f);
            AssertSelected(_gate, "Left off Relics closes the ring on the gate");
        }

        // ---- the two columns, top to bottom by screen y --------------------
        [UnityTest]
        public IEnumerator Down_TheLeftArm_MainMenuToTalentsToPrincipalityToTheGate()
        {
            yield return LoadHub();
            EventSystem.current.SetSelectedGameObject(_mainMenu.gameObject);
            yield return null;

            yield return Move(0f, -1f);
            AssertSelected(_talents, "MainMenuButton is corner chrome above the left arm");
            yield return Move(0f, -1f);
            AssertSelected(_principality, "Talents stands above Principality on the left arm");
            yield return Move(0f, -1f);
            AssertSelected(_gate, "the gate is the foot of the left arm");
            yield return Move(0f, -1f);
            AssertSelected(_gate, "the column is clamped -- there is nothing below the gate");
        }

        [UnityTest]
        public IEnumerator Up_FromTheGate_ReachesPrincipality_TheLeftArmAboveIt()
        {
            yield return LoadHub();
            EventSystem.current.SetSelectedGameObject(_gate.gameObject);
            yield return null;

            yield return Move(0f, 1f);

            AssertSelected(_principality,
                "Up off the gate means the left arm now -- the old link to CharacterSheet was tied there " +
                "arbitrarily by WireNavigation's own admission, and Character Sheet is one Right away instead");
        }

        [UnityTest]
        public IEnumerator Down_FromRelics_ReachesCharacterSheet_TheRightArmBelowIt()
        {
            yield return LoadHub();
            EventSystem.current.SetSelectedGameObject(_relics.gameObject);
            yield return null;

            yield return Move(0f, -1f);

            AssertSelected(_characterSheet, "Relics stands above Character Sheet on the right arm");
        }

        [UnityTest]
        public IEnumerator Down_FromCharacterSheet_ReachesTheGate_TheRightArmsFoot()
        {
            yield return LoadHub();
            EventSystem.current.SetSelectedGameObject(_characterSheet.gameObject);
            yield return null;

            yield return Move(0f, -1f);

            AssertSelected(_gate,
                "the right arm's foot reaches the gate through an explicit link, so the primary action " +
                "is one press away from both arms");
        }

        [UnityTest]
        public IEnumerator Up_FromTalents_ReachesMainMenuButton_TheCornerUtility()
        {
            yield return LoadHub();
            EventSystem.current.SetSelectedGameObject(_talents.gameObject);
            yield return null;

            yield return Move(0f, 1f);

            AssertSelected(_mainMenu,
                "MainMenuButton is not part of the staged composition at all, but corner chrome reached " +
                "from Talents, the building nearest it on screen");
        }

        // The required action (docs/GAMEPAD_NAVIGATION_PLAN.md phase 3's own
        // per-screen test list): CharacterSheetButton's Submit, not the
        // gate's -- StartRunButton's own action is an unscripted-length
        // coroutine ending in a real scene load (BeginDescentTransition ->
        // EnterTheDescent -> Navigation.Go), which is not a fact this
        // dispatcher test needs to prove and would make the test slow and
        // load-bearing on the descent transition instead of on navigation.
        // Opening the character overlay is instant, inspectable
        // (HubController.CharacterOverlayIsOpen) and already proven to run
        // through the same Button.onClick Submit already exercises for
        // every other themed control in this project (SystemMenu's own
        // stepper test makes the identical point).
        [UnityTest]
        public IEnumerator Submit_OnCharacterSheetButton_OpensTheOverlayExactlyOnce()
        {
            yield return LoadHub();
            EventSystem.current.SetSelectedGameObject(_characterSheet.gameObject);
            yield return null;

            Assert.IsFalse(_hub.CharacterOverlayIsOpen, "the overlay should start closed");

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.IsTrue(_hub.CharacterOverlayIsOpen, "Submit on the selected CharacterSheetButton should open it");
        }
    }
}
