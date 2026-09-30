using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The CHARACTER tab's shell (plan C15, spec section 3): the portrait strip
    // in stage order with the bench dimmed after a divider, the rail's
    // identity block and XP bar, the pane filling its box, and the SKILLS
    // section's list.
    //
    // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene); every test seeds
    // its own save and puts the pane back to rest first.
    public class DossierRailTests
    {
        private string _root;
        private SystemMenuController _menu;
        private CharacterDossierController _dossier;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-dossier-rail-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            SharedScene.AfterTest();
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private IEnumerator OpenTheCharacterTab(params string[] seats)
        {
            yield return SharedScene.Ensure("Hub");

            _menu = DossierPane.Menu();
            _dossier = DossierPane.Dossier();
            DossierPane.Rest();

            if (seats.Length > 0) SaveSlotManager.CurrentSave.selectedCharacterIds = new List<string>(seats);

            _menu.Open(SystemMenuTab.Character);
            yield return null;
            DossierPane.SelectFirstChip(_dossier);
            _dossier.Refresh();
            yield return null;
        }

        private static RectTransform Rect(string name) =>
            Resources.FindObjectsOfTypeAll<RectTransform>().First(r => r.name == name && r.gameObject.scene.IsValid());

        private static UnityEngine.Rect Corners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return UnityEngine.Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        // ---- the strip -------------------------------------------------------------------

        // seats are Front, Middle, Rear; the strip reads Rear, Middle, Front.
        [UnityTest]
        public IEnumerator TheStripShowsTheSeatedPartyInStageOrder()
        {
            yield return OpenTheCharacterTab("bear", "sheep", "owl");

            CollectionAssert.AreEqual(new[] { "Odette", "Shawn", "Bjorn" },
                Enumerable.Range(0, 3).Select(i => DossierPane.TextOf($"DossierChip{i}Name")).ToArray());
            CollectionAssert.AreEqual(new[] { "Rear", "Middle", "Front" },
                Enumerable.Range(0, 3).Select(i => DossierPane.TextOf($"DossierChip{i}Seat")).ToArray());

            Assert.AreEqual("owl", _dossier.CharacterId, "the tab opens on the strip's first chip");
            Assert.IsTrue(DossierPane.Control("DossierChip0").transform.Find("DossierChip0Selected").gameObject.activeSelf,
                "the selected chip wears no rim");
            Assert.IsFalse(DossierPane.Control("DossierChip1").transform.Find("DossierChip1Selected").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator TheBenchSitsAfterADividerAndIsDimmed()
        {
            yield return OpenTheCharacterTab("bear", "sheep", "");

            Assert.AreEqual("Odette", DossierPane.TextOf("DossierChip2Name"), "the benched character is not after the seated");
            Assert.AreEqual("Benched", DossierPane.TextOf("DossierChip2Seat"));
            Assert.AreEqual("Odette", DossierPane.DrawnTextOf("DossierChip2Name"), "the benched chip draws no name");
            Assert.AreEqual("Benched", DossierPane.DrawnTextOf("DossierChip2Seat"), "the benched chip draws no seat");

            var name = Resources.FindObjectsOfTypeAll<TMP_Text>().First(t => t.name == "DossierChip2Name" && t.gameObject.scene.IsValid());
            var seated = Resources.FindObjectsOfTypeAll<TMP_Text>().First(t => t.name == "DossierChip0Name" && t.gameObject.scene.IsValid());
            Assert.AreEqual(seated.color.a * 0.6f, name.color.a, 0.01f, "a benched chip is not at 60%");

            var divider = Rect("DossierStripDivider");
            Assert.IsTrue(divider.gameObject.activeSelf, "there is no divider before the bench");
            Assert.Less(divider.anchoredPosition.x, Rect("DossierChip2").anchoredPosition.x);
            Assert.Greater(divider.anchoredPosition.x, Rect("DossierChip1").anchoredPosition.x);
        }

        [UnityTest]
        public IEnumerator AChipClickSelectsThatCharacter()
        {
            yield return OpenTheCharacterTab("bear", "sheep", "owl");

            DossierPane.Press("DossierChip2");

            Assert.AreEqual("bear", _dossier.CharacterId);
            Assert.AreEqual("Bjorn", DossierPane.TextOf("DossierName"));
        }

        // ---- the rail ----------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheRailNamesTheCharacterAndTheirLevel()
        {
            yield return OpenTheCharacterTab("bear", "sheep", "owl");
            SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == "owl").level = 7;
            _dossier.Refresh();

            Assert.AreEqual("Odette", DossierPane.TextOf("DossierName"));
            Assert.AreEqual("Level 7", DossierPane.TextOf("DossierLevel"));
        }

        // THE FILL IS A WIDTH ON ITS TRACK, never anchors (which are relative
        // to the rail): measured rect against rect, for every character.
        [UnityTest]
        public IEnumerator TheXpFillNeverLeavesItsTrack_ForEveryCharacter()
        {
            yield return OpenTheCharacterTab("bear", "sheep", "owl");

            foreach (var character in SaveSlotManager.CurrentSave.roster)
            {
                character.exp = Character.ExpToNextLevel(character.level) - 1;
            }

            for (int i = 0; i < 3; i++)
            {
                _dossier.Refresh();
                yield return null;

                var track = Corners(Rect("DossierXpTrack"));
                var fill = Corners(Rect("DossierXpFill"));

                Assert.GreaterOrEqual(fill.xMin, track.xMin - 0.5f, $"character {i}: the fill starts left of its track");
                Assert.LessOrEqual(fill.xMax, track.xMax + 0.5f, $"character {i}: the fill runs past its track");

                DossierPane.Press("DossierStripNext");
            }
        }

        [UnityTest]
        public IEnumerator TheDossierFillsTheContentPane()
        {
            yield return OpenTheCharacterTab();

            var root = Rect("CharacterDossier");
            Assert.AreEqual(SystemMenuLayout.PanelWidth, root.rect.width, 1f);
            Assert.AreEqual(SystemMenuLayout.ContentHeight, root.rect.height, 1f);
        }

        // ---- SKILLS ------------------------------------------------------------------------

        private static void GrantShawn(params string[] talentIds)
        {
            var shawn = SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == "sheep");
            shawn.unlockedTalentIds.Clear();
            foreach (var id in talentIds) shawn.unlockedTalentIds.Add(id);
        }

        [UnityTest]
        public IEnumerator TheSkillsSectionListsOneRowPerGrantedSkill()
        {
            yield return OpenTheCharacterTab("sheep", "bear", "owl");
            GrantShawn("sheep_ram_provoke_1", "sheep_ram_charge_1");
            _dossier.SelectCharacter("sheep");

            DossierPane.Press(DossierPane.SectionRow(CharacterSection.Skills));
            yield return null;

            Assert.AreEqual(CharacterSection.Skills, _dossier.Section);
            Assert.AreEqual("Provoke", DossierPane.TextOf("DossierSkill0Name"));
            Assert.AreEqual("The target's next attack must hit you.", DossierPane.TextOf("DossierSkill0Description"));
            Assert.AreEqual("Headbutt", DossierPane.TextOf("DossierSkill1Name"));

            for (int i = 2; i < DossierLayout.SkillsVisible; i++)
            {
                Assert.IsFalse(Rect($"DossierSkill{i}Name").gameObject.activeInHierarchy,
                    $"DossierSkill{i}Name is showing with nothing granted for it");
            }

            Assert.IsFalse(Rect("DossierSkillsEmptyHint").gameObject.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator ACharacterWithNoGrantedSkills_SeesTheEmptySentence()
        {
            yield return OpenTheCharacterTab("sheep", "bear", "owl");
            GrantShawn();
            _dossier.SelectCharacter("sheep");

            DossierPane.Press(DossierPane.SectionRow(CharacterSection.Skills));
            yield return null;

            Assert.IsTrue(Rect("DossierSkillsEmptyHint").gameObject.activeInHierarchy);
            Assert.AreEqual("Nothing learned yet.", DossierPane.TextOf("DossierSkillsEmptyHint"));
            Assert.IsFalse(Rect("DossierSkill0Name").gameObject.activeInHierarchy);
        }

        // Nothing covers the rail: every section leaves it standing.
        [UnityTest]
        public IEnumerator EverySectionLeavesTheRailStanding()
        {
            yield return OpenTheCharacterTab();

            foreach (var section in CharacterSections.All)
            {
                _dossier.ShowSection(section, focusRow: false);
                yield return null;

                foreach (var other in CharacterSections.All)
                {
                    Assert.IsTrue(DossierPane.Control(DossierPane.SectionRow(other)).gameObject.activeInHierarchy,
                        $"{other}'s rail row is hidden while {section} shows");
                }

                Assert.IsTrue(Rect("DossierName").gameObject.activeInHierarchy, $"the identity block is hidden under {section}");
            }
        }
    }
}
