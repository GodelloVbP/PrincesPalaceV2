using System;
using PrincesPalace.Domain.Stats;


namespace PrincesPalace.Domain.Combat
{
    // Pure combat math: no state of its own, only functions over
    // CombatantState. Kept separate from CombatEncounter so every formula is
    // trivially unit-testable without needing a running fight.
    public static class CombatMath
    {
        // Fallback only — used when no per-character spell tier applies
        // (a non-player CombatantState with no Character behind it; enemies
        // never use Skill today, but FightController.SkillManaCostFor keeps
        // this as an honest fallback rather than assuming that). Real
        // player casters use ContentDatabase.GetSpellTierForLevel's
        // manaCost instead — see Assets/_Project/ContentData/spells.json.
        // Set equal to that file's level-1 manaCost so this constant can't
        // silently drift from what a level-1 character actually pays.
        public const int SkillManaCost = 10;

        // Health pools moved to a x10 scale (a Stone Golem holds 350, not
        // 35). Stat-derived damage has to move with them or an ordinary
        // attack stops meaning anything — 200 attacks could not finish a
        // fight, which is exactly how the regression was caught.
        //
        // This is a multiplier on the FINAL figure rather than a x10 on the
        // authored Attack and Defense numbers, and the difference matters in
        // two places. It keeps a single point of Strength worth a visible
        // amount of damage instead of rounding away, and it scales the
        // max(1, ...) floor along with everything else — x10'ing the stats
        // instead would leave a well-armoured target taking literally 1.
        //
        // Fixed damage authored directly on a skill (a spell's
        // damageInstances) is already written on the new scale and is
        // deliberately NOT routed through here.
        public const int DamageScale = 10;

        // Basic attack: attack minus defense, floored at 1 so defense alone
        // can never make a combatant unhittable.
        //
        // The weapon's SCALING is folded into the attack side, before defense
        // is subtracted, rather than applied to the finished figure. That is
        // the same place a spell's power multiplier goes in ComputeSkillDamage
        // below, and putting it anywhere else breaks armour: multiplying the
        // final number means a 2x weapon doubles the damage that got through,
        // so heavy armour would be worth half as much against exactly the
        // weapons it most needs to blunt.
        public static int ComputeAttackDamage(CombatantState attacker, CombatantState target)
        {
            return Scale(ScaledAttack(attacker, attacker.WeaponScaling, 1f) - EffectiveDefense(target, attacker));
        }

        // The Defense a hit actually has to get through right now.
        //
        // Exactly target.Defense, EXCEPT while a BreakShield is broken — for
        // that one turn's window, armour stops counting entirely. This is the
        // single place that rule lives, so every formula that subtracts
        // Defense (a plain Attack, a Skill, SkillResolution's own damage
        // case) reads THROUGH it rather than each deciding for itself whether
        // to check BreakShield, which is exactly the kind of duplicated
        // judgment call that has already produced a real bug once in this
        // pipeline (AUDIT.md #16, three different rounding conventions in one
        // pipeline because no one function owned the decision).
        //
        // Fixed damageInstances packets never subtracted Defense to begin
        // with (they are "exactly what it says"), so Break's bonus is
        // naturally a non-event there rather than something that needed a
        // special case.
        public static int EffectiveDefense(CombatantState target)
        {
            if (target?.BreakShield != null && target.BreakShield.IsBroken)
            {
                return 0;
            }

            if (target == null)
            {
                return 0;
            }

            // Last Stand T1: armour that only exists once he is hurt. Folded
            // in HERE, in the one function every formula that subtracts
            // Defense reads through, for exactly the reason that function's
            // own header already gives — a bonus applied at the call sites
            // would apply to a plain Attack and silently not to a Skill.
            int bonusPercent = target.Talents.BestBelowHealth(
                TalentEffectType.DefenseBonusPercentBelowHealth, target);

            return bonusPercent <= 0
                ? target.Defense
                : target.Defense + target.Defense * bonusPercent / 100;
        }

