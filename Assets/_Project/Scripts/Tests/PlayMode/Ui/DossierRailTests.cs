using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
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
            TalentController.RequestedCharacterId = null;
            RunManager.ResetForTests();
            Navigation.Reset();
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

        // A character as a save would hold them at the start of a descent's
        // camp: level 1, nothing collected, nothing kindled, nothing taught.
        private static Character Fresh(string id)
        {
            var character = SaveSlotManager.CurrentSave.roster.First(c => c.definitionId == id);
            character.level = 1;
            character.claimedTrackLevel = 0;
            character.unlockedTalentIds.Clear();
            character.unlockedSkillIds.Clear();
            return character;
        }

        private IEnumerator OpenSkillsOf(string id, params string[] talents)
        {
            yield return OpenTheCharacterTab("sheep", "bear", "owl");

            var character = Fresh(id);
            foreach (string talent in talents) character.unlockedTalentIds.Add(talent);

            _dossier.SelectCharacter(id);
            DossierPane.Press(DossierPane.SectionRow(CharacterSection.Skills));
            _dossier.Refresh();
            yield return null;
            Assert.AreEqual(CharacterSection.Skills, _dossier.Section);
        }

        private static bool Shown(string name) => Rect(name).gameObject.activeInHierarchy;

        // The old list counted only talent-granted skills and read "0 known"
        // for Bjorn, who fights with Slam from level 1.
        [UnityTest]
        public IEnumerator ABrandNewBjornListsHisInnateSlamUnderInnate()
        {
            yield return OpenSkillsOf("bear");

            Assert.AreEqual("INNATE", DossierPane.TextOf("DossierSkillHeader0"));
            Assert.AreEqual("Slam", DossierPane.TextOf("DossierSkillRow0Name"));
            Assert.AreEqual("Spends Fury  ·  One enemy  ·  Front rank only",
                DossierPane.TextOf("DossierSkillRow0Meta"), "the cost is in Bjorn's own pool, and Slam is melee");
            StringAssert.StartsWith("Deal damage plus 17 to one target.", DossierPane.TextOf("DossierSkillRow0Description"));
            Assert.IsFalse(Shown("DossierSkillRow1"), "nothing else is his yet");
        }

        [UnityTest]
        public IEnumerator BelowTheSkillsTheTalentsBlockSaysNoneKindledAndHowManyEmbersAreLeft()
        {
            yield return OpenSkillsOf("bear");

            Assert.AreEqual("TALENTS", DossierPane.TextOf("DossierSkillHeader1"));
            Assert.AreEqual("No talents kindled yet.", DossierPane.TextOf("DossierSkillNote0"));
            Assert.AreEqual("30 embers unspent", DossierPane.TextOf("DossierSkillNote1"));
        }

        [UnityTest]
        public IEnumerator SkillsFromTalentsGroupUnderTheirOwnHeaderAndTheTalentsListBelow()
        {
            yield return OpenSkillsOf("bear", "bear_sen_root", "bear_sen_bellow_1");

            Assert.AreEqual("INNATE", DossierPane.TextOf("DossierSkillHeader0"));
            Assert.AreEqual("FROM TALENTS", DossierPane.TextOf("DossierSkillHeader1"));
            Assert.AreEqual("TALENTS", DossierPane.TextOf("DossierSkillHeader2"));

            CollectionAssert.AreEqual(
                new[] { "Slam", "Bellow", "Brace", "The Sentinel", "Bellow" },
                Enumerable.Range(0, 5).Select(i => DossierPane.TextOf($"DossierSkillRow{i}Name")).ToArray());
            Assert.AreEqual("Free  ·  Cooldown 3  ·  One enemy", DossierPane.TextOf("DossierSkillRow1Meta"));
            Assert.AreEqual("", DossierPane.TextOf("DossierSkillRow3Meta"), "a talent has no cost line");
        }

        // A signature cost is written in the signature's own name.
        [UnityTest]
        public IEnumerator AWoolSkillIsCostedInWool()
        {
            yield return OpenSkillsOf("sheep", "sheep_ram_charge_1");

            Assert.AreEqual("2 Wool  ·  One enemy", MetaOfRow("Headbutt"));
        }

        // NO WORD A PLAYER NEEDS ENDS IN AN ELLIPSIS. Every skill and every
        // talent the game authors, on all three characters, read through the
        // whole list a window at a time: the name, the right-hand line and the
        // description must each fit the box they are drawn in. The description
        // box was once two nominal lines tall and drew one and an ellipsis.
        [UnityTest]
        public IEnumerator EveryDescriptionInTheListIsShownWholeNeverCutToAnEllipsis()
        {
            yield return OpenTheCharacterTab("sheep", "bear", "owl");

            var cut = new List<string>();
            int rows = 0, tall = 0;

            foreach (string id in new[] { "sheep", "bear", "owl" })
            {
                var character = Fresh(id);
                character.level = 40;
                character.claimedTrackLevel = 40;
                foreach (var talent in ContentDatabase.Talents) character.unlockedTalentIds.Add(talent.id);

                _dossier.SelectCharacter(id);
                DossierPane.Press(DossierPane.SectionRow(CharacterSection.Skills));
                _dossier.Refresh();
                yield return null;

                _dossier.ScrollSkills(-1000);

                for (int step = 0; step <= _dossier.SkillEntryCount; step++)
                {
                    for (int i = 0; i < DossierLayout.SkillsRowSlots; i++)
                    {
                        if (!Shown($"DossierSkillRow{i}")) continue;

                        var description = Rect($"DossierSkillRow{i}Description").GetComponent<TMP_Text>();
                        var name = Rect($"DossierSkillRow{i}Name").GetComponent<TMP_Text>();
                        var meta = Rect($"DossierSkillRow{i}Meta").GetComponent<TMP_Text>();

                        foreach (var label in new[] { description, name, meta })
                        {
                            label.ForceMeshUpdate();
                            if (label.isTextOverflowing) cut.Add($"{id}: \"{name.text}\" {label.name} is cut");
                        }

                        rows++;
                        if (description.textInfo.lineCount > DossierLayout.SkillsMinDescriptionLines) tall++;
                    }

                    _dossier.ScrollSkills(1);
                    yield return null;
                }
            }

            Assert.IsEmpty(cut.Distinct().ToList(), "text that ends in an ellipsis: " + string.Join("; ", cut.Distinct().Take(8)));
            Assert.Greater(rows, 100, "fixture: the walk read hardly any rows");
            Assert.Greater(tall, 0, "fixture: no description needed more than two lines, so a taller row was never exercised");
        }

        // Rows are read by scrolling the window to them.
        private string MetaOfRow(string name)
        {
            _dossier.ScrollSkills(-1000);

            for (int step = 0; step <= _dossier.SkillEntryCount; step++)
            {
                for (int i = 0; i < DossierLayout.SkillsRowSlots; i++)
                {
                    if (Shown($"DossierSkillRow{i}") && DossierPane.TextOf($"DossierSkillRow{i}Name") == name)
                    {
                        return DossierPane.TextOf($"DossierSkillRow{i}Meta");
                    }
                }

                _dossier.ScrollSkills(1);
            }

            Assert.Fail($"no row named {name} in the list");
            return null;
        }

        [UnityTest]
        public IEnumerator AtCampTheConstellationLinkOpensTheTalentsSceneOnTheShownCharacter()
        {
            yield return OpenSkillsOf("bear");
            string loaded = null;
            Navigation.LoadOverride = scene => loaded = scene;

            Assert.IsTrue(Shown("DossierSkillLink"), "the camp offers the constellation");
            Assert.AreEqual("Open the constellation",
                DossierPane.Need("DossierSkillLink").GetComponentInChildren<TMP_Text>(includeInactive: true).text);

            DossierPane.Press("DossierSkillLink");

            Assert.AreEqual(Navigation.Talents, loaded, "the link did not take the hub's Talents route");
            Assert.AreEqual("bear", TalentController.RequestedCharacterId, "and it did not ask for Bjorn's page");
            Assert.IsFalse(_menu.IsOpen, "the menu is still up over the scene it left");
        }

        [UnityTest]
        public IEnumerator InADescentTheTalentsAreReadableAndThereIsNoConstellationLink()
        {
            RunManager.StartRun(4242UL);
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _menu = DossierPane.Menu();
            _dossier = DossierPane.Dossier();
            _menu.Open(SystemMenuTab.Character);
            yield return null;

            Fresh("bear").unlockedTalentIds.Add("bear_sen_bellow_1");
            _dossier.SelectCharacter("bear");
            _dossier.ShowSection(CharacterSection.Skills, focusRow: false);
            _dossier.Refresh();
            yield return null;

            Assert.AreEqual("TALENTS", DossierPane.TextOf("DossierSkillHeader2"));
            Assert.IsFalse(Shown("DossierSkillLink"), "the constellation is a camp building");

            // Reading the whole list to its end never finds it.
            _dossier.ScrollSkills(1000);
            yield return null;
            Assert.IsFalse(Shown("DossierSkillLink"));
        }

        // A press does nothing on a row, so the footer offers no A there; the
        // link is the one press in the list.
        [UnityTest]
        public IEnumerator TheFooterOffersNoSelectOnARowAndOpenOnTheLink()
        {
            yield return OpenSkillsOf("bear");

            DossierPane.Select("DossierSkillRow0");
            yield return null;
            CollectionAssert.AreEqual(new[] { PromptButton.Cancel, PromptButton.Tabs, PromptButton.Pages },
                _menu.CurrentPrompts().Select(p => p.Button).ToArray());

            DossierPane.Select("DossierSkillLink");
            yield return null;
            var prompts = _menu.CurrentPrompts();
            CollectionAssert.AreEqual(new[] { PromptButton.Confirm, PromptButton.Cancel },
                prompts.Select(p => p.Button).ToArray());
            Assert.AreEqual("Open", prompts[0].Label.Format());
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
