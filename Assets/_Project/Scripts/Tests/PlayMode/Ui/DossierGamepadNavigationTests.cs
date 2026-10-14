using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The CHARACTER tab's focus map (spec section 9), pinned edge by edge:
    // the rail, the LOADOUT grid of slots, the pack beside it and the filters
    // over it, the book row under the gear, ATTRIBUTES' steppers, the reward
    // track's entry, LB/RB paging and the two pending states' focus moves.
    //
    // Driven through the REAL production dispatcher -- a scripted BaseInput
    // via inputOverride, `yield return null`, assert resulting state -- on the
    // REAL Hub scene. Where a test starts somewhere other than the entry it
    // selects that control directly and presses once, so a failure points at
    // the one edge that broke.
    //
    // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene): OpenTheCharacterTab
    // puts it back (DossierPane.Rest) and every test seeds its own save.
    public class DossierGamepadNavigationTests
    {
        private string _root;
        private SystemMenuController _menu;
        private CharacterDossierController _dossier;
        private ScriptedBaseInput _input;

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

        // Two wearables with art in the pack, so the grid has cells to land on.
        private static void TwoItemsInThePack()
        {
            var pair = ContentDatabase.Equippables
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.iconPath))
                .OrderBy(i => i.sortOrder)
                .Take(2)
                .ToList();
            Assert.AreEqual(2, pair.Count, "the catalogue has fewer than two equippable items with art");

            var save = SaveSlotManager.CurrentSave;
            save.stockpiledItems.Clear();
            foreach (var item in pair) InventoryOps.Add(save.stockpiledItems, item.id);
        }

        private IEnumerator OpenTheCharacterTab(bool withPack = true)
        {
            yield return SharedScene.Ensure("Hub");

            _menu = DossierPane.Menu();
            _dossier = DossierPane.Dossier();
            DossierPane.Rest();
            NavSceneReuse.CloseHubModals();

            if (withPack) TwoItemsInThePack();
            else SaveSlotManager.CurrentSave.stockpiledItems.Clear();

            _input = NavSceneReuse.TakeOverInput();
            NavSceneReuse.ForgetFocusMemory();
            yield return null;

            _menu.Open(SystemMenuTab.Character);
            DossierPane.SelectFirstChip(_dossier);
            _dossier.Refresh();
            yield return null;
        }

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator Press(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return DriveFrame();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;
            yield return DriveFrame();
        }

        private IEnumerator Down() => Press(0f, -1f);
        private IEnumerator Up() => Press(0f, 1f);
        private IEnumerator Left() => Press(-1f, 0f);
        private IEnumerator Right() => Press(1f, 0f);

        private IEnumerator Submit()
        {
            _input.SubmitDown = true;
            yield return DriveFrame();
        }

        private IEnumerator Cancel()
        {
            _input.CancelDown = true;
            yield return DriveFrame();
        }

        private static void Select(string name) => DossierPane.Select(name);

        private static void AssertSelected(string name, string because) => DossierPane.AssertSelected(name, because);

        private static string Slot(EquipmentSlot slot) => DossierPane.SlotName(slot);

        private string TabName => "SystemTab" + _menu.SelectedTab;

        // ---- the frame and the rail -------------------------------------------------

        [UnityTest]
        public IEnumerator Opening_LandsOnTheRailsSelectedSection()
        {
            yield return OpenTheCharacterTab();

            AssertSelected("DossierSection0", "opening lands on the pane's entry, the rail's selected section (LOADOUT)");
        }

        [UnityTest]
        public IEnumerator DownFromTheTab_ReachesTheRail_AndUpComesBack()
        {
            yield return OpenTheCharacterTab();
            Select(TabName);

            yield return Down();
            AssertSelected("DossierSection0", "Down from the tab reaches the pane's entry");

            yield return Up();
            AssertSelected(TabName, "Up from the rail's first row reaches the selected tab");
        }

        // Selection follows focus down the rail, and the rail clamps.
        [UnityTest]
        public IEnumerator DownTheRail_ShowsEachSectionInTurn_AndClampsAtTheFoot()
        {
            yield return OpenTheCharacterTab();
            Select("DossierSection0");

            var expected = new[] { CharacterSection.Attributes, CharacterSection.Skills, CharacterSection.RewardTrack };
            for (int i = 0; i < expected.Length; i++)
            {
                yield return Down();
                AssertSelected($"DossierSection{i + 1}", "the rail is a list, top to bottom");
                Assert.AreEqual(expected[i], _dossier.Section, "the section did not follow focus");
            }

            yield return Down();
            AssertSelected("DossierSection3", "the rail clamps at its foot");
        }

        [UnityTest]
        public IEnumerator TheStripIsNotInTheDPadGraph()
        {
            yield return OpenTheCharacterTab();

            for (int i = 0; i < DossierLayout.ChipCapacity; i++)
            {
                var chip = DossierPane.Control($"DossierChip{i}");
                Assert.IsNotNull(chip, $"the strip drew no chip {i}");
                Assert.AreEqual(UnityEngine.UI.Navigation.Mode.None, chip.navigation.mode,
                    $"chip {i} can be reached by a Move, so the character could change by accident");
            }

            Assert.AreEqual(UnityEngine.UI.Navigation.Mode.None, DossierPane.Need("DossierStripNext").navigation.mode);
        }

        // ---- LOADOUT: the slots ---------------------------------------------------------

        [UnityTest]
        public IEnumerator RightFromTheLoadoutRow_ReachesHead_AndLeftFromNecklaceComesBack()
        {
            yield return OpenTheCharacterTab();
            Select("DossierSection0");

            yield return Right();
            AssertSelected(Slot(EquipmentSlot.Head), "the LOADOUT detail's entry is the first gear slot, Head");

            yield return Left();
            AssertSelected(Slot(EquipmentSlot.Necklace), "Head and Necklace share the top row");

            yield return Left();
            AssertSelected("DossierSection0", "Left from the slot column's left edge reaches the rail");
        }

        // Head sits over the gap between the files; a tie goes to the left
        // file, which is what the spec's press counts read.
        [UnityTest]
        public IEnumerator DownFromHead_ReachesTorso_ThenGloves_ThenLegs_ThenTheFirstBookSlot()
        {
            yield return OpenTheCharacterTab();
            Select(Slot(EquipmentSlot.Head));

            yield return Down();
            AssertSelected(Slot(EquipmentSlot.Torso), "Down from Head reaches the left file");
            yield return Down();
            AssertSelected(Slot(EquipmentSlot.Gloves), "the left file, top to bottom");
            yield return Down();
            AssertSelected(Slot(EquipmentSlot.Legs), "the left file, top to bottom");
            yield return Down();
            AssertSelected("DossierBook0", "the book row is below Legs and Shoes");
        }

        [UnityTest]
        public IEnumerator TheTwoFilesPairByRow()
        {
            yield return OpenTheCharacterTab();

            Select(Slot(EquipmentSlot.Torso));
            yield return Right();
            AssertSelected(Slot(EquipmentSlot.Weapon1), "Torso is drawn level with Weapon 1");

            Select(Slot(EquipmentSlot.Legs));
            yield return Right();
            AssertSelected(Slot(EquipmentSlot.Shoes), "Legs is drawn level with Shoes");

            Select(Slot(EquipmentSlot.Weapon2));
            yield return Left();
            AssertSelected(Slot(EquipmentSlot.Gloves), "Weapon 2 is drawn level with Gloves");

            Select(Slot(EquipmentSlot.Weapon1));
            yield return Up();
            AssertSelected(Slot(EquipmentSlot.Head), "Up from Weapon 1 reaches the top row's nearest slot, Head");
        }

        [UnityTest]
        public IEnumerator UpFromTheTopRow_ReachesTheSelectedTab()
        {
            yield return OpenTheCharacterTab();
            Select(Slot(EquipmentSlot.Necklace));

            yield return Up();
            AssertSelected(TabName, "Up from the pane's top row reaches the selected tab");
        }

        // ---- LOADOUT: the pack ------------------------------------------------------------

        [UnityTest]
        public IEnumerator RightFromTheSlotColumn_ReachesThePack_AndLeftComesBack()
        {
            yield return OpenTheCharacterTab();
            Select(Slot(EquipmentSlot.Weapon1));

            yield return Right();
            AssertSelected("DossierPackCell0", "Right from the slot column reaches the pack cell at its height");

            yield return Left();
            AssertSelected(Slot(EquipmentSlot.Weapon1), "Left from the pack's first column reaches the slot at its height");
        }

        [UnityTest]
        public IEnumerator RightFromThePack_GoesNowhere_ForCompareIsNotFocusable()
        {
            yield return OpenTheCharacterTab();
            Select("DossierPackCell1");

            yield return Right();
            AssertSelected("DossierPackCell1", "with two items the second is the row's last; compare takes no focus");
        }

        [UnityTest]
        public IEnumerator UpFromThePacksTopRow_ReachesTheFilters_AndDownComesBack()
        {
            yield return OpenTheCharacterTab();
            Select("DossierPackCell0");

            yield return Up();
            AssertSelected("DossierFilter0", "Up from the grid's top row reaches the filter chips over it");

            yield return Right();
            AssertSelected("DossierFilter1", "the filters are a rail");

            Select("DossierFilter0");
            yield return Down();
            AssertSelected("DossierPackCell0", "Down from a filter reaches the grid");

            yield return Up();
            yield return Up();
            AssertSelected(TabName, "Up from the filters reaches the selected tab");
        }

        [UnityTest]
        public IEnumerator AFilterChip_FiltersThePack()
        {
            yield return OpenTheCharacterTab();
            Select("DossierFilter4");

            yield return Submit();

            Assert.AreEqual(PackFilter.Books, _dossier.Filter);
            Assert.IsFalse(DossierPane.Control("DossierPackCell0").gameObject.activeInHierarchy,
                "the Books filter still drew the two wearables");
        }

        // Down off the window's bottom row scrolls the pack under a focus that
        // stays put, so every entry is reachable on a pad.
        [UnityTest]
        public IEnumerator DownOffTheBottomRow_ScrollsThePackAndKeepsFocus()
        {
            yield return OpenTheCharacterTab(withPack: false);

            var save = SaveSlotManager.CurrentSave;
            foreach (var item in ContentDatabase.Equippables.Take(DossierLayout.PackVisibleCells + DossierLayout.PackColumns))
            {
                InventoryOps.Add(save.stockpiledItems, item.id);
            }

            _dossier.Refresh();
            yield return null;
            Assert.Greater(_dossier.VisiblePack.Count, DossierLayout.PackVisibleCells, "fixture: the pack fits its window");

            Select("DossierPackCell9");
            yield return Down();

            Assert.AreEqual(1, _dossier.ScrollRow, "Down off the bottom row did not scroll the pack");
            AssertSelected("DossierPackCell9", "scrolling moved focus off its cell");

            yield return Up();
            yield return Up();
            yield return Up();
            yield return Up();
            Assert.AreEqual(0, _dossier.ScrollRow, "Up off the top row did not scroll back");
            AssertSelected("DossierPackCell0", "scrolling back moved focus off its cell");
        }

        // ---- the pending states' focus (spec section 9) ------------------------------------

        // SlotArmed moves focus to the first matching pack cell; B returns to
        // the slot.
        [UnityTest]
        public IEnumerator AOnAnEmptySlot_MovesFocusIntoThePack_AndBReturnsToTheSlot()
        {
            yield return OpenTheCharacterTab(withPack: false);

            var character = SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == _dossier.CharacterId);
            var item = ContentDatabase.Equippables.First(i => string.IsNullOrEmpty(character.equipment.Get(i.equipSlot)));
            InventoryOps.Add(SaveSlotManager.CurrentSave.stockpiledItems, item.id);
            _dossier.Refresh();
            yield return null;

            Select(Slot(item.equipSlot));
            yield return Submit();

            Assert.IsTrue(_dossier.HasPending, "A on an empty slot did not arm it");
            AssertSelected("DossierPackCell0", "arming moves focus to the first matching pack cell");

            yield return Cancel();

            Assert.IsFalse(_dossier.HasPending, "B did not drop the armed slot");
            Assert.IsTrue(_menu.IsOpen, "B with a slot armed closed the menu");
            AssertSelected(Slot(item.equipSlot), "B returns focus to the slot that was armed");
        }

        // ---- ATTRIBUTES ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Attributes_RightFromTheRail_ReachesTheFirstPlus_AndTheStepperIsARail()
        {
            yield return OpenTheCharacterTab();
            Select("DossierSection1");
            yield return null;

            yield return Right();
            AssertSelected("DossierAttrPlus0", "ATTRIBUTES' entry is the first attribute's +");

            yield return Left();
            AssertSelected("DossierAttrMinus0", "- and + are a rail within the row");

            yield return Left();
            AssertSelected("DossierSection1", "Left from - reaches the rail");

            Select("DossierAttrPlus0");
            yield return Down();
            AssertSelected("DossierAttrPlus1", "rows are a list");
        }

        // ---- SKILLS (spec section 9: a list; Down past the last row reaches the link) ----------

        // Bjorn's list, with `talents` kindled. Two talents make it longer than
        // the window, so the last stretch is reached by scrolling.
        private IEnumerator OpenBjornsSkills(params string[] talents)
        {
            yield return OpenTheCharacterTab();

            var bjorn = SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == "bear");
            bjorn.level = 1;
            bjorn.claimedTrackLevel = 0;
            bjorn.unlockedTalentIds.Clear();
            foreach (string talent in talents) bjorn.unlockedTalentIds.Add(talent);

            Assert.IsTrue(_dossier.SelectCharacter("bear"), "fixture: Bjorn is not on the strip");
            _dossier.Refresh();

            Select("DossierSection2");
            yield return null;
            Assert.AreEqual(CharacterSection.Skills, _dossier.Section);
        }

        [UnityTest]
        public IEnumerator Skills_RightFromTheRail_ReachesTheFirstRow_AndLeftReturns()
        {
            yield return OpenBjornsSkills();

            yield return Right();
            AssertSelected("DossierSkillRow0", "SKILLS' entry is the first row");

            yield return Left();
            AssertSelected("DossierSection2", "Left from a row reaches the rail");
        }

        [UnityTest]
        public IEnumerator Skills_UpFromTheFirstRow_ReachesTheSelectedTab()
        {
            yield return OpenBjornsSkills();
            Select("DossierSkillRow0");

            yield return Up();

            AssertSelected(TabName, "Up off the list's first row is the tab");
        }

        // Header, Slam, header, the two talent skills, header, the two talents,
        // the embers line and the link: six controls the pad can stand on.
        [UnityTest]
        public IEnumerator Skills_DownPastTheLastRowReachesTheLink_ScrollingTheWindowToShowIt()
        {
            yield return OpenBjornsSkills("bear_sen_root", "bear_sen_bellow_1");
            Select("DossierSkillRow0");
            Assert.AreEqual(0, _dossier.SkillsTop);

            var seen = new List<string> { "Slam" };
            for (int press = 0; press < 4; press++)
            {
                yield return Down();
                seen.Add(DossierPane.TextOf(DossierPane.SelectedName + "Name"));
            }

            CollectionAssert.AreEqual(new[] { "Slam", "Bellow", "Brace", "The Sentinel", "Bellow" }, seen,
                "Down walks the rows in list order, skipping the headers");
            Assert.IsFalse(DossierPane.Control("DossierSkillLink").gameObject.activeInHierarchy,
                "the link is below the window until the list is scrolled");

            yield return Down();

            AssertSelected("DossierSkillLink", "Down off the last row reaches the camp's link");
            Assert.AreEqual(2, _dossier.SkillsTop, "the window scrolled just far enough to show it");

            yield return Down();
            AssertSelected("DossierSkillLink", "nothing is below the link");
        }

        [UnityTest]
        public IEnumerator Skills_UpFromTheLinkWalksBackToTheHeadOfTheList()
        {
            yield return OpenBjornsSkills("bear_sen_root", "bear_sen_bellow_1");
            Select("DossierSkillRow0");
            for (int press = 0; press < 5; press++) yield return Down();
            AssertSelected("DossierSkillLink", "fixture: the link was not reached");

            for (int press = 0; press < 5; press++) yield return Up();

            AssertSelected("DossierSkillRow0", "Up retraces the rows");
            Assert.AreEqual(0, _dossier.SkillsTop, "and the window is back at its head, showing the first group's name");
            Assert.IsTrue(DossierPane.Control("DossierSkillRow0").gameObject.activeInHierarchy);
        }

        // Scrolling the window under the pad keeps it on the same content while
        // that is still in view.
        [UnityTest]
        public IEnumerator Skills_TheWheelKeepsFocusOnTheSameContent()
        {
            yield return OpenBjornsSkills("bear_sen_root", "bear_sen_bellow_1");
            Select("DossierSkillRow2");
            string name = DossierPane.TextOf("DossierSkillRow2Name");

            _dossier.ScrollSkills(2);
            yield return null;

            Assert.AreEqual(name, DossierPane.TextOf(DossierPane.SelectedName + "Name"),
                "the window moved under the pad and focus followed the row it was on");
        }

        // ---- LB / RB -----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator RB_StepsToTheNextCharacter_AndFocusStaysWhereItWas()
        {
            yield return OpenTheCharacterTab();
            Select(Slot(EquipmentSlot.Torso));
            string before = _dossier.CharacterId;

            _input.TabNextDown = true;
            yield return DriveFrame();

            Assert.AreNotEqual(before, _dossier.CharacterId, "RB did not step the character");
            AssertSelected(Slot(EquipmentSlot.Torso), "paging the character moved focus");
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab, "RB switched tabs");
        }

        [UnityTest]
        public IEnumerator LB_FromTheFirstChip_WrapsToTheLast()
        {
            yield return OpenTheCharacterTab();
            var chips = CharacterStrip.Order(SaveSlotManager.CurrentSave.selectedCharacterIds,
                SaveSlotManager.CurrentSave.roster.Select(c => c.definitionId).ToList());

            Assert.AreEqual(chips[0].Id, _dossier.CharacterId, "fixture: the strip does not open on its first chip");

            _input.TabPrevDown = true;
            yield return DriveFrame();

            Assert.AreEqual(chips[chips.Count - 1].Id, _dossier.CharacterId, "LB from the first chip should wrap");
        }

        [UnityTest]
        public IEnumerator Cancel_WithNothingPending_ClosesTheMenu()
        {
            yield return OpenTheCharacterTab();
            Select(Slot(EquipmentSlot.Head));

            yield return Cancel();

            Assert.IsFalse(_menu.IsOpen, "B with nothing pending should close the menu");
            Assert.AreEqual(1f, Time.timeScale, 0.0001f);
        }
    }
}
