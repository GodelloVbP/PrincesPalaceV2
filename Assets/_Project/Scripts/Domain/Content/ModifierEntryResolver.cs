using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // Validates modifiers.json. Same collected-not-first-only error
    // reporting as the other resolvers (ItemEntryResolver, TalentEntryResolver).
    //
    // Validates ONE modifier entry's internal consistency -- correct type
    // names, correct threshold/magnitude/damageType shape for that type, no
    // dead-weight duplicates. It does not decide how many slots an item can
    // roll or how RiftTier interacts with the roll (ModifierTable's job) or
    // how a resolved effect's magnitude gets scaled by tier/rift
    // (ModifierMagnitude's job, at the ContentDatabase.ModifierEffects seam)
    // -- this is purely "is this one authored entry well-formed".
    public static class ModifierEntryResolver
    {
        // Which ModifierEffectType members read `threshold` at all. Empty
        // today — see ModifierEffect.cs' own header — but kept as a real
        // set rather than an inline `false` so a future health-gated member
        // (the plan's Phase C "retaliate below X% health" candidate) is one
        // line here plus one line in ModifierEffectSet, matching
        // TalentEntryResolver.UsesThreshold's own shape exactly.
        private static readonly HashSet<ModifierEffectType> UsesThreshold = new HashSet<ModifierEffectType>
        {
        };

        // The one rule that is a FLAG: present or absent, with no number
        // attached. Authoring a magnitude on it is the same class of mistake
        // as authoring a threshold on a rule with no health gate — see
        // TalentEntryResolver.IgnoresMagnitude for the identical check.
        private static readonly HashSet<ModifierEffectType> IgnoresMagnitude = new HashSet<ModifierEffectType>
        {
            ModifierEffectType.GuaranteedFirstAction,

            // Runic's mana->Ward conversion: a FLAG, not a number — see that
            // member's own comment on ModifierEffectType. The conversion
            // RATE is a fixed code constant (FightTuning.RunicWardConversionRate),
            // deliberately not author-tunable per modifier.
            ModifierEffectType.ManaToWardOnTurnStartPercent,
        };

        // Which ModifierEffectType members read `damageType` at all — the
        // same two-member set TypedResistanceFlat used to have alone.
        // ElementalDamageOnHitPercent joined it in Phase C: it needs the
        // identical selector (which of six DamageTypes), just for an on-hit
        // rider instead of a resistance.
        private static readonly HashSet<ModifierEffectType> UsesDamageType = new HashSet<ModifierEffectType>
        {
            ModifierEffectType.TypedResistanceFlat,
            ModifierEffectType.ElementalDamageOnHitPercent,
        };

        // Which of UsesDamageType also accepts the "magical" shorthand for
        // "every type that is not Physical". TypedResistanceFlat does —
        // resistance is naturally a blanket "protects against magic" idea.
        // ElementalDamageOnHitPercent does NOT: a hit is dealt AS one
        // specific element, and "magical" names five different elements at
        // once, which is not a coherent single damage instance.
        private static readonly HashSet<ModifierEffectType> AcceptsMagicalShorthand = new HashSet<ModifierEffectType>
        {
            ModifierEffectType.TypedResistanceFlat,
        };

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

            resolvedModifier = new ResolvedModifier(raw.id, raw.displayName, raw.description ?? "", sortOrder, effects);
            error = null;
            return true;
        }

        private static bool TryResolveEffects(RawModifierEntry raw, string label,
            out List<ModifierEffect> effects, out string error)
        {
            effects = new List<ModifierEffect>();
            error = null;

            if (raw.effects == null || raw.effects.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < raw.effects.Length; i++)
            {
                var authored = raw.effects[i];
                string where = $"{label}: effect #{i + 1}";

                if (authored == null || string.IsNullOrWhiteSpace(authored.type))
                {
                    error = $"{where} has no type.";
                    return false;
                }

                if (!Enum.TryParse<ModifierEffectType>(authored.type.Trim(), ignoreCase: true, out var type)
                    || type == ModifierEffectType.None)
                {
                    error = $"{where}: '{authored.type}' isn't a modifier effect. Valid options: " +
                            $"{string.Join(", ", Enum.GetNames(typeof(ModifierEffectType)).Where(n => n != nameof(ModifierEffectType.None)))}.";
                    return false;
                }

                bool wantsThreshold = UsesThreshold.Contains(type);
                if (!wantsThreshold && authored.threshold != 0)
                {
                    error = $"{where} ({type}) has no health gate, so threshold {authored.threshold} would do nothing. Remove it.";
                    return false;
                }

                if (wantsThreshold && (authored.threshold <= 0 || authored.threshold > 100))
                {
                    error = $"{where} ({type}) needs a threshold between 1 and 100 (percent of max health); got {authored.threshold}.";
                    return false;
                }

                bool wantsMagnitude = !IgnoresMagnitude.Contains(type);
                if (wantsMagnitude && authored.magnitude <= 0)
                {
                    error = $"{where} ({type}) needs a positive magnitude; got {authored.magnitude}.";
                    return false;
                }

                if (!wantsMagnitude && authored.magnitude != 0)
                {
                    error = $"{where} ({type}) is a flag — it reads no magnitude, so {authored.magnitude} would do nothing. Remove it.";
                    return false;
                }

                // A TARGETED EFFECT HAS TO SAY WHAT IT TARGETS — same rule
                // and the same reasoning as RelicEntryResolver's
                // ResistanceFlat handling (RelicEntryResolver.cs): unnamed,
                // it is a number protecting (or hitting) nothing.
                DamageType? against = null;
                bool againstMagical = false;

                if (UsesDamageType.Contains(type))
                {
                    string named = (authored.damageType ?? "").Trim();

                    if (AcceptsMagicalShorthand.Contains(type) &&
                        string.Equals(named, "magical", StringComparison.OrdinalIgnoreCase))
                    {
                        againstMagical = true;
                    }
                    else if (Enum.TryParse<DamageType>(named, ignoreCase: true, out var parsed))
                    {
                        against = parsed;
                    }
                    else
                    {
                        string allowed = AcceptsMagicalShorthand.Contains(type)
                            ? "a damage type or 'magical'"
                            : "a damage type";
                        error = $"{where}: '{type}' names damageType " +
                                $"'{authored.damageType}', which isn't {allowed}.";
                        return false;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(authored.damageType))
                {
                    error = $"{where} ({type}) names a damageType, which only " +
                            $"{string.Join("/", UsesDamageType)} read.";
                    return false;
                }

                effects.Add(new ModifierEffect(type, authored.magnitude, authored.threshold, against, againstMagical));
            }

            // Two entries of the same (type, target) on ONE modifier are
            // always a mistake: ModifierEffectSet takes the strongest of a
            // type, so a duplicate is dead weight — except for
            // TypedResistanceFlat, where TWO DIFFERENT elements on one
            // modifier is the whole point (a Hardened-style modifier
            // resisting both Fire and Ice, say). Grouping by (type, against,
            // againstMagical) rather than type alone is what tells those two
            // cases apart without a special case: every non-resistance
            // member always carries the same (null, false) target, so the
            // grouping key degrades to plain type equality for them.
            foreach (var clash in effects.GroupBy(e => (e.Type, e.Against, e.AgainstMagical)).Where(g => g.Count() > 1))
            {
                if (UsesThreshold.Contains(clash.Key.Item1))
                {
                    continue;
                }

                error = $"{label}: {clash.Key.Item1} is listed {clash.Count()} times" +
                        (UsesDamageType.Contains(clash.Key.Item1) ? " against the same target" : "") +
                        " — only the strongest would ever apply. Merge them into one entry.";
                return false;
            }

            return true;
        }
    }
}
