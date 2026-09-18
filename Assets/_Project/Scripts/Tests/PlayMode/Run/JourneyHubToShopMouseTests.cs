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
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // docs/GAMEPAD_NAVIGATION_PLAN.md phase 4, item 3: segment 5's mouse-only
    // regression -- see JourneyHubToShopTests' own header for why this is
    // Hub -> gate -> the relic draft -> Map -> a Shop room's own entry
    // rather than a Hub building (the Hub has no Shop building at all).
    // Every Move-then-Submit walk across the Gear grid collapses to a click
    // on the card, then a click on Buy -- there is no "aim across the grid"
    // for a pointer, only the two controls themselves.
    public class JourneyHubToShopMouseTests : JourneyFixture
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-journey-shop-mouse-" + System.Guid.NewGuid().ToString("N"));
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

        private static ulong SeedWithAShopAtDepth1EntryAndAPricedFirstCard()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = DescentMapGenerator.GenerateLeg(new SeededRandom(seed), 0);
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
            NamedInScene("ShopDetailLabel")?.GetComponent<TMP_Text>()?.text ?? "";

        private IEnumerator ReachTheShop(ulong seed)
        {
            SaveSlotManager.EnterSlot(0);

            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            RunOrchestrator.StartRun(seed);

            yield return Click(Node("StartRunGate")); // StartOrResumeRun -> BeginDescentTransition -> EnterTheDescent
            yield return WaitUntil(() =>
            {
                var draft = Node("DraftCard0");
                return draft != null && draft.activeInHierarchy;
            }, 5f, "clicking the gate never opened the relic draft on a fresh run");
            yield return null;

            yield return Click(Node("DraftCard0")); // Select(0) -- takes the entry card
            yield return Click(Node("DraftDescendButton")); // Commit -> FinishDraft, Navigation.Go(Map)

            yield return WaitForScene("Map", 5f, "Descend should commit the draft and load the Map");
            yield return null;
            yield return null;
            TakeOverInput();
            // A fresh scene's own layout can still be mid-settle the frame it
            // activates -- this suite found that gap under the full parallel
            // gate (never under a single-class or single-area slice), so every
            // mouse click aimed at a screen coordinate waits real time here
            // first, not just the two engine frames TakeOverInput's own callers
            // already pay.
            yield return new WaitForSecondsRealtime(0.5f);
            var entryNode = RunManager.Map.AtDepth(1).First();
            Assert.AreEqual(RoomType.Shop, entryNode.Type,
                $"fixture: seed {seed} should put a Shop room at the map's own entry (depth 1, slot 0)");

            string entryName = "MapNode" + MapLayout.IndexFor(entryNode.Depth, entryNode.Slot);

            yield return Click(Node(entryName)); // walk to the room -> Arrival.Shop -> OpenShop() in place
            yield return WaitUntil(() =>
            {
                var card = Node("ShopGearCard0");
                return card != null && card.activeInHierarchy;
            }, 5f, "clicking the shop's entry should walk there and open the shop");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuyingTheEntryCard_RefusesAGoldShort_ThenBuysExactlyOnceAffordable_MouseOnly()
        {
            ulong seed = SeedWithAShopAtDepth1EntryAndAPricedFirstCard();
            yield return ReachTheShop(seed);

            var card = RunOrchestrator.CurrentShopStock.First(e =>
                e != null && e.section == ShopStock.GearSection && e.index == 0);

            // ---- refused: a gold short ----------------------------------------
            RunManager.Run.gold = card.price - 1;
            int goldShort = RunManager.Run.gold;

            yield return Click(Node("ShopGearCard0")); // OnCardPressed -> select the entry card for purchase
            yield return Click(Node("ShopBuyButton")); // Buy() -> Commit() -> refused, NotEnoughGold

            Assert.AreEqual(goldShort, RunManager.Run.gold,
                "a purchase one gold short of the price should not have gone through");
            StringAssert.Contains("Not enough gold", ShopDetailText(),
                "the refusal should be painted, not a silent dead press");

            // ---- affordable: the same card, now within reach -----------------
            RunManager.Run.gold = card.price + 500;
            int goldBefore = RunManager.Run.gold;

            yield return Click(Node("ShopGearCard0")); // re-select the card as the purchase target
            yield return Click(Node("ShopBuyButton")); // Buy() -> Commit() -> applied, exactly once

            Assert.AreEqual(goldBefore - card.price, RunManager.Run.gold,
                "clicking the card then Buy should purchase it exactly once, for exactly its price, mouse-only " +
                "same as on the pad");
        }

        [UnityTest]
        public IEnumerator LeaveLeave_LeavesTheShop_BackOntoTheSameMap_MouseOnly()
        {
            ulong seed = SeedWithAShopAtDepth1EntryAndAPricedFirstCard();
            yield return ReachTheShop(seed);

            yield return Click(Node("ShopLeaveButton")); // arms leaving, the same first click the pad's own first Cancel needs
            yield return Click(Node("ShopLeaveButton")); // confirms leaving

            yield return WaitUntil(() =>
            {
                var card = Node("ShopGearCard0");
                return card == null || !card.activeInHierarchy;
            }, 5f, "two clicks on Leave should have closed the shop");
            yield return null;

            AssertTopIsNotFight("leaving the shop should never leave Fight's own non-selecting context on top");

            var selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            Assert.IsNotNull(selected, "leaving the shop should restore a selection on the Map underneath it -- " +
                "the dispatcher's own reselection rule runs the same way regardless of input mode");
            StringAssert.StartsWith("MapNode", selected.name,
                "leaving the shop should land back on the Map's own node graph");
        }
    }
}
