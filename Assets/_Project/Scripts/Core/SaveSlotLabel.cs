using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // How a save slot is described, in ONE place.
    //
    // This existed three times over in v1 -- SaveSlotController's filled and
    // empty cases, and ResetProgressController's own pair -- with different
    // wording and different fields, so the Play screen and the Options screen
    // described the same slot differently. One of them also printed `exp`, which
    // nothing has ever written, so every label permanently read ", 0 exp".
    //
    // The wording itself now lives in UiStrings rather than in interpolated
    // literals here, which is the same fix the wallet line got: authored copy
    // belongs in the manifest, so it cannot drift from whatever else renders it.
    public static class SaveSlotLabel
    {
        // "Slot 1: 320 gold, 4 relics" or "Slot 1: Empty".
        public static string For(int slotIndex)
        {
            return SaveSystem.SlotExists(slotIndex)
                ? For(slotIndex, SaveSystem.Load(slotIndex))
                : Empty(slotIndex);
        }

        public static string For(int slotIndex, SaveData data)
        {
            return data == null
                ? Empty(slotIndex)
                : UiStrings.SlotFilled.Format(slotIndex + 1, data.Gold);
        }

        public static string Empty(int slotIndex)
        {
            return UiStrings.SlotEmpty.Format(slotIndex + 1);
        }
    }
}
