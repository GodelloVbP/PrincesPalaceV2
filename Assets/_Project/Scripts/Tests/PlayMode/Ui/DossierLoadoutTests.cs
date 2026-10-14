using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // LOADOUT (plan C16, spec 3.1) on the Map, where every change it offers is
    // allowed: gear on and off the slots, books into and out of the book row,
    // the two pending states (SlotArmed, BookPicked), the pack's filters and
    // the compare column.
    //
    // Presses go through each control's own onClick -- the pad's A and the
    // mouse's click both land there (DossierGamepadNavigationTests drives the
    // dispatcher). The fixture seats Shawn in the middle, Odette at the rear
    // and Bjorn in front, and every test selects whom it is about.
    public class DossierLoadoutTests
    {
        private const string Shawn = "sheep";
        private const string Odette = "owl";
        private const string Bjorn = "bear";

        private const string BookA = "frost_flare";
        private const string BookB = "mud_burst";
        private const string BookC = "lightning_bolt";
        private const string BookD = "cinderfault";

        private string _root;
        private SystemMenuController _menu;
        private CharacterDossierController _dossier;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-loadout-" + System.Guid.NewGuid().ToString("N"));
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
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // A run on the Map, the three seated, an empty pack and no books.
        private IEnumerator OpenOnTheMap(string who)
        {
            RunManager.StartRun(4242UL);

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { Bjorn, Shawn, Odette };
            save.stockpiledItems.Clear();
            foreach (var member in save.roster) member.equipment.Clear();

            RunManager.Run.unassignedSpellBooks = new List<string>();
            RunManager.Run.learnedSpells = new List<LearnedSpellEntry>();

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _menu = DossierPane.Menu();
            _dossier = DossierPane.Dossier();

            _menu.Open(SystemMenuTab.Character);
            yield return null;

            Assert.IsTrue(_dossier.SelectCharacter(who), $"fixture: {who} is not on the strip");
            _dossier.ShowSection(CharacterSection.Loadout, focusRow: false);
            yield return null;
        }

        private static Character Of(string id) => SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == id);

        private void Pack(params string[] itemIds)
        {
            foreach (var id in itemIds) InventoryOps.Add(SaveSlotManager.CurrentSave.stockpiledItems, id);
            _dossier.Refresh();
        }

        private void Books(params string[] skillIds)
        {
            RunManager.Run.unassignedSpellBooks.AddRange(skillIds);
            _dossier.Refresh();
        }

        private static void Learned(string characterId, string skillId, int slot) =>
            RunManager.Run.learnedSpells.Add(new LearnedSpellEntry { characterId = characterId, skillId = skillId, slot = slot });

        private static string BookIn(string characterId, int slot) =>
            RunManager.Run.learnedSpells.FirstOrDefault(e => e.characterId == characterId && e.slot == slot)?.skillId;

        private static ItemDefinition ItemFor(EquipmentSlot slot) =>
            ContentDatabase.Equippables.First(i => i.equipSlot == slot);

        private string Notice => DossierPane.TextOf("SystemFooterNotice");

        // ---- what is drawn: a label's string can be set while TMP draws none of it
        // (its box shorter than one line), so these read the built mesh ---------------------

        [UnityTest]
        public IEnumerator EveryGearSlotDrawsItsName_AndEveryBookSlotItsNumber()
        {
            yield return OpenOnTheMap(Odette);

            foreach (var slot in EquipmentSlots.All)
            {
                Assert.AreEqual(UiStrings.EquipmentSlotName(slot).Format(),
                    DossierPane.DrawnTextOf("DossierSlotCaption" + slot),
                    $"the {slot} slot draws no caption");
            }

            Assert.AreEqual("Head", DossierPane.DrawnTextOf("DossierSlotCaptionHead"));
            Assert.AreEqual("Weapon 2", DossierPane.DrawnTextOf("DossierSlotCaptionWeapon2"));

            for (int i = 0; i < SpellBooks.MaxSpellSlots; i++)
            {
                Assert.AreEqual($"Book {i + 1}", DossierPane.DrawnTextOf($"DossierBook{i}Caption"),
                    $"the empty book slot {i} draws no caption");
            }
        }

        // A filled book slot is captioned with the book, cut to its caption.
        [UnityTest]
        public IEnumerator AFilledBookSlotDrawsTheBooksName()
        {
            yield return OpenOnTheMap(Odette);
            Learned(Odette, BookA, 1);
            _dossier.Refresh();

            string name = ContentDatabase.GetSkill(BookA).Data.DisplayName;
            string drawn = DossierPane.DrawnTextOf("DossierBook1Caption");
            Assert.IsNotEmpty(drawn, "the filled book slot draws no caption");
            Assert.AreEqual(name.Substring(0, Mathf.Min(4, name.Length)), drawn.Substring(0, Mathf.Min(4, drawn.Length)),
                "the caption is not the book's name");
        }

        [UnityTest]
        public IEnumerator EveryStripChipDrawsItsNameAndSeat_AndABenchedOneIsDimmed()
        {
            yield return OpenOnTheMap(Odette);

            Assert.AreEqual("Odette", DossierPane.DrawnTextOf("DossierChip0Name"));
            Assert.AreEqual("Rear", DossierPane.DrawnTextOf("DossierChip0Seat"));
            Assert.AreEqual("Shawn", DossierPane.DrawnTextOf("DossierChip1Name"));
            Assert.AreEqual("Middle", DossierPane.DrawnTextOf("DossierChip1Seat"));
            Assert.AreEqual("Bjorn", DossierPane.DrawnTextOf("DossierChip2Name"));
            Assert.AreEqual("Front", DossierPane.DrawnTextOf("DossierChip2Seat"));
        }

        // The generic net under the three above: any label that ends in an
        // ellipsis or wraps inside a box and has words must draw at least a
        // glyph of them.
        [UnityTest]
        public IEnumerator NoLabelOnTheCharacterTabHasWordsAndDrawsNone()
        {
            yield return OpenOnTheMap(Odette);
            Pack(ItemFor(EquipmentSlot.Head).id);

            var silent = new List<string>();
            int checkedLabels = 0;
            foreach (var text in _dossier.GetComponentsInChildren<TMP_Text>(includeInactive: false))
            {
                if (!text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) continue;
                if (text.overflowMode != TextOverflowModes.Ellipsis) continue;

                checkedLabels++;
                if (string.IsNullOrEmpty(DossierPane.DrawnText(text))) silent.Add(text.name);
            }

            Assert.Greater(checkedLabels, 12, "the scan found almost nothing to check");
            Assert.IsEmpty(silent, "labels with words that draw none: " + string.Join(", ", silent));
        }

        // ---- the compare column at its longest ---------------------------------------------------

        [UnityTest]
        public IEnumerator TheLongestDeltaFitsItsRowOnOneLine_WithoutTouchingTheNextRow()
        {
            yield return OpenOnTheMap(Odette);

            int defence = Enumerable.Range(0, SheetStats.Derived.Length)
                .First(i => SheetStats.Derived[i] == SheetStat.PhysicalDefense);
            string worst = LoadoutCompare.DeltaText(
                SheetStats.DisplayText(SheetStat.PhysicalDefense, 1000),
                SheetStats.DisplayText(SheetStat.PhysicalDefense, 1200), 200);

            // Every row at the worst the column can be asked to hold.
            var values = new List<TMP_Text>();
            var names = new List<TMP_Text>();
            for (int i = 0; i < SheetStats.Derived.Length; i++)
            {
                var value = DossierPane.Label($"DossierCompareStat{i}Value");
                value.SetContent(worst);
                DossierPane.Label($"DossierCompareStat{i}Name").SetContent(UiStrings.StatPhysicalDefense.Format());
                values.Add(value);
                names.Add(DossierPane.Label($"DossierCompareStat{i}Name"));
            }

            Assert.AreEqual(worst, DossierPane.DrawnText(values[defence]), "the delta was cut or wrapped in its row");
            Assert.AreEqual(1, values[defence].textInfo.lineCount, "the delta wrapped onto a second line");
            Assert.LessOrEqual(values[defence].preferredWidth, values[defence].rectTransform.rect.width,
                "the delta is wider than its row");

            var all = new List<TMP_Text>();
            for (int i = 0; i < values.Count; i++) { all.Add(names[i]); all.Add(values[i]); }

            for (int a = 0; a < all.Count; a++)
            {
                for (int b = a + 1; b < all.Count; b++)
                {
                    Assert.IsFalse(DossierPane.Overlap(all[a].rectTransform, all[b].rectTransform),
                        $"{all[a].name} overlaps {all[b].name}");
                }
            }
        }

        // The focused item's summary wraps to its four lines instead of ending
        // at "..." after the first.
        [UnityTest]
        public IEnumerator AGearSummaryWrapsInsteadOfTruncating()
        {
            yield return OpenOnTheMap(Odette);

            // Longest over every item at every tier of its art ladder: the
            // numbers (and so the text) grow with the tier.
            var (longest, longestTier) = ContentDatabase.Equippables
                .SelectMany(i => Enumerable.Range(0, i.curve.maxTier + 1).Select(t => (Item: i, Tier: t)))
                .OrderByDescending(x => ItemDescription.CardSummary(ItemInstance.Of(x.Item.id, x.Tier), 0).Length)
                .First();
            InventoryOps.Add(SaveSlotManager.CurrentSave.stockpiledItems, longest.id, 1, tier: longestTier);
            _dossier.Refresh();

            DossierPane.Select(DossierPane.PackItemCell(_dossier, longest.id));
            yield return null;

            var description = DossierPane.Label("DossierCompareDescription");
            string drawn = DossierPane.DrawnText(description);

            Assert.IsNotEmpty(drawn, "the summary draws nothing");
            StringAssert.DoesNotContain("…", drawn, "the summary of the longest item still ends in an ellipsis");
            Assert.LessOrEqual(description.textInfo.lineCount, 4, "the summary needs more than four lines");
        }

        // A book's words take the column the stat rows leave: the description
        // and the rows are hidden while the body shows.
        [UnityTest]
        public IEnumerator ABooksWordsShowInTheBody_AndTheStatRowsStandDown()
        {
            yield return OpenOnTheMap(Odette);

            string id = ContentDatabase.Skills
                .Where(s => s.Data.Description != null)
                .OrderByDescending(s => s.Data.Description.Length)
                .First().Data.Id;
            Books(id);

            DossierPane.Select(DossierPane.PackBookCell(_dossier, id));
            yield return null;

            var body = DossierPane.Label("DossierCompareBody");
            Assert.IsTrue(body.gameObject.activeInHierarchy, "a book's words have no place to show");
            Assert.IsFalse(DossierPane.Label("DossierCompareDescription").gameObject.activeInHierarchy);
            Assert.AreEqual("", DossierPane.TextOf("DossierCompareStat0Value"), "a stat row shows for a book");

            string drawn = DossierPane.DrawnText(body);
            StringAssert.DoesNotContain("…", drawn, "the longest book description is cut");

            // And back: a gear entry shows the description and the rows, not the body.
            Pack(ItemFor(EquipmentSlot.Head).id);
            DossierPane.Select(DossierPane.PackItemCell(_dossier, ItemFor(EquipmentSlot.Head).id));
            yield return null;

            Assert.IsFalse(DossierPane.Label("DossierCompareBody").gameObject.activeInHierarchy, "the body stays over a gear entry");
            Assert.IsTrue(DossierPane.Label("DossierCompareDescription").gameObject.activeInHierarchy);
        }

        // A salve with no art (AUDIT #131) names itself in its cell, and a
        // cell that has art does not.
        [UnityTest]
        public IEnumerator AnArtlessPackCellDrawsItsName()
        {
            yield return OpenOnTheMap(Shawn);

            Pack("health_potion", "health_potion", ItemFor(EquipmentSlot.Head).id);

            string potion = DossierPane.PackItemCell(_dossier, "health_potion");
            string cell = potion.Substring("DossierPackCell".Length);
            var name = DossierPane.Label("DossierPackName" + cell);

            Assert.IsTrue(name.gameObject.activeInHierarchy, "an artless cell shows no name");
            StringAssert.Contains("Health", DossierPane.DrawnText(name), "the cell does not say what it holds");
            Assert.Greater(name.textInfo.characterCount, 0, "the name label built no mesh");

            string gear = DossierPane.PackItemCell(_dossier, ItemFor(EquipmentSlot.Head).id)
                .Substring("DossierPackCell".Length);
            Assert.IsFalse(DossierPane.Label("DossierPackName" + gear).gameObject.activeInHierarchy,
                "a cell with art also prints its name over it");
        }

        // Nothing focused: the derived list starts under the "Stats" heading,
        // not below the description a focused entry would fill.
        [UnityTest]
        public IEnumerator TheStatListClosesUpUnderItsHeadingWhenNothingIsFocused()
        {
            yield return OpenOnTheMap(Shawn);

            EventSystem.current.SetSelectedGameObject(null);
            _dossier.Refresh();
            yield return null;

            var first = DossierPane.Label("DossierCompareStat0Name").rectTransform;
            Assert.AreEqual(244.5f, first.anchoredPosition.y, 0.01f, "the list did not close up under the heading");
            Assert.IsNotEmpty(DossierPane.DrawnTextOf("DossierCompareStat0Name"), "the first stat draws nothing");

            Pack(ItemFor(EquipmentSlot.Head).id);
            DossierPane.Select(DossierPane.PackItemCell(_dossier, ItemFor(EquipmentSlot.Head).id));
            yield return null;

            Assert.AreEqual(104.5f, first.anchoredPosition.y, 0.01f, "a gear summary needs the room the list gave up");
        }

        // ---- the strip's pager plates are the footer's ---------------------------------------------

        [UnityTest]
        public IEnumerator TheStripsPagerPlatesAreTheFootersGlyphPlates()
        {
            yield return OpenOnTheMap(Odette);
            yield return null;

            var device = PromptGlyphs.DeviceFor(NavigationInputModule.LastInputWasPad);
            Assert.AreEqual(PromptGlyphs.For(device, PromptButton.PageBack).Text, DossierPane.TextOf("DossierStripPrevPlateText"));
            Assert.AreEqual(PromptGlyphs.For(device, PromptButton.PageForward).Text, DossierPane.TextOf("DossierStripNextPlateText"));

            // Same rim and fill as a footer plate, so they read as one control.
            Assert.AreEqual(DossierPane.ColourOf("SystemPrompt0GlyphRim"), DossierPane.ColourOf("DossierStripPrevPlateRim"));
            Assert.AreEqual(DossierPane.ColourOf("SystemPrompt0GlyphFill"), DossierPane.ColourOf("DossierStripPrevPlateFill"));

            // Read the footer's own letters at the same size.
            Assert.AreEqual(DossierPane.Label("SystemPrompt0GlyphText").fontSize, DossierPane.Label("DossierStripPrevPlateText").fontSize);
        }

        // ---- gear ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator APackItemEquipsOntoItsSlot_AndAWornSlotUnequips()
        {
            yield return OpenOnTheMap(Shawn);
            var helm = ItemFor(EquipmentSlot.Head);
            Pack(helm.id);

            DossierPane.Press(DossierPane.PackItemCell(_dossier, helm.id));
            Assert.AreEqual(helm.id, Of(Shawn).equipment.Get(EquipmentSlot.Head), "A on pack gear did not equip it");
            Assert.AreEqual(0, SaveSlotManager.CurrentSave.stockpiledItems.Count, "the equip left a copy in the pack");

            DossierPane.Press(DossierPane.SlotName(EquipmentSlot.Head));
            Assert.That(Of(Shawn).equipment.Get(EquipmentSlot.Head), Is.Null.Or.Empty, "A on a worn slot did not unequip it");
            Assert.AreEqual(1, SaveSlotManager.CurrentSave.stockpiledItems.Sum(e => e.count), "the unequip lost the item");
        }

        // The armed slot is where the item goes, the off hand included: a
        // Weapon 1 item fits an empty Weapon 2.
        [UnityTest]
        public IEnumerator AnArmedSlotTakesTheItemIntoThatSlot_TheOffHandIncluded()
        {
            yield return OpenOnTheMap(Shawn);
            var sword = ItemFor(EquipmentSlot.Weapon1);
            Pack(sword.id);

            DossierPane.Press(DossierPane.SlotName(EquipmentSlot.Weapon2));
            Assert.IsTrue(_dossier.Pending.ArmsGear, "A on the empty off hand did not arm it");
            Assert.IsTrue(_dossier.VisiblePack.All(e => LoadoutPack.FitsGearSlot(e, EquipmentSlot.Weapon2)),
                "the armed pack shows something the off hand cannot take");

            DossierPane.Press("DossierPackCell0");

            Assert.IsFalse(_dossier.HasPending, "the equip left the slot armed");
            Assert.AreEqual(sword.id, Of(Shawn).equipment.Get(EquipmentSlot.Weapon2), "the item did not go to the armed slot");
            Assert.That(Of(Shawn).equipment.Get(EquipmentSlot.Weapon1), Is.Null.Or.Empty);
            DossierPane.AssertSelected(DossierPane.SlotName(EquipmentSlot.Weapon2), "focus goes back to the slot it filled");
        }

        [UnityTest]
        public IEnumerator ArmingNarrowsThePackToTheSlot_AndCancelPutsTheFilterBack()
        {
            yield return OpenOnTheMap(Shawn);
            Pack(ItemFor(EquipmentSlot.Head).id, ItemFor(EquipmentSlot.Legs).id, ItemFor(EquipmentSlot.Weapon1).id);

            DossierPane.Press("DossierFilter2");
            Assert.AreEqual(PackFilter.Weapons, _dossier.Filter, "fixture: the Weapons chip did not filter");

            DossierPane.Press(DossierPane.SlotName(EquipmentSlot.Legs));
            Assert.AreEqual(1, _dossier.VisiblePack.Count, "the armed pack shows more than the one item Legs takes");
            Assert.AreEqual(EquipmentSlot.Legs, _dossier.VisiblePack[0].Item.Slot);

            _dossier.CancelPending();

            Assert.AreEqual(PackFilter.Weapons, _dossier.Filter, "cancelling the armed slot lost the chosen filter");
            Assert.AreEqual(1, _dossier.VisiblePack.Count);
            Assert.That(Of(Shawn).equipment.Get(EquipmentSlot.Legs), Is.Null.Or.Empty, "cancelling equipped something");
        }

        [UnityTest]
        public IEnumerator AnEmptySlotWithNothingThatFits_SaysSoAndArmsNothing()
        {
            yield return OpenOnTheMap(Shawn);

            DossierPane.Press(DossierPane.SlotName(EquipmentSlot.Shoes));

            Assert.IsFalse(_dossier.HasPending, "a slot armed over a pack with nothing for it");
            Assert.AreEqual("Nothing in the pack fits this slot.", Notice);
        }

        // ---- books ---------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator APackBookGoesToTheLowestFreeSlot()
        {
            yield return OpenOnTheMap(Shawn);
            Learned(Shawn, BookA, 0);
            Books(BookB);

            DossierPane.Press(DossierPane.PackBookCell(_dossier, BookB));

            Assert.AreEqual(BookB, BookIn(Shawn, 1), "the book did not go to the lowest free slot");
            CollectionAssert.DoesNotContain(RunManager.Run.unassignedSpellBooks, BookB);
            Assert.AreEqual(ContentDatabase.GetSkill(BookB).Data.DisplayName, DossierPane.TextOf("DossierBook1Caption"));
        }

        // Equipping a pack book removes the focused cell, so focus must move to a
        // live control in the same section. Falling back to the menu context's
        // entry from the last tab change would jump to another section (the
        // REWARD TRACK row when the menu opened there). Any book, any character.
        [UnityTest]
        public IEnumerator EquippingAPackBook_NeverJumpsSections_WhateverSectionTheMenuOpenedOn()
        {
            foreach (var who in new[] { Shawn, Odette })
            {
                yield return OpenOnTheMap(who);

                _dossier.ShowSection(CharacterSection.RewardTrack, focusRow: false);
                _menu.Open(SystemMenuTab.Character);
                yield return null;
                _dossier.ShowSection(CharacterSection.Loadout, focusRow: false);
                yield return null;

                Books(BookB);
                DossierPane.Select(DossierPane.PackBookCell(_dossier, BookB));
                DossierPane.Press(DossierPane.PackBookCell(_dossier, BookB));
                yield return null;
                yield return null;

                Assert.AreEqual(CharacterSection.Loadout, _dossier.Section, $"{who}: equipping a book changed the section");
                DossierPane.AssertSelected("DossierBook0", $"{who}: focus should land on the slot the book took");
            }
        }

        // The context's fallback focus follows the shown section, not the section
        // the menu opened on.
        [UnityTest]
        public IEnumerator AVanishedFocus_FallsBackToTheShownSectionsRow()
        {
            yield return OpenOnTheMap(Shawn);

            _dossier.ShowSection(CharacterSection.RewardTrack, focusRow: false);
            _menu.Open(SystemMenuTab.Character);
            yield return null;
            _dossier.ShowSection(CharacterSection.Loadout, focusRow: false);
            yield return null;

            Books(BookB);
            string cell = DossierPane.PackBookCell(_dossier, BookB);
            DossierPane.Select(cell);
            yield return null;
            DossierPane.Need(cell).gameObject.SetActive(false);
            yield return null;
            yield return null;

            DossierPane.AssertSelected(DossierPane.SectionRow(CharacterSection.Loadout),
                "a vanished focus fell back to another section's row");
            Assert.AreEqual(CharacterSection.Loadout, _dossier.Section);
        }

        // BookPicked: every slot full, so the pack book waits for a slot to
        // replace; the replaced book goes back to the pack.
        [UnityTest]
        public IEnumerator WithEveryBookSlotFull_APackBookIsPicked_AndTheSlotPressedIsReplaced()
        {
            yield return OpenOnTheMap(Shawn);
            Learned(Shawn, BookA, 0);
            Learned(Shawn, BookB, 1);
            Learned(Shawn, BookC, 2);
            Books(BookD);

            DossierPane.Press(DossierPane.PackBookCell(_dossier, BookD));

            Assert.AreEqual(LoadoutPendingKind.BookPicked, _dossier.Pending.Kind, "a full book row did not pick the book");
            DossierPane.AssertSelected("DossierBook0", "picking a book moves focus to the first book slot");

            DossierPane.Press("DossierBook1");

            Assert.IsFalse(_dossier.HasPending);
            Assert.AreEqual(BookD, BookIn(Shawn, 1), "the pressed slot was not replaced");
            CollectionAssert.Contains(RunManager.Run.unassignedSpellBooks, BookB, "the replaced book did not return to the pack");
        }

        [UnityTest]
        public IEnumerator CancellingAPickedBook_ReturnsFocusToItsPackCell_AndChangesNothing()
        {
            yield return OpenOnTheMap(Shawn);
            Learned(Shawn, BookA, 0);
            Learned(Shawn, BookB, 1);
            Learned(Shawn, BookC, 2);
            Books(BookD);

            string cell = DossierPane.PackBookCell(_dossier, BookD);
            DossierPane.Press(cell);
            Assert.IsTrue(_dossier.HasPending, "fixture: the book was not picked");

            _dossier.CancelPending();

            Assert.IsFalse(_dossier.HasPending);
            DossierPane.AssertSelected(cell, "cancelling a picked book returns focus to its pack cell");
            Assert.AreEqual(BookA, BookIn(Shawn, 0));
            CollectionAssert.Contains(RunManager.Run.unassignedSpellBooks, BookD);
        }

        // A on a filled book slot gives the book back to the pack
        // (RunOrchestrator.UnlearnSpell, wired here).
        [UnityTest]
        public IEnumerator AFilledBookSlotGivesItsBookBackToThePack()
        {
            yield return OpenOnTheMap(Shawn);
            Learned(Shawn, BookA, 0);
            _dossier.Refresh();

            DossierPane.Press("DossierBook0");

            Assert.IsNull(BookIn(Shawn, 0), "the book stayed in its slot");
            CollectionAssert.Contains(RunManager.Run.unassignedSpellBooks, BookA, "the removed book did not reach the pack");
            Assert.AreEqual("Book 1", DossierPane.TextOf("DossierBook0Caption"));
        }

        [UnityTest]
        public IEnumerator AnEmptyBookSlotArms_AndThePackBookFillsExactlyThatSlot()
        {
            yield return OpenOnTheMap(Odette);
            Books(BookA);

            DossierPane.Press("DossierBook2");
            Assert.IsTrue(_dossier.Pending.ArmsBook, "A on an empty book slot did not arm it");
            Assert.AreEqual(PackEntryKind.Book, _dossier.VisiblePack[0].Kind, "the armed pack is not books");

            DossierPane.Press("DossierPackCell0");

            Assert.AreEqual(BookA, BookIn(Odette, 2), "the book did not go to the armed slot");
            Assert.IsNull(BookIn(Odette, 0), "the book went to the lowest slot rather than the armed one");
        }

        // Bjorn fights with Fury: one sentence where the book row would be,
        // and a pack book pressed for him says why.
        [UnityTest]
        public IEnumerator ACharacterWhoCannotCarryBooks_GetsOneSentenceInsteadOfTheBookRow()
        {
            yield return OpenOnTheMap(Bjorn);
            Books(BookA);

            var line = DossierPane.Control("DossierBook0");
            Assert.IsFalse(line.gameObject.activeInHierarchy, "the book row is drawn for a character who cannot carry books");

            string sentence = DossierPane.TextOf("DossierNoBooksLine");
            StringAssert.Contains("Bjorn", sentence);
            StringAssert.Contains("cannot carry spell books", sentence);

            DossierPane.Press(DossierPane.PackBookCell(_dossier, BookA));
            StringAssert.Contains("cannot carry spell books", Notice, "a pack book pressed for Bjorn said nothing");
            CollectionAssert.Contains(RunManager.Run.unassignedSpellBooks, BookA);
        }

        [UnityTest]
        public IEnumerator ABookTheCharacterAlreadyKnows_IsRefusedWithWhy()
        {
            yield return OpenOnTheMap(Shawn);
            Learned(Shawn, BookA, 0);
            Books(BookA);

            DossierPane.Press(DossierPane.PackBookCell(_dossier, BookA));

            Assert.AreEqual("You already have this spell prepared", Notice);
            CollectionAssert.Contains(RunManager.Run.unassignedSpellBooks, BookA, "the duplicate left the pack");
        }

        [UnityTest]
        public IEnumerator ABookShowsItsArtInThePackAndInItsSlot()
        {
            yield return OpenOnTheMap(Shawn);
            Learned(Shawn, BookA, 0);
            Books(BookB);

            Assert.IsTrue(DossierPane.Control("DossierBook0").transform.Find("DossierBook0Icon").gameObject.activeSelf,
                "the placed book draws no art");

            string cell = DossierPane.PackBookCell(_dossier, BookB);
            int index = int.Parse(cell.Substring("DossierPackCell".Length));
            Assert.IsTrue(DossierPane.Control(cell).transform.Find($"DossierPackIcon{index}").gameObject.activeSelf,
                "the pack book draws no art");
        }

        // ---- the pack's filters ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator EachFilterShowsItsKind()
        {
            yield return OpenOnTheMap(Shawn);
            var salve = ContentDatabase.Items.First(i => i != null && !i.IsEquippable);
            Pack(ItemFor(EquipmentSlot.Head).id, ItemFor(EquipmentSlot.Weapon1).id, salve.id);
            Books(BookA);

            var expected = new Dictionary<int, int> { { 0, 4 }, { 1, 1 }, { 2, 1 }, { 3, 1 }, { 4, 1 } };
            foreach (var pair in expected)
            {
                DossierPane.Press($"DossierFilter{pair.Key}");
                Assert.AreEqual(pair.Value, _dossier.VisiblePack.Count, $"filter {LoadoutPack.Filters[pair.Key]}");
                Assert.IsTrue(DossierPane.Control($"DossierFilter{pair.Key}").transform
                        .Find($"DossierFilter{pair.Key}Marker").gameObject.activeSelf,
                    $"filter {LoadoutPack.Filters[pair.Key]}'s marker is not lit");
            }
        }

        [UnityTest]
        public IEnumerator AnEmptyPackSaysSo_AndAnEmptyFilterSaysItsOwnSentence()
        {
            yield return OpenOnTheMap(Shawn);
            _dossier.Refresh();

            Assert.AreEqual("Nothing carried. Gear from fights and shops lands here.", DossierPane.TextOf("DossierPackEmptyHint"));

            Pack(ItemFor(EquipmentSlot.Head).id);
            DossierPane.Press("DossierFilter4");
            Assert.AreEqual("Nothing of this kind carried.", DossierPane.TextOf("DossierPackEmptyHint"));
        }

        // ---- compare -------------------------------------------------------------------------

        // "Health 280 → 338 (+58)": what it is, what it would be, the change --
        // and the equip then does exactly what the preview said.
        [UnityTest]
        public IEnumerator APackItemsCompareReadsNowThenNextThenTheChange()
        {
            yield return OpenOnTheMap(Shawn);
            var character = Of(Shawn);
            var item = ContentDatabase.Equippables.FirstOrDefault(i =>
                ItemDescription.Compare(character, ItemInstance.Of(i.id, 0)).StatDelta.maxHealth > 0);
            Assert.IsNotNull(item, "fixture: no item in the catalogue raises health");
            Pack(item.id);

            DossierPane.Select(DossierPane.PackItemCell(_dossier, item.id));
            yield return null;

            string label = UiStrings.StatMaxHealth.Format();
            int row = Enumerable.Range(0, SheetStats.Derived.Length)
                .First(i => DossierPane.TextOf($"DossierCompareStat{i}Name") == label);
            string shown = DossierPane.TextOf($"DossierCompareStat{row}Value");

            int before = ContentDatabase.EffectiveStats(character).maxHealth;
            DossierPane.Press(DossierPane.PackItemCell(_dossier, item.id));
            int after = ContentDatabase.EffectiveStats(character).maxHealth;

            Assert.Greater(after, before, "fixture: the equip did not raise health");
            Assert.AreEqual($"{before} → {after} (+{after - before})", shown,
                "the compare column did not read now, then next, then the change");
        }

        [UnityTest]
        public IEnumerator TheArrowInTheCompareColumnRenders()
        {
            yield return OpenOnTheMap(Shawn);

            var value = Resources.FindObjectsOfTypeAll<TMP_Text>()
                .First(t => t.name == "DossierCompareStat0Value" && t.gameObject.scene.IsValid());
            Assert.IsTrue(value.font.HasCharacter('→', searchFallbacks: true, tryAddCharacter: true),
                "the compare column's font has no arrow, so a change would draw a missing-glyph box");
        }

        // ---- a pending state is dropped by everything else (plan C6) -------------------------------

        private void ArmTheHead()
        {
            Pack(ItemFor(EquipmentSlot.Head).id);
            DossierPane.Press(DossierPane.SlotName(EquipmentSlot.Head));
            Assert.IsTrue(_dossier.HasPending, "fixture: the head slot did not arm");
        }

        private void AssertDroppedWithNothingChanged(string by)
        {
            Assert.IsFalse(_dossier.HasPending, $"{by} left the slot armed");
            Assert.That(Of(Shawn).equipment.Get(EquipmentSlot.Head), Is.Null.Or.Empty, $"{by} committed the armed slot");
            Assert.AreEqual("", DossierPane.TextOf("SystemFooterNotice"), $"{by} left the armed line on the footer");
        }

        [UnityTest]
        public IEnumerator APendingSlotIsDroppedByEveryOtherWayOut()
        {
            yield return OpenOnTheMap(Shawn);

            // Paging the character.
            ArmTheHead();
            _dossier.StepSection(1);
            AssertDroppedWithNothingChanged("LB/RB");
            _dossier.SelectCharacter(Shawn);

            // A rail section change.
            ArmTheHead();
            DossierPane.Press(DossierPane.SectionRow(CharacterSection.Attributes));
            AssertDroppedWithNothingChanged("a rail section change");
            _dossier.ShowSection(CharacterSection.Loadout, focusRow: false);

            // Stepping or clicking the tab.
            ArmTheHead();
            _menu.StepTab(1);
            AssertDroppedWithNothingChanged("LT/RT");
            _menu.Select(SystemMenuTab.Character);
            _dossier.SelectCharacter(Shawn);
            _dossier.ShowSection(CharacterSection.Loadout, focusRow: false);

            // A deep link.
            ArmTheHead();
            _menu.Open(SystemMenuTab.Character, MenuTarget.Pack);
            AssertDroppedWithNothingChanged("a deep link");

            // Clicking elsewhere in the tab: a filter chip.
            ArmTheHead();
            DossierPane.Press("DossierFilter1");
            AssertDroppedWithNothingChanged("a filter press");

            // Closing the menu (the lintel's X).
            ArmTheHead();
            _menu.Close();
            AssertDroppedWithNothingChanged("closing the menu");
            yield return null;
        }

        // Start with something pending cancels it and the menu stays open
        // (plan C6); a second Start closes.
        [UnityTest]
        public IEnumerator StartWithASlotArmed_DropsItAndKeepsTheMenuOpen()
        {
            yield return OpenOnTheMap(Shawn);
            var input = NavSceneReuse.TakeOverInput();

            ArmTheHead();

            input.SystemMenuDown = true;
            yield return null;
            input.ClearOneFrameFlags();

            Assert.IsTrue(_menu.IsOpen, "Start with a slot armed closed the menu");
            AssertDroppedWithNothingChanged("Start");

            input.SystemMenuDown = true;
            yield return null;
            input.ClearOneFrameFlags();

            Assert.IsFalse(_menu.IsOpen, "Start with nothing pending should close the menu");
        }
    }
}
