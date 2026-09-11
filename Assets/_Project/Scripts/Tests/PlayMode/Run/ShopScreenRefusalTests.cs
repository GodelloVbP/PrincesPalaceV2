using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // THE SHOP SAYS WHY IT REFUSED (AUDIT #115).
    //
    // ShopController's own header states the design -- "only BUY can refuse,
    // and it refuses through RunOrchestrator's own ShopResult rather than a
    // client-side guess" -- and ShopResult's says "the screen can say so". It
    // did not: Commit read only `result.Applied` and Reroll and SellRow threw
    // the result away, so pressing BUY one gold short did nothing, said
    // nothing, and looked exactly like a dead button.
    //
    // Driven through the buttons a hand presses rather than by calling
    // RunOrchestrator, because the mechanism was never in doubt -- the gap was
    // between the orchestrator's answer and the screen, and only a press
    // crosses it. ShopMutationTests owns the orchestrator half.
    public class ShopScreenRefusalTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-shop-refusal-" + System.Guid.NewGuid().ToString("N"));
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
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // Lifted from ShopCaptureTests, which lifted it from ShopArrivalTests:
        // the generator is pure and reads no content, so hunting a seed whose
        // first column offers a shop costs nothing and needs no save writes.
        private static ulong SeedWithAShopInTheFirstColumn()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = DescentMapGenerator.GenerateLeg(new SeededRandom(seed), 0);
                if (map.AtDepth(1).Any(n => n.Type == RoomType.Shop)) return seed;
            }

            throw new AssertionException("No seed under 4000 generated a shop in the first column.");
        }

        private static GameObject Named(string name) =>
            Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(go => go.name == name && go.scene.IsValid());

        private static void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}' in the scene");
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, $"'{name}' has no Button to press");
            button.onClick.Invoke();
        }

        private static string DetailText() =>
            Named("ShopDetailLabel")?.GetComponent<TMP_Text>()?.text ?? "";

        [UnityTest]
        public IEnumerator BuyingACardYouCannotAffordSaysWhy()
        {
            RunManager.StartRun(SeedWithAShopInTheFirstColumn());

            var node = RunManager.Map.AtDepth(1).First(n => n.Type == RoomType.Shop);
            RunOrchestrator.ArriveAt(node);

            // The first gear card with a real price, and a purse exactly one
            // gold short of it. One below rather than zero: a purse at zero
            // would also refuse every OTHER card, and the assertion below is
            // about the card that was actually pressed.
            var card = RunOrchestrator.CurrentShopStock
                .FirstOrDefault(e => e != null && e.section == ShopStock.GearSection
                                     && !e.noOffer && !e.sold && e.price > 0);
            Assert.IsNotNull(card, "the shop rolled no priced gear card, so nothing here can be refused");

            RunManager.Run.gold = card.price - 1;
            SaveSlotManager.SaveCurrent();

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var map = Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            Assert.IsNotNull(map, "the Map scene has no MapController");
            map.OpenShop();
            yield return null;

            int goldBefore = RunManager.Run.gold;

            // ONE press selects, BUY commits -- a second press on the card
            // would commit too, and going through BUY is the path the finding
            // named.
            Click($"ShopGearCard{card.index}");
            yield return null;
            Click("ShopBuyButton");
            yield return null;

            Assert.AreEqual(goldBefore, RunManager.Run.gold,
                "the purchase went through despite the purse being a gold short");
            StringAssert.Contains("Not enough gold", DetailText(),
                "BUY refused and the screen said nothing at all, which reads as a dead button");
        }

        // AND THE REFUSAL CLEARS. A line that outlives the card it was about
        // is a second bug wearing the first one's clothes -- the detail label
        // is where the NEXT card's description has to land.
        [UnityTest]
        public IEnumerator SelectingAnotherCardClearsTheRefusal()
        {
            RunManager.StartRun(SeedWithAShopInTheFirstColumn());

            var node = RunManager.Map.AtDepth(1).First(n => n.Type == RoomType.Shop);
            RunOrchestrator.ArriveAt(node);

            var cards = RunOrchestrator.CurrentShopStock
                .Where(e => e != null && e.section == ShopStock.GearSection
                            && !e.noOffer && !e.sold && e.price > 0)
                .OrderBy(e => e.index)
                .ToList();
            Assert.GreaterOrEqual(cards.Count, 2, "this needs two priced gear cards to move between");

            RunManager.Run.gold = 0;
            SaveSlotManager.SaveCurrent();

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var map = Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            Assert.IsNotNull(map, "the Map scene has no MapController");
            map.OpenShop();
            yield return null;

            Click($"ShopGearCard{cards[0].index}");
            yield return null;
            Click("ShopBuyButton");
            yield return null;
            StringAssert.Contains("Not enough gold", DetailText(), "fixture: the refusal should be up");

            Click($"ShopGearCard{cards[1].index}");
            yield return null;

            StringAssert.DoesNotContain("Not enough gold", DetailText(),
                "the refusal survived onto a card it was never about");
        }
    }
}
