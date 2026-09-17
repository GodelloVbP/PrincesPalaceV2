using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.PlayModeTests
{
    // THE PICTURE FOR THE OWNER, AND THE ASSERTION FOR THE SUITE
    // (docs/GAMEPAD_NAVIGATION_PLAN.md phase 3b item 1's own gate: "verified
    // by a runtime capture, not just asserted").
    //
    // Job 1's placement rule is that a focus-driven tooltip must never cover
    // the thing it describes -- and unlike a hover tooltip, there is no cursor
    // to move off it, so a box that lands on its own subject stays there. The
    // arithmetic is pinned with literals in TooltipPlacementTests; what THAT
    // cannot prove is that the rects fed to it are the rects on screen. So
    // each of the four extreme cells in the pack window is selected in turn,
    // the real placed box is measured against the real cell in WORLD space,
    // and the frame is photographed for the owner to look at.
    //
    // Four cells, not one: the corners are where the flip and the clamp
    // actually fire, and the middle of a grid is the one sample that cannot
    // fail.
    //
    // Graphics device only: tools/screenshot.ps1 -Runtime -RuntimeFilter
    // DossierTooltipCaptureTests.
    public class DossierTooltipCaptureTests
    {
        private string _root;

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        // Its own save root, for the reason DossierEquipTests records: without
        // it the pack holds whatever save the runner happened to be carrying,
        // and the shot is of a different bag every run.
        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-tip-" + System.Guid.NewGuid().ToString("N"));
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

        private static GameObject Node(string name) =>
            Resources.FindObjectsOfTypeAll<RectTransform>()
                .FirstOrDefault(r => r.name == name && r.gameObject.scene.IsValid())
                ?.gameObject;

        // In WORLD space and off the drawn corners, which is the whole point
        // of doing this here rather than in the EditMode placement test: it
        // measures what the canvas actually put on screen, through whatever
        // parents, pivots and scales sit between the two rects.
        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return new Rect(corners[0].x, corners[0].y,
                corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

        [UnityTest]
        public IEnumerator CaptureTheTooltipAtEachEdgeOfThePack()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter DossierTooltipCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the hub scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");
            menu.Open();
            menu.Select(0);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the Character pane has no dossier controller");

            // SIX ITEMS, one per cell of the window, so all four extreme
            // cells are bound: an unbound cell is not in the nav group and
            // nothing would select it.
            var stock = ContentDatabase.Equippables
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.iconPath))
                .OrderByDescending(i => i.tier)
                .Take(6)
                .ToList();

            Assert.AreEqual(6, stock.Count, "the catalogue has fewer than six equippable items with art");

            var save = SaveSlotManager.CurrentSave;
            save.stockpiledItems.Clear();
            foreach (var item in stock) InventoryOps.Add(save.stockpiledItems, item.id);
            SaveSlotManager.SaveCurrent();

            dossier.ShowPack(true);
            dossier.Refresh();
            yield return null;

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);

            // The window is two abreast and three deep (DossierLayout.Pack
            // Columns / PackVisibleRows), so these four cells are its corners:
            // top-left, top-right, bottom-left, bottom-right.
            var corners = new (int Cell, string Name)[]
            {
                (0, "topleft"),
                (1, "topright"),
                (4, "bottomleft"),
                (5, "bottomright"),
            };

            var tooltip = Node("DossierTooltip");
            Assert.IsNotNull(tooltip, "the dossier drew no tooltip");
            var tooltipRect = (RectTransform)tooltip.transform;

            foreach (var corner in corners)
            {
                var cell = Node($"DossierPackCell{corner.Cell}");
                Assert.IsNotNull(cell, $"the pack drew no cell named DossierPackCell{corner.Cell}");

                // THE SELECTION, not a pointer: this is the path with no
                // cursor to anchor to, which is the one job 1 exists for.
                EventSystem.current.SetSelectedGameObject(cell);

                // Real seconds. The box and the stat-row preview both settle
                // on unscaled time, which a batchmode frame count wildly
                // under-waits -- ReckoningCaptureTests records the capture
                // that came out mid-wipe.
                yield return new WaitForSecondsRealtime(0.6f);

                Assert.IsTrue(tooltip.activeSelf,
                    $"selecting DossierPackCell{corner.Cell} did not open the tooltip");

                var box = WorldRect(tooltipRect);
                var subject = WorldRect((RectTransform)cell.transform);

                Assert.IsFalse(box.Overlaps(subject),
                    $"the tooltip for the {corner.Name} cell covers the cell it describes: " +
                    $"box {box}, cell {subject}");

                string path = Path.Combine(OutputDir, $"Dossier_tooltip_{corner.Name}.png");
                CanvasCapture.RenderToFile(canvas, path);
                Assert.IsTrue(File.Exists(path));
                Debug.Log($"[TooltipCapture] wrote {path} -- box {box}, cell {subject}, overlap {box.Overlaps(subject)}");
            }
        }
    }
}
