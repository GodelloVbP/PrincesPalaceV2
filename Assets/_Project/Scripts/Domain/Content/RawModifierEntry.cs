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

        // The rule this modifier grants. `type` is a
        // Domain.Combat.ModifierEffectType member name, matched
        // case-insensitively — see that enum for what each one means and
        // which of magnitude/threshold/damageType it reads.
        //
        // A LIST for the same shape RawTalentEntry.effects uses, but
        // ModifierEntryResolver rejects anything longer than one — see its
        // own comment. This USED TO be genuinely multi-effect (Phase C's
        // Fiery bundled a burn AND a fire resistance under one roll, Runic
        // bundled four rules); a later designer pass decided that read as
        // one affix pretending to be several, so every modifier now grants
        // exactly one rule and a family that wants several rules is several
        // separate ids (Fiery + Emberguard, not one two-effect Fiery). The
        // field stays an array rather than shrinking to one bare
        // RawModifierEffect because the resolver, not the JSON shape, is
        // what should refuse a bundle — a length check errors with a clear
        // message; a scalar field would silently make a second effect
        // impossible to even attempt, which hides the rule instead of
        // stating it.
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
