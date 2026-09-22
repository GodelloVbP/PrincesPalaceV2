using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // THE ONLY CAMERA THAT CAN SEE THE SHOP.
    //
    // The shop is a panel nested inside the Map screen and it starts inactive,
    // so ScreenshotTool's Edit-Mode render -- which photographs a scene at
    // rest, by registered panel name -- cannot reach it at all, exactly as it
    // cannot reach the relic draft. Worse, the shop at rest is empty: every
    // name, price and icon on it is written by ShopController.Paint at Open(),
    // so a still of the unpainted tree would be a grid of blank boxes.
    //
    // That gap is why the screen shipped once looking nothing like its own
    // design handoff without anyone seeing it. This is the check that would
    // have caught it, and the one to re-run after touching ShopScreen:
    //
    //   tools/screenshot.ps1 -Runtime -RuntimeFilter ShopCaptureTests
    //
    // Compare the output against docs/handoffs/shop_v2/screenshots/01-shop.png
    // and pack.png, which are what it is supposed to look like.
    public class ShopCaptureTests
    {
        private string _root;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-shop-shot-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // Lifted from ShopArrivalTests: the generator is pure and reads no
        // content, so hunting a seed whose first column offers a shop costs
        // nothing and needs no save writes.
        private static ulong SeedWithAShopInTheFirstColumn()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = DescentMapGenerator.GenerateLeg(new SeededRandom(seed), 0);
                if (map.AtDepth(1).Any(n => n.Type == RoomType.Shop)) return seed;
            }

            throw new AssertionException("No seed under 4000 generated a shop in the first column.");
        }

        [UnityTest]
        public IEnumerator CaptureTheShopAndItsPack()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter ShopCaptureTests");
            }

            ulong seed = SeedWithAShopInTheFirstColumn();
            RunManager.StartRun(seed);

            var node = RunManager.Map.AtDepth(1).First(n => n.Type == RoomType.Shop);
            RunOrchestrator.ArriveAt(node);

            // A GENEROUS PURSE, so the shot shows the ordinary state of a card
            // rather than four NEED chips. The unaffordable state has its own
            // reference render (02-edge-states.png) and is not what this
            // capture is for.
            RunManager.Run.gold = 2000;

            // Three things in the bag, so the PACK modal photographs with rows
            // in it. Sell prices come off real items, so an empty catalogue
            // fails here loudly rather than producing a picture of nothing.
            var save = SaveSlotManager.CurrentSave;
            var sellable = ContentDatabase.Items.Where(i => i != null).Take(3).ToList();
            Assert.IsNotEmpty(sellable, "no items in the catalogue to put in the bag");
            save.stockpiledItems.Clear();
            foreach (var item in sellable) InventoryOps.Add(save.stockpiledItems, item.id);
            SaveSlotManager.SaveCurrent();

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var map = Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            Assert.IsNotNull(map, "the Map scene has no MapController");
            map.OpenShop();
            yield return null;

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, "no root canvas in the Map scene");

            Directory.CreateDirectory(OutputDir);

            string shopPath = Path.Combine(OutputDir, "Shop.png");
            CanvasCapture.RenderToFile(canvas, shopPath, 1920, 1080);
            Assert.IsTrue(File.Exists(shopPath));
            Debug.Log($"[ShopCapture] wrote {shopPath}");

            // The PACK modal, driven through its own button rather than a
            // private method -- the same thing a player's click reaches.
            var pack = Object.FindObjectsByType<Button>(FindObjectsInactive.Include)
                .FirstOrDefault(b => b.name == "ShopPackButton");
            Assert.IsNotNull(pack, "the shop drew no button named ShopPackButton");
            pack.onClick.Invoke();
            yield return null;

            string packPath = Path.Combine(OutputDir, "Shop_pack.png");
            CanvasCapture.RenderToFile(canvas, packPath, 1920, 1080);
            Assert.IsTrue(File.Exists(packPath));
            Debug.Log($"[ShopCapture] wrote {packPath}");
        }

        // THE COMPARISON PANEL (owner ask #3, 2026-09-22): a gear card
        // selected, so the affix names on the card (owner ask #2) and the
        // comparison panel's stats/affix-lines/VS.-equipped delta
        // (ItemDescription.ComparisonBody, via ItemComparisonPanel) are both
        // in the same frame. One click, through the same button a player's
        // click reaches -- ShopScreenRefusalTests' own pattern.
        [UnityTest]
        public IEnumerator CaptureTheShopWithAGearCardSelected()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter ShopCaptureTests");
            }

            ulong seed = SeedWithAShopInTheFirstColumn();
            RunManager.StartRun(seed);

            var node = RunManager.Map.AtDepth(1).First(n => n.Type == RoomType.Shop);
            RunOrchestrator.ArriveAt(node);

            // A GENEROUS PURSE, same reason CaptureTheShopAndItsPack's own
            // comment gives: the ordinary state, not four NEED chips.
            RunManager.Run.gold = 2000;
            SaveSlotManager.SaveCurrent();

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var map = Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            Assert.IsNotNull(map, "the Map scene has no MapController");
            map.OpenShop();
            yield return null;

            // Prefers a card that actually rolled an affix, so the shot also
            // proves ModifierAffixLines' named, numbered lines render inside
            // the panel -- not just the plain stat delta every gear card has.
            var gearCards = RunOrchestrator.CurrentShopStock
                .Where(e => e != null && e.section == ShopStock.GearSection && !e.noOffer && !e.sold)
                .ToList();
            var card = gearCards.FirstOrDefault(e => e.modifiers != null && e.modifiers.Count > 0)
                ?? gearCards.FirstOrDefault();
            Assert.IsNotNull(card, "the shop rolled no real gear card to select");

            var button = Object.FindObjectsByType<Button>(FindObjectsInactive.Include)
                .FirstOrDefault(b => b.name == $"ShopGearCard{card.index}");
            Assert.IsNotNull(button, $"the shop drew no button named ShopGearCard{card.index}");
            button.onClick.Invoke();
            yield return null;

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, "no root canvas in the Map scene");

            Directory.CreateDirectory(OutputDir);

            string path = Path.Combine(OutputDir, "Shop_gear_selected.png");
            CanvasCapture.RenderToFile(canvas, path, 1920, 1080);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[ShopCapture] wrote {path}");
        }
    }
}
