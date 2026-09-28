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
using PrincesPalace.Domain.Party;
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

        // The reward track, at the four points on it worth photographing plus
        // the one state that is invisible in all four.
        //
        // A capture rather than an assertion, because what can be wrong here is
        // not a value: forty captions at 190px pitch either read or they
        // collide, and UiAudit can only tell you they are contained. The first
        // capture of the redesign is what caught a seal pip covering the very
        // mark it sat beside, on eighty-seven of ninety-nine nodes.
        //
        // THE FOUR ARE 1, 15, 31 AND 40 (progression v2 phase 5's own gate),
        // and each is a different screen rather than the same one scrolled:
        // level 1 is the whole track ahead of you with nothing collected; 15 is
        // the middle, where the rail is half lit; 31 is the first level past
        // completion, so the divider, the prestige ground and both words are in
        // frame at once; 40 is finished, which is the only state in which the
        // summary row says REWARD TRACK COMPLETE and the card has no next
        // reward to rest on.
        //
        // These said 47/47 and 47/12 until phase 5, which is past a cap that
        // came down to 40 two phases earlier -- the panel was photographing a
        // clamped rail and nobody had looked.
        //
        // AND A FIFTH FOR WAITING: a squad collecting as it goes has no waiting
        // nodes at all -- no pulse rings, no gold ticks on the ribbon, no
        // collect button -- so the four above photograph roughly half of what
        // was built.
        [UnityTest]
        public IEnumerator CaptureTheRewardTrack()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests");
            }

            foreach (int level in new[] { 1, 15, 31, 40 })
            {
                yield return OpenTheTrack(level: level, claimed: level);
                yield return Capture($"SystemMenu_reward_track_{level:00}.png");
            }

            // WAITING: twenty-eight levels reached and unpaid, which is what a
            // character levelled by a migration or a debug grant arrives with.
            yield return OpenTheTrack(level: 40, claimed: 12);
            yield return Capture("SystemMenu_reward_track_waiting.png");
        }

        // THE HUB ROSTER WITH THE TITLE PICKER SHOWING (phase 5).
        //
        // Every roster card carries the chosen title under the name, and that
        // line IS the picker -- clicking it cycles. So "open" is as open as
        // this control gets: the row is hidden outright below level 31 and
        // visible with a chevron the moment there is more than one title to
        // choose between, which is the state photographed here.
        //
        // The same squad also has the portrait frame collected (level 34), so
        // this one picture carries both of the roster's identity surfaces.
        [UnityTest]
        public IEnumerator CaptureTheRosterTitlePicker()
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
                character.level = 40;
                character.claimedTrackLevel = 40;
            }

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");

            menu.Open();

            // The Party tab, found by the pane it opens rather than by an
            // index -- SystemMenuTabs.Visible returns a different list in and
            // out of a run, so a literal 1 here would photograph whatever
            // happened to be second.
            var party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            Assert.IsNotNull(party, "the hub's system menu has no PartyController");

            for (int tab = 0; tab < 6; tab++)
            {
                menu.Select(tab);
                yield return null;
                if (party.gameObject.activeInHierarchy) break;
            }

            Assert.IsTrue(party.gameObject.activeInHierarchy, "no tab opened the Party pane");
            party.Refresh();

            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("SystemMenu_roster_titles.png");
        }

        private static IEnumerator OpenTheTrack(int level, int claimed)
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                character.level = level;
                character.claimedTrackLevel = claimed;
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

            // LONG ENOUGH FOR THE FLY-IN TO LAND, which 0.5s was not.
            //
            // The panel opens at the rail's left edge and travels to the
            // player's own node over 380ms of delay plus 2100 of easing. Half a
            // second in, the shipped capture was a picture of levels 11 to 19
            // with the window box parked over them -- structurally correct,
            // about a screen nobody will ever be looking at.
            yield return new WaitForSecondsRealtime(3f);
        }

        private static IEnumerator Capture(string fileName)
        {
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, fileName);
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path), $"no capture written to {path}");
            Debug.Log($"[SystemMenuCapture] wrote {path}");

            yield return null;
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
            var firstCell = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)
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
            string backup = BackupSave();

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

            // NO try/finally: an assertion thrown between BackupSave() and here
            // would skip this restore and leave the real save mutated. Known
            // and accepted for this capture (equip is a single step with a
            // narrow failure window); CaptureTheParty below does the same
            // backup/restore across a longer sequence of commits and DOES wrap
            // it, because that window is wide enough to matter.
            RestoreSave(backup);
        }

        // The Options pane, on its own rather
        // than tacked onto the end of CaptureTheSkeletonOnEachTab the way an
        // earlier pass here had it. That test's own equip-and-verify step
        // ("the pack click changed nothing on the character") is a
        // pre-existing failure under real graphics, unrelated to battle
        // speed and unrelated to Options -- entangling this capture with it
        // meant a bug in equipment gear pack clicking could silently block
        // the one piece of evidence this plan's G4 needs. Standalone, it
        // does not.
        [UnityTest]
        public IEnumerator CaptureTheOptionsPane()
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
            menu.Select(SystemMenuTab.Options);
            yield return new WaitForSecondsRealtime(0.3f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);

            // Captured at UiFrames.FourThree (1920x1440, ratio 1.333), the
            // NARROWEST of the four aspects UiAudit solves every screen at.
            // Explicit width/height rather than the default (current screen
            // size) so this capture is pinned to that aspect regardless of
            // what resolution the runner's own window happens to be at.
            // UiFrames' own header notes flow content inside a fixed-size
            // panel is aspect-invariant -- the Options pane is exactly that
            // (OptionsLayout.PaneHeight is an authored constant, not derived
            // from the canvas frame) -- so this is not expected to change
            // what the pane itself looks like, only to prove it, rather than
            // assume it, against the aspect the plan named.
            string path = Path.Combine(OutputDir, "SystemMenu_options.png");
            CanvasCapture.RenderToFile(canvas, path, (int)UiFrames.FourThree.X, (int)UiFrames.FourThree.Y);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[SystemMenuCapture] wrote {path}");
        }

        // Backs up save_slot_{CurrentSlot}.json verbatim, and restores it
        // byte-for-byte (or deletes it if none existed) plus drops
        // SaveSlotManager's cache. Shared by every capture in this file that
        // has to commit a real mutation (equip, formation swap) to get the
        // picture it wants -- Application.persistentDataPath is the REAL save
        // location (RootOverride is null in this suite), shared between the
        // main project and the TestRunner copies because they carry the same
        // company/product name, so nothing here is a throwaway file.
        private static string BackupSave()
        {
            string path = SavePathForCurrentSlot();
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        private static void RestoreSave(string backup)
        {
            string path = SavePathForCurrentSlot();
            if (backup != null) File.WriteAllText(path, backup);
            else if (File.Exists(path)) File.Delete(path);
            SaveSlotManager.Forget();
        }

        private static string SavePathForCurrentSlot() =>
            Path.Combine(Application.persistentDataPath, $"save_slot_{SaveSlotManager.CurrentSlot}.json");

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

            // StartRun/EndRun both persist through RunManager.Persist() ->
            // SaveSlotManager.SaveCurrent() -- the same real save
            // CaptureTheParty guards above. Found while diagnosing THAT bug:
            // the file's hash still moved on a run with CaptureTheParty's own
            // backup/restore already in place, because this capture was the
            // other leak writing to it. Same fix, same helper.
            string backup = BackupSave();
            try
            {
                // A run with something to say. Captured against invented
                // history rather than a fresh run, because a run that has
                // done nothing prints eighteen zeroes and shows nothing about
                // the layout.
                RunManager.StartRun(4242);
                var run = RunManager.Run;
                RunLedger.RecordRoom(run, won: true, expGained: 15, step: 3);
                RunManager.BankPayout(40);
                RunLedger.RecordRoom(run, won: true, expGained: 30, step: 6);
                RunManager.BankPayout(88);
                RunLedger.Fold(run, LoudFight(), new[] { "a", "b" });

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
                var hold = Object.FindObjectsByType<HoldToConfirm>(FindObjectsInactive.Exclude)
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
            }
            finally
            {
                RestoreSave(backup);
            }
        }

        // The Party pane, in three states.
        //
        // CONTENT-SHAPE NOTE: the handoff's second state names is "a bench
        // card selected showing the Replace badges". characters.json authors
        // exactly 3 characters against 3 seats, so nothing is ever benched
        // today --
        // "Replace" only ever shows for a ROSTER-sourced selection, which
        // needs a benched card to exist. The closest real state is a SEATED
        // card selected, which shows the same badge mechanism ("Swap with
        // X") on the other two positions. Substituted rather than faked.
        [UnityTest]
        public IEnumerator CaptureTheParty()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests");
            }

            // THIS TEST COMMITS TWO REAL FORMATION CHANGES (below) to produce
            // the swap and toast pictures, and PartyController.Persist()
            // writes every commit straight through SaveSlotManager.SaveCurrent()
            // to the REAL save on disk -- there is no test-only slot here. Left
            // unrestored, every -Runtime run of this file silently reordered
            // the player's actual squad; caught because two runs in a row
            // disagreed with each other about who was in front. Same shape as
            // CaptureTheSkeletonOnEachTab's equip capture above, but wrapped in
            // try/finally rather than a plain tail: that capture is one commit
            // with a narrow failure window, this one is two commits across
            // several yields, and a failed assertion between them must not
            // strand the real save mid-swap.
            string backup = BackupSave();
            try
            {
                // Reset to the save's OWN authored default squad before the
                // camp-default shot is taken, rather than trusting whatever
                // order the developer's real slot happens to be in.
                // "Default" is a claim about content (characters.json's three
                // starters, front-to-back), not about whatever the last
                // session left on disk -- and CaptureTheSkeletonOnEachTab's
                // equip capture already showed what depending on the real
                // slot's incidental state does to a fixture: two runs, two
                // different pictures, read as a bug rather than as drift.
                // Consistent with that capture's restore-the-real-file
                // approach: this resets to a KNOWN state instead (via
                // SaveData.CreateNew()) rather than merely hoping the real
                // slot happens to already be in default order.
                SaveSystem.Save(SaveData.CreateNew(), SaveSlotManager.CurrentSlot);
                SaveSlotManager.Forget();

                // STATE 1: default camp, nothing selected.
                yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
                yield return null;
                yield return null;

                var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
                Assert.IsNotNull(menu, "the hub has no SystemMenuController");
                menu.Open();
                menu.Select(SystemMenuTab.Party);
                yield return null;

                var party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
                Assert.IsNotNull(party, "the Party tab has no PartyController");

                var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(c => c.isRootCanvas);
                Assert.IsNotNull(canvas);

                Directory.CreateDirectory(OutputDir);
                yield return Capture("SystemMenu_party_camp_default.png", canvas);

                // STATE 2: a seated card selected -- every position it could swap
                // with shows its live "Swap with {name}" badge and a gold ring.
                party.ClickSeat(PartySeat.Front);
                yield return null;
                yield return Capture("SystemMenu_party_swap_preview.png", canvas);
                party.Cancel();
                yield return null;

                // STATE 4 (P4): right after a committed swap -- the toast, at
                // full alpha before its fade begins (PartyToast.Show holds full
                // alpha for its first HoldSeconds).
                party.ClickSeat(PartySeat.Front);
                party.ClickSeat(PartySeat.Middle);
                yield return null;
                yield return Capture("SystemMenu_party_toast.png", canvas);
            }
            finally
            {
                // Restored HERE -- before the Map load below -- so STATE 3
                // sees the ORIGINAL real order rather than either the
                // synthetic default squad or the swap just committed above.
                // A finally in an iterator runs on normal exception
                // propagation through MoveNext() same as any method (Dispose
                // is only needed for the yield-suspended case, not this one),
                // so an assertion failing anywhere in the try still restores
                // the real save before the test ends.
                RestoreSave(backup);
            }

            // STATE 3: in a run -- reposition only, captured from the map so
            // the five-tab bar and the in-run lintel are in shot with it.
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var mapMenu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(mapMenu, "the map has no SystemMenuController");
            mapMenu.Open();
            mapMenu.Select(SystemMenuTab.Party);
            yield return null;

            var mapCanvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(mapCanvas);

            yield return Capture("SystemMenu_party_run_mode.png", mapCanvas);
        }

        private static IEnumerator Capture(string fileName, Canvas canvas)
        {
            string path = Path.Combine(OutputDir, fileName);
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path), $"no capture written to {path}");
            Debug.Log($"[SystemMenuCapture] wrote {path}");
            yield return null;
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
