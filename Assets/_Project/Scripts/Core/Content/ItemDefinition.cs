using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;
using UnityEngine;

namespace PrincesPalace.Content
{
    // An authored item: a stackable Consumable, or something worn in one of
    // the eight paperdoll slots. Authored in
    // Assets/_Project/ContentData/items.json and generated into an asset by
    // ContentBuilder — never hand-edited under Resources/Content, which
    // ContentBuilder deletes wholesale on every build.
    public class ItemDefinition : ScriptableObject, IOrderedContent
    {
        [Tooltip("Stable identifier written into save files. Never rename this after a save exists.")]
        public string id;

        public string displayName;

        [TextArea]
        public string description;

        [Tooltip("Consumable items are spent in combat for a one-off effect; Weapons and Equipment are worn from the Equipment screen for a lasting bonus.")]
        public ItemKind kind = ItemKind.Consumable;

        [Tooltip("Consumables only: what this item does when used in combat.")]
        public ItemEffect effect = ItemEffect.Heal;

        [Tooltip("Consumables only: amount restored (health or mana, depending on effect) when used in combat.")]
        public int amount = 20;

        [Tooltip("Weapons and Equipment: which of the eight paperdoll slots this is worn in. A Weapon1 item also fits Weapon2.")]
        public EquipmentSlot equipSlot = EquipmentSlot.Weapon1;

        [Tooltip("Granted once into a brand new profile's stash. Not re-granted on load — this is a starting kit, not income.")]
        public bool startingStock;

        [Tooltip("Editor-time path to this item's icon art. Baked into scenes by SceneBuilder; empty means no icon is shown.")]
        public string iconPath;

        [Tooltip("Permanent Save.Gold cost to buy one in the Divine Principality store.")]
        public int cost = 15;

        // Resources.LoadAll returns assets in filename (alphabetical) order,
        // not authoring order — the same trap Characters/Enemies/Upgrades/
        // Talents already guard against with an explicit sortOrder. There
        // was only ever one item until now, so this was never load-bearing;
        // closing it now that a second item exists.
        public int sortOrder;

        [Tooltip("Set pieces only: which armour set this came from. Empty for a one-off item. Kept so a future set bonus can group pieces without parsing ids.")]
        public string setId;

        [Tooltip("Generated weapons only: which family this came from (e.g. 'sword'). Empty for a hand-authored weapon or a non-weapon. Kept so every generated weapon's family can be identified without parsing its id — see ContentDatabase.Offerable.")]
        public string weaponFamilyId;

        [Tooltip("Generated items only: which rung of the ladder this per-tier asset is. The tier a copy carries is ItemInstance.Tier; what it grants is read through ContentDatabase.Effective.")]
        public int tier;

        [Tooltip("The base this definition belongs to: '<set>_<piece>' or '<family>' for generated gear, the id itself for a hand-authored item. The id a copy carries once content holds one asset per base (docs/PLAN_ITEMS.md 2.3).")]
        public string baseId;

        [Tooltip("Equippables: what the base is worth at every tier. ContentDatabase.Effective reads this with a copy's tier and its slot's honing.")]
        public GearCurve curve;

        // Whether this came out of itemsets.json rather than items.json.
        // A generated piece is otherwise indistinguishable from a
        // hand-authored item on purpose — equipment, the inventory, the
        // paperdoll and the save file all stay unaware that sets exist.
        public bool IsSetPiece => !string.IsNullOrEmpty(setId);

        // THE test for "can this go on a character". Always ask this rather
        // than `kind != Consumable` or `kind == Weapon` — those two spellings
        // disagree the moment a worn kind is added, and one of them decides
        // whether combat will let you drink a pair of gloves.
        public bool IsEquippable => kind == ItemKind.Weapon || kind == ItemKind.Equipment;

        // Listed by the authored order ContentBuilder stamped on it.
        public int SortOrder => sortOrder;
    }
}
