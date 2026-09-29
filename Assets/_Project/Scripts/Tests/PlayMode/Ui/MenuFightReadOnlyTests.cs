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
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Party;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // A FIGHT IS READ-ONLY (docs/PLAN_SYSTEM_MENU.md C3): every change the
    // system menu can make is refused in the Fight scene, through the control a
    // player presses AND through the domain operation behind it, and nothing
    // moves in memory or on disk.
    //
    // WHY BOTH ROUTES. The controls ask MenuPermissions, and so do the
    // operations, and either could regress alone: a control that stopped asking
    // still lands on a refusing operation, and an operation that stopped asking
    // is still guarded by its control. The bug this closes had the third shape
    // -- the spell-book handlers were wired with no guard at all, so a book
    // could be placed mid-fight.
    //
    // WHAT "NOTHING MOVES" MEANS. One photograph of everything a refused
    // action could touch (the roster's characters, the bag, the seat order, the
    // run's books) taken before and after, plus the save file's bytes and the
    // fight's own party actors. The Map copy of the same arrangement proves the
    // photograph is not vacuous: the same presses there DO move it.
    //
    // A run is real (RunManager.StartRun, a fight room, the Fight scene the game
    // opens), in a throwaway save root, so an action that slips through can
    // only damage a temp file.
    public class MenuFightReadOnlyTests
    {
        // FightAfterTheEliteTests' seed, as MidRunCollectionTests uses it: a leg
        // that offers a plain fight room.
        private const ulong Seed = 639228196442867409UL;

        // The top of the track with the watermark at 38: two levels waiting,
        // and three collected titles to cycle between (reward_tracks.json).
        private const int Level = 40;
        private const int Watermark = 38;

        // sheep holds books; bear (fury) does not. Two books in the run, one
        // already placed, so a press can both learn (slot 1) and replace (slot 0).
        private const string Holder = "sheep";
        private const string PlacedBook = "frost_flare";
        private const string SpareBook = "mud_burst";
        private const string SecondSpareBook = "lightning_bolt";

        private string _root;
        private readonly List<string> _navigated = new List<string>();
        private float _battleSpeedBefore;

        private ItemDefinition _worn;
        private ItemDefinition _packed;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-menu-readonly-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            _navigated.Clear();
            Navigation.LoadOverride = scene => _navigated.Add(scene);
            Navigation.QuitOverride = () => { };

            _battleSpeedBefore = GameSettings.BattleSpeed;
        }

        [TearDown]
        public void Restore()
        {
            foreach (var hold in Object.FindObjectsByType<HoldToConfirm>(FindObjectsInactive.Include))
            {
                hold.Cancel();
            }

            GameSettings.SetBattleSpeed(_battleSpeedBefore);
            SharedScene.MarkDirty("MenuFightReadOnlyTests loads its own scenes over a real run");
            SharedScene.AfterTest();

            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- the arrangement -------------------------------------------------

        // A run in a plain fight room, the squad (sheep first) at Level with
        // Watermark collected, and everything a menu change could touch
        // present: an unspent point and an invested one, one worn item and a
        // different one in the pack, a placed book and two spare ones.
        private void ArrangeARun()
        {
            RunManager.StartRun(Seed);

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { Holder, "owl", "bear" };

            foreach (var member in save.roster)
            {
                member.level = Level;
                member.claimedTrackLevel = Watermark;
            }

            var shawn = save.roster.First(c => c.definitionId == Holder);
            shawn.unspentStatPoints = 3;
            Assert.IsTrue(shawn.Invest(AbilityScore.Strength), "fixture: could not place the invested point");

            var wearables = ContentDatabase.Equippables;
            _worn = wearables.First();
            _packed = wearables.First(item => item.id != _worn.id);

            // Equip wears a copy OFF the bag, so the worn one goes in first.
            InventoryOps.Add(save.stockpiledItems, _worn.id);
            Assert.IsTrue(EquipmentOps.Equip(MenuContext.Descent, save, shawn, _worn.id, _worn.equipSlot, _worn.IsEquippable),
                "fixture: the first wearable could not be put on");
            InventoryOps.Add(save.stockpiledItems, _packed.id);

            var run = RunManager.Run;
            run.learnedSpells.Add(new LearnedSpellEntry { characterId = Holder, skillId = PlacedBook, slot = 0 });
            run.unassignedSpellBooks = new List<string> { SpareBook, SecondSpareBook };

            Assert.IsTrue(SaveSlotManager.SaveCurrent(), "fixture: the save did not reach the throwaway root");
        }

        private IEnumerator EnterTheFight()
        {
            var room = RunManager.Choices().FirstOrDefault(n => n.Type == PrincesPalace.Domain.Dungeon.RoomType.Fight);
            Assert.IsNotNull(room, "the leg offered no plain fight room to play");
            Assert.IsTrue(RunManager.MoveTo(room.Id), "could not move into the fight room");
            SaveSlotManager.SaveCurrent();

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the Fight scene has no FightController");
            Assert.IsTrue(fight.HasSession, "the room built no session");
        }

        private IEnumerator EnterTheMap()
        {
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        // ---- the photograph ----------------------------------------------------

        [System.Serializable]
        private class Photograph
        {
            public List<Character> roster;
            public List<InventoryEntry> pack;
            public List<string> seats;
            public RunSnapshot run;
        }

        // Everything a refused action could have touched, as text: the roster's
        // whole characters (equipment, attributes, watermark, title), the bag,
        // the seat order and the run (learned and unassigned books).
        private static string Photo()
        {
            var save = SaveSlotManager.CurrentSave;
            return JsonUtility.ToJson(new Photograph
            {
                roster = save.roster,
                pack = save.stockpiledItems,
                seats = save.selectedCharacterIds,
                run = RunManager.Run,
            });
        }

        private byte[] SaveBytes() => File.ReadAllBytes(Path.Combine(_root, "save_slot_0.json"));

        // The fight's own party, as the session holds it. A refused change
        // never rebuilds it, and an accepted one would still not (the session
        // is built once), so this is the weaker half of the assertion.
        private static string Party(FightController fight) =>
            string.Join(";", fight.Session.Encounter.PlayerParty.Select(a =>
                $"{a.Name}|{a.MaxHealth}|{a.CurrentHealth}|{a.Attack}|{a.Speed}|{a.PhysicalDefense}|{a.MagicalDefense}"));

        // ---- finding controls -------------------------------------------------------

        private static Button Find(SystemMenuController menu, string name) =>
            menu.GetComponentsInChildren<Button>(includeInactive: true).FirstOrDefault(b => b.name == name);

        private static Button Need(SystemMenuController menu, string name)
        {
            var button = Find(menu, name);
            Assert.IsNotNull(button, $"the menu has no '{name}' control");
            return button;
        }

        private static void Press(SystemMenuController menu, string name) => Need(menu, name).onClick.Invoke();

        private static SystemMenuController Menu()
        {
            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the scene has no SystemMenuController");
            return menu;
        }

        private static string SlotName(EquipmentSlot slot) => "DossierSlot" + EquipmentSlots.DisplayName(slot).Replace(" ", "");

        // ---- the fight refuses everything, both ways ----------------------------

        [UnityTest]
        public IEnumerator EveryChangeIsRefusedInAFightThroughItsControl()
        {
            ArrangeARun();
            yield return EnterTheFight();

            var fight = Object.FindAnyObjectByType<FightController>();
            var menu = Menu();
            menu.Open();
            menu.Select(SystemMenuTab.Character);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.AreEqual(MenuContext.Fight, dossier.Context, "fixture: this is not the fight's dossier");
            dossier.Refresh();
            yield return null;

            string before = Photo();
            byte[] bytesBefore = SaveBytes();
            string partyBefore = Party(fight);

            // Gear: put the pack item on, take the worn one off.
            dossier.ShowPack(true);
            yield return null;
            Press(menu, "DossierPackCell0");
            Press(menu, SlotName(_worn.equipSlot));

            // Attributes: the compact grid's and the panel's plus and minus, on
            // every cell (which cell holds the invested score is per character).
            for (int i = 0; i < 6; i++)
            {
                Press(menu, $"DossierAttrPlus{i}");
                Press(menu, $"DossierAttrMinus{i}");
                Press(menu, $"DossierAttrRowPlus{i}");
                Press(menu, $"DossierAttrRowMinus{i}");
            }

            // Books: select each spare in turn and press the empty slot (learn)
            // and the placed one (replace). Selecting a book is viewing.
            dossier.ShowSpells(true);
            yield return null;
            for (int row = 0; row < 2; row++)
            {
                Press(menu, $"DossierUnassigned{row}");
                Press(menu, "DossierSpellSlot1");
                Press(menu, "DossierSpellSlot0");
            }

            // Rewards: open the track and press the node that collects.
            Press(menu, "DossierTrackRow");
            yield return null;
            Press(menu, $"TrackDot{Level}");
            Press(menu, "TrackCollectButton");
            yield return null;

            // Party: seats, cards, bench, and cycling every card's title.
            menu.Select(SystemMenuTab.Party);
            yield return null;
            var party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            Assert.AreEqual(PartyMode.ViewOnly, party.Formation.Mode, "the fight's party pane is not view-only");
            for (int i = 0; i < PartySeat.Count; i++)
            {
                Press(menu, $"PartySeat{i}Button");
                Press(menu, $"PartyCard{i}Button");
                Press(menu, $"PartyCard{i}Title");
            }
            Press(menu, "PartySeat0Button");
            Press(menu, "PartySeat2Button");
            party.SendToBench();
            party.Cancel();
            yield return null;

            Assert.AreEqual(before, Photo(),
                "a control changed the roster, the pack, the seats or the run's books in a fight");
            CollectionAssert.AreEqual(bytesBefore, SaveBytes(), "a control wrote the save in a fight");
            Assert.AreEqual(partyBefore, Party(fight), "the fight's party actors changed");
        }

        [UnityTest]
        public IEnumerator EveryChangeIsRefusedInAFightThroughItsOperation()
        {
            ArrangeARun();
            yield return EnterTheFight();

            var fight = Object.FindAnyObjectByType<FightController>();
            var save = SaveSlotManager.CurrentSave;
            var shawn = save.roster.First(c => c.definitionId == Holder);
            yield return null;

            string before = Photo();
            byte[] bytesBefore = SaveBytes();
            string partyBefore = Party(fight);

            const MenuContext inFight = MenuContext.Fight;

            Assert.IsFalse(EquipmentOps.Equip(inFight, save, shawn, _packed.id, _packed.equipSlot, _packed.IsEquippable),
                "Equip changed gear in a fight");
            Assert.IsFalse(EquipmentOps.Unequip(inFight, save, shawn, _worn.equipSlot),
                "Unequip changed gear in a fight");

            Assert.IsFalse(TalentOps.Invest(inFight, shawn, AbilityScore.Strength), "Invest spent a point in a fight");
            Assert.IsFalse(TalentOps.Refund(inFight, shawn, AbilityScore.Strength), "Refund took a point back in a fight");

            Assert.AreEqual(ShopRefusal.NotPermitted, RunOrchestrator.LearnSpell(inFight, Holder, SpareBook, 1).Reason);
            Assert.AreEqual(ShopRefusal.NotPermitted, RunOrchestrator.LearnSpell(inFight, Holder, SpareBook).Reason);
            Assert.AreEqual(ShopRefusal.NotPermitted, RunOrchestrator.ReplaceSpell(inFight, Holder, SpareBook, 0).Reason);
            Assert.AreEqual(ShopRefusal.NotPermitted, RunOrchestrator.UnlearnSpell(inFight, Holder, 0).Reason);

            Assert.IsFalse(RewardTracks.Claim(inFight, shawn, Level), "a reward was collected in a fight");

            // The party model: a fight's mode is view-only, and every command
            // on the formation the pane built reports no change.
            Assert.AreEqual(PartyMode.ViewOnly, PartyModes.For(inFight));

            var menu = Menu();
            menu.Open();
            menu.Select(SystemMenuTab.Party);
            yield return null;
            var formation = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include).Formation;
            Assert.AreEqual(PartyMode.ViewOnly, formation.Mode, "the fight's formation is not view-only");
            Assert.IsFalse(formation.ClickSeat(PartySeat.Front).Changed);
            Assert.IsFalse(formation.ClickSeat(PartySeat.Rear).Changed);
            Assert.IsFalse(formation.ClickCard("owl").Changed);
            Assert.IsFalse(formation.SendToBench().Changed);
            Assert.IsFalse(formation.Drop(PartySelectionSource.Seat(PartySeat.Front), PartySeat.Middle).Changed);
            Assert.IsNull(formation.SelectedId, "a refused click left a selection carried");

            Assert.AreEqual(before, Photo(), "an operation changed state in a fight");
            CollectionAssert.AreEqual(bytesBefore, SaveBytes(), "an operation wrote the save in a fight");
            Assert.AreEqual(partyBefore, Party(fight), "the fight's party actors changed");
        }

        // ---- and every refusal says why (C11) ------------------------------------------
        //
        // A refused press that changes nothing and says nothing reads as a dead
        // control. Each one below is pressed through its control, and the
        // menu's one notice must then read exactly what MenuPermissions gives
        // for that action -- the words come from the table, so the notice, the
        // footer's locked prompt and the refusal cannot drift apart.

        private static TMP_Text NoticeLine(SystemMenuController menu) =>
            menu.GetComponentsInChildren<TMP_Text>(includeInactive: true).First(t => t.name == "SystemFooterNotice");

        // Blanks the notice between presses: closing its object drops what it
        // was saying (MenuNotice.OnDisable), so each press starts from silence.
        private static void ClearNotice(SystemMenuController menu)
        {
            var go = NoticeLine(menu).gameObject;
            go.SetActive(false);
            go.SetActive(true);
        }

        private static void AssertRefusalIsSaid(
            SystemMenuController menu, MenuAction action, MenuContext context, string what, System.Action press)
        {
            ClearNotice(menu);
            Assert.AreEqual("", NoticeLine(menu).text, $"fixture: the notice was not clear before {what}");

            press();

            string reason = MenuPermissions.Reason(action, context).Format();
            Assert.IsNotEmpty(reason, $"fixture: the table gives no reason for {action} in {context}");
            Assert.AreEqual(reason, NoticeLine(menu).text, $"{what} was refused with no reason, or the wrong one");
        }

        [UnityTest]
        public IEnumerator EveryRefusedPressInAFightSaysWhyOnTheNotice()
        {
            ArrangeARun();
            yield return EnterTheFight();

            var menu = Menu();
            menu.Open();
            menu.Select(SystemMenuTab.Character);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            dossier.Refresh();
            yield return null;

            const MenuContext fight = MenuContext.Fight;

            dossier.ShowPack(true);
            yield return null;
            AssertRefusalIsSaid(menu, MenuAction.EquipGear, fight, "equipping from the pack",
                () => Press(menu, "DossierPackCell0"));
            AssertRefusalIsSaid(menu, MenuAction.UnequipGear, fight, "taking a worn item off",
                () => Press(menu, SlotName(_worn.equipSlot)));

            AssertRefusalIsSaid(menu, MenuAction.SpendAttribute, fight, "spending a point",
                () => Press(menu, "DossierAttrPlus0"));
            AssertRefusalIsSaid(menu, MenuAction.SpendAttribute, fight, "spending a point on the attributes panel",
                () => Press(menu, "DossierAttrRowPlus0"));
            AssertRefusalIsSaid(menu, MenuAction.RefundAttribute, fight, "refunding a point",
                () => Press(menu, "DossierAttrMinus0"));

            dossier.ShowSpells(true);
            yield return null;
            Press(menu, "DossierUnassigned0");
            AssertRefusalIsSaid(menu, MenuAction.PlaceBook, fight, "placing a book in an empty slot",
                () => Press(menu, "DossierSpellSlot1"));
            AssertRefusalIsSaid(menu, MenuAction.ReplaceBook, fight, "replacing a placed book",
                () => Press(menu, "DossierSpellSlot0"));

            Press(menu, "DossierTrackRow");
            yield return null;
            AssertRefusalIsSaid(menu, MenuAction.CollectReward, fight, "collecting on a node",
                () => Press(menu, $"TrackDot{Level}"));
            AssertRefusalIsSaid(menu, MenuAction.CollectReward, fight, "collecting all",
                () => Press(menu, "TrackCollectButton"));

            menu.Select(SystemMenuTab.Party);
            yield return null;
            AssertRefusalIsSaid(menu, MenuAction.RepositionParty, fight, "picking up a seat",
                () => Press(menu, "PartySeat0Button"));
            AssertRefusalIsSaid(menu, MenuAction.SwapParty, fight, "benching",
                () => Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include).SendToBench());
            AssertRefusalIsSaid(menu, MenuAction.CycleTitle, fight, "cycling a title",
                () => Press(menu, "PartyCard0Title"));
        }

        // Options and exits are not refused, so they say nothing.
        [UnityTest]
        public IEnumerator AnAllowedChangeInAFightSaysNothingOnTheNotice()
        {
            ArrangeARun();
            yield return EnterTheFight();

            var menu = Menu();
            menu.Open();
            menu.Select(SystemMenuTab.Options);
            yield return null;

            ClearNotice(menu);
            var options = Object.FindAnyObjectByType<OptionsController>(FindObjectsInactive.Include);
            options.Step("battlespeed", GameSettings.BattleSpeed < 2f ? +1 : -1);

            Assert.AreEqual("", NoticeLine(menu).text, "an allowed option change was announced as a refusal");
        }

        // The other contexts have their own reasons, from the same table: a
        // refund is a camp privilege, and party changes and books are not.
        [UnityTest]
        public IEnumerator ADescentRefusesARefundAndABenchWithTheirOwnWords()
        {
            ArrangeARun();
            yield return EnterTheMap();

            var menu = Menu();
            menu.Open();
            menu.Select(SystemMenuTab.Character);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            dossier.Refresh();
            yield return null;

            AssertRefusalIsSaid(menu, MenuAction.RefundAttribute, MenuContext.Descent, "refunding in a descent",
                () => Press(menu, "DossierAttrMinus0"));
            Assert.AreEqual("Refunds at camp", NoticeLine(menu).text);

            menu.Select(SystemMenuTab.Party);
            yield return null;
            var party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            party.ClickSeat(PartySeat.Front);
            AssertRefusalIsSaid(menu, MenuAction.SwapParty, MenuContext.Descent, "benching in a descent",
                () => party.SendToBench());
            Assert.AreEqual("Party changes happen at camp", NoticeLine(menu).text);
        }

        [UnityTest]
        public IEnumerator CampRefusesABookWithItsOwnWords()
        {
            ArrangeARun();
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Menu();
            menu.Open();
            menu.Select(SystemMenuTab.Character);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            dossier.Refresh();
            dossier.ShowSpells(true);
            yield return null;

            Press(menu, "DossierUnassigned0");
            AssertRefusalIsSaid(menu, MenuAction.PlaceBook, MenuContext.Camp, "placing a book at camp",
                () => Press(menu, "DossierSpellSlot1"));
            Assert.AreEqual("Spell books are placed during a descent", NoticeLine(menu).text);
        }

        // ---- the same presses where they are allowed ---------------------------------
        //
        // The photograph above would pass on a menu that did nothing at all.
        // The Map copy runs the same arrangement, and there each change lands.

        [UnityTest]
        public IEnumerator TheSamePressesChangeThingsInADescent()
        {
            ArrangeARun();
            yield return EnterTheMap();

            var save = SaveSlotManager.CurrentSave;
            var shawn = save.roster.First(c => c.definitionId == Holder);

            var menu = Menu();
            menu.Open();
            menu.Select(SystemMenuTab.Character);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.AreEqual(MenuContext.Descent, dossier.Context, "fixture: this is not the map's dossier");
            dossier.Refresh();
            yield return null;

            // Gear.
            string worn = shawn.equipment.Get(_packed.equipSlot);
            dossier.ShowPack(true);
            yield return null;
            Press(menu, "DossierPackCell0");
            Assert.AreNotEqual(worn, shawn.equipment.Get(_packed.equipSlot), "a descent did not equip from the pack");

            // Attributes: a point goes in, a refund is still refused.
            int unspent = shawn.unspentStatPoints;
            string invested = JsonUtility.ToJson(shawn.investedAbilityScores);
            for (int i = 0; i < 6; i++) Press(menu, $"DossierAttrMinus{i}");
            Assert.AreEqual(unspent, shawn.unspentStatPoints, "a descent refunded a point");
            Assert.AreEqual(invested, JsonUtility.ToJson(shawn.investedAbilityScores));
            Press(menu, "DossierAttrPlus0");
            Assert.AreEqual(unspent - 1, shawn.unspentStatPoints, "a descent did not spend a point");

            // Books: learn into the empty slot, then replace the placed one.
            dossier.ShowSpells(true);
            yield return null;
            var run = RunManager.Run;
            Press(menu, "DossierUnassigned0");
            Press(menu, "DossierSpellSlot1");
            Assert.IsTrue(run.learnedSpells.Any(e => e.characterId == Holder && e.slot == 1 && e.skillId == SpareBook),
                "a descent did not learn the book into the empty slot");
            Press(menu, "DossierUnassigned0");
            Press(menu, "DossierSpellSlot0");
            Assert.IsTrue(run.learnedSpells.Any(e => e.characterId == Holder && e.slot == 0 && e.skillId == SecondSpareBook),
                "a descent did not replace the placed book");
            CollectionAssert.Contains(run.unassignedSpellBooks, PlacedBook, "the replaced book did not return to the pool");

            // Rewards.
            Press(menu, "DossierTrackRow");
            yield return null;
            Press(menu, $"TrackDot{Level}");
            Assert.AreEqual(Level, shawn.claimedTrackLevel, "a descent did not collect the waiting rewards");

            // Titles.
            menu.Select(SystemMenuTab.Party);
            yield return null;
            int title = shawn.selectedTitleTrackLevel;
            int cards = 0;
            for (int i = 0; i < PartySeat.Count; i++) { Press(menu, $"PartyCard{i}Title"); cards++; }
            Assert.AreNotEqual(title, shawn.selectedTitleTrackLevel, "a descent did not cycle the title");
            Assert.AreEqual(PartySeat.Count, cards);
        }

        // ---- options and exits stay open ---------------------------------------------

        [UnityTest]
        public IEnumerator AnOptionStillChangesInAFight()
        {
            ArrangeARun();
            yield return EnterTheFight();

            var menu = Menu();
            menu.Open();
            menu.Select(SystemMenuTab.Options);
            yield return null;

            var options = Object.FindAnyObjectByType<OptionsController>(FindObjectsInactive.Include);
            Assert.IsNotNull(options, "the menu has no OptionsController");

            float before = GameSettings.BattleSpeed;
            options.Step("battlespeed", before < 2f ? +1 : -1);

            Assert.AreNotEqual(before, GameSettings.BattleSpeed, "an option did not change in a fight");
        }

        [UnityTest]
        public IEnumerator AnExitStillEndsTheRunInAFight()
        {
            ArrangeARun();
            yield return EnterTheFight();
            Assert.IsTrue(RunManager.HasRun, "fixture: no run to end");

            var menu = Menu();
            menu.Open();
            menu.Select(SystemMenuTab.Exit);
            yield return null;

            // Two presses: the first only arms it (SystemMenuExitsTests).
            Press(menu, "ExitTitle");
            CollectionAssert.IsEmpty(_navigated, "one press left the scene");
            Assert.IsTrue(RunManager.HasRun, "one press ended the run");

            Press(menu, "ExitTitle");
            Assert.IsFalse(RunManager.HasRun, "the exit did not reach EndRun in a fight");
            CollectionAssert.AreEqual(new[] { Navigation.MainMenu }, _navigated);
        }
    }
}
