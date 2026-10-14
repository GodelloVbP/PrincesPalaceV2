using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace
{
    // HONING'S TWO SERVICES (docs/PLAN_ITEMS.md 1.5): the shop sells +1s on
    // any slot, the Rest room gives one for free in place of the heal. Both
    // write RunSnapshot.slotHoning through RewardPages.Hone, the path "hone
    // instead" already takes, so the three sources share one writer and its
    // carried-health rescale.
    //
    // Honing is slot-agnostic: the price reads the slot's current plus and
    // the step, never what the slot holds.
    public static partial class RunOrchestrator
    {
        // ---- the shop's service ----------------------------------------------------

        // Step-0 gold is HoneAuthored; what is charged is scaled to the step.
        public static int HonePriceFor(string characterId, EquipmentSlot slot)
        {
            var run = RunManager.Run;
            if (run == null) return ShopPricing.HonePrice(0, 0);

            return ShopPricing.HonePrice(SlotHoning.PlusOf(run.slotHoning, characterId, slot), run.step);
        }

        // One +1 on `slot` of `characterId`, paid from the purse. Room shop
        // only: a merchant shelf sells no honing.
        public static ShopResult BuyHone(string characterId, EquipmentSlot slot)
        {
            // 1. VALIDATE.
            var run = RunManager.Run;
            var save = SaveSlotManager.CurrentSave;
            if (!ShopIsOpen(run) || save == null) return ShopResult.Refused(ShopRefusal.NoShop);
            if (!InSquad(save, characterId)) return ShopResult.Refused(ShopRefusal.BadIndex);
            if (!SlotHoning.CanHone(run.slotHoning, characterId, slot)) return ShopResult.Refused(ShopRefusal.FullyHoned);

            int price = HonePriceFor(characterId, slot);
            if (run.gold < price) return ShopResult.Refused(ShopRefusal.NotEnoughGold);
            if (Refused()) return ShopResult.Refused(ShopRefusal.Injected);

            // 2. APPLY.
            run.gold -= price;
            RewardPages.Hone(save, characterId, slot);

            // 3. PERSIST.
            return Persisted(-price);
        }

        // ---- the Rest room ---------------------------------------------------------

        // The party stands in a Rest room whose choice is still to make.
        public static bool RestIsOpen => RestIsOpenOn(RunManager.Run);

        private static bool RestIsOpenOn(RunSnapshot run)
        {
            var node = RunManager.CurrentNode;
            return run != null && run.hasRun && run.restOpen
                && node != null && node.Type == RoomType.Rest
                && !run.clearedNodeIds.Contains(node.Id);
        }

        private static void OpenRest()
        {
            var run = RunManager.Run;
            if (run == null) return;

            run.restOpen = true;
            SaveSlotManager.SaveCurrent();
        }

        // The heal: what a Rest room has always done, and what the bot picks.
        public static bool RestHeal()
        {
            var run = RunManager.Run;
            if (!RestIsOpenOn(run)) return false;

            run.restOpen = false;
            RoomResolver.Resolve(run, RoomType.Rest);
            RunManager.ClearCurrentRoom();
            return true;
        }

        // The free +1, instead of the heal.
        public static bool RestHone(string characterId, EquipmentSlot slot)
        {
            var run = RunManager.Run;
            var save = SaveSlotManager.CurrentSave;
            if (!RestIsOpenOn(run) || save == null || !InSquad(save, characterId)) return false;
            if (!SlotHoning.CanHone(run.slotHoning, characterId, slot)) return false;

            run.restOpen = false;
            RewardPages.Hone(save, characterId, slot);
            RunManager.ClearCurrentRoom();
            return true;
        }

        private static bool InSquad(SaveData save, string characterId) =>
            !string.IsNullOrEmpty(characterId)
            && save.ActiveSquad().Any(c => c != null && c.definitionId == characterId);
    }
}
