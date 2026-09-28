using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // AN EVENT'S SHELF ON THE SHOP SCREEN (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md
    // M5), in the real Map scene: the event hands over to the shelf, the shelf
    // shows what a merchant has (gear, and consumables under their own header
    // in the book panel) and stands down what it has not (relics, rerolls,
    // the pack), a revealed fake wears its mark, and LEAVE goes back to the
    // event rather than the map. CaravanShelfRunTests owns the rules.
    public class CaravanShelfScreenTests
    {
        private const string Caravan = "m5_screen_caravan";
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-caravan-screen-" + Guid.NewGuid().ToString("N"));
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
            ContentDatabase.Reset();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static RawEventChoice Choice(string text, string goTo, params RawEventEffect[] effects) =>
            new RawEventChoice { text = text, outcomes = new[] { new RawEventOutcome { goTo = goTo, effects = effects } } };

        private static RawEventEntry Fixture() => new RawEventEntry
        {
            id = Caravan,
            mayReturn = true,
            floors = new[] { 999 },
            pages = new[]
            {
                new RawEventPage
                {
                    id = "caravan", title = "Caravan", body = "B",
                    choices = new[]
                    {
                        Choice("Browse with Odette", "after_browse",
                            new RawEventEffect { kind = "shelf", shelf = "wares", reveal = true }),
                        Choice("Walk on", "Leave"),
                    },
                },
                new RawEventPage
                {
                    id = "after_browse", title = "Caravan", body = "B",
                    choices = new[] { Choice("Walk on", "Leave") },
                },
            },
            shelves = new[]
            {
                new RawEventShelf
                {
                    id = "wares", priceFactorPercent = 70, fakeShare = 3, sections = new[] { "gear" }, consumableCount = 2,
                },
            },
        };

        private static GameObject Named(string name) =>
            Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(go => go.name == name && go.scene.IsValid());

        private static void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}' in the scene");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private static string Text(string name) => Named(name)?.GetComponent<TMP_Text>()?.text ?? "";

        [UnityTest]
        public IEnumerator TheShelfShowsAMerchantsPanels_MarksItsFakes_AndLeavesBackToTheEvent()
        {
            FixtureEvents.Append(Fixture());
            RunManager.StartRun(21UL);
            Assert.IsTrue(RunOrchestrator.OpenEventForDebug(Caravan));
            var picked = EventPicks.OnCurrentPage(0);
            Assert.AreEqual(EventChoiceOutcome.Ok, picked.Outcome, $"{picked.Reason}");
            Assert.IsTrue(RunOrchestrator.EventShelfPending);

            // A Map start with the event open reopens its panel, which finds
            // the shelf in front and hands over to it.
            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var shop = UnityEngine.Object.FindAnyObjectByType<ShopController>(FindObjectsInactive.Include);
            var eventPanel = UnityEngine.Object.FindAnyObjectByType<EventController>(FindObjectsInactive.Include);
            Assert.IsNotNull(shop);
            Assert.IsNotNull(eventPanel);
            Assert.IsTrue(shop.gameObject.activeSelf, "the event did not hand over to the shelf");
            Assert.IsFalse(eventPanel.gameObject.activeSelf, "the event panel stayed up over the shelf");

            Assert.IsFalse(Named("ShopRelicPanel").activeSelf, "a merchant shelf showed the relic panel");
            Assert.IsFalse(Named("ShopGearReroll").activeSelf, "a merchant shelf offered a reroll");
            Assert.IsFalse(Named("ShopBookReroll").activeSelf, "a merchant shelf offered a reroll");
            Assert.IsFalse(Named("ShopPackButton").activeSelf, "a merchant shelf offered to buy the bag");
            Assert.AreEqual("CONSUMABLES", Text("ShopBookPanelHeader"));
            Assert.AreEqual("BUYING FOR", Text("ShopKeeperPanelHeader"),
                "a merchant shelf still captioned the buyer's column SHOPKEEPER beside the keeper's own panel");
            Assert.IsTrue(Named("ShopBookCard0").activeSelf);
            Assert.IsTrue(Named("ShopBookCard1").activeSelf);
            Assert.IsFalse(Named("ShopBookCard2").activeSelf, "a card the shelf does not stock is shown");

            // Browse with Odette: every fake card's meta line wears the mark,
            // and no genuine one does. The mark alone is in the loss red
            // (ItemStatLines.LossHex); the rest of the line is untouched.
            const string mark = "<color=#E05A5A>FAKE</color> · ";
            foreach (var entry in RunOrchestrator.CurrentShopStock)
            {
                string card = entry.section == ShopStock.GearSection ? $"ShopGearCard{entry.index}" : $"ShopBookCard{entry.index}";
                string meta = Text(card + "Meta");
                bool marked = meta.StartsWith(mark, StringComparison.Ordinal);
                Assert.AreEqual(entry.fake, marked, $"{card} ({entry.contentId}, fake={entry.fake}): '{meta}'");
                if (marked) StringAssert.DoesNotContain("<color", meta.Substring(mark.Length), "only the mark is coloured");
                Assert.IsFalse(!entry.fake && meta.Contains("FAKE"), $"{card} is genuine but reads '{meta}'");
            }

            // LEAVE arms, then leaves -- back to the event, room uncleared.
            int node = RunManager.Run.currentNodeId;
            Click("ShopLeaveButton");
            Click("ShopLeaveButton");
            yield return null;

            Assert.IsFalse(RunOrchestrator.EventShelfPending, "LEAVE did not leave the shelf");
            Assert.IsFalse(shop.gameObject.activeSelf);
            Assert.IsTrue(eventPanel.gameObject.activeSelf, "leaving the shelf did not return to the event");
            Assert.AreEqual("after_browse", RunManager.Run.eventPageId);
            CollectionAssert.DoesNotContain(RunManager.Run.clearedNodeIds, node);
        }
    }
}
