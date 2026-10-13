using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Equipment
{
    // How honed one member's slot is (docs/PLAN_ITEMS.md 1.5). Honing belongs
    // to the slot, not to what the slot holds: a sword moved from Weapon1 to
    // Weapon2 reads Weapon2's honing, an emptied slot keeps its entry, and a
    // benched member keeps theirs. It lives on RunSnapshot.slotHoning and so
    // ends with the run.
    [Serializable]
    public class SlotHoningEntry
    {
        public string characterId = "";
        public EquipmentSlot slot;
        public int plus;

        public SlotHoningEntry()
        {
        }

        public SlotHoningEntry(string characterId, EquipmentSlot slot, int plus)
        {
            this.characterId = characterId ?? "";
            this.slot = slot;
            this.plus = plus;
        }
    }

    // The one reader and writer of a slotHoning list. At most one entry per
    // (character, slot); absent reads as +0.
    public static class SlotHoning
    {
        public static int PlusOf(IReadOnlyList<SlotHoningEntry> honing, string characterId, EquipmentSlot slot)
        {
            var entry = Find(honing, characterId, slot);
            return entry == null ? 0 : Clamp(entry.plus);
        }

        public static bool CanHone(IReadOnlyList<SlotHoningEntry> honing, string characterId, EquipmentSlot slot) =>
            PlusOf(honing, characterId, slot) < ItemUpgrade.MaxPlus;

        // One +1, capped at ItemUpgrade.MaxPlus. Returns the slot's plus
        // afterwards; at the cap nothing changes.
        public static int Hone(List<SlotHoningEntry> honing, string characterId, EquipmentSlot slot)
        {
            if (honing == null) throw new ArgumentNullException(nameof(honing));
            if (string.IsNullOrEmpty(characterId)) throw new ArgumentException("a slot belongs to a character", nameof(characterId));

            var entry = Find(honing, characterId, slot);
            if (entry == null)
            {
                entry = new SlotHoningEntry(characterId, slot, 0);
                honing.Add(entry);
            }

            entry.plus = Clamp(entry.plus + 1);
            return entry.plus;
        }

        private static SlotHoningEntry Find(IReadOnlyList<SlotHoningEntry> honing, string characterId, EquipmentSlot slot)
        {
            if (honing == null || string.IsNullOrEmpty(characterId)) return null;

            for (int i = 0; i < honing.Count; i++)
            {
                var entry = honing[i];
                if (entry != null && entry.slot == slot && entry.characterId == characterId)
                {
                    return entry;
                }
            }

            return null;
        }

        private static int Clamp(int plus) => plus < 0 ? 0 : plus > ItemUpgrade.MaxPlus ? ItemUpgrade.MaxPlus : plus;
    }
}
