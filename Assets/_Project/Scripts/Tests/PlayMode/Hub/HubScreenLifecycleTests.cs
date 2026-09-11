using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.PlayModeTests
{
    // The hub across a re-enable, and with the descent transition interrupted.
    //
    // HubDescentTransitionTests drives the transition twice: once at real speed
    // to prove it plays, once sped up to prove it still reaches the relic
    // draft. Both watch it FINISH. What is left is the 0.72 seconds in the
    // middle, where the screen is mid-zoom, the gate is disabled and
    // EnterTheDescent has not run yet -- so the save still has no run while the
    // player has already committed to one.
    //
    // A press of the hub's own way out landing in that window is the shape of
    // 45be6e6a: EndRun with no run to end used to strip every roster
    // character's equipment, empty the stockpile, and count a lifetimeRunsEnded
    // for a descent that never happened. RunEndingTests pins that contract at
    // the seam, calling RunManager directly. Nothing drove it through the
    // screen, which is the only place the window actually exists.
    //
    // scenarios B13, C7, D2 (docs/hunt/SCENARIOS.md).
    public class HubScreenLifecycleTests
    {
        private string _root;
        private readonly List<string> _navigated = new List<string>();

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-hub-life-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            _navigated.Clear();
            Navigation.LoadOverride = scene => _navigated.Add(scene);
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- B13: the hub repaints on the way back in --------------------------------

        // OnEnable, NOT Start, and HubController says why in as many words: the
        // hub is returned to repeatedly -- from a finished fight, from an
        // abandoned run -- and Start fires once, so gold banked during a
        // descent would be one scene load late.
        //
        // ScreenWiringTests pins the currency line's FORMAT against the
        // builder's template. This pins that something repaints it at all when
        // the screen comes back, which is the half a format test cannot see.
        [UnityTest]
        public IEnumerator ComingBackToTheHubRepaintsTheWalletRatherThanShowingTheOldFigure()
        {
            yield return OpenTheHub();

            var hub = Object.FindAnyObjectByType<HubController>();
            Assert.IsNotNull(hub, "the Hub scene has no HubController");

            var label = Named(hub, "CurrencyLabel")?.GetComponent<TMP_Text>()
                        ?? hub.GetComponentsInChildren<TMP_Text>(true)
                            .FirstOrDefault(t => t.name.Contains("Currency"));
            Assert.IsNotNull(label, "the hub has no currency label");

            var save = SaveSlotManager.CurrentSave;
            save.wallet.gold = 12;
            hub.Refresh();
            yield return null;
            StringAssert.Contains("12", label.text, "fixture: the currency line is not showing the wallet");

            // What a descent does to the wallet while the hub is not looking.
            save.wallet.gold = 4831;

            hub.gameObject.SetActive(false);
            yield return null;
            hub.gameObject.SetActive(true);
            yield return null;

            StringAssert.Contains("4831", label.text,
                "the hub came back still showing the wallet it had when it left, so gold banked " +
                "during a descent is one scene load late");
        }

        // ---- D2: leaving during the transition --------------------------------------------

        // THE WINDOW IS REAL AND IT IS 0.72 SECONDS LONG. The gate press starts
        // the zoom; EnterTheDescent -- which is what actually seeds a run --
        // runs at the END of it. So between those two moments the player has
        // committed to a descent and the save has no run in it, and any exit
        // taken here calls EndRun with nothing to end.
        //
        // Driven through the hub's OWN way out rather than through the system
        // menu's quit: both reach the same RunManager.EndRun, and the hub's
        // button needs no menu opened over a screen that is mid-zoom. The point
        // is the seam the press reaches, not which button reaches it.
        [UnityTest]
        public IEnumerator LeavingMidDescentTransitionKeepsTheGearTheStockpileAndTheRunCount()
        {
            // REAL SPEED, so the press genuinely lands mid-transition rather
            // than after it.
            HubController.MotionSpeedMultiplier = 1f;

            yield return OpenTheHub();

            var hub = Object.FindAnyObjectByType<HubController>();
            var save = SaveSlotManager.CurrentSave;

            var character = save.roster.FirstOrDefault();
            Assert.IsNotNull(character, "fixture: the save has no roster");
            character.equipment.Set(EquipmentSlot.Weapon1, "health_potion", plus: 3);

            save.stockpiledItems.Clear();
            save.stockpiledItems.Add(new InventoryEntry("health_potion", 2));

            int endedBefore = save.lifetimeRunsEnded;
            Assert.IsFalse(RunManager.HasRun, "fixture: this window is the one BEFORE a run exists");

            Click(hub, "StartRunGate");
            yield return null;

            Assert.IsFalse(RunManager.HasRun,
                "fixture: the transition already seeded the run, so the press below is not landing " +
                "in the window this test is about");

            // Out, mid-zoom.
            Click(hub, "MainMenuButton");
            yield return null;

            Assert.IsFalse(character.equipment.IsEmpty(EquipmentSlot.Weapon1),
                "leaving during the descent transition stripped the roster's gear for a descent that " +
                "had not started");
            Assert.AreEqual(1, save.stockpiledItems.Count,
                "leaving during the descent transition emptied the stockpile");
            Assert.AreEqual(endedBefore, save.lifetimeRunsEnded,
                "leaving during the descent transition counted a run that never happened");

            CollectionAssert.Contains(_navigated, Navigation.MainMenu, "the way out did not go anywhere");
        }

        // ---- C7: a scene arriving over a live transition ------------------------------------

        // The transition is a coroutine on the hub panel, so a scene load takes
        // it with it -- including the EnterTheDescent at its end. What must
        // hold is that it is taken QUIETLY and that nothing half-settled is
        // left behind: either a run exists and its snapshot agrees, or there is
        // no run.
        [UnityTest]
        public IEnumerator ASceneArrivingOverTheDescentTransitionLeavesNoHalfSettledRun()
        {
            HubController.MotionSpeedMultiplier = 1f;

            yield return OpenTheHub();

            var hub = Object.FindAnyObjectByType<HubController>();

            Click(hub, "StartRunGate");
            yield return null;
            yield return null;

            var panel = (RectTransform)hub.transform;
            Assert.AreNotEqual(1f, panel.localScale.x,
                "fixture: the transition is not under way, so there is nothing to interrupt");

            yield return SceneManager.LoadSceneAsync(Navigation.Hub, LoadSceneMode.Single);
            yield return null;
            yield return null;

            // WHICHEVER ANSWER, IT HAS TO BE A WHOLE ONE. HasRun reads the
            // snapshot's own in-band flag, so the two agreeing is the claim
            // that nothing was written half-way.
            Assert.AreEqual(RunManager.HasRun, SaveSlotManager.CurrentSave.activeRun.hasRun,
                "the run's flag and the run itself disagree after a scene arrived over the descent " +
                "transition, so something was written half-way through starting a descent");

            // And the hub that arrived is usable: the gate the previous screen
            // disabled on itself is not a state the new one can inherit.
            var again = Object.FindAnyObjectByType<HubController>();
            var gate = again.GetComponentsInChildren<Button>(includeInactive: true)
                .First(b => b.name == "StartRunGate");
            Assert.IsTrue(gate.interactable,
                "the hub came up with its gate still disabled from a transition that belonged to a " +
                "screen that no longer exists");
            Assert.AreEqual(1f, ((RectTransform)again.transform).localScale.x, 0.001f,
                "the hub came up still zoomed");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- fixture ---------------------------------------------------------------------------

        private static IEnumerator OpenTheHub()
        {
            yield return SceneManager.LoadSceneAsync(Navigation.Hub, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static GameObject Named(HubController hub, string name) =>
            hub.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private static void Click(HubController hub, string name)
        {
            var go = Named(hub, name);
            Assert.IsNotNull(go, $"the hub has no object named '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }
    }
}
