using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.PlayModeTests
{
    // The dossier's Skills row used to carry no onClick at all (owner bug
    // report, 2026-09-19: "Skills in the char menu, when you click on it,
    // nothing happens") -- CharacterDossierController.DeclareColumnARows'
    // own former header named the mechanism: no skillsRow field for
    // UiAutoBind to fill, and no pane to open. This pins the fix, on both
    // inputs the row now answers: a real click on the row opens a real pane
    // with the character's kit in it, and the pad's Submit/Cancel pair does
    // the identical open/close the panel's own Close button and every other
    // dossier panel already answer to.
    //
    // SHAWN ("sheep") IS THE FIXTURE CHARACTER, not whoever squad[0] happens
    // to be -- he is the only character whose talent tree grants skills
    // today (talents.json; Odette's and Bjorn's trees do not), so a test
    // built around "the first squad member" would be testing whichever
    // character the roster defaults to rather than this feature. GrantShawn
    // below puts him alone in the squad and states exactly which talents he
    // holds, so the panel's content is never a guess about save state.
    public class DossierSkillsPanelTests
    {
        private string _root;
        private SystemMenuController _menu;
        private CharacterDossierController _dossier;
        private ScriptedBaseInput _input;

        private static string OutputDir => CaptureOutput.LabelDir("dossier_skills");

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-dossier-skills-" + System.Guid.NewGuid().ToString("N"));
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
            TestGlobals.ResetAll();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // Grants Shawn exactly the talents named (clearing whatever a fresh
        // save started him with) and makes him the whole squad, so the
        // dossier opens on him directly -- no paging needed to reach the
        // fixture character.
        private static void GrantShawn(params string[] talentIds)
        {
            var save = SaveSlotManager.CurrentSave;
            Assert.IsNotNull(save);

            save.selectedCharacterIds = new List<string> { "sheep" };
            var shawn = save.roster.FirstOrDefault(c => c != null && c.definitionId == "sheep");
            Assert.IsNotNull(shawn, "fixture: 'sheep' is not in the roster");

            shawn.unlockedTalentIds.Clear();
            foreach (var id in talentIds) shawn.unlockedTalentIds.Add(id);
            SaveSlotManager.SaveCurrent();
        }

        private IEnumerator OpenTheCharacterTab()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            // Start() runs one frame after activation.
            yield return null;
            yield return null;

            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "the hub scene's EventSystem is not running NavigationInputModule");

            EventSystem.current = module.GetComponent<EventSystem>();
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "the hub has no SystemMenuController");

            _dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_dossier, "the Character pane has no dossier controller");

            _menu.Open();
            _menu.Select(0);
            yield return null;
        }

        private static GameObject Named(Component root, string name) =>
            root.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private static string TextOf(GameObject node) => node?.GetComponent<TMP_Text>()?.text;

        private IEnumerator DriveFrame()
        {
            yield return null;
            _input.ClearOneFrameFlags();
        }

        // ---- the click (the reported bug) ---------------------------------------

        [UnityTest]
        public IEnumerator ClickingTheRow_OpensThePanelWithOneRowPerGrantedSkill()
        {
            GrantShawn("sheep_ram_provoke_1", "sheep_ram_charge_1");

            yield return OpenTheCharacterTab();

            var row = Named(_dossier, "DossierSkillsRow");
            Assert.IsNotNull(row, "DossierSkillsRow is not in the built scene");

            // THE ACTUAL BUG, driven the way a player triggers it: a click on
            // the row's own Button, not a direct call to ShowSkills. Before
            // this fix the row carried no onClick listener at all, so this
            // Invoke was a no-op and IsSkillsShown never went true.
            row.GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsTrue(_dossier.IsSkillsShown, "clicking the row should have opened the skills panel");

            Assert.AreEqual("Provoke", TextOf(Named(_dossier, "DossierSkill0Name")));
            Assert.AreEqual("Head down, eyes up. Whatever it was going to do, it is going to do it to you.",
                TextOf(Named(_dossier, "DossierSkill0Description")));

            Assert.AreEqual("Headbutt", TextOf(Named(_dossier, "DossierSkill1Name")));
            Assert.AreEqual("Not elegant. Knocks the target a place down the order.",
                TextOf(Named(_dossier, "DossierSkill1Description")));

            // Every row past the granted two is hidden, not blank -- a blank
            // name is still a box the player reads as "this character knows
            // nothing", which is not what an unused row means.
            for (int i = 2; i < CharacterDossierScreen.SkillsVisibleCount; i++)
            {
                var name = Named(_dossier, $"DossierSkill{i}Name");
                Assert.IsNotNull(name, $"DossierSkill{i}Name is missing from the built scene");
                Assert.IsFalse(name.activeInHierarchy, $"DossierSkill{i}Name is showing with nothing granted for it");
            }

            var empty = Named(_dossier, "DossierSkillsEmptyHint");
            Assert.IsNotNull(empty, "DossierSkillsEmptyHint is not in the built scene");
            Assert.IsFalse(empty.activeInHierarchy, "the empty hint is showing for a character who has two skills");

            if (!CanvasCapture.IsSupported) yield break;

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            if (canvas == null) yield break;

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "skills_panel_two_rows.png");
            CanvasCapture.RenderToFile(canvas, path);
            Debug.Log($"[DossierSkills] wrote {path}");
        }

        [UnityTest]
        public IEnumerator ACharacterWithNoTalentGrantedSkillsYet_ShowsTheEmptyHintAndNoRows()
        {
            GrantShawn();

            yield return OpenTheCharacterTab();

            var row = Named(_dossier, "DossierSkillsRow");
            Assert.IsNotNull(row, "DossierSkillsRow is not in the built scene");
            row.GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsTrue(_dossier.IsSkillsShown, "clicking the row should still open the panel with nothing in it");

            var empty = Named(_dossier, "DossierSkillsEmptyHint");
            Assert.IsNotNull(empty, "DossierSkillsEmptyHint is not in the built scene");
            Assert.IsTrue(empty.activeInHierarchy, "a character with no granted skills should see the empty hint");

            for (int i = 0; i < CharacterDossierScreen.SkillsVisibleCount; i++)
            {
                var name = Named(_dossier, $"DossierSkill{i}Name");
                Assert.IsNotNull(name, $"DossierSkill{i}Name is missing from the built scene");
                Assert.IsFalse(name.activeInHierarchy, $"DossierSkill{i}Name is showing for a character with nothing granted");
            }
        }

        // ---- the pad --------------------------------------------------------------

        [UnityTest]
        public IEnumerator Submit_OnSkillsRow_OpensThePanelWithCloseSelected()
        {
            GrantShawn("sheep_ram_provoke_1");

            yield return OpenTheCharacterTab();

            var row = Named(_dossier, "DossierSkillsRow").GetComponent<Selectable>();
            EventSystem.current.SetSelectedGameObject(row.gameObject);
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.IsTrue(_dossier.IsSkillsShown, "fixture: Submit on the row should have opened the panel");

            var close = Named(_dossier, "DossierSkillsClose");
            Assert.IsNotNull(close, "DossierSkillsClose is not in the built scene");
            Assert.AreEqual(close, EventSystem.current.currentSelectedGameObject,
                "opening the panel should land on its own entry -- Close, since nothing inside this " +
                "read-only pane takes a press (see CharacterDossierController.SkillsEntry)");
        }

        [UnityTest]
        public IEnumerator Cancel_WhileTheSkillsPanelIsOpen_ClosesThePanelAndReturnsToItsRow()
        {
            GrantShawn("sheep_ram_provoke_1");

            yield return OpenTheCharacterTab();
            _dossier.ShowSkills(true);
            yield return null;

            var close = Named(_dossier, "DossierSkillsClose");
            EventSystem.current.SetSelectedGameObject(close);
            yield return null;

            _input.CancelDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_dossier.IsSkillsShown, "Cancel should have closed the skills panel");
            Assert.IsTrue(_menu.IsOpen, "and must not have taken the menu down with it");

            var row = Named(_dossier, "DossierSkillsRow");
            Assert.AreEqual(row, EventSystem.current.currentSelectedGameObject,
                "the press costs exactly one level: back to the row that opened it, the same rule the " +
                "pack and the spell books already follow");
        }

        [UnityTest]
        public IEnumerator Close_OnTheSkillsPanel_ReturnsSelectionToTheRow()
        {
            GrantShawn("sheep_ram_provoke_1");

            yield return OpenTheCharacterTab();
            _dossier.ShowSkills(true);
            yield return null;

            var close = Named(_dossier, "DossierSkillsClose");
            EventSystem.current.SetSelectedGameObject(close);
            yield return null;

            _input.SubmitDown = true;
            yield return DriveFrame();

            Assert.IsFalse(_dossier.IsSkillsShown, "fixture: Submit on Close should have closed the panel");

            var row = Named(_dossier, "DossierSkillsRow");
            Assert.AreEqual(row, EventSystem.current.currentSelectedGameObject,
                "closing the panel should hand selection straight back to the row that opened it");
        }
    }
}
