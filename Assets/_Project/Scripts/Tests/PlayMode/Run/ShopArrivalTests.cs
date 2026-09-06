using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // WALKING INTO A SHOP, AND THE SEAM THE SHELF IS ROLLED THROUGH.
    //
    // Two subjects, one fixture, because both need a real map and real
    // content: what ArriveAt answers for a Shop node, and that the shop's
    // gear roll is the SAME three-axis roll the reward path uses.
    public class ShopArrivalTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-shop-arrival-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        [TearDown]
        public void RestoreSaveRoot()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // A seed whose first column offers a shop, found by generating maps
        // directly rather than by starting thousands of runs -- the generator
        // is pure and reads no content, so this costs nothing and needs no
        // save writes.
        //
        // Shop is 3 of 269 in the middle-room table, so a column of up to
        // three nodes offers one about 3% of the time; a few hundred seeds is
        // ample and the search is bounded so a table rebalance that removed
        // Shop entirely would fail here loudly rather than hang.
        private static ulong SeedWithAShopInTheFirstColumn()
        {
            for (ulong seed = 1; seed < 4000UL; seed++)
            {
                var map = DescentMapGenerator.GenerateLeg(new SeededRandom(seed), 0);
                if (map.AtDepth(1).Any(n => n.Type == RoomType.Shop)) return seed;
            }

            throw new AssertionException("No seed under 4000 generated a shop in the first column.");
        }

        [Test]
        public void ArrivingAtAShopRollsTheShelfAndDoesNotClearTheRoom()
        {
            ulong seed = SeedWithAShopInTheFirstColumn();
            RunManager.StartRun(seed);

            var shop = RunManager.Map.AtDepth(1).First(n => n.Type == RoomType.Shop);
            var arrival = RunOrchestrator.ArriveAt(shop);

            Assert.AreEqual(RunOrchestrator.Arrival.Shop, arrival);

            // A shop is the second room that leads to a screen, so it must
            // NOT clear itself the way every other non-fight room does.
            CollectionAssert.DoesNotContain(RunManager.Run.clearedNodeIds, shop.Id);

            Assert.AreEqual(shop.Id, RunManager.Run.shopNodeId);
            Assert.IsNotEmpty(RunManager.Run.shopStock);
            Assert.AreEqual(ShopStock.GearCount + ShopStock.BookCount + ShopStock.RelicCount,
                RunManager.Run.shopStock.Count);
        }

        // The shelf is on disk before the player can see it, which is what
        // makes quitting inside a shop unable to reroll it. Read back off the
        // file rather than off the cached SaveData, since the cache is the
        // thing that would hide a missing write.
        [Test]
        public void TheShelfIsPersistedOnArrival()
        {
            ulong seed = SeedWithAShopInTheFirstColumn();
            RunManager.StartRun(seed);
            var shop = RunManager.Map.AtDepth(1).First(n => n.Type == RoomType.Shop);
            RunOrchestrator.ArriveAt(shop);

            var onDisk = SaveSystem.Load(SaveSlotManager.CurrentSlot);

            Assert.AreEqual(shop.Id, onDisk.activeRun.shopNodeId);
            CollectionAssert.AreEqual(
                RunManager.Run.shopStock.Select(e => e.contentId + ":" + e.price).ToList(),
                onDisk.activeRun.shopStock.Select(e => e.contentId + ":" + e.price).ToList());
        }

        [Test]
        public void LeavingAShopClearsTheRoomTheWayEveryOtherRoomDoes()
        {
            ulong seed = SeedWithAShopInTheFirstColumn();
            RunManager.StartRun(seed);
            var shop = RunManager.Map.AtDepth(1).First(n => n.Type == RoomType.Shop);
            RunOrchestrator.ArriveAt(shop);

            RunOrchestrator.LeaveShop();

            CollectionAssert.Contains(RunManager.Run.clearedNodeIds, shop.Id);
            Assert.IsEmpty(RunManager.Run.shopStock);
        }

        // ---- the extracted roll -------------------------------------------------

        // THE REWARD PATH ROLLS BYTE-IDENTICALLY BEFORE AND AFTER RollOne WAS
        // EXTRACTED OUT OF IT.
        //
        // Rebuilt here from the public pieces rather than compared against
        // values copied out of a run: RarityTable.RollTier, ItemOfferTable.
        // Choose, RarityTable.RollPlus, ModifierTable.RollRiftTier and
        // ModifierTable.PickModifiers, drawn in that ORDER off a stream
        // opened at the same seed. The order is the whole content of the
        // extraction -- every draw shifts every draw after it, so a reordered
        // or duplicated call inside RollOne shows up as different items, not
        // just different affixes.
        //
        // The two modifier pools are rebuilt the same way ItemOfferRoll's own
        // private ModifierPool does, from ContentDatabase.Modifiers and
        // ModifierTable.IsOffensiveModifier -- both public, so this is a
        // reimplementation of the rule rather than a call into it.
        [Test]
        public void TheRewardRollIsExactlyChooseThenRollOnePerOffer()
        {
            const ulong Seed = 987654321UL;
            const int Depth = 24;
            const int Favor = 0;
            const int Count = 3;

            var rolled = ItemOfferRoll.Roll(EncounterClass.Normal, Depth, Favor, Draw(Seed), Count);

            var next = Draw(Seed);
            var candidates = ItemOfferRoll.Candidates();
            int maxTier = ItemOfferRoll.MaxTier;
            int target = RarityTable.RollTier(EncounterClass.Normal, Depth, maxTier, Favor, next);

            var weaponPool = ModifierPool(weapons: true);
            var armorPool = ModifierPool(weapons: false);
            var kindById = ContentDatabase.Items
                .Where(i => i != null && !string.IsNullOrEmpty(i.id))
                .ToDictionary(i => i.id, i => i.kind);

            var expected = new List<string>();
            foreach (var offer in ItemOfferTable.Choose(candidates, target, maxTier, next, Count))
            {
                int plus = RarityTable.RollPlus(EncounterClass.Normal, Favor, next);
                var rift = ModifierTable.RollRiftTier(EncounterClass.Normal, Favor, next);
                bool isWeapon = kindById.TryGetValue(offer.ItemId, out var kind) && kind == ItemKind.Weapon;
                var modifiers = ModifierTable.PickModifiers(isWeapon ? weaponPool : armorPool, (int)rift, next);

                expected.Add(Describe(offer.ItemId, plus, (int)rift, modifiers));
            }

            CollectionAssert.AreEqual(expected,
                rolled.Select(o => Describe(o.ItemId, o.Plus, (int)o.RiftTier, o.Modifiers)).ToList(),
                "The reward roll no longer draws in the order it drew before RollOne was extracted, " +
                "which renumbers every reward every existing seed has ever produced.");
        }

        // The shop's gear shelf goes through the same seam, so a card and a
        // reward are the same object rolled the same way.
        [Test]
        public void TheShopGearShelfUsesTheSameRollAsTheRewardPath()
        {
            const ulong Seed = 13579UL;
            const int Depth = 24;

            var next = Draw(Seed);
            var context = ItemOfferRoll.BuildRollContext();
            var shelf = ShopStock.RollGear(ItemOfferRoll.Candidates(), Depth, ItemOfferRoll.MaxTier,
                offer => ItemOfferRoll.RollOne(offer, context, EncounterClass.Normal, 0, next), next);

            var again = Draw(Seed);
            var context2 = ItemOfferRoll.BuildRollContext();
            var repeat = ShopStock.RollGear(ItemOfferRoll.Candidates(), Depth, ItemOfferRoll.MaxTier,
                offer => ItemOfferRoll.RollOne(offer, context2, EncounterClass.Normal, 0, again), again);

            CollectionAssert.AreEqual(
                shelf.Select(e => e.contentId + ":" + e.price + ":" + string.Join("|", e.modifiers)).ToList(),
                repeat.Select(e => e.contentId + ":" + e.price + ":" + string.Join("|", e.modifiers)).ToList());

            Assert.IsTrue(shelf.All(e => e.noOffer || e.price > 0));
        }

        private static Func<int, int> Draw(ulong seed)
        {
            var rng = new SeededRandom(seed);
            return bound => rng.NextInt(0, bound);
        }

        private static string Describe(string itemId, int plus, int rift, IReadOnlyList<string> modifiers) =>
            $"{itemId}+{plus}r{rift}[{string.Join("|", modifiers ?? new List<string>())}]";

        private static List<string> ModifierPool(bool weapons)
        {
            return ContentDatabase.Modifiers
                .Where(m => m != null && !string.IsNullOrEmpty(m.id))
                .Where(m =>
                {
                    var effects = m.Data.Effects;
                    var type = effects != null && effects.Length > 0 ? effects[0].Type : ModifierEffectType.None;
                    return ModifierTable.IsOffensiveModifier(type) == weapons;
                })
                .Select(m => m.id)
                .ToList();
        }
    }
}
