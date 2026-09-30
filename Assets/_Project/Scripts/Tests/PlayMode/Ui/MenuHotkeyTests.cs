using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Party;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The one hotkey owner, driven through the REAL dispatcher on the real
    // scenes: a scripted BaseInput presses the Input Manager button, one frame
    // runs, and the assertion is on what the menu did. Never a direct
    // Toggle/Open call standing in for a press -- the point of the change is
    // that the hub, the map and the fight no longer read C and I themselves,
    // so a test that called their methods would pass with the owner deleted.
    //
    // Transitions are plan C6's; the one place this file departs from its
    // letter is I with the pack already up, which closes (MenuHotkeyRule's
    // comment has the reason).
    public class MenuHotkeyTests
    {
        private string _root;
        private ScriptedBaseInput _input;
        private SystemMenuController _menu;
        private FightController _fight;

        private const string C = "HotkeyC";
        private const string I = "HotkeyI";
        private const string P = "HotkeyP";
        private const string R = "HotkeyR";
        private const string View = "View";

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-menu-hotkeys-" + System.Guid.NewGuid().ToString("N"));
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
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // The scripted input goes on whatever module the scene just built, and
        // EventSystem.current has to be that scene's own.
        private void TakeOverInput()
        {
            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "this scene's EventSystem is not running NavigationInputModule");
            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "this scene carries no SystemMenuController");
        }

        private IEnumerator Press(string button)
        {
            _input.Press(button);
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private IEnumerator StartPress()
        {
            _input.SystemMenuDown = true;
            yield return null;
            _input.ClearOneFrameFlags();
        }

        private static CharacterDossierController Dossier() =>
            Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);

        private IEnumerator LoadHub()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();
            yield return null;
        }

        private IEnumerator LoadMap()
        {
            RunManager.StartRun(4242);
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();
            yield return null;
        }

        // A REAL BOUND SESSION, for the reason StartOpensSystemMenuTests gives:
        // Fight's NavContext is pushed by Bind, so without one the dispatcher
        // has no fight context to offer a hotkey to.
        private IEnumerator LoadFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = ContentDatabase.Characters.FirstOrDefault(c => c != null);
            var enemyIds = ContentDatabase.Enemies.Where(e => e != null).Take(2).Select(e => e.id).ToList();
            Assert.IsNotNull(hero, "no characters in content");

            var built = FightEncounterAdapter.Build(
                new List<string> { hero.id }, enemyIds, new Domain.Rng.SeededRandom(3));
            built.Session.Begin();
            _fight.Bind(built.Session, Domain.Rewards.EncounterClass.Normal);
            yield return null;

            for (int i = 0; i < 120 && !built.Session.IsPlayerTurn; i++) yield return null;
            Assert.IsTrue(built.Session.IsPlayerTurn, "never reached a player turn to press a hotkey on");

            TakeOverInput();
            yield return null;
        }

        // ---- the hub: every C6 row ---------------------------------------------------

        [UnityTest]
        public IEnumerator C_OpensTheCharacterTabWithoutThePack()
        {
            yield return LoadHub();
            Assert.IsFalse(_menu.IsOpen, "fixture: the menu should start closed");

            yield return Press(C);

            Assert.IsTrue(_menu.IsOpen, "C did not open the menu");
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab);
            Assert.IsFalse(Dossier().IsPackShown, "C opens the sheet, not the pack");
            Assert.AreEqual(0f, Time.timeScale, "the menu did not pause the game");
        }

        [UnityTest]
        public IEnumerator P_OpensThePartyTab()
        {
            yield return LoadHub();

            yield return Press(P);

            Assert.IsTrue(_menu.IsOpen, "P did not open the menu");
            Assert.AreEqual(SystemMenuTab.Party, _menu.SelectedTab);
        }

        [UnityTest]
        public IEnumerator APlainHotkeySwitchesTabsAndClosesOnItsOwnTab()
        {
            yield return LoadHub();

            yield return Press(C);
            yield return Press(P);
            Assert.IsTrue(_menu.IsOpen, "P from Character should switch, not close");
            Assert.AreEqual(SystemMenuTab.Party, _menu.SelectedTab);

            yield return Press(C);
            Assert.IsTrue(_menu.IsOpen, "C from Party should switch, not close");
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab);

            yield return Press(C);
            Assert.IsFalse(_menu.IsOpen, "C on the Character tab should close the menu");
            Assert.AreEqual(1f, Time.timeScale, 0.0001f, "closing by hotkey left the game paused");

            yield return Press(P);
            yield return Press(P);
            Assert.IsFalse(_menu.IsOpen, "P on the Party tab should close the menu");
        }

        [UnityTest]
        public IEnumerator I_GoesToThePackAndDoesNotCloseTheMenuFromCharacterWithoutIt()
        {
            yield return LoadHub();

            yield return Press(C);
            Assert.IsFalse(Dossier().IsPackShown, "fixture: C opened with the pack down");

            yield return Press(I);

            Assert.IsTrue(_menu.IsOpen, "I on the Character tab closed the menu instead of going to the pack");
            Assert.IsTrue(Dossier().IsPackShown, "I did not bring the pack up");
        }

        [UnityTest]
        public IEnumerator I_OpensTheClosedMenuOnTheCharacterTabWithThePack()
        {
            yield return LoadHub();

            yield return Press(I);

            Assert.IsTrue(_menu.IsOpen);
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab);
            Assert.IsTrue(Dossier().IsPackShown);
        }

        [UnityTest]
        public IEnumerator I_FromAnotherTabNavigatesToTheCharacterPack()
        {
            yield return LoadHub();

            yield return Press(P);
            Assert.AreEqual(SystemMenuTab.Party, _menu.SelectedTab, "fixture: P opened Party");

            yield return Press(I);

            Assert.IsTrue(_menu.IsOpen, "I from Party closed the menu");
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab);
            Assert.IsTrue(Dossier().IsPackShown);
        }

        [UnityTest]
        public IEnumerator I_WithThePackAlreadyUpClosesTheMenu()
        {
            yield return LoadHub();

            yield return Press(I);
            yield return Press(I);

            Assert.IsFalse(_menu.IsOpen, "the second I, with nowhere further to go, should close the sheet");
            Assert.AreEqual(1f, Time.timeScale, 0.0001f);
        }

        [UnityTest]
        public IEnumerator C_WithThePackUpClosesTheMenuAndTheNextOpenStartsWithoutIt()
        {
            yield return LoadHub();

            yield return Press(I);
            yield return Press(C);

            Assert.IsFalse(_menu.IsOpen, "C on the Character tab should close the menu, pack up or not");
            Assert.IsFalse(Dossier().IsPackShown, "a pending state must not survive a close");

            yield return Press(C);
            Assert.IsFalse(Dossier().IsPackShown, "the reopened sheet came back with the pack still up");
        }

        [UnityTest]
        public IEnumerator AHotkeyCancelsAPartyCarryBeforeSwitchingAway()
        {
            yield return LoadHub();

            SaveSlotManager.CurrentSave.selectedCharacterIds = new List<string> { "owl", "sheep", "bear" };

            yield return Press(P);
            var party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            Assert.IsNotNull(party, "the hub carries no PartyController");
            yield return null;

            party.ClickCard("owl");
            Assert.IsNotNull(party.Formation.SelectedId, "fixture: clicking a card should start a carry");

            yield return Press(C);

            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab);
            Assert.IsNull(party.Formation.SelectedId, "the carry survived a hotkey that left the Party tab");
            Assert.AreEqual("owl", party.Formation.SeatIds[PartySeat.Front],
                "cancelling the carry changed the formation");
        }

        // ---- R: the run, where there is one ------------------------------------------

        [UnityTest]
        public IEnumerator R_OpensTheRunTabInADescentAndClosesOnItsOwnTab()
        {
            yield return LoadMap();

            yield return Press(R);
            Assert.IsTrue(_menu.IsOpen, "R did not open the menu on the map");
            Assert.AreEqual(SystemMenuTab.Run, _menu.SelectedTab);

            yield return Press(C);
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab, "C from Run should switch, not close");

            yield return Press(R);
            Assert.AreEqual(SystemMenuTab.Run, _menu.SelectedTab, "R from Character should switch to Run");

            yield return Press(R);
            Assert.IsFalse(_menu.IsOpen, "R on the Run tab should close the menu");
            Assert.AreEqual(1f, Time.timeScale, 0.0001f, "closing by hotkey left the game paused");
        }

        [UnityTest]
        public IEnumerator R_InAFightOpensTheRunTab()
        {
            yield return LoadFight();

            yield return Press(R);

            Assert.IsTrue(_menu.IsOpen, "R did not open the menu over a fight");
            Assert.AreEqual(SystemMenuTab.Run, _menu.SelectedTab);
        }

        // Camp has no run to show. The key is spent on nothing rather than on a
        // menu the player did not ask for.
        [UnityTest]
        public IEnumerator R_AtCampDoesNothing()
        {
            yield return LoadHub();

            yield return Press(R);
            Assert.IsFalse(_menu.IsOpen, "R opened a menu at camp, where there is no RUN tab");

            yield return Press(C);
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab);
            yield return Press(R);
            Assert.IsTrue(_menu.IsOpen, "R closed or moved the menu it has no tab in");
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab, "R switched to a tab camp does not have");
        }

        // ---- the pad's View button --------------------------------------------------

        [UnityTest]
        public IEnumerator ViewAtCampOpensTheCharacterTabAndClosesItAgain()
        {
            yield return LoadHub();

            yield return Press(View);
            Assert.IsTrue(_menu.IsOpen, "View did not open the menu at camp");
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab);

            yield return Press(View);
            Assert.IsFalse(_menu.IsOpen, "View on its own tab should close the menu");
        }

        [UnityTest]
        public IEnumerator ViewInADescentOpensTheRunTab()
        {
            yield return LoadMap();

            yield return Press(View);

            Assert.IsTrue(_menu.IsOpen, "View did not open the menu on the map");
            Assert.AreEqual(SystemMenuTab.Run, _menu.SelectedTab);
        }

        [UnityTest]
        public IEnumerator ViewInAFightOpensTheRunTab()
        {
            yield return LoadFight();

            yield return Press(View);

            Assert.IsTrue(_menu.IsOpen, "View did not open the menu over a fight");
            Assert.AreEqual(SystemMenuTab.Run, _menu.SelectedTab);
        }

        // ---- absorbed by a modal ----------------------------------------------------

        // Same rule as Start: a modal with its own context absorbs the press,
        // so the screen beneath cannot open a menu on top of it.
        [UnityTest]
        public IEnumerator HotkeysAreAbsorbedWhileAModalIsTop()
        {
            yield return LoadHub();

            var hub = Object.FindAnyObjectByType<HubController>();
            hub.SetGlossary(true);
            yield return null;
            Assert.IsTrue(hub.GlossaryIsOpen, "fixture: the glossary did not open");

            yield return Press(C);
            yield return Press(P);
            yield return Press(I);
            yield return Press(View);

            Assert.IsFalse(_menu.IsOpen, "a hotkey reached the menu through an open glossary");
            Assert.IsTrue(hub.GlossaryIsOpen, "a hotkey closed the glossary");

            hub.SetGlossary(false);
            yield return null;

            yield return Press(C);
            Assert.IsTrue(_menu.IsOpen, "fixture: the hotkey should work again once the modal is gone");
        }

        // ---- the map and the fight own the same hotkeys ----------------------------

        [UnityTest]
        public IEnumerator TheMapAnswersC_I_AndP()
        {
            yield return LoadMap();

            yield return Press(C);
            Assert.IsTrue(_menu.IsOpen, "C did not open the menu on the map");
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab);

            yield return Press(C);
            Assert.IsFalse(_menu.IsOpen);

            yield return Press(I);
            Assert.IsTrue(_menu.IsOpen);
            Assert.IsTrue(Dossier().IsPackShown, "I did not bring the pack up on the map");

            yield return Press(P);
            Assert.AreEqual(SystemMenuTab.Party, _menu.SelectedTab);
        }

        [UnityTest]
        public IEnumerator TheFightAnswersC_I_AndP()
        {
            yield return LoadFight();

            yield return Press(C);
            Assert.IsTrue(_menu.IsOpen, "C did not open the menu over the fight");
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab);

            yield return Press(C);
            Assert.IsFalse(_menu.IsOpen);

            yield return Press(I);
            Assert.IsTrue(_menu.IsOpen);
            Assert.IsTrue(Dossier().IsPackShown, "I did not bring the pack up over the fight");

            yield return Press(P);
            Assert.AreEqual(SystemMenuTab.Party, _menu.SelectedTab);
        }

        // ---- the reopen rule --------------------------------------------------------

        [UnityTest]
        public IEnumerator TheMenuReopensOnTheLastGameTabAndNeverOnASystemTab()
        {
            yield return LoadHub();

            yield return Press(P);
            _menu.Close();
            yield return null;

            yield return StartPress();
            Assert.IsTrue(_menu.IsOpen);
            Assert.AreEqual(SystemMenuTab.Party, _menu.SelectedTab, "Start did not reopen on the last game tab");

            _menu.Select(SystemMenuTab.Options);
            _menu.Close();
            yield return null;

            yield return StartPress();
            Assert.AreEqual(SystemMenuTab.Party, _menu.SelectedTab,
                "a visit to Options was remembered; system tabs must never be");
        }

        [UnityTest]
        public IEnumerator AClickedGameTabIsRememberedToo()
        {
            yield return LoadHub();

            yield return StartPress();
            _menu.Select(SystemMenuTab.Party);
            _menu.Close();
            yield return null;

            yield return StartPress();
            Assert.AreEqual(SystemMenuTab.Party, _menu.SelectedTab);
        }

        [UnityTest]
        public IEnumerator TheMemoryIsPerContextAndSurvivesSceneLoads()
        {
            yield return LoadHub();
            yield return Press(P);
            _menu.Close();
            yield return null;

            yield return LoadMap();
            yield return StartPress();
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab,
                "the descent inherited the camp's memory; each context keeps its own");
            _menu.Select(SystemMenuTab.Run);
            _menu.Close();
            yield return null;

            yield return LoadHub();
            yield return StartPress();
            Assert.AreEqual(SystemMenuTab.Party, _menu.SelectedTab,
                "the camp's memory did not survive two scene loads");
            _menu.Close();
            yield return null;

            yield return LoadMap();
            yield return StartPress();
            Assert.AreEqual(SystemMenuTab.Run, _menu.SelectedTab,
                "the descent's memory did not survive a scene load");
        }

        // Plan C7: with no memory, a fight reopens on RUN, where the menu is
        // opened to look something up.
        [UnityTest]
        public IEnumerator AFightOpensOnRunWhenNothingIsRemembered()
        {
            yield return LoadFight();

            yield return StartPress();

            Assert.IsTrue(_menu.IsOpen, "Start did not open the menu over the fight");
            Assert.AreEqual(SystemMenuTab.Run, _menu.SelectedTab,
                "a fight with no memory should open on RUN, not on the squad");
        }

        [UnityTest]
        public IEnumerator AHotkeyOverridesTheRememberedTabAndUpdatesIt()
        {
            yield return LoadHub();

            yield return Press(P);
            _menu.Close();
            yield return null;

            yield return Press(C);
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab, "the hotkey lost to the memory");
            _menu.Close();
            yield return null;

            yield return StartPress();
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab, "the hotkey did not update the memory");
        }

        [UnityTest]
        public IEnumerator OpenWithAnAbsentTabFallsBackToTheReopenRule()
        {
            yield return LoadHub();

            // Run is a run-only tab; the camp has none to show.
            _menu.Open(SystemMenuTab.Run);
            yield return null;

            Assert.IsTrue(_menu.IsOpen);
            Assert.AreEqual(SystemMenuTab.Character, _menu.SelectedTab,
                "the hub showed a pane no tab button leads to");
        }

        // ---- the entry API's targets ------------------------------------------------

        [UnityTest]
        public IEnumerator OpenWithACharacterTargetSelectsThatCharacterInTheDossier()
        {
            yield return LoadHub();

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "owl", "sheep", "bear" };

            _menu.Open(SystemMenuTab.Character, MenuTarget.Character("bear"));
            yield return null;

            var name = Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude)
                .FirstOrDefault(t => t.name == "DossierName");
            Assert.IsNotNull(name, "the dossier has no name label");
            Assert.AreEqual("Bjorn", name.text, "the dossier did not select the character the target names");
        }

        [UnityTest]
        public IEnumerator OpenIgnoresATargetTheTabHasNoPlaceFor()
        {
            yield return LoadHub();

            Assert.DoesNotThrow(() => _menu.Open(SystemMenuTab.Party, MenuTarget.Glossary("anything")));
            Assert.AreEqual(SystemMenuTab.Party, _menu.SelectedTab);
        }
    }
}
