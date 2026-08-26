using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;
using UnityEngine;

namespace PrincesPalace.Content
{
    // One item modifier ("Rift affix"): a rule an item can roll and grant
    // while worn. Authored in Assets/_Project/ContentData/modifiers.json and
    // generated into an asset by ContentBuilder — never hand-edited under
    // Resources/Content, which ContentBuilder deletes wholesale on every
    // build.
    //
    // MIRRORS TalentDefinition at a much smaller scale, per the plan: no
    // grid position, no prerequisites, no character ownership — a modifier
    // is a flat pool entry any item can roll (see the plan's "one modifier
    // pool shared across all item types"). What survives the mirror is the
    // one thing both types actually need: `effects`, converted to the
    // Domain-layer struct list at the same single boundary
    // TalentDefinition.ResolvedEffects() already establishes.
    public class ModifierDefinition : ScriptableObject, IOrderedContent
    {
        [Tooltip("Stable identifier written into save files (EquipmentSlotEntry/InventoryEntry.modifierIds). Never rename this after a save exists.")]
        public string id;

        public string displayName;

        [TextArea]
        public string description;

        [Tooltip("Rules this modifier grants while its item is worn. Empty is rejected by ModifierEntryResolver at build time -- a modifier granting nothing is always a content mistake.")]
        public ModifierEffectEntry[] effects = new ModifierEffectEntry[0];

        // Resources.LoadAll returns assets in filename (alphabetical) order,
        // not authoring order -- the same trap every other content type here
        // guards against with an explicit sortOrder (see IOrderedContent's
        // own header).
        public int sortOrder;

        // A parallel [Serializable] carrier rather than storing
        // Domain.Combat.ModifierEffect directly -- Unity will not serialise a
        // readonly struct's fields onto a ScriptableObject, the identical
        // reason TalentDefinition.TalentEffectEntry exists rather than an
        // array of TalentEffect. The conversion is one Select, right here, at
        // the only boundary that needs it.
        [System.Serializable]
        public class ModifierEffectEntry
        {
            public ModifierEffectType type;
            public int magnitude;
            public int threshold;

            // TypedResistanceFlat's and ElementalDamageOnHitPercent's target
            // only -- two fields rather than a
            // nullable DamageType because Unity's serializer does not round-
            // trip System.Nullable and Physical is DamageType's own zero
            // value, so a dropped nullable would silently read back as
            // "resists Physical" instead of "resists nothing". Mirrors
            // RelicModifierEntry's identical against/hasAgainst pair
            // (RelicDefinition.cs) rather than reinventing the fix.
            public DamageType against;
            public bool hasAgainst;
            public bool againstMagical;
        }

        public IEnumerable<ModifierEffect> ResolvedEffects()
        {
            if (effects == null)
            {
                yield break;
            }

            foreach (var entry in effects)
            {
                if (entry == null || entry.type == ModifierEffectType.None)
                {
                    continue;
                }

                DamageType? against = entry.hasAgainst ? entry.against : (DamageType?)null;
                yield return new ModifierEffect(entry.type, entry.magnitude, entry.threshold, against, entry.againstMagical);
            }
        }

        // Listed by the authored order ContentBuilder stamped on it.
        public int SortOrder => sortOrder;
    }
}
