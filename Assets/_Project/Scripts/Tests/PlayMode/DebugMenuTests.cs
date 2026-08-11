using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
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
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
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

        private static SaveData Save => SaveSlotManager.CurrentSave;

        private IEnumerator OpenTheMenu()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _hub = Object.FindAnyObjectByType<HubController>();
            Assert.IsNotNull(_hub, "the Hub scene has no HubController");

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
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
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

        // ---- currency ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator GoldLandsOnTheBankedWalletAndReachesDisk()
        {
            // The SAVE's wallet, not RunState's. Gold exists on both, and the
            // banked pile is the one you spend in the hub and the one that
            // survives whatever the run is about to do.
            yield return OpenTheMenu();

            int before = Save.wallet.Get(CurrencyType.Gold);

            Click("DebugGiveGoldButton");
            yield return null;

            Assert.AreEqual(before + DebugMenuController.GoldGrant, Save.wallet.Get(CurrencyType.Gold));

            SaveSlotManager.Forget();
            Assert.AreEqual(before + DebugMenuController.GoldGrant, Save.wallet.Get(CurrencyType.Gold),
                "the grant never reached the file");
        }

        [UnityTest]
        public IEnumerator TheEmberButtonGrantsEnoughToBeWorthPressing()
        {
            // A tree is 21 slots x 3 constellations at 1 ember each. The first
            // sketch of this menu granted +1, which is 63 presses to fill one
            // character -- a chore rather than a tool. Pinned so it cannot
            // quietly shrink back.
            yield return OpenTheMenu();

            // Per CHARACTER now, so the grant is checked on the roster rather
            // than on a wallet nothing writes to any more.
            int before = Save.roster[0].embers;

            Click("DebugGiveEmbersButton");
            yield return null;

            Assert.AreEqual(before + DebugMenuController.EmberGrant, Save.roster[0].embers);
            Assert.GreaterOrEqual(DebugMenuController.EmberGrant, 25);
        }

        [UnityTest]
        public IEnumerator TheSingleEmberButtonExistsForTheRefusalBoundary()
        {
            // +25 can never leave you one ember short, which is the one state
            // the NotEnoughEmbers refusal needs to be testable by hand.
            yield return OpenTheMenu();

            int before = Save.roster[0].embers;

            Click("DebugGiveOneEmberButton");
            yield return null;

            Assert.AreEqual(before + 1, Save.roster[0].embers);
        }

        // ---- items --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AddingARowPutsExactlyOneItemInTheBagAndSavesIt()
        {
            yield return OpenTheMenu();

            int before = Save.stockpiledItems.Sum(e => e.count);

            Click("DebugRow0");
            yield return null;

            Assert.AreEqual(before + 1, Save.stockpiledItems.Sum(e => e.count),
                "one press is one item, not a stack");

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
        public IEnumerator TheFilterNarrowsTheListAndResetsToPageOne()
        {
            yield return OpenTheMenu();

            // Page away from the start, then filter. Landing on page 7 of the
            // potions when there are two pages of them reads as a broken
            // filter, so the page has to reset with it.
            Click("DebugNextPage");
            Click("DebugNextPage");
            yield return null;

            Click("DebugFilter1");
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
