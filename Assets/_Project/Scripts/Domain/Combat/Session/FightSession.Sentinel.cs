using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Session
{
    // THE SENTINEL'S SESSION HALF (docs/PLAN_BJORN_CONSTELLATIONS.md section 2,
    // "Slice C"): the fight-start arm for the planted shield's switches and the
    // rules the tree still needed that are general mechanics any character
    // could carry, not shield logic.
    //
    //   ArmSentinelSeams        -- engine and every PlantedShield switch
    //                              (called from ArmEngineSeams)
    //   ResolvePlantShield      -- Plant the Shield's cast
    //   ApplyShieldBash /
    //   ApplySkillSplash        -- a skill that spends the planted shield, and
    //                              a skill whose hit spills onto neighbours
    //   CleanseForSkill         -- a buff skill that also strips debuffs
    //   PayHoldTheLine          -- Fury when a buffed ally is hit
    //   PayProvokeFury          -- Fury for each enemy a Provoke took
    //   PreferenceExtras        -- the standing, weighted taunt
    //
    // Every rule is inert for a combatant whose talent set does not carry it,
    // which is every combatant until the Sentinel content is bought.
    public sealed partial class FightSession
    {
        // Only ever turns seams ON, like ArmEngineSeams. The percent seams take
        // the strongest owned figure (Best), so a tier that restates a number
        // never lowers it.
        private static void ArmSentinelSeams(CombatantState actor, TalentEffectSet talents)
        {
            if (talents.Has(TalentEffectType.FuryEngineSentinel))
            {
                actor.FuryEngine.Kind = FuryEngineKind.Sentinel;
            }

            var shield = actor.PlantedShield;

            if (talents.Has(TalentEffectType.PlantedShieldBreakShards)) shield.BreakShards = true;
            if (talents.Has(TalentEffectType.ShieldBashShortWait)) shield.ShortWaitAfterBash = true;
            if (talents.Has(TalentEffectType.ShieldwallCoversParty)) shield.CoversParty = true;
            if (talents.Has(TalentEffectType.SilenceCasterOnSpellHit)) shield.SilenceCasterOnSpellHit = true;
            if (talents.Has(TalentEffectType.ReflectGrantsFury)) shield.ReflectGrantsFury = true;
            if (talents.Has(TalentEffectType.SlowsAttacker)) shield.SlowsAttacker = true;
            if (talents.Has(TalentEffectType.DisarmsOnBreak)) shield.DisarmsOnBreak = true;

            shield.ThornsPercent = Math.Max(shield.ThornsPercent, talents.Best(TalentEffectType.ThornsPercent));
            shield.ReflectMagicPercent = Math.Max(shield.ReflectMagicPercent,
                talents.Best(TalentEffectType.ReflectMagicPercent));
        }

        // ---- Plant the Shield --------------------------------------------------------

        // The cast of SkillEffect.PlantShield. CombatActions.IsLegalFor has
        // already refused it while a shield stands or the wait runs, so
        // PlantShield's own refusal is a backstop, not a path.
        private void ResolvePlantShield(CombatantState actor, ResolvedSkill skill)
        {
            BeginBeat(actor, actor, isCast: true);
            RecordSpellPresentation(skill);

            PlantShield(actor);
        }

        // ---- Shield Bash ---------------------------------------------------------------

        // A skill that spends the caster's planted shield: the placement's
        // absorbed damage, at the row's percent, is added to the raw figure.
        // Consumes the shield (and starts the re-place wait) even when the
        // blow is then dodged: the shield was swung.
        private int ApplyShieldBash(int amount, ResolvedSkill skill, CombatantState actor)
        {
            if (skill.PlantedShieldBashPercent <= 0 || actor == null) return amount;

            int absorbed = BashPlantedShield(actor);
            AppendMessage($"{actor.Name} swings the planted shield ({absorbed} soaked).");
            return amount + PlantedShield.BashBonus(absorbed, skill.PlantedShieldBashPercent);
        }

        // The same figure for the skill card, which must not consume anything.
        private static int ApplyShieldBashPreview(int amount, ResolvedSkill skill, CombatantState actor)
        {
            if (skill.PlantedShieldBashPercent <= 0 || actor == null || !actor.PlantedShield.IsPlaced) return amount;

            return amount + PlantedShield.BashBonus(actor.PlantedShield.AbsorbedThisPlacement,
                skill.PlantedShieldBashPercent);
        }

        // A named skill's landed hit spills a share of what it dealt onto the
        // enemies drawn either side of its target (Shield Bash T3), through the
        // splash Black Ram Mode already uses.
        private void ApplySkillSplash(CombatantState actor, ResolvedSkill skill, CombatantState target, int damage)
        {
            if (damage <= 0 || actor == null) return;

            int percent = actor.Talents.BestFor(TalentEffectType.SkillSplashPercent, skill.Id);
            if (percent <= 0) return;

            SplashOntoNeighbours(actor, target, Math.Max(1, damage * percent / 100),
                $"{skill.DisplayName} hits wider than it looks");
        }

        // ---- Hold the Line -------------------------------------------------------------

        // A buff skill that also strips debuffs from each ally it buffs (Hold
        // the Line T3): the named skill's SkillCleansesDebuffs count, oldest
        // debuff first. A cleansed Chilled hands its speed malus back.
        private void CleanseForSkill(CombatantState caster, ResolvedSkill skill, CombatantState ally)
        {
            if (caster == null || ally == null || !ally.IsAlive) return;

            int count = caster.Talents.BestFor(TalentEffectType.SkillCleansesDebuffs, skill.Id);
            for (int i = 0; i < count; i++)
            {
                var debuff = ally.Statuses.FirstOrDefault(s => StatusEffects.IsDebuff(s.Type));
                if (debuff == null) return;

                ally.Statuses.Remove(debuff);
                if (debuff.Type == StatusEffectType.Chilled) RefreshChilledSpeed(ally);

                AppendMessage($"{ally.Name}'s {debuff.Type} is cleansed.");
            }
        }

        // Called from the one seam where the pools hear a hit: each holder who
        // put a Fortified on this ally is paid PrimaryGainWhenBuffedAllyHit
        // (once per holder however many buffs he cast). Not for a hit on the
        // holder himself, whose own engine already answers it.
        private void PayHoldTheLine(CombatantState target)
        {
            if (target == null || !target.IsPlayerSide || target.Statuses.Count == 0) return;

            List<CombatantState> holders = null;
            foreach (var status in target.Statuses)
            {
                if (status.Type != StatusEffectType.Fortified) continue;

                var holder = status.Source;
                if (holder == null || !holder.IsAlive || ReferenceEquals(holder, target)) continue;

                holders = holders ?? new List<CombatantState>();
                if (!holders.Contains(holder)) holders.Add(holder);
            }

            if (holders == null) return;

            foreach (var holder in holders)
            {
                int gain = holder.Talents.Best(TalentEffectType.PrimaryGainWhenBuffedAllyHit);
                if (gain > 0) GrantPrimary(holder, gain);
            }
        }

        // ---- Bellow --------------------------------------------------------------------

        // Bellow T3: a Provoke cast pays the caster per enemy it provoked.
        private void PayProvokeFury(CombatantState actor, int provoked)
        {
            if (actor == null || provoked <= 0) return;

            int gain = actor.Talents.Best(TalentEffectType.PrimaryGainPerProvokedEnemy) * provoked;
            if (gain <= 0) return;

            GrantPrimary(actor, gain);
            AppendMessage($"{actor.Name} draws {gain} {actor.PrimaryPool?.DisplayName} from the attention.");
        }

        // Bellow T2, the standing taunt: how many extra copies of `member` the
        // enemy target draw holds, TargetPreferencePercent / 100 of them
        // (200 = counted three times in all). 0 for everyone who has not
        // bought it, so an ordinary party draws exactly as it always did.
        private static int PreferenceExtras(CombatantState member) =>
            member == null ? 0 : member.Talents.Best(TalentEffectType.TargetPreferencePercent) / 100;

        private static int PreferenceExtras(IReadOnlyList<CombatantState> members)
        {
            int extras = 0;
            foreach (var member in members) extras += PreferenceExtras(member);
            return extras;
        }

        // The enemy's telegraph-time pick of a party member, drawn as the real
        // intent pass draws it, for a test of the weighting.
        public CombatantState PickIntentTargetForTest(CombatantState enemy, EnemyAbility? ability = null) =>
            PickIntentTarget(enemy, ability);

        // The candidates with each preferred member repeated by his extras.
        private static IReadOnlyList<CombatantState> WeightedByPreference(IReadOnlyList<CombatantState> candidates)
        {
            if (PreferenceExtras(candidates) == 0) return candidates;

            var weighted = new List<CombatantState>(candidates);
            foreach (var member in candidates)
            {
                for (int i = 0; i < PreferenceExtras(member); i++) weighted.Add(member);
            }

            return weighted;
        }
    }
}
