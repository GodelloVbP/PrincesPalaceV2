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
        [ContentDoc("Stable identifier; written into save files and never renamed once used.")]
        public string id;
        [ContentDoc("The name shown for this character.")]
        public string displayName;

        // Parsed against CharacterRole by name (case-insensitive) — a typo
        // is a content-build failure via CharacterEntryResolver, not a
        // character that silently falls through to the wrong Skill case.
        [ContentDoc("Which CharacterRole this is, matched case-insensitively.")]
        public string role = "";

        // Parsed against DamageType the same way. Defaults to Physical when
        // left empty, which is what an unauthored character should be.
        [ContentDoc("Which DamageType this character's attacks carry; blank means Physical.")]
        public string attackType = "";

        // Base stats, before any talents. The old single `defense` field is
        // GONE, not renamed to either of these — see StatType's own header.
        // Defaults carry the old default (defense 3) through the Phase 1
        // placeholder-conversion formula (8x/4x) rather than dropping to
        // zero, so an unauthored character still starts with SOME armour.
        [ContentDoc("Base max health, before talents.")]
        public int maxHealth = 20;
        [ContentDoc("Base speed, before talents; drives how often this character acts in the charge-based turn order.")]
        public int speed = 10;
        [ContentDoc("Base attack, before talents.")]
        public int attack = 5;
        [ContentDoc("Base flat reduction against Physical damage, before talents.")]
        public int physicalDefense = 24;
        [ContentDoc("Base flat reduction against non-Physical damage, before talents.")]
        public int magicalDefense = 12;

        // The six ability scores. They must total exactly
        // CharacterEntryResolver.AbilityScoreBudget — see its comment for why
        // that is a budget rather than a floor.
        [ContentDoc("Base Strength; the six ability scores must total exactly the resolver's budget.")]
        public int strength = 10;
        [ContentDoc("Base Dexterity; the six ability scores must total exactly the resolver's budget.")]
        public int dexterity = 10;
        [ContentDoc("Base Constitution; the six ability scores must total exactly the resolver's budget.")]
        public int constitution = 10;
        [ContentDoc("Base Wisdom; the six ability scores must total exactly the resolver's budget.")]
        public int wisdom = 10;
        [ContentDoc("Base Intelligence; the six ability scores must total exactly the resolver's budget.")]
        public int intelligence = 10;
        [ContentDoc("Base Charisma; the six ability scores must total exactly the resolver's budget.")]
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
        [ContentDoc("This character's luck stat; a separate axis from the six ability scores, 0 is the honest default.")]
        public int princesFavor;

        [ContentDoc("Assets-relative path to a head-and-shoulders portrait baked into the scene at build time; empty means no art yet.")]
        public string portraitPath = "";
        [ContentDoc("Resources-relative folder of full-body stance art loaded at runtime; empty means no art yet.")]
        public string battleSpritePath = "";

        // Which way the battle art is drawn in its source file. "Right" or
        // "Left"; the stage mirrors as needed so a character always faces the
        // opposition rather than off the edge of the screen.
        [ContentDoc("Which way the battle art is drawn in its source file: 'Right' or 'Left'.")]
        public string battleSpriteFacing = "";

        // Optional private combat resource. An empty signatureId means the
        // character has none — that is the meaningful distinction, not a
        // zero capacity (see CombatantState.Signature).
        [ContentDoc("The id of this character's private signature resource; empty means the character has none.")]
        public string signatureId = "";
        [ContentDoc("The name shown for the signature resource, e.g. 'Wool'.")]
        public string signatureDisplayName = "";
        [ContentDoc("How much signature resource this character can hold.")]
        public int signatureCapacity;
        [ContentDoc("Signature resource gained automatically at the start of each of this character's turns.")]
        public int signatureGainPerTurn;
        [ContentDoc("Signature resource gained when this character attacks.")]
        public int signatureGainOnAttack;
        [ContentDoc("Signature resource gained when this character takes damage.")]
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
        [ContentDoc("Whether the signature resource also soaks incoming damage before health.")]
        public bool signatureAbsorbsDamage;

        // WHO A NEW PROFILE OPENS WITH, said out loud in content instead of
        // falling out of where the row happens to sit in the file.
        //
        // It used to be the first three entries, full stop -- SaveData.
        // CreateNew took `roster.Take(EffectiveMaxSquadSize())` and the roster
        // is authored order. Nothing said so anywhere an author would look
        // (the Step 0 baseline found it only by reading CreateNew), and it
        // meant a character appended to the end of characters.json was in the
        // roster and could never be fielded, while inserting one at position
        // three silently benched whoever was there. Both are the trap
        // CLAUDE.md gotcha 4 names, in the one place a player would notice.
        //
        // EXACTLY THREE, EACH WITH A DIFFERENT SLOT, and CharacterEntryResolver
        // refuses the file otherwise, naming the ids. Two flags rather than one
        // ordered list because the fact belongs on the character: a roster is
        // edited a row at a time, and a separate list somewhere else is the
        // thing that ends up disagreeing with the rows.
        [ContentDoc("Whether a fresh profile fields this character; exactly three characters must set it.")]
        public bool startsInSquad;

        [ContentDoc("This character's place in the starting squad, 1-3 and unique; only read when startsInSquad is set.")]
        public int squadSlot;
    }

    // JsonUtility cannot deserialize a bare top-level array.
    [Serializable]
    public class RawCharacterFile
    {
        [ContentDoc("This file's characters; see RawCharacterEntry.")]
        public RawCharacterEntry[] characters = Array.Empty<RawCharacterEntry>();
    }
}