        // The same, minus whatever the ATTACKER is authored to ignore —
        // Sharp Horns' armour penetration.
        //
        // A second overload rather than a parameter on the existing one,
        // because the two questions are genuinely different and nearly every
        // caller only has the first. "What armour does this target have right
        // now" is a property of the target (the break window, Last Stand);
        // "how much of it does this particular swing get through" needs both
        // sides. SkillResolution.Damage asks the first and cannot answer the
        // second, so making the attacker a required parameter would have
        // meant handing it a null there and hoping.
        //
        // Penetration applies to the POST-bonus figure: Sharp Horns ignores a
        // fraction of the armour actually in the way, which is the reading
        // that keeps the two strands composing sensibly instead of racing to
        // be applied first.
        public static int EffectiveDefense(CombatantState target, CombatantState attacker)
        {
            int defense = EffectiveDefense(target);
            if (defense <= 0 || attacker == null)
            {
                return defense;
            }

            int ignorePercent = attacker.Talents.Best(TalentEffectType.IgnoreDefensePercent);
            return ignorePercent <= 0 ? defense : defense - defense * ignorePercent / 100;
        }

        // Trample T1: more damage against a target already on its way down.
        //
        // Applied to a FINISHED damage figure rather than to the attack side,
        // unlike weapon scaling — this is a bonus against a weakened target,
        // not a property of the swing, so it should not be blunted by the
        // armour it is being aimed past. That is the same place the Assassin
        // role's own execute bonus already sits.
        public static int ApplyExecuteBonus(int damage, CombatantState attacker, CombatantState target)
        {
            if (damage <= 0 || attacker == null || target == null)
            {
                return damage;
            }

            int bonusPercent = attacker.Talents.BestBelowHealth(
                TalentEffectType.ExecuteDamageBonusPercent, target);

            return bonusPercent <= 0 ? damage : damage + damage * bonusPercent / 100;
        }

        // Last Stand T2: while badly wounded, no single hit may take more
        // than a stated slice of the target's maximum health.
        //
        // Stands in for the handoff's "cannot be critically hit", which has
        // nothing to hook onto in a game with no critical hits — see
        // TalentEffectType.DamageCapPercentBelowHealth' own comment for the
        // full reasoning. The intent survives intact: the spike that ends the
        // run is the thing removed.
        public static int CapSpikeDamage(int damage, CombatantState target)
        {
            if (damage <= 0 || target == null || target.MaxHealth <= 0)
            {
                return damage;
            }

            int capPercent = target.Talents.BestBelowHealth(
                TalentEffectType.DamageCapPercentBelowHealth, target);
            if (capPercent <= 0)
            {
                return damage;
            }

            // Floored at 1 for the same reason every other reducer in this
            // file is: a cap is not immunity, and a very small max health
            // must not round the cap down to zero.
            return Math.Min(damage, Math.Max(1, target.MaxHealth * capPercent / 100));
        }

        // Attack after the wielder's scaling and any action multiplier.
        //
        // One function so a plain swing and a cast round identically —
        // AUDIT.md #16 is about three rounding conventions already coexisting
        // in this pipeline and this is where a fourth would have appeared.
        // Internal rather than private: SkillResolution.Damage reuses this
        // exact rounding for its own scaled-Attack term (Phase 4 of the
        // stat-scaling plan) rather than adding a fourth convention of
        // its own.
        internal static int ScaledAttack(CombatantState attacker, ScalingSet scaling, float actionMultiplier)
        {
            float scaled = scaling.MultiplierFor(attacker.AbilityScores) * actionMultiplier;

            // Everything CombatMath cannot work out for itself — the Lamb's
            // Weight of Wool, and Gift: Fury — arrives pre-summed on the
            // combatant. See CombatantState.BonusAttackPercent for who is
            // allowed to write it.
            //
            // Folded in HERE, on the attack side before defense is
            // subtracted, for exactly the reason ComputeAttackDamage's own
            // header gives about weapon scaling: multiplying the finished
            // figure means a 4x multiplier doubles the damage that GOT
            // THROUGH, so heavy armour would be worth a quarter as much
            // against precisely the swings it most needs to blunt.
            if (attacker.BonusAttackPercent != 0)
            {
                scaled *= 1f + attacker.BonusAttackPercent / 100f;
            }

            return Rounding.AwayFromZero(attacker.Attack * scaled);
        }

        // The one place the floor and the scale are applied, so no formula
        // can pick up one without the other.
        public static int Scale(int rawDifference)
        {
            return Math.Max(1, rawDifference) * DamageScale;
        }

