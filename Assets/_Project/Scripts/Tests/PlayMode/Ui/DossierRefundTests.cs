using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // The dossier's refund minus, and which copies of the screen show it.
    //
    // Only the hub's copy is live -- the same asymmetry CharacterSheetInFight
    // Tests pins for the equip buttons, applied to the newer control beside
    // them. A build revised mid-run would be a free undo on every fight
    // decision the run has made so far, so the map and fight copies must both
    // refuse it, for the two different reasons each already refuses equip
    // changes: the map is mid-run (inDescent), the fight is mid-battle
    // (lockedForFight).
    public class DossierRefundTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-dossierrefund-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // All six, rather than guessing which score cell 0 sorts to -- the
        // grid order is per-character and this is not about the grid, it is
        // about whether the button shows up at all where it should.
        private static void InvestOnePointInEveryScore(Character character)
        {
            character.unspentStatPoints = 0;
            character.investedAbilityScores = new AbilityScoreBlock
            {
                strength = 1,
                dexterity = 1,
                constitution = 1,
                wisdom = 1,
                intelligence = 1,
                charisma = 1,
            };
        }

        private static Character First() =>
            SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);

        private static Button[] Minuses(GameObject scope) =>
            Enumerable.Range(0, 6)
                .Select(i => scope.GetComponentsInChildren<Button>(includeInactive: true)
                    .FirstOrDefault(b => b.name == $"DossierAttrMinus{i}"))
                .ToArray();

        [UnityTest]
        public IEnumerator TheHubCopyShowsTheMinusForAnInvestedScore()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the hub has no SystemMenuController");
            menu.Open();
            menu.Select(0);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the hub has no dossier controller");

            InvestOnePointInEveryScore(First());
            dossier.Refresh();
            yield return null;

            var minuses = Minuses(menu.gameObject);
            for (int i = 0; i < minuses.Length; i++)
            {
                Assert.IsNotNull(minuses[i], $"cell {i} drew no DossierAttrMinus{i}");
                Assert.IsTrue(minuses[i].gameObject.activeSelf,
                    $"the hub's dossier should show the minus on cell {i}, which has a point invested");
            }
        }

        [UnityTest]
        public IEnumerator TheMapCopyDoesNotShowTheMinus()
        {
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the map has no SystemMenuController");
            menu.Open();
            menu.Select(0);
            yield return null;

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the map has no dossier controller");

            InvestOnePointInEveryScore(First());
            dossier.Refresh();
            yield return null;

            var minuses = Minuses(menu.gameObject);
            for (int i = 0; i < minuses.Length; i++)
            {
                Assert.IsNotNull(minuses[i], $"cell {i} drew no DossierAttrMinus{i}");
                Assert.IsFalse(minuses[i].gameObject.activeSelf,
                    $"the map's dossier is mid-run and must not offer a refund on cell {i}");
            }
        }

        [UnityTest]
        public IEnumerator TheFightCopyDoesNotShowTheMinus()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>(FindObjectsInactive.Include);
            Assert.IsNotNull(fight, "the Fight scene has no FightController");

            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(dossier, "the fight scene has no dossier controller");
            Assert.IsTrue(dossier.EquipLocked, "fixture: the fight's copy should be locked");

            InvestOnePointInEveryScore(First());

            fight.ToggleCharacterSheet(inventory: false);
            yield return null;

            var minuses = Minuses(dossier.gameObject);
            for (int i = 0; i < minuses.Length; i++)
            {
                Assert.IsNotNull(minuses[i], $"cell {i} drew no DossierAttrMinus{i}");
                Assert.IsFalse(minuses[i].gameObject.activeSelf,
                    $"the fight's dossier is locked and must not offer a refund on cell {i}");
            }
        }
    }
}
