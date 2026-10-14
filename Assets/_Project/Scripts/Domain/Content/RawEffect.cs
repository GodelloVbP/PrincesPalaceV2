using System;

namespace PrincesPalace.Domain.Content
{
    // One effect exactly as typed into relics.json or modifiers.json, before
    // EffectEntryResolver parses `type`. Separate from Domain.Combat.Effect
    // because JsonUtility reads an enum only from an integer, and the files
    // carry the member name.
    [Serializable]
    public class RawEffect
    {
        [ContentDoc("Which Domain.Combat.EffectKind this grants, matched case-insensitively; the holder must be allowed it (EffectCatalog).")]
        public string type = "";

        [ContentDoc("How much: percent, points or rating as the kind says. An affix's is the UNSCALED base (the fight reads base x TierMultiplier x RiftMultiplier). Omitted for a flag kind; may be negative only on a relic stat.")]
        public int magnitude;

        [ContentDoc("Percent of max health for a health-gated kind (WorldEndersCrown); omitted otherwise.")]
        public int threshold;

        [ContentDoc("A DamageType name the kind targets, 'magical' for every non-Physical type where the kind accepts it (ResistanceFlat), or 'wearer' for the wearing character's own attackType, Physical if none (ElementalDamageOnHitPercent); required by those two kinds only.")]
        public string damageType = "";
    }
}
