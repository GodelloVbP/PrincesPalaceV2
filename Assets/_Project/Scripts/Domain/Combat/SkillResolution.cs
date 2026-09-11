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
        // sites in FightSession.Skills.cs pass real values; a heal or a
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

                // A HEAL ALSO SCALES WITH THE CASTER'S WISDOM. Split off from
                // the mana restore below, which shares the flat-plus-resource
                // shape but must not: mana is not health, and a mana pool that
                // grew with the caster's HEALTH bar would be nonsense.
                // CombatMath.WisdomHealBonus carries the rate and the reasoning.
                case SkillEffect.HealSelf:
                case SkillEffect.HealParty:
                    return Math.Max(0, flatAmount + power * resourceSpent
                                       + CombatMath.WisdomHealBonus(actor));

                case SkillEffect.RestorePartyMana:
                    return Math.Max(0, flatAmount + power * resourceSpent);

                // No number to preview -- the whole action is redirecting who
                // gets attacked next turn (StatusEffectType.Provoked), not a
                // health bar moving. See SkillEffect.Provoke's own header.
                case SkillEffect.Provoke:
                    return 0;

                // No number to preview -- entering the transform is a state
                // change (stat grants come from the skill's own
                // TransformGrant block, applied by EnterTransform), not
                // damage or healing scored against power/flatAmount.
                case SkillEffect.Transform:
                    return 0;

                // The shield strain a Ward grants IS a number worth showing,
                // but it lives on the caster's WardReductionPercent talent,
                // not on power/flatAmount/resourceSpent -- see
                // SkillEffect.Ward's own header on why every Ward/Shatter
                // number is talent-authored. Mirrors ApplyWard/WardOne
                // (FightSession.Talents.cs) exactly, short of the side
                // effects a preview must not have: same zero-if-unwarded
                // gate, same floor of 1 once it lands.
                case SkillEffect.Ward:
                {
                    int reduction = actor.Talents.Best(TalentEffectType.WardReductionPercent);
                    return reduction <= 0 ? 0 : Math.Max(1, reduction);
                }

                // Previews ONE ward's worth of detonation damage -- how many
                // actually go off depends on how many wards are out, which
                // Amount has no way to see with no side effects. Mirrors the
                // per-wearer share ResolveShatter computes (FightSession.
                // Talents.cs), skipping only the self-ward triple (that
                // depends on which ward is being detonated, a question this
                // signature cannot ask).
                case SkillEffect.Shatter:
                {
                    int percent = actor.Talents.Best(TalentEffectType.ShatterDamagePercentOfAttack);
                    return CombatMath.Scale(actor.Attack * percent / 100);
                }

                // No number to preview -- the Lamb's Wail only applies its
                // authored status to the party, deliberately with no heal
                // amount authored alongside it. See SkillEffect.BuffParty's
                // own header on why that is not HealParty with an omission.
                case SkillEffect.BuffParty:
                    return 0;

                // No number to preview -- each Gift hands an ally a status or
                // a share of the caster's own resource (ResolveGift), not a
                // figure scaled by power/flatAmount/resourceSpent.
                case SkillEffect.GiftMana:
                case SkillEffect.GiftFury:
                case SkillEffect.GiftHaste:
                    return 0;

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

        // Armour no longer applies HERE at all, regardless of `ignoresDefense`
        // — mitigation is a DamagePipeline-only concern now (see its own
        // header for the canonical equation), so this always returns the
        // raw, scaled figure. `target` and `ignoresDefense` are kept as
        // parameters for call-site stability (SkillEntryResolverTests and
        // FightSession.Enemies.cs/Skills.cs already have both in hand at
        // every call site) even though neither is read below any more —
        // `ignoresDefense` in particular still matters, just one layer up:
        // every caller of this method also calls DamagePipeline.
        // AfterDefences with the SAME flag, which is the one place it is now
        // actually consulted.
        //
        // WHAT THE FLAG USED TO ANSWER is worth keeping because it describes
        // a bug rather than a design:
        //
        //   "damage is max(1, attack - defense) with no variance, his Attack
        //    is 5, and the Throne Colossus has Defense 9. Every ordinary
        //    action he has bottoms out on the floor against it -- 10 damage
        //    into a 950-point pool, i.e. 95 turns of nothing. A
        //    defense-ignoring line is the only thing that answers a wall."
        //
        // That described the subtraction cliff the diminishing-returns curve
        // (now DamagePipeline.AfterDefences) already removed once, long
        // before this rename -- there is no wall any more, so the flag is
        // what it always read as: a strong property on one line of Shawn's
        // kit, not the difference between playing and not. He still gets
        // exactly one, and his AOE still deliberately does not.
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

            // FLOORED AT 1, NOT SCALED — this is a wool/authored-skill cast
            // (DamageSingle/DamageAll with no fixed damageInstances), and it
            // flows into the exact same DamagePipeline.AfterDefences funnel
            // ComputeAttackDamage/ComputeSkillDamage do (FightSession.
            // Skills.cs's ResolveDamageSingle/ResolveDamageAll, both real
            // production casts, not just PreviewSkillPower). Scaling it by
            // CombatMath.DamageScale while the other two entry points do not
            // would leave every wool skill hitting 5x harder than a plain
            // swing of the same nominal size -- exactly the bug D1 removed
            // from ComputeAttackDamage/ComputeSkillDamage, just one call site
            // over. See CombatMath.ComputeAttackDamage's own header.
            return Math.Max(1, raw);
        }

        // How much of the resource a cast should actually consume. A skill
        // flagged spendsAll takes everything the caster has, which is what
        // lets a capstone scale with a whole fight's hoarding instead of a
        // fixed cost.
        public static int ResourceToSpend(ResourcePool resource, int cost, bool spendsAll)
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
        // `manaCost` MEANS "SPENT FROM THE PRIMARY POOL" -- only the field
        // name is still mana-specific, and phase F renames it (31 authored
        // rows). A skill authored at 6 therefore costs 6 of whatever its
        // caster's pool is, with nothing re-authored: the alternative, a
        // costs-by-id map, re-authors those rows, loops this one rule, and
        // invents a book costing a pool its buyer does not have. See the
        // plan's P5.
        //
        // PRICED BEFORE DISCOUNTS, DELIBERATELY. `manaCost` here is the
        // AUTHORED cost, and Runic's PendingManaDiscountPercent is applied
        // afterwards by FightSession.ChargeSkillMana -- so a wearer with 3
        // mana, a 6-cost skill and a 50% discount armed sees the row greyed
        // for a cast the game would in fact have charged 3 for. That is one-
        // sided in the player's disfavour and it is the decision, not an
        // oversight: ChargeSkillMana's own header states it ("Conservative
        // rather than wrong: the discount is a bonus on a cast the player
        // could already pay for, not a new way to afford one they could not").
        // Cross-referenced from here because a reader arriving at
        // affordability from this side had no way to find that sentence, and
        // an unexplained mismatch between what the menu refuses and what the
        // charge takes reads as a bug every time it is rediscovered.
        public static bool CanAfford(CombatantState actor, int manaCost, int resourceCost)
        {
            if (actor == null)
            {
                return false;
            }

            if (actor.PrimaryPool == null || !actor.PrimaryPool.CanSpend(manaCost))
            {
                return false;
            }

            if (resourceCost <= 0)
            {
                return true;
            }

            return actor.SignaturePool != null && actor.SignaturePool.CanSpend(resourceCost);
        }
    }
}
