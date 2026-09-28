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
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // The shop's comparison panel compares.
    //
    // A capture showing only the offered item with no "VS. EQUIPPED"
    // section cannot tell whether the panel never compares or whether the
    // picked character simply wore nothing in that slot: SaveData.CreateNew
    // builds bare characters, so a fresh run's leader has an empty slot,
    // and against an empty WEAPON slot the delta is zero (gear grants no
    // flat Attack) and DMG has no equipped number to point at.
    // This pins the first half of the answer -- with something worn in the
    // card's slot, the panel's text changes and carries the comparison --
    // so the next capture that shows no comparison can be read at a glance.
    public class ShopComparisonPanelTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-shop-compare-" + System.Guid.NewGuid().ToString("N"));
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

        private static ulong SeedWithAShopInTheFirstColumn()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = DescentMapGenerator.GenerateLegFor(seed, 0);
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
            Named("ShopDetailPanelBody")?.GetComponent<TMP_Text>()?.text ?? "";

        [UnityTest]
        public IEnumerator AGearCard_ComparesAgainstWhatThePickedCharacterWearsInThatSlot()
        {
            RunManager.StartRun(SeedWithAShopInTheFirstColumn());

            var node = RunManager.Map.AtDepth(1).First(n => n.Type == RoomType.Shop);
            RunOrchestrator.ArriveAt(node);
            RunManager.Run.gold = 2000;

            var card = RunOrchestrator.CurrentShopStock
                .FirstOrDefault(e => e != null && e.section == ShopStock.GearSection && !e.noOffer && !e.sold);
            Assert.IsNotNull(card, "the shop rolled no gear card");
            var offered = ContentDatabase.GetItem(card.contentId);
            Assert.IsNotNull(offered, $"gear card '{card.contentId}' resolves to no item");

            // Something ELSE for the same slot, preferring one with no
            // requirements so it is live on a fresh character: an inert piece
            // contributes nothing, and "nothing" is the empty-slot case again.
            var sameSlot = ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable && i.equipSlot == offered.equipSlot && i.id != offered.id)
                .ToList();
            var worn = sameSlot.FirstOrDefault(i => default(AbilityScoreBlock).Meets(i.requirements))
                       ?? sameSlot.FirstOrDefault();
            Assert.IsNotNull(worn, $"no second item for slot {offered.equipSlot} to wear against '{offered.id}'");

            SaveSlotManager.SaveCurrent();

            yield return SceneManager.LoadSceneAsync("Map", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var map = Object.FindAnyObjectByType<MapController>(FindObjectsInactive.Include);
            Assert.IsNotNull(map, "the Map scene has no MapController");
            map.OpenShop();
            yield return null;

            Click($"ShopGearCard{card.index}");
            yield return null;
            // Against an empty slot. For armour this already reads VS.
            // EQUIPPED (the delta from nothing is the item's own stats); for
            // a weapon it does not, because gear grants no flat Attack
            // (ContentDatabase.EffectiveStats) and DMG has nothing to point
            // an arrow at -- which is the Bound Staff shot QA flagged.
            string againstNothing = DetailText();

            // Put the second item on the leader -- the character the picker
            // starts on -- and look again. Open() resets the selection, so
            // the card is pressed afresh, the same as a player would.
            var leader = SaveSlotManager.CurrentSave.ActiveSquad().First(c => c != null);
            leader.equipment.Set(leader.equipment.ResolveTargetSlot(worn.equipSlot), worn.id);

            map.OpenShop();
            yield return null;
            Click($"ShopGearCard{card.index}");
            yield return null;
            string againstWorn = DetailText();

            Assert.AreNotEqual(againstNothing, againstWorn,
                $"wearing '{worn.id}' changed nothing in the panel for '{offered.id}' -- it is not comparing");
            Assert.IsTrue(againstWorn.Contains("VS. EQUIPPED") || againstWorn.Contains(" -> "),
                $"the panel for '{offered.id}' against a worn '{worn.id}' shows no delta:\n{againstWorn}");
        }
    }
}
