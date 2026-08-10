using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // Which axis a hit's damage rides: the wielder's WEAPON (basic-Attack
    // scaling) or their SPELL (spell tier + spellScaling). Auto derives it
    // from the damage TYPE — Physical rides the weapon, everything else
    // rides the spell — which is right for the overwhelming majority of
    // skills without a word of content needed per skill.
    //
    // A skill's own flavour does not always match what its damage type
    // would derive, though — a headbutt authored as the caster's own
    // (magical) attack type is still, narratively, a physical attack — so
    // Weapon/Spell exist as an explicit per-skill override, and None for a
    // skill that should ride neither (a status-only effect, say).
    public enum ScalingAxis
    {
        Auto,
        Weapon,
        Spell,
        None,
    }

    public static class ScalingAxes
    {
        public static ScalingAxis For(DamageType type)
        {
            return CombatMath.IsPhysical(type) ? ScalingAxis.Weapon : ScalingAxis.Spell;
        }
    }
}
