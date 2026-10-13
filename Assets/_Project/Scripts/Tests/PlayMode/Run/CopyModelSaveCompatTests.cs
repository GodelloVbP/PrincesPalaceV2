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
    // It holds before and after content collapses to bases: while "_pN"
    // assets exist the ids survive and read their own tier through
    // ContentDatabase.Effective; once they are gone, Reconcile prunes them.
    // Either way nothing throws and nothing dangling survives.
    public class CopyModelSaveCompatTests
    {
        private const string OldBootsId = "brigandine_boots_p3";
        private const string OldSwordId = "sword_p5";

        private static string StripToOldShape(string json) =>
            Regex.Replace(json.Replace(",\"tier\":0", ""), "\"slotHoning\":\\[\\],?", "");

        [Test]
        public void APreCopyModelSave_Loads_AndKeepsOnlyCopiesThatResolve()
        {
            var save = SaveData.CreateNew();
            var character = save.roster.FirstOrDefault();
            Assert.IsNotNull(character, "fixture: CreateNew should seed at least one roster member");

            save.Gold = 555;
            save.activeRun.hasRun = true;
            save.activeRun.gold = 41;
            InventoryOps.Add(save.activeRun.inventory, OldBootsId, 1, plus: 2, riftTier: 1);
            character.equipment.Set(EquipmentSlot.Weapon1, OldSwordId, plus: 3);
            save.activeRun.shopStock.Add(new ShopStockEntry
            {
                kind = ShopEntryKind.Gear, contentId = "sword_p7", plus = 1, riftTier = 1, price = 60,
            });

            string oldJson = StripToOldShape(JsonUtility.ToJson(save));
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

            foreach (var entry in loaded.activeRun.inventory)
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

            // Per-tier assets still exist, so the old id survives and reads
            // its own tier. When content collapses to bases this flips: the
            // id no longer resolves and Weapon1 is empty
            // (docs/handoffs/item_redesign/I2A_REMAINING.md).
            var sword = ContentDatabase.Effective(wearer.equipment.GetInstance(EquipmentSlot.Weapon1)).Value;
            Assert.AreEqual(5, sword.Tier);
        }
    }
}
