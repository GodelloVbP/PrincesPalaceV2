using System;

namespace PrincesPalace.Domain.Content
{
    // One relic, exactly as typed into relics.json.
    [Serializable]
    public class RawRelicEntry
    {
        [ContentDoc("Stable identifier; written into save data via unlock/achievement references.")]
        public string id;
        [ContentDoc("The name shown for this relic when offered.")]
        public string displayName;
        [ContentDoc("Flavor/rules text shown to the player.")]
        public string description = "";

        // Parsed against RelicEffect by name (case-insensitive) — a typo here
        // is a content-build failure via RelicEntryResolver, not a silently
        // inert relic.
        [ContentDoc("Which RelicEffect this grants, matched case-insensitively; empty resolves to None.")]
        public string effect = "";

        // Optional. Points at the keyed icon under Assets/_Project/Art/Items/
        // Relics/Processed/ — empty until the art lands, in which case
        // SceneBuilder.LoadSprite leaves it blank (with a console warning)
        // and RelicsController falls back to the flat accent-coloured circle
        // it already draws today.
        [ContentDoc("Editor-time path to this relic's icon under Assets/_Project/Art/Items/Relics/Processed/; empty falls back to a flat accent-coloured circle.")]
        public string iconPath = "";

        // How rare the OFFER is. Parsed against RelicRarity by name
        // (case-insensitive); empty means Common. Changing a relic's band is
        // exactly this one word and nothing else -- no code, no rebuild of
        // anything but content.
        [ContentDoc("How rare the offer is, one of RelicRarity's names; empty means Common.")]
        public string rarity = "";

        // The achievement that has to be earned before this relic can appear
        // at all. Empty means available from the first run.
        //
        // A STRING rather than an enum so content can name an achievement the
        // code has not implemented yet: AchievementIds validates it at build
        // time, so a typo is a content-build failure rather than a relic that
        // silently never unlocks.
        [ContentDoc("The achievement id that must be earned before this relic can be offered; empty means available from the first run.")]
        public string unlockedBy = "";

        // Numeric changes this relic makes. Empty for a relic whose whole
        // behaviour is a RelicEffect, and populated for one that is just a
        // number -- which is most of them, and none of which should cost C#.
        [ContentDoc("Numeric stat changes this relic grants; see RawRelicModifier.")]
        public RawRelicModifier[] modifiers = System.Array.Empty<RawRelicModifier>();

        // Mechanic (g), ACQUISITION GATE: true means this relic is only
        // ever offered to a party that has a convergence/ultimate ability
        // (ConvergenceGate.HasConvergenceAbility) -- false (the default)
        // means every relic that does not say otherwise, which is nearly
        // all of them.
        [ContentDoc("Whether this relic is only ever offered to a party that already has a convergence/ultimate ability.")]
        public bool requiresConvergenceAbility;

        // A relic the run holds but only ONE character acts on. Run relics
        // reach every fielded kit, so without this an event reward meant for
        // Shawn would work for whoever else was carrying the run.
        //
        // EFFECT ONLY. FightEncounterAdapter flattens relic modifiers onto
        // every party member regardless, so RelicEntryResolver refuses a
        // bearer on a relic that also has modifiers rather than let the
        // field promise something the adapter does not keep.
        [ContentDoc("Optional character id; when set, only that character gets this relic's effect. Refused together with modifiers, which apply party-wide.")]
        public string bearer = "";

        // False keeps a relic out of every offer -- the draft and the shop's
        // relic shelf both read RunOrchestrator.AvailableRelicOptions. For a
        // relic that is granted some other way (an event), where drafting it
        // would skip the story that grants it.
        [ContentDoc("Whether this relic can be offered in a relic draft or the shop's relic shelf; false for relics granted another way, such as by an event.")]
        public bool draftable = true;

        // HOW A TRIGGERED RELIC LOOKS when it fires on its own beat (the
        // Toll of the Flock's ghost sheep), the same SpellPresentation a
        // skill carries so it plays through the same beat path. Optional:
        // omitted is SpellPresentation.None and nothing extra plays.
        [ContentDoc("Optional presentation played when this relic's effect fires on its own beat; see SpellPresentation. Omitted plays nothing.")]
        public SpellPresentation vfx = new SpellPresentation();
    }

    // JsonUtility cannot deserialize a bare top-level array.
    [Serializable]
    public class RawRelicFile
    {
        public RawRelicEntry[] relics = Array.Empty<RawRelicEntry>();
    }
}
