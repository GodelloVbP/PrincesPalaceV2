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

        // THE TWO-PICK DOOR, by kit index -- the arity Palace Passage needs
        // (plan 1.12). Every other command still comes through CastSkill
        // above and is a list of one on the other side of it.
        //
        // A DIFFERENT NAME RATHER THAN AN OVERLOAD, which is a deviation from
        // the plan's own wording and is forced rather than chosen. C# cannot
        // resolve `CastSkill(0, null)` between a CombatantState parameter and
        // an IReadOnlyList<CombatantState> one, and that literal appears at
        // some forty call sites across the suite for every Self and Party
        // cast in the game. Overloading would have turned each of them into a
        // compile error and every future one into a trap. The requirement the
        // plan was actually stating -- "there is still exactly one
        // dispatcher" -- is met: both names reach the same body, and the
        // single-target one is a list of one.
        public bool CastSkillOnPicks(int index, IReadOnlyList<CombatantState> picks, DamageType? element = null)
        {
            var actor = Current;
            var kit = KitFor(actor);
            if (kit == null || index < 0 || index >= kit.Skills.Count) return false;

            return CastSkillOnPicks(kit.Skills[index], picks, element);
        }

        // A NULL TARGET STAYS A ONE-ELEMENT LIST HOLDING NULL, not an empty
        // one: "you did not aim this" and "this cast takes no target" are
        // different refusals, and the ally branch below tells them apart by
        // the difference.
        public bool CastSkill(ResolvedSkill skill, CombatantState target, DamageType? element = null)
        {
            return CastSkillOnPicks(skill, new[] { target }, element);
        }

        public bool CastSkillOnPicks(ResolvedSkill skill, IReadOnlyList<CombatantState> targets,
            DamageType? element = null) =>
            CastCore(skill, targets, element, NoDestinationSeat);

        // ---- a seat as the destination (PLAN_BELLWETHER_KIT 1.2/3.6) -----------
        //
        // PALACE PASSAGE'S DOOR: a traveller and a destination SEAT, occupied
        // or empty. An occupied seat is handed on as today's two-ally pick
        // list [traveller, occupant] -- the identical cast, refusals and
        // messages, not a second path that merely agrees with it. Only an
        // EMPTY seat, which has nobody to put in a list, rides as a seat
        // number. Every refusal is still before payment (CastCore).
        public bool CastSkillToSeat(int index, CombatantState traveller, int seat)
        {
            var kit = KitFor(Current);
            if (kit == null || index < 0 || index >= kit.Skills.Count) return false;

            return CastSkillToSeat(kit.Skills[index], traveller, seat);
        }

        public bool CastSkillToSeat(ResolvedSkill skill, CombatantState traveller, int seat)
        {
            if (skill == null) return false;

            var occupant = _encounter.OccupantOf(seat);
            return occupant != null
                ? CastCore(skill, new[] { traveller, occupant }, null, NoDestinationSeat)
                : CastCore(skill, new[] { traveller }, null, seat);
        }

        // WOULD CastSkillToSeat ACCEPT THIS RIGHT NOW? The same refusals, in
        // the same order, asked without a message or a point spent -- for a
        // bot that must never be offered a command the session refuses
        // (FightAction's own header on the stall that cost). Pure: nothing
        // here writes to the board or the log.
        public bool CanCastToSeat(int index, CombatantState traveller, int seat)
        {
            var actor = Current;
            var kit = KitFor(actor);
            if (actor == null || kit == null || index < 0 || index >= kit.Skills.Count) return false;

            var skill = kit.Skills[index];
            if (skill == null || !SkillEffects.LastPickIsSeat(skill.Effect)) return false;
            if (!EligibleAllies(actor, skill).Contains(traveller)) return false;

            var occupant = _encounter.OccupantOf(seat);
            if (occupant != null && !EligibleAllies(actor, skill).Contains(occupant)) return false;

            if (!SkillResolution.CanAfford(actor, skill.ManaCost, skill.ResourceCost, skill.HealthCostPercent))
            {
                return false;
            }

            var picks = occupant != null
                ? new[] { traveller, occupant }
                : new[] { traveller };
            if (!CanResolveSkill(actor, skill, picks, occupant != null ? NoDestinationSeat : seat, out _))
            {
                return false;
            }

            return !(IsFreeAction(actor, skill) && ReferenceEquals(_freeActionTakenBy, actor));
        }

        // "No seat named": the pick list carries every end of the cast.
        private const int NoDestinationSeat = -1;

        private bool CastCore(ResolvedSkill skill, IReadOnlyList<CombatantState> targets,
            DamageType? element, int destinationSeat)
        {
            var actor = Current;
            if (actor == null) return false;

            // AN EMPTY LIST IS A CAST NOBODY AIMED, which is the same thing
            // as a null target and must refuse the same way (AUDIT #147: "a
            // caller that hands this nothing has failed to ask"). Normalised
            // here, once, rather than guarded at each refusal below -- caught
            // by SkillDispatchTests.ACastHandedNoTargetAtAllIsRefused_-
            // ThroughEitherDoor, which found a Ward quietly landing on the
            // caster because `foreach` over an empty list checks nothing.
            if (targets == null || targets.Count == 0) targets = new CombatantState[] { null };

            // THE FIRST PICK IS "THE" TARGET for everything that has ever
            // read one. A two-pick cast's second ally is read only by the
            // resolution that knows what to do with it.
            var target = PrimaryTarget(targets);

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
            //
            // EVERY PICK, NOT ONE (plan 1.1 step 2 under 1.12). The existing
            // single-pick case is a list of one and behaves identically; a
            // Palace Passage whose SECOND ally is a corpse or a stranger is
            // refused here for the same reason and with the same sentence as
            // one whose first is.
            if (skill.Targeting == SkillTargeting.SingleAlly)
            {
                var candidates = EligibleAllies(actor, skill);
                if (candidates.Count > 0)
                {
                    foreach (var pick in targets)
                    {
                        if (candidates.Contains(pick)) continue;

                        AppendMessage(pick == null
                            ? $"{skill.DisplayName} needs an ally to aim at."
                            : $"{pick.Name} cannot take {skill.DisplayName}.");
                        return false;
                    }
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
            if (chosen != null) return CastCore(chosen, targets, null, destinationSeat);

            // MANA, SIGNATURE AND HEALTH, VALIDATED TOGETHER (plan 1.1/1.2):
            // a cast that could pay one and not another must spend neither.
            if (!SkillResolution.CanAfford(actor, skill.ManaCost, skill.ResourceCost, skill.HealthCostPercent))
            {
                AppendMessage($"{actor.Name} cannot pay for {skill.DisplayName}.");
                return false;
            }

            // Refused BEFORE anything is paid. A Shatter with nothing to
            // detonate and a Gift with nobody to give it to are both
            // conditional on board state the player can misread, and eating the
            // resource AND the turn for a cast that visibly did nothing is the
            // worst possible answer. `target` is threaded through as of
            // milestone B for requiresStatus (Ashen Reckoning) -- a
            // board-state refusal about the TARGET, which the two effects
            // above never needed to ask about.
            if (!CanResolveSkill(actor, skill, targets, destinationSeat, out string refusal))
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

            // PAID DIRECTLY, NOT THROUGH DealDamage (plan 1.2) -- a health
            // cost is a payment, not incoming damage: no ward, no
            // Protect/Vulnerable, no relic mechanic, no ledger Took row, and
            // it can never itself settle a death (HealthCost.CanPay already
            // refused a cast that would leave less than 1 HP, above).
            HealthCost.Pay(actor, skill.HealthCostPercent);

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

            ResolveCharacterSkill(actor, skill, targets, PointsSpent(skill, resourceSpent, primarySpent), poolTier,
                destinationSeat);

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

            // PLAN 1.11'S POST-ACTION HOOK, the player half of it (see
            // AdvanceAfterAction's own comment) -- this cast's OWN authored
            // classification, not the effect it resolved to. A free-action
            // cast never reaches this line at all (the early return above),
            // so it is consistent both ways with Palace Passage being
            // non-physical.
            AdvanceAfterAction(physicalMove: CombatActions.IsPhysicalMove(skill));
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
        // TAKES THE WHOLE PICK LIST, not one target, since milestone C. Every
        // arm but SwapAllies reads only the first of them, and does so
        // through the `target` local below so the bodies are untouched -- a
        // two-pick cast is the one case where the second pick is a fact about
        // the resolution rather than about the aiming.
        private void ResolveCharacterSkill(CombatantState actor, ResolvedSkill skill,
            IReadOnlyList<CombatantState> targets, int resourceSpent,
            PoolTierResolution.Result poolTier = default, int destinationSeat = NoDestinationSeat)
        {
            var target = PrimaryTarget(targets);

            // ONE CAST, one advance of the counter, however many things it
            // lands on. See FightSession.Potency.
            RelicsBeforeCast(actor);

            try
            {
                ResolveCharacterSkillInner(actor, skill, targets, resourceSpent, poolTier, destinationSeat);
            }
            finally
            {
                // Every path out, including the ones that resolve nothing --
                // a charge left armed would be spent by whatever acted next.
                RelicsAfterCast(actor, skill, target, resourceSpent, poolTier);
            }
        }

        private void ResolveCharacterSkillInner(CombatantState actor, ResolvedSkill skill,
            IReadOnlyList<CombatantState> targets, int resourceSpent, PoolTierResolution.Result poolTier,
            int destinationSeat = NoDestinationSeat)
        {
            var target = PrimaryTarget(targets);

            switch (skill.Effect)
            {
                case SkillEffect.DamageSingle:
                    ResolveDamageSingle(actor, skill, target, resourceSpent, poolTier);
                    break;

                case SkillEffect.DamageAll:
                    ResolveDamageAll(actor, skill, resourceSpent, poolTier);
                    break;

                case SkillEffect.Reclaim:
                    ResolveReclaim(actor, skill, target);
                    break;

                case SkillEffect.Hasten:
                    ResolveHasten(actor, skill, target);
                    break;

                case SkillEffect.SwapAllies:
                    ResolveSwapAllies(actor, skill, targets, destinationSeat);
                    break;

                case SkillEffect.Afflict:
                    ResolveAfflict(actor, skill, target);
                    break;

                case SkillEffect.Enthrall:
                    ResolveEnthrall(actor, skill);
                    break;

                case SkillEffect.Reposition:
                    ResolveReposition(actor, skill, target);
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
                    // AT LEAST ONE VICTIM, ALWAYS. Provoke is SingleEnemy
                    // targeting, so CastSkill's reach gate has already refused
                    // the cast unless the target is a living enemy -- and a
                    // living enemy is exactly what ApplyProvoke then provokes,
                    // whether or not ProvokeHitsEveryEnemy widens the set. The
                    // "bellows at nothing in particular" line that used to sit
                    // on a `provoked == 0` branch here was unreachable from the
                    // menu and from the bot alike (AUDIT #151); the refusal is
                    // the one path a player can see, and it is pinned by
                    // FightTalentTests.BellowingAtACorpseIsRefusedRatherThanResolved.
                    int provoked = ApplyProvoke(actor, target);
                    AppendMessage(provoked == 1
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
                    // ZERO IS REACHABLE NOW, and it must not print a line
                    // saying the fleece landed. One ward per character and
                    // the bigger pool wins (StatusEffects.ApplyWard), so a
                    // thinner ward cast over a deeper one is refused -- and
                    // WardOne has already said so in the wearer's own words.
                    int warded = ApplyWard(actor, wearer, skill, resourceSpent);
                    if (warded > 0)
                    {
                        AppendMessage(
                            warded > 1 ? $"{actor.Name} throws the fleece wide - {warded} of them are warded."
                            : ReferenceEquals(wearer, actor) ? $"{actor.Name} pulls the fleece close."
                            : $"{actor.Name} wraps {wearer.Name} in the fleece.");
                    }

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

            // A SEAT-SIZED HIT THAT SIZES TO NOTHING WHERE THE TARGET STANDS
            // (plan 1.5: the rear seat of the knell) is no hit at all -- no
            // dodge roll, no number, no riders, no hit-taken gains. The beat
            // is already open, so the cast still plays.
            if (skill.HasDamageBySeat && SeatSizedDamageBase(skill, target) <= 0)
            {
                AppendMessage($"{castLabel} passes over {target.Name}.");
                return;
            }

            // CROWNFALL'S PACKET SWAP (plan 2.4, 1.8): A READ, never a
            // consume -- Marks.IsMarked-shaped, run BEFORE the roll so the
            // choice of packet list cannot itself depend on anything the
            // roll changes. The actual spend (step 6) happens after the hit
            // has landed, below, on the same side of the dodge check
            // RelicsAfterSwing already sits on -- a dodged Crownfall consumes
            // nothing (1.8's own rule).
            bool consumesStatusPresent = skill.HasConsumesStatus
                && target.Statuses.Any(s => s.Type == skill.ConsumesStatus);
            var packets = consumesStatusPresent ? skill.DamageInstancesIfConsumed : skill.DamageInstances;

            int damage;
            if (skill.HasFixedDamage)
            {
                // A spell with authored packets deals exactly what it says, per
                // element, and reports the split.
                var detail = new StringBuilder();
                damage = ResolveDamageInstances(actor, skill, target, detail, out bool dodgedInstances, packets);

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
                var castType = CastTypeOf(actor, skill);
                DeclareOwnDamageType(skill, castType);

                // THE BASE, held so the charge can be measured against it
                // rather than against whatever the pipeline turns it into.
                // A seat-sized hit's base is the target's own bar
                // (SeatSizedDamageBase) -- no attack, rally or tier touches it.
                int baseAmount;
                if (skill.HasDamageBySeat)
                {
                    baseAmount = SeatSizedDamageBase(skill, target);
                }
                else
                {
                    baseAmount = SkillResolution.Amount(skill.Effect, actor, target, skill.Power,
                        skill.FlatAmount, resourceSpent, skill.IgnoresDefense, castType, skill.ScalingAxis);

                    // THE FURY TIER'S MULTIPLIER, applied to the skill's own
                    // computed damage and BEFORE defences -- so a x4 slam is a
                    // x4 RAW hit, not a x4 hit after the target's armour
                    // already took its cut. See
                    // PoolTierResolution.ApplyDamageMultiplier.
                    baseAmount = PoolTierResolution.ApplyDamageMultiplier(baseAmount, poolTier);
                }

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
            ApplyFinalDamage(actor, target, damage, CombatActions.IsPhysicalMove(skill),
                skill.HasFixedDamage ? (DamageType?)null : CastTypeOf(actor, skill));

            // STEP 6 OF 2.4: the consume, on a LANDED hit only -- both dodge
            // arms above already returned before this line, and a lethal hit
            // still reaches it (ApplyFinalDamage does not stop for death), so
            // "the mark is consumed on the blow that killed" is this line
            // running unconditionally on anything that was not evaded. Spends
            // the GENERAL status through the same StatusEffects.TrySpend seam
            // Marks.ConsumeMark now calls -- never the Drowned Lantern's own
            // private _marked set below, which is an independent mechanic
            // (1.8).
            if (consumesStatusPresent)
            {
                StatusEffects.TrySpend(target.Statuses, skill.ConsumesStatus.Value, out _);
            }

            // The Drowned Lantern: a damaging spell marks whatever it lands
            // on, for an attack to cash in later.
            ApplyMark(actor, target);

            // Magic Marker (mechanic a). Independent of the line above.
            MagicMarkerApplyMark(actor, target);

            if (target.IsAlive)
            {
                ApplySkillStatus(skill, target, actor);
                ApplyQueuePush(actor, skill, target);

                // A monster's hit that also drags (toSeat on a DamageSingle):
                // the same placement Reposition makes, after the hit landed.
                if (skill.ToSeat > 0) PlaceBySkill(actor, skill, target);
            }
        }

        // DARK CHAINS AND EVERY REPOSITION AFTER IT (plan 1.4/3.5,
        // SkillEffect.Reposition). One party member put in the authored seat,
        // no damage; the row's appliesStatus, if any, lands as on an Afflict.
        // No dodge roll: with no damage instance nothing enters the pipeline.
        private void ResolveReposition(CombatantState actor, ResolvedSkill skill, CombatantState target)
        {
            if (target == null) return;

            BeginBeat(actor, target, isCast: true);
            RecordSpellPresentation(skill);
            PlaceBySkill(actor, skill, target);

            if (target.IsAlive) ApplySkillStatus(skill, target, actor);
        }

        // THE ONE PLACEMENT A SKILL MAKES, through CombatEncounter.PlaceAt --
        // the rule Move and Palace Passage already share, so Rooted holds at
        // either end here exactly as it does there. Returns what PlaceAt said.
        //
        // AN ENEMY MOVING A PARTY MEMBER IS NOT THE PARTY'S DELIBERATE MOVE
        // (plan 1.4): NoteDeliberateMove is paid only when a party member
        // cast it, the way Palace Passage pays its caster.
        private PlaceOutcome PlaceBySkill(CombatantState actor, ResolvedSkill skill, CombatantState target)
        {
            int seat = skill.ToSeat - 1;
            var outcome = _encounter.PlaceAt(target, seat, out var occupant);

            switch (outcome)
            {
                case PlaceOutcome.Placed:
                    AppendMessage(occupant != null
                        ? $"{target.Name} is dragged to the {SeatWord(seat)}, trading places with {occupant.Name}."
                        : $"{target.Name} is dragged to the {SeatWord(seat)}.");
                    if (actor != null && actor.IsPlayerSide)
                    {
                        NoteDeliberateMove(target, actor);
                        if (occupant != null) NoteDeliberateMove(occupant, actor);
                    }
                    break;
                case PlaceOutcome.MemberRooted:
                    AppendMessage($"{target.Name} is rooted and does not move.");
                    break;
                case PlaceOutcome.OccupantRooted:
                    AppendMessage($"{occupant?.Name ?? "Whoever stands there"} is rooted, and {target.Name} does not move.");
                    break;
                case PlaceOutcome.NoSuchSeat:
                    // Already in that seat (plan 2.5: chains on a Shawn who
                    // is already front move nothing).
                    AppendMessage($"{target.Name} already stands at the {SeatWord(seat)}.");
                    break;
                default:
                    // NotOnTheField: an enemy, or nobody alive to move.
                    break;
            }

            return outcome;
        }

        // THE BASE OF A SEAT-SIZED HIT (ResolvedSkill.DamageBySeatMaxHpPercent,
        // plan 3.4 as amended): the entry for the seat `target` stands in NOW,
        // as a percent of the target's own max health, rounded away from zero
        // and floored at 1. 0 means no hit -- a 0 entry, a dead or absent
        // target, or a skill that is not seat-sized. An enemy's seat is its
        // living rank (CombatEncounter.SeatOf); a rank past the rear reads the
        // rear entry.
        //
        // PUBLIC: the resolution, the telegraph (PreviewSkill) and M4's intent
        // badge all read this one figure, so none can size the hit differently.
        public int SeatSizedDamageBase(ResolvedSkill skill, CombatantState target)
        {
            if (skill == null || !skill.HasDamageBySeat || target == null) return 0;
            return SeatSizedDamageBase(skill, target, _encounter.SeatOf(target));
        }

        // The same figure for `seat` rather than the one the target stands in:
        // the intent's per-seat table ("610 at the front, 180 in the middle").
        public int SeatSizedDamageBase(ResolvedSkill skill, CombatantState target, int seat)
        {
            if (skill == null || !skill.HasDamageBySeat || target == null || !target.IsAlive) return 0;
            if (seat < 0) return 0;

            var table = skill.DamageBySeatMaxHpPercent;
            int percent = table[System.Math.Min(seat, table.Length - 1)];
            if (percent <= 0) return 0;

            return System.Math.Max(1, Rounding.AwayFromZero(target.MaxHealth * (percent / 100f)));
        }

        // THE TYPE A SKILL'S ATTACK-SCALED (OR SEAT-SIZED) DAMAGE IS DEALT
        // AS: the skill's own damageType when it authors one, else the
        // caster's attackType, else Physical. The one reading every
        // resolver, preview and card label shares (plan 3.3).
        public DamageType CastTypeOf(CombatantState actor, ResolvedSkill skill) =>
            skill?.OwnDamageType ?? ActorAttackType(actor) ?? DamageType.Physical;

        // A skill that types its own damage paints its own beat -- the
        // controller's actor paint would otherwise colour the Void knell's
        // popup as its Physical caster's swing (CombatBeat.DeclareDamageType).
        private void DeclareOwnDamageType(ResolvedSkill skill, DamageType castType)
        {
            if (skill.OwnDamageType.HasValue) _recordingBeat?.DeclareDamageType(castType);
        }

        // ASHEN RECKONING (plan 2.5, SkillEffect.Reclaim). No packet of its
        // own: `damage` does not exist until a target's Poison has been
        // consumed, which is why this is a member rather than a
        // DamageSingle variant (D10) -- HasFixedDamage is false and
        // skill.DamageInstances is empty for a Reclaim row.
        private void ResolveReclaim(CombatantState actor, ResolvedSkill skill, CombatantState target)
        {
            target = target ?? _encounter.OpponentsOf(actor).FirstOrDefault();
            if (target == null) return;

            BeginBeat(actor, target, isCast: true);
            RecordSpellPresentation(skill);

            // DODGE, ONCE, BEFORE ANYTHING IS CONSUMED (2.5 step 3). Rolled
            // here rather than inside a shared packet resolver, because by
            // the time ResolveDamageInstances would roll it the Poison this
            // cast lives on would already be gone -- a dodged Reckoning must
            // leave the target's Poison standing untouched.
            if (DamagePipeline.RollDodge(target, actor, _rng))
            {
                RecordMiss();
                AppendMessage($"{target.Name} dodges {actor.Name}'s {skill.DisplayName}!");
                return;
            }

            // ONE DETONATION AT THE SKILL'S OWN PREMIUM (1.6/1.7/D2): every
            // Poison instance on the target, summed, removed in one
            // consumption, the premium applied once to the total. No caster
            // scaling here -- the figure IS the snapshot Poison already took,
            // marked up once.
            int worth = StatusCombos.SpendPoisonIfMatched(target, DamageType.Poison, skill.DetonationPercent);
            if (worth <= 0)
            {
                // Reachable only for a Poison pile that summed to exactly
                // zero (every live instance's own Magnitude was 0) --
                // CanResolveSkill's requiresStatus refusal already turned
                // away a target carrying none at all. Nothing to split,
                // nothing to deal, and no status: the ordinary "landed on
                // nothing" shape every other effect already has.
                AppendMessage($"{actor.Name}'s {skill.DisplayName} finds nothing left in {target.Name} to reclaim.");
                return;
            }

            var split = ConsumedTotalSplit.Split(worth, skill.DetonationSplit);

            // EACH PACKET RESOLVES INDEPENDENTLY (1.7), with
            // resolveDetonation LEFT NULL -- the consumption already
            // happened above, so the Poison-typed half of this very split
            // cannot re-enter SpendPoisonIfMatched and eat what it is itself
            // carrying (AshenReckoningTests.
            // TheReckoningsPoisonHalf_TriggersNoSecondDetonation).
            var detail = new StringBuilder();
            int total = 0;
            foreach (var packet in split)
            {
                var outcome = DamagePipeline.AfterDefences(
                    packet.amount, packet.type, target,
                    affinity: AffinityOf(target),
                    varianceRange: DamageVarianceRange,
                    rng: _rng,
                    resolveWard: ResolveWard,
                    attacker: actor,
                    dodgeAlreadyResolved: true,
                    resolveDetonation: null);

                DepleteBreakShield(target, outcome.Effectiveness);
                total += outcome.Damage;
                detail.Append($" {outcome.Damage} {packet.type}{EffectivenessSuffix(outcome.Effectiveness)}");
            }

            AppendMessage($"{actor.Name}'s {skill.DisplayName} tears {worth} out of {target.Name} and returns it for {total}! -{detail}");

            // ONE DEATH SETTLEMENT ACROSS THE SPLIT (1.7): ApplyFinalDamage
            // runs once, on the packets' sum -- the same shape every other
            // fixed-packet spell already uses for a multi-typed cast
            // (ResolveDamageSingle's HasFixedDamage arm), not something this
            // spell invents.
            ApplyFinalDamage(actor, target, total, CombatActions.IsPhysicalMove(skill));

            ApplyMark(actor, target);
            MagicMarkerApplyMark(actor, target);

            if (target.IsAlive)
            {
                ApplySkillStatus(skill, target, actor);
            }
        }

        // THE FIRST PICK OF A CAST, or null when there is none. One reading
        // of "the target", so a cast handed an empty list and a cast handed a
        // list holding null cannot start meaning different things to
        // different arms of the resolution.
        private static CombatantState PrimaryTarget(IReadOnlyList<CombatantState> targets) =>
            targets != null && targets.Count > 0 ? targets[0] : null;

        // VELVET SHACKLES AND EVERY AFFLICT AFTER IT (plan 2.10,
        // SkillEffect.Afflict). One enemy, this row's authored status, no
        // damage at all.
        //
        // IT IS FOUR LINES BECAUSE THE SEAM ALREADY DOES THE WORK.
        // ApplySkillStatus reads appliesStatus / statusMagnitude /
        // statusDuration off the row and goes through ApplyStatusTo (plan D6),
        // which is where Chilled's speed bookkeeping and every future status's
        // hangs -- so Censer of Embers and Thorn Tithe join this arm as
        // content rows and no code, which is the whole reason Afflict is a
        // member rather than three spell-shaped branches.
        //
        // NO DODGE ROLL, and that is a rule rather than an omission: with no
        // damage instance the cast never enters AfterDefences, so an Afflict
        // lands or was refused before payment. Nothing in between.
        //
        // THE TARGET IS LIVE BY CONSTRUCTION -- SingleEnemy targeting means
        // CastSkill's reach gate has already refused a cast aimed at nobody,
        // and ApplySkillStatus re-checks IsAlive anyway for the case where a
        // rider killed the target between the two.
        private void ResolveAfflict(CombatantState actor, ResolvedSkill skill, CombatantState target)
        {
            if (target == null) return;

            BeginBeat(actor, target, isCast: true);
            RecordSpellPresentation(skill);
            ApplySkillStatus(skill, target, actor);
        }

        // BORROWED MOMENT (plan 2.7, SkillEffect.Hasten). The refusals have
        // already run -- CanResolveSkill has established that this ally is in
        // the order and is not already at forecast position 1 -- so this is
        // the movement and the sentence about it, and nothing else.
        //
        // NO DAMAGE, NO STATUS, NOTHING LEFT BEHIND: the whole cast is a
        // position, which is why it carries no dodge roll, no ledger row and
        // no mark.
        private void ResolveHasten(CombatantState actor, ResolvedSkill skill, CombatantState target)
        {
            if (target == null) return;

            BeginBeat(actor, target, isCast: true);
            RecordSpellPresentation(skill);

            int before = _encounter.ForecastPositionOf(target);
            if (!_encounter.PullForward(target, skill.AdvanceSlots))
            {
                return;
            }

            int after = _encounter.ForecastPositionOf(target);

            // SAYS WHERE THEY LANDED, not how many slots were spent. A slot
            // moves past a charge LEVEL, so one slot can be worth two places
            // when two combatants are tied and worth none when the level
            // above is already the next action -- the number the player can
            // check against the tracker is the one worth printing.
            int places = before < 0 || after < 0 ? 0 : before - after;
            AppendMessage(places > 0
                ? $"{target.Name} moves {places} place{(places == 1 ? "" : "s")} earlier in the order."
                : $"{target.Name} is hurried, but there is nowhere earlier to go.");
        }

        // PALACE PASSAGE (plan 2.9/1.12, SkillEffect.SwapAllies). Two allies
        // trade FIELD places -- not turn-order places -- as the caster's one
        // free action.
        //
        // REUSES Move's WHOLE MECHANISM and adds nothing to it: the same
        // placement rule (CombatEncounter.PlaceAt), the same NoteDeliberateMove pair with the acting
        // character named, and the same recomputed-not-stored formation
        // (CombatEncounter.LivingRankOf). What differs from Move is only who
        // chooses the pair and what it costs, and both of those were settled
        // before this method was reached.
        //
        // THE ROOTED REFUSAL IS NOT HERE. It is a CanResolveSkill refusal, so
        // it spends nothing -- see 1.12. By the time this runs the cast is
        // committed and the swap must happen.
        //
        // ANY SEAT SINCE PLAN_BELLWETHER_KIT M3: the second end is a seat.
        // An occupied one arrives as the old pick list and trades; an empty
        // one arrives as `destinationSeat` and the traveller steps into it
        // (PassageDestination is the one reading of the two forms).
        private void ResolveSwapAllies(CombatantState actor, ResolvedSkill skill, IReadOnlyList<CombatantState> targets,
            int destinationSeat)
        {
            var traveller = PrimaryTarget(targets);
            int seat = PassageDestination(targets, destinationSeat);
            if (traveller == null || seat < 0) return;
            if (IndexInParty(_encounter.PlayerParty, traveller) < 0) return;

            BeginBeat(actor, traveller, isCast: true);

            // THROUGH THE SAME DOOR EVERY OTHER RESOLVER USES. This arm used
            // to pose the caster with a bare SetStance(Idle) and never call
            // RecordSpellPresentation, so palace_passage's authored layers
            // (both "target-centre", resolving against targets[0] -- the
            // beat's Target set above, before the swap moves anyone) never
            // reached the beat and the cast played with no VFX at all.
            // RecordSpellPresentation both attaches the layers and poses the
            // caster (StanceFor falls back to "cast" since no SwapAllies
            // skill has authored a stance), so the manual Idle is gone too.
            RecordSpellPresentation(skill);

            // THE ONE PLACEMENT RULE (CombatEncounter.PlaceAt): the traveller
            // goes to the destination seat, trading with its occupant -- the
            // two-ally swap, exactly -- or stepping into it when it is empty.
            if (_encounter.PlaceAt(traveller, seat, out var occupant) != PlaceOutcome.Placed) return;

            AppendMessage(occupant != null
                ? $"{traveller.Name} and {occupant.Name} step through and trade places."
                : $"{traveller.Name} steps through to the empty {SeatWord(seat)}.");

            // EVERY figure that moved is noted, with the CASTER as the acting
            // character -- Sparring Buckler pays whoever acted for a move that
            // changed any position, Sparring Saber pays only the one who chose
            // to move. On a Passage the chooser is the caster, who may not be
            // either traveller, and NoteDeliberateMove's own header is what
            // says that distinction is the point of the pair.
            NoteDeliberateMove(traveller, actor);
            if (occupant != null) NoteDeliberateMove(occupant, actor);
        }

        // WHERE A PASSAGE SENDS ITS TRAVELLER: the named empty seat, or --
        // for the two-ally form -- the second ally's seat. -1 when the cast
        // names neither, which the refusal in CanResolveSkill turns into a
        // sentence before anything is paid.
        private int PassageDestination(IReadOnlyList<CombatantState> targets, int destinationSeat)
        {
            if (destinationSeat >= 0) return destinationSeat;

            return targets != null && targets.Count >= 2 && targets[1] != null
                ? _encounter.SeatOf(targets[1])
                : NoDestinationSeat;
        }

        private static int IndexInParty(IReadOnlyList<CombatantState> party, CombatantState member)
        {
            if (party == null || member == null) return -1;

            for (int i = 0; i < party.Count; i++)
            {
                if (ReferenceEquals(party[i], member)) return i;
            }

            return -1;
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
            var castType = CastTypeOf(actor, skill);
            DeclareOwnDamageType(skill, castType);

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

            // WHO ACTUALLY TOOK A HIT, for the queue delay this sweep may
            // carry (Gale Scythe, plan 2.8 step 5). Collected rather than
            // re-derived afterwards, because "was hit" is not a question the
            // board can answer once the cast is over: a dodger and a
            // survivor who was struck look identical from the outside, and
            // only one of them loses a place.
            var struckAndLanded = new List<CombatantState>();

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

                    ApplyFinalDamage(actor, enemy, packetTotal, CombatActions.IsPhysicalMove(skill));
                    RecordTargetResult(enemy, packetTotal);
                    struckAndLanded.Add(enemy);

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
                ApplyFinalDamage(actor, enemy, landed, CombatActions.IsPhysicalMove(skill), castType);

                RecordTargetResult(enemy, landed);
                struckAndLanded.Add(enemy);

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

            // AFTER EVERY ENEMY HAS RESOLVED AND EVERY DEATH HAS SETTLED
            // (plan 1.9 rule 6, 2.8 steps 4-5). Not inside the loop: a
            // destination computed while the sweep was still killing things
            // would be measured against a board the player never sees.
            ApplyQueuePushAll(actor, skill, struckAndLanded);

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

        // `packets` DEFAULTS TO THE SKILL'S OWN, and is overridable for
        // exactly one reason (plan 2.4): Crownfall reads whether the target
        // is Marked BEFORE this call and hands in the heavier
        // damageInstancesIfConsumed list instead of skill.DamageInstances --
        // everything else about resolving those packets (dodge once, scale
        // once, resolve each through AfterDefences) is identical either way.
        private int ResolveDamageInstances(CombatantState actor, ResolvedSkill skill, CombatantState target,
            StringBuilder detail, out bool dodged, DamageInstance[] packets = null)
        {
            dodged = DamagePipeline.RollDodge(target, actor, _rng);
            if (dodged)
            {
                return 0;
            }

            int total = 0;
            float multiplier = SkillPowerMultiplierFor(actor) * SpellScalingMultiplierFor(actor);

            foreach (var instance in packets ?? skill.DamageInstances)
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
                    // THE REPAIR (plan 1.3/1.6 item 1): this call used to omit
                    // ignoresDefense entirely, so a fixed-packet spell's flag
                    // was inert -- true on the raw entry, false at every
                    // resolution, and nobody the wiser because no live
                    // content had authored the combination until Blackglass
                    // Spear. The scaled path (ResolveDamageSingle's `else`
                    // arm, just below in this file) already passed it; this
                    // is the one call that did not.
                    ignoresDefense: skill.IgnoresDefense,
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
                        // THE ELEMENTAL RIDER TOO. This branch multiplied by
                        // tier and spell scaling only until 2026-09-20, while
                        // ResolveDamageInstances applied ElementalDamagePercent
                        // as well -- so a caster wearing a +Fire item was shown
                        // one figure on the power row and dealt another, and
                        // the two disagreed silently because nothing compared
                        // them. The arithmetic here is deliberately spelled the
                        // same way the resolution path spells it, in the same
                        // order, so the two round identically;
                        // PreviewPurityTests compares them rather than trusting
                        // that they look alike.
                        float elemental = 1f + ElementalDamagePercentFor(actor, instance.type) / 100f;
                        total += System.Math.Max(1,
                            Rounding.AwayFromZero(instance.amount * multiplier * elemental));
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

                var castType = CastTypeOf(actor, skill);

                return SkillResolution.Amount(skill.Effect, actor, null, skill.Power,
                    skill.FlatAmount, pointsSpent, skill.IgnoresDefense, castType, skill.ScalingAxis,
                    skill.PercentOfMaxHealthPerPoint, skill.PercentOfCasterMaxHealth);
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
            ApplyQueuePushAll(actor, skill, new[] { target });
        }

        // THE SAME DELAY OVER SEVERAL TARGETS AT ONCE -- Gale Scythe's sweep
        // (plan 2.8), and the arity the Black Ram's Headbutt is a list of one
        // of. Two arities, one rule, for the same reason CastSkill has two:
        // the single-target path was here first and must not change.
        //
        // SURVIVORS ONLY, AND ONE FORECAST (1.9 rules 1 and 6). The dead are
        // filtered here rather than by the caller because "is this one still
        // standing" is a question about the board at the moment of the
        // displacement, and the caller's list was assembled while the sweep
        // was still resolving. CombatEncounter.PushBackAll then fixes every
        // destination against the board as it stood before any of them moved.
        //
        // IN FORECAST ORDER, which changes nothing about where anybody lands
        // -- the destinations are computed independently -- and everything
        // about the order the lines are printed in. The log is read against
        // the tracker, so it reads in the tracker's order.
        private void ApplyQueuePushAll(CombatantState actor, ResolvedSkill skill,
            IReadOnlyList<CombatantState> targets)
        {
            if (skill.QueuePushSlots <= 0 || targets == null || targets.Count == 0) return;

            var survivors = targets.Where(t => t != null && t.IsAlive).Distinct().ToList();
            if (survivors.Count == 0) return;

            if (survivors.Count > 1)
            {
                var forecast = _encounter.UpcomingTurns(_encounter.ForecastWindow).ToList();
                survivors = survivors
                    .OrderBy(t =>
                    {
                        int at = forecast.IndexOf(t);
                        return at < 0 ? int.MaxValue : at;
                    })
                    .ToList();
            }

            if (_encounter.PushBackAll(survivors, skill.QueuePushSlots) == 0) return;

            foreach (var survivor in survivors)
            {
                ApplyQueuePushTo(actor, survivor);
            }
        }

        private void ApplyQueuePushTo(CombatantState actor, CombatantState target)
        {
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

        private void ResolveEnthrall(CombatantState actor, ResolvedSkill skill)
        {
            var enemies = _encounter.OpponentsOf(actor).Where(e => e != null && e.IsAlive).ToList();
            BeginBeat(actor, enemies.FirstOrDefault(), isCast: true);
            RecordSpellPresentation(skill);

            var bosses = new List<CombatantState>();
            foreach (var enemy in enemies)
            {
                if (SourceFor(enemy)?.Source?.IsBoss == true)
                {
                    bosses.Add(enemy);
                    continue;
                }

                if (HardControlRecoveryBlocks(enemy, StatusEffectType.Feared))
                {
                    AppendMessage($"{enemy.Name} steels itself against another hard control.");
                }
                else
                {
                    Fear.Apply(enemy, Fear.DefaultTurns, actor);
                    AppendMessage($"{enemy.Name} recoils from the whispering court.");
                }
            }

            if (bosses.Count > 0)
            {
                _encounter.PushBackAll(bosses, skill.QueuePushSlots);
                foreach (var boss in bosses)
                {
                    AppendMessage($"{boss.Name} resists the fear but is delayed.");
                }
            }

            ApplySkillStatus(skill, actor, actor);
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

            if (HardControlRecoveryBlocks(recipient, type))
            {
                AppendMessage($"{recipient.Name} resists another hard control until it acts.");
                return;
            }

            // THROUGH THE SEAM, not through StatusEffects.Apply. This line read
            // `StatusEffects.Apply(...)` until 2026-09-20 and was the latent
            // half of plan D6: a content row authoring `appliesStatus: Chilled`
            // would have put the badge up and slowed nobody, because
            // ApplyChilled is the only path that registers the speed malus.
            // Winter's Rebuke is the first row that authors one.
            ApplyStatusTo(recipient, type, skill.StatusMagnitude, skill.StatusDuration, caster);

            bool isBeneficial = type == StatusEffectType.Regen || type == StatusEffectType.Protect;
            AppendMessage(isBeneficial
                ? $"{recipient.Name} gains {type}!"
                : $"{recipient.Name} is afflicted with {type}!");
        }

        private void RaiseMagicalShield(CombatantState actor)
        {
            if (!HasRelic(actor, RelicEffect.MagicalShield)) return;

            // ANOTHER ENTRY, never a replacement: wards stack, so a relic
            // shield raised over a Bulwark adds to it rather than arguing with
            // it. MagicalShieldDurationTurns rather than the skills' one-turn
            // default, because this relic's promise is a shield that stands
            // until something spends it.
            RaiseWard(actor, FightTuning.MagicalShieldPoints, FightTuning.MagicalShieldDurationTurns, actor);
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
