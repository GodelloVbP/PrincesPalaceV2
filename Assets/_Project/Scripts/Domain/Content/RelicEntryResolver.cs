using PrincesPalace.Domain.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Content
{
    // Validates relics.json. Same collected-not-first-only reporting as the
    // other resolvers.
    public static class RelicEntryResolver
    {
        // Takes the known achievement ids rather than reading a hard-coded
        // whitelist. That whitelist was the weak point: a constant in Domain
        // that content had to match by hand is just a second place to get the
        // same string wrong. ContentBuilder resolves achievements.json FIRST
        // and hands the real ids down, so the only source of truth for what
        // achievements exist is the file that defines them.
        public static bool TryResolveAll(IReadOnlyList<RawRelicEntry> entries,
            IReadOnlyCollection<string> knownAchievementIds,
            IReadOnlyCollection<string> knownCharacterIds,
            out List<ResolvedRelic> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedRelic>();
            errors = new List<string>();

            for (int i = 0; i < entries.Count; i++)
            {
                if (TryResolveOne(entries[i], i, resolved.Count, knownAchievementIds, knownCharacterIds,
                        out var single, out string error))
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

            // Two relics with the same mechanic would make one of them a dead
            // choice in a draft -- every relic on offer needs to be its own
            // reason to pick it. Stat-only and empty relics are exempt: a
            // pool of named-but-unimplemented relics must be draftable.
            foreach (var duplicate in resolved
                         .Where(r => r.Mechanic != EffectKind.None)
                         .GroupBy(r => r.Mechanic)
                         .Where(g => g.Count() > 1))
            {
                errors.Add($"Relics {string.Join(", ", duplicate.Select(r => r.Id))} all resolve to " +
                           $"{duplicate.Key} -- one relic per mechanic.");
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveOne(RawRelicEntry raw, int index, int sortOrder,
            IReadOnlyCollection<string> knownAchievementIds, IReadOnlyCollection<string> knownCharacterIds,
            out ResolvedRelic resolvedRelic, out string error)
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

            // An omitted rarity is Common -- the band a relic belongs to unless
            // somebody says otherwise, and the one the starting pool is made of.
            string rawRarity = string.IsNullOrWhiteSpace(raw.rarity) ? nameof(RelicRarity.Common) : raw.rarity;
            if (!ContentEnum.TryParseName<RelicRarity>(rawRarity, out var rarity))
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
            if (!string.IsNullOrWhiteSpace(unlockedBy)
                && (knownAchievementIds == null || !knownAchievementIds.Contains(unlockedBy)))
            {
                error = $"{label}: unlockedBy '{unlockedBy}' is not an achievement in achievements.json " +
                        $"({(knownAchievementIds == null || knownAchievementIds.Count == 0 ? "none defined" : string.Join(", ", knownAchievementIds))}).";
                return false;
            }

            // An unknown or disallowed kind is a build failure: it would be a
            // relic that is authored, offered, and quietly does nothing.
            var effects = new List<Effect>();
            for (int i = 0; raw.effects != null && i < raw.effects.Length; i++)
            {
                if (!EffectEntryResolver.TryResolve(raw.effects[i], EffectHolder.Relic,
                        $"{label}: effect #{i + 1}", out var effect, out error))
                {
                    return false;
                }

                effects.Add(effect);
            }

            // One mechanic per relic: the draft's one-relic-per-mechanic rule
            // and ResolvedRelic.Mechanic both name a relic by it.
            int mechanics = effects.Count(e => EffectCatalog.Of(e.Kind).IsMechanic);
            if (mechanics > 1)
            {
                error = $"{label}: authors {mechanics} mechanics; a relic carries at most one.";
                return false;
            }

            string bearer = (raw.bearer ?? "").Trim();

            // A misspelled bearer is a relic whose effect reaches NOBODY, and
            // that fails silently -- the same reason unlockedBy is checked
            // above. Validated against the real roster (characters.json),
            // handed in by ContentBuilder like the achievement ids.
            if (bearer.Length > 0 && (knownCharacterIds == null || !knownCharacterIds.Contains(bearer)))
            {
                error = $"{label}: bearer '{bearer}' is not a character id in characters.json " +
                        $"({(knownCharacterIds == null || knownCharacterIds.Count == 0 ? "none defined" : string.Join(", ", knownCharacterIds))}).";
                return false;
            }

            if (!ArtPathConvention.Check(label, "iconPath", raw.iconPath, out error)) return false;
            if (!DescriptionMarkup.Check(label, raw.description, out error)) return false;

            // The same two checks every other SpellPresentation goes through
            // (skills, enemies), not a relic-flavoured copy of them.
            var vfx = raw.vfx ?? new SpellPresentation();
            if (!SpellPresentationPaths.Check(label, vfx, out error)) return false;
            if (!SpellLayerRules.TryCheck(label, vfx, out error)) return false;

            resolvedRelic = new ResolvedRelic(raw.id, raw.displayName, raw.description ?? "", effects, sortOrder,
                raw.iconPath ?? "", rarity, unlockedBy, raw.requiresConvergenceAbility,
                bearer, raw.draftable)
            {
                Vfx = vfx.Copy(),
            };
            error = null;
            return true;
        }
    }
}
