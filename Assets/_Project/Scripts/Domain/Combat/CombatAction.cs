namespace PrincesPalace.Domain.Combat
{
    // The choices on the combat menu. Item and Run don't need a target
    // (Item always affects the acting combatant; Run just leaves the fight),
    // so only Attack and Skill are ever passed around as a "pending targeted
    // action" waiting on a target selection. Default needs no target either —
    // it resolves against nobody, it just ends the turn and banks a point for
    // a later Brave.
    public enum CombatAction
    {
        Attack,
        Skill,
        Item,
        Run,
        Default
    }
}
