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
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

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

        // The reward track, opened over the dossier and levelled enough that
        // the rail has a lit half and a "you are here" node to scroll to.
        //
        // A capture rather than an assertion, because what can be wrong here is
        // not a value: a hundred captions at 190px pitch either read or they
        // collide, and UiAudit can only tell you they are contained.
        [UnityTest]
        public IEnumerator CaptureTheRewardTrack()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                character.level = 47;
            }

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");

            menu.Open();
            menu.Select(0);
            yield return null;

            var row = menu.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .FirstOrDefault(b => b.name == "DossierTrackRow");
            Assert.IsNotNull(row, "the dossier has no reward-track row");
            row.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.5f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "SystemMenu_reward_track.png");
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path), $"no capture written to {path}");
            Debug.Log($"[SystemMenuCapture] wrote {path}");
        }

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

            // And the Options pane, which is the other half of what this menu
            // now carries.
            menu.Select(SystemMenuTab.Options);
            yield return new WaitForSecondsRealtime(0.3f);

            string optionsPath = Path.Combine(OutputDir, "SystemMenu_options.png");
            CanvasCapture.RenderToFile(canvas, optionsPath);
            Assert.IsTrue(File.Exists(optionsPath));
            Debug.Log($"[SystemMenuCapture] wrote {optionsPath}");

            if (backup != null) File.WriteAllText(savePath, backup);
            else if (File.Exists(savePath)) File.Delete(savePath);
            SaveSlotManager.Forget();
        }

        // The two panes that only exist mid-run, captured FROM the map so the
        // five-tab bar and the in-run lintel are in shot with them.
        //
        // Run statistics is the one capture on this menu that cannot be judged
        // any other way: every assertion about it is about a figure, and the
        // question a screenshot answers -- whether three cards of four to eight
        // rows read as a set or as an emptier pane than it should be -- is not
        // one a count of active objects can reach.
        [UnityTest]
        public IEnumerator CaptureTheRunOnlyPanes()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests");
            }

            // A run with something to say. Captured against invented history
            // rather than a fresh run, because a run that has done nothing
            // prints eighteen zeroes and shows nothing about the layout.
            RunManager.StartRun(4242);
            var run = RunManager.Run;
            RunLedger.RecordRoom(run, won: true, goldGained: 40, expGained: 15, step: 3);
            RunLedger.RecordRoom(run, won: true, goldGained: 88, expGained: 30, step: 6);
            RunLedger.Fold(run, LoudFight());

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the Map scene has no SystemMenuController");

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);

            menu.Open();
            menu.Select(SystemMenuTab.RunStats);
            yield return new WaitForSecondsRealtime(0.4f);

            string statsPath = Path.Combine(OutputDir, "SystemMenu_run_stats.png");
            CanvasCapture.RenderToFile(canvas, statsPath);
            Assert.IsTrue(File.Exists(statsPath));
            Debug.Log($"[SystemMenuCapture] wrote {statsPath}");

            // And the exits, with abandon showing -- which it only does in a
            // descent, so this scene is the only place the card can be seen.
            menu.Select(SystemMenuTab.MainMenu);
            yield return new WaitForSecondsRealtime(0.4f);

            string exitsPath = Path.Combine(OutputDir, "SystemMenu_main_menu.png");
            CanvasCapture.RenderToFile(canvas, exitsPath);
            Assert.IsTrue(File.Exists(exitsPath));
            Debug.Log($"[SystemMenuCapture] wrote {exitsPath}");

            // Half way through the hold, which is the state the design actually
            // specified -- a fill drawn in the button -- and the one thing about
            // this pane that a still of it at rest does not show.
            var hold = Object.FindObjectsByType<HoldToConfirm>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault();
            Assert.IsNotNull(hold, "the abandon button has no hold behaviour attached");

            hold.Begin();
            hold.Advance(hold.Seconds * 0.55f);
            yield return null;

            string holdPath = Path.Combine(OutputDir, "SystemMenu_abandon_hold.png");
            CanvasCapture.RenderToFile(canvas, holdPath);
            Assert.IsTrue(File.Exists(holdPath));
            Debug.Log($"[SystemMenuCapture] wrote {holdPath}");

            // Let go before the capture's own hold ends the run it is standing
            // in. Advance() fires Completed at the top, so leaving this out
            // would abandon the descent and navigate mid-capture.
            hold.Cancel();
            RunManager.EndRun();
            SaveSlotManager.Forget();
        }

        // One fight's worth of numbers, invented. A CombatLedger rather than
        // RunLedgerEntry directly, because Fold is the seam the game uses and a
        // capture that hand-wrote the run's ledger would be showing a shape the
        // game never produces.
        private static CombatLedger LoudFight()
        {
            var fight = new CombatLedger();
            fight.Dealt("a", DamageType.Physical, 1840);
            fight.Dealt("a", DamageType.Fire, 620);
            fight.Dealt("b", DamageType.Physical, 940);
            fight.Took("a", 410);
            fight.Took("b", 265, shielded: 90);
            fight.Restored("b", 180);
            fight.ScoredKill("a");
            fight.ScoredKill("a");
            fight.ScoredKill("b");
            fight.WentDown("b");
            return fight;
        }
    }
}
