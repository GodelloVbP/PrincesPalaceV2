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
    // ONE MOVE PRESS PER TEST, each from a freshly loaded scene.
    // SystemMenuGamepadNavigationTests' own header has the full reasoning:
    // StandaloneInputModule's move debounce is a real-time timestamp on the
    // module instance (plan section 2 flags it as outside the seam a scripted
    // BaseInput can control), so a second press moments later by test-clock
    // time is silently swallowed as a repeat. Where a test needs the selection
    // to START somewhere other than the entry it sets it directly and then
    // presses once, exactly as the Options row tests do.
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
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private IEnumerator OpenTheCharacterTab()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            // Start() runs one frame after activation.
            yield return null;
            yield return null;

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the hub scene's EventSystem is not running NavigationInputModule");

            // Set BEFORE letting any frame run: a previously-loaded scene's
            // EventSystem can still be current (DefeatGamepadNavigationTests'
            // own note, and MainMenuGamepadNavigationTests before it).
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the hub has no SystemMenuController");

            _dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_dossier, "the Character pane has no dossier controller");

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
        // lookup -- the same shape Halo above already uses for the same
        // reason.
        private static GameObject TooltipEvenWhileHidden =>
            Resources.FindObjectsOfTypeAll<Transform>()
                .FirstOrDefault(t => t.name == "DossierTooltip" && t.gameObject.scene.IsValid())
                ?.gameObject;

        private static Image Halo(string name) =>
            Resources.FindObjectsOfTypeAll<Image>()
                .FirstOrDefault(im => im.name == name && im.gameObject.scene.IsValid());

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

        [UnityTest]
        public IEnumerator RightFromTheFirstWeaponSlot_ReachesTheFirstAbilityScore()
        {
            yield return OpenTheCharacterTab();
            Select("DossierSlotWeapon1");

            yield return Press(1f, 0f);

            AssertSelected("DossierAttrCell0",
                "the loadout's right file hands off to the scores' own left column");
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
            yield return Press(0f, 1f);

            AssertSelected("DossierPackSort0", "precondition: Up out of the window reaches the sort tabs");
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

        // ---- the selected halo (AUDIT.md #160) ----------------------------------

        [UnityTest]
        public IEnumerator SelectingAPackCell_ShowsItsSelectedHalo()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();

            Select("DossierPackCell0");
            yield return null;

            Assert.IsTrue(Halo("DossierPackHalo0").gameObject.activeSelf,
                "AUDIT.md #160: a NoChrome pack cell carries no ThemedButtonState of its own, so its halo " +
                "is the only thing that can show focus without the tooltip");
        }

        [UnityTest]
        public IEnumerator MovingOffAPackCell_HidesItsSelectedHalo()
        {
            yield return OpenTheCharacterTab();
            yield return OpenThePack();

            Select("DossierPackCell0");
            yield return null;
            Assert.IsTrue(Halo("DossierPackHalo0").gameObject.activeSelf, "precondition: the halo is up");

            // Up out of the window lands on a sort tab (see
            // MovingOffAPackCellOntoSomethingWithNoTooltip_HidesIt above).
            yield return Press(0f, 1f);

            Assert.IsFalse(Halo("DossierPackHalo0").gameObject.activeSelf,
                "the halo belongs to the selection, so it goes when the selection moves away");
        }

        [UnityTest]
        public IEnumerator SelectingAnEquipmentSlot_ShowsItsSelectedHalo()
        {
            yield return OpenTheCharacterTab();
            Select("DossierSlotHead");
            yield return null;

            Assert.IsTrue(Halo("DossierSlotHaloHead").gameObject.activeSelf,
                "AUDIT.md #160: the loadout's slots are the same NoChrome shape as the pack cells, with the " +
                "same missing selected-visual");
        }

        [UnityTest]
        public IEnumerator SelectingAnAbilityScoreCell_ShowsItsSelectedHalo()
        {
            yield return OpenTheCharacterTab();
            Select("DossierAttrCell0");
            yield return null;

            Assert.IsTrue(Halo("DossierAttrHalo0").gameObject.activeSelf,
                "AUDIT.md #160: the ability-score cells are the third NoChrome group this pass covers");
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
