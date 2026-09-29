using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Content
{
    // Validates pools.json. Same collected-not-first-only reporting as the
    // other resolvers: one build tells you everything wrong with the file,
    // rather than one problem per run.
    public static class PoolEntryResolver
    {
        // The meter tag is drawn inside an 18px meter row beside a value, so
        // a long one does not wrap -- it pushes the numbers out of the bar.
        // Six characters is "FURY" with headroom, and UiTextFitAudit only
        // measures the sample the screen tree pins, so nothing downstream
        // would notice a twelve-character tag until somebody looked at a
        // screenshot.
        public const int MaxShortTagLength = 6;

        public static bool TryResolveAll(IReadOnlyList<RawPoolEntry> entries, out List<ResolvedPool> resolved, out List<string> errors)
        {
            resolved = new List<ResolvedPool>();
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

            // A pool id is what a character's primaryPoolId names, so two
            // pools sharing one would make "which resource does this
            // character carry" unanswerable rather than merely confusing.
            foreach (string duplicateId in resolved.GroupBy(p => p.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add($"Duplicate pool id '{duplicateId}' -- every id must be unique, and a character's " +
                           "primaryPoolId resolves against it.");
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveOne(RawPoolEntry raw, int index, int sortOrder, out ResolvedPool resolvedPool, out string error)
        {
            resolvedPool = default;
            string label = string.IsNullOrEmpty(raw.id) ? $"pools.json entry #{index + 1}" : $"pool '{raw.id}'";

            if (string.IsNullOrWhiteSpace(raw.id))
            {
                error = $"{label}: id is required -- it is what a character's primaryPoolId names.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(raw.displayName))
            {
                error = $"{label}: displayName is required -- the character sheet's max-resource row reads it.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(raw.shortTag))
            {
                error = $"{label}: shortTag is required -- it is the tag drawn on the combat meter.";
                return false;
            }

            string shortTag = raw.shortTag.Trim();
            if (shortTag.Any(char.IsWhiteSpace))
            {
                error = $"{label}: shortTag '{raw.shortTag}' contains whitespace; it is one word drawn inside a " +
                        "meter row.";
                return false;
            }

            if (shortTag.Length > MaxShortTagLength)
            {
                error = $"{label}: shortTag '{shortTag}' is {shortTag.Length} characters and the meter row fits " +
                        $"{MaxShortTagLength}.";
                return false;
            }

            // REQUIRED, both of them: they are the two fields that decide
            // what a pool is, so they get the overload `role` uses at
            // CharacterEntryResolver.cs:130 rather than the blank-defaulting
            // one battleSpriteFacing uses.
            if (!TryParseEnum<PoolCapacityRule>(raw.capacityRule, out var capacityRule))
            {
                error = $"{label}: capacityRule '{raw.capacityRule}' is not a PoolCapacityRule and it is required " +
                        $"-- it decides whether every Max Mana source adds to this pool. " +
                        $"Valid: {NamesOf<PoolCapacityRule>()}.";
                return false;
            }

            if (!TryParseEnum<PoolStartRule>(raw.startRule, out var startRule))
            {
                error = $"{label}: startRule '{raw.startRule}' is not a PoolStartRule and it is required -- it " +
                        $"decides whether this pool is a budget or an arc. Valid: {NamesOf<PoolStartRule>()}.";
                return false;
            }

            if (raw.capacity <= 0)
            {
                error = $"{label}: capacity must be positive (got {raw.capacity}) -- a pool that holds nothing is " +
                        "a meter that can only ever read 0/0.";
                return false;
            }

            foreach (var (name, value) in new (string, int)[]
                     {
                         ("gainPerTurn", raw.gainPerTurn), ("gainOnAttack", raw.gainOnAttack),
                         ("gainOnDamageTaken", raw.gainOnDamageTaken), ("decayPerIdleTurn", raw.decayPerIdleTurn),
                     })
            {
                if (value < 0)
                {
                    error = $"{label}: {name} is {value}; a gain or a decay is stated as a magnitude, never as a " +
                            "negative of the other one.";
                    return false;
                }
            }

            if (!TryParseEnum(raw.decayUnless, out PoolDecayTrigger decayUnless, PoolDecayTrigger.Damage))
            {
                error = $"{label}: decayUnless '{raw.decayUnless}' is not a PoolDecayTrigger. " +
                        $"Valid: {NamesOf<PoolDecayTrigger>()}.";
                return false;
            }

            // AUTHORING A TRIGGER FOR A DECAY THAT CANNOT HAPPEN is a
            // sentence somebody meant to finish. Refused rather than
            // ignored, the way a squadSlot without startsInSquad is.
            if (!string.IsNullOrWhiteSpace(raw.decayUnless) && raw.decayPerIdleTurn <= 0)
            {
                error = $"{label}: decayUnless '{raw.decayUnless}' is authored but decayPerIdleTurn is " +
                        $"{raw.decayPerIdleTurn}, so nothing ever decays and the trigger could not fire.";
                return false;
            }

            if (startRule == PoolStartRule.Value)
            {
                if (raw.startValue < 1 || raw.startValue > raw.capacity)
                {
                    error = $"{label}: startRule Value with startValue {raw.startValue}; it must be 1-{raw.capacity} " +
                            "(the authored capacity). Use startRule Zero for an empty pool and Full for a brimming " +
                            "one, rather than a Value that means one of those.";
                    return false;
                }
            }
            else if (raw.startValue != 0)
            {
                error = $"{label}: startValue {raw.startValue} is set with startRule {startRule}, which decides the " +
                        "opening value on its own. A number nothing reads is a switch somebody meant to set.";
                return false;
            }

            foreach (var (name, value) in new (string, string)[]
                     {
                         ("brightHex", raw.brightHex), ("deepHex", raw.deepHex), ("textHex", raw.textHex),
                     })
            {
                if (!IsColourToken(value))
                {
                    error = $"{label}: {name} '{value}' is not a colour token; write '#RRGGBB' or '#RRGGBBAA'. " +
                            "Alpha is part of a colour in this kit, not a separate field.";
                    return false;
                }
            }

            // A POOL THAT CAN NEVER FILL. All three gains zero and an empty
            // start means the meter sits at 0 for the whole fight and every
            // cost it prices is unaffordable -- the same wording, and the
            // same reasoning, CharacterEntryResolver uses for a signature
            // resource that gains from none of its triggers.
            bool opensEmpty = startRule == PoolStartRule.Zero;
            bool canGain = raw.gainPerTurn > 0 || raw.gainOnAttack > 0 || raw.gainOnDamageTaken > 0
                           || raw.engineFed;
            if (opensEmpty && !canGain)
            {
                error = $"{label}: starts at zero and gains nothing from any of the three triggers, so it would sit " +
                        "at zero for the whole fight. A pool a talent's Fury engine fills says engineFed.";
                return false;
            }

            resolvedPool = new ResolvedPool(
                raw.id.Trim(),
                raw.displayName.Trim(),
                shortTag,
                capacityRule,
                raw.capacity,
                raw.gainPerTurn,
                raw.gainOnAttack,
                raw.gainOnDamageTaken,
                raw.decayPerIdleTurn,
                decayUnless,
                startRule,
                raw.startValue,
                (raw.brightHex ?? string.Empty).Trim(),
                (raw.deepHex ?? string.Empty).Trim(),
                (raw.textHex ?? string.Empty).Trim(),
                raw.pulse,
                raw.allowsSpellBooks,
                raw.restoredByManaEffects,
                raw.absorbsDamage,
                sortOrder);
            error = null;
            return true;
        }

        // '#' then exactly six or eight hex digits. Written out here rather
        // than parsed with a colour type because Domain has no colour type --
        // FightHudPalette is a table of these same strings, and the kit
        // parses them one layer up. Internal: an event fight's overlay tint
        // is the same token and is checked with this same rule.
        internal static bool IsColourToken(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string token = raw.Trim();
            if (token.Length != 7 && token.Length != 9) return false;
            if (token[0] != '#') return false;

            for (int i = 1; i < token.Length; i++)
            {
                if (!Uri.IsHexDigit(token[i])) return false;
            }

            return true;
        }

        private static bool TryParseEnum<T>(string raw, out T value) where T : struct, Enum
        {
            return Enum.TryParse(raw, ignoreCase: true, out value) && Enum.IsDefined(typeof(T), value);
        }

        // Empty means "leave it at the sensible default" rather than
        // "invalid" -- the same pair of overloads CharacterEntryResolver
        // carries, and for the same reason.
        private static bool TryParseEnum<T>(string raw, out T value, T fallback) where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                value = fallback;
                return true;
            }

            return TryParseEnum(raw, out value);
        }

        private static string NamesOf<T>() where T : struct, Enum => string.Join(", ", Enum.GetNames(typeof(T)));
    }
}
