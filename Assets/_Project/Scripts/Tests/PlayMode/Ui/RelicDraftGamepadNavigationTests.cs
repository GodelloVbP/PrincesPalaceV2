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
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 3, item 1 (AUDIT.md #158): the
    // relic draft, one of the three Hub-covering modals that did not push
    // their own NavContext. RelicDraftController.RefreshNavigation (called
    // at the end of every Paint()) is the mechanism; this file drives it
    // through the REAL production dispatcher (scripted BaseInput via
    // inputOverride, `yield return null`, assert resulting state), never a
    // direct Select()/Commit() call standing in for a press.
    //
    // Cancel here is a DELIBERATE NO-OP, unlike this file's two siblings
    // (Glossary/the debug menu, which both close on Cancel): the draft's own
    // header says why ("a draft you can navigate around is not a draft") and
    // there is no close affordance on the mouse path either, only Descend --
    // matching what HubController.HandleEscape used to enforce by hand
    // before this pass gave the draft its own context.
    public class RelicDraftGamepadNavigationTests
    {
        private string _root;
        private HubController _hub;
        private RelicDraftController _draft;
        private SystemMenuController _menu;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-draft-nav-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };

            // See RelicDraftTests.UseAThrowawaySaveRoot for why this is set
            // absurdly high rather than merely fast: BeginDescentTransition
            // strings three phases together and stepping between them still
            // costs Unity's coroutine driver a real engine frame apiece.
            HubController.MotionSpeedMultiplier = 100000f;
        }

        [TearDown]
        public void Restore()
        {
            HubController.MotionSpeedMultiplier = 1f;
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _hub.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private IEnumerator OpenTheDraft()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _hub = Object.FindAnyObjectByType<HubController>();
            Assert.IsNotNull(_hub, "the Hub scene has no HubController");

            _draft = _hub.GetComponentInChildren<RelicDraftController>(includeInactive: true);
            Assert.IsNotNull(_draft, "the draft was never wired into the hub");

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the hub scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);

            Named("StartRunGate").GetComponent<Button>().onClick.Invoke();

            // POLLED, not a fixed frame count -- MotionSpeedMultiplier
            // resolves each transition phase on its first check, but
            // stepping from one `yield return AnimateZoom(...)` to the next
            // still costs a real engine frame apiece (RelicDraftTests' own
            // comment on PressStartRunGateAndWaitForTheDraft).
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!_draft.gameObject.activeSelf && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_draft.gameObject.activeSelf, "the gate never opened the draft");

            yield return null;
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        [UnityTest]
        public IEnumerator EntryIsTheFirstCard_AsSoonAsTheDraftOpens()
        {
            yield return OpenTheDraft();

            Assert.AreEqual(Named("DraftCard0"), EventSystem.current.currentSelectedGameObject,
                "the first card is RefreshNavigation's own entry");
        }

        [UnityTest]
        public IEnumerator Right_FromCard0_ReachesCard1()
        {
            yield return OpenTheDraft();

            _input.Horizontal = 1f;
            yield return DriveFrame();
            _input.Horizontal = 0f;

            Assert.AreEqual(Named("DraftCard1"), EventSystem.current.currentSelectedGameObject,
                "Right from the first card should reach the second, the Rail's own next member");
        }

        [UnityTest]
        public IEnumerator Left_FromCard0_WrapsToTheLastCard()
        {
            yield return OpenTheDraft();

            _input.Horizontal = -1f;
            yield return DriveFrame();
            _input.Horizontal = 0f;

            Assert.AreEqual(Named("DraftCard2"), EventSystem.current.currentSelectedGameObject,
                "the card Rail wraps (owner default, section 12.3) -- Left from the first card should " +
                "reach the last of the three starting-pool cards");
        }

        [UnityTest]
        public IEnumerator Down_FromACard_ReachesDescend()
        {
            yield return OpenTheDraft();

            _input.Vertical = -1f;
            yield return DriveFrame();
            _input.Vertical = 0f;

            Assert.AreEqual(Named("DraftDescendButton"), EventSystem.current.currentSelectedGameObject,
                "Down from a card should reach Descend, the explicit link RefreshNavigation adds since a " +
                "Rail alone has nothing below it");
        }

        [UnityTest]
        public IEnumerator Submit_OnACard_SelectsItExactlyOnce_AndDescendCommitsIt()
        {
            yield return OpenTheDraft();

            _input.SubmitDown = true;
            yield return DriveFrame();

            Named("DraftDescendButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, RunManager.Run.relicIds.Count,
                "Submit on the entry card should select it exactly once, same as one click -- a double " +
                "dispatch would have toggled it back off (Select()'s own re-press-deselects rule)");
        }

        [UnityTest]
        public IEnumerator Cancel_IsADeliberateNoOp_TheDraftStaysOpenAndTheMenuDoesNotOpen()
        {
            yield return OpenTheDraft();

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsTrue(_draft.gameObject.activeSelf,
                "a draft you can navigate around is not a draft (RelicDraftController's own header) -- " +
                "Cancel should be spent, not close it");
            Assert.IsFalse(_menu != null && _menu.IsOpen,
                "Cancel must not fall through to the system menu while a draft is in progress, matching " +
                "what HubController.HandleEscape used to enforce by hand");
        }
    }
}
