using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The character sheet is reachable during a fight, and inert while it is.
    //
    // Two halves of one rule and both need pinning, because they fail in
    // opposite directions and each looks fine on its own: a sheet that is
    // missing mid-fight is a feature that quietly is not there, and a sheet
    // that is editable mid-fight lets a player re-plate between swings, which
    // turns every hard fight into a loadout puzzle.
    //
    // The lock is a serialized bool decided at BUILD time -- the hub's copy is
    // live, the fight's is locked -- so what this really checks is that
    // ScreenRegistry wired the right one to the right scene. Getting that
    // backwards would compile, run, and be wrong in both places at once.
    public class CharacterSheetInFightTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-sheetfight-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            // Every global this fixture touched, plus the ones it did not --
            // one call, so the list cannot go stale here while it grows
            // somewhere else. See TestGlobals.
            TestGlobals.ResetAll();

            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // The dossier inside the system menu, which replaced the paperdoll
        // this used to find. The rule it pins did not change: the fight's copy
        // must exist and must be inert.
        private static CharacterDossierController Sheet()
        {
            return Object.FindObjectsByType<CharacterDossierController>(FindObjectsInactive.Include)
                .FirstOrDefault();
        }


        [UnityTest]
        public IEnumerator TheFightSceneCarriesASheetAndItIsLocked()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var sheet = Sheet();

            Assert.IsNotNull(sheet,
                "the character sheet is not reachable during a fight -- it was never mounted in the scene");
            Assert.IsTrue(sheet.EquipLocked,
                "the fight's copy of the sheet is EDITABLE: gear could be swapped between swings");
        }

        // Opening it must not require leaving the fight, and it must not be up
        // by default -- a modal covering the stage on scene load would hide the
        // fight it belongs to.
        [UnityTest]
        public IEnumerator TheSheetStartsClosedAndOpensOnDemand()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>(FindObjectsInactive.Include);
            Assert.IsNotNull(fight);

            Assert.IsFalse(fight.CharacterSheetIsOpen, "the sheet is covering the stage on open");

            fight.ToggleCharacterSheet(inventory: false);
            yield return null;
            Assert.IsTrue(fight.CharacterSheetIsOpen, "C did not open the sheet in a fight");

            fight.ToggleCharacterSheet(inventory: false);
            yield return null;
            Assert.IsFalse(fight.CharacterSheetIsOpen, "pressing the same key again did not close it");
        }

        // The switch-vs-close rule this used to pin ("pressing the other
        // pane's key switches panes rather than closing") was retired when
        // Character and Inventory merged into one tab -- correctly, at the
        // time, since there was nothing left to switch between. It came back
        // when the pack argument was wired to CharacterDossierController.
        // ShowPack: I and C each mean a real state again (pack up / pack
        // down), so the same rule the old test asserted applies again, just
        // retargeted at the pack instead of at a tab. A user report is what
        // caught the gap between the two: pressing I then C closed the whole
        // sheet instead of switching the pack down, because the toggle was
        // only ever comparing open-vs-closed.
        [UnityTest]
        public IEnumerator BothKeysDriveTheSameScreenAndTheOtherOneSwitchesThePack()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>(FindObjectsInactive.Include);
            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the fight scene lost its dossier");

            fight.ToggleCharacterSheet(inventory: false);
            yield return null;
            Assert.IsTrue(fight.CharacterSheetIsOpen, "C did not open the character screen");
            Assert.IsFalse(dossier.IsPackShown, "C should open WITHOUT the pack");

            // THE OTHER KEY SWITCHES, it does not close. This is the exact
            // gesture the report named: I while the sheet is already open
            // (via C) should bring the pack up and leave the sheet open, not
            // close the whole screen.
            fight.ToggleCharacterSheet(inventory: true);
            yield return null;
            Assert.IsTrue(fight.CharacterSheetIsOpen,
                "pressing the OTHER key while open should switch the pack, not close the sheet");
            Assert.IsTrue(dossier.IsPackShown, "I should have brought the pack up");

            // THE SAME KEY, ASKING FOR WHAT IS ALREADY SHOWING, closes it --
            // this is the one case that still has to behave like a plain
            // toggle, or there would be no keyboard way to close the sheet at
            // all short of Escape.
            fight.ToggleCharacterSheet(inventory: true);
            yield return null;
            Assert.IsFalse(fight.CharacterSheetIsOpen,
                "pressing the SAME key again, with nothing left to switch to, should close the sheet");
        }

        // ---- the clock the sheet borrows --------------------------------------

        // CLOSING THE SHEET HAS TO GIVE THE PAUSE BACK.
        //
        // The sheet IS the system menu, and that menu pauses the game while it
        // is up -- Time.timeScale = 0, restored by its Close(). SheetPanel
        // opened through the controller and closed by deactivating the panel
        // behind its back, so the pause was taken and never returned: every
        // C-then-C left the clock stopped.
        //
        // Nothing on screen says so. The reward screen the player is looking
        // at runs on unscaled time and behaves perfectly; the bill arrives one
        // scene later, on the map, whose walk to the next room counts in
        // Time.deltaTime and therefore never finishes. Reported as "I cannot
        // continue to the next step after winning a battle and then opening
        // the inventory".
        //
        // Asserted on the CLOCK rather than on the map, deliberately: the map
        // is one victim of a stopped clock and every scaled coroutine in the
        // game is another, so the clock is the claim worth pinning.
        [UnityTest]
        public IEnumerator ClosingTheSheetWithTheSameKeyRestoresTheClock()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>(FindObjectsInactive.Include);
            Assert.IsNotNull(fight);

            Assert.AreEqual(1f, Time.timeScale, 0.0001f, "fixture: the clock was not running to begin with");

            fight.ToggleCharacterSheet(inventory: false);
            yield return null;
            Assert.AreEqual(0f, Time.timeScale, "opening the sheet did not pause the game");

            fight.ToggleCharacterSheet(inventory: false);
            yield return null;

            Assert.IsFalse(fight.CharacterSheetIsOpen, "the sheet did not close");
            Assert.AreEqual(1f, Time.timeScale, 0.0001f,
                "the sheet closed but the game is still paused -- every scaled coroutine from here " +
                "on is frozen, including the map walk that leads to the next room");
        }

        // The other key closes it too, by the switch-then-close route, and the
        // clock has to come back the same way. Written out rather than folded
        // into the case above because the two reach Set(open: false) from
        // different branches of Toggle, and it is the CLOSE that was broken --
        // a fix that only covered one of them would look right in one test.
        [UnityTest]
        public IEnumerator ClosingTheSheetAfterSwitchingPacksAlsoRestoresTheClock()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>(FindObjectsInactive.Include);
            Assert.IsNotNull(fight);

            fight.ToggleCharacterSheet(inventory: false);
            yield return null;
            fight.ToggleCharacterSheet(inventory: true);
            yield return null;
            Assert.IsTrue(fight.CharacterSheetIsOpen, "fixture: the other key closed it instead of switching");

            fight.ToggleCharacterSheet(inventory: true);
            yield return null;

            Assert.IsFalse(fight.CharacterSheetIsOpen, "the sheet did not close");
            Assert.AreEqual(1f, Time.timeScale, 0.0001f, "the sheet closed but the game is still paused");
        }

        // The hub's copy is the live one. Same tree, opposite setting, and the
        // whole design rests on ScreenRegistry giving each scene the right one.
        [UnityTest]
        public IEnumerator TheHubsCopyIsNotLocked()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var sheet = Sheet();

            Assert.IsNotNull(sheet, "the hub lost its character sheet");
            Assert.IsFalse(sheet.EquipLocked,
                "the hub's sheet is locked -- equipment cannot be changed anywhere");
        }
    }
}
