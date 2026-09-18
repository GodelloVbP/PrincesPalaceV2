using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Party;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // EIGHT PICTURES THE OWNER JUDGES, one per screen their hardware
    // play-test named, each with the pad focus standing somewhere specific.
    // The arrow's size, its colour, its distance from the control and which
    // side it picks are all decisions no assertion can settle -- the same
    // bargain PartyGamepadVisualCaptureTests already struck for plan section
    // 8's four-state picture, and it is struck again here for the same
    // reason.
    //
    // WHAT EACH PICTURE IS FOR, so the owner is not asked to judge "does this
    // look right" in the abstract:
    //   hub_gate           - the complaint that started this ("the selector
    //                        on the start descent is huge and looks weird").
    //                        The old halo was 620 units across because the
    //                        gate is; this is the same focus, same control.
    //   talents_orb        - a screen that had NO focus visual at all, so any
    //                        arrow is an improvement and the question is only
    //                        whether it reads against the constellation art.
    //   party_carry        - the four-state case. Ring = a legal
    //                        destination, several at once; arrow = the one
    //                        Submit will resolve on. If those two do not
    //                        separate at a glance this is the picture that
    //                        says so.
    //   dossier_pack       - "a player can't see where they're going in the
    //                        character sheets screen". Cell focused, its
    //                        tooltip open, on the busiest screen in the game.
    //   fight_verb         - the verb column at rest with focus on ATTACK:
    //                        the SAME state that used to paint the open-branch
    //                        plate. Read it for "can I tell focus from open".
    //   fight_target       - the arrow over an enemy plate, which is the
    //                        thing the owner actually asked for ("a small
    //                        hovering arrow for the thing you're targeting").
    //   reckoning_card     - the arrow beside a rarity halo it must no longer
    //                        be confused with.
    //   options_row        - a wide row, so the one case where the marker
    //                        goes to the LEFT rather than above.
    //
    // NOT A GATE. run_tests_parallel.ps1 passes -nographics, where
    // CanvasCapture.IsSupported is false and every test here ignores itself --
    // the guard every *CaptureTests fixture in this project carries. Driven by
    //   tools/screenshot.ps1 -Runtime -RuntimeFilter FocusMarkerVisualCaptureTests
    // and the PNGs are copied out of tools/screenshots/runtime/ afterwards,
    // because the next -Runtime run wipes that directory.
    public class FocusMarkerVisualCaptureTests
    {
        // FLAT, not a subfolder -- tools/screenshot.ps1's copy-back check
        // reads this directory's own top level only.
        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        private string _root;
        private ScriptedBaseInput _input;
        private Canvas _canvas;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-marker-shot-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private void RequireAGraphicsDevice()
        {
            if (CanvasCapture.IsSupported) return;
            Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter " +
                          "FocusMarkerVisualCaptureTests");
        }

        // ---- the eight ---------------------------------------------------------

        [UnityTest]
        public IEnumerator HubGateFocused()
        {
            RequireAGraphicsDevice();
            yield return LoadScene("Hub");

            // The hub's own entry IS the gate (HubController.RegisterNavContext),
            // so arriving is enough -- no Move is needed and none is made,
            // which is also the state a player is in one second into the game.
            yield return Settle();
            Shoot("marker_hub_gate");
        }

        [UnityTest]
        public IEnumerator TalentsOrbFocused()
        {
            RequireAGraphicsDevice();
            yield return LoadScene("Talents");

            // Up from the root reaches the centre tier-1 stone -- a MOVE
            // rather than the entry, so the picture shows the marker
            // somewhere the player put it.
            yield return Move(0f, 1f);
            yield return Settle();
            Shoot("marker_talents_orb");
        }

        [UnityTest]
        public IEnumerator PartyMidCarryWithADestinationFocused()
        {
            RequireAGraphicsDevice();
            yield return LoadScene("Hub");

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "owl", "sheep", "bear" };

            var menu = Menu();
            menu.Open();
            menu.Select(SystemMenuTab.Party);
            yield return null;

            var party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            Assert.IsNotNull(party, "the hub carries no PartyController");

            EventSystem.current.SetSelectedGameObject(Node($"PartySeat{PartySeat.Middle}Button"));
            yield return null;
            yield return Submit();
            Assert.IsNotNull(party.Formation.SelectedId, "fixture: nobody was picked up");

            EventSystem.current.SetSelectedGameObject(Node($"PartySeat{PartySeat.Rear}Button"));
            yield return Settle();
            Shoot("marker_party_carry");
        }

        [UnityTest]
        public IEnumerator DossierPackCellFocusedWithItsTooltip()
        {
            RequireAGraphicsDevice();
            yield return LoadScene("Hub");

            var menu = Menu();
            menu.Open();
            menu.Select(SystemMenuTab.CharacterInventory);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the Character pane has no dossier controller");

            // Two items so the pack has something in it and the tooltip has
            // something to describe -- DossierGamepadNavigationTests' own
            // fixture shape.
            var pair = ContentDatabase.Equippables
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.iconPath))
                .OrderByDescending(i => i.tier)
                .Take(2)
                .ToList();
            Assert.AreEqual(2, pair.Count, "the catalogue has fewer than two equippable items with art");

            var save = SaveSlotManager.CurrentSave;
            save.stockpiledItems.Clear();
            InventoryOps.Add(save.stockpiledItems, pair[0].id);
            InventoryOps.Add(save.stockpiledItems, pair[1].id);
            SaveSlotManager.SaveCurrent();

            dossier.ShowPack(true);
            dossier.Refresh();
            yield return null;

            EventSystem.current.SetSelectedGameObject(Node("DossierPackCell0"));
            yield return Settle();
            Shoot("marker_dossier_pack_cell");
        }

        [UnityTest]
        public IEnumerator FightVerbFocusedAndThenAnEnemyTargetFocused()
        {
            RequireAGraphicsDevice();
            yield return LoadScene("Fight");

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");
            Assert.GreaterOrEqual(enemyIds.Count, 2, "need two enemies for a target rack worth showing");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));
            built.Session.Begin();
            fight.Bind(built.Session, EncounterClass.Normal);
            yield return null;
            for (int i = 0; i < 60 && !built.Session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(built.Session.IsPlayerTurn, "never reached a player turn");

            // ONE FRAME OF STICK AT REST is what puts the marker up here:
            // Fight's own branch answers focus from its model, and the model
            // is already on ATTACK. The picture is the resting verb column,
            // which is exactly the state the owner called permanently
            // hovered.
            yield return Move(0f, 0f);
            yield return Settle();
            Shoot("marker_fight_verb");

            yield return Submit();
            yield return Settle();
            Shoot("marker_fight_target");
        }

        [UnityTest]
        public IEnumerator ReckoningCardFocused()
        {
            RequireAGraphicsDevice();
            yield return LoadScene("Fight");

            var reckoning = Object.FindAnyObjectByType<ReckoningController>(FindObjectsInactive.Include);
            Assert.IsNotNull(reckoning, "the Reckoning was never wired into the fight scene");

            var reward = new CombatReward { GoldGained = 46 };
            reward.Characters.Add(new CharacterReward("shawn", "Shawn", 1, 0, 1, 79, 100, 79, expToNextBefore: 100));

            // The highest tiers with art, so every card has a real icon and a
            // real rarity halo -- which is the thing the arrow must no longer
            // be confused with.
            var offers = ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable && !string.IsNullOrWhiteSpace(i.iconPath))
                .OrderByDescending(i => i.tier)
                .Take(3)
                .Select(i => new ItemOffer(i.id, i.tier, 0))
                .ToList();
            Assert.AreEqual(3, offers.Count, "need three equippable items with art to offer");

            reckoning.Show(reward, offers);

            // THE ENTRANCE IS A REAL-TIME SWEEP, and a picture taken during
            // it catches a card mid-flight as a vertical sliver -- which is
            // what the first run of this capture produced. 2.5s is
            // ReckoningCaptureTests' own settle for the same animation.
            yield return new WaitForSecondsRealtime(2.5f);

            // Right off the entry, so the arrow is on the MIDDLE card with a
            // lit neighbour either side of it.
            yield return Move(1f, 0f);
            yield return Settle();
            Shoot("marker_reckoning_card");
        }

        [UnityTest]
        public IEnumerator OptionsRowFocused()
        {
            RequireAGraphicsDevice();
            yield return LoadScene("Hub");

            var menu = Menu();
            menu.Open();
            menu.Select(SystemMenuTab.Options);
            yield return null;

            // A row is far wider than it is tall, so this is the one picture
            // where FocusMarkerPlacement sends the arrow to the LEFT of the
            // control rather than above it.
            var row = Node("OptionsRowbattlespeed") ?? Node("OptionsRowresolution");
            Assert.IsNotNull(row, "the options pane built neither of the two stepper rows this looks for");
            EventSystem.current.SetSelectedGameObject(row);
            yield return Settle();
            Shoot("marker_options_row");
        }

        // ---- fixture -----------------------------------------------------------

        private IEnumerator LoadScene(string name)
        {
            yield return SceneManager.LoadSceneAsync(name, LoadSceneMode.Single);
            yield return null;

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, $"the {name} scene's EventSystem is not running NavigationInputModule");

            // Set BEFORE letting any frame run: a previously-loaded scene's
            // EventSystem can still be current for a frame.
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            yield return null;
            yield return null;

            _canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.isRootCanvas && c.gameObject.scene == SceneManager.GetActiveScene());
            Assert.IsNotNull(_canvas, $"the {name} scene has no root Canvas to render");
        }

        private static SystemMenuController Menu()
        {
            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub carries no SystemMenuController");
            return menu;
        }

        private static GameObject Node(string name) =>
            Resources.FindObjectsOfTypeAll<RectTransform>()
                .FirstOrDefault(r => r.name == name && r.gameObject.scene.IsValid())
                ?.gameObject;

        private IEnumerator Move(float horizontal, float vertical)
        {
            _input.Horizontal = horizontal;
            _input.Vertical = vertical;
            yield return null;
            _input.ClearOneFrameFlags();
            _input.Horizontal = 0f;
            _input.Vertical = 0f;
            yield return null;
        }

        private IEnumerator Submit()
        {
            _input.SubmitDown = true;
            yield return null;
            _input.ClearOneFrameFlags();
        }

        // TWO FRAMES BEFORE EVERY SHOT. The marker positions itself in
        // LateUpdate off whatever the dispatcher settled during Process, so a
        // picture taken on the same frame as the selection change catches it
        // one step behind.
        private IEnumerator Settle()
        {
            yield return null;
            yield return null;
        }

        private void Shoot(string fileName)
        {
            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, fileName + ".png");
            CanvasCapture.RenderToFile(_canvas, path);
            FileAssert.Exists(path);

            var marker = NavigationInputModule.Marker;
            Debug.Log($"[FocusMarkerVisualCapture] {fileName}: marker "
                      + (marker != null && marker.IsShown
                          ? $"on '{(marker.Target == null ? "?" : marker.Target.name)}' at {marker.LocalPosition}"
                          : "NOT DRAWN -- the picture will not show one"));
        }
    }
}
