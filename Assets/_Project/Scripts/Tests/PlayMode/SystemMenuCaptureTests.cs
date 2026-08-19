using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.PlayModeTests
{
    // Captures the overarching menu open, which no static capture can reach --
    // it is an inactive modal inside another scene's tree, the same situation
    // AUDIT #43 records for the Reckoning.
    //
    // Graphics device only: tools/screenshot.ps1 -Runtime -RuntimeFilter
    // SystemMenuCaptureTests.
    public class SystemMenuCaptureTests
    {
        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [UnityTest]
        public IEnumerator CaptureTheSkeletonOnEachTab()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController - the menu was never embedded");

            menu.Open();
            menu.Select(0);   // Character, which is the pane the dossier lives in
            yield return new WaitForSecondsRealtime(0.5f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "SystemMenu_skeleton.png");
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[SystemMenuCapture] wrote {path}");

            // And the pack open, over column A, which is the other half of the
            // Character pane and the state the Inventory tab lands on.
            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the Character pane has no dossier controller");
            dossier.ShowPack(true);
            yield return new WaitForSecondsRealtime(0.3f);

            string packPath = Path.Combine(OutputDir, "SystemMenu_pack.png");
            CanvasCapture.RenderToFile(canvas, packPath);
            Assert.IsTrue(File.Exists(packPath));
            Debug.Log($"[SystemMenuCapture] wrote {packPath}");

            // Hovering a pack item, which is the state that has to be LOOKED at
            // rather than asserted: the tooltip and the would-be stat values
            // appear together, and whether they collide or read as one thing is
            // not something a count of active objects can answer.
            //
            // Driven through ExecuteEvents rather than by calling the handler,
            // so the HoverIndex component attached at runtime is exercised too
            // -- a hover that works only when invoked directly is not a hover.
            // Found by the name the screen emits rather than through the
            // controller's fields, which are internal to the runtime assembly.
            // Widening them so a screenshot could reach them would be the tail
            // wagging the dog; the emitted name is already a stable contract,
            // because UiAudit fails the build on a duplicate one.
            var firstCell = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(b => b.name == "DossierPackCell0");
            Assert.IsNotNull(firstCell, "the pack drew no cell named DossierPackCell0");

            ExecuteEvents.Execute(firstCell.gameObject, new PointerEventData(EventSystem.current),
                                  ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(0.3f);

            string hoverPath = Path.Combine(OutputDir, "SystemMenu_pack_hover.png");
            CanvasCapture.RenderToFile(canvas, hoverPath);
            Assert.IsTrue(File.Exists(hoverPath));
            Debug.Log($"[SystemMenuCapture] wrote {hoverPath}");

            // And the equip itself. Clicking the cell is the whole gesture, so
            // the shot after it is the proof that the item moved onto the
            // mannequin and out of the pack rather than merely being previewed.
            //
            // Equipping WRITES THE SAVE, which makes this the one capture that
            // can change what the next run sees. It already did: two runs in a
            // row produced different weapons in hand and a stat going up in one
            // shot and down in the next, which reads as a bug in the preview
            // rather than as drift in the fixture. So the file is put back
            // exactly as it was, and the manager's cache dropped with it.
            string savePath = Path.Combine(Application.persistentDataPath,
                                           $"save_slot_{SaveSlotManager.CurrentSlot}.json");
            string backup = File.Exists(savePath) ? File.ReadAllText(savePath) : null;

            var whoever = SaveSlotManager.CurrentSave?.ActiveSquad()?.FirstOrDefault(c => c != null);
            var wornBefore = whoever?.equipment == null ? null
                : EquipmentSlots.All.Select(sl => whoever.equipment.Get(sl)).ToArray();

            firstCell.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);

            // A capture of an unchanged screen is indistinguishable from a
            // capture of a click that was ignored, so the shot is not allowed to
            // stand on its own.
            if (wornBefore != null)
            {
                var wornAfter = EquipmentSlots.All.Select(sl => whoever.equipment.Get(sl)).ToArray();
                Debug.Log("[SystemMenuCapture] equip changed: " + string.Join(", ",
                    EquipmentSlots.All.Select((sl, i) => new { sl, i })
                        .Where(x => wornBefore[x.i] != wornAfter[x.i])
                        .Select(x => $"{x.sl}: '{wornBefore[x.i]}' -> '{wornAfter[x.i]}'")));

                Assert.IsFalse(wornBefore.SequenceEqual(wornAfter),
                    "the pack click changed nothing on the character: " +
                    string.Join(", ", EquipmentSlots.All.Select((sl, i) => $"{sl}='{wornBefore[i]}'")));
            }

            string equipPath = Path.Combine(OutputDir, "SystemMenu_pack_equipped.png");
            CanvasCapture.RenderToFile(canvas, equipPath);
            Assert.IsTrue(File.Exists(equipPath));
            Debug.Log($"[SystemMenuCapture] wrote {equipPath}");

            if (backup != null) File.WriteAllText(savePath, backup);
            else if (File.Exists(savePath)) File.Delete(savePath);
            SaveSlotManager.Forget();
        }
    }
}
