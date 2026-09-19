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
    //
    // ROUND 2 (2026-09-19): the ring's Left/Right assertions below are
    // adapted again. Round 1 (a9f89ebe) hand-typed the ring in the order the
    // owner's words gave it literally -- "Left goes Principality then
    // Talents" -- which this file pinned verbatim. On real screen x, though,
    // Talents (-349) sits BETWEEN Principality (-673) and the gate (0), so
    // that order made Left overshoot Talents and land back on it on the
    // second press -- exactly the follow-up report ("press left twice you
    // are left middle"). HubController.WireNavigation now sorts the ring by
    // each control's actual x instead of by hand, so Left off the gate
    // reaches Talents (the nearer one) before Principality. See
    // HubRingAdjacencyTests (EditMode) for the same claim pinned without a
    // scene.
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
        // Left steps to the nearest building on screen, then the next
        // nearest -- Talents (x -349) before Principality (x -673). Round 1
        // pinned the owner's words ("Left goes to Principality, then Left
        // again to Talents") verbatim instead, which put the farther
        // building first; this is the 2026-09-19 correction.
        [UnityTest]
        public IEnumerator Left_FromTheGate_ReachesTalents_ThenPrincipality()
        {
            yield return LoadHub();
            AssertSelected(_gate, "the gate is the entry");

            yield return Move(-1f, 0f);
            AssertSelected(_talents, "Talents (x -349) is the nearest thing left of the gate (x 0)");

            yield return Move(-1f, 0f);
            AssertSelected(_principality, "Principality (x -673) is the next nearest, one more press further left");
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
        // Ring order by screen x: Principality(-673), Talents(-349),
        // Gate(0), Relics(295), CharacterSheet(628) -- see this file's
        // header for why that replaced the hand-typed name order.
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
            AssertSelected(_principality, "the ring wraps off its right end to Principality, the lowest x");
            yield return Move(1f, 0f);
            AssertSelected(_talents, "Right off Principality reaches Talents");
            yield return Move(1f, 0f);
            AssertSelected(_gate, "Right off Talents closes the ring on the gate");

            yield return Move(-1f, 0f);
            AssertSelected(_talents, "Left off the gate reaches Talents");
            yield return Move(-1f, 0f);
            AssertSelected(_principality, "Left off Talents reaches Principality");
            yield return Move(-1f, 0f);
            AssertSelected(_characterSheet, "the ring wraps off its left end to Character Sheet, the highest x");
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
