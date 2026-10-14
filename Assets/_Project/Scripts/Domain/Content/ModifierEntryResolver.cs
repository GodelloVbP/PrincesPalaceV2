using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Content
{
    // Validates modifiers.json. Same collected-not-first-only error
    // reporting as the other resolvers (ItemEntryResolver, TalentEntryResolver).
    //
    // Validates ONE modifier entry's internal consistency -- correct type
    // names, correct threshold/magnitude/damageType shape for that type, no
    // dead-weight duplicates. It does not decide how many slots an item can
    // roll or how a resolved effect's magnitude gets scaled by tier
    // (ModifierMagnitude's job, at the ContentDatabase.Effects seam)
    // -- this is purely "is this one authored entry well-formed".
    public static class ModifierEntryResolver
    {
        public static bool TryResolveAll(IReadOnlyList<RawModifierEntry> entries, out List<ResolvedModifier> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedModifier>();
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

            foreach (string duplicateId in resolved.GroupBy(m => m.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add($"Duplicate modifier id '{duplicateId}' — every id must be unique.");
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveOne(RawModifierEntry raw, int index, int sortOrder, out ResolvedModifier resolvedModifier, out string error)
        {
            resolvedModifier = default;
            string label = string.IsNullOrEmpty(raw.id) ? $"modifiers.json entry #{index + 1}" : $"modifier '{raw.id}'";

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

            if (!TryResolveEffects(raw, label, out var effects, out error))
            {
                return false;
            }

            // A modifier that grants nothing would roll onto an item and do
            // nothing — the same rule Items/Talents/Equipment already get
            // for the same reason.
            if (effects.Count == 0)
            {
                error = $"{label}: grants no effects at all, so rolling it onto an item would do nothing.";
                return false;
            }

            // EXACTLY ONE EFFECT PER MODIFIER, STRUCTURALLY ENFORCED — the
            // designer's rule after the Phase C bundles (Fiery granting both
            // a burn AND a fire resistance off one roll, Runic granting four
            // rules at once) turned out to feel like one affix pretending to
            // be several rather than several genuine finds. A modifier that
            // wants to grant N rules is now always N separate modifiers.json
            // entries, each a distinct rollable id — never one entry with an
            // effects array of length > 1. Checked here, not left as an
            // authoring convention, so a future bundle is impossible to
            // author rather than merely discouraged.
            if (effects.Count > 1)
            {
                error = $"{label}: authors {effects.Count} effects, but a modifier may grant exactly one. " +
                        "Split this into separate modifiers.json entries, one id per effect.";
                return false;
            }

            resolvedModifier = new ResolvedModifier(raw.id, raw.displayName, raw.description ?? "", sortOrder, effects);
            error = null;
            return true;
        }

        private static bool TryResolveEffects(RawModifierEntry raw, string label,
            out List<Effect> effects, out string error)
        {
            effects = new List<Effect>();
            error = null;

            if (raw.effects == null || raw.effects.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < raw.effects.Length; i++)
            {
                if (!EffectEntryResolver.TryResolve(raw.effects[i], EffectHolder.Affix,
                        $"{label}: effect #{i + 1}", out var effect, out error))
                {
                    return false;
                }

                effects.Add(effect);
            }

            // The same (kind, target) twice on ONE modifier is a copy-paste
            // mistake: affix copies stack best-of, so the duplicate is dead
            // weight. Reported here, before TryResolveOne's generic "exactly
            // one effect", because it is the sharper diagnosis.
            foreach (var clash in effects.GroupBy(e => (e.Kind, e.Against, e.AgainstMagical)).Where(g => g.Count() > 1))
            {
                if (EffectCatalog.Of(clash.Key.Item1).UsesThreshold)
                {
                    continue;
                }

                error = $"{label}: {clash.Key.Item1} is listed {clash.Count()} times" +
                        (EffectCatalog.Of(clash.Key.Item1).Target != EffectTarget.None ? " against the same target" : "") +
                        " — only the strongest would ever apply. Merge them into one entry.";
                return false;
            }

            return true;
        }
    }
}
