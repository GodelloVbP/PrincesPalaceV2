using System.Collections;
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
    // THE MOCK-UP IN FRONT OF THE GATE, not instead of it.
    //
    // Everything RelicDraftTests already checks about WHAT the gate reaches
    // is unchanged -- it just now happens after BeginDescentTransition plays,
    // and that file speeds the transition down to nothing so its own
    // assertions keep counting frames the way they did before the transition
    // existed. This file is the other half: that the transition actually
    // RUNS (the gate goes uninteractable, the panel visibly moves) and that
    // it still ends at the same place the direct call used to.
    public class HubDescentTransitionTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-hub-descent-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            HubController.MotionSpeedMultiplier = 1f;
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static IEnumerator OpenTheHub()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            EngineRoots.GrantToSquad();
        }

        private static void Click(HubController hub, string name)
        {
            var go = hub.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;
            Assert.IsNotNull(go, $"no object named '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator PressingTheGateDisablesItAndVisiblyMovesThePanelBeforeAnythingElseHappens()
        {
            // Real speed on purpose -- this is checking that the transition
            // is actually running, which a sped-up multiplier would skip
            // straight past.
            HubController.MotionSpeedMultiplier = 1f;

            yield return OpenTheHub();

            var hub = Object.FindAnyObjectByType<HubController>();
            Assert.IsNotNull(hub, "the Hub scene has no HubController");

            var gate = hub.GetComponentsInChildren<Button>(includeInactive: true)
                .First(b => b.name == "StartRunGate");
            var panel = (RectTransform)hub.transform;

            Assert.AreEqual(1f, panel.localScale.x, 0.001f, "the panel starts at rest");
            Assert.IsTrue(gate.interactable, "the gate starts pressable");

            Click(hub, "StartRunGate");
            yield return null;

            Assert.IsFalse(gate.interactable,
                "the gate is still pressable mid-transition, so a second press could stack a second one");
            Assert.AreNotEqual(1f, panel.localScale.x,
                "the panel has not moved a frame after the gate was pressed, so there is no transition " +
                "playing, only the instant snap the mock-up replaced");
        }

        [UnityTest]
        public IEnumerator TheTransitionStillReachesTheRelicDraftAFreshRunUsedToReachDirectly()
        {
            // Sped up here -- this test is about the DESTINATION, not the
            // shape of the trip, and it needs the trip to actually finish.
            HubController.MotionSpeedMultiplier = 200f;

            yield return OpenTheHub();

            var hub = Object.FindAnyObjectByType<HubController>();
            var draft = hub.GetComponentInChildren<RelicDraftController>(includeInactive: true);
            Assert.IsNotNull(draft, "the draft was never wired into the hub");
            Assert.IsFalse(draft.gameObject.activeSelf);

            Click(hub, "StartRunGate");

            float watched = 0f;
            while (!draft.gameObject.activeSelf && watched < 2f)
            {
                watched += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(draft.gameObject.activeSelf,
                "the transition finished (or ran long enough to) without ever reaching the relic draft " +
                "a direct press of the gate used to open");

            var gate = hub.GetComponentsInChildren<Button>(includeInactive: true)
                .First(b => b.name == "StartRunGate");
            Assert.IsTrue(gate.interactable,
                "the draft is up but the gate stayed disabled -- the hub is stuck behind it");
        }
    }
}
