using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // Phase 3a of docs/GAMEPAD_NAVIGATION_PLAN.md, screen 5: Shop, wired as a
    // MODAL context (pushed on Open(), popped on Leave()/OnDisable, not a
    // whole-scene base context) -- ShopController.RefreshShelfNavigation /
    // RefreshPackNavigation's own headers cover the shape. The seed-search
    // helper and OpenShop() path are ShopScreenRefusalTests' own, reused
    // rather than re-derived.
    public class ShopGamepadNavigationTests
    {
        private string _root;
        private ScriptedBaseInput _input;
        private ShopController _shop;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-shop-nav-" + System.Guid.NewGuid().ToString("N"));
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
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static ulong SeedWithAShopInTheFirstColumn()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = DescentMapGenerator.GenerateLeg(new SeededRandom(seed), 0);
                if (map.AtDepth(1).Any(n => n.Type == RoomType.Shop)) return seed;
            }

            throw new AssertionException("No seed under 4000 generated a shop in the first column.");
        }

        private IEnumerator OpenTheShop()
        {
            RunManager.StartRun(SeedWithAShopInTheFirstColumn());
            var node = RunManager.Map.AtDepth(1).First(n => n.Type == RoomType.Shop);
            RunOrchestrator.ArriveAt(node);

            // Plenty of gold: the navigation tests are about where Move and
            // Submit land, not about affordability -- ShopScreenRefusalTests
            // already owns the refusal-wording coverage.
            RunManager.Run.gold = 9999;
            SaveSlotManager.SaveCurrent();

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the map scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            yield return null;
            yield return null;

            var map = Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            Assert.IsNotNull(map, "the Map scene has no MapController");
            map.OpenShop();
            yield return null;

            _shop = Object.FindAnyObjectByType<ShopController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_shop, "the map scene has no ShopController");
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
        }

        private static GameObject Node(string name) => GameObject.Find(name);

        [UnityTest]
        public IEnumerator EntryIsTheFirstGearCard()
        {
            yield return OpenTheShop();

            Assert.AreEqual(Node("ShopGearCard0"), EventSystem.current.currentSelectedGameObject,
                "the buy grid (Gear, the only real 2-wide grid on this screen) is the stated primary " +
                "surface -- entry is its first card");
        }

        [UnityTest]
        public IEnumerator Right_AcrossTheGearGrid_StepsWithinTheTopRow()
        {
            yield return OpenTheShop();
            EventSystem.current.SetSelectedGameObject(Node("ShopGearCard0"));
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("ShopGearCard1"), EventSystem.current.currentSelectedGameObject,
                "Gear is a 2-column Grid -- Right from column 0 should reach column 1, same row");
        }

        [UnityTest]
        public IEnumerator Down_AcrossTheGearGrid_StepsToTheSecondRow()
        {
            yield return OpenTheShop();
            EventSystem.current.SetSelectedGameObject(Node("ShopGearCard0"));
            yield return null;

            yield return Move(0f, -1f);

            Assert.AreEqual(Node("ShopGearCard2"), EventSystem.current.currentSelectedGameObject,
                "Down should step a whole Grid row, never diagonally");
        }

        [UnityTest]
        public IEnumerator Right_FromRelicShelf_ReachesBookShelf()
        {
            yield return OpenTheShop();
            EventSystem.current.SetSelectedGameObject(Node("ShopRelicCard0"));
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("ShopBookCard0"), EventSystem.current.currentSelectedGameObject,
                "Relic and Book are two single-column Lists side by side, joined row for row");
        }

        [UnityTest]
        public IEnumerator Right_FromGearsRightColumn_ReachesTheActionsList()
        {
            yield return OpenTheShop();
            EventSystem.current.SetSelectedGameObject(Node("ShopGearCard1"));
            yield return null;

            yield return Move(1f, 0f);

            Assert.AreEqual(Node("ShopBuyButton"), EventSystem.current.currentSelectedGameObject,
                "Gear's right column should reach the actions list (Buy/Pack/Leave), entry ShopBuyButton");
        }

        [UnityTest]
        public IEnumerator Down_FromTheActionsListEntry_StepsToPack()
        {
            yield return OpenTheShop();
            EventSystem.current.SetSelectedGameObject(Node("ShopBuyButton"));
            yield return null;

            yield return Move(0f, -1f);

            Assert.AreEqual(Node("ShopPackButton"), EventSystem.current.currentSelectedGameObject,
                "the actions column is a List -- Down from Buy should reach Pack");
        }

        // The required action for the shelf: Submit twice (select, then Buy)
        // should commit a real purchase -- the same two-press path
        // ShopScreenRefusalTests already drives by mouse, now through the
        // real dispatcher.
        [UnityTest]
        public IEnumerator Submit_SelectThenBuy_PurchasesTheCard_ExactlyOnce()
        {
            yield return OpenTheShop();

            var card = RunOrchestrator.CurrentShopStock
                .FirstOrDefault(e => e != null && e.section == ShopStock.GearSection
                                     && !e.noOffer && !e.sold && e.price > 0);
            Assert.IsNotNull(card, "the shop rolled no priced gear card to buy");

            int goldBefore = RunManager.Run.gold;

            EventSystem.current.SetSelectedGameObject(Node($"ShopGearCard{card.index}"));
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();

            EventSystem.current.SetSelectedGameObject(Node("ShopBuyButton"));
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.AreEqual(goldBefore - card.price, RunManager.Run.gold,
                "Submit on the card then Submit on ShopBuyButton should purchase it exactly once");
        }

        // Pack surface: sell buttons (disable-on-ineligible, PaintPack's own
        // `packSellOneButtons[i].interactable = sellPrice > 0`) reached via
        // explicit links, entry on the first row.
        [UnityTest]
        public IEnumerator PackOpen_EntryIsTheFirstSellButton()
        {
            yield return OpenTheShop();

            var save = SaveSlotManager.CurrentSave;
            save.stockpiledItems.Add(new InventoryEntry(ContentDatabase.Items.First(i => i != null).id, 1));

            EventSystem.current.SetSelectedGameObject(Node("ShopPackButton"));
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();
            yield return null; // reselection onto the pack's own entry

            Assert.AreEqual(Node("ShopPackRow0SellOne"), EventSystem.current.currentSelectedGameObject,
                "opening the pack should move the entry onto the first row's sell button");
        }

        [UnityTest]
        public IEnumerator CancelInsideThePack_ClosesItAndRestoresTheShelf()
        {
            yield return OpenTheShop();

            EventSystem.current.SetSelectedGameObject(Node("ShopPackButton"));
            yield return null;
            _input.SubmitDown = true;
            yield return DriveFrame();

            var packRoot = GameObject.Find("ShopPackRoot") ?? Node("ShopPackClose")?.transform.parent?.gameObject;

            _input.CancelDown = true;
            yield return DriveFrame();
            yield return null;

            Assert.AreEqual(Node("ShopGearCard0"), EventSystem.current.currentSelectedGameObject,
                "Cancel inside the pack should close it and restore the shelf's own entry");
        }

        [UnityTest]
        public IEnumerator CancelOnTheShelf_ArmsThenConfirmsLeave()
        {
            yield return OpenTheShop();

            Assert.IsTrue(_shop.gameObject.activeSelf, "the shop should be open before either Cancel");

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsTrue(_shop.gameObject.activeSelf,
                "the first Cancel should only ARM leaving, the same first press ShopLeaveButton itself needs");

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_shop.gameObject.activeSelf,
                "the second Cancel should confirm leaving, the same path ShopLeaveButton's own second press takes");
        }
    }
}
