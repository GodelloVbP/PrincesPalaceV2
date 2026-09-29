using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 2, segment 5: a Shop
    // room reached from the Hub's own gate.
    //
    // The Hub has no Shop building: HubController.WireNavigation's four
    // staged buildings are Talents, Relics, Principality and
    // CharacterSheet. The in-run shop is a MAP ROOM
    // (ShopGamepadNavigationTests' fixture reaches it the same way). So
    // this segment is Hub -> the descent gate -> the relic draft (spent,
    // same as segment 1) -> the Map -> a Shop room's entry node -> Submit,
    // which walks there and opens the shop as a panel over the map
    // (MapController.Walk.Arrive's Arrival.Shop branch calls OpenShop() in
    // place -- no scene load). Leaving therefore returns to the same Map,
    // never "the hub": there is no path from this shop straight to the hub,
    // on the pad or on the mouse.
    public class JourneyHubToShopTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-shop-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            HubController.MotionSpeedMultiplier = 100000f;
        }

        [TearDown]
        public void Restore()
        {
            TestGlobals.ResetAll();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // Same shape as JourneyToFirstFightTests' own
        // SeedWithAPlainFightAtDepth1Entry, for a Shop room instead of a
        // Fight one, and narrowed one step further: the shop's own FIRST
        // gear card (index 0, ShopGearCard0) must be priced, so the segment
        // needs no grid navigation across an unknown row -- the buy/refuse
        // pair is what this segment is about, and ShopGamepadNavigationTests
        // already owns proving Move across the gear grid itself. Computed
        // live, never pinned as a magic literal (this project's own
        // established seed-search idiom).
        private static ulong SeedWithAShopAtDepth1EntryAndAPricedFirstCard()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = DescentMapGenerator.GenerateLegFor(seed, 0);
                var entry = map.AtDepth(1).FirstOrDefault();
                if (entry == null || entry.Type != RoomType.Shop) continue;

                RunManager.ResetForTests();
                RunManager.StartRun(seed);
                RunOrchestrator.ArriveAt(RunManager.Map.AtDepth(1).First());
                var card0 = RunOrchestrator.CurrentShopStock.FirstOrDefault(e =>
                    e != null && e.section == ShopStock.GearSection && e.index == 0
                    && !e.noOffer && !e.sold && e.price > 0);
                if (card0 != null) return seed;
            }

            throw new AssertionException(
                "No seed under 4000 puts a priced gear card at the shop's own first slot (depth 1 entry).");
        }

        private static GameObject NamedInScene(string name) =>
            Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(go => go.name == name && go.scene.IsValid());

        private static string ShopDetailText() =>
            NamedInScene("ShopDetailPanelBody")?.GetComponent<TMP_Text>()?.text ?? "";

        private IEnumerator ReachTheShop(ulong seed)
        {
            SaveSlotManager.EnterSlot(0);

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            EngineRoots.GrantToSquad();
            TakeOverInput();

            AssertSelectedName("StartRunGate", "the gate is the hub's own stated primary action and entry");

            // Prepared here, not driven -- JourneyToFirstFightTests' own
            // header on why seeding is orchestrator state rather than a pad
            // press, and why it happens after EnterSlot rather than before.
            RunOrchestrator.StartRun(seed);

            yield return PressSubmit(); // StartOrResumeRun -> BeginDescentTransition -> EnterTheDescent
            yield return WaitUntil(() =>
            {
                var draft = Node("DraftCard0");
                return draft != null && draft.activeInHierarchy;
            }, 5f, "the gate never opened the relic draft on a fresh run");
            yield return null;

            yield return PressSubmit(); // Select(0) -- takes the entry card
            yield return MoveDown(); // Descend, RefreshNavigation's own explicit link
            yield return PressSubmit(); // Commit -> FinishDraft, Navigation.Go(Map)

            yield return WaitForScene("Map", 5f, "Descend should commit the draft and load the Map");
            yield return null;
            yield return null;
            TakeOverInput();

            var entryNode = RunManager.Map.AtDepth(1).First();
            Assert.AreEqual(RoomType.Shop, entryNode.Type,
                $"fixture: seed {seed} should put a Shop room at the map's own entry (depth 1, slot 0)");

            string entryName = "MapNode" + MapLayout.IndexFor(entryNode.Depth, entryNode.Slot);
            AssertSelectedName(entryName, "the current node's first reachable choice is the declared entry");

            yield return PressSubmit(); // walk to the room -> Arrival.Shop -> OpenShop() in place
            yield return WaitUntil(() =>
            {
                var card = Node("ShopGearCard0");
                return card != null && card.activeInHierarchy;
            }, 5f, "Submit on the shop's entry should walk there and open the shop");
            yield return null;

            AssertSelectedName("ShopGearCard0", "the buy grid's entry is its first card");
        }

        // BOTH ELIGIBILITY SHAPES (plan section 2's own verification note),
        // in the order the master brief states them, against the SAME card:
        // refused a gold short, then bought once the purse can cover it --
        // one continuous session rather than two disconnected fixtures.
        [UnityTest]
        public IEnumerator BuyingTheEntryCard_RefusesAGoldShort_ThenBuysExactlyOnceAffordable()
        {
            ulong seed = SeedWithAShopAtDepth1EntryAndAPricedFirstCard();
            yield return ReachTheShop(seed);

            var card = RunOrchestrator.CurrentShopStock.First(e =>
                e != null && e.section == ShopStock.GearSection && e.index == 0);

            // ---- refused: a gold short ----------------------------------------
            RunManager.Run.gold = card.price - 1;
            int goldShort = RunManager.Run.gold;

            yield return PressSubmit(); // OnCardPressed -> select the entry card for purchase
            yield return MoveRight(); // Gear is a 2-column Grid -- col0 -> col1, same row
            yield return MoveRight(); // Gear's right column -> the actions List, entry ShopBuyButton
            AssertSelectedName("ShopBuyButton", "should be standing on Buy before either press");

            yield return PressSubmit(); // Buy() -> Commit() -> refused, NotEnoughGold

            Assert.AreEqual(goldShort, RunManager.Run.gold,
                "a purchase one gold short of the price should not have gone through");
            StringAssert.Contains("Not enough gold", ShopDetailText(),
                "the refusal should be painted, not a silent dead press");

            // ---- affordable: the same card, now within reach -----------------
            RunManager.Run.gold = card.price + 500;
            int goldBefore = RunManager.Run.gold;

            yield return MoveLeft(); // back onto the Gear grid's right column
            yield return MoveLeft(); // back onto the entry card, col0
            AssertSelectedName("ShopGearCard0", "should be back on the entry card before re-selecting it");

            yield return PressSubmit(); // re-select the card as the purchase target
            yield return MoveRight();
            yield return MoveRight();
            AssertSelectedName("ShopBuyButton", "should be standing on Buy again before the affordable press");

            yield return PressSubmit(); // Buy() -> Commit() -> applied, exactly once

            Assert.AreEqual(goldBefore - card.price, RunManager.Run.gold,
                "Submit on the card then Submit on Buy should purchase it exactly once, for exactly its price");
        }

        [UnityTest]
        public IEnumerator CancelCancel_LeavesTheShop_BackOntoTheSameMap()
        {
            ulong seed = SeedWithAShopAtDepth1EntryAndAPricedFirstCard();
            yield return ReachTheShop(seed);

            yield return PressCancel(); // arms leaving, same first press ShopLeaveButton itself needs
            yield return PressCancel(); // confirms leaving

            yield return WaitUntil(() =>
            {
                var card = Node("ShopGearCard0");
                return card == null || !card.activeInHierarchy;
            }, 5f, "two Cancels should have closed the shop");
            yield return null;

            AssertTopIsNotFight("leaving the shop should never leave Fight's own non-selecting context on top");

            var selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            Assert.IsNotNull(selected, "leaving the shop should restore a selection on the Map underneath it");
            StringAssert.StartsWith("MapNode", selected.name,
                "leaving the shop should land back on the Map's own node graph, not strand selection " +
                "somewhere else -- there is no path from this shop straight to the hub");
        }
    }
}
