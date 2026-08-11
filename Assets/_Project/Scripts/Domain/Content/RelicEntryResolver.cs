using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Content
{
    // Validates relics.json. Same collected-not-first-only reporting as the
    // other resolvers.
    public static class RelicEntryResolver
    {
        public static bool TryResolveAll(IReadOnlyList<RawRelicEntry> entries, out List<ResolvedRelic> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedRelic>();
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

            foreach (string duplicateId in resolved.GroupBy(r => r.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add($"Duplicate relic id '{duplicateId}' — every id must be unique.");
            }

            // Two relics doing the same thing would make one of them a dead
            // choice in a draft — every relic on offer needs to be its own
            // reason to pick it.
            //
            // None IS EXEMPT, and the exemption is the point: None means "no
            // mechanic yet", and the whole reason it exists is so that a pool
            // of authored-but-unimplemented relics can be drafted against.
            // Applying uniqueness to it would allow exactly one placeholder in
            // the entire game.
            foreach (var duplicateEffect in resolved
                         .Where(r => r.Effect != RelicEffect.None)
                         .GroupBy(r => r.Effect)
                         .Where(g => g.Count() > 1))
            {
                errors.Add($"Relics {string.Join(", ", duplicateEffect.Select(r => r.Id))} all resolve to " +
                           $"{duplicateEffect.Key} — one relic per effect.");
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveOne(RawRelicEntry raw, int index, int sortOrder, out ResolvedRelic resolvedRelic, out string error)
        {
            resolvedRelic = default;
            string label = string.IsNullOrEmpty(raw.id) ? $"relics.json entry #{index + 1}" : $"relic '{raw.id}'";

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

            // An omitted effect is None, not an error. That is what lets a
            // relic be authored as a name and a rarity before anybody has
            // decided what it does.
            string rawEffect = string.IsNullOrWhiteSpace(raw.effect) ? nameof(RelicEffect.None) : raw.effect;
            if (!System.Enum.TryParse<RelicEffect>(rawEffect, ignoreCase: true, out var effect))
            {
                error = $"{label}: effect '{raw.effect}' is not a known RelicEffect.";
                return false;
            }

            // An omitted rarity is Common -- the band a relic belongs to unless
            // somebody says otherwise, and the one the starting pool is made of.
            string rawRarity = string.IsNullOrWhiteSpace(raw.rarity) ? nameof(RelicRarity.Common) : raw.rarity;
            if (!System.Enum.TryParse<RelicRarity>(rawRarity, ignoreCase: true, out var rarity))
            {
                error = $"{label}: rarity '{raw.rarity}' is not a known RelicRarity " +
                        $"(common, uncommon, rare, ultrarare, mythic, godlike).";
                return false;
            }

            // A relic gated on an achievement nobody can earn is a relic that
            // does not exist, and it fails SILENTLY -- it simply never appears
            // in a draft and nothing anywhere says why. Caught at build time
            // instead.
            string unlockedBy = raw.unlockedBy ?? "";
            if (!string.IsNullOrWhiteSpace(unlockedBy) && !AchievementIds.IsKnown(unlockedBy))
            {
                error = $"{label}: unlockedBy '{unlockedBy}' is not a known achievement " +
                        $"({string.Join(", ", AchievementIds.All)}).";
                return false;
            }

            resolvedRelic = new ResolvedRelic(raw.id, raw.displayName, raw.description ?? "", effect, sortOrder,
                raw.iconPath ?? "", rarity, unlockedBy);
            error = null;
            return true;
        }
    }
}
