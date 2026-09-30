using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // Finding things on the CHARACTER tab, for the fixtures that drive it.
    // Carries no test of its own, which is what lets it live in Shared/.
    //
    // Controls are found by the names the screen emits (CharacterDossierScreen)
    // rather than through the controller's fields, which are internal to the
    // runtime assembly: the emitted name is a stable contract, because UiAudit
    // fails the build on a duplicate.
    internal static class DossierPane
    {
        public static SystemMenuController Menu()
        {
            var menu = UnityEngine.Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the scene has no SystemMenuController");
            return menu;
        }

        public static CharacterDossierController Dossier()
        {
            var dossier = UnityEngine.Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the Character pane has no dossier controller");
            return dossier;
        }

        // The character the tab shows, and so the one its reward track pays:
        // the strip's selected chip, which opens on the rear seat.
        public static Character Shown()
        {
            string id = Dossier().CharacterId;
            Assert.IsNotNull(id, "the dossier shows nobody");
            return SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == id);
        }

        // A control by name, whether or not it is active, in the loaded scene.
        public static Selectable Control(string name) =>
            Resources.FindObjectsOfTypeAll<Selectable>()
                .FirstOrDefault(s => s.name == name && s.gameObject.scene.IsValid());

        public static Selectable Need(string name)
        {
            var control = Control(name);
            Assert.IsNotNull(control, $"the pane drew no control named {name}");
            return control;
        }

        public static void Press(string name)
        {
            var button = Need(name) as Button;
            Assert.IsNotNull(button, $"{name} is not a button");
            button.onClick.Invoke();
        }

        public static void Select(string name) => EventSystem.current.SetSelectedGameObject(Need(name).gameObject);

        public static GameObject Selected => EventSystem.current.currentSelectedGameObject;

        public static string SelectedName => Selected != null ? Selected.name : null;

        public static void AssertSelected(string name, string because) =>
            Assert.AreEqual(name, SelectedName, because);

        public static string SlotName(EquipmentSlot slot) => "DossierSlot" + EquipmentSlots.DisplayName(slot).Replace(" ", "");

        // The rail row of a section: DossierSection{index in CharacterSections}.
        public static string SectionRow(CharacterSection section) => "DossierSection" + CharacterSections.IndexOf(section);

        // The pack cell drawing the first entry that matches, by where the
        // window actually put it.
        public static string PackCellOf(CharacterDossierController dossier, Func<PackEntry, bool> which)
        {
            int index = dossier.VisiblePack.ToList().FindIndex(e => which(e));
            Assert.GreaterOrEqual(index, 0, "the pack shows no such entry");
            return "DossierPackCell" + (index - dossier.ScrollRow * DossierLayout.PackColumns);
        }

        public static string PackBookCell(CharacterDossierController dossier, string bookId) =>
            PackCellOf(dossier, e => e.Kind == PackEntryKind.Book && e.BookId == bookId);

        public static string PackItemCell(CharacterDossierController dossier, string itemId) =>
            PackCellOf(dossier, e => e.Kind != PackEntryKind.Book && e.Item.Id == itemId);

        public static string TextOf(string name)
        {
            var text = Resources.FindObjectsOfTypeAll<TMPro.TMP_Text>()
                .FirstOrDefault(t => t.name == name && t.gameObject.scene.IsValid());
            Assert.IsNotNull(text, $"the pane drew no label named {name}");
            return text.text;
        }

        // The strip's first chip, as a fresh open shows it: the dossier keeps
        // whoever it last showed, so a fixture that needs the default says so.
        public static void SelectFirstChip(CharacterDossierController dossier)
        {
            var save = SaveSlotManager.CurrentSave;
            var chips = CharacterStrip.Order(save.selectedCharacterIds,
                save.roster.Where(c => c != null).Select(c => c.definitionId).ToList());
            if (chips.Count > 0) dossier.SelectCharacter(chips[0].Id);
        }

        // A REUSED scene put back to rest: nothing pending, LOADOUT showing,
        // the menu closed and its memory forgotten.
        public static void Rest()
        {
            var dossier = UnityEngine.Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            if (dossier != null)
            {
                dossier.CancelPending();
                dossier.ShowSection(CharacterSection.Loadout, focusRow: false);
            }

            var menu = UnityEngine.Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            if (menu != null && menu.IsOpen) menu.Close();

            MenuMemory.Clear();
        }
    }
}
