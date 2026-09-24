using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 3b, item 1: the character dossier,
    // inside the system menu's Character & Inventory tab. Four groups (the
    // loadout's two files, the ability scores, the pack window and its sort
    // tabs) plus the explicit links between them, and the focus-driven
    // tooltip job 1 built.
    //
    // Driven through the REAL production dispatcher -- a scripted BaseInput
    // via inputOverride, `yield return null`, assert resulting state -- on the
    // REAL Hub scene, the same shape every other file in this family uses.
    //
    // Where a test needs the selection to START somewhere other than the
    // entry it sets it directly and then presses once, so a failure points at
    // the one edge that broke. That is the only reason for it: a second press
    // straight after the first lands fine (the 0.1s uGUI re-press gate is
    // lifted by NavSceneReuse.TakeOverInput; JourneyFixture.Move has the
    // measurement).
    //
    // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene) -- see
    // OpenTheCharacterTab for what is put back between tests.
    public class DossierGamepadNavigationTests
    {
        private string _root;
        private SystemMenuController _menu;
        private CharacterDossierController _dossier;
        private ScriptedBaseInput _input;

        // The two items put in the bag, so cell 0 and cell 1 are both bound
        // and "hovering ANOTHER item while one is selected" has a second item
        // to hover. WHICH cell holds which is BagSort's business, not this
        // file's -- so nothing here predicts the order; the tests assert
        // either "the box names one of these two" or "the box did not
        // change", both of which are order-free.
        private ItemDefinition _one;
        private ItemDefinition _other;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-dossier-nav-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            NavSceneReuse.AfterTest(_input);
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // A reused hub is put back through the dossier's own public setters
        // and buttons: the reward track shut through its close button (shut
        // only by the menu around it, it would stay activeSelf and replay its
        // OnEnable on the next open), the pack, books, skills and attributes
        // panels shut, then the menu closed on its default tab
        // (NavSceneReuse.CloseHubModals). The menu going down also disables
        // every pack cell, whose HoverIndex.OnDisable ends any hover a test
        // left, and pops the dossier's context, which drops its tooltip.
        // Nothing in this file pages the roster, so the character shown is
        // the first squad member either way. NavSceneReuse puts back the
        // input and the focus memory.
        private IEnumerator OpenTheCharacterTab()
        {
            yield return SharedScene.Ensure("Hub");

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the hub has no SystemMenuController");

            _dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_dossier, "the Character pane has no dossier controller");

            var trackClose = Control("TrackCloseButton") as Button;
            if (trackClose != null && trackClose.gameObject.activeInHierarchy) trackClose.onClick.Invoke();
            if (_dossier.IsPackShown) _dossier.ShowPack(false);
            if (_dossier.IsSpellsShown) _dossier.ShowSpells(false);
            if (_dossier.IsSkillsShown) _dossier.ShowSkills(false);
            if (_dossier.IsAttributesShown) _dossier.ShowAttributes(false);
            NavSceneReuse.CloseHubModals();

            _input = NavSceneReuse.TakeOverInput();
            NavSceneReuse.ForgetFocusMemory();
            yield return null;

            _menu.Open();
            _menu.Select(0);
            yield return null;
        }

        // The pack, with exactly two items in it and nothing else -- BagView's
        // sort is not this file's business to predict, and two items make
        // "which cell holds which" a question with one answer per cell.
        private IEnumerator OpenThePack()
        {
            var pair = ContentDatabase.Equippables
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.iconPath))
                .OrderBy(i => i.tier)
                .Take(2)
                .ToList();

            Assert.AreEqual(2, pair.Count, "the catalogue has fewer than two equippable items with art");

            var save = SaveSlotManager.CurrentSave;
            Assert.IsNotNull(save);

            save.stockpiledItems.Clear();
            InventoryOps.Add(save.stockpiledItems, pair[0].id);
            InventoryOps.Add(save.stockpiledItems, pair[1].id);
            SaveSlotManager.SaveCurrent();

            _one = pair[0];
            _other = pair[1];
            Assert.AreNotEqual(_one.displayName, _other.displayName,
                "the two items need different names, or 'the box did not change' proves nothing");

            _dossier.ShowPack(true);
            _dossier.Refresh();
            yield return null;
        }

        private static GameObject Node(string name) =>
            Resources.FindObjectsOfTypeAll<RectTransform>()
                .FirstOrDefault(r => r.name == name && r.gameObject.scene.IsValid())
                ?.gameObject;

        private static Selectable Control(string name) =>
            Resources.FindObjectsOfTypeAll<Selectable>()
                .FirstOrDefault(s => s.name == name && s.gameObject.scene.IsValid());

        private static GameObject Tooltip => Node("DossierTooltip");

        // The same box, found whether or not it is active -- Node is
        // GameObject.Find, which sees only the active hierarchy, so it answers
        // null the moment the menu's panel goes down and takes the box with
        // it. The box's OWN activeSelf flag is the only thing that can tell
        // "this tooltip was closed" from "the panel it lives in went away",
        // and reading it while the menu is shut needs an inactive-inclusive
        // lookup -- the same inactive-inclusive shape Node above cannot
        // give, since GameObject.Find sees only the active hierarchy.
        private static GameObject TooltipEvenWhileHidden =>
            Resources.FindObjectsOfTypeAll<Transform>()
                .FirstOrDefault(t => t.name == "DossierTooltip" && t.gameObject.scene.IsValid())
                ?.gameObject;

        private static TMP_Text TooltipTitle =>
            Resources.FindObjectsOfTypeAll<TMP_Text>()
                .FirstOrDefault(t => t.name == "DossierTooltipTitle" && t.gameObject.scene.IsValid());

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        // One press of one direction, from wherever the selection already is.
        private IEnumerator Press(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;
        }

        private static void Select(string name)
        {
            var control = Control(name);
            Assert.IsNotNull(control, $"the dossier drew no control named {name}");
            EventSystem.current.SetSelectedGameObject(control.gameObject);
        }

        private static void AssertSelected(string name, string because)
        {
            var expected = Control(name);
            Assert.IsNotNull(expected, $"the dossier drew no control named {name}");
            Assert.AreEqual(expected.gameObject, EventSystem.current.currentSelectedGameObject, because);
        }

        // ---- the graph ----------------------------------------------------------

        [UnityTest]
        public IEnumerator DownFromTheCharacterTab_LandsOnTheFirstEquipmentSlot()
        {
            yield return OpenTheCharacterTab();

            // The pane's own declared entry (INavPaneEntry), not the first
            // Selectable in tree order -- which is the roster pager beside the
            // character's name, a paging arrow rather than the subject of the
            // pane.
            yield return Press(0f, -1f);

            AssertSelected("DossierSlotHead",
                "Down off the Character tab should reach the loadout's first slot, the pane's declared entry");
        }

        [UnityTest]
        public IEnumerator DownFromHead_ReachesNecklace()
        {
            yield return OpenTheCharacterTab();
            Select("DossierSlotHead");

            yield return Press(0f, -1f);

            AssertSelected("DossierSlotNecklace",
                "the loadout's left file is a List in drawn order: Head, Necklace, Torso, Gloves, Legs");
        }

        [UnityTest]
        public IEnumerator RightFromHead_ReachesTheFirstWeaponSlot()
        {
            yield return OpenTheCharacterTab();
            Select("DossierSlotHead");

            yield return Press(1f, 0f);

            AssertSelected("DossierSlotWeapon1",
                "the two slots above the right file's first entry clamp onto it rather than dead-ending");
        }

        [UnityTest]
        public IEnumerator RightFromTheTorso_ReachesTheWeaponBesideIt()
        {
            yield return OpenTheCharacterTab();
            Select("DossierSlotTorso");

            yield return Press(1f, 0f);

            AssertSelected("DossierSlotWeapon1",
                "the two files are paired by body row -- Torso is drawn level with Weapon 1");
        }

        // ADAPTED, NOT RELAXED (hardware round 1, item 6). This expected
        // DossierAttrCell0 while the index-pairing rule was in force -- Weapon
        // 1 is the first member of the right file and cell 0 is the first
        // member of the scores' left column, so index pairing put them
        // together. On screen they are 196 units apart vertically and cell 3
        // is 132, so the press was travelling further than it had to in order
        // to reach a control that is not the nearest one. Same claim, the
        // right target.
        [UnityTest]
        public IEnumerator RightFromTheFirstWeaponSlot_ReachesTheNearestAbilityScore()
        {
            yield return OpenTheCharacterTab();
            Select("DossierSlotWeapon1");

            yield return Press(1f, 0f);

            AssertSelected("DossierAttrCell3",
                "the loadout's right file hands off to whichever member of the scores' left column sits " +
                "nearest it, which is the BOTTOM one -- the whole score block is drawn above the right file");
        }

        [UnityTest]
        public IEnumerator RightFromTheFirstAbilityScore_ReachesTheSecond()
        {
            yield return OpenTheCharacterTab();
            Select("DossierAttrCell0");

            yield return Press(1f, 0f);

            AssertSelected("DossierAttrCell1", "the scores are a three-wide Grid; Right steps a column");
        }

        [UnityTest]
        public IEnumerator DownFromTheFirstAbilityScore_ReachesTheOneBelowIt()
        {
            yield return OpenTheCharacterTab();
            Select("DossierAttrCell0");

            yield return Press(0f, -1f);

            AssertSelected("DossierAttrCell3",
                "Down in a three-wide Grid steps a whole row, never diagonally");
        }

        [UnityTest]
        public IEnumerator LeftFromTheFirstAbilityScore_ReachesTheLoadoutAgain()
        {
            yield return OpenTheCharacterTab();
            Select("DossierAttrCell0");

            yield return Press(-1f, 0f);

            AssertSelected("DossierSlotWeapon1",
                "the scores' Grid is declared CLAMPED rather than wrapping its rows, so column 0's Left is " +
                "free to be the link back into the loadout");
        }

        [UnityTest]
        public IEnumerator LeftFromHead_ReachesColumnAsRosterPager()
        {
            yield return OpenTheCharacterTab();
            Select("DossierSlotHead");

            yield return Press(-1f, 0f);

            AssertSelected("DossierNextCharacter",
                "the pager's RIGHT arrow is the control physically nearest the loadout, so it is column A's " +
                "representative in the handoff");
        }

        [UnityTest]
        public IEnumerator DownFromTheRosterPager_ReachesTheFirstNavRow()
        {
            yield return OpenTheCharacterTab();
            Select("DossierNextCharacter");

            yield return Press(0f, -1f);

            AssertSelected("DossierSpellsRow",
                "the pager sits at the top of column A and the nav rows at its foot, with the portrait between");
        }

        // ---- the pack, which covers column A while it is open -------------------

        [UnityTest]
        public IEnumerator DownFromTheFirstSortTab_ReachesTheFirstPackCell()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();
            Select("DossierPackSort0");

            yield return Press(0f, -1f);

            AssertSelected("DossierPackCell0", "the sort tabs sit over the window and pair with its first row");
        }

        [UnityTest]
        public IEnumerator UpFromTheFirstSortTab_ReachesThePacksOwnCloseButton()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();
            Select("DossierPackSort0");

            yield return Press(0f, 1f);

            AssertSelected("DossierPackClose",
                "Close stays in the graph even now that Cancel steps back a level for free -- it is the " +
                "mouse's own way out of the pack, and a Move has to be able to reach it");
        }

        [UnityTest]
        public IEnumerator RightFromTheSecondPackCell_ReachesTheLoadout()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();
            Select("DossierPackCell1");

            yield return Press(1f, 0f);

            AssertSelected("DossierSlotNecklace",
                "the rightmost cell of each pack row hands off to the loadout, with Close standing at the " +
                "top of the column the way the pager's right arrow does when the pack is shut");
        }

        // ---- the spells panel, which also covers column A while it is open ------
        // ---- (AUDIT.md #161) -----------------------------------------------------

        private IEnumerator OpenTheSpells()
        {
            _dossier.ShowSpells(true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Submit_OnSpellsRow_OpensThePanelWithItsEntrySelected()
        {
            yield return OpenTheCharacterTab();

            Select("DossierSpellsRow");
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.IsTrue(_dossier.IsSpellsShown, "fixture: Submit on the row should have opened the panel");
            AssertSelected("DossierSpellSlot0",
                "opening the panel should land on its own entry -- the first spell slot for a character " +
                "whose pool reads books (the shipped roster's first squad member does) -- ShowSpells's own " +
                "explicit reselect on the opening edge, since the dispatcher's own next-frame rule never " +
                "fires here (SpellsRow stays 'declared' to SystemMenuController even hidden behind the panel)");
        }

        [UnityTest]
        public IEnumerator Up_FromTheFirstSlot_ReachesTheSpellsCloseButton()
        {
            yield return OpenTheCharacterTab();
            yield return OpenTheSpells();
            Select("DossierSpellSlot0");

            yield return Press(0f, 1f);

            AssertSelected("DossierSpellsClose",
                "Close stays in the graph for the identical reason the pack's does -- it is the mouse's " +
                "own way out of the books, reachable by a Move as well as by a click");
        }

        [UnityTest]
        public IEnumerator Close_OnTheSpellsPanel_ReturnsSelectionToTheRow()
        {
            yield return OpenTheCharacterTab();
            yield return OpenTheSpells();
            Select("DossierSpellsClose");
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_dossier.IsSpellsShown, "fixture: Submit on Close should have closed the panel");
            AssertSelected("DossierSpellsRow",
                "closing the panel should hand selection straight back to the row that opened it -- " +
                "ShowSpells's own explicit reselect on the closing edge, the same reason the opening edge " +
                "needs one");
        }

        // ADAPTED, NOT RELAXED. This test used to assert that Cancel inside
        // the books panel closed the whole menu, on the reasoning that the
        // pane claimed nothing. The hardware play-test rejected exactly that
        // ("Back (B) should not close a screen but take you back first"), so
        // the claim is inverted and the test keeps asking the same question:
        // what does one Cancel press cost from in here?
        [UnityTest]
        public IEnumerator Cancel_WhileTheSpellsPanelIsOpen_ClosesThePanelAndReturnsToItsRow()
        {
            yield return OpenTheCharacterTab();
            yield return OpenTheSpells();
            Select("DossierSpellSlot0");
            yield return null;

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_dossier.IsSpellsShown, "Cancel should have closed the books panel");
            Assert.IsTrue(_menu.IsOpen, "and must not have taken the menu down with it");
            AssertSelected("DossierSpellsRow", "the press costs exactly one level: back to the row that opened it");
        }

        [UnityTest]
        public IEnumerator Cancel_WhileThePackIsOpen_ClosesThePackAndReturnsToItsRow()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();
            Select("DossierPackCell0");
            yield return null;

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_dossier.IsPackShown, "Cancel should have closed the pack");
            Assert.IsTrue(_menu.IsOpen, "and must not have taken the menu down with it");
            AssertSelected("DossierPackRow", "the press costs exactly one level: back to the row that opened it");
        }

        // THE OWNER'S OWN EXAMPLE, end to end: "reward track to character
        // sheet screen". Two presses, two levels, in that order -- and the
        // second one is what proves the claim is per-level rather than a flat
        // "the dossier never closes the menu".
        [UnityTest]
        public IEnumerator Cancel_FromTheRewardTrack_ReturnsToItsRow_AndOnlyTheNextCancelClosesTheMenu()
        {
            yield return OpenTheCharacterTab();

            Select("DossierTrackRow");
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();

            var panel = Node("RewardTrackPanel");
            Assert.IsNotNull(panel, "fixture: the dossier drew no reward track panel");
            Assert.IsTrue(panel.activeSelf, "fixture: Submit on the track row should have opened the panel");

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(panel.activeSelf, "the first Cancel should close the reward track");
            Assert.IsTrue(_menu.IsOpen, "and leave the character sheet standing behind it");
            AssertSelected("DossierTrackRow", "with the row that opened it selected, ready to be pressed again");

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_menu.IsOpen,
                "only from the pane's base level does Cancel close the menu -- with nothing left to step " +
                "back out of, the dossier declines the press and SystemMenu's own Close runs");
        }

        [UnityTest]
        public IEnumerator TheColumnARowsAreUnreachableWhileTheSpellsPanelIsOpen()
        {
            yield return OpenTheCharacterTab();
            yield return OpenTheSpells();

            Select("DossierSpellsClose");
            yield return null;

            // Up from the panel's own top control should stay inside the
            // panel's graph (there is nowhere above Close) rather than
            // reaching a column-A row that Move should not be able to see --
            // DeclareSpells never declares dossierRows/dossierRoster at all
            // while this state is active, the same way DeclarePack does not.
            yield return Press(0f, 1f);

            AssertSelected("DossierSpellsClose",
                "Close has nothing above it in this state -- a Move that somehow reached a column-A row " +
                "instead would mean the rows were still wired in underneath the panel");
        }

        // ---- the tooltip, focus-driven (job 1) ----------------------------------

        [UnityTest]
        public IEnumerator SelectingAPackCell_ShowsItsPreview()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();

            Select("DossierPackCell0");
            yield return null;

            Assert.IsTrue(Tooltip.activeSelf, "selecting a pack cell should open the comparison box");
            Assert.IsTrue(TooltipTitle.text.Contains(_one.displayName)
                          || TooltipTitle.text.Contains(_other.displayName),
                $"the box should name the item in the cell; it says '{TooltipTitle.text}'");
        }

        [UnityTest]
        public IEnumerator MovingOffAPackCellOntoSomethingWithNoTooltip_HidesIt()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();

            Select("DossierPackCell0");
            yield return null;
            Assert.IsTrue(Tooltip.activeSelf, "precondition: the box is open on the cell");

            // Up out of the window lands on a sort tab, which has no tooltip
            // of its own -- so this is "the selection moved away", not "it
            // moved to another describable thing".
            //
            // Sort tab ONE, not zero, since hardware round 1 item 6 made a
            // cross-group handoff pick the nearest control on the axis the
            // press does not travel along. The cells are 140 units wide and
            // the tabs 63, so tabs 0 and 1 BOTH sit over cell 0; the nearest
            // by centre is tab 1 (28 units) rather than tab 0 (35). Either is
            // "directly above", and this test's own claim -- the box goes when
            // the selection leaves the window -- is indifferent to which.
            yield return Press(0f, 1f);

            AssertSelected("DossierPackSort1", "precondition: Up out of the window reaches the sort tabs");
            Assert.IsFalse(Tooltip.activeSelf,
                "the box belongs to the selected node, so it goes when the selection does");
        }

        [UnityTest]
        public IEnumerator HoveringAnotherItemWhileOneIsSelected_DoesNotSwapThePreview()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();

            Select("DossierPackCell0");
            yield return null;
            string before = TooltipTitle.text;
            Assert.IsNotEmpty(before, "precondition: the selected cell's box names something");

            // Through the component rather than by faking a pointer, the same
            // way DossierPackCaptureTests drives this: the canvas is
            // ScreenSpaceCamera, where a screen coordinate is not simply a
            // rect position, so a scripted mouse here would be testing
            // arithmetic rather than the rule.
            var hover = Control("DossierPackCell1").GetComponent<HoverIndex>();
            Assert.IsNotNull(hover, "DossierPackCell1 carries no HoverIndex");
            hover.OnPointerEnter(null);
            yield return null;

            Assert.IsTrue(Tooltip.activeSelf);
            Assert.AreEqual(before, TooltipTitle.text,
                "focus outranks hover: the hovered cell must not take the box off the selected one " +
                "(the two cells hold differently-named items, so a swap would show here)");
        }

        [UnityTest]
        public IEnumerator APointerLeavingTheSelectedCell_DoesNotHideIt()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();

            var hover = Control("DossierPackCell0").GetComponent<HoverIndex>();
            hover.OnPointerEnter(null);
            Select("DossierPackCell0");
            yield return null;

            hover.OnPointerExit(null);
            yield return null;

            Assert.IsTrue(Tooltip.activeSelf,
                "the mouse leaving a cell the stick is standing on must not close the box");
        }

        [UnityTest]
        public IEnumerator AContextPushedOverTheMenu_ForceClosesTheTooltip()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();

            Select("DossierPackCell0");
            yield return null;
            Assert.IsTrue(Tooltip.activeSelf, "precondition: the box is open");

            // PUSHED DIRECTLY, and that is honest rather than convenient: no
            // shipped screen pushes a context over the system menu today
            // (AUDIT.md #158's three unwired modals cover the HUB, not this
            // menu), so there is no production path to drive. The rule is
            // still the plan's -- section 7 -- and this is the only way to
            // prove it fires without waiting for the first modal that needs
            // it to also be the thing that discovers it does not.
            var modal = new NavContext(entry: null, selectables: null, cancel: null);
            NavigationInputModule.Contexts.Push(modal);
            yield return null;

            Assert.IsFalse(Tooltip.activeSelf,
                "a context going up over the screen force-closes its tooltip -- hooked off the stack's own " +
                "Changed event, never polled");

            NavigationInputModule.Contexts.Remove(modal);
        }

        // RENAMED AND SPLIT IN TWO when focus memory landed (AUDIT.md #163),
        // rather than left red or quietly relaxed. What this used to assert
        // was that the box is still down AFTER the menu is reopened, on the
        // stated grounds that "left set, the box would simply reappear with
        // the menu, describing a selection from before it closed". That
        // reasoning was sound while a reopened menu could only ever land on
        // its entry; it is not any more. The dispatcher now restores this
        // screen's remembered focus on reopen, so the cell IS selected again,
        // and a box describing the selected cell is the tooltip doing exactly
        // what section 7 says it does -- following focus -- not a stale flag
        // surviving a close.
        //
        // So both halves are pinned instead of the one the old sequence could
        // see: the force-close itself (the box's own flag, read while the
        // menu is shut, which is the claim that was always the point), and
        // the reopen, whose box is now required to come back WITH the focus
        // it describes. The second half would have hidden a real regression
        // if it had simply been deleted -- a box coming back with no
        // selection behind it is still wrong, and asserting the selection
        // alongside it is what tells the two apart.
        [UnityTest]
        public IEnumerator ClosingTheMenu_DropsTheTooltip_AndReopeningBringsItBackWithTheFocusItDescribes()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();

            Select("DossierPackCell0");
            yield return null;
            Assert.IsTrue(Tooltip.activeSelf, "precondition: the box is open");

            _menu.Close();
            yield return null;

            // activeSelf, NOT activeInHierarchy: hiding the menu's panel
            // makes the box inactive in the hierarchy whatever else happens,
            // so only its own flag can tell "the tooltip was closed" from
            // "the panel it lives in went away and took it along".
            var box = TooltipEvenWhileHidden;
            Assert.IsNotNull(box, "fixture: the dossier drew no tooltip box at all");
            Assert.IsFalse(box.activeSelf,
                "the screen's own context coming off the stack hides its tooltip -- its own flag, not " +
                "merely its parent's");

            _menu.Open();
            _menu.Select(0);
            yield return null;

            AssertSelected("DossierPackCell0",
                "reopening the menu should restore this screen's remembered focus (plan section 4's push " +
                "rule), which is what makes the box below correct rather than stale");
            Assert.IsTrue(box.activeSelf,
                "the box follows focus, so focus landing back on the cell brings its tooltip back with it");
        }

        // ---- where the stick is standing ----------------------------------------
        //
        // ADAPTED, NOT RELAXED (hardware round 1's visual pass). These two
        // were SelectingAPackCell_ShowsItsSelectedHalo and
        // MovingOffAPackCell_HidesItsSelectedHalo, asserting
        // DossierPackHalo0's activeSelf -- AUDIT.md #160's answer to "a
        // NoChrome cell answers gamepad focus with nothing at all". The owner
        // played this screen on a pad and the answer did not work: "a player
        // can't see where they're going in the character sheets screen: no
        // obvious selectors". The halos are gone; the claim is unchanged and
        // now asks it of Core/FocusMarker.cs.

        [UnityTest]
        public IEnumerator SelectingAPackCell_PutsTheFocusMarkerOnIt()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();

            Select("DossierPackCell0");
            yield return null;

            Assert.IsTrue(NavigationInputModule.Marker.IsShown, "the marker is not drawn at all");
            Assert.AreSame(Node("DossierPackCell0").transform, NavigationInputModule.Marker.Target,
                "the marker is not on the selected cell");
        }

        [UnityTest]
        public IEnumerator MovingOffAPackCell_TakesTheFocusMarkerWithIt()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();

            Select("DossierPackCell0");
            yield return null;
            Assert.AreSame(Node("DossierPackCell0").transform, NavigationInputModule.Marker.Target,
                "precondition: the marker is on the cell");

            // Up out of the window lands on a sort tab (see
            // MovingOffAPackCellOntoSomethingWithNoTooltip_HidesIt above).
            yield return Press(0f, 1f);

            Assert.AreNotSame(Node("DossierPackCell0").transform, NavigationInputModule.Marker.Target,
                "the marker belongs to the selection, so it goes when the selection moves away");
            Assert.AreSame(EventSystem.current.currentSelectedGameObject.transform,
                NavigationInputModule.Marker.Target,
                "and it lands on whatever the Move actually selected");
        }

        // ADAPTED for the same reason the pair above it were: these two asked
        // DossierSlotHaloHead and DossierAttrHalo0 for their activeSelf, and
        // AUDIT.md #160's three per-group halos are gone. All three groups on
        // this screen now answer focus the same way as each other and as
        // every other screen.

        [UnityTest]
        public IEnumerator SelectingAnEquipmentSlot_PutsTheFocusMarkerOnIt()
        {
            yield return OpenTheCharacterTab();
            Select("DossierSlotHead");
            yield return null;

            Assert.AreSame(Node("DossierSlotHead").transform, NavigationInputModule.Marker.Target,
                "the loadout's slots are the same NoChrome shape as the pack cells and get the same marker");
        }

        [UnityTest]
        public IEnumerator SelectingAnAbilityScoreCell_PutsTheFocusMarkerOnIt()
        {
            yield return OpenTheCharacterTab();
            Select("DossierAttrCell0");
            yield return null;

            Assert.AreSame(Node("DossierAttrCell0").transform, NavigationInputModule.Marker.Target,
                "the ability-score cells are the third group, answered by the same one marker");
        }

        // ---- the ring, pinned literally (hardware round 1, item 6) ---------------
        //
        // "A player can't see where they're going in the character sheets
        // screen: no obvious selectors and no intuitive navigating, apart from
        // the equipped gear selector." The selectors are the marker above.
        // This is the navigation half, and the defect behind it was
        // CharacterDossierController.PairAcross pairing two groups by LIST
        // INDEX -- a rule that is only correct when the two lists are already
        // aligned across the axis being crossed, which on this screen none of
        // them are. That method's own comment carries the measured
        // before-and-after.
        //
        // Every expectation below is a literal node name, checked against the
        // solved layout rather than derived from the arithmetic the production
        // code uses -- a test that recomputed the pairing would pass whatever
        // the pairing did (CLAUDE.md gotcha 5). The heights they encode, in
        // canvas units off the dossier's own centre:
        //
        //     column A            the loadout          the scores
        //     pager     y   49    Head       y  159    cells 0/1/2  y 182
        //     Spells    y  -87    Necklace   y   54    cells 3/4/5  y 118
        //     Track     y -129    Torso      y  -14
        //     Skills    y -171    Gloves     y  -85
        //     Pack      y -213    Legs       y -156
        //
        // Skills is new to this table, not new to the screen -- the row was
        // always drawn at -171 (DossierLayout.SkillsRowCentreY, one
        // NavRowHeight above Pack), but it carried no onClick, so
        // DeclareColumnARows left it out of the spine and no pairing could
        // reach it. It is in the spine now, which moves exactly one
        // expectation below; the geometry did not move at all.

        // One press and one rest frame, no wall-clock settle (the rest frame
        // re-arms the dispatcher's edge; JourneyFixture.Move has why nothing
        // else is needed). Selection is placed first so each edge is asserted
        // from a known start rather than walked to, which keeps a failure
        // pointing at the edge that broke.
        private IEnumerator Step(string from, float horizontal, float vertical)
        {
            Select(from);
            yield return null;
            yield return Press(horizontal, vertical);
            yield return DriveFrame();
        }

        [UnityTest]
        public IEnumerator TheLoadoutsLeftFileHandsOffToWhateverInColumnAIsLevelWithIt()
        {
            yield return OpenTheCharacterTab();

            yield return Step("DossierSlotHead", -1f, 0f);
            AssertSelected("DossierNextCharacter", "Head (159) is level with nothing in column A but the pager (49)");

            yield return Step("DossierSlotNecklace", -1f, 0f);
            AssertSelected("DossierNextCharacter", "Necklace (54) sits 5 units off the pager");

            yield return Step("DossierSlotTorso", -1f, 0f);
            AssertSelected("DossierNextCharacter",
                "Torso (-14) is 63 from the pager and 73 from the Spells row -- the pager, narrowly");

            yield return Step("DossierSlotGloves", -1f, 0f);
            AssertSelected("DossierSpellsRow",
                "Gloves (-85) sits 2 units off the Spells row, which is what index pairing sent it 170 past");

            yield return Step("DossierSlotLegs", -1f, 0f);
            AssertSelected("DossierSkillsRow",
                "Legs (-156) is nearest the Skills row (-171), 15 off it against 27 to the reward-track row " +
                "(-129) -- the row Skills joining the spine takes over from Track");
        }

        [UnityTest]
        public IEnumerator ColumnAHandsBackToTheLoadoutAtItsOwnHeight()
        {
            yield return OpenTheCharacterTab();

            yield return Step("DossierNextCharacter", 1f, 0f);
            AssertSelected("DossierSlotNecklace", "the pager (49) is level with Necklace (54), not with Head");

            yield return Step("DossierSpellsRow", 1f, 0f);
            AssertSelected("DossierSlotGloves", "the Spells row (-87) is level with Gloves (-85)");

            yield return Step("DossierTrackRow", 1f, 0f);
            AssertSelected("DossierSlotLegs", "the reward-track row (-129) is nearest Legs (-156)");

            yield return Step("DossierSkillsRow", 1f, 0f);
            AssertSelected("DossierSlotLegs", "and the Skills row (-171) is nearer Legs still");

            yield return Step("DossierPackRow", 1f, 0f);
            AssertSelected("DossierSlotLegs",
                "the Pack row (-213) is below the whole left file, so it clamps onto its lowest slot -- three " +
                "rows reaching the same slot is the honest answer when one column is longer than the other");
        }

        [UnityTest]
        public IEnumerator TheColumnARowsAreAListInTheOrderTheyAreDrawn()
        {
            yield return OpenTheCharacterTab();

            yield return Step("DossierNextCharacter", 0f, -1f);
            AssertSelected("DossierSpellsRow", "Down off the pager enters the nav rows at the top one");

            yield return Step("DossierSpellsRow", 0f, -1f);
            AssertSelected("DossierTrackRow", "and the rows step in drawn order");

            // ADAPTED, NOT RELAXED (owner bug report, 2026-09-19: "Skills in
            // the char menu, when you click on it, nothing happens"). This
            // used to assert Down from Track reached Pack directly, on the
            // grounds that Skills carried no onClick and so was left out of
            // the List entirely. It opens a real pane now (see
            // DeclareColumnARows' own updated header) and is wired into the
            // same List between Track and Pack, exactly as drawn.
            yield return Step("DossierTrackRow", 0f, -1f);
            AssertSelected("DossierSkillsRow",
                "the Skills row now opens a real pane, so it takes its place in the column's own List, " +
                "between Track and Pack as drawn");

            yield return Step("DossierSkillsRow", 0f, -1f);
            AssertSelected("DossierPackRow", "and the rows continue stepping in drawn order");

            yield return Step("DossierPackRow", 0f, -1f);
            AssertSelected("DossierPackRow", "the foot of the column clamps rather than wrapping to its head");
        }

        [UnityTest]
        public IEnumerator TheLoadoutIsWalkedInBodyOrder_DownTheLeftFileAndAcrossByRow()
        {
            yield return OpenTheCharacterTab();

            yield return Step("DossierSlotHead", 0f, -1f);
            AssertSelected("DossierSlotNecklace", "the left file steps down the BODY, not down a declaration list");

            yield return Step("DossierSlotTorso", 1f, 0f);
            AssertSelected("DossierSlotWeapon1", "Torso and Weapon 1 are drawn on the same body row");

            yield return Step("DossierSlotGloves", 1f, 0f);
            AssertSelected("DossierSlotWeapon2", "Gloves and Weapon 2 likewise");

            yield return Step("DossierSlotLegs", 1f, 0f);
            AssertSelected("DossierSlotShoes", "and Legs with Shoes at the foot of both files");

            yield return Step("DossierSlotHead", 1f, 0f);
            AssertSelected("DossierSlotWeapon1",
                "Head has no opposite number and clamps onto the topmost slot that does");
        }

        [UnityTest]
        public IEnumerator TheRightFileHandsOffToTheNearestScoreCell_WhichIsAlwaysTheBottomRow()
        {
            yield return OpenTheCharacterTab();

            yield return Step("DossierSlotWeapon1", 1f, 0f);
            AssertSelected("DossierAttrCell3", "the score block is drawn entirely ABOVE the right file");

            yield return Step("DossierSlotShoes", 1f, 0f);
            AssertSelected("DossierAttrCell3", "so every member of that file reaches its lowest-left cell");

            yield return Step("DossierAttrCell3", -1f, 0f);
            AssertSelected("DossierSlotWeapon1", "and the nearest slot coming back is the file's topmost");

            yield return Step("DossierAttrCell0", -1f, 0f);
            AssertSelected("DossierSlotWeapon1", "the top-left cell reaches the same slot -- it is still the nearest");
        }

        [UnityTest]
        public IEnumerator ThePackWindowIsWalkedInReadingOrderAndHandsOffAtItsOwnHeight()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();

            yield return Step("DossierPackCell0", 1f, 0f);
            AssertSelected("DossierPackCell1", "a two-wide Grid steps across its own row first");

            yield return Step("DossierPackClose", 1f, 0f);
            AssertSelected("DossierSlotHead",
                "Close stands at the head of the covered column and is level with the loadout's first slot");

            yield return Step("DossierPackCell1", 1f, 0f);
            AssertSelected("DossierSlotNecklace", "and the window's right-hand column hands off at its own height");
        }

        [UnityTest]
        public IEnumerator TheBooksPanelIsOneColumnAndHandsOffAtItsOwnHeight()
        {
            yield return OpenTheCharacterTab();
            yield return OpenTheSpells();

            yield return Step("DossierSpellsClose", 0f, -1f);
            AssertSelected("DossierSpellSlot0", "Close sits above the slots band, as drawn");

            yield return Step("DossierSpellSlot0", 0f, -1f);
            AssertSelected("DossierSpellSlot1", "and the slots are a single vertical stack, not a grid");

            yield return Step("DossierSpellSlot2", 1f, 0f);
            AssertSelected("DossierSlotTorso", "the lowest slot (11) is level with Torso (-14)");

            yield return Step("DossierSlotNecklace", -1f, 0f);
            AssertSelected("DossierSpellSlot1", "and Necklace (54) comes back to the slot level with it (59)");
        }

        [UnityTest]
        public IEnumerator Cancel_FromInsideTheDossier_ClosesTheMenu()
        {
            yield return OpenTheCharacterTab();
            Select("DossierSlotHead");
            yield return null;

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_menu.IsOpen,
                "the dossier claims Cancel one LEVEL at a time, not one PANE at a time -- with no panel " +
                "open there is nothing to step back out of, so the press falls through to SystemMenu's " +
                "own Close exactly as it always did");
        }
    }
}
