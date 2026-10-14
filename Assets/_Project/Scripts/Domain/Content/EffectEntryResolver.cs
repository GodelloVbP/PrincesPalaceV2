using System;
using System.Linq;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // Parses one authored effect for a holder, against the kind's catalog row.
    // The one place relics.json and modifiers.json effects are checked, so a
    // rule about a kind (does it read a threshold, which elements may it name)
    // is stated once, in EffectCatalog.
    public static class EffectEntryResolver
    {
        public static bool TryResolve(RawEffect raw, EffectHolder holder, string where,
            out Effect effect, out string error)
        {
            effect = default;

            if (raw == null || string.IsNullOrWhiteSpace(raw.type))
            {
                error = $"{where} has no type.";
                return false;
            }

            if (!Enum.TryParse<EffectKind>(raw.type.Trim(), ignoreCase: true, out var kind)
                || kind == EffectKind.None || !EffectCatalog.Has(kind) || !EffectCatalog.Of(kind).Allows(holder))
            {
                string valid = string.Join(", ", EffectCatalog.All.Where(i => i.Allows(holder)).Select(i => i.Kind.ToString()));
                error = $"{where}: '{raw.type}' isn't an effect a {HolderName(holder)} can grant. " +
                        $"Valid options: {valid}.";
                return false;
            }

            var info = EffectCatalog.Of(kind);

            if (!info.UsesThreshold && raw.threshold != 0)
            {
                error = $"{where} ({kind}) has no health gate, so threshold {raw.threshold} would do nothing. Remove it.";
                return false;
            }

            if (info.UsesThreshold && (raw.threshold <= 0 || raw.threshold > 100))
            {
                error = $"{where} ({kind}) needs a threshold between 1 and 100 (percent of max health); got {raw.threshold}.";
                return false;
            }

            if (!info.UsesMagnitude && raw.magnitude != 0)
            {
                error = $"{where} ({kind}) is a flag -- it reads no magnitude, so {raw.magnitude} would do nothing. Remove it.";
                return false;
            }

            // A relic stat may be a drawback (Whetstone Heart's -10% Defense);
            // everything else is a bonus, and best-of stacking assumes it.
            bool mayBeNegative = holder == EffectHolder.Relic && info.Stat != EffectStat.None;
            if (info.UsesMagnitude && (mayBeNegative ? raw.magnitude == 0 : raw.magnitude <= 0))
            {
                error = mayBeNegative
                    ? $"{where} ({kind}) has a magnitude of 0 and would do nothing."
                    : $"{where} ({kind}) needs a positive magnitude; got {raw.magnitude}.";
                return false;
            }

            // A TARGETED EFFECT HAS TO SAY WHAT IT TARGETS: unnamed, it is a
            // number protecting (or hitting) nothing.
            DamageType? against = null;
            bool againstMagical = false;
            bool againstWearer = false;
            string named = (raw.damageType ?? "").Trim();

            if (info.Target != EffectTarget.None)
            {
                bool acceptsMagical = info.Target == EffectTarget.ElementOrMagical;
                bool acceptsWearer = info.Target == EffectTarget.ElementOrWearer;

                if (acceptsMagical && string.Equals(named, "magical", StringComparison.OrdinalIgnoreCase))
                {
                    againstMagical = true;
                }
                else if (acceptsWearer && string.Equals(named, "wearer", StringComparison.OrdinalIgnoreCase))
                {
                    againstWearer = true;
                }
                else if (Enum.TryParse<DamageType>(named, ignoreCase: true, out var parsed))
                {
                    against = parsed;
                }
                else
                {
                    error = $"{where}: '{kind}' names damageType '{raw.damageType}', which isn't " +
                            (acceptsMagical ? "a damage type or 'magical'." : acceptsWearer ? "a damage type or 'wearer'." : "a damage type.");
                    return false;
                }
            }
            else if (named.Length > 0)
            {
                error = $"{where} ({kind}) names a damageType, which only " +
                        $"{string.Join("/", EffectCatalog.All.Where(i => i.Target != EffectTarget.None).Select(i => i.Kind))} read.";
                return false;
            }

            effect = new Effect(kind, raw.magnitude, raw.threshold, against, againstMagical, againstWearer);
            error = null;
            return true;
        }

        private static string HolderName(EffectHolder holder)
        {
            switch (holder)
            {
                case EffectHolder.Affix: return "modifier";
                case EffectHolder.Track: return "reward track";
                case EffectHolder.Talent: return "talent";
                case EffectHolder.Trait: return "trait";
                default: return "relic";
            }
        }
    }
}
