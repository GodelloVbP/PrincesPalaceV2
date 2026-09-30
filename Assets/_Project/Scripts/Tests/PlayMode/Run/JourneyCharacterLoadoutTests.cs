using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The spec's press counts for the LOADOUT validation tasks (spec section
    // 10, tasks 4 and 5), on a pad, from the plan's fixed start: Shawn,
    // Odette and Bjorn seated (Odette at the rear, so she is the strip's
    // first chip), a known pack, one unassigned book, menu memory cleared, on
    // the Map. A count above the spec's is a regression.
    //
    // Every press goes through the REAL dispatcher (JourneyFixture); nothing
    // calls a controller.
    public class JourneyCharacterLoadoutTests : JourneyFixture
    {
        private string _root;

        private const string Book = "frost_flare";

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-loadout-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
            MenuMemory.Clear();
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private ItemDefinition _boots;
        private int _presses;

        // The fixed start. The pack holds the boots and nothing else; nobody
        // wears anything, so Shawn's Shoes slot is empty.
        private IEnumerator FixedStartOnTheMap()
        {
            RunManager.StartRun(4242UL);

            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "bear", "sheep", "owl" };
            foreach (var member in save.roster) member.equipment.Clear();

            _boots = ContentDatabase.Equippables.First(i => i.equipSlot == EquipmentSlot.Shoes);
            save.stockpiledItems.Clear();
            InventoryOps.Add(save.stockpiledItems, _boots.id);

            RunManager.Run.learnedSpells = new List<LearnedSpellEntry>();
            RunManager.Run.unassignedSpellBooks = new List<string> { Book };
            SaveSlotManager.SaveCurrent();

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();
            yield return null;

            _presses = 0;
        }

        private IEnumerator Count(IEnumerator press)
        {
            _presses++;
            yield return press;
        }

        private static Character Of(string id) => SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == id);

        // Start, RB (Shawn), Right (detail), Down x3 and Right to Shoes,
        // A (armed), A (equip): 9.
        [UnityTest]
        public IEnumerator Task4_BetterBootsOnShawn_TakesNinePresses()
        {
            yield return FixedStartOnTheMap();

            yield return Count(PressSystemMenu());
            AssertSelectedName("DossierSection0", "Start lands on the rail's selected section, LOADOUT");

            yield return Count(PressTabNext());
            Assert.AreEqual("Shawn", Node("DossierName").GetComponent<TMPro.TMP_Text>().text, "RB did not reach Shawn");

            yield return Count(MoveRight());
            AssertSelectedName("DossierSlotHead", "Right enters the LOADOUT at Head");

            yield return Count(MoveDown());
            yield return Count(MoveDown());
            yield return Count(MoveDown());
            yield return Count(MoveRight());
            AssertSelectedName("DossierSlotShoes", "Down x3 and Right reach Shoes");

            yield return Count(PressSubmit());
            AssertSelectedName("DossierPackCell0", "A on the empty Shoes arms it and focus goes to the boots");

            yield return Count(PressSubmit());

            Assert.AreEqual(_boots.id, Of("sheep").equipment.Get(EquipmentSlot.Shoes), "Shawn is not wearing the boots");
            Assert.AreEqual(9, _presses, "the spec counts nine presses for task 4");
        }

        // Start, Right, Down x4 to book slot 1, A, A: 8.
        [UnityTest]
        public IEnumerator Task5_TheBookOnOdette_TakesEightPresses()
        {
            yield return FixedStartOnTheMap();

            yield return Count(PressSystemMenu());
            Assert.AreEqual("Odette", Node("DossierName").GetComponent<TMPro.TMP_Text>().text,
                "the strip does not open on Odette, the rear seat");

            yield return Count(MoveRight());
            yield return Count(MoveDown());
            yield return Count(MoveDown());
            yield return Count(MoveDown());
            yield return Count(MoveDown());
            AssertSelectedName("DossierBook0", "Down x4 from Head reaches the first book slot");

            yield return Count(PressSubmit());
            yield return Count(PressSubmit());

            Assert.IsTrue(RunManager.Run.learnedSpells.Any(e => e.characterId == "owl" && e.slot == 0 && e.skillId == Book),
                "the book is not in Odette's first slot");
            CollectionAssert.DoesNotContain(RunManager.Run.unassignedSpellBooks, Book);
            Assert.AreEqual(8, _presses, "the spec counts eight presses for task 5");
        }
    }
}