        // Skill: costs mana, hits harder than a plain Attack by
        // powerMultiplier. The multiplier is a required parameter, not a
        // hardcoded constant, specifically so it can scale with the
        // caster's level — see ContentDatabase.GetSpellTierForLevel and
        // FightController.SpellTierFor, which is where a real player
        // caster's multiplier actually comes from. This function itself
        // stays level-agnostic; Domain has no concept of Character or
        // level at all.
        //
        // The caster's SKILL scaling multiplies alongside the tier — a spell
        // rides its own stats (Intelligence for arcane, Wisdom for nature)
        // rather than the sword in the caster's other hand, which is why the
        // two profiles are separate fields on CombatantState.
        public static int ComputeSkillDamage(CombatantState attacker, CombatantState target, float powerMultiplier)
        {
            return Scale(ScaledAttack(attacker, attacker.SkillScaling, powerMultiplier) - EffectiveDefense(target, attacker));
        }

        // Rudimentary weakness/resistance: an attack matching the target's
        // weakness deals double, matching its resistance deals half,
        // anything else is unaffected. Weakness is checked first, so an
        // enemy authored with the same type in both fields (a content
        // mistake, not a valid design) resolves as a weakness rather than
        // silently cancelling out.
        // Symmetric: +50% into a weakness, -50% into a resistance.
        //
        // Weakness was 2x (a full +100%) and is now 1.5x, so the pair finally
        // mirrors. At 2x/0.5x a single matched element swung damage by a
        // factor of four end to end, which is a lot of weight to hang on one
        // authored field — and it matters more now that a spell can carry
        // several elements at once and land on both sides of the chart in the
        // same cast.
        private const float WeaknessMultiplier = 1.5f;
        public const float ResistanceMultiplier = 0.5f;

        // Applies one effectiveness multiplier to one packet of fixed damage.
        //
        // Lives here rather than in the caller so a spell's typed instances
        // and every other scaled figure round the SAME way — see
        // Rounding.AwayFromZero's own header for why away-from-zero rather
        // than UnityEngine.Mathf.RoundToInt's to-even is the rule everywhere
        // now (AUDIT.md #16). The difference is not cosmetic: a 25-point
        // packet into a resistance is exactly 12.5, the tie case.
        //
        // Floored at 1 for the usual reason: resisted is not immune.
        public static int ApplyEffectiveness(int baseDamage, float multiplier)
        {
            return Math.Max(1, Rounding.AwayFromZero(baseDamage * multiplier));
        }

        // Protect/Vulnerable, applied at the same point EffectivenessMultiplier
        // is — one more multiplicative step in the same funnel, not a second
        // one a caller has to remember to add. Floored the same way
        // ApplyEffectiveness is, for the same reason: a buff can reduce
        // damage a great deal, never to zero.
        public static int ApplyStatusEffects(int damage, CombatantState target)
        {
            if (target == null || target.Statuses.Count == 0)
            {
                return damage;
            }

            return Math.Max(1, Rounding.AwayFromZero(damage * StatusEffects.DamageTakenMultiplier(target.Statuses)));
        }

        public static float EffectivenessMultiplier(DamageType attackType, DamageType weakness, DamageType resistance)
        {
            if (attackType == weakness)
            {
                return WeaknessMultiplier;
            }

            if (attackType == resistance)
            {
                return ResistanceMultiplier;
            }

            return 1f;
        }

        // How many points of resistance halve incoming damage.
        //
        // Resistance is deliberately NOT a percentage. Equipment sets stack
        // five pieces, each worth up to ~17 points at the top plus level, so
        // a full set reaches ~85 — as flat percent that is either immunity or
        // a hard cap that makes the last three pieces worthless, and both are
        // bad answers. On this curve 85 points is a 46% reduction, and no
        // amount of stacking ever reaches 100%: every point helps, later
        // points help less, and the number can grow as far as the game wants
        // it to without anyone having to revisit this function.
        //
        // reduction = R / (R + Softener), so R == Softener is exactly half.
        public const int ResistanceSoftener = 100;

        // Applies a resistance value to a damage figure.
        //
        // Integer arithmetic throughout, and floored at 1 for the same reason
        // every other damage path is: no amount of armour makes a combatant
        // unhittable.
        public static int AfterResistance(int damage, int resistance)
        {
            if (damage <= 0)
            {
                return 0;
            }

            if (resistance <= 0)
            {
                return damage;
            }

            long reduced = (long)damage * ResistanceSoftener / (resistance + ResistanceSoftener);
            return (int)Math.Max(1, reduced);
        }

