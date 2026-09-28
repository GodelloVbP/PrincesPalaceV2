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
            ScalingAxis axis = ScalingAxis.Auto,
            // APPENDED LAST, same convention as type/axis above: percent of
            // the CASTER'S own max health per point of the pool actually
            // spent, read only by the three heals. Bjorn's Second Wind is 1,
            // so a hundred Fury is a full heal whatever his bar has grown to
            // -- a flat `power` tracks that at exactly one point on the
            // health curve and drifts everywhere else.
            int percentOfMaxHealthPerPoint = 0,
            // APPENDED LAST AGAIN, and deliberately NOT a widening of the
            // parameter above it: this is a FLAT percent of the caster's own
            // max health, read only by Ward, where the one beside it is paid
            // per point of the pool spent and read only by the heals. One
            // field meaning both would have to be told apart by the effect,
            // which is a third meaning for a number that already has two
            // homes' worth of documentation.
            int percentOfCasterMaxHealth = 0)
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
                                       + PercentOfMaxHealth(actor, percentOfMaxHealthPerPoint, resourceSpent)
                                       + CombatMath.WisdomHealBonus(actor));

                // THE ONE HEAL WITH AN ATTACK TERM. Mend is authored as "20
                // plus her spell attack", so it adds the same
                // CombatMath.ScaledAttack on the same axis that a spell's
                // DAMAGE rides -- one function, one axis resolution, no
                // second answer to "what is her spell attack". The other two
                // heals are unchanged above: a heal that grew with Attack
                // would be a silent buff to Woolgathering and to every
                // monster's self-heal.
                //
                // AUTHORED AXIS ONLY. `Auto` derives the axis from the
                // caster's own attackType, which is right for damage (a
                // Nature caster's hit IS Nature) and meaningless for a heal
                // -- a heal has no element. So a HealSingle that says
                // nothing scales with nothing, and Mend says "Spell".
                case SkillEffect.HealSingle:
                    return Math.Max(0, AuthoredAttackTerm(actor, axis) + flatAmount + power * resourceSpent
                                       + PercentOfMaxHealth(actor, percentOfMaxHealthPerPoint, resourceSpent)
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

                // How many SHIELD POINTS a Ward puts up. Mirrors ApplyWard/
                // WardOne (FightSession.Talents.cs) exactly, short of the side
                // effects a preview must not have: same terms, same floor of
                // 1 once it lands.
                case SkillEffect.Ward:
                {
                    // WHAT A WARD IS WORTH, in ONE place, read by the
                    // preview (this) and by the cast (FightSession's
                    // ApplyWard, which calls straight through here).
                    //
                    // SHIELD POINTS, not a percentage (AUDIT #152, owner
                    // 2026-09-16: a ward IS a shield). The number is built
                    // from exactly the flat + per-resource-power + scaled-
                    // attack terms every damage and heal effect above already
                    // shares, plus the one term a ward alone wants -- a
                    // percent of the CASTER'S own max health, which is how
                    // Bulwark stays worth something on a Bjorn whose bar has
                    // grown. It rides percentOfCasterMaxHealth and NOT the
                    // heals' percentOfMaxHealthPerPoint beside it: that one
                    // is paid per point of the pool spent, and Bulwark's cost
                    // is fifty Fury off the primary pool, which is not a
                    // number a ward should get bigger with.
                    //
                    // THE TALENT IS A MULTIPLIER NOW, not a rival figure.
                    // WardReductionPercent used to BE the ward ("the next hit
                    // is 40% softer") and the larger of talent-or-authored
                    // won; under a pool it reads as "+40% shield amount" and
                    // scales whatever the skill authored. That is what lets
                    // Fleece Ward and Thicker Fleece deepen Tuck In and
                    // Bulwark alike instead of being a floor that the authored
                    // wards quietly stepped over.
                    int authored = AuthoredAttackTerm(actor, axis) + flatAmount + power * resourceSpent
                                   + PercentOfCasterMaxHealth(actor, percentOfCasterMaxHealth);
                    if (authored <= 0) return 0;

                    int bonusPercent = actor.Talents.Best(TalentEffectType.WardReductionPercent);
                    return Math.Max(1, authored + authored * bonusPercent / 100);
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

                // No number to preview -- Ashen Reckoning's whole damage is a
                // TARGET's consumed Poison marked up (StatusCombos.
                // SpendPoisonIfMatched, ConsumedTotalSplit), not a figure
                // built from power/flatAmount/resourceSpent. This signature
                // carries no consumable status to read, so it cannot honestly
                // answer anything but zero; the skill-detail card's POWER row
                // is one of the readers this milestone did not extend to ask
                // the target-aware question instead (see PLAN_SPELL_EXPANSION
                // milestone B's status line).
                case SkillEffect.Reclaim:
                    return 0;

                // No number to preview, and unlike Reclaim above there is no
                // honest number these could grow later either: what Borrowed
                // Moment and Palace Passage deliver is measured in PLACES and
                // in field positions, not in points of anything this function
                // returns. The detail card says what they do in words (its
                // EFFECT verb and the authored description); a zero here is
                // the same "nothing to preview" answer Provoke and Transform
                // already give, not a gap.
                case SkillEffect.Hasten:
                case SkillEffect.SwapAllies:
                    return 0;

                // AN AFFLICT'S WHOLE PAYLOAD IS ITS STATUS, and a status's
                // strength is its own authored magnitude and duration, neither
                // of which is a quantity this function returns (it answers in
                // points of damage, healing, shield or mana). Velvet Shackles
                // delivers two turns, not a number -- the detail card says so
                // in words, the same way Provoke's does.
                case SkillEffect.Afflict:
                case SkillEffect.Enthrall:
                    return 0;

                // A seat, not a quantity -- the same answer Hasten and
                // SwapAllies give above. The enemy telegraph asks this for
                // every skill (PreviewSkill), so the answer must exist.
                case SkillEffect.Reposition:
                    return 0;

                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect, "SkillResolution has no case for this effect.");
            }
        }

        // INTEGER DIVISION, ONCE, at the end -- 1% of 260 per point over 40
        // points is 104, not 2 x 40. Rounding per point would throw away the
        // fraction forty times over and make the authored "at 100 Fury, a
        // full heal" quietly false. Same integers-only discipline Amount's
        // own header states for `power`.
        private static int PercentOfMaxHealth(CombatantState actor, int percentPerPoint, int pointsSpent)
        {
            if (actor == null || percentPerPoint <= 0 || pointsSpent <= 0) return 0;

            return actor.MaxHealth * percentPerPoint * pointsSpent / 100;
        }

        // The flat twin of the above, for a ward whose size is a share of the
        // caster's own bar rather than of what the cast spent. Floored at 1
        // once it is authored at all, the same way every other ward term is:
        // a Bulwark on a character with a tiny health bar is a small shield,
        // never no shield.
        private static int PercentOfCasterMaxHealth(CombatantState actor, int percent)
        {
            if (actor == null || percent <= 0 || actor.MaxHealth <= 0) return 0;

            return Math.Max(1, actor.MaxHealth * percent / 100);
        }

        // THE CASTER'S SCALED ATTACK, but only when the skill EXPLICITLY
        // names an axis to ride.
        //
        // `Auto` (the default every skill that says nothing resolves to)
        // means "derive the axis from the caster's own attackType", which
        // only makes sense for damage -- a heal and a ward have no element
        // for an attackType to agree with. So Auto and None both contribute
        // nothing here, and a skill that wants Odette's spell attack in its
        // figure says `"scalingAxis": "Spell"` and gets exactly the
        // multiplier her spell damage gets.
        private static int AuthoredAttackTerm(CombatantState actor, ScalingAxis axis)
        {
            if (actor == null) return 0;

            if (axis != ScalingAxis.Weapon && axis != ScalingAxis.Spell) return 0;

            return CombatMath.ScaledAttack(actor, ScalingSetFor(actor, axis), 1f);
        }

        // THE axis-to-ScalingSet lookup, shared by AuthoredAttackTerm (which
        // axis a skill explicitly names) and Damage (which axis Auto
        // resolves to) so there is exactly one place that knows Weapon
        // means WeaponScaling and Spell means SkillScaling.
        private static ScalingSet ScalingSetFor(CombatantState actor, ScalingAxis axis)
        {
            return axis switch
            {
                ScalingAxis.Weapon => actor.WeaponScaling,
                ScalingAxis.Spell => actor.SkillScaling,
                _ => ScalingSet.None,
            };
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
            var scalingSet = ScalingSetFor(actor, resolvedAxis);

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
        //
        // `cap` (progression v2 phase 4) puts a CEILING on that "everything"
        // -- Shawn's Tuck In spends up to four banked Wool and no more, so
        // holding twelve does not make it three times the ward. 0, the
        // default, is no ceiling, which is every spendsAll skill authored
        // before this existed. The stated cost is still the MINIMUM either
        // way (see CanAfford), so a capped spendsAll skill has a floor and a
        // ceiling and scales between them.
        public static int ResourceToSpend(ResourcePool resource, int cost, bool spendsAll, int cap = 0)
        {
            if (resource == null)
            {
                return 0;
            }

            if (!spendsAll) return Math.Min(cost, resource.Current);

            int available = resource.Current;
            return cap > 0 ? Math.Min(cap, available) : available;
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
        // healthCostPercent JOINS MANA AND SIGNATURE IN THIS ONE CALL (plan
        // 1.1/1.2): a cast that can pay one cost and not another must spend
        // NEITHER, and the only way to guarantee that is to validate every
        // cost before any of them is charged. Defaults to 0, which is every
        // skill authored before Blackglass Spear and is a no-op check
        // (HealthCost.CanPay is trivially true for a non-positive percent).
        public static bool CanAfford(CombatantState actor, int manaCost, int resourceCost, int healthCostPercent = 0)
        {
            if (actor == null)
            {
                return false;
            }

            if (actor.PrimaryPool == null || !actor.PrimaryPool.CanSpend(manaCost))
            {
                return false;
            }

            if (!HealthCost.CanPay(actor, healthCostPercent))
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
