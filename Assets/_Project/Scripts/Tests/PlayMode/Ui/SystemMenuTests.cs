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

        [TearDown]
        public void AfterEach() => SharedScene.AfterTest();

        // ADAPTED for gamepad-navigation phase 2, step B: SystemMenuController
        // no longer polls Escape itself at all -- Open()/Close() are ordinary
        // method calls, pushing/popping a NavContext (plan section 3/4), so
        // there is no "is it listening" precondition left to guard. What
        // still matters, and what these two now check instead: the
        // controller exists and can be opened from closed in every scene
        // that carries it, in the shape whatever DOES call Open() (the hub's
        // own NavContext.Cancel handler today) needs to find.
        [UnityTest]
        public IEnumerator TheMenuCanBeOpenedFromClosed()
        {
            yield return OpenTheHub();

            Assert.IsFalse(_menu.IsOpen, "the menu should start closed");
            _menu.Open();
            Assert.IsTrue(_menu.IsOpen, "Open() should work from a closed state");
        }

        // Same precondition, in the two scenes where the menu opens over
        // something that is already using Escape.
        [UnityTest]
        public IEnumerator TheMenuExistsInEverySceneThatCarriesIt()
        {
            foreach (string scene in new[] { "Map", "Fight" })
            {
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                yield return null;
                yield return null;

                var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
                Assert.IsNotNull(menu, $"{scene} has no SystemMenuController");
                Assert.IsFalse(menu.IsOpen, $"the menu should start closed in {scene}");
                menu.Open();
                Assert.IsTrue(menu.IsOpen, $"Open() should work from a closed state in {scene}");
            }
        }

        // CANCEL, WITH SOMETHING ELSE ALREADY UP.
        //
        // RE-ADAPTED for gamepad-navigation phase 3, item 1 (docs/
        // GAMEPAD_NAVIGATION_PLAN.md, AUDIT.md #158): this used to be
        // HubController.HandleEscape's own job -- a priority branch that
        // checked the glossary/debug-menu/relic-draft panels itself and
        // closed whichever was open before ever considering the system
        // menu. That branch is gone now that GlossaryController (and
        // DebugMenuController, and RelicDraftController) push their OWN
        // NavContext on open: while the glossary is up it, not the hub, is
        // top of the stack, so the real dispatcher calls the GLOSSARY's own
        // Cancel handler (GlossaryController.Close) and never reaches
        // HandleEscape at all -- GlossaryGamepadNavigationTests'
        // Cancel_ClosesTheGlossary_AndTheGateIsReselected proves that
        // through the real dispatcher, the only place this claim can still
        // be tested honestly.
        //
        // What is left for THIS file to pin is narrower and still real:
        // HandleEscape itself no longer knows the glossary exists, so
        // calling it directly no longer closes a glossary a caller happened
        // to switch on by hand -- it only ever opens the system menu, full
        // stop. A regression that resurrected the old branch (or a new one
        // like it) would fail this by closing the glossary here too.
        [UnityTest]
        public IEnumerator HandleEscapeNoLongerKnowsAboutTheGlossary_ItOnlyEverOpensTheMenu()
        {
            yield return OpenTheHub();

            var hub = Object.FindAnyObjectByType<HubController>(FindObjectsInactive.Include);
            Assert.IsNotNull(hub, "the hub has no HubController");

            hub.SetGlossary(true);
            Assert.IsFalse(_menu.IsOpen);

            hub.HandleEscape();

            Assert.IsTrue(hub.GlossaryIsOpen,
                "HandleEscape should no longer special-case the glossary -- that panel closes through its " +
                "own NavContext.Cancel now, never through the hub's handler directly");
            Assert.IsTrue(_menu.IsOpen, "HandleEscape's only remaining job is opening the system menu");

            yield return null;
        }

        // The other half: with nothing else up, Cancel must still open it.
        // A guard that fixed the old race by never opening would pass the
        // test above and be useless.
        [UnityTest]
        public IEnumerator CancelWithNothingElseUpOpensTheMenu()
        {
            yield return OpenTheHub();

            var hub = Object.FindAnyObjectByType<HubController>(FindObjectsInactive.Include);
            Assert.IsNotNull(hub, "the hub has no HubController");

            Assert.IsFalse(_menu.IsOpen);
            hub.HandleEscape();
            Assert.IsTrue(_menu.IsOpen, "the hub's own Cancel handler did not open the menu when nothing else owned it");

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

        // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene). What a test
        // leaves up is the menu (open, which also leaves the clock paused --
        // Close puts back the clock Open found) and the glossary; both are
        // put back here through the controllers' own calls. On a fresh load
        // neither is open and this does nothing.
        private IEnumerator OpenTheHub()
        {
            yield return SharedScene.Ensure("Hub");

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the hub has no SystemMenuController");

            if (_menu.IsOpen) _menu.Close();
            var hub = Object.FindAnyObjectByType<HubController>(FindObjectsInactive.Include);
            if (hub != null && hub.GlossaryIsOpen) hub.SetGlossary(false);
            Time.timeScale = 1f;
        }

        // ---- the context rule ---------------------------------------------------
        //
        // The tab SET is decided at runtime, and the three-tab layout exists
        // ONLY at runtime -- the scene on disk carries the five-tab bar. So
        // nothing in the build can catch a three-tab bar that lays out wrong,
        // and these are the only things standing between that and a player.

        [UnityTest]
        public IEnumerator BetweenRunsTheBarHasFourTabs()
        {
            yield return OpenTheHub();

            SaveSlotManager.CurrentSave.activeRun.hasRun = false;
            _menu.Open();
            yield return null;

            CollectionAssert.AreEqual(
                new[]
                {
                    SystemMenuTabs.IndexOf(SystemMenuTab.CharacterInventory),
                    SystemMenuTabs.IndexOf(SystemMenuTab.Party),
                    SystemMenuTabs.IndexOf(SystemMenuTab.Options),
                    SystemMenuTabs.IndexOf(SystemMenuTab.MainMenu),
                },
                _menu.VisibleTabs.ToArray(),
                "out of a run the bar should carry exactly the four tabs that have something to show " +
                "(Party joined this set - it is meaningful at camp, see docs/handoffs/archive/party_screen/DECISIONS.md)");
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

            Assert.AreEqual(4, _menu.VisibleTabs.Count,
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
            SharedScene.MarkDirty("asserts the menu is closed on load, not after a reset closed it");
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
            SharedScene.MarkDirty("needs the menu's first activation, when Start() has yet to run");
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
