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
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // The system menu's RUN pane (plan C23, spec sections 4 and 9).
    //
    // THE RULE ITS FIGURES ARE HELD TO is the design's own: every row binds to
    // a tracked field or gets cut. That is not a thing anyone can keep true by
    // remembering, so it is mechanised here -- every key the table declares is
    // swept through the binding, and a row that stops resolving fails a test
    // rather than printing a plausible number.
    //
    // The dossier's Dodge, Carried, Shop-price and Morale rows are what this
    // exists to prevent: four rows that read like data and were furniture.
    public class SystemMenuRunTests
    {
        private string _root;
        private SystemMenuController _menu;
        private ScriptedBaseInput _input;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-run-pane-tests-" + System.Guid.NewGuid().ToString("N"));
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

        // ---- the rule, mechanised ------------------------------------------------

        [Test]
        public void EveryDeclaredRowResolvesToAFigure()
        {
            var run = ARunThatHasDoneThings();

            foreach (var row in RunStatRows.AllRows)
            {
                Assert.IsTrue(RunController.TryFigure(run, row.Key, out _),
                    $"the '{row.Key}' row has no tracked field behind it. Bind it or cut the row - " +
                    "a figure the game does not record is worse than an absent one, because the " +
                    "player believes it.");
            }
        }

        // NON-VACUITY. Without this, the sweep above would still pass if
        // TryFigure returned true for everything, which is exactly the shape a
        // careless default arm would take.
        [Test]
        public void AKeyNothingSuppliesIsRefusedRatherThanZeroed()
        {
            var run = ARunThatHasDoneThings();

            Assert.IsFalse(RunController.TryFigure(run, "morale", out _),
                "an unbound key answered with a number");
            Assert.AreEqual("-", RunController.Figure(run, "morale"),
                "an unbound row printed a figure instead of admitting it has none");
        }

        // ---- the figures are the run's own ----------------------------------------

        [Test]
        public void TheBattleFiguresAreTheWholePartysLedgerAddedUp()
        {
            var run = ARunThatHasDoneThings();

            // 300 + 40 physical, 120 + 10 other, across two characters.
            Assert.AreEqual("470", RunController.Figure(run, "damage_dealt"));
            Assert.AreEqual("95", RunController.Figure(run, "damage_taken"));
            Assert.AreEqual("60", RunController.Figure(run, "healed"));
            Assert.AreEqual("7", RunController.Figure(run, "kills"));
            Assert.AreEqual("1", RunController.Figure(run, "times_downed"));
        }

        // deepestStep, not step. The party stands where they stand; the question
        // this row asks is how far down they got before they stopped.
        [Test]
        public void TheDepthShownIsTheDeepestReachedRatherThanWhereTheyStand()
        {
            var run = ARunThatHasDoneThings();
            run.step = 3;
            run.deepestStep = 11;

            Assert.AreEqual("11", RunController.Figure(run, "deepest_room"));
        }

        // Held and earned diverge the moment anything is spent, and a shop
        // exists. The pane shows earned in Spoils and held as the supplies' gold.
        [Test]
        public void GoldHeldAndGoldEarnedAreDifferentQuestions()
        {
            var run = ARunThatHasDoneThings();

            Assert.AreEqual("128", RunController.Figure(run, "gold_held"));
            Assert.AreEqual("640", RunController.Figure(run, "gold_earned"));
        }

        // Stacks count by their contents. Three salves in one slot are three
        // items in the pack, which is what the row asks.
        [Test]
        public void ThePackCountsItemsRatherThanStacks()
        {
            var run = ARunThatHasDoneThings();
            Assert.AreEqual("5", RunController.Figure(run, "pack"));
        }

        [Test]
        public void FiguresPastAThousandAreGrouped()
        {
            var run = ARunThatHasDoneThings();
            run.ledger.Add(new RunLedgerEntry { characterId = "c", physicalDealt = 8000 });

            Assert.AreEqual("8,470", RunController.Figure(run, "damage_dealt"),
                "a five-figure damage total is printed as a number you have to count");
        }

        // The pane sits on a run-only tab, so this should be unreachable through
        // the UI -- but Select() takes an index and nothing stops a caller
        // passing this one. Thirteen zeroes is a claim that a run went badly.
        [Test]
        public void WithNoRunEveryFigureReadsAsAbsentRatherThanZero()
        {
            foreach (var row in RunStatRows.AllRows)
            {
                Assert.AreEqual("-", RunController.Figure(null, row.Key),
                    $"the '{row.Key}' row printed a number for a run that does not exist");
                Assert.AreEqual("-", RunController.Figure(new RunSnapshot { hasRun = false }, row.Key),
                    $"the '{row.Key}' row printed a number for a run that has ended");
            }
        }

        // ---- the pane, in a descent --------------------------------------------------

        [UnityTest]
        public IEnumerator ThePaneShowsTheRunsRelicsEffectsSuppliesAndFigures()
        {
            var ids = ARunWithEverything(relics: 7);
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;

            // relics: a page of five, in held order, with the count in the header
            Assert.AreEqual("RELICS · 7", TextOf("RunRelicsHeading"));
            var first = ContentDatabase.GetRelic(ids[0]);
            Assert.AreEqual(first.Data.DisplayName, TextOf("RunRelic0Name"));
            Assert.AreEqual(ContentDatabase.GetRelic(ids[4]).Data.DisplayName, TextOf("RunRelic4Name"));
            Assert.IsTrue(Named("RunRelic4").activeSelf, "the fifth row of the page is missing");
            Assert.AreEqual("1 / 2", TextOf("RunRelicPageLabel"));
            Assert.IsFalse(Named("RunRelicsEmpty").activeSelf);
            Assert.AreEqual(RelicRowsRarityText(ids[0]), TextOf("RunRelic0Rarity"));

            // effects: the squad's Favor (Shawn's authored 4 plus the buff's 2),
            // the run-wide bonus damage
            Assert.AreEqual("Prince's favor", TextOf("RunEffect0Name"));
            Assert.AreEqual("6", TextOf("RunEffect0Value"));
            Assert.AreEqual("Bonus damage", TextOf("RunEffect1Name"));
            Assert.AreEqual("+6 %", TextOf("RunEffect1Value"));
            Assert.IsFalse(Named("RunEffect2").activeSelf, "a third effect row with nothing in it");
            Assert.IsFalse(Named("RunEffectsEmpty").activeSelf);

            // supplies: the two consumables with counts, then the gold
            Assert.AreEqual("Health Potion", TextOf("RunSupply0Name"));
            Assert.AreEqual("×2", TextOf("RunSupply0Count"));
            Assert.AreEqual("Mana Potion", TextOf("RunSupply1Name"));
            Assert.AreEqual("×1", TextOf("RunSupply1Count"));
            Assert.IsFalse(Named("RunSupply2").activeSelf);
            Assert.AreEqual("128", TextOf("RunGoldValue"));

            // this descent
            Assert.AreEqual("2", TextOf("RunStatValuerooms_cleared"));
            Assert.AreEqual("65", TextOf("RunStatValuegold_earned"));
            Assert.AreEqual("5", TextOf("RunStatValuedeepest_room"));
        }

        [UnityTest]
        public IEnumerator ARunWithNothingSaysSoInEveryColumn()
        {
            RunManager.StartRun(4242);
            RunManager.Run.relicIds.Clear();
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;

            Assert.AreEqual("RELICS · 0", TextOf("RunRelicsHeading"));
            Assert.IsTrue(Named("RunRelicsEmpty").activeSelf);
            Assert.AreEqual("You carry no relics on this descent.", TextOf("RunRelicsEmpty"));
            Assert.IsFalse(Named("RunRelic0").activeSelf);

            // The squad's own Favor (Shawn's authored 4) is always in force,
            // so the effects column holds that one row and nothing else.
            Assert.IsFalse(Named("RunEffectsEmpty").activeSelf);
            Assert.AreEqual("Prince's favor", TextOf("RunEffect0Name"));
            Assert.AreEqual("4", TextOf("RunEffect0Value"));
            Assert.IsFalse(Named("RunEffect1").activeSelf, "a second effect row with nothing in it");

            Assert.IsTrue(Named("RunSuppliesEmpty").activeSelf);
            Assert.IsFalse(Named("RunRelicPageLabel").activeSelf, "a pager with one page");
        }

        [UnityTest]
        public IEnumerator ARelicIdNamingContentThatIsGoneStillGetsAnUnknownRow()
        {
            RunManager.StartRun(4242);
            RunManager.Run.relicIds.Clear();
            RunManager.Run.relicIds.Add("relic_that_no_longer_exists");
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;

            Assert.IsTrue(Named("RunRelic0").activeSelf, "the unknown relic has no row");
            Assert.AreEqual("relic_that_no_longer_exists", TextOf("RunRelic0Name"));
            Assert.AreEqual("Unknown relic", TextOf("RunRelic0Body"));
        }

        // The pane reads the run when it opens, so the figures it shows after a
        // fight are the fight's, not the ones it had when last shut.
        [UnityTest]
        public IEnumerator ReOpeningThePaneRepaintsFromTheRun()
        {
            RunManager.StartRun(4242);
            RunManager.BankPayout(40);
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;
            Assert.AreEqual("40", TextOf("RunStatValuegold_earned"), "fixture: the pane is not reading the run's gold");

            RunManager.BankPayout(25);
            var pane = _menu.GetComponentInChildren<RunController>(includeInactive: true);
            pane.gameObject.SetActive(false);
            yield return null;
            pane.gameObject.SetActive(true);
            yield return null;

            Assert.AreEqual("65", TextOf("RunStatValuegold_earned"),
                "the pane came back showing the figure it had when it closed");
        }

        // A relic row exists to say what the relic does, so its description box
        // must hold the longest one the game authors. Measured on the real label
        // (font, size and line spacing), against the box the scene was built with.
        [UnityTest]
        public IEnumerator EveryAuthoredRelicDescriptionFitsItsRow()
        {
            ARunWithEverything(relics: 1);
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;

            var body = Named("RunRelic0Body").GetComponent<TMP_Text>();
            var box = body.rectTransform.rect;
            Assert.Greater(box.width, 100f, "fixture: the description box has no width");

            var tooTall = new List<string>();
            foreach (var relic in ContentDatabase.Relics.Where(r => r != null))
            {
                string text = DescriptionRenderer.Render(relic.Data.Description);
                float height = body.GetPreferredValues(text, box.width, Mathf.Infinity).y;
                if (height > box.height + 0.5f) tooTall.Add($"{relic.id} needs {height:F0}px of {box.height:F0}");
            }

            Assert.IsEmpty(tooTall, "relic descriptions that overflow their row: " + string.Join("; ", tooTall));
        }

        // ---- the same run, seen from a fight ---------------------------------------------

        [UnityTest]
        public IEnumerator InAFightThePaneShowsTheSameRun()
        {
            ARunWithEverything(relics: 2);
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Find();

            Assert.AreEqual(MenuContext.Fight, _menu.Context, "fixture: this is not the fight's menu");
            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;

            Assert.AreEqual("RELICS · 2", TextOf("RunRelicsHeading"));
            Assert.AreEqual("Prince's favor", TextOf("RunEffect0Name"));
            Assert.AreEqual("Health Potion", TextOf("RunSupply0Name"));
        }

        // ---- the tab -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheRunTabIsAbsentAtCamp()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Find();

            _menu.Open();
            yield return null;

            CollectionAssert.DoesNotContain(_menu.VisibleTabs.ToList(), SystemMenuTabs.IndexOf(SystemMenuTab.Run));
            Assert.IsFalse(_menu.HasTab(SystemMenuTab.Run));
        }

        // ---- deep link --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator OpenWithARelicTargetPagesToItAndFocusesItsRow()
        {
            var ids = ARunWithEverything(relics: 7);
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run, MenuTarget.RelicEntry(ids[6]));
            yield return null;
            yield return null;

            Assert.AreEqual("2 / 2", TextOf("RunRelicPageLabel"), "the pane did not turn to the relic's page");
            Assert.AreEqual(ContentDatabase.GetRelic(ids[6]).Data.DisplayName, TextOf("RunRelic1Name"));
            Assert.AreEqual("RunRelic1", EventSystem.current.currentSelectedGameObject.name,
                "focus did not land on the relic's row");
        }

        [UnityTest]
        public IEnumerator OpenWithARelicTheRunDoesNotHoldLeavesThePaneAlone()
        {
            ARunWithEverything(relics: 7);
            yield return LoadMap();

            Assert.DoesNotThrow(() => _menu.Open(SystemMenuTab.Run, MenuTarget.RelicEntry("nope")));
            yield return null;

            Assert.AreEqual(SystemMenuTab.Run, _menu.SelectedTab);
            Assert.AreEqual("1 / 2", TextOf("RunRelicPageLabel"));
        }

        // ---- focus (spec section 9) ------------------------------------------------------

        // Down from the tab goes to the pane's entry: the first relic row.
        [UnityTest]
        public IEnumerator TheTabsDownLinkLandsOnTheFirstRelicRow()
        {
            ARunWithEverything(relics: 3);
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;

            var tab = Named("SystemTabRun").GetComponent<Button>();
            Assert.AreEqual("RunRelic0", tab.navigation.selectOnDown.name);
        }

        // LT/RT steps into the pane, landing on its entry.
        [UnityTest]
        public IEnumerator SteppingToRunFromAnotherTabFocusesTheFirstRelicRow()
        {
            ARunWithEverything(relics: 3);
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Party);
            yield return null;
            yield return null;

            _menu.StepTab(1);
            yield return null;

            Assert.AreEqual(SystemMenuTab.Run, _menu.SelectedTab);
            Assert.AreEqual("RunRelic0", EventSystem.current.currentSelectedGameObject.name);
        }

        [UnityTest]
        public IEnumerator UpAndDownStepWithinTheRelicListAndLeftAndRightCrossToTheEffects()
        {
            ARunWithEverything(relics: 3);
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;

            var row0 = Named("RunRelic0").GetComponent<Button>();
            var row1 = Named("RunRelic1").GetComponent<Button>();
            var row2 = Named("RunRelic2").GetComponent<Button>();

            Assert.AreEqual(row1, row0.navigation.selectOnDown, "Down from the first relic");
            Assert.AreEqual(row0, row1.navigation.selectOnUp, "Up from the second relic");
            Assert.AreEqual(row2, row1.navigation.selectOnDown);
            Assert.IsNull(row2.navigation.selectOnDown, "the list clamps at its last row");

            Assert.IsNotNull(row0.navigation.selectOnRight, "Right from a relic reaches nothing");
            StringAssert.StartsWith("RunEffect", row0.navigation.selectOnRight.name);

            var effect = row0.navigation.selectOnRight;
            Assert.IsNotNull(effect.navigation.selectOnLeft, "Left from the effects reaches nothing");
            StringAssert.StartsWith("RunRelic", effect.navigation.selectOnLeft.name);

            Assert.AreEqual(Named("SystemTabRun").GetComponent<Button>(), row0.navigation.selectOnUp,
                "Up from the first relic should reach the selected tab");
        }

        // LB/RB page the relics, and focus stays on its slot.
        [UnityTest]
        public IEnumerator TheShouldersPageTheRelicsAndFocusStaysOnItsSlot()
        {
            ARunWithEverything(relics: 7);
            yield return LoadMap();
            TakeOverInput();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;
            EventSystem.current.SetSelectedGameObject(Named("RunRelic1"));
            yield return null;

            _input.TabNextDown = true;
            yield return null;
            _input.ClearOneFrameFlags();

            Assert.AreEqual("2 / 2", TextOf("RunRelicPageLabel"), "RB did not page the relics");
            Assert.IsTrue(Named("RunRelic1").activeSelf);
            Assert.IsFalse(Named("RunRelic2").activeSelf, "a short last page is padded");
            Assert.AreEqual("RunRelic1", EventSystem.current.currentSelectedGameObject.name);

            _input.TabPrevDown = true;
            yield return null;
            _input.ClearOneFrameFlags();

            Assert.AreEqual("1 / 2", TextOf("RunRelicPageLabel"), "LB did not page back");
        }

        // The footer names what the shoulders do, and offers no A: nothing on
        // this pane does anything when pressed.
        [UnityTest]
        public IEnumerator TheFooterOffersThePagerAndNoSelect()
        {
            ARunWithEverything(relics: 7);
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;
            EventSystem.current.SetSelectedGameObject(Named("RunRelic0"));
            yield return null;

            var prompts = _menu.CurrentPrompts();
            CollectionAssert.AreEqual(
                new[] { PromptButton.Cancel, PromptButton.Tabs, PromptButton.Pages },
                prompts.Select(p => p.Button).ToList());
            Assert.AreEqual("Page", prompts.Last().Label.Format());
        }

        // A on the tab that is already showing selects nothing, and the menu
        // opens with focus there, so the footer offers no A on it (the state the
        // pane is in the moment it opens, and in a capture). It still names the
        // pager while there is one.
        [UnityTest]
        public IEnumerator OnTheTabAlreadyShowingTheFooterOffersNoSelect()
        {
            ARunWithEverything(relics: 7);
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;
            EventSystem.current.SetSelectedGameObject(Named("SystemTabRun"));
            yield return null;

            CollectionAssert.AreEqual(
                new[] { PromptButton.Cancel, PromptButton.Tabs, PromptButton.Pages },
                _menu.CurrentPrompts().Select(p => p.Button).ToList());
        }

        // On any other tab A does switch to it, so the frame's row stands there.
        [UnityTest]
        public IEnumerator OnAnotherTabTheFrameOffersSelect()
        {
            ARunWithEverything(relics: 7);
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;
            EventSystem.current.SetSelectedGameObject(Named("SystemTabParty"));
            yield return null;

            CollectionAssert.AreEqual(
                new[] { PromptButton.Confirm, PromptButton.Cancel, PromptButton.Tabs, PromptButton.Pages },
                _menu.CurrentPrompts().Select(p => p.Button).ToList());
        }

        // LB/RB turn relic pages, so with one page there is nothing for them to
        // do and the footer says nothing about them, on the rows or off them.
        [UnityTest]
        public IEnumerator WithOneRelicPageTheFooterNamesNoPager()
        {
            ARunWithEverything(relics: 3);
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;

            EventSystem.current.SetSelectedGameObject(Named("RunRelic0"));
            yield return null;
            CollectionAssert.AreEqual(
                new[] { PromptButton.Cancel, PromptButton.Tabs },
                _menu.CurrentPrompts().Select(p => p.Button).ToList());

            EventSystem.current.SetSelectedGameObject(Named("SystemTabParty"));
            yield return null;
            CollectionAssert.AreEqual(
                new[] { PromptButton.Confirm, PromptButton.Cancel, PromptButton.Tabs },
                _menu.CurrentPrompts().Select(p => p.Button).ToList());
        }

        // ---- the lintel's gold: one source per context (plan C4) -----------------------

        [UnityTest]
        public IEnumerator InADescentTheLintelShowsTheRunsGoldAndNotTheSaves()
        {
            ARunWithEverything(relics: 1);
            SaveSlotManager.CurrentSave.Gold = 7;
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;

            Assert.AreEqual("Gold 128", TextOf("SystemLintelGold"));
            Assert.AreEqual("128", TextOf("RunGoldValue"), "fixture: the pane's own gold line");
        }

        [UnityTest]
        public IEnumerator InAFightTheLintelShowsTheRunsGoldToo()
        {
            ARunWithEverything(relics: 1);
            SaveSlotManager.CurrentSave.Gold = 7;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Find();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;

            Assert.AreEqual("Gold 128", TextOf("SystemLintelGold"));
        }

        [UnityTest]
        public IEnumerator AtCampTheLintelShowsTheSavesGold()
        {
            SaveSlotManager.CurrentSave.Gold = 7;
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Find();

            _menu.Open();
            yield return null;
            yield return null;

            Assert.AreEqual("Gold 7", TextOf("SystemLintelGold"));
        }

        // ---- supplies art -----------------------------------------------------------

        // A supply row shows its item's art exactly when the item authors some.
        // Both potions author none today (AUDIT #131 records them as icon-less on
        // purpose), so their rows leave the icon off; the day art lands in
        // items.json this fails if the pane's icon table did not come with it.
        [UnityTest]
        public IEnumerator ASupplyRowShowsItsItemsArtExactlyWhenTheItemHasAny()
        {
            ARunWithEverything(relics: 1);
            yield return LoadMap();

            _menu.Open(SystemMenuTab.Run);
            yield return null;
            yield return null;

            var stacks = RunOrchestrator.BuildSatchel();
            Assert.GreaterOrEqual(stacks.Count, 2, "fixture: the satchel holds both potions");

            for (int i = 0; i < stacks.Count && i < 4; i++)
            {
                bool authored = !string.IsNullOrEmpty(ContentDatabase.GetItem(stacks[i].ItemId).iconPath);
                Assert.AreEqual(authored, Named($"RunSupply{i}Icon").GetComponent<Image>().enabled,
                    $"{stacks[i].ItemId}: the icon is {(authored ? "missing" : "showing")} against its content");
            }
        }

        // ---- fixture -----------------------------------------------------------------

        private void Find()
        {
            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            if (module != null) EventSystem.current = module.GetComponent<EventSystem>();

            _menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(_menu, "this scene has no SystemMenuController");
        }

        private void TakeOverInput()
        {
            var module = Object.FindAnyObjectByType<NavigationInputModule>(FindObjectsInactive.Include);
            Assert.IsNotNull(module, "this scene's EventSystem is not running NavigationInputModule");
            _input = module.gameObject.AddComponent<ScriptedBaseInput>();
            module.inputOverride = _input;
        }

        private IEnumerator LoadMap()
        {
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Find();
        }

        private GameObject Named(string nodeName)
        {
            var go = _menu.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == nodeName)?.gameObject;
            Assert.IsNotNull(go, $"the RUN pane has no '{nodeName}'");
            return go;
        }

        private string TextOf(string nodeName)
        {
            var label = Named(nodeName).GetComponent<TMP_Text>();
            Assert.IsNotNull(label, $"'{nodeName}' carries no text");
            return label.text;
        }

        private static string RelicRowsRarityText(string id) =>
            Domain.Content.RelicRarityNames.Of(ContentDatabase.GetRelic(id).Data.Rarity);

        // A run with two rooms cleared, `relics` relics in held order, one event
        // buff, six percent bonus damage, and a health and a mana potion in the
        // stockpile. Returns the relic ids.
        private static List<string> ARunWithEverything(int relics)
        {
            RunManager.StartRun(4242);
            var run = RunManager.Run;

            var ids = ContentDatabase.Relics.Where(r => r != null).Take(relics).Select(r => r.id).ToList();
            Assert.AreEqual(relics, ids.Count, "fixture: the content has too few relics");
            run.relicIds.Clear();
            run.relicIds.AddRange(ids);

            run.eventBuffs.Add(new EventBuffEntry { kind = EventBuffs.PrincesFavor, amount = 2 });
            run.bonusDamagePercent = 6;

            // Gold arrives through BankPayout, which is the one seam that
            // credits both held and earned -- RecordRoom stopped taking a gold
            // argument when treasure and shop sales turned out to bypass it.
            RunLedger.RecordRoom(run, won: true, expGained: 15, step: 3);
            RunManager.BankPayout(40);
            RunLedger.RecordRoom(run, won: true, expGained: 10, step: 5);
            RunManager.BankPayout(25);
            run.gold = 128;

            var stash = SaveSlotManager.CurrentSave.stockpiledItems;
            InventoryOps.Add(stash, "health_potion", 2);
            InventoryOps.Add(stash, "mana_potion", 1);
            return ids;
        }

        // A run with a history, written literally rather than computed. Nothing
        // here re-derives a production sum to build its own expectation -- that
        // makes a test a tautology, and it has already masked a rounding flake
        // in this project once.
        private static RunSnapshot ARunThatHasDoneThings() => new RunSnapshot
        {
            hasRun = true,
            floor = 2,
            step = 9,
            deepestStep = 9,
            roomsCleared = 6,
            gold = 128,
            goldEarned = 640,
            expEarned = 210,
            bossesKilled = new List<string> { "boss_a" },
            relicIds = new List<string> { "relic_a", "relic_b" },
            inventory = new List<InventoryEntry>
            {
                new InventoryEntry { itemId = "salve", count = 3 },
                new InventoryEntry { itemId = "blade", count = 2 },
            },
            ledger = new List<RunLedgerEntry>
            {
                new RunLedgerEntry
                {
                    characterId = "a",
                    physicalDealt = 300, otherDealt = 120, damageTaken = 80,
                    healed = 40, shielded = 25, kills = 5, timesDowned = 1,
                },
                new RunLedgerEntry
                {
                    characterId = "b",
                    physicalDealt = 40, otherDealt = 10, damageTaken = 15,
                    healed = 20, shielded = 0, kills = 2, timesDowned = 0,
                },
            },
        };
    }
}
