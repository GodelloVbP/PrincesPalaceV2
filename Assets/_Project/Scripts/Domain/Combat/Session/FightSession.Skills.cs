using System.Collections.Generic;
using System.Linq;
using System.Text;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // Everything the player can do that is not a plain swing: the generic
    // Skill action, and the fourteen authored skill effects.
    //
    // Ported from v1's FightController.Actions.cs. Every number and every
    // effect comes from the resolved skill, so adding a skill stays a line in
    // skills.json rather than a const, two dictionary entries, a switch case
    // and a Resolve method.
    public sealed partial class FightSession
    {
        // ---- the generic Skill action ----------------------------------------

        // The Skill verb: one cast, one target, role rider on top. Distinct
        // from CastSkill below, which runs an AUTHORED skill off the
        // character's own strip.
        public void ExecuteSkill(CombatantState target)
        {
            var actor = Current;
            if (actor == null || target == null) return;

            _actionCanBrave = true;
            BeginBeat(actor, target);

            // Weight of Wool counts warded party members and Gift: Fury is
            // spent by the swing, and CombatMath can see neither from inside
            // its own scaling. Summed onto the actor here, immediately before
            // the figure is computed, which is the only arrangement that
            // cannot go stale.
            RefreshAttackBonus(actor, spendingGift: true);

            CombatMath.SpendMana(actor, SkillManaCostFor(actor));

            var outcome = DamagePipeline.AfterDefences(
                CombatMath.ComputeSkillDamage(actor, target, SkillPowerMultiplierFor(actor)),
                actor, target,
                attackType: ActorAttackType(actor),
                weakness: SourceFor(target)?.Weakness,
                resistance: SourceFor(target)?.Resistance,
                varianceRange: DamageVarianceRange,
                rng: _rng,
                resolveWard: ResolveWard);

            DepleteBreakShield(target, outcome.Effectiveness);

            var role = ActorRole(actor);
            bool isExecute = role == CharacterRole.Assassin
                             && IsBelowHealthFraction(target, FightTuning.AssassinExecuteHealthFraction);

            int damage = isExecute
                ? Rounding.AwayFromZero(outcome.Damage * FightTuning.AssassinExecuteBonusMultiplier)
                : outcome.Damage;

            AppendMessage($"{actor.Name} casts {SkillDisplayNameFor(actor)} on {target.Name} for {damage} damage!{EffectivenessSuffix(outcome.Effectiveness)}");
            ApplySkillRoleEffect(role, actor, target, damage, isExecute);
            ApplyFinalDamage(actor, target, damage);

            // The actor poses too, not just the victim -- the player can see
            // which of their own actions actually went off.
            SetStance(actor, Stances.Cast);

            // Magical Shield rises after CASTING specifically, never after a
            // plain attack. Apply's refresh-not-stack rule is the whole of the
            // "does not stack" requirement.
            RaiseMagicalShield(actor);

            CommitBeat();
            AdvanceAfterAction();
        }

        // ---- authored character skills ---------------------------------------

        // Casts the actor's Nth authored skill. Returns false when the cast was
        // refused outright, so the caller knows the turn was not spent.
        public bool CastSkill(int index, CombatantState target)
        {
            var actor = Current;
            var kit = KitFor(actor);
            if (kit == null || index < 0 || index >= kit.Skills.Count) return false;

            return CastSkill(kit.Skills[index], target);
        }

        public bool CastSkill(ResolvedSkill skill, CombatantState target)
        {
            var actor = Current;
            if (actor == null) return false;

            if (!SkillResolution.CanAfford(actor, skill.ManaCost, skill.ResourceCost))
            {
                AppendMessage($"{actor.Name} cannot pay for {skill.DisplayName}.");
                return false;
            }

            // Refused BEFORE anything is paid. A Shatter with nothing to
            // detonate and a Gift with nobody to give it to are both
            // conditional on board state the player can misread, and eating the
            // resource AND the turn for a cast that visibly did nothing is the
            // worst possible answer.
            if (!CanResolveSkill(actor, skill, out string refusal))
            {
                AppendMessage(refusal);
                return false;
            }

            _actionCanBrave = true;

            int resourceSpent = SkillResolution.ResourceToSpend(actor.Signature, skill.ResourceCost, skill.SpendsAllResource);
            CombatMath.SpendMana(actor, skill.ManaCost);
            actor.Signature?.TrySpend(resourceSpent);

            // Only a cast that can actually deal damage spends Gift: Fury. A
            // ward burning somebody else's gift would be a present the player
            // never got to open.
            RefreshAttackBonus(actor, spendingGift: DealsDamage(skill));

            ResolveCharacterSkill(actor, skill, target, resourceSpent);

            // SOURCED by the actor. The relic's shield and the Lamb's Ward are
            // the same status, and an unsourced one would be a ward whose
            // caster nobody can name -- fine for the reduction, wrong for
            // everything the Lamb hangs off it. This does mean a relic shield
            // on a Lamb pays her engine; that reads correctly (she warded
            // herself, by another route) and is worth saying out loud rather
            // than discovering.
            RaiseMagicalShield(actor);

            // Fleece Ward T3: warding stops costing the turn. The single
            // biggest quality-of-life node in the Lamb's path -- before it,
            // every ward is a turn not spent doing anything else, which on a
            // frail character with no damage output feels like being punished
            // for playing her correctly.
            //
            // The beat is still committed and played, or the ward lands with no
            // animation at all. Only the TURN does not advance.
            if (skill.Effect == SkillEffect.Ward && actor.Talents.Has(TalentEffectType.WardIsFreeAction))
            {
                CommitBeat();
                return true;
            }

            CommitBeat();
            AdvanceAfterAction();
            return true;
        }

        // Whether a cast can put a number on an enemy. The question Gift: Fury
        // asks before letting itself be spent.
        private static bool DealsDamage(ResolvedSkill skill) =>
            skill.Effect == SkillEffect.DamageSingle
            || skill.Effect == SkillEffect.DamageAll
            || skill.Effect == SkillEffect.Shatter;

        // Every skill effect, resolved from data. Damage goes through
        // CombatMath like everything else, so a target with its own signature
        // resource soaks it exactly as it would any other hit.
        private void ResolveCharacterSkill(CombatantState actor, ResolvedSkill skill, CombatantState target, int resourceSpent)
        {
            switch (skill.Effect)
            {
                case SkillEffect.DamageSingle:
                    ResolveDamageSingle(actor, skill, target, resourceSpent);
                    break;

                case SkillEffect.DamageAll:
                    ResolveDamageAll(actor, skill, resourceSpent);
                    break;

                case SkillEffect.HealSelf:
                {
                    BeginBeat(actor, actor, isCast: true);
                    RecordSpellPresentation(skill);
                    int amount = SkillResolution.Amount(skill.Effect, actor, actor, skill.Power, skill.FlatAmount, resourceSpent, false);
                    HealAndCount(actor, amount);
                    RecordBeatAmount(amount, isHealing: true);
                    AppendMessage($"{actor.Name} uses {skill.DisplayName} and recovers {amount} HP.");
                    ApplySkillStatus(skill, actor, actor);
                    break;
                }

                case SkillEffect.HealParty:
                {
                    BeginBeat(actor, actor, isCast: true);
                    RecordSpellPresentation(skill);
                    int amount = SkillResolution.Amount(skill.Effect, actor, actor, skill.Power, skill.FlatAmount, resourceSpent, false);
                    foreach (var ally in _encounter.LivingPlayerParty.ToList())
                    {
                        HealAndCount(ally, amount);
                        ApplySkillStatus(skill, ally, actor);
                    }

                    RecordBeatAmount(amount, isHealing: true);
                    AppendMessage($"{actor.Name}'s {skill.DisplayName} mends the squad for {amount}.");
                    break;
                }

                case SkillEffect.RestorePartyMana:
                {
                    BeginBeat(actor, actor, isCast: true);
                    RecordSpellPresentation(skill);
                    int amount = SkillResolution.Amount(skill.Effect, actor, actor, skill.Power, skill.FlatAmount, resourceSpent, false);
                    foreach (var ally in _encounter.LivingPlayerParty.ToList())
                    {
                        CombatMath.RestoreMana(ally, amount);
                    }

                    AppendMessage($"{actor.Name}'s {skill.DisplayName} restores the squad's mana!");
                    break;
                }

                case SkillEffect.Provoke:
                {
                    BeginBeat(actor, target ?? _encounter.LivingEnemies.FirstOrDefault(), isCast: true);
                    RecordSpellPresentation(skill);
                    SetStance(actor, Stances.Cast);

                    int provoked = ApplyProvoke(actor, target);
                    AppendMessage(provoked == 0
                        ? $"{actor.Name} bellows at nothing in particular."
                        : provoked == 1
                            ? $"{actor.Name} bellows - one enemy can see nothing else."
                            : $"{actor.Name} bellows - all {provoked} of them come for him.");
                    break;
                }

                case SkillEffect.Transform:
                {
                    BeginBeat(actor, actor, isCast: true);
                    RecordSpellPresentation(skill);
                    SetStance(actor, Stances.Cast);
                    EnterTransform(actor, skill);
                    break;
                }

                case SkillEffect.Ward:
                {
                    BeginBeat(actor, actor, isCast: true);
                    RecordSpellPresentation(skill);
                    SetStance(actor, Stances.Cast);

                    int warded = ApplyWard(actor);
                    AppendMessage(warded <= 1
                        ? $"{actor.Name} pulls the fleece close."
                        : $"{actor.Name} throws the fleece wide - {warded} of them are warded.");
                    break;
                }

                case SkillEffect.Shatter:
                {
                    BeginBeat(actor, _encounter.LivingEnemies.FirstOrDefault(), isCast: true);
                    RecordSpellPresentation(skill);
                    SetStance(actor, Stances.Cast);
                    ResolveShatter(actor);
                    break;
                }

                case SkillEffect.BuffParty:
                {
                    BeginBeat(actor, actor, isCast: true);
                    RecordSpellPresentation(skill);
                    SetStance(actor, Stances.Cast);

                    foreach (var ally in _encounter.LivingPlayerParty.ToList())
                    {
                        ApplySkillStatus(skill, ally, actor);
                    }

                    AppendMessage($"{actor.Name} lets out {skill.DisplayName}.");
                    break;
                }

                case SkillEffect.GiftMana:
                case SkillEffect.GiftFury:
                case SkillEffect.GiftHaste:
                {
                    BeginBeat(actor, actor, isCast: true);
                    RecordSpellPresentation(skill);
                    SetStance(actor, Stances.Cast);
                    ResolveGift(actor, skill);
                    break;
                }
            }
        }

        private void ResolveDamageSingle(CombatantState actor, ResolvedSkill skill, CombatantState target, int resourceSpent)
        {
            target = target ?? _encounter.LivingEnemies.FirstOrDefault();
            if (target == null) return;

            BeginBeat(actor, target, isCast: true);
            RecordSpellPresentation(skill);

            int damage;
            if (skill.HasFixedDamage)
            {
                // A spell with authored packets deals exactly what it says, per
                // element, and reports the split.
                var detail = new StringBuilder();
                damage = ResolveDamageInstances(actor, skill, target, detail);
                AppendMessage($"{actor.Name} casts {skill.DisplayName} on {target.Name} for {damage}! -{detail}");
            }
            else
            {
                // The cast type is read ONCE and handed to both the scaling
                // axis and the effectiveness check, which is what guarantees
                // they can never disagree about which element this cast is.
                var castType = ActorAttackType(actor) ?? DamageType.Physical;
                var outcome = DamagePipeline.AfterDefences(
                    SkillResolution.Amount(skill.Effect, actor, target, skill.Power, skill.FlatAmount,
                        resourceSpent, skill.IgnoresDefense, castType, skill.ScalingAxis),
                    actor, target,
                    attackType: castType,
                    weakness: SourceFor(target)?.Weakness,
                    resistance: SourceFor(target)?.Resistance,
                    varianceRange: DamageVarianceRange,
                    rng: _rng,
                    resolveWard: ResolveWard);

                damage = outcome.Damage;
                DepleteBreakShield(target, outcome.Effectiveness);
                AppendMessage($"{actor.Name} uses {skill.DisplayName} on {target.Name} for {damage} damage!{EffectivenessSuffix(outcome.Effectiveness)}");
            }

            // Through the shared tail rather than its own copy of it, so a
            // character skill's kill earns the same riders a plain attack's
            // does. A swing is a swing.
            ApplyFinalDamage(actor, target, damage);

            if (target.IsAlive)
            {
                ApplySkillStatus(skill, target, actor);
                ApplyQueuePush(actor, skill, target);
            }
        }

        private void ResolveDamageAll(CombatantState actor, ResolvedSkill skill, int resourceSpent)
        {
            var summary = new StringBuilder();
            summary.Append($"{actor.Name} unleashes {skill.DisplayName}!");

            BeginBeat(actor, _encounter.LivingEnemies.FirstOrDefault(), isCast: true);
            RecordSpellPresentation(skill);

            // The caster's own type does not change per target, so this reads
            // once -- same reasoning as the single-target branch.
            var castType = ActorAttackType(actor) ?? DamageType.Physical;

            foreach (var enemy in _encounter.LivingEnemies.ToList())
            {
                // Effectiveness is resolved PER ENEMY: one cast can be super
                // effective against one target and resisted by another in the
                // same fight.
                var outcome = DamagePipeline.AfterDefences(
                    SkillResolution.Amount(skill.Effect, actor, enemy, skill.Power, skill.FlatAmount,
                        resourceSpent, skill.IgnoresDefense, castType, skill.ScalingAxis),
                    actor, enemy,
                    attackType: castType,
                    weakness: SourceFor(enemy)?.Weakness,
                    resistance: SourceFor(enemy)?.Resistance,
                    varianceRange: DamageVarianceRange,
                    rng: _rng,
                    resolveWard: ResolveWard);

                DepleteBreakShield(enemy, outcome.Effectiveness);

                // Counted with the CAST's type, not the caster's swing: a
                // physical character throwing a fire skill dealt fire.
                DealDamage(actor, enemy, outcome.Damage, castType);

                // One beat shows one number, so an AOE reports its largest
                // single hit rather than a total that matches no one enemy's HP
                // drop. The LEDGER takes the full amount per enemy, which is
                // why it cannot be derived from the beats.
                RecordBeatAmount(System.Math.Max(outcome.Damage, LargestAmountSoFar));
                SetStance(enemy, enemy.IsAlive ? Stances.Hurt : Stances.Defeated);
                summary.Append($" {enemy.Name} takes {outcome.Damage}{EffectivenessSuffix(outcome.Effectiveness)}");

                if (!enemy.IsAlive)
                {
                    summary.Append($" {enemy.Name} is defeated!");
                    _killedThisAction = true;
                    RecordKill(actor, enemy);
                }
                else
                {
                    ApplySkillStatus(skill, enemy, actor);
                }
            }

            AppendMessage(summary.ToString());
        }

        private int ResolveDamageInstances(CombatantState actor, ResolvedSkill skill, CombatantState target, StringBuilder detail)
        {
            int total = 0;
            float multiplier = SkillPowerMultiplierFor(actor);

            foreach (var instance in skill.DamageInstances)
            {
                int scaled = System.Math.Max(1, Rounding.AwayFromZero(instance.amount * multiplier));
                var outcome = DamagePipeline.AfterDefences(
                    scaled, instance.type, target,
                    weakness: SourceFor(target)?.Weakness,
                    resistance: SourceFor(target)?.Resistance,
                    varianceRange: DamageVarianceRange,
                    rng: _rng,
                    resolveWard: ResolveWard);

                DepleteBreakShield(target, outcome.Effectiveness);
                total += outcome.Damage;
                detail.Append($" {outcome.Damage} {instance.type}{EffectivenessSuffix(outcome.Effectiveness)}");
            }

            return total;
        }

        // ---- riders on a resolved skill --------------------------------------

        private void ApplySkillRoleEffect(CharacterRole? role, CombatantState actor, CombatantState target, int damage, bool isExecute)
        {
            switch (role)
            {
                case CharacterRole.Assassin:
                    if (isExecute)
                    {
                        AppendMessage($"{actor.Name} finds an opening on the weakened {target.Name}!");
                    }
                    break;

                case CharacterRole.Tank:
                    int lifesteal = Rounding.AwayFromZero(damage * FightTuning.TankSkillLifestealFraction);
                    if (lifesteal > 0)
                    {
                        HealAndCount(actor, lifesteal);
                        AppendMessage($"{actor.Name} recovers {lifesteal} HP from the blow.");
                    }
                    break;

                case CharacterRole.CrowdControl:
                    target.Defense = System.Math.Max(0, target.Defense - FightTuning.CrowdControlDefenseShred);
                    AppendMessage($"{target.Name}'s defenses are shredded!");
                    break;

                // Utility and Support shared one case until v1 split them,
                // which had made two characters mechanically identical.
                // Utility feeds its own engine instead of the party's health
                // bar -- a no-op for a Utility character with no signature
                // resource, graceful by construction rather than special case.
                case CharacterRole.Utility:
                    int gained = actor.Signature?.Gain(FightTuning.UtilitySkillSignatureGain) ?? 0;
                    if (gained > 0)
                    {
                        AppendMessage($"{actor.Name} gathers {gained} {actor.Signature.DisplayName} from the effort.");
                    }
                    break;

                case CharacterRole.Support:
                    foreach (var ally in _encounter.LivingPlayerParty.ToList())
                    {
                        HealAndCount(ally, FightTuning.SupportSkillPartyHealAmount);
                    }
                    AppendMessage($"{actor.Name}'s Skill also mends the squad's wounds.");
                    break;
            }
        }

        // Headbutt's shove, and the intent it can take with it.
        //
        // A separate method rather than four lines inside the damage case,
        // because the two halves belong to different systems -- the push is the
        // skill's own authored property, the cancel is a talent riding on it --
        // and reading them together is what makes the dependency obvious: no
        // push, no cancel.
        private void ApplyQueuePush(CombatantState actor, ResolvedSkill skill, CombatantState target)
        {
            if (skill.QueuePushSlots <= 0 || target == null || !target.IsAlive) return;
            if (!_encounter.PushBack(target, skill.QueuePushSlots)) return;

            AppendMessage($"{target.Name} is knocked back down the order.");

            // Charge T2: the shove does not merely delay the telegraphed
            // action, it takes it away. Removing the committed intent IS that
            // -- PrepareEnemyIntents re-rolls one for whoever has none when the
            // player's turn next comes round, so what the enemy was winding up
            // is genuinely gone rather than postponed.
            if (!actor.Talents.Has(TalentEffectType.HeadbuttCancelsIntent)) return;

            // Only says so when there WAS something telegraphed. A plain attack
            // is not shown on the nameplate at all, so announcing that one was
            // cancelled would be claiming credit for interrupting nothing.
            string intent = IntentFor(target);
            bool wasTelegraphed = !string.IsNullOrEmpty(intent) && intent != IntentAttack;
            _intents.Remove(target);

            if (wasTelegraphed)
            {
                AppendMessage($"{target.Name} loses hold of {intent} entirely!");
            }
        }

        // Applies whatever status this skill carries to whoever its own effect
        // just resolved against -- a damage effect's status lands on the enemy
        // it hit, a heal effect's on whoever was healed.
        //
        // `caster` is who gets the CREDIT, and attribution is not cosmetic: two
        // of the three wool engines are paid for statuses they applied, and an
        // unsourced status pays nobody.
        private void ApplySkillStatus(ResolvedSkill skill, CombatantState recipient, CombatantState caster = null)
        {
            if (!skill.AppliesStatus.HasValue || recipient == null || !recipient.IsAlive) return;

            var type = skill.AppliesStatus.Value;
            StatusEffects.Apply(recipient.Statuses, type, skill.StatusMagnitude, skill.StatusDuration, caster);

            bool isBeneficial = type == StatusEffectType.Regen || type == StatusEffectType.Protect;
            AppendMessage(isBeneficial
                ? $"{recipient.Name} gains {type}!"
                : $"{recipient.Name} is afflicted with {type}!");
        }

        private void RaiseMagicalShield(CombatantState actor)
        {
            if (RelicEffectFor(actor) != RelicEffect.MagicalShield) return;

            StatusEffects.Apply(actor.Statuses, StatusEffectType.Shielded,
                FightTuning.MagicalShieldReductionPercent, FightTuning.MagicalShieldDurationTurns, actor);
            AppendMessage($"{actor.Name}'s Magical Shield rises!");
        }

        // ---- what the actor's kit says ---------------------------------------

        private CharacterRole? ActorRole(CombatantState actor) => KitFor(actor)?.Role;

        private DamageType? ActorAttackType(CombatantState actor) => KitFor(actor)?.AttackType;

        // The generic Skill verb's numbers come from the character's basic
        // spell tier, if their level grants one, and fall back to plain
        // defaults otherwise -- graceful on missing content, house style.
        //
        // Public because the HUD model builds the basic spell's submenu row
        // from exactly these, through exactly this path. v1 hand-rolled a
        // separate mana check for that row, which is one of the two bugs the
        // decomposition was meant to kill.
        public int BasicSpellManaCostFor(CombatantState actor) => KitFor(actor)?.BasicSpell?.ManaCost ?? 0;

        public string BasicSpellNameFor(CombatantState actor) => KitFor(actor)?.BasicSpell?.DisplayName ?? "Spell";

        public bool CanAffordBasicSpell(CombatantState actor) =>
            SkillResolution.CanAfford(actor, BasicSpellManaCostFor(actor), 0);

        private int SkillManaCostFor(CombatantState actor) => BasicSpellManaCostFor(actor);

        private float SkillPowerMultiplierFor(CombatantState actor) => KitFor(actor)?.BasicSpell?.PowerMultiplier ?? 1f;

        private string SkillDisplayNameFor(CombatantState actor) => BasicSpellNameFor(actor);

        private static bool IsBelowHealthFraction(CombatantState combatant, float fraction) =>
            combatant.MaxHealth > 0 && combatant.CurrentHealth <= combatant.MaxHealth * fraction;

        private void RecordSpellPresentation(ResolvedSkill skill) =>
            RecordSpellPresentation(skill.VfxPath, skill.VfxSeconds, skill.VfxImpactFrame, skill.SfxPath,
                skill.VfxFromCaster, skill.VfxDepartFrame);
    }
}
