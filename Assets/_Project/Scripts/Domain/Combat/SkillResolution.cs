using System;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // How much a skill does. Pure arithmetic over CombatantState, split out
    // of the driver so every number a skill produces is unit-testable
    // without a scene, a ScriptableObject or a running fight — the specific
    // complaint AUDIT.md REBUILD 1 makes about combat's rules living inside
    // a 1900-line MonoBehaviour.
    public static class SkillResolution
    {
        // INTEGERS ONLY, deliberately. AUDIT.md #16 already records three
        // different rounding conventions inside one damage pipeline; scaling
        // a skill by a float per point of resource spent would add a fourth
        // and hand the suite a rounding flake to find later.
        //
        // The shape is: a flat part, plus `power` for every point of the
        // signature resource the cast actually spent. That is what makes a
        // resource skill scale with how long you have been hoarding rather
        // than with a level curve.
        // `type`/`axis` are appended last with defaults so every existing
        // call site keeps compiling unchanged — only the two damage call
        // sites in FightController.Actions.cs pass real values; a heal or a
        // mana restore never reads either.
        public static int Amount(
            SkillEffect effect,
            CombatantState actor,
            CombatantState target,
            int power,
            int flatAmount,
            int resourceSpent,
            bool ignoresDefense,
            DamageType type = DamageType.Physical,
            ScalingAxis axis = ScalingAxis.Auto)
        {
            switch (effect)
            {
                case SkillEffect.DamageSingle:
                case SkillEffect.DamageAll:
                    return Damage(actor, target, power, flatAmount, resourceSpent, ignoresDefense, type, axis);

                case SkillEffect.HealSelf:
                case SkillEffect.HealParty:
                case SkillEffect.RestorePartyMana:
                    return Math.Max(0, flatAmount + power * resourceSpent);

                // No number to preview -- what a Summon does is add a
                // combatant, not move a health bar. FightSession.Enemies'
                // PreviewSkill calls this unconditionally for every enemy
                // skill's telegraph, and Roar is the first enemy ability
                // whose effect is neither a damage nor a heal, which is what
                // exposed this case ever being reached at all.
                case SkillEffect.Summon:
                    return 0;

                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect, "SkillResolution has no case for this effect.");
            }
        }

        // Defense is subtracted unless the skill explicitly ignores it.
        //
        // That flag is the single most important number in Shawn's kit and
        // is not a power fantasy: damage is max(1, attack - defense) with no
        // variance, his Attack is 5, and the Throne Colossus has Defense 9.
        // Every ordinary action he has bottoms out on the floor against it —
        // 10 damage into a 950-point pool, i.e. 95 turns of nothing. A
        // defense-ignoring line is the only thing that answers a wall, which
        // is why he gets exactly one and his AOE deliberately does not.
        private static int Damage(CombatantState actor, CombatantState target, int power, int flatAmount, int resourceSpent, bool ignoresDefense,
            DamageType type, ScalingAxis axis)
        {
            // MULTIPLIES ONLY THE ATTACK TERM, not the whole raw figure —
            // `raw` is additive (Attack + flatAmount + power*resourceSpent),
            // and scaling the sum would let the scaling axis silently
            // double-dip with the signature-resource axis, amplifying
            // flatAmount/power right along with Attack when the two are
            // meant to be independent levers.
            var resolvedAxis = axis == ScalingAxis.Auto ? ScalingAxes.For(type) : axis;
            var scalingSet = resolvedAxis switch
            {
                ScalingAxis.Weapon => actor.WeaponScaling,
                ScalingAxis.Spell => actor.SkillScaling,
                _ => ScalingSet.None,
            };

            int scaledAttack = CombatMath.ScaledAttack(actor, scalingSet, 1f);
            int raw = scaledAttack + flatAmount + power * resourceSpent;
            if (!ignoresDefense && target != null)
            {
                // The attacker-aware overload, so Sharp Horns' armour
                // penetration applies to a character SKILL exactly as it does
                // to a plain swing. "Attacks ignore 25% of the target's
                // defense" reads as every attack, and a rule that quietly
                // stopped applying the moment the player pressed a different
                // button would be the kind of inconsistency AUDIT.md #16 is
                // about.
                raw -= CombatMath.EffectiveDefense(target, actor);
            }

            // Floored at 1 and put on the x10 health scale by the same
            // helper ComputeAttackDamage uses, so a skill and a plain attack
            // can never end up on different scales.
            return CombatMath.Scale(raw);
        }

        // How much of the resource a cast should actually consume. A skill
        // flagged spendsAll takes everything the caster has, which is what
        // lets a capstone scale with a whole fight's hoarding instead of a
        // fixed cost.
        public static int ResourceToSpend(SignatureResource resource, int cost, bool spendsAll)
        {
            if (resource == null)
            {
                return 0;
            }

            return spendsAll ? resource.Current : Math.Min(cost, resource.Current);
        }

        // Whether the caster can pay. A spendsAll skill still needs its
        // stated cost as a MINIMUM, so a capstone cannot be cast on an empty
        // gauge for nothing.
        public static bool CanAfford(CombatantState actor, int manaCost, int resourceCost)
        {
            if (actor == null)
            {
                return false;
            }

            if (actor.CurrentMana < manaCost)
            {
                return false;
            }

            if (resourceCost <= 0)
            {
                return true;
            }

            return actor.Signature != null && actor.Signature.CanSpend(resourceCost);
        }
    }
}
