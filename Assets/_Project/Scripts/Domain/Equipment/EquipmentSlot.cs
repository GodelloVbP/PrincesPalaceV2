namespace PrincesPalace.Domain.Equipment
{
    // The eight places a character can wear something. Lives in Domain (not
    // Content) for the same reason DamageType does: it is a plain enum with
    // no engine or content dependency, so the JSON resolver, the save data
    // and the Unity-side ItemDefinition can all name the same values instead
    // of maintaining a mirror enum and a name-mapping step.
    //
    // Declaration order is also UI order — EquipmentSlots.All hands this
    // straight to the paperdoll, so re-ordering here re-orders the screen.
    //
    // Weapon1/Weapon2 are two slots that accept the SAME item ("a sword fits
    // either hand"). An item declares Weapon1 as the slot it belongs to;
    // EquipmentSlots.Accepts is the single place that knows Weapon2 also
    // takes it. That rule is deliberately one function rather than a second
    // "which slots does this fit" enum on the item side.
    public enum EquipmentSlot
    {
        Head,
        Necklace,
        Torso,
        Legs,
        Shoes,
        Gloves,
        Weapon1,
        Weapon2,
    }
}
