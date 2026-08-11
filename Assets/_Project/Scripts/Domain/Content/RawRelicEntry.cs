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

        // How rare the OFFER is. Parsed against RelicRarity by name
        // (case-insensitive); empty means Common. Changing a relic's band is
        // exactly this one word and nothing else -- no code, no rebuild of
        // anything but content.
        public string rarity = "";

        // The achievement that has to be earned before this relic can appear
        // at all. Empty means available from the first run.
        //
        // A STRING rather than an enum so content can name an achievement the
        // code has not implemented yet: AchievementIds validates it at build
        // time, so a typo is a content-build failure rather than a relic that
        // silently never unlocks.
        public string unlockedBy = "";
    }

    // JsonUtility cannot deserialize a bare top-level array.
    [Serializable]
    public class RawRelicFile
    {
        public RawRelicEntry[] relics = Array.Empty<RawRelicEntry>();
    }
}
