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

        // THE BUG THIS EXISTS FOR: the controller used to be attached to the
        // menu's own modal, which is inactive until the menu opens. So its
        // Update() did not run while it was closed, and the Escape that is
        // supposed to OPEN the menu could never fire -- Escape only ever closed
        // one that something else had already opened.
        //
        // It survived a whole design pass listing "Escape opens the menu" as
        // built, because every test and every screenshot calls Open() directly
        // and legacy Input cannot be pressed headlessly. Nothing in CI was in a
        // position to notice, so this checks the PRECONDITION instead: the
        // thing that listens for Escape has to be running while the menu is
        // shut.
        [UnityTest]
        public IEnumerator TheMenuIsListeningWhileItIsClosed()
        {
            yield return OpenTheHub();

            Assert.IsFalse(_menu.IsOpen, "the menu should start closed");
            Assert.IsTrue(_menu.isActiveAndEnabled,
                "SystemMenuController is not running while the menu is closed, so its Update never " +
                "polls Escape and nothing can open the menu with the keyboard");
        }

        // Same precondition, in the two scenes where the menu opens over
        // something that is already using Escape.
        [UnityTest]
        public IEnumerator TheMenuIsListeningInEverySceneThatCarriesIt()
        {
            foreach (string scene in new[] { "Map", "Fight" })
            {
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                yield return null;
                yield return null;

                var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
                Assert.IsNotNull(menu, $"{scene} has no SystemMenuController");
                Assert.IsTrue(menu.isActiveAndEnabled,
                    $"the system menu in {scene} is not listening while closed, so Escape cannot open it");
            }
        }

        // ESCAPE, WITH SOMETHING ELSE ALREADY UP.
        //
        // The hub closes its glossary on Escape and this menu opens on Escape,
        // and since the fix that made the menu listen while closed they sit on
        // the SAME GameObject -- so Unity's Update order between them is
        // whatever serialization order happens to be. Driven in BOTH orders
        // here, because "it works" in one order is exactly what this class of
        // bug looks like right up until it does not.
        //
        // Both handlers are split from their key reads precisely so this test
        // can exist: legacy Input cannot be pressed headlessly.
        [UnityTest]
        public IEnumerator EscapeOverTheGlossaryDoesNotAlsoOpenTheMenu()
        {
            yield return OpenTheHub();

            var hub = Object.FindAnyObjectByType<HubController>(FindObjectsInactive.Include);
            Assert.IsNotNull(hub, "the hub has no HubController");

            // Hub first.
            EscapeKey.Reset();
            hub.SetGlossary(true);
            Assert.IsFalse(_menu.IsOpen);

            hub.HandleEscape();
            _menu.HandleEscape();

            Assert.IsFalse(_menu.IsOpen,
                "Escape closed the glossary and opened the system menu on top of it");

            // Menu first, same press.
            EscapeKey.Reset();
            hub.SetGlossary(true);

            _menu.HandleEscape();
            hub.HandleEscape();

            Assert.IsFalse(_menu.IsOpen,
                "the system menu opened on an Escape that belonged to the glossary");

            yield return null;
        }

        // The other half: with nothing else up, Escape must still open it.
        // A guard that fixed the race by never opening would pass the test
        // above and be useless.
        [UnityTest]
        public IEnumerator EscapeWithNothingElseUpOpensTheMenu()
        {
            yield return OpenTheHub();
            EscapeKey.Reset();

            Assert.IsFalse(_menu.IsOpen);
            _menu.HandleEscape();
            Assert.IsTrue(_menu.IsOpen, "Escape did not open the menu when nothing else owned the key");

            // And closes it again on the next press.
            EscapeKey.Reset();
            _menu.HandleEscape();
            Assert.IsFalse(_menu.IsOpen, "Escape did not close the menu it had just opened");

            yield return null;
        }

        // Opening the menu pauses; closing puts the clock back where it was
        // rather than assuming 1, so a slow-motion or fast-forward the game set
        // for its own reasons survives a visit to the menu.
        [UnityTest]
        public IEnumerator ClosingRestoresTheClockItFound()
        {
            yield return OpenTheHub();

            Time.timeScale = 0.5f;
            try
            {
                _menu.Open();
                Assert.AreEqual(0f, Time.timeScale, "opening the menu did not pause");

                _menu.Close();
                Assert.AreEqual(0.5f, Time.timeScale, 0.0001f,
                    "closing the menu reset the clock to 1 instead of what it found");
            }
            finally
            {
                Time.timeScale = 1f;
            }

            yield return null;
        }

        // A menu that opens itself onto "CONTENT TO COME" reads as broken. The
        // design wants Floor map during a run; that pane is a placeholder, so
        // the default has to fall back until it is built.
        [Test]
        public void TheDefaultTabIsNeverAPlaceholder()
        {
            foreach (bool inRun in new[] { false, true })
            {
                var tab = SystemMenuTabs.DefaultFor(inRun);
                var def = SystemMenuTabs.All[SystemMenuTabs.IndexOf(tab)];

                Assert.IsTrue(def.Built,
                    $"the menu opens on '{def.Key}' when inRun={inRun}, and that pane is still a placeholder");
            }
        }

        // The default also has to be a tab the context actually shows -- opening
        // onto a pane whose tab is hidden leaves no way back to it.
        [Test]
        public void TheDefaultTabIsVisibleInItsOwnContext()
        {
            foreach (bool inRun in new[] { false, true })
            {
                int index = SystemMenuTabs.IndexOf(SystemMenuTabs.DefaultFor(inRun));

                CollectionAssert.Contains(SystemMenuTabs.VisibleIndices(inRun).ToList(), index,
                    $"the default tab for inRun={inRun} is not among the tabs that context shows");
            }
        }

        // Silent-wrong-answer guard. IndexOf used to return 0 for anything it
        // could not find, so every miss came back as Character & Inventory.
        [Test]
        public void EveryTabInTheEnumIsInTheTable()
        {
            foreach (SystemMenuTab tab in System.Enum.GetValues(typeof(SystemMenuTab)))
            {
                Assert.DoesNotThrow(() => SystemMenuTabs.IndexOf(tab),
                    $"{tab} is in the enum but not in SystemMenuTabs.All");
            }
        }

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
        public IEnumerator InADescentTheBarGainsTheTwoRunTabs()
        {
            // ON THE MAP, not in the hub with hasRun poked true.
            //
            // That is what this test used to do, and it was asserting the wrong
            // rule. A run exists from the moment the relic draft is rolled, and
            // that draft is offered in the HUB and deliberately survives
            // leaving and coming back -- so "a run exists" was true while the
            // player stood in the hub with no descent under way, and Floor map
            // and Run statistics showed up on a screen with no floor to name.
            //
            // The tab set is a property of the scene now, so this asks the
            // scene.
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the map has no SystemMenuController");

            menu.Open();
            yield return null;

            Assert.AreEqual(SystemMenuTabs.Count, menu.VisibleTabs.Count,
                "in a descent every tab should be present");
        }

        // The other half, and the one the author actually reported: standing in
        // the hub there is no floor and no descent, so those two tabs have
        // nothing to describe and must not be there -- even mid-run, which is a
        // state the hub can genuinely be in.
        [UnityTest]
        public IEnumerator TheHubNeverShowsTheDescentTabs()
        {
            yield return OpenTheHub();

            var save = SaveSlotManager.CurrentSave;
            if (save?.activeRun != null) save.activeRun.hasRun = true;

            _menu.Open();
            yield return null;

            Assert.AreEqual(3, _menu.VisibleTabs.Count,
                "the hub showed the descent tabs; it has no descent to describe");

            foreach (var absent in new[] { SystemMenuTab.FloorMap, SystemMenuTab.RunStats })
            {
                CollectionAssert.DoesNotContain(_menu.VisibleTabs.ToList(),
                    SystemMenuTabs.IndexOf(absent), $"{absent} is showing in the hub");
            }
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
