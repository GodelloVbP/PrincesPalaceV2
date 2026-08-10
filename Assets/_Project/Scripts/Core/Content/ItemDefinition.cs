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
    public class ItemDefinition : ScriptableObject
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

        [Tooltip("Weapons only: added to the equipping character's Attack while this is equipped.")]
        public int attackBonus;

        [Tooltip("Weapons and Equipment: which of the eight paperdoll slots this is worn in. A Weapon1 item also fits Weapon2.")]
        public EquipmentSlot equipSlot = EquipmentSlot.Weapon1;

        [Tooltip("Weapons and Equipment: combat stats granted for as long as this is worn.")]
        public StatBlock statBonus;

        [Tooltip("Weapons and Equipment: ability scores granted for as long as this is worn.")]
        public AbilityScoreBlock abilityScoreBonus;

        [Tooltip("Weapons and Equipment: ability scores required from everything ELSE worn (plus base scores and talents) before this piece counts as worn at all. Zero on a score means no requirement. An unmet requirement leaves the piece visible and equipped but inert -- see RequirementResolver.")]
        public AbilityScoreBlock requirements;

        [Tooltip("Weapons: which ability scores this rides, and how hard, for the plain Attack. Ten is neutral; every point above multiplies damage and every point below divides it. All grades None (the default) means this weapon does not scale at all and hits for exactly its Attack, which is how every item behaved before scaling existed.")]
        public ScalingProfile scaling;

        [Tooltip("Weapons: which ability scores this contributes to SPELL power (the Skill action), separately from `scaling` above. A staff can carry spellScaling INT A and scaling None -- it does nothing for a plain Attack but everything for a cast. In the off hand this is the ONLY axis that counts; an off-hand weapon never contributes to `scaling` (a swing is one weapon, not two).")]
        public ScalingProfile spellScaling;

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

        [Tooltip("Generated weapons only: which family this came from (e.g. 'sword'). Empty for a hand-authored weapon or a non-weapon. Kept so every modifier of a family can be found and compared without parsing ids — see ItemPowerBudget.")]
        public string weaponFamilyId;

        [Tooltip("Generated items only: which rung of the ladder this is — 0 for the weakest, 10 for the best. Drives the icon, the base stats, the scaling grades, the tier adjective in the name and the rarity colour. NOT the same as an instance's plus, which lives on the InventoryEntry.")]
        public int tier;

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

        // Which rarity band this item's tier falls in, and therefore what
        // colour its name is drawn in. Derived rather than stored so a
        // retuned band boundary cannot leave 261 generated assets disagreeing
        // with RarityBands about what they are.
        public Rarity Rarity => RarityBands.For(tier);

        // Everything this item does to the combat stats while worn, with a
        // Weapon's attackBonus folded in. Weapons keep their own field
        // because the loot tiering sorts on it (FightController.
        // RollWeaponDrop), but no stat consumer should have to know that —
        // they all read this instead, so a weapon and a breastplate are
        // summed by exactly one code path.
        //
        // The plus-zero case. Anything holding a real inventory entry should
        // call StatBonusAt instead; this stays for the store and the item
        // catalogue, which describe the DEFINITION rather than a copy of it.
        public StatBlock TotalStatBonus => StatBonusAt(0);

        // What this item grants when the copy being worn is honed to `plus`.
        //
        // The single place plus reaches worn stats. ItemUpgrade owns the
        // curve; this owns which numbers it applies to — every stat the item
        // grants, including the weapon's flat Attack, so honing a sword and
        // honing a breastplate mean the same thing.
        //
        // Ability scores are deliberately NOT scaled here (see
        // AbilityScoreBonusAt): they feed derived stats through
        // AbilityDerivation, so multiplying them would apply plus twice to
        // anything that reads a derived value.
        public StatBlock StatBonusAt(int plus)
        {
            var withAttack = statBonus + new StatBlock(0, 0, kind == ItemKind.Weapon ? attackBonus : 0, 0);
            if (plus <= 0)
            {
                return withAttack;
            }

            var scaled = StatBlock.Zero;
            foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
            {
                scaled = scaled + StatBlock.ForStat(stat, ItemUpgrade.Apply(withAttack[stat], plus));
            }

            return scaled;
        }

        // Ability scores are NOT touched by plus, on purpose.
        //
        // A score's real worth is whatever AbilityDerivation turns it into,
        // and the stats it derives are already being scaled above. Scaling
        // the score as well would compound the two — a +10 item would raise
        // derived health by 1.4x on top of a 1.4x raw bonus. Kept as a named
        // method anyway so the decision is visible at the call site rather
        // than being an absence someone later "fixes".
        public AbilityScoreBlock AbilityScoreBonusAt(int plus)
        {
            return abilityScoreBonus;
        }
    }
}
