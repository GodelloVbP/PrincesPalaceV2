using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Content;

namespace PrincesPalace.Tests.PlayMode
{
    // Clicking a pack cell has to actually move the item onto the character.
    //
    // This exists because a screenshot could not answer it. The capture showed
    // an equip working on one run and doing nothing on the next, and a picture
    // of an unchanged screen looks identical whether the click was ignored, the
    // rules refused it, or the refresh simply did not repaint. An assertion on
    // the loadout says which.
    public class DossierEquipTests
    {
        private string _root;

        // Its own save root, seeded with a known wearable item.
        //
        // Without this the test read whatever save the runner happened to be
        // carrying, which is order-dependent: it passed for as long as some
        // earlier test left 27 items lying around and failed the moment the
        // ordering changed, with "nothing in the pack to equip" -- a message
        // about the fixture wearing the costume of a product bug.
        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-dossier-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        [UnityTest]
        public IEnumerator ClickingAPackItemEquipsIt()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");
            menu.Open();
            menu.Select(0);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the Character pane has no dossier controller");
            dossier.ShowPack(true);
            yield return null;

            var save = SaveSlotManager.CurrentSave;
            var character = save?.ActiveSquad()?.FirstOrDefault(c => c != null);
            Assert.IsNotNull(character?.equipment, "no squad member with a loadout to equip onto");

            // The first cell is whatever BagView sorted to the front, which is a
            // wearable item by construction -- IsEquippable is its first sort key.
            var cell = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)
                .FirstOrDefault(b => b.name == "DossierPackCell0");
            Assert.IsNotNull(cell, "the pack drew no cell named DossierPackCell0");

            // Seeded here rather than assumed: the first equippable item the
            // content database has, put in the pack, so the cell the test
            // clicks is known to be wearable.
            var wearable = ContentDatabase.Equippables.FirstOrDefault();
            Assert.IsNotNull(wearable, "the content database has no equippable item to test with");

            InventoryOps.Add(save.stockpiledItems, wearable.id);
            SaveSlotManager.SaveCurrent();
            dossier.Refresh();
            yield return null;

            // The WHOLE loadout, not one named slot: which slot an item lands in
            // is ResolveTargetSlot's business, and a test that guesses the slot
            // would fail for a correct equip into the other weapon hand.
            var before = EquipmentSlots.All.ToDictionary(sl => sl, sl => character.equipment.Get(sl));
            int carriedBefore = save.stockpiledItems.Sum(e => e.count);
            Assert.Greater(carriedBefore, 0, "nothing in the pack to equip");

            cell.onClick.Invoke();
            yield return null;

            var changed = EquipmentSlots.All
                .Where(s => character.equipment.Get(s) != before[s])
                .ToList();

            Assert.IsNotEmpty(changed,
                "clicking the pack cell changed nothing on the character. The loadout was " +
                string.Join(", ", EquipmentSlots.All.Select(s => $"{s}='{before[s]}'")));

            // And nothing was duplicated or destroyed on the way: a swap puts the
            // displaced item back, so the carried total moves by one at most.
            int carriedAfter = save.stockpiledItems.Sum(e => e.count);
            Assert.That(carriedBefore - carriedAfter, Is.InRange(0, 1),
                "the equip changed the carried count by more than the one item it moved");
        }
    }
}
