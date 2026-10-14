using System;
using System.Collections.Generic;
using System.Reflection;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Content
{
    // Validates rewards.json. Every row error is collected; any error refuses
    // the whole file (.claude/rules/content.md).
    public static class RewardSourceEntryResolver
    {
        public static bool TryResolveAll(IReadOnlyList<RawRewardSourceEntry> entries,
            out List<ResolvedRewardSource> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedRewardSource>();
            errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var rows = entries ?? new List<RawRewardSourceEntry>();

            if (Enum.GetValues(typeof(RelicRarity)).Length != ResolvedRewardSource.RelicRarityCount)
            {
                errors.Add("RelicRarity changed size; ResolvedRewardSource.RelicRarityCount must follow it.");
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var raw = rows[i];
                string label = $"rewards.json entry #{i + 1}" + (raw != null && !string.IsNullOrEmpty(raw.id) ? $" ('{raw.id}')" : "");
                if (raw == null)
                {
                    errors.Add($"{label}: the row is missing.");
                    continue;
                }

                int before = errors.Count;
                if (string.IsNullOrWhiteSpace(raw.id)) errors.Add($"{label}: id is required.");
                else if (!seen.Add(raw.id)) errors.Add($"{label}: id '{raw.id}' is used twice.");

                var kind = Parse<RewardPageKind>(raw.kind, "kind", label, errors);
                var qualityFloor = Parse<Quality>(raw.qualityFloor, "qualityFloor", label, errors);
                var weaponFloor = Parse<Quality>(raw.weaponFloor, "weaponFloor", label, errors);
                var rarityMin = Parse<RelicRarity>(raw.relicRarityMin, "relicRarityMin", label, errors);
                var unused = Parse<UnusedChoicePays>(raw.unusedPays, "unusedPays", label, errors);

                if (raw.extraCards < 0 || raw.extraCards > 6)
                    errors.Add($"{label}: extraCards {raw.extraCards} is outside 0-6.");

                if (!TryStream(raw.stream, out uint stream))
                    errors.Add($"{label}: stream '{raw.stream}' is not an RngStreams constant.");

                if (kind == RewardPageKind.Gear)
                {
                    if (raw.tierStep <= 0f || raw.tierStep >= 1f)
                        errors.Add($"{label}: tierStep {raw.tierStep} must be inside (0, 1).");
                    if (raw.ladderStep <= 0f || raw.ladderStep >= 1f)
                        errors.Add($"{label}: ladderStep {raw.ladderStep} must be inside (0, 1).");
                    if (raw.tierOffset < 0)
                        errors.Add($"{label}: tierOffset {raw.tierOffset} must not be negative.");
                    if (qualityFloor == Quality.Legendary)
                        errors.Add($"{label}: qualityFloor Legendary leaves the climb nothing to roll.");
                    if (unused != UnusedChoicePays.Hone)
                        errors.Add($"{label}: a gear page's unused choices hone (unusedPays Hone).");
                }
                else
                {
                    var weights = raw.relicWeights ?? Array.Empty<int>();
                    if (weights.Length != ResolvedRewardSource.RelicRarityCount)
                        errors.Add($"{label}: relicWeights needs {ResolvedRewardSource.RelicRarityCount} ints, one per RelicRarity.");
                    else if (Array.Exists(weights, w => w < 0))
                        errors.Add($"{label}: relicWeights may not be negative.");
                    if (unused != UnusedChoicePays.CheapestSale)
                        errors.Add($"{label}: a relic page's unused choices pay gold (unusedPays CheapestSale).");
                }

                if (errors.Count > before) continue;

                var row = new ResolvedRewardSource
                {
                    Id = raw.id,
                    Kind = kind,
                    ExtraCards = raw.extraCards,
                    TierOffset = raw.tierOffset,
                    TierStep = raw.tierStep,
                    LadderStep = raw.ladderStep,
                    QualityFloor = qualityFloor,
                    WeaponGuarantee = raw.weaponGuarantee,
                    WeaponFloor = weaponFloor,
                    RelicRarityMin = rarityMin,
                    UnusedPays = unused,
                    Stream = stream,
                    SortOrder = raw.sortOrder,
                };
                if (kind == RewardPageKind.Relic) row.RelicWeights = (int[])raw.relicWeights.Clone();
                resolved.Add(row);
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            return true;
        }

        private static T Parse<T>(string raw, string field, string label, List<string> errors) where T : struct, Enum
        {
            if (!string.IsNullOrWhiteSpace(raw) && Enum.TryParse(raw, ignoreCase: false, out T value) &&
                Enum.IsDefined(typeof(T), value))
            {
                return value;
            }

            errors.Add($"{label}: {field} '{raw}' is not one of {string.Join(", ", Enum.GetNames(typeof(T)))}.");
            return default;
        }

        private static bool TryStream(string name, out uint stream)
        {
            stream = 0;
            if (string.IsNullOrWhiteSpace(name)) return false;

            var field = typeof(RngStreams).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field == null || !field.IsLiteral || field.FieldType != typeof(uint)) return false;

            stream = (uint)field.GetRawConstantValue();
            return true;
        }
    }
}
