using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Content
{
    // Validates the human-edited spells.json — every field is required here
    // (see RawSpellTierEntry), so this is pure validation, not defaulting.
    // Same collected-not-first-only error reporting as EnemyEntryResolver,
    // for the same reason: a hand-edited file should say everything wrong
    // with it in one pass, not make the author fix and rebuild once per
    // typo.
    public static class SpellTierEntryResolver
    {
        public static bool TryResolveAll(IReadOnlyList<RawSpellTierEntry> entries, out List<ResolvedSpellTier> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedSpellTier>();
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

            var duplicateLevels = resolved
                .GroupBy(t => t.Level)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (int duplicateLevel in duplicateLevels)
            {
                errors.Add($"Duplicate spell tier for level {duplicateLevel} — every level must appear at most once.");
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            resolved = resolved.OrderBy(t => t.Level).ToList();
            return true;
        }

        private static bool TryResolveOne(RawSpellTierEntry raw, int index, int sortOrder, out ResolvedSpellTier resolvedTier, out string error)
        {
            resolvedTier = default;
            string label = $"spells.json entry #{index + 1}" + (raw.level > 0 ? $" (level {raw.level})" : "");

            if (raw.level < 1)
            {
                error = $"{label}: level must be 1 or higher (got {raw.level}).";
                return false;
            }

            if (string.IsNullOrWhiteSpace(raw.displayName))
            {
                error = $"{label}: displayName is required.";
                return false;
            }

            if (raw.manaCost <= 0)
            {
                error = $"{label}: manaCost must be a positive number (got {raw.manaCost}).";
                return false;
            }

            if (raw.powerMultiplier <= 0f)
            {
                error = $"{label}: powerMultiplier must be a positive number (got {raw.powerMultiplier}).";
                return false;
            }

            var scalingErrors = new List<string>();
            if (!ScalingLineParser.TryParse(raw.scalesWith, label, out var scaling, scalingErrors))
            {
                error = string.Join(" ", scalingErrors);
                return false;
            }

            var requirementErrors = new List<string>();
            if (!AbilityScoreLineParser.TryParse(raw.requires, label, out var requirements, requirementErrors))
            {
                error = string.Join(" ", requirementErrors);
                return false;
            }

            resolvedTier = new ResolvedSpellTier(raw.level, raw.displayName, raw.manaCost, raw.powerMultiplier, sortOrder, scaling, requirements);
            error = null;
            return true;
        }

        // The tier FightController should actually use for a caster at
        // `characterLevel`: the highest-level tier not exceeding it. Assumes
        // `tiers` is already sorted ascending by Level (TryResolveAll always
        // returns them that way). Returns null only if `tiers` is empty or
        // every tier requires a level higher than `characterLevel` — with a
        // real spells.json (starting at level 1) that second case can't
        // happen for any real character, since Character.level starts at 1.
        public static ResolvedSpellTier TierForLevel(IReadOnlyList<ResolvedSpellTier> tiers, int characterLevel)
        {
            // A plain reference null since ResolvedSpellTier became a class --
            // the same shape change ResolvedSkill made; `?.DisplayName` at the
            // call sites reads identically either way.
            ResolvedSpellTier best = null;
            foreach (var tier in tiers)
            {
                if (tier.Level <= characterLevel)
                {
                    best = tier;
                }
                else
                {
                    break;
                }
            }

            return best;
        }
    }
}
