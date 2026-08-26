using System;

namespace PrincesPalace.Domain.Content
{
    // One item modifier ("Rift affix"), exactly as typed into modifiers.json.
    //
    // No -1 sentinels here, same reasoning as RawTalentEntry: id/displayName/
    // description are plain strings with an obvious "omitted" reading, and
    // `effects` is the only real payload — an empty array already means
    // "authored nothing", which ModifierEntryResolver rejects the same way
    // RawTalentEntry's "grants nothing at all" check does for a talent.
    [Serializable]
    public class RawModifierEntry
    {
        public string id;
        public string displayName;
        public string description = "";

        // Rules this modifier grants. `type` is a
        // Domain.Combat.ModifierEffectType member name, matched
        // case-insensitively — see that enum for what each one means and
        // which of magnitude/threshold/damageType it reads. A LIST rather
        // than one entry for the same reason RawTalentEntry.effects is: a
        // future modifier granting two rules at once (a Fiery/Frosty hybrid,
        // say) is one modifier expressed as two entries rather than two
        // modifiers.
        public RawModifierEffect[] effects = Array.Empty<RawModifierEffect>();
    }

    // One rule from a modifier's `effects` list, before the enum name has
    // been parsed. Separate from Domain.Combat.ModifierEffect for exactly
    // the reason RawTalentEffect is separate from TalentEffect: JsonUtility
    // deserialises an enum field from an integer and nothing else, so the
    // authored file has to carry a string and ModifierEntryResolver is what
    // turns it into the enum.
    [Serializable]
    public class RawModifierEffect
    {
        public string type = "";
        public int magnitude;
        public int threshold;

        // TypedResistanceFlat's target only — one of the DamageType names,
        // or "magical" for every type that is not Physical. Required by that
        // type and refused on every other, the identical rule
        // RelicEntryResolver already enforces for RawRelicModifier.damageType
        // (RelicEntryResolver.cs) — an author who names a damage type on a
        // FlatSpeedBonus believes they have made a typed thing.
        public string damageType = "";
    }

    // JsonUtility cannot deserialize a bare top-level array.
    [Serializable]
    public class RawModifierFile
    {
        public RawModifierEntry[] modifiers = Array.Empty<RawModifierEntry>();
    }
}
