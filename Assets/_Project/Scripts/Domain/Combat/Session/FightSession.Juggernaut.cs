using System;
using System.Collections.Generic;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // THE JUGGERNAUT'S SESSION HALF (docs/PLAN_BJORN_CONSTELLATIONS.md section
    // 4, "Slice B"): the fight-start arm for the Phase 4 seams the tree
    // switches on, and the rules that have no seam of their own because they
    // are general mechanics any character could carry.
    //
    //   ArmJuggernautSeams      -- engine, cooldown override, Unyielding,
    //                              Ignore Pain, Blood Price (called from
    //                              ArmEngineSeams)
    //   WrathBonusPercent       -- damage per point of own missing health,
    //                              read by AttackBonusFor
    //   TickHealthCurveRegen    -- Thick Blood's regen, at the turn-start tick
    //   ApplySkillHealthBonus / SkillLifestealFor / ApplySkillLifesteal --
    //                              a named skill's health-gated bonus and its
    //                              heal-from-damage
    //   ResolveUnbroken / ResolveCursedBlood -- the two window skills
    //
    // Every rule is inert for a combatant whose talent set does not carry it,
    // which is every combatant until the Juggernaut content is bought.
    public sealed partial class FightSession
    {
        // Thick Blood's regen, Wrath and Gorge read live health, so none of
        // them is switched on at fight start: their talents are simply looked
        // up where they apply. Only the rules that are state on a Domain seam
        // are armed here, and only ever ON (a seam a test set by hand stays).
        private static void ArmJuggernautSeams(CombatantState actor, TalentEffectSet talents)
        {
            if (talents.Has(TalentEffectType.FuryEngineJuggernaut))
            {
                actor.FuryEngine.Kind = FuryEngineKind.Juggernaut;
            }

            foreach (var effect in talents.All)
            {
                if (effect.Type != TalentEffectType.SkillCooldownTurns) continue;
                if (string.IsNullOrEmpty(effect.SkillId) || effect.Magnitude <= 0) continue;

                actor.CooldownOverrides[effect.SkillId] = effect.Magnitude;
            }

            // Unyielding's three nodes nest, so the tier is the whole rule:
            // T1 is the passive on a 4-turn cooldown, T2 shortens it to 3, T3
            // adds 20 Fury to the trigger.
            int unyielding = talents.Best(TalentEffectType.UnyieldingTier);
            if (unyielding >= 1 && actor.CrowdControl.Unyielding == null)
            {
                actor.CrowdControl.Unyielding = new UnyieldingRule(
                    cooldownTurns: unyielding >= 2 ? 3 : 4,
                    furyGain: unyielding >= 3 ? 20 : 0);
            }

            int deferred = talents.Best(TalentEffectType.DelayedDamagePercent);
            if (deferred > 0 && actor.DelayedDamage == null)
            {
                actor.DelayedDamage = new DelayedDamagePool(deferred,
                    talents.Threshold(TalentEffectType.DelayedDamagePercent),
                    healsReducePool: talents.Has(TalentEffectType.HealReducesDelayedDamage));
            }

            int price = talents.Best(TalentEffectType.ShortfallPaidInHealthPermille);
            if (price > 0) actor.ShortfallHealthPermille = price;
        }

        // ---- Wrath -------------------------------------------------------------------

        // WRATH: `DamagePerMissingHealth` hundredths of a percent of damage
        // for each whole 1% of the holder's own max health that is missing.
        // Whole percent of missing health and a floored result, in integers:
        // 50 at 75% missing is 37, 100 at 75% missing is 75. Added to the
        // attack bonus, so it scales the attack term the same way a ward
        // bonus does and the preview reads the same figure the swing does.
        private static int WrathBonusPercent(CombatantState actor)
        {
            if (actor == null || actor.MaxHealth <= 0) return 0;

            int rate = actor.Talents.Best(TalentEffectType.DamagePerMissingHealth);
            if (rate <= 0) return 0;

            long missing = actor.MaxHealth - Math.Max(0, Math.Min(actor.MaxHealth, actor.CurrentHealth));
            long missingPercent = missing * 100 / actor.MaxHealth;
            return (int)(missingPercent * rate / 100);
        }

        // ---- Thick Blood's regen -------------------------------------------------------

        // A heal at the start of the holder's turn on HealthCurveRegen's curve,
        // through the heal funnel (trigger-free, like the Regen status: the
        // crown never heard a Regen tick) so Cursed Blood converts it and
        // Ignore Pain T3 pays down with it. Its own beat, the Regen status'
        // shape, opened before the heal lands so a converted tick's lines sit
        // on it. Nothing at full health: there is nothing to heal and no
        // reason to draw a beat.
        private void TickHealthCurveRegen(CombatantState actor, Dictionary<CombatantState, Vitals> preTick)
        {
            var talents = actor?.Talents;
            if (talents == null || !actor.IsAlive || actor.CurrentHealth >= actor.MaxHealth) return;

            int ceiling = talents.Best(TalentEffectType.HealthCurveRegen);
            if (ceiling <= 0) return;

            int amount = HealthCurveRegen.AmountFor(actor.CurrentHealth, actor.MaxHealth,
                talents.Threshold(TalentEffectType.HealthCurveRegen), ceiling);

            bool ownsBeat = OpenStatusTickBeat(actor, preTick, StatusEffectType.Regen, isHealing: true);
            int healed = HealWithoutTriggers(actor, amount);

            if (healed > 0)
            {
                if (ownsBeat) RecordBeatAmount(healed, isHealing: true);
                AppendMessage($"{actor.Name}'s thick blood mends {healed} health.");
            }

            if (ownsBeat) CommitOrDropStatusTickBeat();
        }

        // ---- skill-scoped rules (Gorge) -------------------------------------------------

        // GORGE T3: the named skill hits harder while its caster is at or below
        // the node's share of his own max health. On the raw figure, before
        // defences, like every other damage multiplier a skill carries.
        private static int ApplySkillHealthBonus(int amount, ResolvedSkill skill, CombatantState actor)
        {
            if (amount <= 0 || skill == null || actor == null) return amount;

            int percent = actor.Talents.BestForBelowHealth(
                TalentEffectType.SkillDamageBonusBelowOwnHealth, skill.Id, actor);
            return percent <= 0 ? amount : (int)((long)amount * (100 + percent) / 100);
        }

        // The share of its damage a skill heals its caster for: the row's own
        // lifestealPercent, or a talent's raised figure for that skill when it
        // is higher (Gorge T2's 40 over the row's 30).
        private static int SkillLifestealFor(CombatantState actor, ResolvedSkill skill)
        {
            if (skill == null || actor == null) return 0;

            return Math.Max(skill.LifestealPercent,
                actor.Talents.BestFor(TalentEffectType.SkillLifestealPercent, skill.Id));
        }

        // Heals the caster for a share of what the skill just dealt, through the
        // heal funnel like every other lifesteal. `damage` is the landed
        // figure, the same one an item's Vampiric reads.
        private void ApplySkillLifesteal(CombatantState actor, ResolvedSkill skill, int damage)
        {
            if (damage <= 0) return;

            int percent = SkillLifestealFor(actor, skill);
            if (percent <= 0) return;

            int healed = Rounding.AwayFromZero(damage * percent / 100f);
            if (healed <= 0) return;

            bool converts = actor.HealConversion.IsActive;
            int landed = HealWithoutTriggers(actor, healed);
            if (!converts && landed > 0) AppendMessage($"{actor.Name} drinks {landed} health from the blow.");
        }

        // ---- the two window skills -------------------------------------------------------

        // UNBROKEN: a Regen of regenPercentOfMaxHealth per turn and Unstoppable
        // (Phase 4e), both for windowTurns of his own turns. The Regen is the
        // ordinary status, so Cursed Blood converts each tick like any heal.
        // Floored, and never below 1.
        private void ResolveUnbroken(CombatantState actor, ResolvedSkill skill)
        {
            BeginBeat(actor, actor, isCast: true);
            RecordSpellPresentation(skill);

            int perTurn = Math.Max(1, actor.MaxHealth * skill.RegenPercentOfMaxHealth / 100);
            if (ApplyStatusTo(actor, StatusEffectType.Regen, perTurn, skill.WindowTurns, actor))
            {
                AppendMessage($"{actor.Name} gains Regen!");
            }

            OpenUnstoppable(actor, skill.WindowTurns);
            AppendMessage($"{actor.Name} stands unbroken - nothing will hold him.");
        }

        // CURSED BLOOD: opens the heal-conversion window (Phase 4b).
        private void ResolveCursedBlood(CombatantState actor, ResolvedSkill skill)
        {
            BeginBeat(actor, actor, isCast: true);
            RecordSpellPresentation(skill);

            OpenCursedBlood(actor, skill.WindowTurns);
        }
    }
}
