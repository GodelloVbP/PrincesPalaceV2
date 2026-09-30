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
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE MENU'S LINTEL PILL, NOTICE LINE AND PROMPT FOOTER in the running
    // scenes (plan C10-C12, spec sections 2 and 8). The arithmetic and the
    // tables are pinned in EditMode (MenuFooterLayoutTests, MenuPromptsTests);
    // this holds the built scenes to them: the pill says the context and fits
    // its words, the footer is where the spec puts it, and its prompts follow
    // the device, the focus and the pending state through the real dispatcher.
    public class MenuFooterTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-menu-footer-" + System.Guid.NewGuid().ToString("N"));
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
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Time.timeScale = 1f;
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // ---- fixture -----------------------------------------------------------

        private SystemMenuController _menu;

        private IEnumerator OpenTheMenu(string scene, SystemMenuTab tab = SystemMenuTab.Character)
        {
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null;
            TakeOverInput();
            yield return null;

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, $"{scene} has no SystemMenuController");

            _menu.Open(tab);
            yield return DriveFrame();
            yield return DriveFrame();
        }

        private GameObject Node(string name) =>
            _menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private GameObject Need(string name)
        {
            var go = Node(name);
            Assert.IsNotNull(go, $"the menu has no '{name}'");
            return go;
        }

        private TMP_Text TextOf(string name) => Need(name).GetComponent<TMP_Text>();

        // What the footer says, left to right: "<glyph> <label>" per visible pair.
        private string Prompts()
        {
            var parts = new List<string>();
            for (int i = 0; i < MenuFooterLayout.PromptSlots; i++)
            {
                var glyph = Node($"SystemPrompt{i}GlyphText");
                var label = Node($"SystemPrompt{i}Label");
                if (glyph == null || label == null || !glyph.activeInHierarchy) continue;

                parts.Add($"{glyph.GetComponent<TMP_Text>().text} {label.GetComponent<TMP_Text>().text}");
            }

            return string.Join(" | ", parts);
        }

        private IEnumerator Focus(GameObject go)
        {
            EventSystem.current.SetSelectedGameObject(go);
            yield return DriveFrame();
            yield return DriveFrame();
        }

        private IEnumerator UsePointer()
        {
            Input.MousePosition = new Vector2(420f, 310f);
            yield return DriveFrame();
            yield return DriveFrame();
            Assert.IsFalse(NavigationInputModule.LastInputWasPad, "fixture: the mouse did not take over");
        }

        private IEnumerator UsePad()
        {
            Input.Horizontal = 1f;
            yield return DriveFrame();
            Input.Horizontal = 0f;
            yield return DriveFrame();
            Assert.IsTrue(NavigationInputModule.LastInputWasPad, "fixture: the stick did not take over");
        }

        // ---- the mode pill (C10) ------------------------------------------------

        [UnityTest]
        public IEnumerator ThePillSaysAtCampInTheHub() => ThePillReads("Hub", "AT CAMP");

        [UnityTest]
        public IEnumerator ThePillSaysInADescentOnTheMap() => ThePillReads("Map", "IN A DESCENT");

        [UnityTest]
        public IEnumerator ThePillSaysViewOnlyInAFight() => ThePillReads("Fight", "IN A FIGHT — VIEW ONLY");

        private IEnumerator ThePillReads(string scene, string words)
        {
            yield return OpenTheMenu(scene);

            var label = TextOf("SystemModePillLabel");
            Assert.AreEqual(words, label.text, $"the pill in {scene}");

            // It is sized to its words, never past its authored maximum, and its
            // left edge does not move with the context.
            var pill = (RectTransform)Need("SystemModePill").transform;
            float wanted = label.GetPreferredValues(label.text).x;
            Assert.GreaterOrEqual(pill.sizeDelta.x, wanted + 2f * SystemMenuLayout.PillPadX - 2f,
                "the pill is narrower than its own words");
            Assert.LessOrEqual(pill.sizeDelta.x, SystemMenuLayout.PillMaxWidth + 0.01f);
            Assert.AreEqual(SystemMenuLayout.PillHeight, pill.sizeDelta.y, 0.01f);
            Assert.AreEqual(SystemMenuLayout.PillLeftEdgeX, pill.anchoredPosition.x - pill.sizeDelta.x * 0.5f, 0.6f,
                "the pill's left edge moved");
        }

        // ---- where the footer sits (spec section 2) ---------------------------------

        [UnityTest]
        public IEnumerator TheFooterIsA1600By48StripFourPixelsBelowThePanel()
        {
            yield return OpenTheMenu("Hub");

            var footer = (RectTransform)Need("SystemMenuFooter").transform;
            var frame = (RectTransform)Need("SystemMenuFrame").transform;

            Assert.AreEqual(new Vector2(1600f, 48f), footer.sizeDelta);
            Assert.AreEqual(new Vector2(1600f, 900f), frame.sizeDelta, "the panel lost height to the footer");

            float frameBottom = frame.anchoredPosition.y - frame.sizeDelta.y * 0.5f;
            float footerTop = footer.anchoredPosition.y + footer.sizeDelta.y * 0.5f;
            Assert.AreEqual(4f, frameBottom - footerTop, 0.01f, "the footer is not 4 px under the panel");
        }

        [UnityTest]
        public IEnumerator EveryLintelAndFooterLabelDrawsAtOrOverTheFloor()
        {
            yield return OpenTheMenu("Hub");

            var frame = new[] { "SystemMenuLintel", "SystemMenuFooter", "SystemMenuBar" };
            var labels = frame.SelectMany(n => Need(n).GetComponentsInChildren<TMP_Text>(includeInactive: true)).ToList();
            Assert.Greater(labels.Count, 20, "fixture: the frame's labels were not found");

            var small = labels
                .Where(t => t.fontSize < MenuTypeFloor.MixedCase)
                .Select(t => $"{t.name} at {t.fontSize}")
                .ToList();

            Assert.IsEmpty(small, "labels on the frame are drawing under the floor: " + string.Join(", ", small));
        }

        // ---- the prompts follow the device (C12) ----------------------------------------

        // The frame's row is read on ANOTHER tab's button: A on the tab that is
        // already showing selects nothing, so the row drops it there (below).
        [UnityTest]
        public IEnumerator TheDefaultRowIsSelectCloseTabAndTheCharacterPager()
        {
            yield return OpenTheMenu("Hub");
            yield return Focus(Need("SystemTabParty"));

            Assert.AreEqual("A Select | B Close | LT/RT Tab | LB/RB Character", Prompts());
        }

        [UnityTest]
        public IEnumerator OnTheTabAlreadyShowingTheRowHasNoSelect()
        {
            yield return OpenTheMenu("Hub");
            yield return Focus(Need("SystemTabCharacter"));

            Assert.AreEqual("B Close | LT/RT Tab | LB/RB Character", Prompts());
        }

        [UnityTest]
        public IEnumerator ThePromptsSwitchToKeyNamesOnTheMouseAndBackOnThePad()
        {
            yield return OpenTheMenu("Hub");
            yield return Focus(Need("SystemTabParty"));
            Assert.AreEqual("A Select | B Close | LT/RT Tab | LB/RB Character", Prompts(), "fixture: not the pad's row");

            yield return UsePointer();
            Assert.AreEqual("Enter Select | Esc Close | Click Tab | Q/E Character", Prompts());

            yield return UsePad();
            Assert.AreEqual("A Select | B Close | LT/RT Tab | LB/RB Character", Prompts());
        }

        // Plan C8: a tab that pages nothing shows no pager prompt.
        [UnityTest]
        public IEnumerator ATabThatPagesNothingShowsNoPagerPrompt()
        {
            yield return OpenTheMenu("Hub", SystemMenuTab.Party);
            yield return Focus(Need("SystemTabCharacter"));

            Assert.AreEqual("A Select | B Close | LT/RT Tab", Prompts());
        }

        [UnityTest]
        public IEnumerator ThePadGlyphsWearTheirButtonColours()
        {
            yield return OpenTheMenu("Hub");
            yield return Focus(Need("SystemTabParty"));

            Assert.IsTrue(ColorUtility.TryParseHtmlString(PromptGlyphs.Green, out var green));
            Assert.IsTrue(ColorUtility.TryParseHtmlString(PromptGlyphs.Red, out var red));

            var a = TextOf("SystemPrompt0GlyphText").color;
            var b = TextOf("SystemPrompt1GlyphText").color;
            Assert.AreEqual(green.g, a.g, 0.01f, "A is not green");
            Assert.AreEqual(red.r, b.r, 0.01f, "B is not red");
        }

        // The row is flush against the footer's right edge whatever it holds.
        [UnityTest]
        public IEnumerator TheRowIsFlushRightAndDoesNotReachTheNotice()
        {
            yield return OpenTheMenu("Hub");
            yield return Focus(Need("SystemTabCharacter"));

            var last = Enumerable.Range(0, MenuFooterLayout.PromptSlots)
                .Select(i => Node($"SystemPrompt{i}Label"))
                .Where(go => go != null && go.activeInHierarchy)
                .Select(go => (RectTransform)go.transform)
                .Last();
            float right = last.anchoredPosition.x + last.sizeDelta.x * 0.5f;
            Assert.AreEqual(MenuFooterLayout.PromptsRightX, right, 0.6f, "the row is not flush right");

            var first = (RectTransform)Need("SystemPrompt0Glyph").transform;
            float left = first.anchoredPosition.x - first.sizeDelta.x * 0.5f;
            Assert.GreaterOrEqual(left, MenuFooterLayout.PromptsLeftX - 0.6f, "the row reaches into the notice");
        }

        // ---- the prompts follow the focus and the pending state --------------------------

        // Spec section 8, LOADOUT: a worn slot offers Unequip, an empty one
        // Choose from pack, a pack cell Equip -- and while a slot is armed the
        // pack cell's B steps back out of it rather than closing.
        [UnityTest]
        public IEnumerator TheLoadoutSaysWhatAWornSlotAnEmptySlotAndAPackCellDo()
        {
            var save = SaveSlotManager.CurrentSave;
            var character = save.ActiveSquad().First(c => c != null);
            var wearables = ContentDatabase.Equippables;
            var worn = wearables.First();
            var spare = wearables.First(item => item.equipSlot != worn.equipSlot);

            InventoryOps.Add(save.stockpiledItems, worn.id);
            Assert.IsTrue(EquipmentOps.Equip(MenuContext.Camp, save, character, worn.id, worn.equipSlot, worn.IsEquippable),
                "fixture: the first wearable could not be put on");
            save.stockpiledItems.Clear();
            InventoryOps.Add(save.stockpiledItems, spare.id);
            Assert.IsTrue(string.IsNullOrEmpty(character.equipment.Get(spare.equipSlot)), "fixture: the spare's slot is taken");

            yield return OpenTheMenu("Hub");
            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            dossier.SelectCharacter(character.definitionId);

            yield return Focus(Need(SlotName(worn.equipSlot)));
            Assert.AreEqual("A Unequip | B Close", Prompts(), "a worn slot");

            yield return Focus(Need(SlotName(spare.equipSlot)));
            Assert.AreEqual("A Choose from pack | B Close", Prompts(), "an empty slot");

            yield return Focus(Need("DossierPackCell0"));
            Assert.AreEqual("A Equip | B Close", Prompts(), "a pack cell, nothing pending");

            // A on the empty slot arms it: focus goes into the pack, and B
            // there steps back out of the armed slot.
            yield return Focus(Need(SlotName(spare.equipSlot)));
            Input.SubmitDown = true;
            yield return DriveFrame();
            yield return DriveFrame();
            Assert.IsTrue(dossier.HasPending, "fixture: the slot did not arm");
            Assert.AreEqual("A Equip | B Back", Prompts(), "a pack cell while a slot is armed");
        }

        // The same table the controls consult: in a fight the mutating prompt
        // is locked, and the press the prompt names says why on the notice.
        [UnityTest]
        public IEnumerator InAFightAMutatingPromptIsLockedAndItsPressSaysWhy()
        {
            var save = SaveSlotManager.CurrentSave;
            var character = save.ActiveSquad().First(c => c != null);
            var worn = ContentDatabase.Equippables.First();
            InventoryOps.Add(save.stockpiledItems, worn.id);
            Assert.IsTrue(EquipmentOps.Equip(MenuContext.Camp, save, character, worn.id, worn.equipSlot, worn.IsEquippable),
                "fixture: the first wearable could not be put on");

            yield return OpenTheMenu("Fight");
            var dossier = Object.FindAnyObjectByType<CharacterDossierController>(FindObjectsInactive.Include);
            dossier.SelectCharacter(character.definitionId);

            yield return Focus(Need(SlotName(worn.equipSlot)));
            Assert.AreEqual("A Unequip (locked) | B Close", Prompts());
            Assert.AreEqual("", TextOf("SystemFooterNotice").text, "fixture: a notice was already up");

            Need(SlotName(worn.equipSlot)).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual(MenuPermissions.Reason(MenuAction.UnequipGear, MenuContext.Fight).Format(),
                TextOf("SystemFooterNotice").text, "the locked press was silent");
        }

        // A Party carry is the pane's pending state: the prompt row follows it.
        [UnityTest]
        public IEnumerator APartyCarryChangesTheRowToPlaceAndPutBack()
        {
            var save = SaveSlotManager.CurrentSave;
            save.selectedCharacterIds = new List<string> { "sheep", "bear", "owl" };

            yield return OpenTheMenu("Hub", SystemMenuTab.Party);
            var party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);

            yield return Focus(Need("PartySeat0Button"));
            Assert.AreEqual("A Pick up | B Close", Prompts(), "a seat, nothing carried");

            party.ClickSeat(0);
            yield return DriveFrame();
            yield return DriveFrame();
            Assert.AreEqual("A Place | B Put back", Prompts(), "a seat, carrying");

            party.Cancel();
            yield return DriveFrame();
            yield return DriveFrame();
            Assert.AreEqual("A Pick up | B Close", Prompts(), "put back");
        }

        [UnityTest]
        public IEnumerator InAFightEveryPartyPromptIsLocked()
        {
            yield return OpenTheMenu("Fight", SystemMenuTab.Party);

            yield return Focus(Need("PartySeat0Button"));
            Assert.AreEqual("A Pick up (locked) | B Close", Prompts());
        }

        // The reward track answers for its own controls; it is a section, not
        // a level, so B says Close there.
        [UnityTest]
        public IEnumerator TheRewardTrackSaysCloseForB()
        {
            yield return OpenTheMenu("Hub");

            Need("DossierSection3").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return DriveFrame();
            yield return DriveFrame();

            var dot = _menu.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: false)
                .First(b => b.name.StartsWith("TrackDot"));
            yield return Focus(dot.gameObject);

            StringAssert.Contains("B Close", Prompts(), "the row said the wrong thing for B on the reward track");
        }

        // The rail: selection follows focus, so there is no A, and the row
        // names the character pager.
        [UnityTest]
        public IEnumerator TheRailOffersNoSelectButNamesTheCharacterPager()
        {
            yield return OpenTheMenu("Hub");
            yield return Focus(Need("DossierSection0"));

            Assert.AreEqual("B Close | LT/RT Tab | LB/RB Character", Prompts());
        }

        private static string SlotName(EquipmentSlot slot) => "DossierSlot" + slot.ToString();

        // ---- the notice (C11) ---------------------------------------------------------------

        // A state line stays under a notice and comes back when it is gone;
        // clearing it empties the line. One real wait: the notice runs on the
        // unscaled clock because the menu has the game paused.
        [UnityTest]
        public IEnumerator ANoticeWinsTheLineThenTheStateLineComesBack()
        {
            yield return OpenTheMenu("Hub");
            var line = TextOf("SystemFooterNotice");

            _menu.SetStateLine(UiStrings.PartyStatusCamp);
            Assert.AreEqual("AT CAMP — Swap freely.", line.text);

            _menu.ShowNotice(UiStrings.MenuLockedInFight);
            Assert.AreEqual("Locked during a fight", line.text, "the notice did not win the line");

            yield return new WaitForSecondsRealtime(MenuNotice.DefaultSeconds + MenuNotice.FadeSeconds + 0.3f);
            Assert.AreEqual("AT CAMP — Swap freely.", line.text, "the state line did not come back");

            _menu.ClearStateLine();
            Assert.AreEqual("", line.text);
        }

        [UnityTest]
        public IEnumerator ANoticeIsGoneWhenTheMenuIsClosedAndReopened()
        {
            yield return OpenTheMenu("Hub");
            var line = TextOf("SystemFooterNotice");

            _menu.ShowNotice(UiStrings.MenuLockedInFight);
            Assert.AreEqual("Locked during a fight", line.text);

            _menu.Close();
            _menu.Open();
            yield return DriveFrame();

            Assert.AreEqual("", line.text, "a refusal from the last visit greeted the next one");
        }
    }
}
