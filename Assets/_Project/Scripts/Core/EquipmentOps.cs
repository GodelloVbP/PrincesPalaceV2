using System.Collections.Generic;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace
{
    // PUTTING GEAR ON, WITH THE SAVE-SIDE HALF INCLUDED.
    //
    // EquipMove (Domain) already owns every RULE about what can be worn: the
    // move is atomic, the displaced piece travels back to the bag with its own
    // plus and affixes, a slot preference is refused when it does not accept
    // the item. What it cannot own is the one thing that happens on either
    // side of that move and touches the SAVE -- carried health is a fraction
    // of a max that gear moves, so the max has to be read before the swap and
    // rescaled after it.
    //
    // That pair had three copies: CharacterDossierController.EquipFromPack,
    // CharacterDossierController.UnequipSlot, and
    // RunOrchestrator.AutoEquipIntoAnEmptySlot -- each with the same
    // "MEASURED BEFORE THE SWAP" comment, which is what three copies of a rule
    // look like just before one of them is edited alone. The bot is the fourth
    // caller and the reason it is one place now, exactly the bargain
    // RunOrchestrator's own header describes for the run's rules: one
    // rulebook, several callers, and a headless player that measures the game
    // rather than a copy of it.
    //
    // DELIBERATELY DOES NOT PERSIST. The three callers differ on when they
    // write, and honestly so: the sheet saves immediately because gear lost to
    // a crash between an equip and a save is unforgivable, TakeOffer saves
    // once after the whole take, and the bot's equip pass saves once per pass.
    // Folding SaveCurrent in here would make two of them write more often than
    // they mean to -- and a save write was 91% of a bot batch's wall clock
    // before BotRunDriver.InMemorySaves existed.
    public static class EquipmentOps
    {
        // Wears this copy off `save.stockpiledItems`. False when EquipMove
        // refused it, having changed nothing.
        //
        // The whole ItemInstance names WHICH COPY -- the bag keys stacks on
        // ItemInstance.SameStack (see InventoryOps' header), so a caller that
        // hands over only an id looks for the plain, unrolled stack and
        // silently fails to find a rolled one or a caravan lot.
        public static bool Equip(
            SaveData save,
            Character character,
            ItemInstance instance,
            EquipmentSlot itemSlot,
            bool isEquippable,
            EquipmentSlot? preferredSlot = null)
        {
            if (save == null || character?.equipment == null) return false;

            // MEASURED BEFORE THE SWAP, because the swap is what moves it.
            // Gear that carries max health changes what the run's carried
            // current health is a fraction OF -- see ScaleCarriedHealth.
            int maxBefore = ContentDatabase.EffectiveStats(character).maxHealth;

            if (!EquipMove.TryEquip(character.equipment, save.stockpiledItems, instance, itemSlot,
                                    isEquippable, preferredSlot))
            {
                return false;
            }

            RunEncounter.ScaleCarriedHealth(character, maxBefore);
            return true;
        }

        // The positional form, for an ordinary copy.
        public static bool Equip(
            SaveData save,
            Character character,
            string itemId,
            EquipmentSlot itemSlot,
            bool isEquippable,
            int plus = 0,
            List<string> modifierIds = null,
            int riftTier = 0,
            EquipmentSlot? preferredSlot = null)
        {
            return Equip(save, character, new ItemInstance(itemId, plus, modifierIds, riftTier),
                itemSlot, isEquippable, preferredSlot);
        }

        // Takes a slot off and puts what was in it back in the bag. False when
        // the slot was already empty.
        //
        // Scales carried health BOTH WAYS, and that symmetry is the point:
        // scaling only on the way in would make an equip/unequip cycle a
        // healing exploit.
        public static bool Unequip(SaveData save, Character character, EquipmentSlot slot)
        {
            if (save == null || character?.equipment == null) return false;

            int maxBefore = ContentDatabase.EffectiveStats(character).maxHealth;

            if (!EquipMove.TryUnequip(character.equipment, save.stockpiledItems, slot))
            {
                return false;
            }

            RunEncounter.ScaleCarriedHealth(character, maxBefore);
            return true;
        }
    }
}
