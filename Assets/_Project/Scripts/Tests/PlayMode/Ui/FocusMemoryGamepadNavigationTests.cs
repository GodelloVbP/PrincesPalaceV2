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
    // FOCUS MEMORY (docs/GAMEPAD_NAVIGATION_PLAN.md sections 4 and 6,
    // AUDIT.md #163's second half): a context popped and re-entered lands
    // where the player left it, not on its entry.
    //
    // Before this, NavContext.Remember was called from nowhere in the whole
    // project -- grepped -- so "remembered ?? entry" was structurally only
    // ever entry, and every one of the three claims below was silently false.
    // The recording now happens in NavigationInputModule.Process, once, for
    // every context: no screen calls Remember and no screen can forget to.
    //
    // Driven through the REAL production dispatcher (scripted BaseInput via
    // inputOverride, `yield return null`, assert resulting state), the same
    // shape HubGamepadNavigationTests / SystemMenuGamepadNavigationTests /
    // TalentGamepadNavigationTests already established for these three
    // scenes -- never a direct Remember/ResolveSelection call standing in for
    // a press, which would prove the mechanism against itself rather than
    // against what a player does.
    //
    // ONE PRESS PER STEP wherever it is enough, and JourneyFixture's own
    // real-time settle where it is not: the ordinary-context Move gate
    // (StandaloneInputModule.AllowMoveEventProcessing's own real-time
    // moveRepeatDelay, JourneyFixture.Move's own header) silently drops a
    // second chained Move inside 0.5s. Move below pays that settle once for
    // every caller rather than each test rediscovering the drop; everything
    // else here is reached by Submit, Cancel or the tab trigger, none of
    // which read that gate.
    public class FocusMemoryGamepadNavigationTests
    {
        private string _root;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-focus-memory-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();

            // No real scene crossing in this file: every claim here is about
            // one screen's own context stack, and a Cancel that fell through
            // to Navigation.Go would swap the scene out from under the
            // assertion instead.
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

        private IEnumerator TakeOverInput()
        {
            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the freshly loaded scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;
            yield return null;
            yield return null;
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

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

        private IEnumerator PressSubmit()
        {
            _input.SubmitDown = true;
            yield return DriveFrame();
        }

        private IEnumerator PressCancel()
        {
            _input.CancelDown = true;
            yield return DriveFrame();
        }

        // START, which is what opens and closes the overarching menu since
        // the owner's 2026-09-19 call -- the hub is a root, so its Cancel is
        // deliberately nothing at all now.
        private IEnumerator PressSystemMenu()
        {
            _input.SystemMenuDown = true;
            yield return DriveFrame();
        }

        // LT/RT step tabs since the owner's 2026-09-19 hardware-round call
        // (LB/RB were reassigned to section/character paging) -- a level,
        // not an edge, so the pulse returns it to 0 AND drives that frame
        // too (JourneyFixture.PressTriggerRight's own header on why an
        // unread 0 leaves TriggerPressed's `armed` flag stuck false for the
        // next pull).
        private IEnumerator PressTriggerRight()
        {
            _input.TriggerRight = 1f;
            yield return DriveFrame();
            _input.TriggerRight = 0f;
            yield return DriveFrame();
        }

        private static GameObject Node(string name) => GameObject.Find(name);

        // ---- (a) the pop case, on the Hub --------------------------------------

        // The plain reading of section 4's pop rule: "restores the previous
        // context's remembered node". The hub's ENTRY is the gate
        // (HubController.RegisterNavContext), so a test that opened a modal
        // straight off the entry could not tell memory from a fallback -- the
        // Move onto a second building is what makes the two answers differ.
        [UnityTest]
        public IEnumerator Hub_AModalOpenedFromASecondBuilding_ReturnsToThatBuildingOnCancel_NotToTheEntry()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return TakeOverInput();

            var hub = Object.FindAnyObjectByType<HubController>(FindObjectsInactive.Include);
            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(hub, "the hub scene has no HubController");
            Assert.IsNotNull(menu, "the hub scene has no SystemMenuController");

            var gate = Node("StartRunGate");
            var characterSheet = Node("CharacterSheetBuilding");
            Assert.AreEqual(gate, EventSystem.current.currentSelectedGameObject,
                "fixture: the hub opens on its own entry, the gate");

            // Two Rights off the gate is CharacterSheetBuilding -- the hub's
            // own ring, pinned literally by
            // HubGamepadNavigationTests.TheWholeRing_RightThenLeft. It used to
            // be one Up, until the hardware play-test rejected the 2x2 Grid
            // that made it one; Up off the gate means the left arm now, whose
            // first stop (PrincipalityBuilding) is permanently disabled and so
            // could not open anything to pop.
            yield return Move(1f, 0f);
            yield return Move(1f, 0f);
            Assert.AreEqual(characterSheet, EventSystem.current.currentSelectedGameObject,
                "fixture: two Rights off the gate should stand on CharacterSheetBuilding");

            // Submit opens the character overlay, which IS SystemMenu.Root
            // (HubController.WireCharacterOverlay's own comment), so this
            // pushes a real second context over the hub's rather than
            // simulating one.
            yield return PressSubmit();
            Assert.IsTrue(menu.IsOpen, "fixture: Submit on CharacterSheetBuilding should open the menu over the hub");
            Assert.AreNotEqual(characterSheet, EventSystem.current.currentSelectedGameObject,
                "fixture: while the menu is up it owns the selection, not the hub");

            yield return PressCancel();
            Assert.IsFalse(menu.IsOpen, "fixture: Cancel on an unclaimed pane should close the menu again");

            yield return null;

            Assert.AreEqual(characterSheet, EventSystem.current.currentSelectedGameObject,
                "closing the modal should hand focus back to the building the player opened it from -- " +
                "the hub's remembered node. Landing on the gate instead would be the entry fallback, " +
                "which is what happened for as long as nothing ever called NavContext.Remember");
        }

        // ---- (b) the cross-visit case, on the System Menu ----------------------

        // Section 4's push rule ("Push selects remembered ?? entry") is only
        // meaningful if a context SURVIVES being closed -- a fresh NavContext
        // per open can only ever answer entry. SystemMenuController now keeps
        // its context across PopNavContext for exactly this claim, and this
        // test is what says so.
        [UnityTest]
        public IEnumerator SystemMenu_ReopenedAfterAClose_LandsBackInsideTheSamePane_NotOnTheTab()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return TakeOverInput();

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub scene has no SystemMenuController");

            yield return PressSystemMenu();
            Assert.IsTrue(menu.IsOpen, "fixture: Start should open the system menu from the hub");

            // Two trigger pulls, each of which lands INSIDE the new pane
            // rather than on its tab (SystemMenuController.StepTab's own
            // contract), so this reaches the THIRD visible tab's pane -- read
            // off VisibleTabs rather than hardcoded, since which tabs are
            // visible depends on whether a descent is under way.
            var visible = menu.VisibleTabs.ToList();
            Assert.GreaterOrEqual(visible.Count, 3, "fixture: this claim needs at least three visible tabs");
            yield return PressTriggerRight();
            yield return PressTriggerRight();

            Assert.AreEqual(visible[2], menu.SelectedIndex,
                "fixture: two TriggerRight pulls off the first visible tab should select the third");

            var insideThePane = EventSystem.current.currentSelectedGameObject;
            Assert.IsNotNull(insideThePane, "fixture: a shoulder press should leave something selected");

            // The context's own ENTRY is the selected TAB button
            // (RefreshSelectables), so a node inside the pane is provably not
            // the entry -- which is what makes the reopen assertion below a
            // claim about memory rather than about the entry happening to be
            // right.
            var tabButtons = menu.GetComponentsInChildren<Button>(includeInactive: true)
                .Where(b => b.name.StartsWith("SystemTab"))
                .Select(b => b.gameObject)
                .ToList();
            Assert.IsNotEmpty(tabButtons, "fixture: the menu has no tab buttons to tell a pane node apart from");
            Assert.IsFalse(tabButtons.Contains(insideThePane),
                "fixture: StepTab should land inside the pane, not on a tab button -- otherwise the reopen " +
                "assertion below would pass on the entry alone and prove nothing about memory");

            yield return PressCancel();
            Assert.IsFalse(menu.IsOpen, "fixture: Cancel on an unclaimed pane should close the menu");

            yield return PressSystemMenu();
            Assert.IsTrue(menu.IsOpen, "fixture: Start should reopen the menu");

            Assert.AreEqual(insideThePane, EventSystem.current.currentSelectedGameObject,
                "reopening the menu should land on the node the last visit left focused -- and land on it " +
                "IMMEDIATELY, in Open()'s own PushNavContext rather than a frame later, so the menu never " +
                "draws a frame focused somewhere else");
        }

        // ---- (c) a remembered node that has since been hidden ------------------

        // The other half of the rule, and the reason NavContext cannot own it
        // alone: a hidden node is still DECLARED (a controller declares what
        // it owns, not what is on screen), so memory has to be checked
        // against what is actually usable or the focus lands somewhere the
        // player can neither see nor move off.
        [UnityTest]
        public IEnumerator Talents_ARememberedInvestButton_ThatHasSinceBeenHidden_FallsBackToTheEntry()
        {
            yield return SceneManager.LoadSceneAsync("Talents", LoadSceneMode.Single);
            yield return TakeOverInput();

            var talents = Object.FindAnyObjectByType<TalentController>(FindObjectsInactive.Include);
            Assert.IsNotNull(talents, "the Talents scene has no TalentController");

            var character = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            character.embers = 99;
            character.unlockedTalentIds.Clear();
            talents.Refresh();
            yield return null;

            var root = Node("Orb0_0");
            Assert.AreEqual(root, EventSystem.current.currentSelectedGameObject,
                "fixture: the tree's entry is path 0's root orb");

            // Selecting the root is what reveals InvestButton and gives the
            // orb a Down link onto it at all (TalentController.
            // RefreshOrbNavigation's own per-selection override).
            yield return PressSubmit();

            var invest = Node("InvestButton");
            Assert.IsNotNull(invest, "fixture: the Talents scene has no InvestButton");
            Assert.IsTrue(invest.activeInHierarchy, "fixture: selecting an authored, reachable root reveals InvestButton");

            yield return Move(0f, -1f);
            Assert.AreEqual(invest, EventSystem.current.currentSelectedGameObject,
                "fixture: Down from the selected root should stand on InvestButton, which is what gets " +
                "remembered under this context's own \"invest\" id");

            // Hidden exactly as its own repaint hides it -- TalentController's
            // PaintDetail calls investButton.SetShown(false), and SetShown is
            // GameObject.SetActive (Show.cs). Reached here as a plain
            // deactivation rather than by driving a state change that happens
            // to cause one, so what the test pins is the DISPATCHER's answer
            // to a vanished focus and not the talent screen's reasons for
            // vanishing it.
            invest.SetActive(false);
            yield return null;

            Assert.AreEqual(root, EventSystem.current.currentSelectedGameObject,
                "a remembered node that has been hidden is not a place focus can sit: the dispatcher " +
                "should fall back to this context's entry (the root orb), never leave the selection " +
                "standing on a control the player cannot see and cannot move off");
        }
    }
}
