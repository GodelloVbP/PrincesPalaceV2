using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;

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
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static CharacterOverlayController Sheet()
        {
            return Object.FindObjectsByType<CharacterOverlayController>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
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

        // Pressing the other pane's key while open switches panes rather than
        // closing: the reader wanted the other half, not the fight back.
        [UnityTest]
        public IEnumerator TheOtherKeySwitchesPanesRatherThanClosing()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>(FindObjectsInactive.Include);
            var sheet = Sheet();

            fight.ToggleCharacterSheet(inventory: false);
            yield return null;
            Assert.IsFalse(sheet.ShowingInventory, "C opened the bag rather than the character");

            fight.ToggleCharacterSheet(inventory: true);
            yield return null;

            Assert.IsTrue(fight.CharacterSheetIsOpen, "I closed the sheet instead of switching to the bag");
            Assert.IsTrue(sheet.ShowingInventory, "I did not switch to the bag");
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
