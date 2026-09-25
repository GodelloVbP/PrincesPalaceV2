using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.DebugMenu;
using PrincesPalace.Domain.Economy;

namespace PrincesPalace.PlayModeTests
{
    // The debug menu, driven through the real hub scene.
    //
    // The filter and page arithmetic are covered without a scene by
    // DebugMenuCatalogTests. What is left for PlayMode is what genuinely needs
    // one: that the buttons reach the wallet and the bag, and that a grant
    // survives being written to disk -- a debug tool that appears to work and
    // silently does not persist would send you hunting a bug in whatever you
    // were actually testing.
    //
    // REBUILT 2026-09-23 (debug menu overhaul): category buttons replace the
    // old kind filter and the currency buttons, and the grant bar, books,
    // relics, levels and the Tools rows are new coverage.
    public class DebugMenuTests
    {
        private string _root;
        private HubController _hub;
        private DebugMenuController _debug;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-debug-" + System.Guid.NewGuid().ToString("N"));
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

            // Every global this fixture touched, plus the ones it did not --
            // one call, so the list cannot go stale here while it grows
            // somewhere else. See TestGlobals.
            TestGlobals.ResetAll();

            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private GameObject Named(string name) =>
            _hub.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        // The event and counter rows move whenever an event is authored (one
        // open row per event, then two per counter), so those tests find
        // their row by its label rather than by a position.
        private string RowLabelled(string prefix)
        {
            for (int i = 0; i < DebugMenuCatalog.RowsPerPage; i++)
            {
                var label = Named($"DebugRow{i}Name");
                if (label != null && label.GetComponent<TMP_Text>().text.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return $"DebugRow{i}";
            }

            throw new AssertionException($"no debug row labelled '{prefix}...' on this page");
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;

        // THE HUB IS SHARED ACROSS THIS FIXTURE (SharedScene), so what one
        // test left on it is put back here, through the same buttons a hand
        // would press. The save needs nothing: [SetUp] points every test at a
        // fresh root and the menu reads SaveSlotManager.CurrentSave live.
        //
        // What does need it is the menu's own view state, which by design
        // only a scene reload resets (DebugMenuController: plus and quantity
        // are sticky, category and page persist), plus the toast, whose text
        // outlives the grant that wrote it -- a refusal test would otherwise
        // pass on the previous test's refusal.
        private IEnumerator TheHub()
        {
            yield return SharedScene.Ensure("Hub");

            _hub = Object.FindAnyObjectByType<HubController>();
            Assert.IsNotNull(_hub, "the Hub scene has no HubController");

            _hub.SetDebugMenu(false);
            _hub.SetCharacterOverlay(false);

            // On a fresh load these land on a menu whose Start has not wired
            // its listeners yet, and do nothing -- the values are already the
            // opening ones.
            Click("DebugCategory0");
            Click("DebugQty0");
            // 10 is ItemUpgrade.MaxPlus; PlusClampsAtTen pins it.
            for (int i = 0; i < 10; i++) Click("DebugPlusMinusButton");
            Named("DebugToastLabel").GetComponent<TMP_Text>().text = "";
        }

        private IEnumerator OpenTheMenu()
        {
            yield return TheHub();

            _debug = _hub.GetComponentInChildren<DebugMenuController>(includeInactive: true);
            Assert.IsNotNull(_debug, "the debug menu was never wired into the hub");

            _hub.SetDebugMenu(true);

            // Start() runs one frame AFTER SetActive, so the listeners do not
            // exist yet (CODE_STANDARDS section 5).
            yield return null;
            yield return null;
        }

        // ---- opening and closing -------------------------------------------------

        [UnityTest]
        public IEnumerator TheMenuStartsClosedAndTheToggleOpensIt()
        {
            // Not TheHub(): its reset closes the menu, and this asserts that a
            // freshly loaded hub opens with it closed.
            SharedScene.MarkDirty("asserts what a freshly loaded hub opens with, not what a reset put back");
            yield return SharedScene.Ensure("Hub");
            _hub = Object.FindAnyObjectByType<HubController>();

            Assert.IsFalse(_hub.DebugMenuIsOpen, "the hub opens with the debug menu down");

            _hub.ToggleDebugMenu();
            yield return null;
            Assert.IsTrue(_hub.DebugMenuIsOpen);

            _hub.ToggleDebugMenu();
            yield return null;
            Assert.IsFalse(_hub.DebugMenuIsOpen);
        }

        [UnityTest]
        public IEnumerator ClosingLeavesTheHubStanding()
        {
            yield return OpenTheMenu();

            Click("DebugCloseButton");
            yield return null;

            Assert.IsFalse(_hub.DebugMenuIsOpen);
            Assert.IsTrue(_hub.gameObject.activeInHierarchy);
        }

        private void Select(DebugCategory category) => Click($"DebugCategory{(int)category}");

        private string Toast => Named("DebugToastLabel").GetComponent<TMP_Text>().text;

        // ---- Resources: gold, embers, levels ----------------------------------------
        //
        // Row order is ResourceActions' declared order: 0-2 gold (+100,
        // +1000, +10000), 3 run gold, 4-5 embers (+1, +25), then two rows per
        // roster member (+1 level, max level).

        [UnityTest]
        public IEnumerator GoldLandsOnTheBankedWalletAndReachesDisk()
        {
            // The SAVE's wallet, not RunState's. Gold exists on both, and the
            // banked pile is the one you spend in the hub and the one that
            // survives whatever the run is about to do.
            yield return OpenTheMenu();

            int before = Save.wallet.Get(CurrencyType.Gold);

            Select(DebugCategory.Resources);
            Click("DebugRow2");
            yield return null;

            Assert.AreEqual(before + 10000, Save.wallet.Get(CurrencyType.Gold));

            SaveSlotManager.Forget();
            Assert.AreEqual(before + 10000, Save.wallet.Get(CurrencyType.Gold),
                "the grant never reached the file");
        }

        [UnityTest]
        public IEnumerator EmbersReachEveryRosterMemberInBothSizes()
        {
            // +25 is the row you press (a tree is 21 slots x 3 constellations
            // at 1 ember each); +1 exists for the NotEnoughEmbers boundary,
            // which +25 can never leave you on.
            yield return OpenTheMenu();

            var before = Save.roster.Select(c => c.embers).ToList();

            Select(DebugCategory.Resources);
            Click("DebugRow4");
            Click("DebugRow5");
            yield return null;

            for (int i = 0; i < Save.roster.Count; i++)
            {
                Assert.AreEqual(before[i] + 26, Save.roster[i].embers, $"roster member {i}");
            }
        }

        [UnityTest]
        public IEnumerator PlusOneLevelMovesExactlyOneLevel()
        {
            yield return OpenTheMenu();

            int before = Save.roster[0].level;

            Select(DebugCategory.Resources);
            Click("DebugRow6");
            yield return null;

            Assert.AreEqual(before + 1, Save.roster[0].level);
        }

        [UnityTest]
        public IEnumerator MaxLevelStopsAtTheCap()
        {
            // 40, literally: the cap the level table is authored for (see
            // LevelCurve.MaxLevel's header). Not read back off the table, which
            // would only prove the loop agrees with itself.
            yield return OpenTheMenu();

            Select(DebugCategory.Resources);
            Click("DebugRow7");
            yield return null;

            Assert.AreEqual(40, Save.roster[0].level);
        }

        // ---- the run-scoped tabs --------------------------------------------------

        [UnityTest]
        public IEnumerator ABookWithNoRunIsRefusedWithAReason()
        {
            yield return OpenTheMenu();
            Assert.IsFalse(RunManager.HasRun, "fixture: the hub opens with no run");

            Select(DebugCategory.SpellBooks);
            yield return null;
            Assert.IsTrue(Named("DebugRow0").activeSelf, "fixture: content has book spells");

            Click("DebugRow0");
            yield return null;

            StringAssert.Contains("Start a run first", Toast);
        }

        [UnityTest]
        public IEnumerator ABookInARunLandsInTheUnassignedPileNotASlot()
        {
            yield return OpenTheMenu();
            RunManager.StartRun(20260923UL);

            Select(DebugCategory.SpellBooks);
            Click("DebugQty1"); // x5
            yield return null;

            int learnedBefore = RunManager.Run.learnedSpells.Count;

            Click("DebugRow0");
            yield return null;

            Assert.AreEqual(5, RunManager.Run.unassignedSpellBooks.Count);
            Assert.AreEqual(learnedBefore, RunManager.Run.learnedSpells.Count,
                "placing a book is the dossier's job, not the debug menu's");
        }

        [UnityTest]
        public IEnumerator ARelicIsGrantedOnceHoweverOftenItIsPressed()
        {
            yield return OpenTheMenu();
            RunManager.StartRun(20260923UL);

            Select(DebugCategory.Relics);
            Click("DebugQty2"); // x10 -- ignored for relics
            yield return null;

            Click("DebugRow0");
            Click("DebugRow0");
            yield return null;

            Assert.AreEqual(1, RunManager.Run.relicIds.Count);
        }

        // ---- Tools ----------------------------------------------------------------
        //
        // Row order is ToolActions' declared order: 0 heal, 1 jump to boss,
        // 2 next leg, 3 clear stash, 4 clear run bag, 5 respec, 6 reset tracks.

        [UnityTest]
        public IEnumerator JumpToBossPutsTheBossOnTheNextChoice()
        {
            yield return OpenTheMenu();
            RunManager.StartRun(20260923UL);

            Select(DebugCategory.Tools);
            Click("DebugRow1");
            yield return null;

            Assert.IsTrue(RunManager.Choices().Any(n => n.Id == RunManager.Map.Boss.Id),
                "after the jump the boss must be one step away");
        }

        [UnityTest]
        public IEnumerator NextLegMovesTheLegStart()
        {
            yield return OpenTheMenu();
            RunManager.StartRun(20260923UL);
            int before = RunManager.Run.legStartStep;

            Select(DebugCategory.Tools);
            Click("DebugRow2");
            yield return null;

            Assert.Greater(RunManager.Run.legStartStep, before);
        }

        [UnityTest]
        public IEnumerator ToolsThatNeedARunRefuseInTheHub()
        {
            yield return OpenTheMenu();

            Select(DebugCategory.Tools);
            Click("DebugRow0"); // heal
            yield return null;

            StringAssert.Contains("Start a run first", Toast);
        }

        [UnityTest]
        public IEnumerator ClearStashEmptiesItAndReachesDisk()
        {
            yield return OpenTheMenu();

            Click("DebugRow0"); // any weapon, so there is something to clear
            yield return null;
            Assert.IsNotEmpty(Save.stockpiledItems, "fixture");

            Select(DebugCategory.Tools);
            Click("DebugRow3");
            yield return null;

            SaveSlotManager.Forget();
            Assert.IsEmpty(Save.stockpiledItems);
        }

        [UnityTest]
        public IEnumerator ResetTracksPutsEveryWatermarkBackToZero()
        {
            yield return OpenTheMenu();

            foreach (var character in Save.roster) character.claimedTrackLevel = 3;

            Select(DebugCategory.Tools);
            Click("DebugRow6");
            yield return null;

            Assert.IsTrue(Save.roster.All(c => c.claimedTrackLevel == 0));
        }

        // Rows 7.. are the event rows, after the seven above: one "open" per
        // authored event, then an add and a reset per known counter -- found
        // by label (RowLabelled), since each new event shifts them.

        [UnityTest]
        public IEnumerator OpenEventOpensTheDemoWhereThePartyStands()
        {
            yield return OpenTheMenu();
            SharedScene.MarkDirty("opens an event over the hub, which this fixture has no cheap way to close");
            RunManager.StartRun(20260923UL);
            Select(DebugCategory.Tools);
            Click(RowLabelled("OPEN EVENT: demo_wishing_well"));
            yield return null;

            Assert.IsTrue(RunOrchestrator.EventIsOpen);
            Assert.AreEqual("demo_wishing_well", RunManager.Run.eventId);
            Assert.AreEqual(RunManager.Run.currentNodeId, RunManager.Run.eventNodeId);

            SaveSlotManager.Forget();
            Assert.AreEqual("demo_wishing_well", Save.activeRun.eventId, "and it reached the disk");
        }

        [UnityTest]
        public IEnumerator TheCounterRowsAddTheStickyQuantityAndReset()
        {
            yield return OpenTheMenu();

            Select(DebugCategory.Tools);
            Click("DebugQty2"); // x10
            Click(RowLabelled("wishing_well_tosses: +"));
            yield return null;
            Assert.AreEqual(10, Save.EventCounter("wishing_well_tosses"));

            Click(RowLabelled("wishing_well_tosses: RESET"));
            yield return null;
            Assert.AreEqual(0, Save.EventCounter("wishing_well_tosses"));
        }

        // ---- items --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AddingARowAtDefaultModifiersPutsExactlyOneItemInTheBagAndSavesIt()
        {
            // Default modifiers on open: +0, x1 -- same grant shape the old
            // menu always made, before plus/quantity existed at all.
            yield return OpenTheMenu();

            int before = Save.stockpiledItems.Sum(e => e.count);

            Click("DebugRow0");
            yield return null;

            Assert.AreEqual(before + 1, Save.stockpiledItems.Sum(e => e.count),
                "one press at the default x1 is one item, not a stack");

            SaveSlotManager.Forget();
            Assert.AreEqual(before + 1, Save.stockpiledItems.Sum(e => e.count),
                "the grant never reached the file");
        }

        [UnityTest]
        public IEnumerator TheRowsShowRealContentRatherThanBlanks()
        {
            // The catalogue is read from ContentDatabase at Refresh. If that
            // resolved to nothing, every assertion above would still pass
            // against an empty list and prove nothing.
            yield return OpenTheMenu();

            var label = Named("DebugRow0Name").GetComponent<TMP_Text>();

            Assert.IsTrue(Named("DebugRow0").activeSelf, "the first row is empty -- no content loaded");
            Assert.IsNotEmpty(label.text);
        }

        [UnityTest]
        public IEnumerator TheCategoryNarrowsTheListAndResetsToPageOne()
        {
            yield return OpenTheMenu();

            // Page away from the start, then switch category. Landing on
            // page 7 of Equipment when there are two pages of it reads as a
            // broken filter, so the page has to reset with it.
            Click("DebugNextPage");
            Click("DebugNextPage");
            yield return null;

            Select(DebugCategory.Equipment);
            yield return null;

            StringAssert.Contains("PAGE 1 OF", Named("DebugPageLabel").GetComponent<TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator UnusedRowsAreHiddenRatherThanDrawnEmpty()
        {
            yield return OpenTheMenu();

            int shown = Enumerable.Range(0, DebugMenuCatalog.RowsPerPage)
                .Count(i => Named($"DebugRow{i}").activeSelf);

            Assert.Greater(shown, 0, "fixture: content has items");
            Assert.LessOrEqual(shown, DebugMenuCatalog.RowsPerPage);
        }

        // ---- the grant bar: plus stepper and quantity selector -------------------------

        [UnityTest]
        public IEnumerator PlusAndQuantityAreStickyAndBothReachTheGrant()
        {
            // Contract 3: plus and quantity are STICKY modifiers, set once on
            // the grant bar and applied to whichever row is next clicked --
            // not per-row, and not reset by paging or filtering.
            yield return OpenTheMenu();

            for (int i = 0; i < 7; i++) Click("DebugPlusPlusButton");
            Click("DebugQty1"); // x5
            yield return null;

            // A clean stash, so the assertions below can only be about this
            // grant and not about whatever the fresh save started with.
            Save.stockpiledItems.Clear();

            Click("DebugRow0");
            yield return null;

            Assert.AreEqual(5, Save.stockpiledItems.Sum(e => e.count), "x5 must grant five, not one");
            Assert.IsTrue(Save.stockpiledItems.All(e => e.plus == 7),
                "the plus stepper's value must reach the grant");
        }

        [UnityTest]
        public IEnumerator PlusClampsAtTen()
        {
            // ItemUpgrade.MaxPlus, pinned as a literal per this repo's own
            // rule against a test recomputing a production formula for its
            // own expected value.
            yield return OpenTheMenu();

            for (int i = 0; i < 15; i++) Click("DebugPlusPlusButton");
            yield return null;

            StringAssert.Contains("+10", Named("DebugPlusLabel").GetComponent<TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator PlusCannotGoBelowZero()
        {
            yield return OpenTheMenu();

            Click("DebugPlusMinusButton");
            Click("DebugPlusMinusButton");
            yield return null;

            StringAssert.Contains("+0", Named("DebugPlusLabel").GetComponent<TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator PlusIsIgnoredWhenGrantingAConsumable()
        {
            yield return OpenTheMenu();

            Select(DebugCategory.Consumables);
            for (int i = 0; i < 5; i++) Click("DebugPlusPlusButton");
            yield return null;

            if (!Named("DebugRow0").activeSelf)
            {
                Assert.Ignore("fixture: no consumables in content");
                yield break;
            }

            Save.stockpiledItems.Clear();

            Click("DebugRow0");
            yield return null;

            Assert.IsNotEmpty(Save.stockpiledItems);
            Assert.IsTrue(Save.stockpiledItems.All(e => e.plus == 0),
                "a consumable grant must ignore the sticky plus stepper");
        }

        // ---- sets: one row grants every piece --------------------------------------

        [UnityTest]
        public IEnumerator GrantingASetsRowGrantsMoreThanOneItem()
        {
            // Contract 4. A weak assertion on purpose -- this fixture does
            // not pin which sets exist in content, only that clicking a Sets
            // row (when the fixture has one) behaves like a multi-piece
            // grant rather than a single-item one.
            yield return OpenTheMenu();

            Select(DebugCategory.Sets);
            yield return null;

            if (!Named("DebugRow0").activeSelf)
            {
                Assert.Ignore("fixture: no item sets in content");
                yield break;
            }

            int before = Save.stockpiledItems.Sum(e => e.count);

            Click("DebugRow0");
            yield return null;

            Assert.Greater(Save.stockpiledItems.Sum(e => e.count), before,
                "granting a Sets row must grant at least its pieces");

            SaveSlotManager.Forget();
            Assert.Greater(Save.stockpiledItems.Sum(e => e.count), before,
                "the set grant never reached the file");
        }

        // ---- stacking against the other overlay --------------------------------------

        [UnityTest]
        public IEnumerator EscapeClosesTheDebugMenuBeforeTheCharacterOverlay()
        {
            // Both are modals over the hub and the debug menu draws on top.
            // Closing the one you cannot see would be a nasty little surprise.
            yield return OpenTheMenu();

            _hub.SetCharacterOverlay(true);
            _hub.SetDebugMenu(true);
            yield return null;

            // Driving the same branch Update's Escape key runs, since legacy
            // Input cannot be simulated headlessly.
            _hub.SetDebugMenu(false);
            yield return null;

            Assert.IsFalse(_hub.DebugMenuIsOpen);
            Assert.IsTrue(_hub.CharacterOverlayIsOpen, "the overlay underneath is untouched");
        }
    }
}
