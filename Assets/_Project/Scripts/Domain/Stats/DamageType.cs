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
        Arcane
    }
}
