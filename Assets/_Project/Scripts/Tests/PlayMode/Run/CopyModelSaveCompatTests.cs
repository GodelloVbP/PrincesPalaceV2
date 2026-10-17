using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;
using UnityEngine;

namespace PrincesPalace.PlayModeTests
{
    // The copy model adds no save version on purpose (docs/PLAN_ITEMS.md
    // 2.5): a run found on disk is settled, never resumed, unknown ids are
    // pruned, and removed fields are ignored. This loads a save shaped the
    // way every save before the copy model is -- per-tier "_pN" ids, `plus` and
    // `riftTier` on bag, slot and shop entries, no `tier`, no `slotHoning` --
    // through the real Migrate/Reconcile path.
    //
    // Content holds one asset per base, so the old "_pN" ids no longer
    // resolve and Reconcile prunes them. Nothing throws and nothing dangling
    // survives.
    public class CopyModelSaveCompatTests
    {
        private const string OldBootsId = "brigandine_boots_p3";
        private const string OldSwordId = "sword_p5";

        private static string StripToOldShape(string json) =>
            Regex.Replace(json.Replace(",\"tier\":0", ""), "\"slotHoning\":\\[\\],?", "");

        // Saves from before the cap sit at tier 11-12 in the bag, on the body
        // and on the shelf. Loading brings every one down to the art ladder's
        // top, merging into the tier-10 stack if there is one.
        // The Extra Recruit Slot upgrade was removed (AUDIT #277). A save that
        // bought it keeps loading: Reconcile prunes the purchase like any id
        // that no longer resolves, and the squad ceiling is unchanged.
        [Test]
        public void ASaveThatBoughtTheRemovedRecruitSlot_LoadsWithoutIt()
        {
            var save = SaveData.CreateNew();
            int before = save.EffectiveMaxSquadSize();
            save.purchasedUpgradeIds.Add("extra_recruit_slot");
            save.purchasedUpgradeIds.Add("starting_gold_boost");

            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            Assert.DoesNotThrow(() => loaded.Reconcile());

            CollectionAssert.AreEqual(new[] { "starting_gold_boost" }, loaded.purchasedUpgradeIds);
            Assert.AreEqual(before, loaded.EffectiveMaxSquadSize());
            Assert.IsNull(ContentDatabase.GetUpgrade("extra_recruit_slot"));
        }

        [Test]
        public void ASaveHoldingTierElevenAndTwelveCopies_LoadsAtTheCap()
        {
            var save = SaveData.CreateNew();
            var wearer = save.roster.First();
            save.stockpiledItems.Clear();
            save.stockpiledItems.Add(new InventoryEntry("brigandine_boots", 1, null, 11));
            save.stockpiledItems.Add(new InventoryEntry("brigandine_boots", 2, null, 12));
            save.stockpiledItems.Add(new InventoryEntry("brigandine_boots", 1, null, 10));
            wearer.equipment.Clear();
            wearer.equipment.slots.Add(new EquipmentSlotEntry(EquipmentSlot.Weapon1, "sword", null, 12));
            save.activeRun.currentNodeId = 3;
            save.activeRun.shopNodeId = 3;
            save.activeRun.shopStock.Add(new ShopStockEntry
            {
                kind = ShopEntryKind.Gear, contentId = "sword", tier = 12, price = 60,
            });

            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            loaded.Reconcile();

            var bag = loaded.stockpiledItems.Where(e => e.itemId == "brigandine_boots").ToList();
            Assert.AreEqual(1, bag.Count, "all three stacks are one tier-10 stack");
            Assert.AreEqual(10, bag[0].tier);
            Assert.AreEqual(4, bag[0].count);
            var worn = loaded.roster.First(c => c.definitionId == wearer.definitionId)
                .equipment.EquippedEntries().Single(e => e.slot == EquipmentSlot.Weapon1);
            Assert.AreEqual(10, worn.tier);
            Assert.AreEqual(10, loaded.activeRun.shopStock.Single().tier);
        }

