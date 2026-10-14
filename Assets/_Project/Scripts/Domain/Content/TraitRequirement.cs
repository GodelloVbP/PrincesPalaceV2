using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Content
{
    // What a member's kit must hold for a trait to do anything for them
    // (modifiers.json `requires`). A closed list, the generalisation of the
    // relic convergence gate (docs/PLAN_ITEMS.md 2.4).
    public enum TraitRequirement
    {
        // A convergence ability (ConvergenceGate).
        Convergence,

        // A damaging cast: the hooks that fire "on a spell".
        Spell,

        // A Transform skill.
        Transform,
    }

    public static class TraitRequirements
    {
        public static bool Met(TraitRequirement requirement, IEnumerable<SkillEffect> kitEffects)
        {
            if (kitEffects == null) return false;

            foreach (var effect in kitEffects)
            {
                switch (requirement)
                {
                    case TraitRequirement.Convergence:
                        if (Combat.Session.ConvergenceGate.IsConvergenceEffect(effect)) return true;
                        break;
                    case TraitRequirement.Transform:
                        if (effect == SkillEffect.Transform) return true;
                        break;
                    case TraitRequirement.Spell:
                        if (effect == SkillEffect.DamageSingle || effect == SkillEffect.DamageAll ||
                            effect == SkillEffect.Shatter)
                        {
                            return true;
                        }

                        break;
                }
            }

            return false;
        }

        public static bool AllMet(IReadOnlyList<TraitRequirement> requires, IReadOnlyList<SkillEffect> kitEffects)
        {
            if (requires == null) return true;
            foreach (var requirement in requires)
            {
                if (!Met(requirement, kitEffects)) return false;
            }

            return true;
        }

        public static bool TryParse(string name, out TraitRequirement requirement) =>
            Enum.TryParse(name?.Trim(), ignoreCase: true, out requirement) && Enum.IsDefined(typeof(TraitRequirement), requirement);
    }
}
