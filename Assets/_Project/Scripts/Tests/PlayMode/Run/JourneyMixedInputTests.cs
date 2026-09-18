using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Party;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 4: the mixed-input pass
    // -- section 3's own ordinary-context reselection rule and section 6's
    // eligibility/lifecycle invariants, each proved on a real screen with
    // BOTH input devices touched in the same test (a pad press, then a mouse
    // click, or the reverse), because that is the ordinary state of a player
    // who has a mouse on the desk and a pad in their hands. One class, one
    // rule per test, each asserting literal state -- no rule shares another
    // rule's setup, since each names a different screen.
    public class JourneyMixedInputTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-mixed-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
        }

        [TearDown]
        public void Restore()
        {
            SaveData.TestSquadOfThreeEnabled = null;
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- (a) ordinary context, background click, then Move ------------------
        //
        // docs/GAMEPAD_NAVIGATION_PLAN.md section 3's own ordinary-context
        // rule: "if selection is null... reselect its remembered node" runs
        // EVERY call, in the SAME Process() a background click nulled it --
        // never a first-Move-restores special case (section 4's own "nothing
        // ever crosses a frame boundary null").
        //
        // A REAL BUG THIS TEST FOUND AND NavigationInputModule.cs NOW FIXES:
        // "remembered" here is NOT NavContext.RememberedId -- grepped, that
        // field is set by NavContext.Remember, and nothing in this project
        // ever calls it (DebugMenuGamepadNavigationTests' own pinned claim,
        // "HubController never calls NavContext.Remember, so there is no
        // per-node memory to restore, only Entry", is about the CROSS-
        // CONTEXT case -- a Cancel that POPS a modal, which correctly falls
        // back to Entry since the popped context's own selection was never
        // a member of the context now on top). This test is the SAME-
        // CONTEXT case instead: a click that deselects without replacing,
        // while the screen underneath never changed. Before this pass, that
        // fell back to Entry too (ResolveSelection's only other answer),
        // which made the stepper-button contract in section 7 ("does
        // nothing to the row's own remembered focus") false the moment a
        // real click -- not a direct method call -- exercised it (see rule
        // (e) below). NavigationInputModule.Process now captures selection
        // BEFORE base.Process() runs and ReselectIfOutsideDeclaredSet
        // prefers THAT over Entry whenever the same top context still
        // declares it -- which is exactly and only this same-context case.
        // The Hub's own ring is the fixture: gate -> Up ->
        // PrincipalityBuilding (the left arm above it) -> Left ->
        // TalentsBuilding (the ring's next member,
        // HubGamepadNavigationTests' own pinned claim). It was the 2x2 Grid
        // until the hardware play-test rejected that shape.
        [UnityTest]
        public IEnumerator BackgroundClick_RestoresTheRememberedNodeSameFrame_NextMoveAdvancesToTheLiteralNeighbour()
        {
            SaveSlotManager.EnterSlot(0);

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            yield return MoveUp(); // gate -> PrincipalityBuilding, the left arm above it
            AssertSelectedName("PrincipalityBuilding", "fixture: Up from the gate should reach PrincipalityBuilding");

            yield return ClickBackground();

            AssertSelectedName("PrincipalityBuilding",
                "a background click nulls selection via PointerInputModule's own DeselectIfSelectionChanged, but " +
                "the dispatcher must restore what was selected a moment ago in the SAME Process() call -- nothing " +
                "should have crossed a frame boundary null, and it must not have fallen back to Entry either");

            yield return MoveLeft(); // the ring's own next member, not a stale selection from before the click

            AssertSelectedName("TalentsBuilding",
                "the next Move should advance to the literal ring neighbour of the RESTORED selection -- if the " +
                "restore above had left a stale reference behind, this Move would prove it by landing somewhere else");
        }

        // ---- (b) modal top, blocked click, then Submit ---------------------------
        //
        // The journey-level twin of NavigationDispatcherTests' own
        // MouseClickBehindModal_DoesNotReachTheScreen_SubmitResolvesAgainstThe
        // Modal, proved here against a REAL Fight scene and a REAL
        // SystemMenu modal rather than a synthetic fixture button.
        // ExitsController.ArmedIndex is the literal "fired exactly once"
        // signal: zero clicks/Submits leaves it at -1, and Fire() (a SECOND
        // press) would have already left the scene entirely -- so ==0 after
        // ONE Submit is proof of exactly one call, not zero and not two.
        //
        // DEPENDS ON RULE (a)'S OWN FIX: the blocked click's currentOverGo
        // is the modal's own dimmer, which has no ISelectHandler ancestor,
        // so DeselectIfSelectionChanged nulls ExitTitle exactly the way a
        // background click nulls a Hub building -- without the same fix,
        // the fallback would have landed on the tab strip's own Entry
        // instead, and the Submit below would have re-selected a tab
        // rather than arming Title.
        [UnityTest]
        public IEnumerator ClickThroughAModal_FiresNothing_ThenSubmitFiresTheModalsSelectedControlOnce()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(1).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");
            Assert.IsNotEmpty(enemyIds, "no enemies in content");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));
            built.Session.Begin();
            fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            var session = built.Session;
            yield return null;

            for (int i = 0; i < 120 && !session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(session.IsPlayerTurn, "never reached a player turn");

            TakeOverInput();

            // A fresh scene's own layout can still be mid-settle the frame it

            // activates -- this suite found that gap under the full parallel

            // gate (never under a single-class or single-area slice), so every

            // mouse click aimed at a screen coordinate waits real time here

            // first, not just the two engine frames TakeOverInput's own callers

            // already pay.

            yield return new WaitForSecondsRealtime(0.5f);
            yield return null;

            int verbBefore = fight.FocusedVerbForTest;
            int enemyHpBefore = session.Encounter.Enemies[0].CurrentHealth;

            yield return PressCancel(); // opens the system menu over Fight (no click equivalent, section 3's own transition case)

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the Fight scene carries no SystemMenuController");
            Assert.IsTrue(menu.IsOpen, "Cancel should have opened the system menu");

            yield return PressTabPrev(); // the pad's own shortcut -- wraps to MainMenu, landing on ExitTitle (its own entry)
            AssertSelectedName("ExitTitle", "the shoulder shortcut should land on the MainMenu pane's own entry");

            var exits = Object.FindAnyObjectByType<ExitsController>(FindObjectsInactive.Include);
            Assert.IsNotNull(exits, "the MainMenu pane has no ExitsController");
            Assert.AreEqual(-1, exits.ArmedIndex, "fixture: nothing should be armed yet");

            // THE MOUSE, at Fight's own Verb0 -- still there underneath, and
            // the modal's dimmer should intercept the raycast before it does.
            yield return Click(Node("Verb0"));

            Assert.AreEqual(verbBefore, fight.FocusedVerbForTest,
                "a click through the modal reached Fight's own verb plate -- the dimmer should have blocked it");
            Assert.AreEqual(enemyHpBefore, session.Encounter.Enemies[0].CurrentHealth,
                "the blocked click must not have fired an attack");
            Assert.AreEqual(-1, exits.ArmedIndex, "the blocked click must not have touched the modal's own state either");
            Assert.IsTrue(menu.IsOpen, "the modal should not have closed on a stray click");

            // THE PAD, immediately after -- resolves against the MODAL's own
            // selected control (ExitTitle), never against Fight underneath.
            yield return PressSubmit();

            Assert.AreEqual(0, exits.ArmedIndex,
                "Submit should have fired the modal's own selected control (Press(0), arming Title) exactly " +
                "once -- zero means it never reached the modal, and a second fire would already have left this scene");
            Assert.IsTrue(menu.IsOpen, "one Submit should only ARM leaving, not fire it -- the menu is still open");
            Assert.AreEqual("Fight", SceneManager.GetActiveScene().name, "one Submit should not have navigated away yet");
        }

        // ---- (c) focus over pointer on the Dossier's pack --------------------
        //
        // docs/GAMEPAD_NAVIGATION_PLAN.md section 7's own Tooltips contract:
        // "Pointer enters another node while a selected node's tooltip is up
        // -> nothing: focus wins." Selection is set directly the same way
        // DossierTooltipCaptureTests' own capture pass already does ("the
        // selection, not a pointer: this is the path with no cursor to
        // anchor to") -- a pad having reached this cell is
        // DossierGamepadNavigationTests' own proven claim, not this test's;
        // what is new here is a REAL mouse hover, through the module's own
        // raycast, arriving on top of that selection.
        [UnityTest]
        public IEnumerator SelectedPackItem_KeepsItsTooltip_WhileTheMouseHoversAnotherItem_AndOnceItLeaves()
        {
            SaveSlotManager.EnterSlot(0);

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");
            menu.Open();
            menu.Select(SystemMenuTab.CharacterInventory);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the Character pane has no dossier controller");

            // Two items, so cells 0 and 1 both hold something -- the same
            // shape DossierGamepadNavigationTests' own OpenThePack uses.
            var pair = ContentDatabase.Equippables
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.iconPath))
                .OrderBy(i => i.tier)
                .Take(2)
                .ToList();
            Assert.AreEqual(2, pair.Count, "the catalogue has fewer than two equippable items with art");

            var save = SaveSlotManager.CurrentSave;
            save.stockpiledItems.Clear();
            foreach (var item in pair) InventoryOps.Add(save.stockpiledItems, item.id);
            SaveSlotManager.SaveCurrent();

            dossier.ShowPack(true);
            dossier.Refresh();
            yield return null;

            var cell0 = Node("DossierPackCell0");
            var cell1 = Node("DossierPackCell1");
            Assert.IsNotNull(cell0, "the pack drew no DossierPackCell0");
            Assert.IsNotNull(cell1, "the pack drew no DossierPackCell1");

            // THE PAD'S OWN HALF: select cell0 (the entry a Move walk already
            // reaches, DossierGamepadNavigationTests' own claim).
            EventSystem.current.SetSelectedGameObject(cell0);
            yield return new WaitForSecondsRealtime(0.6f); // real seconds -- the tooltip settles on unscaled time

            var tooltip = Node("DossierTooltip");
            Assert.IsNotNull(tooltip, "the dossier drew no tooltip");
            Assert.IsTrue(tooltip.activeSelf, "selecting a pack cell should have opened the tooltip");

            string titleForSelected = Node("DossierTooltipTitle")?.GetComponent<TMPro.TMP_Text>()?.text;
            string bodyForSelected = Node("DossierTooltipBody")?.GetComponent<TMPro.TMP_Text>()?.text;
            Assert.IsNotEmpty(titleForSelected, "the tooltip should describe the selected item");

            // THE MOUSE'S OWN HALF: hover a DIFFERENT cell -- focus must win,
            // so the box stays exactly what it was.
            yield return MoveMouseTo(cell1);

            Assert.AreEqual(titleForSelected, Node("DossierTooltipTitle")?.GetComponent<TMPro.TMP_Text>()?.text,
                "the pointer entering another node while a selection is live must change nothing -- the " +
                "selected node's own tooltip stays up");
            Assert.AreEqual(bodyForSelected, Node("DossierTooltipBody")?.GetComponent<TMPro.TMP_Text>()?.text);

            // The mouse leaves entirely -- still nothing changes, since the
            // selection was never the pointer's to give up.
            yield return MoveMouseToWorldPoint(new Vector3(5f, 5f, 0f));

            Assert.AreEqual(titleForSelected, Node("DossierTooltipTitle")?.GetComponent<TMPro.TMP_Text>()?.text,
                "moving the mouse off the pack entirely must still describe the SELECTED item -- there is no " +
                "pointer claim left to fall back to, and none should be needed");
            Assert.AreEqual(bodyForSelected, Node("DossierTooltipBody")?.GetComponent<TMPro.TMP_Text>()?.text);
        }

        // ---- (d) pad pickup, then a mouse drag begins on a seat -------------------
        //
        // PartyController.BeginSeatDrag's own shape: "Cancel() first means
        // the model holds no stale selection to leak into a refused drag."
        // A pad Submit picks a seat up (PartyController.ClickSeat through
        // the real dispatcher, the same as any other Submit this suite
        // presses); a mouse THEN begins a real drag on a different seat --
        // PartyDragSource is an ordinary IBeginDragHandler, so a scripted
        // press-move-while-held crosses EventSystem's own drag threshold the
        // same way a real mouse would, with no direct method call standing
        // in for it.
        [UnityTest]
        public IEnumerator PadPickup_ThenAMouseDragBeginsOnASeat_TheCarryIsCancelled_SelectionIsSane()
        {
            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "owl", "sheep", "bear" };

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");
            menu.Open();
            menu.Select(SystemMenuTab.Party);
            yield return null;

            var party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            Assert.IsNotNull(party, "the Party pane has no PartyController");

            var seat0 = Node("PartySeat0Button");
            var seat1 = Node("PartySeat1Button");
            Assert.IsNotNull(seat0, "the Party pane drew no PartySeat0Button");
            Assert.IsNotNull(seat1, "the Party pane drew no PartySeat1Button");

            // THE PAD: Submit on seat 0 picks it up.
            EventSystem.current.SetSelectedGameObject(seat0);
            yield return PressSubmit();

            Assert.IsNotNull(party.Formation.SelectedId, "fixture: Submit on seat 0 should have picked it up");
            Assert.IsTrue(party.Formation.SelectedFrom.IsSeat && party.Formation.SelectedFrom.SeatIndex == 0,
                "fixture: the carry should be FROM seat 0");

            // THE MOUSE: press on seat 1, then move far enough while held to
            // cross EventSystem.current.pixelDragThreshold and fire OnBeginDrag.
            yield return MoveMouseTo(seat1);
            Input.MouseButton0Down = true;
            Input.MouseButton0Held = true;
            yield return DriveFrame();
            Input.MousePosition += new Vector2(80f, 0f);
            yield return DriveFrame();

            Assert.IsTrue(party.Formation.SelectedFrom.IsSeat && party.Formation.SelectedFrom.SeatIndex == 1,
                "starting a mouse drag on seat 1 should have cancelled seat 0's carry (BeginSeatDrag's own " +
                "Cancel() call) and begun a new one FROM seat 1, not left the old carry standing");

            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.IsNotNull(selected, "selection should not be left null by the drag's own begin step");
            Assert.AreEqual(seat1, selected,
                "selection should be sane -- seat 1's own button, the seat the new carry actually started from, " +
                "not a stale reference to seat 0");

            // Clean release so nothing is left mid-drag for the next test.
            Input.MouseButton0Up = true;
            Input.MouseButton0Held = false;
            yield return DriveFrame();
        }

        // ---- (e) pad-selected stepper row, mouse-clicked stepper button --------
        //
        // THE REAL FINDING BEHIND THIS TEST: OptionsController.cs's own
        // comment on stepPrev/stepNext claims a click "does nothing to the
        // row's own remembered focus, since OnPointerDown never calls
        // SetSelectedGameObject for a None-mode Selectable" -- true as far
        // as it goes, but incomplete. Navigation.Mode.None only stops the
        // CLICKED button from being reselected; PointerInputModule's own
        // DeselectIfSelectionChanged still nulls whatever WAS selected
        // (the row) the instant the click's target differs from it, mode
        // or no mode. Before this pass's fix to NavigationInputModule.cs
        // (rule (a) above), that null fell back to the context's Entry --
        // a real, previously-unproven regression this mouse-driven test is
        // the first to exercise (every earlier stepper test drives the
        // adjustment through the pad's own Right press on the row, never a
        // raycasted click on the button beside it).
        [UnityTest]
        public IEnumerator PadSelectsAStepperRow_MouseClicksItsButton_ValueSteps_FocusUnchanged_ThenPadAdjustsTheSameRow()
        {
            SaveSlotManager.EnterSlot(0);

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");

            yield return PressCancel(); // no click equivalent, the mouse's own ESC key
            yield return Click(Node("SystemTabOptions"));

            var resolutionRow = Node("OptionsRowresolution");
            var resolutionNext = Node("OptionsRowresolutionNext");
            Assert.IsNotNull(resolutionRow, "the options pane has no resolution row");
            Assert.IsNotNull(resolutionNext, "the options pane has no resolution stepper Next button");

            GameSettings.SetResolutionIndex(0);

            // THE PAD: Down until the row itself is selected.
            int guard = 0;
            while (EventSystem.current.currentSelectedGameObject != resolutionRow && guard < 10)
            {
                yield return MoveDown();
                guard++;
            }
            Assert.AreEqual(resolutionRow, EventSystem.current.currentSelectedGameObject,
                "Down presses never reached the resolution row within the guard");

            // THE MOUSE: click the stepper's own Next button.
            yield return Click(resolutionNext);

            Assert.AreEqual(1, GameSettings.ResolutionIndex, "clicking Next should have stepped the value by exactly one");
            Assert.AreEqual(resolutionRow, EventSystem.current.currentSelectedGameObject,
                "OptionsController.SetNoNavigation on the stepper buttons means clicking one must NOT move the " +
                "row's own remembered focus -- selection should still be the row itself");

            // THE PAD AGAIN: Right on the still-selected row adjusts the SAME key.
            yield return MoveRight();

            Assert.AreEqual(2, GameSettings.ResolutionIndex,
                "a pad Right press on the still-selected row should adjust the same resolution stepper, " +
                "continuing from where the mouse click left it");
        }

        // ---- (f) no tab-strip context on top -- a shoulder frame is a no-op ------
        [UnityTest]
        public IEnumerator NoTabStripContextOnTop_AShoulderFrameDoesNothing()
        {
            SaveSlotManager.EnterSlot(0);

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            AssertSelectedName("StartRunGate", "fixture: the hub's own declared entry, with nothing else open");
            int stackCountBefore = NavigationInputModule.Contexts.Count;

            yield return PressTabNext(); // Hub's own NavContext declares no tabStrip -- NavContext.RaiseTabStep no-ops

            AssertSelectedName("StartRunGate", "a shoulder press over a context with no tab strip must select nothing");
            Assert.AreEqual(stackCountBefore, NavigationInputModule.Contexts.Count,
                "a shoulder press over a context with no tab strip must not push or pop anything either");
        }

        // ---- (g) Fight top, a mouse click on a verb, then one stick frame --------
        [UnityTest]
        public IEnumerator FightTop_MouseClicksAVerb_SelectionIsNullNextFrame_ThenOneStickFrameMovesFocusOnce()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(1).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");
            Assert.IsNotEmpty(enemyIds, "no enemies in content");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));
            built.Session.Begin();
            fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            var session = built.Session;
            yield return null;

            for (int i = 0; i < 120 && !session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(session.IsPlayerTurn, "never reached a player turn");

            TakeOverInput();

            // A fresh scene's own layout can still be mid-settle the frame it

            // activates -- this suite found that gap under the full parallel

            // gate (never under a single-class or single-area slice), so every

            // mouse click aimed at a screen coordinate waits real time here

            // first, not just the two engine frames TakeOverInput's own callers

            // already pay.

            yield return new WaitForSecondsRealtime(0.5f);
            yield return null;

            Assert.AreEqual(0, fight.FocusedVerbForTest, "fixture: starts on ATTACK, index 0");

            yield return Click(Node("Verb0")); // the mouse's own click fires OnVerbPressed(0) -> OpenAttack -> targeting

            Assert.AreEqual(-1, fight.HoveredEnemyIndexForTest,
                "the click should have opened targeting fresh -- nothing hovered yet");
            Assert.IsNull(EventSystem.current.currentSelectedGameObject,
                "Fight nulls selection every frame it is top -- true the very next frame after a mouse click too");

            yield return MoveDown(); // ONE stick frame

            Assert.AreEqual(0, fight.HoveredEnemyIndexForTest,
                "one stick Move should have hovered the first living enemy, exactly once -- not stayed at -1 " +
                "and not skipped past it");
        }
    }
}
