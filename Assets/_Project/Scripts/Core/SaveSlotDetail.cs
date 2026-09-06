using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace
{
    // The facts a save-slot card shows, resolved from disk in ONE place.
    //
    // Supersedes SaveSlotLabel, which built a single sentence ("Slot 1: 320
    // gold") for both the Play screen and Manage Saves. The card now shows a
    // number badge, two lines of detail and (Play only) a gold figure, and
    // the two screens say different things about an EMPTY slot -- Choose
    // invites ("New Descent"), Manage Saves states it ("Empty") -- so this
    // hands back the facts and leaves the wording to whichever controller is
    // asking. SaveSlotLabel could only ever serve one sentence to both.
    public readonly struct SaveSlotFacts
    {
        public readonly bool Filled;

        // The lead roster member's display name, or "" when there is no save
        // to name one from. CONTENT, not authored copy -- resolved through
        // ContentDatabase the same way every other character name on screen
        // is, and carried through UiString.FromContent at the call site.
        public readonly string CharacterName;

        public readonly int Floor;
        public readonly float PlaySeconds;
        public readonly int Gold;

        public SaveSlotFacts(bool filled, string characterName, int floor, float playSeconds, int gold)
        {
            Filled = filled;
            CharacterName = characterName;
            Floor = floor;
            PlaySeconds = playSeconds;
            Gold = gold;
        }

        public static readonly SaveSlotFacts Empty = new SaveSlotFacts(false, "", 0, 0f, 0);
    }

    public static class SaveSlotDetail
    {
        public static SaveSlotFacts For(int slotIndex)
        {
            return SaveSystem.SlotExists(slotIndex) ? For(SaveSystem.Load(slotIndex)) : SaveSlotFacts.Empty;
        }

        public static SaveSlotFacts For(SaveData save)
        {
            if (save == null) return SaveSlotFacts.Empty;

            // FIRST OF THE ACTIVE SQUAD, not the whole roster -- the same
            // "who is this save about" a Continue button or a hub greeting
            // would use. A save always resolves at least one member (see
            // ActiveSquad's own fallback), so this is empty only when the
            // roster itself is empty, which CreateNew never leaves it.
            var lead = save.ActiveSquad().FirstOrDefault();
            string name = lead == null ? "" : ContentDatabase.GetCharacter(lead.definitionId)?.Data.DisplayName ?? "";

            int floor = RunDepth.FloorFor(save.lifetimeDeepestStep);

            return new SaveSlotFacts(true, name, floor, save.totalPlaySeconds, save.Gold);
        }
    }
}
