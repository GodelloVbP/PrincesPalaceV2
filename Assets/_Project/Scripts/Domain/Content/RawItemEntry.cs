using System;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // One item, exactly as typed into items.json. Same -1 / "" sentinel
    // trick as RawEnemyEntry: Unity's JsonUtility only overwrites fields
    // actually present in the source JSON, so a C# initializer surviving
    // untouched is how the resolver tells "the author omitted this" from
    // "the author wrote 0" — which matters here because 0 is a legitimate
    // cost.
    //
    // The two bonus blocks need no sentinel: an absent block deserializes
    // to all-zero, which is exactly "grants nothing", so there is nothing to
    // distinguish.
    [Serializable]
    public class RawItemEntry
    {
        [ContentDoc("Stable identifier; written into save files and inventory entries.")]
        public string id;
        [ContentDoc("The name shown for this item.")]
        public string displayName;
        [ContentDoc("Flavor text shown to the player.")]
        public string description = "";

        // "Consumable" (default), "Weapon", or "Equipment", case-insensitive.
        [ContentDoc("Which ItemKind this is: Consumable (default), Weapon, or Equipment, case-insensitive.")]
        public string kind = "";

        // Consumables: "Heal" (default) or "RestoreMana", and how much.
        [ContentDoc("For a Consumable, which effect it triggers: Heal (default) or RestoreMana.")]
        public string effect = "";
        [ContentDoc("For a Consumable, how much the effect heals or restores.")]
        public int amount = -1;

        // Weapons: added to the equipping character's Attack. Required for
        // a Weapon — a weapon that does nothing is a content mistake, not a
        // valid design, so there's deliberately no default for it.
        [ContentDoc("For a Weapon, added to the equipping character's Attack; required for a Weapon.")]
        public int attackBonus = -1;

        // Equipment: which of the eight paperdoll slots this is worn in
        // (see EquipmentSlots.AuthorableNames). Required for Equipment.
        // Optional for a Weapon, which defaults to Weapon1 and may only
        // name a hand slot.
        [ContentDoc("Which paperdoll slot this is worn in; required for Equipment, optional (defaults to Weapon1) for a Weapon.")]
        public string slot = "";

        // Equipment/Weapons: granted for as long as the item is worn.
        // statBonus moves the combat stats (maxHealth/speed/attack/
        // manaRegen/physicalDefense/magicalDefense);
        // abilityScoreBonus moves the six sheet scores (strength/dexterity/
        // constitution/wisdom/intelligence/charisma). Both are optional, but
        // a piece of Equipment granting neither is rejected — same rule, and
        // the same reasoning, as a Weapon with no attackBonus.
        [ContentDoc("Combat stats granted for as long as this Equipment/Weapon is worn.")]
        public StatBlock statBonus;
        [ContentDoc("Ability scores granted for as long as this Equipment/Weapon is worn.")]
        public AbilityScoreBlock abilityScoreBonus;

        // What a character needs, from everything else worn plus base scores
        // and talents, before this item counts as worn at all — see
        // RequirementResolver. "<ability score> <amount>" lines, same shape
        // as scaling's own authoring. Optional; an item with none is always
        // live once equipped, which is every item's behaviour today.
        [ContentDoc("'<ability score> <amount>' lines gating whether this item counts as worn at all.")]
        public string[] requires = Array.Empty<string>();

        // Granted once, into a brand new profile's stash (see
        // SaveData.CreateNew). Not re-granted on load, so this is a starting
        // kit, not an income source.
        [ContentDoc("Whether one is granted, once, into a brand new profile's starting stash.")]
        public bool startingStock;

        // Permanent-currency Store price. Weapons and Equipment default to 0
        // and aren't sold (see ContentDatabase.Consumables); consumables
        // default to 15.
        [ContentDoc("Permanent-currency Store price; Weapons/Equipment default to 0 and are not sold.")]
        public int cost = -1;

        [ContentDoc("Editor-time path to this item's icon.")]
        public string iconPath = "";
    }

    // JsonUtility can't deserialize a bare top-level JSON array, so
    // items.json is one object with an "items" array inside it.
    [Serializable]
    public class RawItemFile
    {
        public RawItemEntry[] items = Array.Empty<RawItemEntry>();
    }
}
