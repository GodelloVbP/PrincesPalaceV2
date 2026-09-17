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
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 3b, item 4: Defeat is a simple
    // two-button Rail, its own NavContext pushed on Show() and popped on
    // OnDisable -- DefeatController.PushNavContext's own header explains why
    // not OnEnable (Show() needs Wire()'s buttons to already exist, and
    // OnDisable is what fires when either button's own handler tears this
    // screen down). Driven through the REAL production dispatcher (scripted
    // BaseInput via inputOverride, `yield return null`, assert resulting
    // state), the same shape every other file in this family uses.
    //
    // Fixture shape borrowed from CancelOpensSystemMenuTests/
    // DefeatScreenWiringTests: load the real Fight scene rather than a bare
    // DefeatController, because NavigationInputModule and its Contexts stack
    // only exist on a scene's own EventSystem.
    public class DefeatGamepadNavigationTests
    {
        private string _root;
        private DefeatController _defeat;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-defeat-nav-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // Numbers do not matter to this file -- only that Show() has
        // something non-null to paint before pushing the context.
        private static RunSettlement.Result Settlement() => new RunSettlement.Result
        {
            GoldLost = 10,
            RoomsCleared = 1,
            DeepestStep = 1,
            ExpEarned = 5,
            EmbersEarned = 0,
        };

        private IEnumerator OpenTheFightAndShowDefeat()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the fight scene's EventSystem is not running NavigationInputModule");
            // Set BEFORE letting any frame run -- MainMenuGamepadNavigationTests'
            // own LoadMenu makes the identical call for the identical reason
            // (a previously-loaded scene's EventSystem can still be current).
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            _defeat = Object.FindAnyObjectByType<DefeatController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_defeat, "the defeat screen was never wired into the fight scene");

            _defeat.Show(Settlement());
            yield return null;
        }

        private static Button Find(string name) =>
            Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b => b.name == name && b.gameObject.scene.IsValid());

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        [UnityTest]
        public IEnumerator EntryIsInspectButton_WhenDefeatShows()
        {
            yield return OpenTheFightAndShowDefeat();

            Assert.AreEqual(Find("DefeatInspectButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "Inspect sits left of Return (DefeatScreen's own Place.At layout) and should be the entry");
        }

        [UnityTest]
        public IEnumerator Right_FromInspect_ReachesReturn()
        {
            yield return OpenTheFightAndShowDefeat();

            _input.Horizontal = 1f;
            yield return DriveFrame();
            _input.Horizontal = 0f;

            Assert.AreEqual(Find("DefeatReturnButton").gameObject, EventSystem.current.currentSelectedGameObject,
                "Right from Inspect should reach Return, the Rail's own next member");
        }

        [UnityTest]
        public IEnumerator Submit_OnReturnButton_RaisesDismissed_ExactlyOnce()
        {
            yield return OpenTheFightAndShowDefeat();

            int dismissed = 0;
            _defeat.Dismissed = () => dismissed++;

            EventSystem.current.SetSelectedGameObject(Find("DefeatReturnButton").gameObject);
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(1, dismissed, "Submit on Return should raise Dismissed exactly once, same as a click");
        }

        [UnityTest]
        public IEnumerator Cancel_RaisesDismissed_SameAsReturn()
        {
            yield return OpenTheFightAndShowDefeat();

            int dismissed = 0;
            _defeat.Dismissed = () => dismissed++;

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.AreEqual(1, dismissed,
                "Cancel on the defeat screen should do what Return does -- there is no other way out, mouse or gamepad");
        }
    }
}