        // Which of a target's two resistances answers a given damage type.
        // Physical is its own thing; everything else — fire, ice, nature,
        // poison, arcane — is magic. One function rather than a flag on
        // DamageType so adding an element cannot forget to say which side it
        // falls on.
        public static bool IsPhysical(DamageType type)
        {
            return type == DamageType.Physical;
        }

        public static int ResistanceAgainst(CombatantState target, DamageType type)
        {
            if (target == null)
            {
                return 0;
            }

            // THE BROAD ANSWER PLUS THE SPECIFIC ONE. Physical-or-magical is
            // what gear rolls and the sheet shows; the typed block is what a
            // cloak worn against fire adds on top of it.
            //
            // Summed rather than taking the larger, because they are different
            // claims: generic warding and a fire cloak both genuinely stand
            // between you and a fire bolt. The softening curve stops that
            // running away -- R/(R+100) means the second hundred is worth much
            // less than the first.
            int broad = IsPhysical(type) ? target.PhysicalResistance : target.MagicalResistance;
            return broad + target.TypedResistance.For(type);
        }

        // Returns how much a signature resource soaked before health was
        // touched, so the caller can say so. Zero for every combatant
        // without one, which is the overwhelming majority.
        //
        // Absorption lives HERE, in the single funnel every damage source
        // already goes through, rather than at the call sites — there are
        // several (player attack, player skill, enemy turn, AOE) and one of
        // them forgetting would make the resource silently stop being armour
        // depending on who hit you.
        public static int ApplyDamage(CombatantState target, int amount)
        {
            return ApplyDamageDetailed(target, amount).Absorbed;
        }

        // What actually happened to a combatant when damage landed.
        //
        // The int-returning overload above is kept as the shape almost every
        // call site wants, and every existing one keeps compiling. This one
        // exists because Last Stand's death save is not visible in the health
        // number afterwards — a combatant left at 1 HP looks exactly like a
        // combatant that was only ever going to be left at 1 HP, so a caller
        // that wants to SAY "he refuses to fall" has no way to know from the
        // outside that anything happened.
        public readonly struct DamageResult
        {
            public readonly int Absorbed;
            public readonly bool CheatedDeath;

            public DamageResult(int absorbed, bool cheatedDeath)
            {
                Absorbed = absorbed;
                CheatedDeath = cheatedDeath;
            }
        }

        public static DamageResult ApplyDamageDetailed(CombatantState target, int amount)
        {
            if (amount <= 0)
            {
                return new DamageResult(0, false);
            }

            // Last Stand T2's spike cap lands here rather than in
            // FightController.AfterDefences, even though that is where every
            // other reduction lives. This is the tighter funnel of the two:
            // AfterDefences has two overloads and is bypassed entirely by a
            // status tick, and a "no hit may exceed X" rule that a poison
            // tick can walk around is not the rule it claims to be.
            amount = CapSpikeDamage(amount, target);

            int absorbed = target.Signature != null ? target.Signature.Absorb(amount) : 0;
            int toHealth = amount - absorbed;

            // Last Stand T3. Checked BEFORE health is written rather than by
            // resurrecting afterwards, so nothing downstream ever observes a
            // dead combatant that is about to be undead — IsAlive is read all
            // over this codebase and a one-statement window where it lies is
            // a bug waiting for a reordering.
            bool cheatedDeath = false;
            if (toHealth >= target.CurrentHealth
                && target.CurrentHealth > 0
                && !target.CheatDeathSpent
                && target.Talents.Has(TalentEffectType.CheatDeathOncePerFight))
            {
                target.CheatDeathSpent = true;
                cheatedDeath = true;
                target.CurrentHealth = 1;
                return new DamageResult(absorbed, true);
            }

            target.CurrentHealth = Math.Max(0, target.CurrentHealth - toHealth);
            return new DamageResult(absorbed, cheatedDeath);
        }

        public static void Heal(CombatantState target, int amount)
        {
            target.CurrentHealth = Math.Min(target.MaxHealth, target.CurrentHealth + amount);
        }

        public static void SpendMana(CombatantState combatant, int amount)
        {
            combatant.CurrentMana = Math.Max(0, combatant.CurrentMana - amount);
        }

        public static void RestoreMana(CombatantState combatant, int amount)
        {
            combatant.CurrentMana = Math.Min(combatant.MaxMana, combatant.CurrentMana + amount);
        }
    }
}
