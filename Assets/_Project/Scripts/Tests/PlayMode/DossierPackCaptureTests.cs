using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.PlayModeTests
{
    // Captures the pack's hover comparison, which no static capture can reach.
    //
    // The Reckoning already had one of these (ReckoningCaptureTests) and the
    // pack is the OTHER place a comparison appears -- same question, different
    // surface: there the player is choosing between three things they do not
    // own, here between a thing they own and a thing they are wearing. The
    // tooltip is built by CharacterDossierController.OnPackHover, lives inside
    // an overlay panel that starts inactive, and is positioned relative to the
    // cell being hovered, so ScreenshotTool's Edit Mode render cannot produce
    // it at all.
    //
    // Graphics device only: tools/screenshot.ps1 -Runtime -RuntimeFilter
    // DossierPackCaptureTests.
    public class DossierPackCaptureTests
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
            _root = Path.Combine(Path.GetTempPath(), "pp-pack-" + System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator CaptureThePackHoverComparison()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter DossierPackCaptureTests");
            }

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

            // A MATCHED PAIR, one worn and one carried, because a comparison
            // against an empty slot is not the picture: every line would read as
            // a gain and the "vs. equipped" half of the tooltip would have
            // nothing to say. Same paperdoll slot, as far apart in tier as the
            // catalogue allows, and both with art -- a cell drawing a blank
            // plate photographs as a bug.
            var byslot = ContentDatabase.Equippables
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.iconPath))
                .GroupBy(i => i.equipSlot)
                .Select(g => g.OrderBy(i => i.tier).ToList())
                .Where(g => g.Count >= 2 && g.Last().tier > g.First().tier)
                .OrderByDescending(g => g.Last().tier - g.First().tier)
                .FirstOrDefault();

            Assert.IsNotNull(byslot,
                "no paperdoll slot has two items of different tiers WITH art, so there is no " +
                "upgrade to photograph");

            var worn = byslot.First();
            var carried = byslot.Last();
            Debug.Log($"[PackCapture] wearing {worn.id} (tier {worn.tier}), " +
                      $"hovering {carried.id} (tier {carried.tier})");

            var save = SaveSlotManager.CurrentSave;
            var character = save?.ActiveSquad()?.FirstOrDefault(c => c != null);
            Assert.IsNotNull(character?.equipment, "no squad member with a loadout to dress");

            character.equipment.Set(character.equipment.ResolveTargetSlot(worn.equipSlot), worn.id);

            // EMPTIED FIRST, so the candidate is cell 0. BagView sorts the pack
            // and the sort is not this test's business to predict; one item in
            // the bag makes the question moot.
            save.stockpiledItems.Clear();
            InventoryOps.Add(save.stockpiledItems, carried.id);
            SaveSlotManager.SaveCurrent();

            dossier.ShowPack(true);
            dossier.Refresh();
            yield return null;

            var cell = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == "DossierPackCell0");
            Assert.IsNotNull(cell, "the pack drew no cell named DossierPackCell0");

            var hover = cell.GetComponent<HoverIndex>();
            Assert.IsNotNull(hover,
                "DossierPackCell0 carries no HoverIndex, so nothing can open the comparison");

            // Driven through the component rather than by faking a pointer: the
            // capture has no real cursor, same as the Reckoning's.
            hover.OnPointerEnter(null);

            // Real seconds. The tooltip and the stat-row preview both animate on
            // unscaled time, which a batchmode frame count wildly under-waits --
            // ReckoningCaptureTests records the capture that came out mid-wipe.
            yield return new WaitForSecondsRealtime(1.0f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "Pack_hover.png");
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[PackCapture] wrote {path}");
        }
    }
}
