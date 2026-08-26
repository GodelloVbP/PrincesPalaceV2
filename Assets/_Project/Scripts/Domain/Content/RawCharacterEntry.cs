using System;

namespace PrincesPalace.Domain.Content
{
    // One playable character, exactly as typed into characters.json.
    //
    // Flat rather than nested (no sub-object for stats or ability scores),
    // because JsonUtility handles flat int fields cleanly and a nested struct
    // would need its own [Serializable] mirror for no gain. The resolver is
    // what assembles these into StatBlock/AbilityScoreBlock.
    [Serializable]
    public class RawCharacterEntry
    {
        public string id;
        public string displayName;

        // Parsed against CharacterRole by name (case-insensitive) — a typo
        // is a content-build failure via CharacterEntryResolver, not a
        // character that silently falls through to the wrong Skill case.
        public string role = "";

        // Parsed against DamageType the same way. Defaults to Physical when
        // left empty, which is what an unauthored character should be.
        public string attackType = "";

        // Base stats, before any talents. The old single `defense` field is
        // GONE, not renamed to either of these — see StatType's own header.
        // Defaults carry the old default (defense 3) through the Phase 1
        // placeholder-conversion formula (8x/4x) rather than dropping to
        // zero, so an unauthored character still starts with SOME armour.
        public int maxHealth = 20;
        public int speed = 10;
        public int attack = 5;
        public int physicalDefense = 24;
        public int magicalDefense = 12;

        // The six ability scores. They must total exactly
        // CharacterEntryResolver.AbilityScoreBudget — see its comment for why
        // that is a budget rather than a floor.
        public int strength = 10;
        public int dexterity = 10;
        public int constitution = 10;
        public int wisdom = 10;
        public int intelligence = 10;
        public int charisma = 10;

        // Optional art. portraitPath is an ASSETS-relative path to a
        // head-and-shoulders portrait baked into the scene at build time;
        // battleSpritePath is a RESOURCES-relative folder of full-body stance
        // art loaded at runtime. The two conventions are different on
        // purpose and the resolver checks each against its own — see
        // docs/ART_PIPELINE.md. Empty means no art yet, which degrades to a
        // blank portrait slot and a plain stage plate respectively.
        // PRINCE'S FAVOR: this character's luck.
        //
        // Not an ability score, deliberately. The six scores spend a fixed
        // budget against each other, so adding a seventh would silently
        // rebalance every authored character; Favor is its own axis and its
        // own decision. 0 is the honest default -- an unfavoured character.
        public int princesFavor;

        public string portraitPath = "";
        public string battleSpritePath = "";

        // Which way the battle art is drawn in its source file. "Right" or
        // "Left"; the stage mirrors as needed so a character always faces the
        // opposition rather than off the edge of the screen.
        public string battleSpriteFacing = "";

        // Optional private combat resource. An empty signatureId means the
        // character has none — that is the meaningful distinction, not a
        // zero capacity (see CombatantState.Signature).
        public string signatureId = "";
        public string signatureDisplayName = "";
        public int signatureCapacity;
        public int signatureGainPerTurn;
        public int signatureGainOnAttack;
        public int signatureGainOnDamageTaken;

        // Whether this resource also soaks incoming damage before health.
        //
        // False by default, which is the opposite of how Wool behaved before
        // the talent rework and is the point: a resource that is both armour
        // and ammunition asks the player the same "bank or spend" question
        // twice and answers it differently each time. Kept as a per-character
        // switch rather than deleted outright because the soak machinery is
        // tuned (see ContentDatabase.SignatureAbsorbPerPoint) and a future
        // character whose resource IS armour should not have to rebuild it.
        public bool signatureAbsorbsDamage;
    }

    // JsonUtility cannot deserialize a bare top-level array.
    [Serializable]
    public class RawCharacterFile
    {
        public RawCharacterEntry[] characters = Array.Empty<RawCharacterEntry>();
    }
}
