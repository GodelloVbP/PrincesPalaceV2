using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Content;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // ATTRIBUTES (plan C17, spec 3.2): one row per score, its minus and plus
    // right beside the value and committing on press (no pending state, plan
    // C5), a header saying how many points wait and why a change is refused,
    // and the derived stats showing before -> after under a focused stepper.
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
            Assert.AreEqual("", DossierPane.TextOf("DossierAttributesLockNote"), "the camp shows the refund lock");
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

        // ---- the preview: before -> after while a stepper is focused (spec 3.2) ------

        private static TMPro.TMP_Text StatValue(SheetStat stat) =>
            DossierPane.Label($"DossierStatValue{System.Array.IndexOf(SheetStats.Derived, stat)}");

        private int HealthNow => ContentDatabase.EffectiveStats(Shown).maxHealth;

        // A plus on Constitution would add 20 Max Health and 2 Physical DEF
        // (AbilityDerivation, pinned in its own tests): the row says what it is
        // and what it would be, old first.
        [UnityTest]
        public IEnumerator AFocusedPlusShowsWhatItWouldMakeOfTheRowsItFeeds()
        {
            yield return OpenAttributesAtCamp(points: 1);
            int health = HealthNow;

            DossierPane.Select($"DossierAttrPlus{RowOf(AbilityScore.Constitution)}");
            yield return null;

            Assert.AreEqual($"{health} → {health + 20}", StatValue(SheetStat.MaxHealth).text,
                "the lit Max Health row does not read before, then after");
            StringAssert.Contains("→", StatValue(SheetStat.PhysicalDefense).text, "Physical DEF is fed by Constitution too");
            Assert.Greater(StatValue(SheetStat.MaxHealth).color.g, StatValue(SheetStat.MaxHealth).color.r, "a gain is not green");

            // Rows Constitution does not feed keep their plain figure.
            StringAssert.DoesNotContain("→", StatValue(SheetStat.Speed).text);
            StringAssert.DoesNotContain("→", StatValue(SheetStat.Attack).text);
        }

        [UnityTest]
        public IEnumerator AFocusedMinusShowsTheRefund()
        {
            yield return OpenAttributesAtCamp(points: 1);
            int row = RowOf(AbilityScore.Constitution);
            DossierPane.Press($"DossierAttrPlus{row}");
            int health = HealthNow;

            DossierPane.Select($"DossierAttrMinus{row}");
            yield return null;

            Assert.AreEqual($"{health} → {health - 20}", StatValue(SheetStat.MaxHealth).text);
            Assert.Greater(StatValue(SheetStat.MaxHealth).color.r, StatValue(SheetStat.MaxHealth).color.g, "a loss is not red");
        }

        // A stepper that would do nothing lights the rows and promises nothing.
        [UnityTest]
        public IEnumerator AStepperThatWouldDoNothingPreviewsNoChange()
        {
            yield return OpenAttributesAtCamp(points: 0);
            int health = HealthNow;

            DossierPane.Select($"DossierAttrPlus{RowOf(AbilityScore.Constitution)}");
            yield return null;
            Assert.AreEqual(health.ToString(), StatValue(SheetStat.MaxHealth).text, "a plus with no point to spend previews a change");

            DossierPane.Select($"DossierAttrMinus{RowOf(AbilityScore.Constitution)}");
            yield return null;
            Assert.AreEqual(health.ToString(), StatValue(SheetStat.MaxHealth).text, "a minus with nothing invested previews a change");
        }

        // Leaving the stepper puts the plain figures back.
        [UnityTest]
        public IEnumerator LeavingTheStepperRestoresThePlainFigures()
        {
            yield return OpenAttributesAtCamp(points: 1);
            int health = HealthNow;

            DossierPane.Select($"DossierAttrPlus{RowOf(AbilityScore.Constitution)}");
            yield return null;
            DossierPane.Select(DossierPane.SectionRow(CharacterSection.Attributes));
            yield return null;

            Assert.AreEqual(health.ToString(), StatValue(SheetStat.MaxHealth).text);
        }

        // The press moves the numbers under a stepper that keeps the focus, so
        // its preview is redrawn for the next press.
        [UnityTest]
        public IEnumerator AfterAPressTheFocusedStepperPreviewsTheNextPoint()
        {
            yield return OpenAttributesAtCamp(points: 2);
            int row = RowOf(AbilityScore.Constitution);

            DossierPane.Select($"DossierAttrPlus{row}");
            yield return null;
            DossierPane.Press($"DossierAttrPlus{row}");
            yield return null;

            int health = HealthNow;
            Assert.AreEqual($"{health} → {health + 20}", StatValue(SheetStat.MaxHealth).text,
                "the preview did not follow the press");

            DossierPane.Press($"DossierAttrPlus{row}");
            yield return null;
            Assert.AreEqual(0, Shown.unspentStatPoints);
            Assert.AreEqual(HealthNow.ToString(), StatValue(SheetStat.MaxHealth).text,
                "with the last point spent the plus still promises a change");
        }

        // ---- the layout, as it is drawn ------------------------------------------------------------

        // "-" and "+" are drawn, at a size that reads, in a font that has them.
        [UnityTest]
        public IEnumerator TheSteppersDrawALegibleMinusAndPlus()
        {
            yield return OpenAttributesAtCamp(points: 1);

            var minus = DossierPane.Label("DossierAttrMinus0Label");
            var plus = DossierPane.Label("DossierAttrPlus0Label");

            Assert.AreEqual("−", DossierPane.DrawnText(minus), "the minus draws nothing");
            Assert.AreEqual("+", DossierPane.DrawnText(plus));
            Assert.GreaterOrEqual(minus.fontSize, 32f);
            Assert.GreaterOrEqual(plus.fontSize, 32f);
            Assert.IsTrue(minus.font.HasCharacter('−', searchFallbacks: true, tryAddCharacter: true),
                "the font has no minus sign, so the refund button draws a missing-glyph box");
        }

        // Measured on screen: a score's value and its plus are neighbours, not
        // the width of the pane apart.
        [UnityTest]
        public IEnumerator EachStepperSitsBesideItsValue()
        {
            yield return OpenAttributesAtCamp(points: 1);

            for (int i = 0; i < 6; i++)
            {
                var value = DossierPane.Label($"DossierAttrValue{i}").rectTransform;
                var plus = DossierPane.Need($"DossierAttrPlus{i}").GetComponent<RectTransform>();
                var minus = DossierPane.Need($"DossierAttrMinus{i}").GetComponent<RectTransform>();

                Assert.Less(Mathf.Abs(plus.position.x - value.position.x), 200f, $"row {i}: the plus is far from its value");
                Assert.Greater(minus.position.x, value.position.x, $"row {i}: the minus is not after the value");
                Assert.Less(minus.position.x, plus.position.x, $"row {i}: the minus is not before the plus");
                Assert.AreEqual(plus.position.y, value.position.y, 1f, $"row {i}: the plus is not level with its value");
                Assert.IsFalse(DossierPane.Overlap(minus, plus), $"row {i}: the steppers overlap");
                Assert.IsFalse(DossierPane.Overlap(value, minus), $"row {i}: the value runs under the minus");
            }
        }

        // The longest effect line (Wisdom's, which names three things) shows
        // whole on two lines rather than ending in "...".
        [UnityTest]
        public IEnumerator EveryEffectLineIsDrawnWholeOnAtMostTwoLines()
        {
            yield return OpenAttributesAtCamp(points: 0);

            for (int i = 0; i < 6; i++)
            {
                var effect = DossierPane.Label($"DossierAttrEffect{i}");
                string drawn = DossierPane.DrawnText(effect);
                Assert.IsNotEmpty(drawn, $"row {i}'s effect line draws nothing");
                StringAssert.DoesNotContain("…", drawn, $"row {i}'s effect line is cut: {drawn}");
                Assert.LessOrEqual(effect.textInfo.lineCount, 2, $"row {i}'s effect line needs more than two lines");
            }
        }

        // The widest row a preview can ask of the derived column.
        [UnityTest]
        public IEnumerator ADerivedRowHoldsItsWidestPreviewOnOneLine()
        {
            yield return OpenAttributesAtCamp(points: 0);

            string worst = UiStrings.StatPreview.Format(
                SheetStats.DisplayText(SheetStat.PhysicalDefense, 1000),
                SheetStats.DisplayText(SheetStat.PhysicalDefense, 1200));

            var value = StatValue(SheetStat.PhysicalDefense);
            value.SetContent(worst);

            Assert.AreEqual(worst, DossierPane.DrawnText(value), "the widest preview is cut in its row");
            Assert.AreEqual(1, value.textInfo.lineCount);
        }

        // ---- the derived rows' names and feeds (plan C21) ---------------------------------

        private static int RowIndexOf(SheetStat stat) => System.Array.IndexOf(SheetStats.Derived, stat);

        private static bool StatRowDrawn(SheetStat stat) =>
            DossierPane.Label($"DossierStatName{RowIndexOf(stat)}").gameObject.activeInHierarchy
            && DossierPane.Label($"DossierStatValue{RowIndexOf(stat)}").gameObject.activeInHierarchy;

        private static bool StatRowLit(SheetStat stat) =>
            Resources.FindObjectsOfTypeAll<RectTransform>()
                .First(r => r.gameObject.scene.IsValid() && r.name == $"DossierStatHighlight{RowIndexOf(stat)}")
                .gameObject.activeSelf;

        private IEnumerator ShowCharacter(string id)
        {
            yield return OpenAttributesAtCamp(points: 0);
            Assert.IsTrue(_dossier.SelectCharacter(id), $"fixture: {id} is not on the strip");
            _dossier.Refresh();
            yield return null;
        }

        // The row that read "Focus / turn" for everyone is written in the
        // character's own resource.
        [UnityTest]
        public IEnumerator ShawnsSignatureRowIsNamedForHisWool()
        {
            yield return ShowCharacter("sheep");

            Assert.AreEqual("Wool per turn", DossierPane.TextOf($"DossierStatName{RowIndexOf(SheetStat.SignatureGain)}"));
            Assert.IsTrue(StatRowDrawn(SheetStat.SignatureGain));
            Assert.AreNotEqual("-", DossierPane.TextOf($"DossierStatValue{RowIndexOf(SheetStat.SignatureGain)}"));
        }

        [UnityTest]
        public IEnumerator ACharacterWithNoSignatureHasNoSignatureRowInsteadOfADash()
        {
            foreach (string id in new[] { "owl", "bear" })
            {
                yield return ShowCharacter(id);

                Assert.IsFalse(StatRowDrawn(SheetStat.SignatureGain), $"{id} has no signature resource but the sheet draws its row");

                DossierPane.Select($"DossierAttrPlus{RowOf(AbilityScore.Charisma)}");
                yield return null;
                Assert.IsFalse(StatRowLit(SheetStat.SignatureGain), $"{id}: Charisma lights a row that is not drawn");
            }
        }

        // Wisdom sets base regen, and Wisdom's own effect line says so, so the
        // row lights and previews for a mana user.
        [UnityTest]
        public IEnumerator FocusOnWisdomLightsManaRegenForAManaUser()
        {
            yield return ShowCharacter("owl");

            DossierPane.Select($"DossierAttrPlus{RowOf(AbilityScore.Wisdom)}");
            yield return null;

            Assert.IsTrue(StatRowLit(SheetStat.ManaRegen), "Wisdom feeds Mana Regen and the row is dark");
            Assert.IsTrue(StatRowLit(SheetStat.MaxMana));
            Assert.IsFalse(StatRowLit(SheetStat.Speed));
        }

        // Bjorn's Fury has a fixed capacity and a fixed income, so Wisdom moves
        // neither of the pool rows for him.
        [UnityTest]
        public IEnumerator FocusOnWisdomLightsNeitherPoolRowForAFuryUser()
        {
            yield return ShowCharacter("bear");

            DossierPane.Select($"DossierAttrPlus{RowOf(AbilityScore.Wisdom)}");
            yield return null;

            Assert.IsFalse(StatRowLit(SheetStat.ManaRegen), "Wisdom does not move a Fixed pool's income");
            Assert.IsFalse(StatRowLit(SheetStat.MaxMana));
            Assert.IsTrue(StatRowLit(SheetStat.MagicalDefense), "Wisdom still mitigates magic for him");
        }
    }
}
