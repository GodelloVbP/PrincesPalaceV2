using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Content
{
    // A SCOPED "WHAT IF THIS SLOT WERE HONED +N MORE" (docs/PLAN_REWARD_SCREENS.md
    // C13). ContentDatabase.HoningOf adds the live previews to the run's
    // slotHoning, so every reader on the stats path (ActiveLoadout,
    // EffectiveStats, weapon power, ItemDescription.SimulateEquip) agrees
    // without a parameter. It never writes slotHoning.
    //
    //   using (HoningPreview.Add("shawn", EquipmentSlot.Weapon1, +1)) { ...Compare... }
    //
    // Previews stack: nested ones for the same slot add. Main thread only.
    public sealed class HoningPreview : IDisposable
    {
        private static readonly List<HoningPreview> Live = new List<HoningPreview>();

        private readonly string _characterId;
        private readonly EquipmentSlot _slot;
        private readonly int _delta;

        private HoningPreview(string characterId, EquipmentSlot slot, int delta)
        {
            _characterId = characterId;
            _slot = slot;
            _delta = delta;
        }

        public static HoningPreview Add(string characterId, EquipmentSlot slot, int delta)
        {
            var preview = new HoningPreview(characterId, slot, delta);
            Live.Add(preview);
            return preview;
        }

        // Disposing twice, or out of order, removes only this preview.
        public void Dispose() => Live.Remove(this);

        // The sum of the live previews for one slot; 0 when there are none.
        internal static int DeltaOf(string characterId, EquipmentSlot slot)
        {
            int total = 0;
            for (int i = 0; i < Live.Count; i++)
            {
                var preview = Live[i];
                if (preview._slot == slot && preview._characterId == characterId) total += preview._delta;
            }

            return total;
        }
    }
}
