using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // Validates and fills in items.json, same collected-not-first-only error
    // reporting as EnemyEntryResolver. The interesting rules here are the
    // cross-field ones: which fields are meaningful depends on the item's
    // kind, so a Weapon carrying a heal amount (or a Consumable carrying an
    // attack bonus) is an authoring mistake worth naming rather than
    // silently dropping.
    //
    // Weapon is Equipment that happens to be locked to the two hand slots.
    // It stays its own kind rather than collapsing into Equipment because
    // attackBonus drives the loot-tiering in FightController.RollWeaponDrop
    // — "which items can drop as weapons, ranked by power" is a real
    // distinction the drop table needs, not just a slot difference.
    public static class ItemEntryResolver
    {
        private const int DefaultConsumableAmount = 20;
        private const int DefaultConsumableCost = 15;
        private const int DefaultWeaponCost = 0;
        private const int DefaultEquipmentCost = 0;

        public static bool TryResolveAll(IReadOnlyList<RawItemEntry> entries, out List<ResolvedItem> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedItem>();
            errors = new List<string>();

            for (int i = 0; i < entries.Count; i++)
            {
                if (TryResolveOne(entries[i], i, resolved.Count, out var single, out string error))
                {
                    resolved.Add(single);
                }
                else
                {
                    errors.Add(error);
                }
            }

            foreach (string duplicateId in resolved.GroupBy(i => i.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add($"Duplicate item id '{duplicateId}' — every id must be unique.");
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveOne(RawItemEntry raw, int index, int sortOrder, out ResolvedItem resolvedItem, out string error)
        {
            resolvedItem = default;
            string label = string.IsNullOrEmpty(raw.id) ? $"items.json entry #{index + 1}" : $"item '{raw.id}'";

            if (string.IsNullOrWhiteSpace(raw.id))
            {
                error = $"{label}: id is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(raw.displayName))
            {
                error = $"{label}: displayName is required.";
                return false;
            }

            var kind = ResolvedItemKind.Consumable;
            if (!string.IsNullOrWhiteSpace(raw.kind) && !Enum.TryParse(raw.kind, ignoreCase: true, out kind))
            {
                error = $"{label}: kind '{raw.kind}' isn't valid. Valid options: {string.Join(", ", Enum.GetNames(typeof(ResolvedItemKind)))}.";
                return false;
            }

            var effect = ResolvedItemEffect.Heal;
            if (!string.IsNullOrWhiteSpace(raw.effect) && !Enum.TryParse(raw.effect, ignoreCase: true, out effect))
            {
                error = $"{label}: effect '{raw.effect}' isn't valid. Valid options: {string.Join(", ", Enum.GetNames(typeof(ResolvedItemEffect)))}.";
                return false;
            }

            bool wearable = kind == ResolvedItemKind.Weapon || kind == ResolvedItemKind.Equipment;
            bool grantsStats = !raw.statBonus.Equals(StatBlock.Zero) || !raw.abilityScoreBonus.Equals(AbilityScoreBlock.Zero);

            // Slot: required for Equipment, optional-and-hand-only for a
            // Weapon, meaningless for a Consumable.
            var slot = EquipmentSlot.Weapon1;
            bool slotAuthored = !string.IsNullOrWhiteSpace(raw.slot);
            if (slotAuthored && !EquipmentSlots.TryParse(raw.slot, out slot))
            {
                error = $"{label}: slot '{raw.slot}' isn't valid. Valid options: {EquipmentSlots.AuthorableNames()}.";
                return false;
            }

            if (!wearable)
            {
                if (slotAuthored)
                {
                    error = $"{label}: slot is an Equipment/Weapon field and has no effect on a Consumable — remove it, or change kind to Equipment.";
                    return false;
                }

                if (grantsStats)
                {
                    error = $"{label}: statBonus/abilityScoreBonus are worn-while-equipped fields and have no effect on a Consumable — remove them, or change kind to Equipment.";
                    return false;
                }
            }

            int amount;
            int attackBonus;
            int cost;

            if (kind == ResolvedItemKind.Weapon)
            {
                if (raw.attackBonus <= 0)
                {
                    error = $"{label}: a Weapon needs a positive attackBonus (got {raw.attackBonus}). " +
                            "A weapon that grants nothing can be equipped but does nothing, which is never intended.";
                    return false;
                }

                if (raw.amount >= 0)
                {
                    error = $"{label}: amount is a Consumable field and has no effect on a Weapon — remove it, or change kind to Consumable.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(raw.effect))
                {
                    error = $"{label}: effect is a Consumable field and has no effect on a Weapon — remove it, or change kind to Consumable.";
                    return false;
                }

                // A weapon in the Head slot would be worn but never
                // wielded — the paperdoll would accept it and combat would
                // never see it, which is exactly the kind of silent
                // half-working state worth failing the build over.
                if (slotAuthored && !EquipmentSlots.IsWeaponSlot(slot))
                {
                    error = $"{label}: a Weapon can only go in a hand slot (Weapon1 or Weapon2), not '{raw.slot}'. " +
                            "Change kind to Equipment if it's meant to be worn there.";
                    return false;
                }

                amount = 0;
                attackBonus = raw.attackBonus;
                cost = raw.cost >= 0 ? raw.cost : DefaultWeaponCost;
            }
            else if (kind == ResolvedItemKind.Equipment)
            {
                if (!slotAuthored)
                {
                    error = $"{label}: Equipment needs a slot. Valid options: {EquipmentSlots.AuthorableNames()}.";
                    return false;
                }

                if (!grantsStats)
                {
                    error = $"{label}: Equipment needs a statBonus or an abilityScoreBonus. " +
                            "A piece that grants nothing can be worn but does nothing, which is never intended.";
                    return false;
                }

                // attackBonus is the Weapon field specifically; Equipment
                // raises Attack through statBonus.attack instead. Two ways
                // to spell the same bonus is how the two paths drift apart.
                if (raw.attackBonus >= 0)
                {
                    error = $"{label}: attackBonus is a Weapon field — Equipment raises Attack via statBonus.attack instead.";
                    return false;
                }

                if (raw.amount >= 0)
                {
                    error = $"{label}: amount is a Consumable field and has no effect on Equipment — remove it, or change kind to Consumable.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(raw.effect))
                {
                    error = $"{label}: effect is a Consumable field and has no effect on Equipment — remove it, or change kind to Consumable.";
                    return false;
                }

                amount = 0;
                attackBonus = 0;
                cost = raw.cost >= 0 ? raw.cost : DefaultEquipmentCost;
            }
            else
            {
                if (raw.attackBonus >= 0)
                {
                    error = $"{label}: attackBonus is a Weapon field and has no effect on a Consumable — remove it, or change kind to Weapon.";
                    return false;
                }

                amount = raw.amount >= 0 ? raw.amount : DefaultConsumableAmount;
                if (amount == 0)
                {
                    error = $"{label}: a Consumable with amount 0 restores nothing when used.";
                    return false;
                }

                attackBonus = 0;
                cost = raw.cost >= 0 ? raw.cost : DefaultConsumableCost;
            }

            var requirementErrors = new List<string>();
            if (!AbilityScoreLineParser.TryParse(raw.requires, label, out var requirements, requirementErrors))
            {
                error = string.Join(" ", requirementErrors);
                return false;
            }

            resolvedItem = new ResolvedItem(raw.id, raw.displayName, raw.description ?? "", kind, effect,
                amount, attackBonus, slot, raw.statBonus, raw.abilityScoreBonus, raw.startingStock,
                cost, raw.iconPath ?? "", sortOrder, requirements);
            error = null;
            return true;
        }
    }
}
