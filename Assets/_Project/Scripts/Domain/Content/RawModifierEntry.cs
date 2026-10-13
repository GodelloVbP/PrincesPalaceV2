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
        [ContentDoc("Stable identifier; written into save data on a rolled item instance.")]
        public string id;
        [ContentDoc("The name shown for this modifier ('Rift affix').")]
        public string displayName;
        [ContentDoc("Flavor/rules text shown to the player.")]
        public string description = "";

        // The rule this modifier grants: one RawEffect whose `type` is an
        // EffectKind an affix may hold (EffectCatalog). A list, but
        // ModifierEntryResolver refuses more than one entry: a family wanting
        // several rules is several ids (Fiery + Emberguard), so each rolls on
        // its own. The resolver, not the JSON shape, states that rule, with a
        // message rather than a silently impossible second entry.
        [ContentDoc("The single rule this modifier grants; more than one entry is rejected.")]
        public RawEffect[] effects = Array.Empty<RawEffect>();
    }

    // JsonUtility cannot deserialize a bare top-level array.
    [Serializable]
    public class RawModifierFile
    {
        [ContentDoc("This file's modifiers; see RawModifierEntry.")]
        public RawModifierEntry[] modifiers = Array.Empty<RawModifierEntry>();
    }
}
