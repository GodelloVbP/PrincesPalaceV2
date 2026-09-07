using System.Collections.Generic;
using System.Linq;
using System.Text;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // Everything the player can do that is not a plain swing: the fourteen
    // authored skill effects. (The generic, nameless "Skill" action every
    // character used to get for free is gone -- see the note below.)
    //
    // Ported from v1's FightController.Actions.cs. Every number and every
    // effect comes from the resolved skill, so adding a skill stays a line in
    // skills.json rather than a const, two dictionary entries, a switch case
    // and a Resolve method.
    public sealed partial class FightSession
    {
        // ---- authored character skills ---------------------------------------
        //
        // The generic Skill verb (ExecuteSkill) that used to sit here is gone
        // (docs/PLAN_SHOP.md §4 Phase E): every cast now goes through
        // CastSkill below, against one of the character's own authored
        // skills -- there is no second, nameless spell any more for a
        // separate verb to run.

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

            // THE REACH CHECK COMES FIRST -- ahead of the cost check, the
            // resolvability check, the cooldown and the beat. A cast the
            // field will not allow must not read as a cast the actor cannot
            // afford, and must spend nothing on the way to being refused.
            //
            // ONLY SingleEnemy. Self, Party and AllEnemies have no
            // single-opponent question to ask (see CanReach's own header),
            // and asking one about them would refuse every group cast the
            // moment a taunt was up.
            if (skill.Targeting == SkillTargeting.SingleEnemy && !CanReach(actor, skill.Reach, target))
            {
                AppendMessage($"{(target == null ? "That target" : target.Name)} is out of reach.");
                return false;
            }

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

            int resourceSpent = SkillResolution.ResourceToSpend(actor.Signature, skill.ResourceCost, skill.SpendsAllResource);
            ChargeSkillMana(actor, skill.ManaCost);

            // Spent alongside the mana, and for the same reason it is spent
            // here rather than at the end: the cast is committed at this point.
            // A resolution that lands on nothing still cost the turn.
            BeginCooldown(actor, skill);
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
            // The shield rose inside ResolveCharacterSkill's own relic moment
            // -- see FightSession.Relics.RelicsAfterCast. It used to be raised
            // here as well, which was harmless only because Apply refreshes
            // rather than stacks.

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
            // ONE CAST, one advance of the counter, however many things it
            // lands on. See FightSession.Potency.
            RelicsBeforeCast(actor);

            try
            {
                ResolveCharacterSkillInner(actor, skill, target, resourceSpent);
            }
            finally
            {
                // Every path out, including the ones that resolve nothing --
                // a charge left armed would be spent by whatever acted next.
                RelicsAfterCast(actor, skill, target, resourceSpent);
            }
        }

        private void ResolveCharacterSkillInner(CombatantState actor, ResolvedSkill skill, CombatantState target, int resourceSpent)
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
                    foreach (var ally in _encounter.AlliesOf(actor).ToList())
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
                    foreach (var ally in _encounter.AlliesOf(actor).ToList())
                    {
                        CombatMath.RestoreMana(ally, amount);
                    }

                    AppendMessage($"{actor.Name}'s {skill.DisplayName} restores the squad's mana!");
                    break;
                }

                case SkillEffect.Provoke:
                {
                    BeginBeat(actor, target ?? _encounter.OpponentsOf(actor).FirstOrDefault(), isCast: true);
                    RecordSpellPresentation(skill);
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
                    EnterTransform(actor, skill);
                    break;
                }

                case SkillEffect.Ward:
                {
                    BeginBeat(actor, actor, isCast: true);
                    RecordSpellPresentation(skill);
                    int warded = ApplyWard(actor);
                    AppendMessage(warded <= 1
                        ? $"{actor.Name} pulls the fleece close."
                        : $"{actor.Name} throws the fleece wide - {warded} of them are warded.");
                    break;
                }

                case SkillEffect.Shatter:
                {
                    BeginBeat(actor, _encounter.OpponentsOf(actor).FirstOrDefault(), isCast: true);
                    RecordSpellPresentation(skill);
                    ResolveShatter(actor);
                    break;
                }

                case SkillEffect.BuffParty:
                {
                    BeginBeat(actor, actor, isCast: true);
                    RecordSpellPresentation(skill);
                    foreach (var ally in _encounter.AlliesOf(actor).ToList())
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
                    ResolveGift(actor, skill);
                    break;
                }

                case SkillEffect.Summon:
                {
                    BeginBeat(actor, actor, isCast: true);
                    RecordSpellPresentation(skill);
                    ResolveSummon(actor, skill);
                    break;
                }

                // MATCHING SkillResolution.Amount's own default, and for the
                // same reason. Falling through here was the quietest failure
                // in the file: the cast had already charged its mana, spent
                // its cooldown and committed the turn by the time it arrived,
                // so a member added without a branch produced a skill that
                // cost everything and did nothing, with no exception, no
                // message and no beat -- indistinguishable from a resolution
                // that legitimately landed on nobody.
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(skill), skill.Effect,
                        "FightSession has no resolution branch for this skill effect.");
            }
        }

        // Which of the CASTER'S OWN stance folders plays for this skill —
        // the authored override if there is one, "cast" otherwise. Every
        // skill written before RawSkillEntry.stance existed left it blank,
        // so this changes nothing for any of them.
        private static string StanceFor(ResolvedSkill skill) =>
            string.IsNullOrEmpty(skill.Stance) ? Stances.Cast : skill.Stance;

        // Roar's whole mechanic: call in one more of whatever this skill
        // names, unless the caster's own side already fields the cap.
        //
        // THE CAP IS ALSO CHECKED AT DRAW TIME (see PrepareEnemyIntents'
        // EffectivePoolFor), which is what stops the boss from visibly
        // winding up for a call that then does nothing turn after turn.
        // Checked again HERE regardless, because a draw made when the field
        // was under the cap can still land after something else filled the
        // gap in the meantime — a second summon resolving first in the same
        // round, say — and resolving into a fizzle beats resolving into a
        // silent over-cap.
        private void ResolveSummon(CombatantState actor, ResolvedSkill skill)
        {
            if (string.IsNullOrEmpty(skill.SummonEnemyId) || _summonFactory == null)
            {
                AppendMessage($"{actor.Name} calls out, but nothing answers.");
                return;
            }

            int living = LivingCountOf(skill.SummonEnemyId);
            if (living >= skill.SummonCap)
            {
                AppendMessage($"{actor.Name} calls out, but there is no room left on the field.");
                return;
            }

            if (!_summonFactory(skill.SummonEnemyId, out var state, out var kit) || state == null || kit == null)
            {
                AppendMessage($"{actor.Name} calls out, but nothing answers.");
                return;
            }

            if (!_encounter.TryAddEnemy(state, _stageSlotsPerSide))
            {
                AppendMessage($"{actor.Name} calls out, but there is no room left on the field.");
                return;
            }

            _enemyKits[state] = kit;
            state.IsSummon = true;
            AppendMessage($"{actor.Name} calls out — {state.Name} answers!");

            // Disgruntled Lackey: an ENEMY's own summons draw the party's
            // grudge -- see its own header.
            DisgruntledLackeyOnEnemySummon(actor);
        }

        private void ResolveDamageSingle(CombatantState actor, ResolvedSkill skill, CombatantState target, int resourceSpent)
        {
            target = target ?? _encounter.OpponentsOf(actor).FirstOrDefault();
            if (target == null) return;

            BeginBeat(actor, target, isCast: true);
            RecordSpellPresentation(skill);

            // THE SKILL'S OWN APPROACH, over the "rooted" BeginBeat assumed
            // for every cast. A melee skill is a swing that happens to be
            // authored as a skill, and until this line it connected from
            // wherever the caster was standing.
            ApproachAs(skill.Approach);

            int damage;
            if (skill.HasFixedDamage)
            {
                // A spell with authored packets deals exactly what it says, per
                // element, and reports the split.
                var detail = new StringBuilder();
                damage = ResolveDamageInstances(actor, skill, target, detail, out bool dodgedInstances);

                // Swift: rolled ONCE for the whole multi-packet cast inside
                // ResolveDamageInstances -- see that method's own header and
                // DamagePipeline.AfterDefences' dodgeAlreadyResolved param
                // for why a packet spell does not roll per packet.
                if (dodgedInstances)
                {
                    RecordMiss();
                    AppendMessage($"{target.Name} dodges {actor.Name}'s {skill.DisplayName}!");
                    return;
                }

                AppendMessage($"{actor.Name} casts {skill.DisplayName} on {target.Name} for {damage}! -{detail}");
            }
            else
            {
                // The cast type is read ONCE and handed to both the scaling
                // axis and the effectiveness check, which is what guarantees
                // they can never disagree about which element this cast is.
                var castType = ActorAttackType(actor) ?? DamageType.Physical;

                // THE BASE, held so the charge can be measured against it
                // rather than against whatever the pipeline turns it into.
                int baseAmount = SkillResolution.Amount(skill.Effect, actor, target, skill.Power,
                    skill.FlatAmount, resourceSpent, skill.IgnoresDefense, castType, skill.ScalingAxis);

                var outcome = DamagePipeline.AfterDefences(
                    baseAmount,
                    actor, target,
                    attackType: castType,
                    affinity: AffinityOf(target),
                    varianceRange: DamageVarianceRange,
                    rng: _rng,
                    resolveWard: ResolveWard,
                    ignoresDefense: skill.IgnoresDefense);

                if (outcome.IsMiss)
                {
                    RecordMiss();
                    AppendMessage($"{target.Name} dodges {actor.Name}'s {skill.DisplayName}!");
                    return;
                }

                damage = TotalDamage(actor, baseAmount, outcome.Damage);
                DepleteBreakShield(target, outcome.Effectiveness);
                AppendMessage($"{actor.Name} uses {skill.DisplayName} on {target.Name} for {damage} damage!{EffectivenessSuffix(outcome.Effectiveness)}");
            }

            // Through the shared tail rather than its own copy of it, so a
            // character skill's kill earns the same riders a plain attack's
            // does. A swing is a swing.
            ApplyFinalDamage(actor, target, damage);

            // The Drowned Lantern: a damaging spell marks whatever it lands
            // on, for an attack to cash in later.
            ApplyMark(actor, target);

            // Magic Marker (mechanic a). Independent of the line above.
            MagicMarkerApplyMark(actor, target);

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

            // EVERYONE ON THE OTHER SIDE, snapshotted before the loop resolves
            // any of them. The beat's Target is the first of them and always was; the
            // rest are what the view needed and never had, which is why an
            // all-enemies spell animated on exactly one rat.
            //
            // Taken BEFORE the damage lands, deliberately: an enemy killed by
            // this very cast should still be drawn taking the hit that killed
            // it, and reading the living list afterwards would skip it.
            var struck = _encounter.OpponentsOf(actor).ToList();

            BeginBeat(actor, struck.FirstOrDefault(), isCast: true);
            RecordSpellPresentation(skill);
            RecordSplashTargets(struck.Skip(1));

            // The caster's own type does not change per target, so this reads
            // once -- same reasoning as the single-target branch.
            var castType = ActorAttackType(actor) ?? DamageType.Physical;

            // One beat shows one number, so an AOE reports its largest single
            // hit rather than a total that matches no one enemy's HP drop --
            // tracked as a plain local rather than re-read off the recording
            // beat (LargestAmountSoFar), because ApplyFinalDamage below
            // OVERWRITES the beat's Amount to that one enemy's own `landed`
            // on every call (RecordBeatAmount assigns, it does not
            // accumulate) -- reading the beat back after that call would see
            // only the most recent hit, not the sweep's running max. The
            // LEDGER takes the full amount per enemy regardless, which is
            // why it cannot be derived from the beats either way.
            int largestLanded = 0;

            foreach (var enemy in _encounter.OpponentsOf(actor).ToList())
            {
                // An earlier enemy THIS SAME SWEEP already fell to might have
                // splashed a kill onto this one (ApplyKillSplash/
                // ApplyModifierKillSplash, now reachable from an AOE hit --
                // see ApplyFinalDamage below). `enemy` was snapshotted alive
                // when the loop began; re-check rather than swing again at a
                // corpse.
                if (!enemy.IsAlive) continue;

                // A SWEEP WITH AUTHORED PACKETS goes through the same
                // per-element resolution a single-target packet spell does, and
                // through nothing else. Before Cinderfault this branch did not
                // exist and a DamageAll skill with damageInstances would have
                // fallen straight through to the Attack-scaled arithmetic
                // below -- which reads Power and FlatAmount, both zero on a
                // packet skill, so the spell would have dealt the floor of 1
                // per enemy with no elemental check anywhere in it. Silent, and
                // wrong in exactly the way a packet spell exists to avoid.
                //
                // Deliberately NOT routed through TotalDamage: the single-
                // target packet branch does not either (see ResolveDamageSingle),
                // and a sweep taking a relic multiplier its own single-target
                // twin does not take would be a balance change riding in on a
                // presentation change.
                if (skill.HasFixedDamage)
                {
                    var packets = new StringBuilder();
                    int packetTotal = ResolveDamageInstances(actor, skill, enemy, packets, out bool packetsDodged);

                    if (packetsDodged)
                    {
                        summary.Append($" {enemy.Name} dodges!");
                        RecordTargetResult(enemy, 0, missed: true);
                        continue;
                    }

                    ApplyFinalDamage(actor, enemy, packetTotal);
                    RecordTargetResult(enemy, packetTotal);

                    largestLanded = System.Math.Max(packetTotal, largestLanded);
                    RecordBeatAmount(largestLanded);
                    summary.Append($" {enemy.Name} takes {packetTotal}! -{packets}");

                    ApplyMark(actor, enemy);
                    MagicMarkerApplyMark(actor, enemy);

                    if (enemy.IsAlive)
                    {
                        ApplySkillStatus(skill, enemy, actor);
                    }

                    continue;
                }

                // Effectiveness is resolved PER ENEMY: one cast can be super
                // effective against one target and resisted by another in the
                // same fight.
                // Per enemy, because the base itself is per enemy -- defence
                // differs. The CHARGE was decided once for the whole cast.
                int baseAmount = SkillResolution.Amount(skill.Effect, actor, enemy, skill.Power,
                    skill.FlatAmount, resourceSpent, skill.IgnoresDefense, castType, skill.ScalingAxis);

                var outcome = DamagePipeline.AfterDefences(
                    baseAmount,
                    actor, enemy,
                    attackType: castType,
                    affinity: AffinityOf(enemy),
                    varianceRange: DamageVarianceRange,
                    rng: _rng,
                    resolveWard: ResolveWard,
                    ignoresDefense: skill.IgnoresDefense);

                // Swift: EACH enemy in an AOE independently rolls its own
                // dodge -- it is a genuinely separate target reacting to the
                // same cast, not one shared roll for the whole sweep. A
                // dodged enemy skips damage/riders/kill-check for itself and
                // the loop continues to the rest; this beat is multi-target
                // already (SplashTargets), so "some hit, some dodged" is
                // reported per enemy in the summary text rather than via
                // CombatBeat.Missed, which only ever describes a single-
                // target beat's own one Amount -- see that field's header.
                if (outcome.IsMiss)
                {
                    summary.Append($" {enemy.Name} dodges!");
                    RecordTargetResult(enemy, 0, missed: true);
                    continue;
                }

                DepleteBreakShield(enemy, outcome.Effectiveness);
                // Through the ONE FUNNEL now -- see FightSession.Relics.TotalDamage
                // for why this call site is the reason it exists.
                int landed = TotalDamage(actor, baseAmount, outcome.Damage);

                // THE SAME RIDER PATH a plain swing and a single-target skill
                // already funnel through -- see ApplyFinalDamage's own
                // header. Before this, ResolveDamageAll called DealDamage
                // directly, which meant an AOE cast silently skipped every
                // item-modifier on-hit rider (elemental procs, lifesteal,
                // push/chill/root chances), Sharp Horns' defence shred, the
                // Black Ram's transform splash, and both kill-splash sources
                // -- all of which fire for a single-target hit. Routing
                // through here is the fix: whatever a landed hit triggers,
                // an AOE's landed hits trigger too, per enemy.
                //
                // castType, not AttackTypeOf(actor): ApplyFinalDamage's own
                // internal DealDamage call reads AttackTypeOf(actor), which
                // resolves identically to castType above (both fall through
                // KitFor(actor)?.AttackType -> SourceFor(actor)?.Source.
                // AttackType -> Physical) -- so this still counts as the
                // CAST's type, not the caster's swing, exactly as before.
                ApplyFinalDamage(actor, enemy, landed);

                RecordTargetResult(enemy, landed);

                largestLanded = System.Math.Max(landed, largestLanded);
                RecordBeatAmount(largestLanded);
                summary.Append($" {enemy.Name} takes {landed}{EffectivenessSuffix(outcome.Effectiveness)}");

                // The Drowned Lantern: a sweep marks everyone it actually hits.
                ApplyMark(actor, enemy);

                // Magic Marker (mechanic a). Independent of the line above.
                MagicMarkerApplyMark(actor, enemy);

                // The kill message happens inside ApplyFinalDamage, and the
                // rider flag and the ledger's kill row deeper still, inside
                // the DealDamage it calls -- this only needs its own
                // AOE-specific call, the status a SURVIVOR takes.
                if (enemy.IsAlive)
                {
                    ApplySkillStatus(skill, enemy, actor);
                }
            }

            AppendMessage(summary.ToString());
        }

        // Balance redesign Phase 3 (D3): the caster's own SkillScaling
        // multiplier (INT/WIS grades against their ACTUAL ability scores) --
        // the same "M" a spell's Attack-scaled formula already rides via
        // CombatMath.ScaledAttack -- applied to a FIXED damageInstances
        // packet too (frost_flare, lightning_bolt), so it keeps pace with a
        // caster's ability-score investment instead of staying flat forever.
        // Spell TIER's own powerMultiplier (SkillPowerMultiplierFor) is a
        // SEPARATE axis, already applied alongside this one -- the two never
        // stood in for each other and neither replaces the other here.
        //
        // Exactly 1f for every enemy caster: an enemy CombatantState's
        // SkillScaling is never assigned (only FightEncounterAdapter's
        // Character-based ToCombatant sets it), so it stays ScalingSet.None
        // and MultiplierFor short-circuits to 1 regardless of AbilityScores
        // — a monster's frost_flare-style hit is unchanged by this.
        private static float SpellScalingMultiplierFor(CombatantState actor) =>
            actor.SkillScaling.MultiplierFor(actor.AbilityScores);

        // `dodged` is true when the WHOLE cast was evaded -- rolled exactly
        // ONCE here, before the packet loop, rather than once per packet.
        // See DamagePipeline.AfterDefences' `dodgeAlreadyResolved` param for
        // the full reasoning: a multi-element spell is one swing the target
        // either evades entirely or is hit by, so every packet below reuses
        // this single roll (dodgeAlreadyResolved: true) instead of each
        // rolling its own. `total` is 0 and `detail` is left untouched on a
        // dodge -- the caller must check `dodged` rather than infer a miss
        // from `total == 0`, the identical discipline
        // DamagePipeline.Outcome.IsMiss already enforces one level down.
        // Sums ModifierEffectType.ElementalDamagePercent across every source
        // that grants it for this element, rather than ModifierEffectSet.
        // Best()'s max-not-sum default -- see FightEncounterAdapter.cs'
        // TypedResistanceFlat precedent ("the two sources stack, same as
        // PhysicalDefense/MagicalDefense already do for relics vs gear
        // stats"): a typed rider meant to combine across sources, not a
        // repeated copy of one rule that Best() would collapse. Reads
        // actor.ModifierEffects directly (an instance member), so this does
        // not need `this` despite living on FightSession.
        private int ElementalDamagePercentFor(CombatantState actor, DamageType type)
        {
            int total = 0;
            foreach (var effect in actor.ModifierEffects.All)
            {
                if (effect.Type == ModifierEffectType.ElementalDamagePercent && effect.Against == type)
                {
                    total += effect.Magnitude;
                }
            }

            return total;
        }

        private int ResolveDamageInstances(CombatantState actor, ResolvedSkill skill, CombatantState target,
            StringBuilder detail, out bool dodged)
        {
            dodged = DamagePipeline.RollDodge(target, actor, _rng);
            if (dodged)
            {
                return 0;
            }

            int total = 0;
            float multiplier = SkillPowerMultiplierFor(actor) * SpellScalingMultiplierFor(actor);

            foreach (var instance in skill.DamageInstances)
            {
                float elementalMultiplier = 1f + ElementalDamagePercentFor(actor, instance.type) / 100f;
                int scaled = System.Math.Max(1, Rounding.AwayFromZero(instance.amount * multiplier * elementalMultiplier));
                var outcome = DamagePipeline.AfterDefences(
                    scaled, instance.type, target,
                    affinity: AffinityOf(target),
                    varianceRange: DamageVarianceRange,
                    rng: _rng,
                    resolveWard: ResolveWard,
                    attacker: actor,
                    dodgeAlreadyResolved: true);

                DepleteBreakShield(target, outcome.Effectiveness);
                total += outcome.Damage;
                detail.Append($" {outcome.Damage} {instance.type}{EffectivenessSuffix(outcome.Effectiveness)}");
            }

            return total;
        }

        // PRE-MITIGATION PREVIEW for the skill-detail card's POWER row: what
        // this cast would deal from the CASTER'S OWN stats/relics/buffs alone
        // -- no target, so no defense, no elemental resistance, no variance,
        // no ward. Read-only: does not touch the RNG stream, does not spend
        // the resource, does not advance PotencyFor's cast tally or any other
        // relic bookkeeping.
        //
        // SkillResolution.Amount/Damage never mitigate at all any more (see
        // Damage's own header) -- mitigation is DamagePipeline's alone now --
        // so passing a null target changes nothing there; this preview simply
        // never calls DamagePipeline.AfterDefences at all, which is the one
        // and only place a defense term is subtracted. The fixed-damage
        // branch mirrors ResolveDamageInstances' own scaling (multiplier,
        // AwayFromZero, floored at 1 per packet), for the same reason.
        public int PreviewSkillPower(CombatantState actor, ResolvedSkill skill)
        {
            if (actor == null) return 0;

            if (skill.HasFixedDamage)
            {
                float multiplier = SkillPowerMultiplierFor(actor) * SpellScalingMultiplierFor(actor);
                int total = 0;
                foreach (var instance in skill.DamageInstances)
                {
                    total += System.Math.Max(1, Rounding.AwayFromZero(instance.amount * multiplier));
                }
                return total;
            }

            int resourceSpent = SkillResolution.ResourceToSpend(actor.Signature, skill.ResourceCost, skill.SpendsAllResource);
            var castType = ActorAttackType(actor) ?? DamageType.Physical;

            return SkillResolution.Amount(skill.Effect, actor, null, skill.Power,
                skill.FlatAmount, resourceSpent, skill.IgnoresDefense, castType, skill.ScalingAxis);
        }

        // ---- riders on a resolved skill --------------------------------------

        // ApplySkillRoleEffect (Tank lifesteal, CrowdControl defense-shred,
        // Support party-heal, Utility signature-gain, Assassin execute
        // messaging) was removed with the BasicSpell cut (docs/PLAN_SHOP.md
        // Gate 4). Its only caller anywhere in the codebase was the deleted
        // ExecuteSkillInner -- these five role riders never fired on an
        // authored/named skill cast, only on the old free "Skill" action, so
        // there is no remaining entry point to preserve them through. Decided
        // 2026-09-03: let them go rather than silently extend five class-role
        // bonuses onto every authored skill, which would have been a real
        // balance change nobody asked for.

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

            // NO SPARRING NOTE. A push moves the target down the TURN ORDER,
            // not along the battle line -- see NoteDeliberateMove's own
            // header for why the two stopped sharing an event.

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
            if (!HasRelic(actor, RelicEffect.MagicalShield)) return;

            StatusEffects.Apply(actor.Statuses, StatusEffectType.Shielded,
                FightTuning.MagicalShieldReductionPercent, FightTuning.MagicalShieldDurationTurns, actor);
            AppendMessage($"{actor.Name}'s Magical Shield rises!");
        }

        // ---- what the actor's kit says ---------------------------------------

        private CharacterRole? ActorRole(CombatantState actor) => KitFor(actor)?.Role;

        // Public: FightHudModel's SCALES row needs to resolve the same
        // Weapon-vs-Spell axis SkillResolution.Damage resolves at cast time,
        // and that resolution starts here.
        //
        // ENEMIES FALL THROUGH TO THEIR OWN AUTHORED TYPE now, via SourceFor
        // -- KitFor only ever answers for player kits (see its own header),
        // so before this an enemy's swing had no attack type at all and
        // MagicalDefense was consequently a dead stat against every monster
        // in the game. See ResolvedEnemy.AttackType and RawEnemyEntry's own
        // comment on the field.
        //
        // `?.Source?.` and not `?.Source.`: EnemyKit is deliberately
        // null-source-tolerant (see its constructor), so a combatant holding
        // a kit with no resolved record -- a summon built without one -- must
        // fall through to "no authored type" here rather than throw on its
        // first swing.
        public DamageType? ActorAttackType(CombatantState actor) =>
            KitFor(actor)?.AttackType ?? SourceFor(actor)?.Source?.AttackType;

        // THE ONE PLACE mana is charged for a skill cast, which is what lets
        // Runic's one-shot discount live in a single spot rather than being
        // duplicated at every call site. Consumes and clears
        // PendingManaDiscountPercent unconditionally, whether or not this
        // particular cast had anything armed -- an unarmed discount is
        // already 0, so "consume" is a no-op the same way spending 0 gold
        // is.
        //
        // NOTE: SkillResolution.CanAfford, at both call sites, is checked
        // BEFORE this runs, against the UNDISCOUNTED cost -- a cast the
        // discount would have made affordable but the raw cost does not is
        // still refused. Conservative rather than wrong: the discount is a
        // bonus on a cast the player could already pay for, not a new way
        // to afford one they could not.
        private void ChargeSkillMana(CombatantState actor, int baseCost)
        {
            if (actor == null) return;

            int discountPercent = actor.PendingManaDiscountPercent;
            actor.PendingManaDiscountPercent = 0;

            int cost = baseCost;
            if (discountPercent > 0)
            {
                cost = Rounding.AwayFromZero(baseCost * (100 - System.Math.Min(100, discountPercent)) / 100f);
                if (cost < 0) cost = 0;
                AppendMessage($"{actor.Name}'s cast costs less, still charged from the last swing.");
            }

            CombatMath.SpendMana(actor, cost);
        }

        // The spell TIER's own powerMultiplier -- kept independent of the
        // BasicSpell removal (docs/PLAN_SHOP.md §4 Phase E), see
        // PlayerKit.SkillPowerMultiplier's own header for why: it scales
        // every FIXED-damage-instance skill (frost_flare, lightning_bolt),
        // not the free action that used to carry it.
        private float SkillPowerMultiplierFor(CombatantState actor) =>
            KitFor(actor)?.SkillPowerMultiplier ?? 1f;

        // Every half of a skill's presentation, recorded together -- the frames
        // it draws, the kick it insists on, and the pose the caster strikes.
        // Every branch of ResolveCharacterSkillInner already calls this, so all
        // three reach the view through exactly the door the spell does rather
        // than through a tenth call somebody has to remember.
        //
        // THE CASTER'S POSE IS THE NEWEST OF THE THREE, and it is here because
        // it kept being forgotten. BeginBeat sets "cast" as the starting pose;
        // a skill that authored its own stance -- the beetle's turtle_up and
        // shell_closed, the treant's trunk_slam -- said so, and honouring it
        // was left to each branch to remember with a SetStance. The damage and
        // summon paths remembered; the six self-buff effects (HealSelf,
        // HealParty, Ward, BuffParty, the Gifts, Provoke) did not, so Shell Up
        // posed the beetle mid-cast while it was supposed to be curling into a
        // ball -- "no animation for the defense self-buff". The enemy path
        // could not fix it from its own side either: it once set the stance
        // before calling in here, which did nothing because the beat this cast
        // belongs to is not opened until BeginBeat several lines later.
        //
        // Posed once, from the door every branch already uses. StanceFor falls
        // back to "cast", so every skill that left the stance blank is
        // unchanged.
        private void RecordSpellPresentation(ResolvedSkill skill)
        {
            RecordSpellPresentation(skill.Vfx);
            RecordShake(skill.Shake);

            if (_recordingBeat?.Actor != null)
            {
                SetStance(_recordingBeat.Actor, StanceFor(skill));
            }
        }
    }
}
