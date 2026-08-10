namespace PrincesPalace.Content
{
    // Whether an item is spent for a one-off effect or worn for a lasting
    // stat bonus. Splitting these apart rather than inferring from "does it
    // have an attackBonus" keeps the Item combat action and the equip UI
    // from ever having to guess: FightController only ever offers
    // Consumables, the paperdoll only ever accepts the worn kinds.
    //
    // Weapon and Equipment are both worn — ItemDefinition.IsEquippable is
    // the check to use, never `kind != Consumable`, so adding a third worn
    // kind later can't silently make it drinkable. They stay separate
    // because attackBonus drives FightController.RollWeaponDrop's power
    // tiering: "what can drop as a weapon, ranked" is a real distinction the
    // loot table needs, not just a difference of slot.
    public enum ItemKind
    {
        Consumable,
        Weapon,
        Equipment,
    }
}
