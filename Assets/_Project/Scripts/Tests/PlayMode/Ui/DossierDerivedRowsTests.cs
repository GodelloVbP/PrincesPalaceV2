using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // WHICH DERIVED ROWS A CHARACTER HAS (plan C21, spec 3.2): the signature
    // row is not drawn for a character with no signature, and the regen row is
    // not drawn for one whose primary pool is Fixed -- Bjorn's Fury takes its
    // income from no source but its own authoring, so a "Mana Regen" beside it
    // would name an income he never has. Both in the ATTRIBUTES column and in
    // the LOADOUT compare column, and the rows that remain close up: a hole in
    // the middle of a list reads as a missing value.
    public class DossierDerivedRowsTests
    {
        private const string Odette = "owl";
        private const string Bjorn = "bear";

        private string _root;
        private CharacterDossierController _dossier;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-derived-rows-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            SharedScene.AfterTest();
            TestGlobals.ResetAll();
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private IEnumerator Open(string who, CharacterSection section)
        {
            yield return SharedScene.Ensure("Hub");
            var menu = DossierPane.Menu();
            _dossier = DossierPane.Dossier();
            DossierPane.Rest();

            menu.Open(SystemMenuTab.Character);
            yield return null;
            Assert.IsTrue(_dossier.SelectCharacter(who), $"fixture: {who} is not on the strip");
            _dossier.ShowSection(section, focusRow: false);
            EventSystem.current.SetSelectedGameObject(null);
            _dossier.Refresh();
            yield return null;
        }

        private static int RowOf(SheetStat stat) => SheetStats.Derived.ToList().IndexOf(stat);

        // Any node of the pane by name: the highlight is a plain rectangle, not a label.
        private static float Y(string name)
        {
            var node = Resources.FindObjectsOfTypeAll<RectTransform>()
                .FirstOrDefault(r => r.name == name && r.gameObject.scene.IsValid());
            Assert.IsNotNull(node, $"the pane drew no node named {name}");
            return node.anchoredPosition.y;
        }

        private static bool Drawn(string name) => DossierPane.Label(name).gameObject.activeSelf;

        // ---- ATTRIBUTES' derived column -------------------------------------------------------

        [UnityTest]
        public IEnumerator OdetteHasARegenRowAndBjornDoesNot()
        {
            yield return Open(Odette, CharacterSection.Attributes);
            int regen = RowOf(SheetStat.ManaRegen);

            Assert.IsTrue(Drawn($"DossierStatName{regen}"), "Odette's mana regenerates: the row must be drawn");
            Assert.AreEqual("Mana Regen", DossierPane.TextOf($"DossierStatName{regen}"));
            Assert.IsTrue(Drawn($"DossierStatValue{regen}"));
            float regenY = Y($"DossierStatName{regen}");
            float nextY = Y($"DossierStatName{RowOf(SheetStat.PhysicalDefense)}");
            Assert.Less(nextY, regenY, "fixture: the row after regen should sit below it");

            yield return Open(Bjorn, CharacterSection.Attributes);

            Assert.IsFalse(Drawn($"DossierStatName{regen}"), "Bjorn's Fury never regenerates: the name is drawn");
            Assert.IsFalse(Drawn($"DossierStatValue{regen}"), "Bjorn's Fury never regenerates: the value is drawn");
            Assert.AreEqual(regenY, Y($"DossierStatName{RowOf(SheetStat.PhysicalDefense)}"), 0.01f,
                "the rows after the missing one did not close up into its place");
            Assert.AreEqual(regenY, Y($"DossierStatValue{RowOf(SheetStat.PhysicalDefense)}"), 0.01f);
            Assert.AreEqual(regenY, Y($"DossierStatHighlight{RowOf(SheetStat.PhysicalDefense)}"), 0.01f);
        }

        // Going back to a character who has the row puts everything where the
        // authored tree had it.
        [UnityTest]
        public IEnumerator TheRowsReturnWhenTheNextCharacterHasThemAgain()
        {
            yield return Open(Odette, CharacterSection.Attributes);
            float authored = Y($"DossierStatName{RowOf(SheetStat.PhysicalDefense)}");

            yield return Open(Bjorn, CharacterSection.Attributes);
            Assert.AreNotEqual(authored, Y($"DossierStatName{RowOf(SheetStat.PhysicalDefense)}"), "fixture: nothing closed up");

            yield return Open(Odette, CharacterSection.Attributes);
            Assert.AreEqual(authored, Y($"DossierStatName{RowOf(SheetStat.PhysicalDefense)}"), 0.01f);
        }

        // ---- the compare column ------------------------------------------------------------------

        private static string[] CompareNames() =>
            Enumerable.Range(0, SheetStats.Derived.Length)
                .Select(i => DossierPane.TextOf($"DossierCompareStat{i}Name"))
                .ToArray();

        [UnityTest]
        public IEnumerator TheCompareColumnListsRegenForOdetteAndNotForBjorn()
        {
            yield return Open(Odette, CharacterSection.Loadout);
            CollectionAssert.Contains(CompareNames(), "Mana Regen");

            yield return Open(Bjorn, CharacterSection.Loadout);
            var bjorn = CompareNames();

            CollectionAssert.DoesNotContain(bjorn, "Mana Regen", "Bjorn's compare column names an income his pool never takes");

            // Closed up: no blank row sits between two that read.
            int lastNamed = System.Array.FindLastIndex(bjorn, n => n != "");
            for (int i = 0; i <= lastNamed; i++) Assert.AreNotEqual("", bjorn[i], $"a blank row at {i} splits the list");
        }
    }
}
