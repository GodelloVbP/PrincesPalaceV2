using System;

namespace PrincesPalace.Domain.Content
{
    // One relic, exactly as typed into relics.json.
    [Serializable]
    public class RawRelicEntry
    {
        public string id;
        public string displayName;
        public string description = "";

        // Parsed against RelicEffect by name (case-insensitive) — a typo here
        // is a content-build failure via RelicEntryResolver, not a silently
        // inert relic.
        public string effect = "";

        // Optional. Points at the keyed icon under Assets/_Project/Art/Items/
        // Relics/Processed/ — empty until the art lands, in which case
        // SceneBuilder.LoadSprite leaves it blank (with a console warning)
        // and RelicsController falls back to the flat accent-coloured circle
        // it already draws today.
        public string iconPath = "";
    }

    // JsonUtility cannot deserialize a bare top-level array.
    [Serializable]
    public class RawRelicFile
    {
        public RawRelicEntry[] relics = Array.Empty<RawRelicEntry>();
    }
}
