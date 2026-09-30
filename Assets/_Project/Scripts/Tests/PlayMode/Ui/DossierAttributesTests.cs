using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // ATTRIBUTES (plan C17; phase 5d redesigns it): one row per score, its
    // "-" and "+" committing on press (no pending state, plan C5), a header
    // saying how many points wait, and the refund lock said where it applies.
    //
    // The rows are sorted highest-first per character, so a row is found by
    // the score its label names, never by index.
    public class DossierAttributesTests
    {
        private string _root;
        private SystemMenuController _menu;
        private CharacterDossierController _dossier;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-dossier-attr-" + System.Guid.NewGuid().ToString("N"));
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

        private Character Shown => SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == _dossier.CharacterId);

        private IEnumerator OpenAttributesAtCamp(int points)
        {
            yield return SharedScene.Ensure("Hub");
            _menu = DossierPane.Menu();
            _dossier = DossierPane.Dossier();
            DossierPane.Rest();

            _menu.Open(SystemMenuTab.Character);
            yield return null;
            DossierPane.SelectFirstChip(_dossier);

            Shown.unspentStatPoints = points;
            Shown.investedAbilityScores = default;
            _dossier.ShowSection(CharacterSection.Attributes, focusRow: true);
            _dossier.Refresh();
            yield return null;
        }

        // The row whose label names `score`, by its short name.
        private static int RowOf(AbilityScore score) =>
            Enumerable.Range(0, 6).First(i => DossierPane.TextOf($"DossierAttrName{i}") == AbilityScores.ShortName(score));

        private string Notice => DossierPane.TextOf("SystemFooterNotice");

        [UnityTest]
        public IEnumerator ThePlusSpendsIntoTheScoreNamedOnItsRow()
        {
            yield return OpenAttributesAtCamp(points: 2);

            int row = RowOf(AbilityScore.Constitution);
            DossierPane.Press($"DossierAttrPlus{row}");

            Assert.AreEqual(1, Shown.unspentStatPoints, "the plus did not spend a point");
            Assert.AreEqual(1, Shown.investedAbilityScores[AbilityScore.Constitution],
                "the point did not land in the score its row names");
        }

        // The rows are FROZEN while one character is on screen, so pressing
        // the same plus twice spends both points into one score even when the
        // first point lifts it past its neighbour.
        [UnityTest]
        public IEnumerator PressingThePlusTwiceSpendsBothPointsIntoOneScore()
        {
            yield return OpenAttributesAtCamp(points: 2);

            var before = Enumerable.Range(0, 6).Select(i => DossierPane.TextOf($"DossierAttrName{i}")).ToArray();

            DossierPane.Press("DossierAttrPlus5");
            DossierPane.Press("DossierAttrPlus5");

            var after = Enumerable.Range(0, 6).Select(i => DossierPane.TextOf($"DossierAttrName{i}")).ToArray();
            CollectionAssert.AreEqual(before, after, "the rows reshuffled under the cursor");

            Assert.AreEqual(0, Shown.unspentStatPoints);
            Assert.AreEqual(1, AbilityScores.All.Count(s => Shown.investedAbilityScores[s] == 2),
                "the two points did not land in one score");
        }

        [UnityTest]
        public IEnumerator TheHeaderSaysHowManyPointsWait()
        {
            yield return OpenAttributesAtCamp(points: 3);
            Assert.AreEqual("3 points to spend", DossierPane.TextOf("DossierAttributesUnspent"));

            Shown.unspentStatPoints = 1;
            _dossier.Refresh();
            Assert.AreEqual("1 point to spend", DossierPane.TextOf("DossierAttributesUnspent"));

            Shown.unspentStatPoints = 0;
            _dossier.Refresh();
            Assert.AreEqual("", DossierPane.TextOf("DossierAttributesUnspent"));
        }

        // A refused control stays drawn and focusable, and pressing it says
        // why (plan C3).
        [UnityTest]
        public IEnumerator WithNoPointsThePlusStaysAndSaysSo()
        {
            yield return OpenAttributesAtCamp(points: 0);

            Assert.IsTrue(DossierPane.Control("DossierAttrPlus0").gameObject.activeInHierarchy, "the plus vanished");
            DossierPane.Press("DossierAttrPlus0");

            Assert.AreEqual("No points to spend.", Notice);
        }

        [UnityTest]
        public IEnumerator AtCampTheMinusRefundsAndTheLockNoteIsHidden()
        {
            yield return OpenAttributesAtCamp(points: 1);
            int row = RowOf(AbilityScore.Strength);
            DossierPane.Press($"DossierAttrPlus{row}");
            Assert.AreEqual(0, Shown.unspentStatPoints, "fixture: the point did not go in");

            DossierPane.Press($"DossierAttrMinus{row}");

            Assert.AreEqual(1, Shown.unspentStatPoints, "the camp's minus did not refund");
            Assert.AreEqual(0, Shown.investedAbilityScores[AbilityScore.Strength]);
            Assert.IsFalse(DossierPane.Control("DossierAttrPlus0").transform.parent.parent
                .Find("DossierAttributesLockNote").gameObject.activeSelf, "the camp shows the refund lock");
        }

        [UnityTest]
        public IEnumerator InADescentTheMinusIsRefusedAndTheLockNoteSaysWhere()
        {
            RunManager.StartRun(4242UL);
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _menu = DossierPane.Menu();
            _dossier = DossierPane.Dossier();
            _menu.Open(SystemMenuTab.Character);
            yield return null;

            Shown.unspentStatPoints = 1;
            Assert.IsTrue(Shown.Invest(AbilityScore.Wisdom), "fixture: could not invest");
            _dossier.ShowSection(CharacterSection.Attributes, focusRow: false);
            _dossier.Refresh();
            yield return null;

            DossierPane.Press($"DossierAttrMinus{RowOf(AbilityScore.Wisdom)}");

            Assert.AreEqual(1, Shown.investedAbilityScores[AbilityScore.Wisdom], "a descent refunded a point");
            Assert.AreEqual("Refunds at camp", Notice);
            Assert.AreEqual("Refunds at camp", DossierPane.TextOf("DossierAttributesLockNote"));
            Assert.IsTrue(Resources.FindObjectsOfTypeAll<TMPro.TMP_Text>()
                .First(t => t.name == "DossierAttributesLockNote" && t.gameObject.scene.IsValid()).gameObject.activeSelf);
        }

        // Focus on a stepper lights the stats its score feeds: Constitution
        // feeds Max Health.
        [UnityTest]
        public IEnumerator FocusOnAStepperLightsTheStatsItsScoreFeeds()
        {
            yield return OpenAttributesAtCamp(points: 0);

            DossierPane.Select($"DossierAttrPlus{RowOf(AbilityScore.Constitution)}");
            yield return null;

            int health = System.Array.IndexOf(SheetStats.Derived, SheetStat.MaxHealth);
            int speed = System.Array.IndexOf(SheetStats.Derived, SheetStat.Speed);
            var highlights = Resources.FindObjectsOfTypeAll<RectTransform>()
                .Where(r => r.gameObject.scene.IsValid() && r.name.StartsWith("DossierStatHighlight"))
                .ToDictionary(r => r.name, r => r.gameObject.activeSelf);

            Assert.IsTrue(highlights[$"DossierStatHighlight{health}"], "Max Health is not lit for Constitution");
            Assert.IsFalse(highlights[$"DossierStatHighlight{speed}"], "Speed is lit for Constitution");
        }
    }
}
