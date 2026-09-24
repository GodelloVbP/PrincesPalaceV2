using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // Expands weapons.json into finished weapons across ONE axis: tier.
    //
    // The armour-set resolver next door generates eleven items from one
    // authored piece; this generates eleven per family the identical way —
    // item-modifier plan Phase B collapsed the second (modifier) axis this
    // file used to iterate (sturdy/nimble/hallowed/arcane/verdant/heavy/
    // quick/cunning, all eight gone). What used to make a Sturdy sword a
    // different pick from a Nimble one is now a ROLLED RIFT MODIFIER on the
    // item instance, drawn from modifiers.json, not a second baked family
    // variant here. A base weapon still carries a single default scaling
    // identity (Sword->STR, Staff->INT, Dagger->DEX, per the plan's Q2) so
    // it is a coherent pick even with zero modifiers rolled.
    //
    // GRADE IS INTERPOLATED, not tabulated. A family says what its scaling
    // reaches at tier 0 and at max tier and every level between falls out,
    // the same both-ends anchoring ItemSetEntryResolver uses for stats and it
    // buys the same three things: retuning is two words, raising maxTier
    // stretches the curve instead of running out of ladder, and a designer
    // never hand-writes eleven letters that later drift out of order.
    //
    // Grade belongs to TIER and only to tier. The instance-level PLUS scales
    // a weapon's flat Attack and nothing else — letting it move the grade too
    // would put two axes in charge of the same number.
    public static class WeaponEntryResolver
    {
        private const int DefaultMaxTier = 10;
        private const int DefaultCost = 0;
        private const int DefaultCostPerTier = 0;
        private const int DefaultAttackAtZero = 3;
        private const int DefaultAttackAtMax = 18;

        // Used when a family authors no adjectives at all, so a missing list
        // is a plain-looking weapon rather than a crash or a blank in a name.
        public const string FallbackAdjective = "Plain";

        public static bool TryResolveAll(IReadOnlyList<RawWeaponEntry> entries, out List<ResolvedWeapon> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedWeapon>();
            errors = new List<string>();

            if (entries == null)
            {
                return true;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ResolveFamily(entries[i], i, resolved, errors);
            }

            foreach (string duplicate in resolved.GroupBy(w => w.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add($"Two generated weapons share the id '{duplicate}'. Family ids must be unique, since the item id is built from it.");
            }

            return errors.Count == 0;
        }

        private static void ResolveFamily(RawWeaponEntry raw, int index, List<ResolvedWeapon> resolved, List<string> errors)
        {
            string label = string.IsNullOrWhiteSpace(raw?.id) ? $"weapon family #{index}" : $"weapon family '{raw.id}'";

            if (raw == null || string.IsNullOrWhiteSpace(raw.id))
            {
                errors.Add($"{label}: id is required — it is written into every item id this family generates, and item ids are written into save files.");
                return;
            }

            if (string.IsNullOrWhiteSpace(raw.displayName))
            {
                errors.Add($"{label}: displayName is required — it is the noun in every generated name.");
                return;
            }

            int maxTier = raw.maxTier >= 0 ? raw.maxTier : DefaultMaxTier;
            if (maxTier > ItemSetEntryResolver.HighestSupportedTier)
            {
                errors.Add($"{label}: maxTier is {maxTier}, above the supported ceiling of {ItemSetEntryResolver.HighestSupportedTier}. Raising the ceiling is a one-line change, but this is usually a typo.");
                return;
            }

            if (!TryParseSlot(raw.slot, out var slot, out string slotError))
            {
                errors.Add($"{label}: {slotError}");
                return;
            }

            // Both are Assets-relative, and BOTH are checked rather than only
            // whichever IconFor happens to pick: iconSheet wins when it is set,
            // so a wrongly-written iconPath sitting behind a working sheet is
            // invisible until the sheet is removed.
            if (!ArtPathConvention.Check(label, "iconSheet", raw.iconSheet, out string sheetError))
            {
                errors.Add(sheetError);
                return;
            }

            if (!ArtPathConvention.Check(label, "iconPath", raw.iconPath, out string iconError))
            {
                errors.Add(iconError);
                return;
            }

            if (slot != EquipmentSlot.Weapon1 && slot != EquipmentSlot.Weapon2)
            {
                errors.Add($"{label}: slot '{raw.slot}' is not a hand. A weapon has to be holdable.");
                return;
            }

            int attackAtZero = raw.attackAtZero >= 0 ? raw.attackAtZero : DefaultAttackAtZero;
            int attackAtMax = raw.attackAtMax >= 0 ? raw.attackAtMax : DefaultAttackAtMax;
            if (attackAtZero <= 0)
            {
                errors.Add($"{label}: attackAtZero must be positive — a weapon that grants no Attack at all is not worth equipping, and EveryWeapon_HasAPositiveAttackBonus asserts as much.");
                return;
            }

            int cost = raw.cost >= 0 ? raw.cost : DefaultCost;
            int costPerTier = raw.costPerTier >= 0 ? raw.costPerTier : DefaultCostPerTier;
            int familyOrder = raw.sortOrder >= 0 ? raw.sortOrder : index;
            int iconLevels = raw.iconLevels > 0 ? raw.iconLevels : ItemSetEntryResolver.DefaultIconLevels;

            if (!TryResolveAxis(raw.primary, raw.primaryAtZero, raw.primaryAtMax,
                    label, "primary", required: true, out var primary, errors))
            {
                return;
            }

            if (!TryResolveAxis(raw.secondary, raw.secondaryAtZero, raw.secondaryAtMax,
                    label, "secondary", required: false, out var secondary, errors))
            {
                return;
            }

            var extraErrors = new List<string>();
            if (!ScalingLineParser.TryParse(raw.alsoScalesWith, label, out var extra, extraErrors))
            {
                errors.AddRange(extraErrors);
                return;
            }

            var spellErrors = new List<string>();
            if (!ScalingLineParser.TryParse(raw.spellScalesWith, label, out var spellScaling, spellErrors))
            {
                errors.AddRange(spellErrors);
                return;
            }

            var requirementErrors = new List<string>();
            if (!AbilityScoreLineParser.TryParse(raw.requiresAtZero, label, out var reqAtZero, requirementErrors)
                | !AbilityScoreLineParser.TryParse(raw.requiresAtMax, label, out var reqAtMax, requirementErrors))
            {
                errors.AddRange(requirementErrors);
                return;
            }

            // Same guard as ItemSetEntryResolver's: only one of
            // requiresAtZero/requiresAtMax authored parses the empty side to
            // AbilityScoreBlock.Zero, which InterpolateScores below reads as
            // a real "requires nothing at that end" -- the maxTier
            // requirement would silently interpolate down to 0.
            if (raw.requiresAtZero.Length == 0 ^ raw.requiresAtMax.Length == 0)
            {
                errors.Add($"{label}: authors only one of requiresAtZero/requiresAtMax -- " +
                           $"both must be given together (or both left empty), " +
                           $"otherwise the missing end silently interpolates to no requirement.");
                return;
            }

            if (secondary.HasValue && primary.Value.Score == secondary.Value.Score)
            {
                errors.Add($"{label}: primary and secondary are both {AbilityScores.ShortName(primary.Value.Score)}, so the secondary curve would silently overwrite the primary one.");
                return;
            }

            for (int tier = 0; tier <= maxTier; tier++)
            {
                resolved.Add(BuildWeapon(raw, slot, tier, maxTier, primary.Value, secondary, extra,
                    // WEAPON GROWTH, not armour's -- balance redesign
                    // Phase 3 (D3). This is the ONLY interpolation in
                    // this file that moves off GearScaling.TierGrowth;
                    // GradeAt below (the scaling letters) deliberately
                    // stays on the default so a weapon's grade ladder is
                    // untouched by how steeply its flat Attack climbs.
                    ItemSetEntryResolver.ValueAt(attackAtZero, attackAtMax, tier, maxTier, GearScaling.WeaponTierGrowth),
                    cost + costPerTier * tier,
                    familyOrder * 10000 + tier,
                    iconLevels, spellScaling,
                    ItemSetEntryResolver.InterpolateScores(reqAtZero, reqAtMax, tier, maxTier)));
            }
        }

        // One authored primary/secondary curve: a stat, and the grade it holds
        // at each end.
        private static bool TryResolveAxis(string rawScore, string rawAtZero, string rawAtMax,
            string label, string which, bool required,
            out (AbilityScore Score, ScalingGrade AtZero, ScalingGrade AtMax)? axis, List<string> errors)
        {
            axis = null;

            if (string.IsNullOrWhiteSpace(rawScore))
            {
                if (required)
                {
                    errors.Add($"{label}: {which} is required — a weapon family that favours no stat is not a way of being balanced.");
                    return false;
                }

                return true;
            }

            if (!AbilityScores.TryParse(rawScore, out var score))
            {
                errors.Add($"{label}: {which} names '{rawScore}', which is not an ability score.");
                return false;
            }

            if (!ScalingGrades.TryParse(rawAtZero, out var atZero) || !ScalingGrades.TryParse(rawAtMax, out var atMax))
            {
                errors.Add($"{label}: {which} grades must be S, A, B, C, D, E or none (got '{rawAtZero}' and '{rawAtMax}').");
                return false;
            }

            if ((int)atMax < (int)atZero)
            {
                errors.Add($"{label}: {which} goes from {ScalingGrades.Letter(atZero)} down to {ScalingGrades.Letter(atMax)} as the weapon improves. Upgrading a weapon should not make it scale worse.");
                return false;
            }

            axis = (score, atZero, atMax);
            return true;
        }

        // The grade a curve sits at for one tier.
        //
        // Straight-line between the two authored ends over the enum's own
        // integer ladder, which is why ScalingGrade's values are consecutive.
        // Floored the same way stats are, via the shared ValueAt, so a grade
        // and a stat cannot round in opposite directions at the same tier.
        private static ScalingGrade GradeAt(ScalingGrade atZero, ScalingGrade atMax, int tier, int maxTier)
        {
            return ScalingGrades.FromIndex(ItemSetEntryResolver.ValueAt((int)atZero, (int)atMax, tier, maxTier));
        }

        // "Keen Sword". The tier adjective leads the family noun; no modifier
        // segment any more (item-modifier plan Phase B) — a rolled Rift
        // modifier is what tells one drop apart from another now, and its
        // name is composed at display time, not baked here.
        //
        // The plus is NOT part of this either. It belongs to the instance,
        // and the UI appends it via ItemNaming.WithPlus at display time.
        private static string NameFor(RawWeaponEntry family, int tier)
        {
            return ItemNaming.Compose(AdjectiveFor(family, tier), family.displayName);
        }

        // The last adjective covers every tier past the end of a short list,
        // rather than the list having to be exactly maxTier + 1 long. A
        // family that authors none at all gets a neutral one, so a missing
        // list reads as unremarkable rather than as a hole in the name.
        private static string AdjectiveFor(RawWeaponEntry family, int tier)
        {
            return ItemNaming.AdjectiveAt(family.tierAdjectives, tier, FallbackAdjective);
        }

        private static ResolvedWeapon BuildWeapon(RawWeaponEntry family, EquipmentSlot slot,
            int tier, int maxTier,
            (AbilityScore Score, ScalingGrade AtZero, ScalingGrade AtMax) primary,
            (AbilityScore Score, ScalingGrade AtZero, ScalingGrade AtMax)? secondary,
            ScalingProfile extra,
            int attackBonus, int cost, int sortOrder, int iconLevels, ScalingProfile spellScaling = default,
            AbilityScoreBlock requirements = default)
        {
            var scaling = extra.With(primary.Score, GradeAt(primary.AtZero, primary.AtMax, tier, maxTier));
            if (secondary.HasValue)
            {
                var s = secondary.Value;
                scaling = scaling.With(s.Score, GradeAt(s.AtZero, s.AtMax, tier, maxTier));
            }

            string familyId = family.id.Trim();

            return new ResolvedWeapon(
                $"{familyId}_p{tier}",
                NameFor(family, tier),
                family.description ?? "",
                slot,
                familyId,
                tier,
                attackBonus,
                scaling,
                cost,
                sortOrder,
                IconFor(family, tier, maxTier, iconLevels),
                spellScaling,
                requirements);
        }

        // Per-level art if the family has a sheet, one flat image if it only
        // has the one, and nothing at all otherwise — which is a supported
        // state rather than a hole, the same as it is for armour.
        private static string IconFor(RawWeaponEntry family, int tier, int maxTier, int iconLevels)
        {
            string fromSheet = ItemSetEntryResolver.IconPathFor(family.iconSheet, tier, maxTier, iconLevels);
            if (!string.IsNullOrEmpty(fromSheet))
            {
                return fromSheet;
            }

            return string.IsNullOrWhiteSpace(family.iconPath) ? "" : family.iconPath.Trim();
        }

        private static bool TryParseSlot(string raw, out EquipmentSlot slot, out string error)
        {
            slot = EquipmentSlot.Weapon1;

            if (string.IsNullOrWhiteSpace(raw))
            {
                error = null;
                return true;
            }

            string trimmed = raw.Trim();
            if (string.Equals(trimmed, "Weapon", StringComparison.OrdinalIgnoreCase))
            {
                error = null;
                return true;
            }

            if (!Enum.TryParse(trimmed, ignoreCase: true, out slot))
            {
                error = $"slot '{raw}' is not an equipment slot.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
