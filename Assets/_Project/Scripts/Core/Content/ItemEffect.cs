namespace PrincesPalace.Content
{
    // What an ItemDefinition does when consumed in combat. A plain enum
    // rather than per-item behaviour scripting — every item so far is a
    // flat restore-some-resource effect, so a switch in FightController is
    // simpler than an authoring-time behaviour system nobody needs yet.
    public enum ItemEffect
    {
        Heal,
        RestoreMana,
    }
}
