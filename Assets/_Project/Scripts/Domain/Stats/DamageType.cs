namespace PrincesPalace.Domain.Stats
{
    // A rudimentary elemental typing for the weakness/resistance system: one
    // type per attack, and an enemy can be weak to one type and resistant to
    // another. Deliberately flat (no type-vs-type chart, no stacking) —
    // "does this specific enemy take extra/less from this specific attack"
    // is the whole rule.
    public enum DamageType
    {
        Physical,
        Fire,
        Ice,
        Nature,
        Poison,
        Arcane,

        // APPENDED, never inserted -- ContentBuilder-generated
        // ScriptableObjects under Resources/Content/ store this enum as a
        // raw int, so reordering (or inserting) an existing member would
        // silently relabel every already-authored weakness/resistance and
        // damageInstance on the next load. New members only ever go on
        // the end, in whatever order they were added.
        Earth,
        Water,
        Wind,
        Lightning,
        Void
    }
}
