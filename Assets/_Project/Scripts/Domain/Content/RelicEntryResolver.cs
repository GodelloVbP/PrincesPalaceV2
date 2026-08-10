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
            // choice on the assign screen — every relic on offer needs to be
            // its own reason to pick it.
            foreach (var duplicateEffect in resolved.GroupBy(r => r.Effect).Where(g => g.Count() > 1))
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

            if (!System.Enum.TryParse<RelicEffect>(raw.effect, ignoreCase: true, out var effect))
            {
                error = $"{label}: effect '{raw.effect}' is not a known RelicEffect.";
                return false;
            }

            resolvedRelic = new ResolvedRelic(raw.id, raw.displayName, raw.description ?? "", effect, sortOrder, raw.iconPath ?? "");
            error = null;
            return true;
        }
    }
}
