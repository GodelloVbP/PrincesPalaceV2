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
    // HARDWARE PLAY-TEST ROUND 1, ITEM 3: "the joystick only is wonky,
    // especially in the main menu... it feels almost random."
    //
    // Driven on the REAL Main Menu, the screen the owner named, through the
    // real dispatcher -- NavigationInputModule.UpdateMoveGate/
    // GetAxisEventData's own armed edge is what these pin, and that field's
    // header carries the measured argument for why the stock analog path
    // wandered.
    //
    // THE STICK IS HELD FOR REAL SECONDS in every test here, never pulsed for
    // a frame or two, and that is what makes these tests discriminating
    // rather than decorative. StandaloneInputModule's own repeat machinery
    // only misbehaves once wall clock has passed: its first repeat waits
    // m_RepeatDelay (0.5s) and everything after that comes at
    // 1/m_InputActionsPerSecond (0.1s). A test that holds the stick for ten
    // engine frames -- about a sixth of a second -- passes identically with
    // and without this fix and proves nothing. HoldSeconds is 1.0 so the
    // stock path would have had time for the delay AND four or five repeats.
    //
    // A THREE-BUTTON MENU, seeded on purpose: with no save on disk the menu
    // is Play over Exit, and a runaway repeat off either one clamps straight
    // back where a single correct Move would also have landed. Continue over
    // Play over Exit is the shortest list on which "moved once" and "moved
    // more than once" look different.
    public class StickThresholdGamepadNavigationTests
    {
        private const float HoldSeconds = 1.0f;

        private string _root;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-stick-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // MainMenuGamepadNavigationTests' own pattern, reused rather than
        // re-invented: a save on disk before the scene loads is what makes
        // ContinueButton exist at all.
        private static void SeedASave(int slot)
        {
            SaveSlotManager.CurrentSlot = slot;
            SaveSlotManager.Forget();
            Assert.IsNotNull(SaveSlotManager.CurrentSave, "CurrentSave should never be null");
            SaveSlotManager.SaveCurrent();
            SaveSlotManager.Forget();
        }

        private IEnumerator LoadMenuWithAThreeButtonColumn()
        {
            SeedASave(0);

            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the main menu scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            yield return null;
            yield return null;

            Assert.AreEqual("ContinueButton", Selected(),
                "fixture: with a save on disk the column is Continue over Play over Exit, and Continue is the entry");
        }

        // Holds the axis at `value` for HoldSeconds of WALL CLOCK, then
        // drives one frame at rest. That frame is all the next Hold needs:
        // it re-arms the dispatcher's edge and resets uGUI's consecutive-move
        // count, and the only uGUI gate left for a re-press is
        // 1/inputActionsPerSecond (0.1s) after the last DISPATCHED move --
        // which was at the start of this hold, HoldSeconds ago. A real-time
        // settle here used to wait out the 0.5s repeat delay, but that
        // delay only guards a held stick, never a re-press after rest.
        private IEnumerator Hold(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;

            float until = Time.realtimeSinceStartup + HoldSeconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                _input.ClearOneFrameFlags();
            }

            _input.Horizontal = 0f;
            _input.Vertical = 0f;
            yield return null;
        }

        private static string Selected() =>
            EventSystem.current.currentSelectedGameObject != null
                ? EventSystem.current.currentSelectedGameObject.name
                : "<null>";

        // 0.3 IS A STICK AT REST ON A WORN PAD, not a press. The project's
        // own joystick Vertical axis has dead 0.19, so a reading like this
        // reaches the module live.
        [UnityTest]
        public IEnumerator AnAxisAt0Point3_HeldForASecond_MovesNothing()
        {
            yield return LoadMenuWithAThreeButtonColumn();

            yield return Hold(0f, -0.3f);

            Assert.AreEqual("ContinueButton", Selected(),
                "an axis below NavigationInputModule.MoveThreshold must dispatch no Move at all, " +
                "however long it is held");
        }

        // 0.8 IS A PRESS -- and exactly one, held or not. Under the stock
        // analog path this same hold produced the first Move, then a repeat
        // half a second later, then one every tenth of a second: Continue to
        // Play to Exit and stuck there.
        [UnityTest]
        public IEnumerator AnAxisAt0Point8_HeldForASecond_MovesExactlyOnce()
        {
            yield return LoadMenuWithAThreeButtonColumn();

            yield return Hold(0f, -0.8f);

            Assert.AreEqual("PlayButton", Selected(),
                "one press is one Move -- landing on ExitButton would mean the held stick repeated, " +
                "which is the 'one flick, two moves' half of the owner's report");
        }

        // THE WANDER ITSELF. 0.65 is above the 0.6 magnitude that
        // BaseInputModule.DetermineMoveDirection used to be the only gate,
        // and it is a plausible rest position for a stick whose only dead
        // zone before the module sees it is InputManager's own 0.19. Nobody
        // is pressing anything here; the selection must not drift.
        [UnityTest]
        public IEnumerator AnAxisRestingJustPastTheOldDeadZone_DriftsAtMostOneStep()
        {
            yield return LoadMenuWithAThreeButtonColumn();

            yield return Hold(0f, -0.65f);

            Assert.AreEqual("PlayButton", Selected(),
                "a stick resting past the old 0.6 dead zone used to repeat at ten selections a second " +
                "because a sub-threshold frame reset m_ConsecutiveMoveCount without touching " +
                "m_PrevActionTime, so the 0.5s repeat delay never armed. One step is all it may cost now");
        }

        // Two deliberate presses still move twice: the gate re-arms on the
        // way back through neutral, it does not latch.
        [UnityTest]
        public IEnumerator TwoSeparatePresses_MoveTwice()
        {
            yield return LoadMenuWithAThreeButtonColumn();

            yield return Hold(0f, -1f);
            Assert.AreEqual("PlayButton", Selected(), "one press down");

            yield return Hold(0f, -1f);
            Assert.AreEqual("ExitButton", Selected(), "a second press, after the stick passed back through rest");

            yield return Hold(0f, -1f);
            Assert.AreEqual("ExitButton", Selected(), "the List clamps -- there is nothing below Exit");
        }

        // The other half of item 3's own brief: a List declares no Left or
        // Right at all, so those presses are a no-op rather than a jump to
        // whatever Unity's Automatic geometry would have found.
        [UnityTest]
        public IEnumerator LeftAndRight_OnTheMainMenuList_AreNoOps()
        {
            yield return LoadMenuWithAThreeButtonColumn();

            var play = Resources.FindObjectsOfTypeAll<Button>()
                .First(b => b.name == "PlayButton" && b.gameObject.scene.IsValid());
            Assert.AreEqual(UnityEngine.UI.Navigation.Mode.Explicit, play.navigation.mode,
                "every member of the menu's List is written Explicit by RuntimeNavWiring -- Automatic would " +
                "let Unity pick a neighbour by geometry, which would be the other half of 'almost random'");
            Assert.IsNull(play.navigation.selectOnLeft, "a List declares no Left");
            Assert.IsNull(play.navigation.selectOnRight, "a List declares no Right");

            yield return Hold(-1f, 0f);
            Assert.AreEqual("ContinueButton", Selected(), "Left is a no-op on this screen");

            yield return Hold(1f, 0f);
            Assert.AreEqual("ContinueButton", Selected(), "Right is a no-op on this screen");
        }
    }
}
