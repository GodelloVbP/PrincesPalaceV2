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
            return Object.FindObjectsByType<CharacterDossierController>(
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

        // The test that used to sit here pinned "pressing the other pane's key
        // switches panes rather than closing". The design pass merged Character
        // and Inventory into ONE tab, so there is no other pane to switch to
        // and both keys are now the same gesture. Deleted rather than rewritten
        // to assert nothing, and recorded here so its absence reads as a
        // decision instead of an oversight.

        [UnityTest]
        public IEnumerator BothKeysDriveTheSameScreen()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>(FindObjectsInactive.Include);

            fight.ToggleCharacterSheet(inventory: false);
            yield return null;
            Assert.IsTrue(fight.CharacterSheetIsOpen, "C did not open the character screen");

            // AND I CLOSES IT. That is a real behaviour change and it is the
            // design's, not an oversight: Character and Inventory used to be two
            // panes and the other key switched between them, so it had somewhere
            // else to go. They are one tab now -- the dossier was always both
            // halves -- so both keys are the same gesture and the second press
            // is a second press.
            fight.ToggleCharacterSheet(inventory: true);
            yield return null;
            Assert.IsFalse(fight.CharacterSheetIsOpen,
                "I left the screen open, so the two keys are still behaving as two doors");
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
