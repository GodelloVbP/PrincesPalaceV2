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
using PrincesPalace.Domain.Party;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE SYSTEM MENU'S PANES ARE OPENED AND CLOSED, NEVER DESTROYED.
    //
    // Selecting a tab is SetActive on one pane and off on another, and the menu
    // itself is opened and closed over the life of a whole scene. So every pane
    // here sees OnEnable dozens of times per session, while its Wire() runs
    // once -- guarded by a `_wired` bool, or moved into Start entirely.
    //
    // That guard is the whole defence, and the failure mode if it goes is
    // quiet: the pane looks right, and one click does its thing two or three
    // times. A gold grant paid twice, a character stepped past, a setting
    // jumping two rows per press. So each of these opens its pane three times
    // and then presses ONCE, against an effect that is NOT idempotent -- a
    // second fire has to be visible, or the test is only watching the guard it
    // cannot see.
    //
    // scenarios A8, B5, B6, B7, B12, C4, D8 (docs/hunt/SCENARIOS.md).
    public class SystemMenuPaneLifecycleTests
    {
        private const int Cycles = 3;

        private string _root;
        private SystemMenuController _menu;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-pane-life-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            SharedScene.AfterTest();
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- B5: the dossier -----------------------------------------------------

        // STEPPING IS THE ONE THING ON THIS PANE THAT COUNTS. With three
        // characters on the roster, a Next wired three times lands back on the
        // character it started on -- which is exactly the shape that would look
        // like a button that does nothing.
        [UnityTest]
        public IEnumerator ThreeOpeningsOfTheDossierStillStepOneCharacterPerPress()
        {
            yield return OpenTheMenu("Hub");

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the hub has no CharacterDossierController");

            Assert.GreaterOrEqual(SaveSlotManager.CurrentSave.roster.Count, 3,
                "fixture: this needs a roster of at least three, or three fires would be invisible");

            yield return Cycle(dossier.gameObject);

            string before = NameOnTheDossier(dossier);
            Click("DossierNextCharacter");
            yield return null;

            string after = NameOnTheDossier(dossier);
            Assert.AreNotEqual(before, after,
                "one press of Next left the dossier on the same character - with a three-strong " +
                "roster that is what a listener wired once per opening looks like");
        }

        // ---- B6: the party pane ---------------------------------------------------

        // A CLICK SELECTS; IT DOES NOT COMMIT. Two clicks are what swap a pair,
        // so a seat button wired three times turns one click into a swap
        // nobody asked for.
        [UnityTest]
        public IEnumerator ThreeOpeningsOfThePartyPaneStillMakeOneSeatClickASelection()
        {
            yield return OpenTheMenu("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "owl", "sheep", "bear" };

            _menu.Select(SystemMenuTab.Party);
            yield return null;

            var party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            Assert.IsNotNull(party, "the hub carries no PartyController");

            yield return Cycle(party.gameObject);

            var seated = party.Formation.SeatIds.ToList();

            Click("PartySeat0Button");
            yield return null;

            Assert.AreEqual(seated[PartySeat.Front], party.Formation.SelectedId,
                "one click on the front seat did not leave its occupant selected");

            for (int seat = 0; seat < seated.Count; seat++)
            {
                Assert.AreEqual(seated[seat], party.Formation.SeatIds[seat],
                    $"seat {seat} changed occupant on a single click, so the click committed a swap - " +
                    "which is what a seat listener added once per opening does");
            }
        }

        // ---- B7: the options pane --------------------------------------------------

        // A STEPPER IS THE PERFECT WITNESS: it moves exactly one row per press
        // and clamps at its ends, so a listener wired three times moves three
        // rows and is unmistakable.
        [UnityTest]
        public IEnumerator ThreeOpeningsOfTheOptionsPaneStillMoveOneRowPerStep()
        {
            yield return OpenTheMenu("Hub");

            _menu.Select(SystemMenuTab.Options);
            yield return null;

            var options = Object.FindAnyObjectByType<OptionsController>(FindObjectsInactive.Include);
            Assert.IsNotNull(options, "the Options pane has no controller");

            // Parked at the bottom of the table so a step has room to move and
            // is not eaten by the clamp.
            GameSettings.SetFpsLimitIndex(0);
            Assert.Greater(GameSettings.FpsLimits.Length, 3,
                "fixture: the fps table is too short for three fires to be distinguishable from one");

            yield return Cycle(options.gameObject);

            Click("OptionsRowfpsNext");
            yield return null;

            Assert.AreEqual(1, GameSettings.FpsLimitIndex,
                "one press of the fps stepper moved more than one row, so its listener was added " +
                "again on every opening of the pane");

            // AND THE RUNTIME COMPONENTS ARE NOT STACKED EITHER. This pane's
            // Wire() does not only add listeners -- it AddComponents a
            // BarSlider per slider (OptionRow, gamepad-navigation phase 2
            // step B, is attached once at BUILD time instead, ScreenRegistry.
            // WireOptions, precisely so re-wiring on every opening cannot
            // pile it up the way a runtime AddComponent could), which an
            // unguarded re-wire would pile up invisibly until every drag
            // fired four times.
            int sliders = options.GetComponentsInChildren<BarSlider>(includeInactive: true).Length;

            yield return Cycle(options.gameObject);

            Assert.AreEqual(sliders, options.GetComponentsInChildren<BarSlider>(includeInactive: true).Length,
                "the Options pane grew more BarSlider components by being opened again");
        }

        // ---- B12: the run statistics pane -------------------------------------------

        // OnEnable => Refresh(), and the figures move while the pane is shut --
        // that is what a descent is. SystemMenuRunStatsTests pins the FIGURES
        // against a run it built; this pins that re-opening repaints them.
        [UnityTest]
        public IEnumerator ReOpeningRunStatsRepaintsTheFiguresRatherThanShowingTheOldOnes()
        {
            RunManager.StartRun(4242);
            RunManager.BankPayout(40);

            yield return OpenTheMenu("Map");

            _menu.Select(SystemMenuTab.RunStats);
            yield return null;
            yield return null;

            var stats = Object.FindAnyObjectByType<RunStatsController>(FindObjectsInactive.Include);
            Assert.IsNotNull(stats, "the Map's menu has no RunStatsController");

            var label = Named("RunStatsValuegold_earned")?.GetComponent<TMP_Text>();
            Assert.IsNotNull(label, "the run statistics pane has no gold_earned figure");
            Assert.AreEqual("40", label.text, "fixture: the pane is not reading the run's gold");

            // What a descent does while the pane is shut.
            RunManager.BankPayout(25);

            stats.gameObject.SetActive(false);
            yield return null;
            stats.gameObject.SetActive(true);
            yield return null;

            Assert.AreEqual("65", label.text,
                "the pane came back showing the figure it had when it closed, so every number on it " +
                "is as old as the last time it was opened");
        }

        // ---- A8: a second toast over a live one --------------------------------------

        // A NEW Show() RESTARTS THE TIMER rather than stacking, and PartyToast's
        // own header argues for it: two swaps in quick succession must snap back
        // to full alpha carrying the NEW text, instead of finishing a fade whose
        // message no longer matches what just happened.
        [UnityTest]
        public IEnumerator ASecondToastRestartsTheTimerAndCarriesTheNewMessage()
        {
            yield return OpenTheMenu("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };

            _menu.Select(SystemMenuTab.Party);
            yield return null;

            var party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            party.ClickSeat(PartySeat.Front);
            party.ClickSeat(PartySeat.Rear);
            yield return null;

            var toast = Named("PartyToast");
            var group = toast.GetComponent<CanvasGroup>();
            string firstMessage = Named("PartyToastText").GetComponent<TMP_Text>().text;
            Assert.AreEqual(1f, group.alpha, 0.01f, "fixture: the first toast never showed");

            // Into the hold, far enough that a toast which merely CONTINUED
            // would be visibly on its way down by the end of this test.
            yield return Settle(PartyToast.HoldSeconds * 0.9f);
            Assert.IsTrue(toast.activeSelf, "fixture: the first toast is already gone");

            party.ClickSeat(PartySeat.Middle);
            party.ClickSeat(PartySeat.Rear);
            yield return null;

            string secondMessage = Named("PartyToastText").GetComponent<TMP_Text>().text;
            Assert.AreNotEqual(firstMessage, secondMessage,
                "the second commit did not change the message, so this test cannot tell the two apart");
            Assert.AreEqual(1f, group.alpha, 0.01f,
                "the second toast arrived part-way through the first one's fade rather than snapping " +
                "back to full");

            // The restart is what the timer does, not just the alpha: a
            // continued toast would be gone by now.
            yield return Settle(PartyToast.HoldSeconds * 0.5f);
            Assert.IsTrue(toast.activeSelf,
                "the second toast expired on the FIRST one's clock, so a message that just arrived " +
                "vanished early");
        }

        // ---- C4: a scene arriving over a live toast -----------------------------------

        [UnityTest]
        public IEnumerator ASceneArrivingOverALiveToastLeavesNoToastOnTheNextScreen()
        {
            yield return OpenTheMenu("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };

            _menu.Select(SystemMenuTab.Party);
            yield return null;

            var party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            party.ClickSeat(PartySeat.Front);
            party.ClickSeat(PartySeat.Rear);
            yield return null;

            Assert.IsTrue(Named("PartyToast").activeSelf, "fixture: no toast was up when the scene changed");

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var carried = Object.FindObjectsByType<PartyToast>(FindObjectsInactive.Include)
                .FirstOrDefault(t => t.gameObject.activeInHierarchy);

            Assert.IsNull(carried,
                "the next screen came up with a toast already showing, so a fade that outlived its " +
                "scene left a message about a swap that happened somewhere else");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- D8: the clock across a scene load ------------------------------------------

        // ASSERTED BEFORE TEARDOWN, deliberately. TestGlobals.ResetAll puts
        // Time.timeScale back to 1, so a fixture that checked after it would
        // pass while shipping a game that comes up frozen -- and a frozen clock
        // is invisible rather than loud: every WaitForSeconds in the fight
        // simply never finishes.
        [UnityTest]
        public IEnumerator ASceneLoadedWithTheMenuStillOpenDoesNotArriveFrozen()
        {
            yield return OpenTheMenu("Hub");

            Assert.AreEqual(0f, Time.timeScale, "fixture: the menu did not pause the game");
            Assert.IsTrue(_menu.IsOpen, "fixture: the menu is not open");

            // Out from under it, with the menu still up and still holding the
            // clock at zero.
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;

            Assert.AreEqual(1f, Time.timeScale,
                "the scene arrived at timeScale 0 because the menu was torn down while paused - the " +
                "next screen comes up frozen, which looks like a hang and is not one");

            LogAssert.NoUnexpectedReceived();
        }

        // ---- fixture ---------------------------------------------------------------------

        // SHARED ACROSS THIS FIXTURE while consecutive tests ask for the same
        // scene (SharedScene). A reused scene has put its panes through MORE
        // openings than a fresh one, which is the direction these tests are
        // about, so reuse costs them nothing. Put back per test: the menu
        // closed (Close resumes the clock Open paused), the party toast down
        // and blank (Close strands it mid-hold, still showing), and the tab
        // the menu opens on -- a fresh menu lands on its default in Start(),
        // which a reused one ran long ago, and the dossier test cycles a pane
        // that only enables under that default tab.
        private IEnumerator OpenTheMenu(string scene)
        {
            yield return SharedScene.Ensure(scene);

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, $"{scene} has no SystemMenuController");

            if (_menu.IsOpen) _menu.Close();
            var toast = _menu.GetComponentsInChildren<PartyToast>(includeInactive: true).FirstOrDefault();
            if (toast != null) toast.gameObject.SetActive(false);
            var toastText = Named("PartyToastText")?.GetComponent<TMP_Text>();
            if (toastText != null) toastText.text = "";

            _menu.Open();
            _menu.Select(SystemMenuTabs.DefaultFor(_menu.InDescent));
            yield return null;
            yield return null;
        }

        // Three closings and three openings, which is a quiet evening with this
        // menu rather than an unusual one.
        private static IEnumerator Cycle(GameObject pane)
        {
            for (int i = 0; i < Cycles; i++)
            {
                pane.SetActive(false);
                yield return null;
                pane.SetActive(true);
                yield return null;
            }
        }

        private static IEnumerator Settle(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        private GameObject Named(string name) =>
            _menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string buttonName)
        {
            var go = Named(buttonName);
            Assert.IsNotNull(go, $"the menu has no '{buttonName}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private static string NameOnTheDossier(CharacterDossierController dossier)
        {
            var label = dossier.GetComponentsInChildren<TMP_Text>(includeInactive: true)
                .FirstOrDefault(t => t.name == "DossierName");

            Assert.IsNotNull(label, "the dossier has no name label to read the stepped character off");
            return label.text;
        }
    }
}
