namespace PrincesPalace.Domain.Combat.Session
{
    // ACQUISITION GATE: whether a relic that wants "a party member has a
    // convergence/ultimate ability" should be offered at all.
    //
    // A CONVERGENCE ABILITY, TODAY, MEANS SkillEffect.Transform -- Shawn's
    // Black Ram Mode is the one implemented example (see SkillEffect.
    // Transform's own header: "Shawn's Black Ram Mode"), and the Fragile
    // Lamb's and the mage's own convergences are documented (Transformation.
    // cs's header) as landing through the same effect the day their strands
    // ship. There is no separate "IsUltimate" flag anywhere in the content
    // vocabulary -- Transform already IS that flag, for the one ability
    // shape a transform-granting skill can be.
    //
    // A PURE PREDICATE OVER AN EFFECT, not over a save or a squad -- Domain
    // cannot see either. RunOrchestrator and TraitRequirement ask it per
    // skill and hand RelicPool.Available the party-level answer.
    public static class ConvergenceGate
    {
        // The single definition of "counts as convergence", exposed so a
        // caller holding raw SkillEffect values (Core's SkillDefinition,
        // rather than Domain's ResolvedSkill -- the active squad in the hub
        // has not been resolved into a fight kit yet) can ask the same
        // question without wrapping its skills first.
        public static bool IsConvergenceEffect(SkillEffect effect) => effect == SkillEffect.Transform;
    }
}
