namespace PrincesPalace.Domain.Content
{
    // Lives in Domain, not Core, alongside the other content enums
    // (RelicEffect, DamageType, SpriteFacing). It was the odd one out in
    // PrincesPalace.Content, which became a problem the moment characters
    // moved to characters.json: a Domain-layer resolver cannot reference
    // Core (the reference direction only ever points Editor/Tests -> Core
    // -> Domain), so validating a role name from JSON was impossible until
    // this moved. It is a pure data enum with no engine dependency, so
    // Domain is where it belonged anyway.
    //
    // Role identity is tied to species, so a player can read a squad's
    // composition from the characters themselves (fly = fast/evasive,
    // dog = bulky/controlling). Roles are expressed mechanically through
    // each definition's base StatBlock, not by special-casing the role.
    public enum CharacterRole
    {
        Utility,
        Assassin,
        CrowdControl,
        Tank,
        Support
    }
}
