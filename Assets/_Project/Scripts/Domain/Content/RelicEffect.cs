namespace PrincesPalace.Domain.Content
{
    // What a relic actually DOES. An enum dispatch rather than a data-driven
    // effect system on purpose: three relics with three unrelated mechanics
    // (a second attack, a consumed damage shield, an extra turn on a kill)
    // have nothing numeric in common to generalize over, and pretending
    // otherwise would be a fake abstraction over three special cases. Each
    // value is handled by its own block in FightController, the same
    // "content concepts belong in Core, not Domain" reasoning CLAUDE.md
    // already gives for role-based Skill effects.
    //
    // No mirror enum on the Content side (contrast ItemKind/ResolvedItemKind,
    // which mirror each other only because Domain cannot see the
    // Content-assembly type) — this enum has no Content-side twin to begin
    // with, so RelicDefinition references it directly.
    public enum RelicEffect
    {
        // Whenever the character makes a plain Attack, it hits the same
        // target a second time.
        DualWield,

        // After the character casts (either the basic Skill action or a
        // character skill), their next incoming hit is reduced 50%. Refresh-
        // not-stack, same rule StatusEffects.Apply already gives every
        // status — recasting while the shield still stands does not
        // compound it.
        MagicalShield,

        // After the character's action kills an enemy, they get an extra
        // turn (capped — see FightTuning.MaxBloodlustChain).
        Bloodlust,
    }
}