        [Test]
        public void APreCopyModelSave_Loads_AndKeepsOnlyCopiesThatResolve()
        {
            var save = SaveData.CreateNew();
            var character = save.roster.FirstOrDefault();
            Assert.IsNotNull(character, "fixture: CreateNew should seed at least one roster member");

            save.Gold = 555;
            save.activeRun.hasRun = true;
            save.activeRun.gold = 41;
            InventoryOps.Add(save.stockpiledItems, OldBootsId, 1);
            character.equipment.Set(EquipmentSlot.Weapon1, OldSwordId);
            save.activeRun.shopStock.Add(new ShopStockEntry
            {
                kind = ShopEntryKind.Gear, contentId = "sword_p7", price = 60,
            });

            // The removed fields no longer exist on the types, so the old keys
            // are written into the JSON by hand next to each id.
            string oldJson = StripToOldShape(JsonUtility.ToJson(save))
                .Replace("\"itemId\":\"" + OldBootsId + "\"", "\"itemId\":\"" + OldBootsId + "\",\"plus\":2,\"riftTier\":1")
                .Replace("\"itemId\":\"" + OldSwordId + "\"", "\"itemId\":\"" + OldSwordId + "\",\"plus\":3")
                .Replace("\"contentId\":\"sword_p7\"", "\"contentId\":\"sword_p7\",\"plus\":1,\"riftTier\":1");
            StringAssert.DoesNotContain("\"tier\"", oldJson, "fixture: the JSON must be the pre-copy-model shape");
            StringAssert.DoesNotContain("slotHoning", oldJson, "fixture: the JSON must be the pre-copy-model shape");
            StringAssert.Contains("\"riftTier\":1", oldJson, "fixture: the removed fields must be present to be ignored");

            SaveData loaded = null;
            Assert.DoesNotThrow(() => loaded = JsonUtility.FromJson<SaveData>(oldJson));
            Assert.DoesNotThrow(() => loaded.Migrate());
            Assert.DoesNotThrow(() => loaded.Reconcile());

            Assert.AreEqual(555, loaded.Gold, "an unrelated field survives");
            Assert.AreEqual(41, loaded.activeRun.gold, "an unrelated run field survives");
            Assert.IsNotNull(loaded.activeRun.slotHoning, "a missing list loads as a real empty one");
            CollectionAssert.IsEmpty(loaded.activeRun.slotHoning);

            foreach (var entry in loaded.stockpiledItems)
            {
                Assert.IsNotNull(ContentDatabase.GetItem(entry.itemId), $"bag kept '{entry.itemId}', which does not resolve");
                Assert.AreEqual(0, entry.tier, "a missing tier loads as 0");
            }

            var wearer = loaded.roster.First(c => c.definitionId == character.definitionId);
            foreach (var slot in wearer.equipment.EquippedEntries())
            {
                Assert.IsNotNull(ContentDatabase.GetItem(slot.itemId), $"slot kept '{slot.itemId}', which does not resolve");
                Assert.IsTrue(ContentDatabase.Effective(slot.Instance).HasValue, slot.itemId);
            }

            // The fixture guard: the old ids really are gone from content.
            Assert.IsNull(ContentDatabase.GetItem("brigandine_boots_p3"));
            Assert.IsNull(ContentDatabase.GetItem(OldSwordId));

            // The pruned sword leaves Weapon1 empty.
            Assert.IsTrue(wearer.equipment.IsEmpty(EquipmentSlot.Weapon1));
            Assert.IsEmpty(loaded.stockpiledItems);
        }

        // totalPlaySeconds was a float; a slot keeps counting past the 145 h
        // where a float swallows a 16 ms frame.
        [Test]
        public void PlaySecondsKeepClimbingPastOneHundredFortyFiveHours()
        {
            var save = new SaveData { totalPlaySeconds = 600000 };

            save.totalPlaySeconds += 0.016f;

            Assert.Greater(save.totalPlaySeconds, 600000.0,
                "a frame delta was rounded away: the play counter is frozen at this size");
        }

        // A save written when the field was a float and run.inventory existed.
        [Test]
        public void ASaveWithAFloatPlayTimeAndTheDeadRunInventoryKey_StillLoads()
        {
            const string oldJson =
                "{\"version\":9,\"totalPlaySeconds\":3725.5,\"activeRun\":{\"hasRun\":false," +
                "\"inventory\":[{\"itemId\":\"health_potion\",\"count\":2}]}}";

            SaveData loaded = null;
            Assert.DoesNotThrow(() => loaded = JsonUtility.FromJson<SaveData>(oldJson));
            Assert.DoesNotThrow(() => loaded.Migrate());
            Assert.DoesNotThrow(() => loaded.Reconcile());

            Assert.AreEqual(3725.5, loaded.totalPlaySeconds, 0.0, "the float literal reads into the double exactly");
            Assert.IsFalse(loaded.activeRun.hasRun);
        }
    }
}
