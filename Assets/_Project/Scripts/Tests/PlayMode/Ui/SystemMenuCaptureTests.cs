using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Content;
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

        // THE GLOSSARY, on its Statuses category with an entry focused: the rail
        // with counts, a page of eight rows with the pager, and the detail with
        // its badge and the fight's own sentence. Reached by the deep link a
        // status in the fight will use, so the picture is also the proof that
        // the link pages to the entry and focuses its row.
        //
        // A capture because what can be wrong is not a value: whether the
        // three columns read as a set, and whether a two-line row, a 28 point
        // name and an eight-line body all fit their boxes, is a question for
        // eyes.
        [UnityTest]
        public IEnumerator CaptureTheGlossaryOnTheStatuses()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");

            // Poison sits on the second page, so the pager and a turned page are
            // both in shot.
            menu.Open(SystemMenuTab.Glossary, MenuTarget.Glossary("poison"));
            yield return null;
            yield return null;

            yield return new WaitForSecondsRealtime(0.4f);
            yield return Capture("SystemMenu_glossary.png");
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
                .FirstOrDefault(b => b.name == "DossierSection3");
            Assert.IsNotNull(row, "the dossier's rail has no REWARD TRACK row");
            row.onClick.Invoke();

            // LONG ENOUGH FOR THE FLY-IN TO LAND, which 0.5s was not.
            //
            // The section opens at the rail's left edge and travels to the
            // player's own node over 380ms of delay plus 2100 of easing; half a
            // second in, the capture is of a screen nobody will look at.
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

        // THE CHARACTER TAB: the frame with LOADOUT showing, then LOADOUT with
        // gear worn, a pack holding gear and a book and a pack item in the
        // compare column, then the same with an empty slot ARMED (plan C5),
        // then the equip itself -- and ATTRIBUTES and SKILLS.
        //
        // The fixture is this test's own: the character on screen, a worn item
        // and a different pack item for the same slot, so the equip at the end
        // is a real change and the capture after it cannot be a picture of a
        // click that was ignored. Equipping writes the save, so the file is
        // put back as it was, and the run this starts for the book is ended.
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

            menu.Open(SystemMenuTab.Character);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("SystemMenu_skeleton.png");

            string backup = BackupSave();
            try
            {
                var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
                Assert.IsNotNull(dossier, "the Character pane has no dossier controller");

                var save = SaveSlotManager.CurrentSave;
                var character = save.roster.First(c => c.definitionId == dossier.CharacterId);

                // Two items for one slot, both with art: one worn, one packed.
                var art = ContentDatabase.Equippables.Where(i => i != null && !string.IsNullOrWhiteSpace(i.iconPath)).ToList();
                var slotWithTwo = art.GroupBy(i => i.equipSlot).First(g => g.Count() >= 2);
                var worn = slotWithTwo.OrderBy(i => i.tier).First();
                var better = slotWithTwo.OrderByDescending(i => i.tier).First(i => i.id != worn.id);
                var other = art.First(i => i.equipSlot != worn.equipSlot);

                foreach (var slot in EquipmentSlots.All) EquipMove.TryUnequip(character.equipment, save.stockpiledItems, slot);
                save.stockpiledItems.Clear();
                InventoryOps.Add(save.stockpiledItems, worn.id);
                Assert.IsTrue(EquipmentOps.Equip(MenuContext.Camp, save, character, worn.id, worn.equipSlot, worn.IsEquippable),
                    "fixture: the worn item could not be put on");
                InventoryOps.Add(save.stockpiledItems, better.id);
                InventoryOps.Add(save.stockpiledItems, other.id);

                // A run, so the pack holds a book beside the gear.
                RunManager.StartRun(4242UL);
                var book = ContentDatabase.Skills.FirstOrDefault(sk => sk != null && !string.IsNullOrWhiteSpace(sk.iconPath))
                           ?? ContentDatabase.Skills.First(sk => sk != null);
                RunManager.Run.unassignedSpellBooks.Add(book.id);

                dossier.ShowSection(CharacterSection.Loadout, focusRow: false);
                dossier.Refresh();
                yield return null;

                // The better item in the compare column: its deltas are the
                // thing the owner reads this capture for.
                var betterCell = Cell(PackCellOf(dossier, e => e.Kind == PackEntryKind.Gear && e.Item.Id == better.id));
                EventSystem.current.SetSelectedGameObject(betterCell.gameObject);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture("SystemMenu_character_loadout.png");

                // An empty slot armed: focus in the pack, filtered to it.
                var emptySlot = other.equipSlot;
                Assert.IsTrue(string.IsNullOrEmpty(character.equipment.Get(emptySlot)), "fixture: the slot to arm is not empty");
                Cell(SlotName(emptySlot)).onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.IsTrue(dossier.HasPending, "the empty slot did not arm");
                yield return Capture("SystemMenu_character_armed.png");

                dossier.CancelPending();
                yield return null;

                // And the equip itself, which must change what is worn.
                string before = character.equipment.Get(worn.equipSlot);
                Cell(PackCellOf(dossier, e => e.Kind == PackEntryKind.Gear && e.Item.Id == better.id)).onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.4f);

                Assert.AreNotEqual(before, character.equipment.Get(worn.equipSlot),
                    "the pack click changed nothing on the character");
                Assert.AreEqual(better.id, character.equipment.Get(worn.equipSlot));
                yield return Capture("SystemMenu_pack_equipped.png");

                dossier.ShowSection(CharacterSection.Attributes, focusRow: true);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture("SystemMenu_character_attributes.png");

                // Bjorn, who has the most to group: his innate Slam, the skills
                // three kindled talents hand him, and the talents themselves
                // under them. The save is put back below.
                var bjorn = save.roster.First(c => c.definitionId == "bear");
                bjorn.unlockedTalentIds.Clear();
                foreach (string talent in new[] { "bear_sen_root", "bear_sen_hold_1", "bear_sen_bellow_1" })
                {
                    bjorn.unlockedTalentIds.Add(talent);
                }

                Assert.IsTrue(dossier.SelectCharacter("bear"), "fixture: Bjorn is not on the strip");
                dossier.ShowSection(CharacterSection.Skills, focusRow: true);
                dossier.Refresh();
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture("SystemMenu_character_skills.png");
            }
            finally
            {
                RunManager.ResetForTests();
                RestoreSave(backup);
            }
        }

        private static string SlotName(EquipmentSlot slot) => "DossierSlot" + slot.ToString();

        private static string PackCellOf(CharacterDossierController dossier, System.Func<PackEntry, bool> which)
        {
            int index = dossier.VisiblePack.ToList().FindIndex(e => which(e));
            Assert.GreaterOrEqual(index, 0, "the pack shows no such entry");
            return "DossierPackCell" + (index - dossier.ScrollRow * DossierLayout.PackColumns);
        }

        private static Button Cell(string name)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsInactive.Include).FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"the pane drew no control named {name}");
            return button;
        }

        // The Options pane, on its own: one capture per failure, so a
        // regression elsewhere never blocks the one piece of evidence this
        // pane's plan needs.
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

        // ---- EVERY TAB AT EVERY ASPECT (plan phase 7) --------------------------------------------
        //
        // One picture per tab per UiFrames aspect, in a descent so RUN exists,
        // named SystemMenu_aspect_<tab>_<w>x<h>.png, plus the fight's CHARACTER
        // (view only, with the mode pill) at each aspect too. The layout is
        // audited at these four frames by UiAudit; what a capture answers is
        // whether it reads: the ultra-wide frame must not strand the panel, and
        // the 4:3 frame must not crowd it.
        //
        // THE WALK IS ONE ITERATOR, and a second test runs it without a camera.
        // The capture test is skipped on a runner with no graphics device, and
        // an iterator that only ever runs there would carry its first exception
        // to the owner's screenshot pass. Walking the same tabs and asserting
        // each is showing is the part a headless gate can hold.
        private static readonly (string Name, SystemMenuTab Tab, CharacterSection? Section)[] AspectTabs =
        {
            ("loadout", SystemMenuTab.Character, CharacterSection.Loadout),
            ("skills", SystemMenuTab.Character, CharacterSection.Skills),
            ("party", SystemMenuTab.Party, null),
            ("run", SystemMenuTab.Run, null),
            ("glossary", SystemMenuTab.Glossary, null),
            ("options", SystemMenuTab.Options, null),
            ("exit", SystemMenuTab.Exit, null),
        };

        private string _captureRoot;

        // A throwaway save, so a capture that starts a run and equips gear
        // leaves the real slot alone.
        private void UseAThrowawaySave()
        {
            _captureRoot = Path.Combine(Path.GetTempPath(), "pp-menu-aspect-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_captureRoot);
            SaveSystem.RootOverride = _captureRoot;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
        }

        private void DropTheThrowawaySave()
        {
            Navigation.Reset();
            TestGlobals.ResetAll();
            RunManager.ResetForTests();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            Time.timeScale = 1f;
            if (_captureRoot != null && Directory.Exists(_captureRoot)) Directory.Delete(_captureRoot, recursive: true);
        }

        [UnityTest]
        public IEnumerator CaptureEveryTabAtEveryAspect()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests");
            }

            yield return WalkEveryTab(capture: true);
        }

        [UnityTest]
        public IEnumerator TheAspectWalkReachesEveryTabInADescent()
        {
            yield return WalkEveryTab(capture: false);
        }

        private IEnumerator WalkEveryTab(bool capture)
        {
            UseAThrowawaySave();
            try
            {
                // A descent with something on every tab: Bjorn with kindled
                // talents (the longest skills list), a worn item and a spare
                // for the pack, an unplaced book, three relics with the longest
                // descriptions, an event buff, bonus damage and two consumables.
                var save = SaveSlotManager.CurrentSave;
                save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };

                var bjorn = save.roster.First(c => c.definitionId == "bear");
                foreach (string talent in new[] { "bear_sen_root", "bear_sen_hold_1", "bear_sen_bellow_1" })
                {
                    bjorn.unlockedTalentIds.Add(talent);
                }

                var art = ContentDatabase.Equippables.Where(i => i != null && !string.IsNullOrWhiteSpace(i.iconPath)).ToList();
                var slotWithTwo = art.GroupBy(i => i.equipSlot).First(g => g.Count() >= 2);
                var worn = slotWithTwo.OrderBy(i => i.tier).First();
                var spare = slotWithTwo.OrderByDescending(i => i.tier).First(i => i.id != worn.id);
                InventoryOps.Add(save.stockpiledItems, worn.id);
                Assert.IsTrue(EquipmentOps.Equip(MenuContext.Camp, save, bjorn, worn.id, worn.equipSlot, worn.IsEquippable),
                    "fixture: the worn item could not be put on");
                InventoryOps.Add(save.stockpiledItems, spare.id);
                InventoryOps.Add(save.stockpiledItems, "health_potion", 2);
                InventoryOps.Add(save.stockpiledItems, "mana_potion", 1);

                RunManager.StartRun(4242UL);
                var run = RunManager.Run;
                var book = ContentDatabase.Skills.FirstOrDefault(sk => sk != null && !string.IsNullOrWhiteSpace(sk.iconPath))
                           ?? ContentDatabase.Skills.First(sk => sk != null);
                run.unassignedSpellBooks.Add(book.id);
                run.relicIds.Clear();
                run.relicIds.AddRange(ContentDatabase.Relics
                    .Where(r => r != null)
                    .OrderByDescending(r => r.Data.Description.Length)
                    .Take(3)
                    .Select(r => r.id));
                run.eventBuffs.Add(new PrincesPalace.Domain.Events.EventBuffEntry
                {
                    kind = PrincesPalace.Domain.Events.EventBuffs.PrincesFavor, amount = 2,
                });
                run.bonusDamagePercent = 6;
                RunLedger.RecordRoom(run, won: true, expGained: 15, step: 3);
                RunManager.BankPayout(40);
                RunLedger.Fold(run, LoudFight(), new[] { "a", "b" });

                yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
                yield return null;
                yield return null;

                var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
                Assert.IsNotNull(menu, "the Map scene has no SystemMenuController");
                Assert.AreEqual(MenuContext.Descent, menu.Context, "fixture: the map's menu is not the descent's");

                var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
                Assert.IsNotNull(dossier, "the Character pane has no dossier controller");

                menu.Open(SystemMenuTab.Character);
                yield return null;
                Assert.IsTrue(dossier.SelectCharacter("bear"), "fixture: Bjorn is not on the strip");

                foreach (var tab in AspectTabs)
                {
                    menu.Select(tab.Tab);
                    if (tab.Section.HasValue)
                    {
                        dossier.ShowSection(tab.Section.Value, focusRow: false);
                        dossier.Refresh();
                    }

                    if (capture) yield return new WaitForSecondsRealtime(0.4f);
                    yield return null;

                    Assert.IsTrue(menu.IsOpen, $"{tab.Name}: the menu closed");
                    Assert.AreEqual(tab.Tab, menu.SelectedTab, $"{tab.Name}: the wrong tab is showing");
                    if (tab.Section.HasValue) Assert.AreEqual(tab.Section.Value, dossier.Section, $"{tab.Name}: the wrong section");

                    if (capture) yield return CaptureAtEveryAspect(tab.Name);
                }

                menu.Close();
            }
            finally
            {
                DropTheThrowawaySave();
            }
        }

        [UnityTest]
        public IEnumerator CaptureTheFightsCharacterViewAtEveryAspect()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter SystemMenuCaptureTests");
            }

            yield return WalkTheFightsCharacterView(capture: true);
        }

        [UnityTest]
        public IEnumerator TheFightsCharacterViewOpensViewOnly()
        {
            yield return WalkTheFightsCharacterView(capture: false);
        }

        // The Fight context once: CHARACTER, every section readable and none
        // changeable, with the lintel's mode pill saying so. Health, pool and
        // effective stats are the fight's own (plan C4).
        private IEnumerator WalkTheFightsCharacterView(bool capture)
        {
            UseAThrowawaySave();
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
            try
            {
                SaveSlotManager.CurrentSave.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };
                RunManager.StartRun(4242UL);

                yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
                yield return null;
                yield return null;

                var fight = Object.FindAnyObjectByType<FightController>();
                Assert.IsNotNull(fight, "the Fight scene has no FightController");
                var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
                Assert.IsNotNull(menu, "the Fight scene carries no SystemMenuController");

                var heroes = new List<string> { "sheep", "bear", "owl" };
                var enemies = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
                var built = FightEncounterAdapter.Build(heroes, enemies, new PrincesPalace.Domain.Rng.SeededRandom(3));
                built.Session.Begin();
                fight.Bind(built.Session, PrincesPalace.Domain.Rewards.EncounterClass.Normal);
                yield return null;
                for (int i = 0; i < 120 && !built.Session.IsPlayerTurn; i++) yield return null;

                menu.Open(SystemMenuTab.Character);
                if (capture) yield return new WaitForSecondsRealtime(0.5f);
                yield return null;

                Assert.AreEqual(MenuContext.Fight, menu.Context, "fixture: this is not the fight's menu");
                Assert.IsTrue(menu.IsOpen);
                Assert.AreEqual(SystemMenuTab.Character, menu.SelectedTab);
                Assert.IsFalse(MenuPermissions.Allows(MenuAction.EquipGear, menu.Context),
                    "a fight's character view must be view only");

                if (capture) yield return CaptureAtEveryAspect("fight_character");

                menu.Close();
            }
            finally
            {
                DropTheThrowawaySave();
            }
        }

        private static IEnumerator CaptureAtEveryAspect(string tab)
        {
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, $"{tab}: no root canvas to capture");

            Directory.CreateDirectory(OutputDir);
            foreach (var frame in UiFrames.All)
            {
                int width = (int)frame.X;
                int height = (int)frame.Y;
                string path = Path.Combine(OutputDir, $"SystemMenu_aspect_{tab}_{width}x{height}.png");

                CanvasCapture.RenderToFile(canvas, path, width, height);
                Assert.IsTrue(File.Exists(path), $"no capture written to {path}");
                Debug.Log($"[SystemMenuCapture] wrote {path}");
                yield return null;
            }
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
        // RUN is the one capture on this menu that cannot be judged any other
        // way: every assertion about it is about a figure or a node, and the
        // question a screenshot answers -- whether three columns of relics,
        // effects and figures read as a set, and whether a three-line relic
        // description fits its row -- is not one a count of active objects can
        // reach.
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
                // history rather than a fresh run, because a run that has done
                // nothing prints zeroes and shows nothing about the layout:
                // three relics (the longest authored descriptions among them,
                // so the worst row is in shot), one event buff, bonus damage
                // and two consumables.
                RunManager.StartRun(4242);
                var run = RunManager.Run;
                run.relicIds.Clear();
                run.relicIds.AddRange(ContentDatabase.Relics
                    .Where(r => r != null)
                    .OrderByDescending(r => r.Data.Description.Length)
                    .Take(3)
                    .Select(r => r.id));
                run.eventBuffs.Add(new PrincesPalace.Domain.Events.EventBuffEntry
                {
                    kind = PrincesPalace.Domain.Events.EventBuffs.PrincesFavor, amount = 2,
                });
                run.bonusDamagePercent = 6;
                InventoryOps.Add(SaveSlotManager.CurrentSave.stockpiledItems, "health_potion", 2);
                InventoryOps.Add(SaveSlotManager.CurrentSave.stockpiledItems, "mana_potion", 1);

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
                menu.Select(SystemMenuTab.Run);
                yield return new WaitForSecondsRealtime(0.4f);

                string statsPath = Path.Combine(OutputDir, "SystemMenu_run.png");
                CanvasCapture.RenderToFile(canvas, statsPath);
                Assert.IsTrue(File.Exists(statsPath));
                Debug.Log($"[SystemMenuCapture] wrote {statsPath}");

                // And the exits, with abandon showing -- which it only does in a
                // descent, so this scene is the only place the card can be seen.
                menu.Select(SystemMenuTab.Exit);
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
            // the swap and notice pictures, and PartyController.Persist()
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

                // STATE 4 (P4): right after a committed swap -- the notice, at
                // full alpha before its fade begins (MenuNotice holds full
                // alpha for its first DefaultSeconds).
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
