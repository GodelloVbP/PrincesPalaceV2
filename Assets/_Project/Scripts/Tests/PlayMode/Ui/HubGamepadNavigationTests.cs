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
    // HubController.WireNavigation's own header for the 2x2 Grid this pins.
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

        private IEnumerator Move(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;
        }

        [UnityTest]
        public IEnumerator EntryIsTheGate_SelectedAssoonAsTheSceneLoads()
        {
            yield return LoadHub();

            Assert.AreEqual(_gate.gameObject, EventSystem.current.currentSelectedGameObject,
                "the gate is the hub's stated primary action (HubScreen's own comment) and its declared entry");
        }

        // hubBuildings is a 2x2 Grid: row 0 (far, upper) is Talents/Relics,
        // row 1 (near, lower) is Principality/CharacterSheet -- HubAnchors'
        // own lateral signs (-1 left, +1 right) and depths (.78/.90 far vs
        // .06/.16 near) are what fix this order, not screen pixels measured
        // by eye.
        [UnityTest]
        public IEnumerator Right_FromTalents_ReachesRelics_TheFarRowsOtherColumn()
        {
            yield return LoadHub();
            EventSystem.current.SetSelectedGameObject(_talents.gameObject);
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(_relics.gameObject, EventSystem.current.currentSelectedGameObject,
                "Right from Talents (far-left) should reach Relics (far-right), the Grid row's other column");
        }

        [UnityTest]
        public IEnumerator Down_FromTalents_ReachesPrincipality_TheNearRowsSameColumn()
        {
            yield return LoadHub();
            EventSystem.current.SetSelectedGameObject(_talents.gameObject);
            yield return null;

            yield return Move(0f, -1f);

            Assert.AreEqual(_principality.gameObject, EventSystem.current.currentSelectedGameObject,
                "Down from Talents (far/row 0) should reach Principality (near/row 1), same Grid column");
        }

        [UnityTest]
        public IEnumerator Down_FromPrincipality_ReachesTheGate_TheExplicitLinkOffTheGrid()
        {
            yield return LoadHub();
            EventSystem.current.SetSelectedGameObject(_principality.gameObject);
            yield return null;

            yield return Move(0f, -1f);

            Assert.AreEqual(_gate.gameObject, EventSystem.current.currentSelectedGameObject,
                "the near row's Down link is explicit (a Grid alone has nothing below its last row) -- " +
                "Principality should reach the gate");
        }

        [UnityTest]
        public IEnumerator Up_FromTheGate_ReachesCharacterSheet_TheExplicitReturnLink()
        {
            yield return LoadHub();
            EventSystem.current.SetSelectedGameObject(_gate.gameObject);
            yield return null;

            yield return Move(0f, 1f);

            Assert.AreEqual(_characterSheet.gameObject, EventSystem.current.currentSelectedGameObject,
                "the gate sits dead centre between both Grid columns -- WireNavigation ties its Up " +
                "arbitrarily to CharacterSheet, and this pins that literal choice");
        }

        [UnityTest]
        public IEnumerator Up_FromTalents_ReachesMainMenuButton_TheCornerUtility()
        {
            yield return LoadHub();
            EventSystem.current.SetSelectedGameObject(_talents.gameObject);
            yield return null;

            yield return Move(0f, 1f);

            Assert.AreEqual(_mainMenu.gameObject, EventSystem.current.currentSelectedGameObject,
                "MainMenuButton is not part of the staged Grid at all, but a corner utility reached from " +
                "Talents, the far-left building nearest it on screen");
        }

        [UnityTest]
        public IEnumerator Down_FromMainMenuButton_ReturnsToTalents()
        {
            yield return LoadHub();
            EventSystem.current.SetSelectedGameObject(_mainMenu.gameObject);
            yield return null;

            yield return Move(0f, -1f);

            Assert.AreEqual(_talents.gameObject, EventSystem.current.currentSelectedGameObject,
                "the corner link back down should be symmetric");
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
