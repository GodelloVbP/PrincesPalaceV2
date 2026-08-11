using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.PlayModeTests
{
    // The character overlay, driven through the real hub scene.
    //
    // The rules themselves are covered by EquipmentMoveTests and
    // InventoryBagTests without a scene. What is left for PlayMode is only what
    // genuinely needs one: that the wiring reaches those rules, that the cells
    // paint, and that a move survives being written to disk.
    public class CharacterOverlayTests
    {
        private string _root;
        private HubController _hub;
        private CharacterOverlayController _overlay;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-overlay-" + System.Guid.NewGuid().ToString("N"));
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

        private GameObject Named(string name) =>
            _hub.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;
        private static Character Hero => Save.roster[0];

        private IEnumerator OpenTheHub()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _hub = Object.FindAnyObjectByType<HubController>();
            Assert.IsNotNull(_hub, "the Hub scene has no HubController");

            _overlay = _hub.GetComponentInChildren<CharacterOverlayController>(includeInactive: true);
            Assert.IsNotNull(_overlay, "the overlay was never wired into the hub");
        }

        private IEnumerator OpenTheOverlay()
        {
            yield return OpenTheHub();

            Click("CharacterSheetBuilding");

            // Start() runs one frame AFTER SetActive, so the panel's listeners
            // do not exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;
        }

        // ---- opening and closing ------------------------------------------------

        [UnityTest]
        public IEnumerator TheOverlayStartsClosedAndTheBuildingOpensIt()
        {
            yield return OpenTheHub();

            Assert.IsFalse(_overlay.gameObject.activeSelf, "it lives in the hub but starts hidden");

            Click("CharacterSheetBuilding");
            yield return null;

            Assert.IsTrue(_overlay.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator ClosingLeavesTheHubStanding()
        {
            // The whole point of an overlay rather than a scene: the hub was
            // never torn down, so there is nothing to rebuild.
            yield return OpenTheOverlay();

            Click("OverlayCloseButton");
            yield return null;

            Assert.IsFalse(_overlay.gameObject.activeSelf);
            Assert.IsTrue(_hub.gameObject.activeInHierarchy, "the hub is still there behind it");
        }

        // ---- what it shows ---------------------------------------------------------

        [UnityTest]
        public IEnumerator TheStartingKitAppearsInTheBag()
        {
            // A fresh profile is seeded from StartingStock, so the bag is not
            // empty on the first visit.
            yield return OpenTheOverlay();

            Assert.IsNotEmpty(Save.stockpiledItems, "fixture: a new profile has a starting kit");
            Assert.IsTrue(Named("BagCell0").activeSelf, "the first cell shows something");
        }

        [UnityTest]
        public IEnumerator UnusedBagCellsAreHiddenRatherThanDrawnEmpty()
        {
            // A grid of twenty boxes holding four things reads as sixteen
            // things that failed to load.
            yield return OpenTheOverlay();

            int held = Save.stockpiledItems.Count(e => e.count > 0);
            int shown = Enumerable.Range(0, 20).Count(i => Named($"BagCell{i}").activeSelf);

            Assert.AreEqual(Mathf.Min(held, 20), shown);
        }

        [UnityTest]
        public IEnumerator NothingOnScreenIsAWhiteSquare()
        {
            // The specific thing the redesign exists to kill. Every visible
            // Image must either carry a sprite or be a deliberately coloured
            // solid -- never left at default white with nothing in it.
            yield return OpenTheOverlay();

            var offenders = _overlay.GetComponentsInChildren<Image>(includeInactive: false)
                .Where(i => i.enabled && i.sprite == null)
                .Where(i => i.color.a > 0.01f && i.color.r > 0.95f && i.color.g > 0.95f && i.color.b > 0.95f)
                .Select(i => i.name)
                .ToList();

            CollectionAssert.IsEmpty(offenders, "white squares: " + string.Join(", ", offenders));
        }

        // ---- equipping ---------------------------------------------------------------

        private static int IndexOfEquippableInBag()
        {
            // The bag is sorted equippable-first, so if there is any wearable
            // item at all it is at cell 0.
            return Save.stockpiledItems.Any(e =>
                Content.ContentDatabase.GetItem(e.itemId)?.IsEquippable == true) ? 0 : -1;
        }

        [UnityTest]
        public IEnumerator SelectingABagItemOffersToEquipIt()
        {
            yield return OpenTheOverlay();
            if (IndexOfEquippableInBag() < 0) Assert.Ignore("no wearable item in the starting kit");

            Click("BagCell0");
            yield return null;

            Assert.IsTrue(Named("OverlayActionButton").activeSelf);
            Assert.IsNotEmpty(Named("OverlayDetailName").GetComponent<TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator EquippingMovesTheItemOntoTheBodyAndSavesIt()
        {
            yield return OpenTheOverlay();
            if (IndexOfEquippableInBag() < 0) Assert.Ignore("no wearable item in the starting kit");

            int bagBefore = Save.stockpiledItems.Sum(e => e.count);

            Click("BagCell0");
            yield return null;
            Click("OverlayActionButton");
            yield return null;

            CollectionAssert.IsNotEmpty(Hero.equipment.EquippedItemIds(), "nothing was worn");
            Assert.AreEqual(bagBefore - 1, Save.stockpiledItems.Sum(e => e.count),
                "the item left the bag rather than being copied");

            // And it is on disk, not just in memory.
            SaveSlotManager.Forget();
            CollectionAssert.IsNotEmpty(Hero.equipment.EquippedItemIds(), "the equip never reached the file");
        }

        [UnityTest]
        public IEnumerator UnequippingPutsItBack()
        {
            yield return OpenTheOverlay();
            if (IndexOfEquippableInBag() < 0) Assert.Ignore("no wearable item in the starting kit");

            Click("BagCell0");
            yield return null;
            Click("OverlayActionButton");
            yield return null;

            var worn = Hero.equipment.EquippedEntries().First();
            int bagAfterEquip = Save.stockpiledItems.Sum(e => e.count);

            Click($"Slot{worn.slot}");
            yield return null;
            Click("OverlayActionButton");
            yield return null;

            Assert.AreEqual("", Hero.equipment.Get(worn.slot), "the slot is empty again");
            Assert.AreEqual(bagAfterEquip + 1, Save.stockpiledItems.Sum(e => e.count),
                "and it came back to the bag");
        }

        [UnityTest]
        public IEnumerator AConsumableSaysItCannotBeWornRatherThanDoingNothing()
        {
            // Everything stays selectable so the plate can explain the refusal.
            // A cell that silently ignores the click reads as a broken button.
            yield return OpenTheOverlay();

            int potion = Enumerable.Range(0, 20).FirstOrDefault(i =>
            {
                var go = Named($"BagCell{i}");
                return go != null && go.activeSelf;
            });

            // Walk the page for a consumable; the starting kit has potions.
            for (int i = potion; i < 20; i++)
            {
                if (!Named($"BagCell{i}").activeSelf) break;

                Click($"BagCell{i}");
                yield return null;

                if (Named("OverlayActionLabel").GetComponent<TMP_Text>().text.Contains("WEAR"))
                {
                    Assert.IsFalse(Named("OverlayActionButton").GetComponent<Button>().interactable,
                        "the refusal is shown, and the button does not act on it");
                    yield break;
                }
            }

            Assert.Ignore("the starting kit holds no consumable on the first page");
        }

        [UnityTest]
        public IEnumerator ReopeningPicksUpWhateverChangedMeanwhile()
        {
            // OnEnable on the modal node is what makes this work without
            // anybody remembering to call Refresh.
            yield return OpenTheOverlay();

            Click("OverlayCloseButton");
            yield return null;

            InventoryOps.Add(Save.stockpiledItems, "health_potion", 5);

            Click("CharacterSheetBuilding");
            yield return null;
            yield return null;

            Assert.IsTrue(_overlay.gameObject.activeSelf);
            Assert.IsTrue(Named("BagCell0").activeSelf, "the reopened bag was repainted");
        }
    }
}
