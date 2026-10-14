using System;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // One thing a holder grants: a relic row, an affix, a reward-track term, a talent node.
    // Combat reads these through EffectSet, never one at a time.
    //
    // [Serializable] with public fields because ResolvedRelic and
    // ResolvedModifier store it in their assets; System.Serializable is BCL,
    // so Domain stays engine-free.
    //
    // An affix's Magnitude is authored UNSCALED and scaled once, where
    // ContentDatabase builds the affix entries (ModifierMagnitude); every hook
    // reads the final number.
    [Serializable]
    public struct Effect
    {
        public EffectKind Kind;

        // Percent, points or rating as the kind's own comment says; 0 on a
        // kind the catalog marks as reading no magnitude. May be negative on a
        // relic stat (a drawback such as Whetstone Heart's -10% Defense).
        public int Magnitude;

        // Percent of max health, for a kind the catalog marks UsesThreshold.
        public int Threshold;

        // The element a typed kind targets (EffectCatalog.Target). The flag is
        // not decoration: Nullable<DamageType> does not survive a serializer,
        // and Physical is DamageType's zero, so a dropped nullable would read
        // back as "Physical" rather than "nothing". AgainstMagical means every
        // non-Physical type, resolved from the "magical" shorthand at authoring
        // time so nothing downstream knows the word.
        public DamageType AgainstType;
        public bool HasAgainst;
        public bool AgainstMagical;

        // "wearer": the element is the wearing character's own attackType,
        // not yet known where the effect is authored. ContentDatabase.
        // CharacterEffects rewrites it into a plain Against before combat
        // reads it.
        public bool AgainstWearer;

        // The skills.json id a skill-scoped talent kind upgrades (EffectCatalog
        // SkillScoped), or empty. Read through EffectSet.BestFor.
        public string SkillId;

        public DamageType? Against => HasAgainst ? (DamageType?)AgainstType : null;

        public Effect(EffectKind kind, int magnitude, int threshold = 0,
            DamageType? against = null, bool againstMagical = false, bool againstWearer = false)
        {
            Kind = kind;
            Magnitude = magnitude;
            Threshold = threshold;
            AgainstType = against ?? default;
            HasAgainst = against.HasValue;
            AgainstMagical = againstMagical;
            AgainstWearer = againstWearer;
            SkillId = "";
        }

        public Effect(EffectKind kind, int magnitude, int threshold, string skillId)
            : this(kind, magnitude, threshold)
        {
            SkillId = skillId ?? "";
        }
    }
}
