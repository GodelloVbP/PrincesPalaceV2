using System;

namespace PrincesPalace.Domain.Equipment
{
    // Everything the rest of the game needs to know ABOUT slots, as opposed
    // to what is currently in them (EquipmentLoadout). Kept separate so the
    // "a weapon fits either hand" rule and the display names each exist
    // exactly once — the UI, the JSON resolver and the equip service all
    // call in here rather than re-deciding.
    public static class EquipmentSlots
    {
        // Declaration order of the enum, which is also paperdoll order.
        // Rebuilt from Enum.GetValues rather than hand-listed so adding a
        // slot cannot silently miss the UI.
        public static readonly EquipmentSlot[] All = (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot));

        public static string DisplayName(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return "Head";
                case EquipmentSlot.Necklace: return "Necklace";
                case EquipmentSlot.Torso: return "Torso";
                case EquipmentSlot.Legs: return "Legs";
                case EquipmentSlot.Shoes: return "Shoes";
                case EquipmentSlot.Gloves: return "Gloves";
                case EquipmentSlot.Weapon1: return "Weapon 1";
                case EquipmentSlot.Weapon2: return "Weapon 2";
                default:
                    throw new ArgumentOutOfRangeException(nameof(slot), slot, "EquipmentSlots has no display name wired up for this slot.");
            }
        }

        // True when an item that belongs in `itemSlot` may be worn in
        // `characterSlot`. The only asymmetry in the whole system: an item
        // authored as Weapon1 also fits Weapon2, so dual-wielding needs no
        // second item field. Nothing else is interchangeable — a helm does
        // not go on your feet.
        public static bool Accepts(EquipmentSlot characterSlot, EquipmentSlot itemSlot)
        {
            if (characterSlot == itemSlot)
            {
                return true;
            }

            return itemSlot == EquipmentSlot.Weapon1 && characterSlot == EquipmentSlot.Weapon2;
        }

        // True for the two hand slots. Used by the content resolver, which
        // rejects a weapon authored into a non-hand slot rather than
        // silently putting a sword on someone's head.
        public static bool IsWeaponSlot(EquipmentSlot slot)
        {
            return slot == EquipmentSlot.Weapon1 || slot == EquipmentSlot.Weapon2;
        }

        // Parses an authored slot name out of items.json. Accepts the enum
        // names case-insensitively, plus the bare alias "Weapon" for
        // Weapon1 — an author writing "Weapon" means "a weapon", and making
        // them remember it is specifically slot ONE (when it fits either
        // hand anyway) would be a trap, not a rule.
        public static bool TryParse(string text, out EquipmentSlot slot)
        {
            slot = EquipmentSlot.Head;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            if (string.Equals(trimmed, "Weapon", StringComparison.OrdinalIgnoreCase))
            {
                slot = EquipmentSlot.Weapon1;
                return true;
            }

            return Enum.TryParse(trimmed, ignoreCase: true, out slot) && Array.IndexOf(All, slot) >= 0;
        }

        // The authorable slot names, for error messages. "Weapon" is listed
        // alongside the real values because TryParse accepts it.
        public static string AuthorableNames()
        {
            return string.Join(", ", Enum.GetNames(typeof(EquipmentSlot))) + ", Weapon";
        }
    }
}
