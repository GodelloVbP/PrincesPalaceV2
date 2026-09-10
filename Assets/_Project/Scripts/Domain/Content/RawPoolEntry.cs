using System;

namespace PrincesPalace.Domain.Content
{
    // One combat resource pool, exactly as typed into pools.json.
    //
    // WHY THIS TYPE EXISTS. Mana was code: a constant in Core
    // (GameplayConstants.DefaultMaxMana, deleted in phase B once this row
    // became its one source), a capacity chain in
    // ContentDatabase.Effective, three colours in FightHudPalette and a
    // "everyone has mana" assumption threaded through the fight. The owner's
    // standing per-PC requirement -- one character trades mana for a
    // different resource, cannot carry spell books, and pays for skills in
    // that resource -- is not expressible against any of that without a
    // special case per switch. So mana stops being code and becomes one row
    // in a catalogue, and a second resource is a second row rather than a
    // parallel system. Bjorn's Fury is the second row; it is NOT authored
    // yet (phase E).
    //
    // Flat, [Serializable], plain public fields -- the shape every Raw*Entry
    // in this folder has, for the reason RawCharacterEntry's header states:
    // JsonUtility reads it, System.Text.Json reads it the same way, and the
    // resolver is what turns the strings into enums.
    [Serializable]
    public class RawPoolEntry
    {
        [ContentDoc("Stable identifier a character's primaryPoolId names; unique across every content type.")]
        public string id;

        [ContentDoc("The name shown for this pool, e.g. 'Mana'; the character sheet's max-resource row reads it.")]
        public string displayName;

        // The meter tag, so it is content rather than the literal "MP" the
        // HUD bakes today. Short because it is drawn inside an 18px meter
        // row; the resolver refuses whitespace and anything over six
        // characters rather than letting a long tag silently overflow a bar
        // that UiTextFitAudit only measures against its pinned sample.
        [ContentDoc("The short tag drawn on the combat meter, e.g. 'MP'; no whitespace, at most 6 characters.")]
        public string shortTag;

        // REQUIRED, not blank-defaulted, and it is one of the two fields
        // that decide what a pool IS -- the same reasoning `role` is
        // required for on a character. Guessing WisdomDerived for an
        // unauthored pool would silently hand every Max Mana bonus in the
        // game to a resource whose author meant it to be a flat 0..100 bar.
        [ContentDoc("Required. How capacity is derived: 'WisdomDerived' (the authored capacity is a base every Max Mana source adds to) or 'Fixed' (the authored capacity is the whole number, from every source).")]
        public string capacityRule;

        // NO SEPARATE "a Fixed pool must state a capacity" CHECK. This one
        // is unconditional -- a pool with no capacity is a pool that holds
        // nothing under either rule -- so a second rule scoped to Fixed
        // could never fire, which is the shape ContentDatabase.Validation's
        // header says was deleted from this codebase.
        [ContentDoc("How much this pool holds; must be positive. Under 'WisdomDerived' it is the BASE every Max Mana source adds to, under 'Fixed' it is the entire capacity.")]
        public int capacity;

        // The three gain triggers ResourcePool has, because a primary pool
        // and a signature resource ARE the same runtime thing with different
        // authored numbers -- one class, two slots on CombatantState (the
        // plan's P1, landed in phase B).
        [ContentDoc("Gained at the start of each of the owner's turns; must not be negative.")]
        public int gainPerTurn;
        // ONE MEANING, TWO SEAMS -- see ResourcePool.GainOnAttack's own
        // comment. A pool authored here is a character's PRIMARY pool, and the
        // primary slot pays this off any damaging action. A signature resource
        // is not authored in this file (it is still per-character on
        // characters.json) and its slot pays only on the plain Attack verb,
        // which is a rule about Wool rather than about this field.
        [ContentDoc("Gained once when the owner performs an action that deals damage -- a plain attack or a damaging skill, once however many targets it hits, and never on a miss or a heal; must not be negative.")]
        public int gainOnAttack;
        [ContentDoc("Gained when the owner takes damage; must not be negative.")]
        public int gainOnDamageTaken;

        // The half a signature resource never had, and the half a rage bar
        // needs: a pool that only ever climbs is a pool the player banks and
        // forgets.
        [ContentDoc("Lost at the start of an idle turn (see decayUnless); must not be negative. 0 means the pool never decays.")]
        public int decayPerIdleTurn;

        [ContentDoc("What stops a turn counting as idle: 'Damage' (dealt or taken) or 'AnyAction'. Blank means Damage, and it may only be authored on a pool that actually decays.")]
        public string decayUnless = "";

        // REQUIRED for the same reason capacityRule is: "does this bar open
        // full or empty" is the difference between a budget and an arc, and
        // an unauthored default would quietly pick one.
        [ContentDoc("Required. What the pool holds at the start of a fight: 'Full', 'Zero', or 'Value' (see startValue).")]
        public string startRule;

        [ContentDoc("Only for startRule 'Value': what the pool opens a fight at, 1..capacity. Must be 0 under any other start rule.")]
        public int startValue;

        // COLOURS ARE CONTENT because a second pool is a second colour, and
        // the alternative is a switch in the HUD per resource. Authored as
        // '#RRGGBB' or '#RRGGBBAA', the token form Domain/UiKit already
        // speaks (see FightHudPalette's header); the meter's rim and shade
        // are deepHex at 0.70 and 0.44, which phase D derives rather than
        // authors.
        [ContentDoc("The meter fill colour, '#RRGGBB' or '#RRGGBBAA'.")]
        public string brightHex;
        [ContentDoc("The meter's dark tone, '#RRGGBB' or '#RRGGBBAA'; its rim and shade are derived from this at 0.70 and 0.44 alpha.")]
        public string deepHex;
        [ContentDoc("The colour of the meter's own text, '#RRGGBB' or '#RRGGBBAA'.")]
        public string textHex;

        // NO trackHex. The meter's TRACK is deliberately not recoloured per
        // pool -- see the plan's P7 -- so a field for it would be one
        // nothing reads. Phase D adds it if the picture asks for it.

        [ContentDoc("Whether the meter beats like a heartbeat while the fight runs.")]
        public bool pulse;

        // THE TWO true DEFAULTS, and they are true on purpose: a pool
        // authored with no opinion is mana-shaped, and it is the exotic
        // resource that opts out.
        //
        // allowsSpellBooks is the plan's P6 -- one predicate, consulted at
        // four gates, rather than a "is this Bjorn" check per screen.
        [ContentDoc("Whether a character whose primary pool this is may hold spell books.")]
        public bool allowsSpellBooks = true;

        // restoredByManaEffects exists because of a real hole found while
        // attacking the model: a mana potion, RestorePartyMana and three bot
        // policies all treat "the primary pool" as mana, so without this
        // switch a mana potion is a Fury potion.
        [ContentDoc("Whether mana potions, RestorePartyMana and the bot's mana accounting refill this pool.")]
        public bool restoredByManaEffects = true;

        [ContentDoc("Whether this pool soaks incoming damage before health, the way a signature resource can.")]
        public bool absorbsDamage;
    }

    // JsonUtility cannot deserialize a bare top-level array.
    [Serializable]
    public class RawPoolFile
    {
        [ContentDoc("This file's resource pools; see RawPoolEntry.")]
        public RawPoolEntry[] pools = Array.Empty<RawPoolEntry>();
    }
}
