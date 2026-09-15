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
        //
        // `element` is the choice the player made at Element depth, and is null
        // for every skill that does not offer one -- which is every skill but
        // the orb. A DEFAULT PARAMETER rather than a second overload: the
        // element is not a different command, it is the same cast with one more
        // thing said about it, and two overloads would give the bot and the
        // controller two doors into one gate.
        public bool CastSkill(int index, CombatantState target, DamageType? element = null)
        {
            var actor = Current;
            var kit = KitFor(actor);
            if (kit == null || index < 0 || index >= kit.Skills.Count) return false;

            return CastSkill(kit.Skills[index], target, element);
        }

        public bool CastSkill(ResolvedSkill skill, CombatantState target, DamageType? element = null)
        {
            var actor = Current;
            if (actor == null) return false;

            // THE REACH CHECK COMES FIRST -- ahead of the cost check, the
            // resolvability check, the cooldown and the beat. A cast the
            // field will not allow must not read as a cast the actor cannot
            // afford, and must spend nothing on the way to being refused.
            //
            // ONLY SingleEnemy. Self, Party and AllEnemies have no
            // single-opponent question to ask (see CanReachEnemy's own header),
            // and asking one about them would refuse every group cast the
            // moment a taunt was up.
            if (skill.Targeting == SkillTargeting.SingleEnemy && !CanReachEnemy(actor, skill.Reach, target))
            {
                AppendMessage($"{(target == null ? "That target" : target.Name)} is out of reach.");
                return false;
            }

            // THE ALLY-SIDE SIBLING, in the same position and for the same
            // reason: a cast aimed at nobody, at a corpse, at the wrong side
            // or at a squadmate this effect will not accept must refuse
            // before a single point of mana, wool or cooldown is spent.
            //
            // A NULL TARGET IS A REFUSAL, NOT AN AUTO-PICK. Every ally-facing
            // skill in the game used to choose its own recipient (the first
            // living ally, the emptiest mana pool) and the owner's call on
            // AUDIT #147 was that deciding for the player is "just stupid" --
            // so a caller that hands this nothing has failed to ask, and gets
            // told, exactly the way a SingleEnemy cast with no mark does.
            //
            // AN EMPTY CANDIDATE LIST IS SOMEBODY ELSE'S REFUSAL. "There is
            // nobody to give this to" is a statement about the board, the
            // same shape as Shatter with no wards out, and CanResolveSkill
            // below is where those live -- underneath the cooldown check, so
            // a gift that could not have been cast this turn anyway says so
            // rather than complaining about the squad.
            if (skill.Targeting == SkillTargeting.SingleAlly)
            {
                var candidates = EligibleAllies(actor, skill);
                if (candidates.Count > 0 && !candidates.Contains(target))
                {
                    AppendMessage(target == null
                        ? $"{skill.DisplayName} needs an ally to aim at."
                        : $"{target.Name} cannot take {skill.DisplayName}.");
                    return false;
                }
            }

            // THE ELEMENT GATE SITS BETWEEN THE REACH CHECK AND THE COST CHECK,
            // and the order is the point: a cast the menu should never have
            // offered must not read to the player as a cast they cannot afford.
            // Nothing is spent on the way to any of these three refusals.
            if (!TryTakeElementChoice(actor, skill, element, out var chosen))
            {
                return false;
            }

            // THE CHOICE MADE, THEN THE ORDINARY CAST. AsElement's copy offers
            // no further choice (see its own header), so this recursion runs
            // the identical path every other skill takes -- one cast pipeline,
            // not a parallel one for typed spells.
            if (chosen != null) return CastSkill(chosen, target);

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

            // A FREE ACTION IS REFUSED BEFORE IT IS PAID FOR, like every
            // other refusal above -- the second Tuck In of a turn must not
            // eat the Wool and then silently end the turn instead.
            bool wantsFreeAction = IsFreeAction(actor, skill);
            if (wantsFreeAction && _freeActionTakenBy != null && ReferenceEquals(_freeActionTakenBy, actor))
            {
                AppendMessage($"{actor.Name} has already taken a free action this turn.");
                return false;
            }

            int resourceSpent = SkillResolution.ResourceToSpend(actor.SignaturePool, skill.ResourceCost,
                skill.SpendsAllResource, skill.ResourceSpendCap);

            // WHAT THE PRIMARY POOL PAYS. Ordinarily the authored ManaCost;
            // for a spendsAllPrimary skill (Bjorn's Second Wind) every point
            // it holds, with ManaCost as the minimum CanAfford already
            // checked. Read BEFORE the charge, because afterwards there is
            // nothing left to read.
            int primarySpent = skill.SpendsAllPrimary
                ? (actor.PrimaryPool?.Current ?? 0)
                : skill.ManaCost;

            ChargeSkillMana(actor, primarySpent);

            // Spent alongside the mana, and for the same reason it is spent
            // here rather than at the end: the cast is committed at this point.
            // A resolution that lands on nothing still cost the turn.
            BeginCooldown(actor, skill);
            actor.SignaturePool?.TrySpend(resourceSpent);

            // THE FURY TIER, spent from the actor's PRIMARY pool -- a
            // different pool and a different question from resourceSpent
            // above, which is the SIGNATURE pool's own wallet. Picked and
            // spent HERE, before resolution, so the multiplier that reaches
            // SkillResolution.Damage and the amount actually taken from the
            // pool always agree -- picking again after the pool has already
            // paid would silently choose a lower tier. See PoolTierResolution.
            var poolTier = PoolTierResolution.Pick(actor.PrimaryPool, skill.PoolTiers);
            if (poolTier.Fired)
            {
                actor.PrimaryPool.TrySpend(poolTier.SpendAmount);
            }

            // Only a cast that can actually deal damage spends Gift: Fury. A
            // ward burning somebody else's gift would be a present the player
            // never got to open.
            RefreshAttackBonus(actor, spendingGift: DealsDamage(skill));

            ResolveCharacterSkill(actor, skill, target, PointsSpent(skill, resourceSpent, primarySpent), poolTier);

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

            // A CAST THAT DOES NOT END THE TURN. Two sources say so and they
            // are ONE flag (see IsFreeAction): a skill authored freeAction --
            // Shawn's Tuck In -- and the Fragile Lamb's Fleece Ward T3, which
            // is the node that made warding stop costing the turn and is the
            // single biggest quality-of-life step in her path. Before it,
            // every ward is a turn not spent doing anything else, which on a
            // frail character with no damage output feels like being punished
            // for playing her correctly.
            //
            // ONCE PER TURN, which is new and is the whole reason this is a
            // generalisation rather than a second talent check. A free action
            // with no limit is an unbounded turn; the talent never reached
            // that because Fleece Ward has a two-turn cooldown, but an
            // authored flag with no lock would hand the next skill an
            // infinite loop. The lock is cleared by AdvanceAfterAction, so it
            // is per TURN and not per fight.
            //
            // The beat is still committed and played, or the ward lands with
            // no animation at all. Only the TURN does not advance.
            if (wantsFreeAction)
            {
                _freeActionTakenBy = actor;
                CommitBeat();
                return true;
            }

            CommitBeat();
            AdvanceAfterAction();
            return true;
        }

        // WHO HAS ALREADY SPENT THIS TURN'S ONE FREE ACTION. Null between
        // turns; cleared by AdvanceAfterAction rather than at turn START, so
        // the actor reference cannot outlive the turn it belongs to.
        private CombatantState _freeActionTakenBy;

        // Whether this cast leaves the turn where it found it. ONE
        // PREDICATE, two sources -- an authored skill field, and the talent
        // that predates it -- rather than a talent special case beside a
        // field: the two say exactly the same thing about the same cast, and
        // a reader asking "does this end my turn" must not have to know
        // which one answered.
        private static bool IsFreeAction(CombatantState actor, ResolvedSkill skill) =>
            skill.FreeAction
            || (skill.Effect == SkillEffect.Ward && actor.Talents.Has(TalentEffectType.WardIsFreeAction));

        // WHICH POOL'S SPEND `power` AND percentOfMaxHealthPerPoint SCALE
        // OFF. A skill scales off the pool it actually empties: the signature
        // resource ordinarily, the PRIMARY pool for a spendsAllPrimary skill
        // (Bjorn has no signature resource at all, and Second Wind's whole
        // figure is "per Fury spent"). Never both -- a cast has one number
        // that means "how much did I hoard", and summing two pools would
        // make it mean neither.
        private static int PointsSpent(ResolvedSkill skill, int resourceSpent, int primarySpent) =>
            skill.SpendsAllPrimary ? primarySpent : resourceSpent;

        // The three ways an element and a skill can fail to agree, and the copy
        // to cast when they do agree.
        //
        // `chosen` comes back null for the overwhelmingly common case -- a
        // skill that offers no choice, cast with no element -- which is what
        // lets the caller fall straight through to the cast it has always done.
        //
        // ALL THREE ARE PROGRAMMER-OR-MENU MISTAKES rather than things a player
        // can do, since the menu only offers Element depth for a skill that
        // asks for one. They are refused with a message anyway rather than
        // thrown on: Domain throws for a caller misusing a pure library, and
        // this is reached from a click handler and from the bot, where a
        // refusal that costs nothing is the house's graceful-degradation
        // posture.
        private bool TryTakeElementChoice(CombatantState actor, ResolvedSkill skill, DamageType? element,
            out ResolvedSkill chosen)
        {
            chosen = null;

            if (!skill.HasElementChoice)
            {
                if (element == null) return true;

                AppendMessage($"{skill.DisplayName} has no element to choose.");
                return false;
            }

            if (element == null)
            {
                AppendMessage($"{actor.Name} must choose an element for {skill.DisplayName}.");
                return false;
            }

            if (!skill.Offers(element.Value))
            {
                AppendMessage($"{skill.DisplayName} cannot be cast as {element.Value}.");
                return false;
            }

            chosen = skill.AsElement(element.Value);
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
        private void ResolveCharacterSkill(CombatantState actor, ResolvedSkill skill, CombatantState target,
            int resourceSpent, PoolTierResolution.Result poolTier = default)
        {
            // ONE CAST, one advance of the counter, however many things it
            // lands on. See FightSession.Potency.
            RelicsBeforeCast(actor);

            try
            {
                ResolveCharacterSkillInner(actor, skill, target, resourceSpent, poolTier);
            }
            finally
            {
                // Every path out, including the ones that resolve nothing --
                // a charge left armed would be spent by whatever acted next.
                RelicsAfterCast(actor, skill, target, resourceSpent, poolTier);
            }
        }

        private void ResolveCharacterSkillInner(CombatantState actor, ResolvedSkill skill, CombatantState target,
            int resourceSpent, PoolTierResolution.Result poolTier)
        {
            switch (skill.Effect)
            {
                case SkillEffect.DamageSingle:
                    ResolveDamageSingle(actor, skill, target, resourceSpent, poolTier);
                    break;

                case SkillEffect.DamageAll:
                    ResolveDamageAll(actor, skill, resourceSpent, poolTier);
                    break;

                case SkillEffect.HealSelf:
                {
                    BeginBeat(actor, actor, isCast: true);
                    RecordSpellPresentation(skill);
                    int amount = HealAmountOf(skill, actor, resourceSpent);

                    // THE RETURN, NOT THE REQUEST. One recipient, so there is
                    // exactly one honest number and no excuse for printing the
                    // other one: HealAndCount clamps at max health, and this
                    // arm used to pop and announce `amount` regardless, so a
                    // Mend on a full bar booked 0 in the ledger while the log
                    // said 40 and a green +40 floated over a bar that had not
                    // moved. Same shape UseConsumable and Gift: Mana already
                    // use.
                    int landed = HealAndCount(actor, amount);
                    RecordBeatAmount(landed, isHealing: true);
                    AppendMessage($"{actor.Name} uses {skill.DisplayName} and recovers {landed} HP.");
                    ApplySkillStatus(skill, actor, actor);
                    break;
                }

                case SkillEffect.HealSingle:
                {
                    // THE CHOSEN ALLY, or the caster when a caller with no
                    // pick in hand gets this far -- CastSkill has already
                    // refused a null target against a non-empty candidate
                    // list, so the fallback is only ever reached by a path
                    // that never asked (a preview, a test fixture).
                    var patient = target ?? actor;
                    BeginBeat(actor, patient, isCast: true);
                    RecordSpellPresentation(skill);
                    int amount = HealAmountOf(skill, actor, resourceSpent);

                    // THE RETURN, NOT THE REQUEST -- the same rule HealSelf
                    // above states at length: HealAndCount clamps at max
                    // health, so announcing the request would float a green
                    // +26 over a bar that did not move.
                    int landed = HealAndCount(patient, amount);
                    RecordBeatAmount(landed, isHealing: true);
                    AppendMessage(ReferenceEquals(patient, actor)
                        ? $"{actor.Name} uses {skill.DisplayName} and recovers {landed} HP."
                        : $"{actor.Name} mends {patient.Name} for {landed}.");
                    ApplySkillStatus(skill, patient, actor);
                    break;
                }

                case SkillEffect.HealParty:
                {
                    BeginBeat(actor, actor, isCast: true);
                    RecordSpellPresentation(skill);
                    int amount = HealAmountOf(skill, actor, resourceSpent);

                    // SUMMED, NOT ASSUMED -- RestorePartyMana's rule, applied
                    // to the arm two cases up that was left alone when it
                    // landed. A party heal lands a different figure on every
                    // member, so the per-ally request is the one number that
                    // is true of nobody; the SUM is what the squad gained and
                    // is what the line now says.
                    int total = 0;
                    int casterGained = 0;
                    foreach (var ally in _encounter.AlliesOf(actor).ToList())
                    {
                        int landed = HealAndCount(ally, amount);
                        total += landed;
                        if (ReferenceEquals(ally, actor)) casterGained = landed;
                        ApplySkillStatus(skill, ally, actor);
                    }

                    // The BEAT is opened on the caster (BeginBeat(actor, actor)
                    // above), so its popup floats over one bar and gets what
                    // that bar gained. The squad total belongs in the line,
                    // where it is labelled as the squad's.
                    RecordBeatAmount(casterGained, isHealing: true);
                    AppendMessage($"{actor.Name}'s {skill.DisplayName} mends the squad for {total}.");
                    break;
                }

                case SkillEffect.RestorePartyMana:
                {
                    BeginBeat(actor, actor, isCast: true);
                    RecordSpellPresentation(skill);
                    int amount = SkillResolution.Amount(skill.Effect, actor, actor, skill.Power, skill.FlatAmount, resourceSpent, false);

                    // SUMMED, NOT ASSUMED. CombatMath.RestoreMana "returns how
                    // much actually landed, so a caller can say the true number
                    // (or say nothing) rather than announcing an amount it hoped
                    // for" -- and it refuses outright for a pool whose row says
                    // restoredByManaEffects is false. This arm discarded every
                    // return and announced a squad-wide restore whatever came
                    // back, so a squad of Fury holders got a cheerful line and
                    // an unmoved bar. The bot has previewed the same cast
                    // correctly all along (Lookahead2Policy sums only the
                    // members CanRestoreMana accepts), so the policy and the log
                    // disagreed about one cast.
                    //
                    // The line still carries no NUMBER, unlike HealParty's two
                    // cases up: a party restore lands a different figure on each
                    // member and there is no one honest number to print. What it
                    // can say truthfully is whether anything landed at all.
                    int restored = 0;
                    foreach (var ally in _encounter.AlliesOf(actor).ToList())
                    {
                        restored += CombatMath.RestoreMana(ally, amount);
                    }

                    AppendMessage(restored > 0
                        ? $"{actor.Name}'s {skill.DisplayName} restores the squad's mana!"
                        : $"{actor.Name}'s {skill.DisplayName} finds nothing to restore.");
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
                    // THE BEAT LANDS ON WHOEVER WAS PICKED, so the shield
                    // pops over the bar that actually gained it. It was
                    // always the caster's own beat because the ward was
                    // always the caster's own ward.
                    var wearer = target ?? actor;
                    BeginBeat(actor, wearer, isCast: true);
                    RecordSpellPresentation(skill);
                    int warded = ApplyWard(actor, wearer, skill, resourceSpent);
                    AppendMessage(
                        warded > 1 ? $"{actor.Name} throws the fleece wide - {warded} of them are warded."
                        : ReferenceEquals(wearer, actor) ? $"{actor.Name} pulls the fleece close."
                        : $"{actor.Name} wraps {wearer.Name} in the fleece.");
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
                    // On the RECIPIENT, for the same reason the ward above
                    // is: the mana, the Empowered and the shove up the queue
                    // all happen to them, so that is the bar the number
                    // belongs over.
                    BeginBeat(actor, target ?? actor, isCast: true);
                    RecordSpellPresentation(skill);
                    ResolveGift(actor, skill, target);
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

        // WHAT A HEAL IS WORTH, in one place for all three heal effects --
        // the arithmetic lives in SkillResolution, this only decides which
        // of the skill's own authored fields are handed to it. Before this,
        // each of the three arms typed the same six-argument call and the
        // day one of them needed a seventh (percentOfMaxHealthPerPoint, for
        // Second Wind) would have been the day two of them silently did not
        // get it.
        private static int HealAmountOf(ResolvedSkill skill, CombatantState actor, int pointsSpent) =>
            SkillResolution.Amount(skill.Effect, actor, actor, skill.Power, skill.FlatAmount, pointsSpent,
                ignoresDefense: false, type: DamageType.Physical, axis: skill.ScalingAxis,
                percentOfMaxHealthPerPoint: skill.PercentOfMaxHealthPerPoint);

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

        private void ResolveDamageSingle(CombatantState actor, ResolvedSkill skill, CombatantState target,
            int resourceSpent, PoolTierResolution.Result poolTier = default)
        {
            target = target ?? _encounter.OpponentsOf(actor).FirstOrDefault();
            if (target == null) return;

            BeginBeat(actor, target, isCast: true);
            RecordSpellPresentation(skill);
            RecordPoolTier(poolTier);

            // THE SKILL'S OWN APPROACH, over the "rooted" BeginBeat assumed
            // for every cast. A melee skill is a swing that happens to be
            // authored as a skill, and until this line it connected from
            // wherever the caster was standing.
            ApproachAs(skill.Approach);

            // WHAT THE LOG/CARD CALLS THIS CAST -- "Slam" under the first
            // tier, "Slam x2"/"Slam x4" once one fires. Same string
            // FightHudModel's row caption previews before the press; this is
            // what actually happened, off the tier already chosen and spent
            // in CastSkill rather than re-picked against a pool the cast has
            // since drained.
            string castLabel = PoolTierResolution.Label(skill.DisplayName, poolTier);

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
                    AppendMessage($"{target.Name} dodges {actor.Name}'s {castLabel}!");
                    return;
                }

                AppendMessage($"{actor.Name} casts {castLabel} on {target.Name} for {damage}! -{detail}");
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

                // THE FURY TIER'S MULTIPLIER, applied to the skill's own
                // computed damage and BEFORE defences -- so a x4 slam is a x4
                // RAW hit, not a x4 hit after the target's armour already
                // took its cut. See PoolTierResolution.ApplyDamageMultiplier.
                baseAmount = PoolTierResolution.ApplyDamageMultiplier(baseAmount, poolTier);

                var outcome = DamagePipeline.AfterDefences(
                    baseAmount,
                    actor, target,
                    attackType: castType,
                    affinity: AffinityOf(target),
                    varianceRange: DamageVarianceRange,
                    rng: _rng,
                    resolveWard: ResolveWard,
                    ignoresDefense: skill.IgnoresDefense,
                    resolveDetonation: ResolveDetonation);

                if (outcome.IsMiss)
                {
                    RecordMiss();
                    AppendMessage($"{target.Name} dodges {actor.Name}'s {castLabel}!");
                    return;
                }

                damage = TotalDamage(actor, baseAmount, outcome.Damage);
                DepleteBreakShield(target, outcome.Effectiveness);
                AppendMessage($"{actor.Name} uses {castLabel} on {target.Name} for {damage} damage!{EffectivenessSuffix(outcome.Effectiveness)}");
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

        private void ResolveDamageAll(CombatantState actor, ResolvedSkill skill, int resourceSpent,
            PoolTierResolution.Result poolTier = default)
        {
            // THE SAME LABEL THE SINGLE-TARGET PATH USES -- "Rampage",
            // "Rampage x2", "Rampage x4" -- off the tier already chosen and
            // spent in CastSkill rather than re-picked against a pool the
            // cast has since drained.
            string castLabel = PoolTierResolution.Label(skill.DisplayName, poolTier);

            var summary = new StringBuilder();
            summary.Append($"{actor.Name} unleashes {castLabel}!");

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
            RecordPoolTier(poolTier);
            RecordSplashTargets(struck.Skip(1));

            // THE SKILL'S OWN APPROACH, for the same reason
            // ResolveDamageSingle takes it: a melee sweep is a swing that
            // happens to hit everybody, and until Rampage every DamageAll in
            // the game was a spell cast from where the caster stood.
            ApproachAs(skill.Approach);

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

                // THE FURY TIER'S MULTIPLIER, applied per enemy and BEFORE
                // defences -- identical treatment to ResolveDamageSingle's,
                // through the same function, so a x4 Rampage is a x4 RAW
                // sweep rather than a x4 of what each target's armour left.
                // The tier itself was chosen and PAID ONCE for the whole
                // cast (CastSkill); this only multiplies.
                baseAmount = PoolTierResolution.ApplyDamageMultiplier(baseAmount, poolTier);

                var outcome = DamagePipeline.AfterDefences(
                    baseAmount,
                    actor, enemy,
                    attackType: castType,
                    affinity: AffinityOf(enemy),
                    varianceRange: DamageVarianceRange,
                    rng: _rng,
                    resolveWard: ResolveWard,
                    ignoresDefense: skill.IgnoresDefense,
                    resolveDetonation: ResolveDetonation);

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
                    dodgeAlreadyResolved: true,
                    resolveDetonation: ResolveDetonation);

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
        //
        // THE ATTACK BONUS IS RE-ASKED, NOT READ OFF THE BOARD.
        // CombatantState.BonusAttackPercent is a snapshot of the last ACTION
        // (RefreshAttackBonus is its only writer, and it runs at cast time),
        // while this row is a promise about the NEXT one -- so reading the
        // field made the card wrong in both directions: blind to a ward bonus
        // the following cast applied, and still quoting a Gift: Fury that cast
        // had already spent. The honest figure comes from AttackBonusFor, the
        // read-only half of the same rule, asked with the same `spendingGift`
        // predicate the real cast passes at :109.
        //
        // BORROWED AND PUT BACK rather than passed down: CombatMath.ScaledAttack
        // folds the percentage into the ATTACK term before SkillResolution's
        // additive flatAmount, so there is no way to scale the finished figure
        // here without inventing a second rounding of the same rule. The field
        // is restored in a finally, so the board a preview leaves behind is
        // byte-for-byte the one it found.
        public int PreviewSkillPower(CombatantState actor, ResolvedSkill skill)
        {
            if (actor == null) return 0;

            int stashed = actor.BonusAttackPercent;
            actor.BonusAttackPercent = AttackBonusFor(actor, spendingGift: DealsDamage(skill));
            try
            {
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

                int resourceSpent = SkillResolution.ResourceToSpend(actor.SignaturePool, skill.ResourceCost,
                    skill.SpendsAllResource, skill.ResourceSpendCap);

                // THE SAME POOL THE CAST WOULD ACTUALLY EMPTY. A
                // spendsAllPrimary skill scales off the primary pool, so a
                // preview reading the signature pool would quote 0 for
                // Second Wind at full Fury -- see CastSkill's PointsSpent.
                int pointsSpent = skill.SpendsAllPrimary
                    ? (actor.PrimaryPool?.Current ?? 0)
                    : resourceSpent;

                var castType = ActorAttackType(actor) ?? DamageType.Physical;

                return SkillResolution.Amount(skill.Effect, actor, null, skill.Power,
                    skill.FlatAmount, pointsSpent, skill.IgnoresDefense, castType, skill.ScalingAxis,
                    skill.PercentOfMaxHealthPerPoint);
            }
            finally
            {
                actor.BonusAttackPercent = stashed;
            }
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

                // AND THE TWO PHASES IN FRONT OF IT, through the same door and
                // for the same reason the strike came here: a per-branch
                // SetStance is what got forgotten six times over, and a
                // per-branch pair of these would be forgotten the same way.
                RecordPhaseStances(skill.ApproachStance, skill.WindupStance);
            }
        }
    }
}
