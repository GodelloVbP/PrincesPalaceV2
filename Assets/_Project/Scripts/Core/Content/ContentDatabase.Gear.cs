using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Content
{
    // THE STAT ACCESSOR SEAM of the copy model (docs/PLAN_ITEMS.md 2.3). A
    // copy is a base id plus a tier; what it grants comes from here, never
    // from fields on the definition it names.
    public static partial class ContentDatabase
    {
        // The definition a copy names. Null for an id content does not know.
        public static ItemDefinition DefinitionOf(ItemInstance copy) =>
            copy == null ? null : GetItem(copy.ItemId);

        // What `copy` grants while worn in a slot honed to `honing`. Null for
        // an id content does not know, and for anything that is not gear.
        //
        public static GearStats? Effective(ItemInstance copy, int honing = 0)
        {
            var definition = DefinitionOf(copy);
            if (definition == null || !definition.IsEquippable) return null;

            return definition.curve.At(copy.Tier).WithHoning(honing);
        }

        // How honed `slot` is for `character`: the run's slotHoning, which ends
        // with the run. Honing belongs to the slot, not to what it holds.
        // Outside a run (a profile screen, a character built by a test) there
        // is none, and every slot reads +0.
        public static int HoningOf(Character character, EquipmentSlot slot) =>
            character == null ? 0 : SlotHoning.PlusOf(RunManager.Run?.slotHoning, character.definitionId, slot);

        // The honing a candidate would wear at: its target slot's, since
        // honing belongs to the slot and not to what it holds.
        public static int HoningFor(Character character, ItemInstance candidate)
        {
            var definition = DefinitionOf(candidate);
            if (character?.equipment == null || definition == null) return 0;

            return HoningOf(character, character.equipment.ResolveTargetSlot(definition.equipSlot));
        }

        // What a copy is called: the tier adjective and base name for gear,
        // the authored name for anything else, the id for content that is
        // gone. No honing suffix (ItemNaming.WithPlus adds that).
        public static string NameOf(ItemInstance copy)
        {
            if (copy == null) return "";

            var gear = Effective(copy);
            if (gear.HasValue && !string.IsNullOrEmpty(gear.Value.DisplayName)) return gear.Value.DisplayName;

            var definition = DefinitionOf(copy);
            return definition == null || string.IsNullOrEmpty(definition.displayName) ? copy.ItemId : definition.displayName;
        }

        // Every icon key one item can show. A consumable has its one path;
        // gear has one per tier on its curve (an art frame, repeated past the
        // art ladder), so a screen's icon table holds all of them and a copy
        // is looked up by ItemIcons.KeyOf.
        public static IEnumerable<string> IconPathsOf(ItemDefinition definition)
        {
            if (definition == null) yield break;

            if (!definition.IsEquippable)
            {
                if (!string.IsNullOrEmpty(definition.iconPath)) yield return definition.iconPath;
                yield break;
            }

            var seen = new HashSet<string>();
            for (int tier = 0; tier <= ItemInstance.TierCap; tier++)
            {
                string path = definition.curve.At(tier).IconPath;
                if (!string.IsNullOrEmpty(path) && seen.Add(path)) yield return path;
            }
        }

        // How good `copy` is: derived, never stored (plan 1.4): its affixes and
        // its trait, told apart by modifiers.json `trait`.
        public static Quality QualityOf(ItemInstance copy)
        {
            if (copy == null) return Quality.Common;

            int traits = TraitRowsOf(copy).Count;
            return ItemQuality.Of(copy.ModifierIds.Count - traits, traits);
        }

        // The step-0 gold `copy` costs to buy: tier, affixes, and a trait
        // priced as ShopPricing.TraitAsAffixes affixes. Sale prices derive
        // from it.
        public static int AuthoredPriceOf(ItemInstance copy)
        {
            if (copy == null) return 0;

            int traits = TraitRowsOf(copy).Count;
            return PrincesPalace.Domain.Rewards.ShopPricing.GearAuthored(copy.Tier, copy.ModifierIds.Count - traits, traits);
        }

        // The copy's trait rows (a Legendary has one), in rolled order.
        public static List<ModifierDefinition> TraitRowsOf(ItemInstance copy)
        {
            var traits = new List<ModifierDefinition>();
            if (copy == null) return traits;

            foreach (string id in copy.ModifierIds)
            {
                var modifier = GetModifier(id);
                if (modifier != null && modifier.Data.Trait) traits.Add(modifier);
            }

            return traits;
        }

        // Worn slots whose trait does nothing because the wearer already gets
        // the mechanic from a relic in `relicIds` or from an earlier slot
        // (TraitStacking). Only live pieces count: an inert piece grants
        // nothing, so it cannot shadow a trait either.
        public static HashSet<EquipmentSlot> InactiveTraitSlots(Character character, IReadOnlyList<string> relicIds)
        {
            var inactive = new HashSet<EquipmentSlot>();
            if (character == null) return inactive;

            var slots = new List<EquipmentSlot>();
            var kinds = new List<PrincesPalace.Domain.Combat.EffectKind>();
            foreach (var (entry, _) in ActiveLoadout(character).LiveEntries.OrderBy(e => System.Array.IndexOf(EquipmentSlots.All, e.Entry.slot)))
            {
                foreach (var trait in TraitRowsOf(entry.Instance))
                {
                    slots.Add(entry.slot);
                    kinds.Add(trait.Data.Effects.Length == 0 ? PrincesPalace.Domain.Combat.EffectKind.None : trait.Data.Effects[0].Kind);
                }
            }

            var relicKinds = new List<PrincesPalace.Domain.Combat.EffectKind>();
            foreach (string id in relicIds ?? new List<string>())
            {
                var relic = GetRelic(id)?.Data;
                if (relic != null && relic.ReachesCharacter(character.definitionId)) relicKinds.Add(relic.Mechanic);
            }

            var flags = TraitStacking.Inactive(kinds, relicKinds);
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i]) inactive.Add(slots[i]);
            }

            return inactive;
        }
    }
}
