using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The overarching menu's behaviour: it opens, it closes, and exactly one
    // tab is selected at a time.
    //
    // The last of those is the one that matters for a skeleton whose panes are
    // empty: a pane and its underline are two halves of "which tab am I on",
    // and they can disagree without anything looking broken until a designer
    // fills the panes and wonders why the highlight lies.
    public class SystemMenuTests
    {
        private SystemMenuController _menu;

        private IEnumerator OpenTheHub()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the hub has no SystemMenuController");
        }

        // ---- the context rule ---------------------------------------------------
        //
        // The tab SET is decided at runtime, and the three-tab layout exists
        // ONLY at runtime -- the scene on disk carries the five-tab bar. So
        // nothing in the build can catch a three-tab bar that lays out wrong,
        // and these are the only things standing between that and a player.

        [UnityTest]
        public IEnumerator BetweenRunsTheBarHasThreeTabs()
        {
            yield return OpenTheHub();

            SaveSlotManager.CurrentSave.activeRun.hasRun = false;
            _menu.Open();
            yield return null;

            CollectionAssert.AreEqual(
                new[]
                {
                    SystemMenuTabs.IndexOf(SystemMenuTab.CharacterInventory),
                    SystemMenuTabs.IndexOf(SystemMenuTab.Options),
                    SystemMenuTabs.IndexOf(SystemMenuTab.MainMenu),
                },
                _menu.VisibleTabs.ToArray(),
                "out of a run the bar should carry exactly the three tabs that have something to show");
        }

        [UnityTest]
        public IEnumerator DuringARunTheBarGainsTheTwoRunTabs()
        {
            yield return OpenTheHub();

            SaveSlotManager.CurrentSave.activeRun.hasRun = true;
            _menu.Open();
            yield return null;

            Assert.AreEqual(SystemMenuTabs.Count, _menu.VisibleTabs.Count,
                "in a run every tab should be present");
        }

        // The tabs that are not in this context must be SWITCHED OFF, not merely
        // left out of the list. A hidden tab that is still active is still
        // clickable, and it sits under the five-tab position nobody moved it
        // away from -- which is a button in dead space that opens a pane the
        // player was told does not exist.
        [UnityTest]
        public IEnumerator TheAbsentTabsAreActuallySwitchedOff()
        {
            yield return OpenTheHub();

            SaveSlotManager.CurrentSave.activeRun.hasRun = false;
            _menu.Open();
            yield return null;

            var buttons = (UnityEngine.UI.Button[])typeof(SystemMenuController)
                .GetField("tabButtons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(_menu);

            for (int i = 0; i < buttons.Length; i++)
            {
                bool shouldShow = _menu.VisibleTabs.Contains(i);
                Assert.AreEqual(shouldShow, buttons[i].gameObject.activeSelf,
                    $"tab {i} ({SystemMenuTabs.All[i].Key}) is " +
                    (shouldShow ? "missing from" : "still live in") + " a run-less bar");
            }
        }

        // Opening pauses. Stated by the design and easy to get wrong in the
        // direction that matters: a menu that pauses and never resumes looks
        // exactly like a hang.
        [UnityTest]
        public IEnumerator OpeningPausesAndClosingResumes()
        {
            yield return OpenTheHub();

            float before = Time.timeScale;

            _menu.Open();
            yield return null;
            Assert.AreEqual(0f, Time.timeScale, "opening the menu did not pause the game");

            _menu.Close();
            yield return null;
            Assert.AreEqual(before, Time.timeScale, 0.0001f,
                "closing the menu did not put the clock back where it found it");
        }

        private GameObject[] Panes() =>
            (GameObject[])typeof(SystemMenuController)
                .GetField("panes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(_menu);

        private GameObject[] Underlines() =>
            (GameObject[])typeof(SystemMenuController)
                .GetField("tabUnderlines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(_menu);

        [UnityTest]
        public IEnumerator ItStartsClosedAndOpens()
        {
            yield return OpenTheHub();

            Assert.IsFalse(_menu.IsOpen, "the overarching menu is open on load");

            _menu.Open();
            yield return null;
            Assert.IsTrue(_menu.IsOpen);

            _menu.Close();
            yield return null;
            Assert.IsFalse(_menu.IsOpen);
        }

        [UnityTest]
        public IEnumerator EveryTabIsWiredToItsOwnPaneAndUnderline()
        {
            yield return OpenTheHub();
            _menu.Open();

            // A frame, so Start() has run and cannot come along afterwards and
            // reset the selection behind a later assertion.
            yield return null;

            var panes = Panes();
            var underlines = Underlines();

            Assert.AreEqual(SystemMenuTabs.PaneOwners.Count, panes.Length, "a pane owner has no pane");
            Assert.AreEqual(SystemMenuTabs.Count, underlines.Length, "a tab has no underline");

            for (int i = 0; i < SystemMenuTabs.Count; i++)
            {
                _menu.Select(i);
                yield return null;

                var def = SystemMenuTabs.All[i];

                // One pane per tab, now that Character and Inventory are one tab
                // rather than two doors onto one pane.
                string paneKey = SystemMenuTabs.All[SystemMenuTabs.PaneIndexFor(i)].Key;

                var livePanes = panes.Where(p => p.activeSelf).Select(p => p.name).ToList();
                CollectionAssert.AreEqual(new[] { $"SystemPane{paneKey}" }, livePanes,
                    $"selecting tab {i} ({def.Key}) should leave exactly pane '{paneKey}' showing");

                // The UNDERLINE still follows the tab that was clicked, not the
                // pane it opened -- otherwise clicking Inventory would light
                // Character and the player could not tell which door they used.
                var liveMarks = underlines.Where(u => u.activeSelf).Select(u => u.name).ToList();
                CollectionAssert.AreEqual(new[] { $"SystemTab{def.Key}Underline" }, liveMarks,
                    $"selecting tab {i} ({def.Key}) underlined a different tab");
            }
        }

        // Start() runs a frame after the object is activated, so its default
        // must not overwrite a tab the opener already picked. The first
        // screenshot of this menu was exactly that: Options showing, Character
        // underlined, because the default landed second.
        [UnityTest]
        public IEnumerator OpeningStraightOntoATabIsNotResetByTheDefault()
        {
            yield return OpenTheHub();

            _menu.Open();
            _menu.Select(SystemMenuTab.Options);

            // The frame on which Start() lands.
            yield return null;
            yield return null;

            Assert.AreEqual(SystemMenuTabs.IndexOf(SystemMenuTab.Options), _menu.SelectedIndex,
                "the default selection overwrote a tab the caller had already chosen");
        }

        [UnityTest]
        public IEnumerator ClickingATabSelectsIt()
        {
            yield return OpenTheHub();
            _menu.Open();
            yield return null;

            var button = _menu.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true)
                .FirstOrDefault(b => b.name == "SystemTabOptions");
            Assert.IsNotNull(button, "the Options tab has no button");

            button.onClick.Invoke();
            yield return null;

            Assert.AreEqual(SystemMenuTabs.IndexOf(SystemMenuTab.Options), _menu.SelectedIndex);
        }
    }
}
