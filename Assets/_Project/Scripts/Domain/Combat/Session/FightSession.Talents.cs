using System.Collections.Generic;
using System.Linq;
using System.Text;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // The combat-time half of the talent vocabulary: the three wool engines,
    // the Fragile Lamb's wards, the Black Ram's transform and splashes, and
    // Provoke.
    //
    // Ported from v1's FightController.Talents.cs, which is the single largest
    // body of pure rules that was trapped behind a MonoBehaviour. Nothing here
    // touches the engine; the reasoning comments are carried over intact,
    // because every one of them records a decision rather than a mechanic.
    public sealed partial class FightSession
    {
        // ---- the wool engines -------------------------------------------------

        // How much wool `actor` should gain at the start of this turn, all three
        // engines and the baseline considered.
        //
        // The health-gated tiers REPLACE the baseline; everything else adds to
        // whatever wins. Replace keeps the Ram's ceiling at +3 a turn before
        // on-hit gains, which is the economy the ability prices were sanity-
        // checked against; stacking takes it to +4 and makes a 7-cost transform
        // a two-turn purchase from full health.
        private int SignaturePerTurnFor(CombatantState actor)
        {
            var signature = actor?.SignaturePool;
            if (signature == null) return 0;

            int perTurn = signature.GainPerTurn;

            // The Black Ram: the lower he falls, the faster the fleece comes
            // in. BestBelowHealth picks the single strongest SATISFIED tier
            // rather than summing the tiers he has passed.
            int wounded = actor.Talents.BestBelowHealth(TalentEffectType.WoolPerTurnBelowHealth, actor);
            if (wounded > perTurn)
            {
                perTurn = wounded;
            }

            // Provoke T3: paid for being the one everything is aimed at. Adds
            // on top, because this is income he ARRANGED rather than income his
            // health handed him -- two different engines inside one path.
            int perProvoked = actor.Talents.Best(TalentEffectType.WoolPerProvokedEnemy);
            if (perProvoked > 0)
            {
                perTurn += perProvoked * StatusEffects.CountAfflictedBy(
                    _encounter.Enemies, actor, StatusEffectType.Provoked);
            }

            // The mage path's engine: paid for board state. Same additive
            // reasoning, and mutually exclusive with the Ram's tiers in
            // practice anyway -- you may only ever hold one root.
            int perStatused = actor.Talents.Best(TalentEffectType.WoolPerStatusedEnemy);
            if (perStatused > 0)
            {
                perTurn += perStatused * StatusEffects.CountAfflictedBy(_encounter.Enemies, actor);
            }

            return System.Math.Max(0, perTurn);
        }

        // ---- the Fragile Lamb --------------------------------------------------

        // Gift: Fury is spent by the next SWING, not by a clock, so its
        // duration only has to be long enough that ordinary ticking can never
        // take it first -- the same "generous rather than infinite" trick
        // every ward in the game used to use before wards got real clocks.
        private const int GiftFuryDurationTurns = 999;

        // HOW LONG THIS WARD STANDS: the skill's own authored `wardTurns`,
        // already defaulted to FightTuning.DefaultWardTurns by the resolver,
        // and nothing else.
        //
        // THE GOLDEN FLEECE IS NOT READ HERE ANY MORE. It used to hand this a
        // sentinel duration, which meant the capstone was baked into a ward at
        // the moment it went up: buying the node did nothing for the wards
        // already out, and the WEARER's talents were what got asked in the
        // version before that. It belongs to the CASTER and it is a live
        // question every tick now -- StatusEffects.NeverExpires owns it.
        private static int WardTurnsFor(ResolvedSkill skill) =>
            skill?.WardTurns ?? FightTuning.DefaultWardTurns;

        // Which caster has already been paid for which wearer this turn.
        //
        // The cap is once per caster per warded combatant per turn, and it is
        // the only belt left holding a real problem: a ward that paid on every
        // hit it absorbed would return more wool than it cost and the economy
        // would run backwards. It used to be keyed on the WEARER alone, which
        // was the same thing while only one ward could stand on anybody; wards
        // stack now, so two casters covering one tank have to be able to be
        // paid for their own work without paying either of them twice.
        //
        // A caster's own entries are dropped when HIS turn begins, which is
        // what makes "per turn" mean a round of his rather than a round of
        // anyone's. Only his: clearing the whole set on any warder's turn
        // would reset a second Lamb's cap on the first one's turn.
        private readonly HashSet<(CombatantState caster, CombatantState wearer)> _wardPayoutsThisTurn =
            new HashSet<(CombatantState caster, CombatantState wearer)>();

        // A ward's clock runs at the END of the wearer's own turn and does not
        // count the wearer's turn it was raised on (StatusEffects' own WARDS
        // header). Which wards are spared is SpareIfAppliedOnWearersTurn's call,
        // declared with the ApplyStatusTo seam in FightSession.Riders.cs -- a
        // ward is no longer the only thing that needs the exemption.

        // THE ONE PLACE A WARD GOES UP. Every ward in the game -- the five
        // skills through WardOne, and the three relic wards -- comes through
        // here, so the "does not count the turn it was raised on" rule has one
        // home rather than four call sites that each have to remember it.
        private void RaiseWard(CombatantState wearer, int points, int turns, CombatantState source)
        {
            var ward = StatusEffects.ApplyWard(wearer.Statuses, points, turns, source);
            SpareIfAppliedOnWearersTurn(wearer, ward);
        }

        // Counts the ending actor's turn-end statuses down and says what
        // lapsed. Called from AdvanceAfterAction, which is the end of a turn.
        //
        // It used to be wards only. Protect, Vulnerable, Chilled, Rooted and
        // Marked joined that clock with plan D1, so this is where five more
        // statuses now expire -- and where the one teardown any of them has
        // (Chilled's speed malus) is settled.
        private void TickStatusesAtTurnEnd(CombatantState actor)
        {
            if (actor == null) return;

            var expired = StatusEffects.TickAtTurnEnd(actor, _sparedAtWearersTurnEnd);
            if (expired.Count == 0) return;

            // A SHIELD SAYS SOMETHING DIFFERENT, and says it by the count
            // rather than by name -- the wording the ward clock has always
            // used, kept because "Shawn's Shielded wears off" is not a
            // sentence about a shield running out.
            int shields = 0;
            foreach (var type in expired)
            {
                if (type == StatusEffectType.Shielded) shields++;
            }

            if (shields > 0)
            {
                AppendMessage(shields == 1
                    ? $"The shield around {actor.Name} fades."
                    : $"{shields} shields around {actor.Name} fade.");
            }

            // CHILLED'S MALUS IS A RECOMPUTE, NOT A REVOKE. The malus is booked
            // in FightSession.SpeedBuffs' dictionary rather than on the status,
            // and Chilled stacks -- one instance lapsing out of three must
            // leave the other two slowing. RefreshChilledSpeed revokes and
            // re-grants from the CURRENT sum, so the same call covers "some
            // went" and "the last one went" and there is no "was that the last
            // one?" question for a future edit to get wrong.
            if (expired.Contains(StatusEffectType.Chilled))
            {
                RefreshChilledSpeed(actor);
            }

            // DISTINCT, because statuses stack -- see the same loop at the turn
            // START in FightSession.Riders.TickStatuses.
            foreach (var type in expired.Distinct())
            {
                if (type == StatusEffectType.Shielded) continue;
                AppendMessage($"{actor.Name}'s {type} wears off.");
            }
        }

        // What the wards on a combatant do to one incoming hit: absorb it, pay
        // whoever put them up, heal the wearer as they break, and start the
        // grace period on a self-ward that ran out.
        //
        // Called from inside the damage funnel, which is where the existing
        // Shielded consumption already lived -- so every typed and untyped
        // damage path reaches it without any of them knowing about wards.
        //
        // WALKS EVERY ENTRY THE HIT TOUCHED, because a hit big enough to reach
        // through two pools is two casters' work and the engine owes both of
        // them. StatusEffects.ConsumeWard hands the entries back in drain
        // order; everything below is per-entry and additive.
        private int ResolveWard(CombatantState target, int damage)
        {
            var outcome = StatusEffects.ConsumeWard(target, damage);

            // CombatBeat.Absorbed's OTHER write site (the first is
            // FightSession.Ledger.ApplyAndCountDamage, for a signature
            // pool's own absorb) -- both shield kinds report through the
            // one field this way, additively, so a beat that found ward AND
            // signature-pool cover in the same swing reports the sum of
            // both rather than whichever happened to write last. Reuses
            // outcome.Absorbed, already computed by ConsumeWard two lines
            // up; nothing here recomputes an absorption figure. Recorded
            // whether or not there were any Hits to walk below -- a hit
            // with nothing left to drain (ASecondHit_FindsWhateverTheFirst
            // OneLeft, WardTests) reports outcome.Absorbed == 0 anyway, so
            // this line is a no-op for it without needing its own guard.
            RecordAbsorbed(outcome.Absorbed);

            if (outcome.Hits.Count == 0) return outcome.Damage;

            foreach (var hit in outcome.Hits)
            {
                var caster = hit.Caster;
                if (caster == null) continue;

                // The engine. Paid for a ward of yours TAKING a blow, not for
                // it breaking -- which is what lets the capstone's
                // never-expiring wards keep earning instead of silently ending
                // his income the moment he finishes his tree.
                int reward = caster.Talents.Best(TalentEffectType.WoolWhenWardedAllyHit);
                if (reward > 0 && caster.IsAlive && _wardPayoutsThisTurn.Add((caster, target)))
                {
                    GrantSignature(caster, reward);
                    AppendMessage(ReferenceEquals(caster, target)
                        ? $"{caster.Name}'s own ward takes the blow - the fleece thickens."
                        : $"{caster.Name}'s ward on {target.Name} is struck - the fleece thickens.");
                }

                if (hit.Healed > 0)
                {
                    AppendMessage($"The ward breaks over {target.Name} and knits {hit.Healed} back.");
                }
                else if (hit.Broke)
                {
                    AppendMessage($"The shield over {target.Name} gives way.");
                }

                // Weight of Wool T3. Only a SELF-ward starts the grace period:
                // the bonus it protects is the self bonus, and an ally's ward
                // popping was never worth 2x to begin with.
                //
                // Asked of THIS caster's own wards rather than of IsWarded,
                // which now answers for anybody's: a relic ward still standing
                // must not stop his grace period starting.
                if (hit.Broke && ReferenceEquals(caster, target)
                    && !StatusEffects.IsWardedBy(target, caster))
                {
                    int grace = caster.Talents.Best(TalentEffectType.WardSelfBonusPersistsTurns);
                    if (grace > 0)
                    {
                        target.SelfWardGraceTurns = grace;
                    }
                }
            }

            return outcome.Damage;
        }

        // Everything Weight of Wool and Gift: Fury add to a swing, summed onto
        // the combatant so CombatMath can read it without being handed the whole
        // encounter.
        //
        // THE ONLY writer of BonusAttackPercent, called immediately before any
        // damage the actor deals. `spendingGift` is false for a cast that heals
        // or buffs -- Gift: Fury is "your next ATTACK", and burning it on a Ward
        // would be a gift the player never got.
        //
        // The ARITHMETIC lives in AttackBonusFor below and this is the only
        // thing that writes the field or spends the gift, because the skill
        // card has to ask the same question about a cast that has not happened
        // yet. It read the leftover field instead until 2026-09-11, which is a
        // snapshot of the PREVIOUS action's board: the card missed a ward bonus
        // the very next cast applied, and went on quoting a Gift: Fury that
        // cast had already burned.
        private void RefreshAttackBonus(CombatantState actor, bool spendingGift)
        {
            if (actor == null) return;

            actor.BonusAttackPercent = AttackBonusFor(actor, spendingGift);

            // The return is deliberately dropped: AttackBonusFor already
            // counted the gift's magnitude. This call is the SPENDING of it,
            // and it is the one line the read-only sibling must not have.
            if (spendingGift)
            {
                StatusEffects.ConsumeEmpowerment(actor);
            }
        }

        // What the actor's attack bonus WOULD be for an action of this shape.
        // Pure: writes no field, spends no gift, so the skill-detail card can
        // ask it about the cast the player is looking at (FightSession.Skills's
        // PreviewSkillPower) without the hover changing the fight.
        private int AttackBonusFor(CombatantState actor, bool spendingGift)
        {
            if (actor == null) return 0;

            int bonus = 0;

            int perAlly = actor.Talents.Best(TalentEffectType.WardDamageBonusPerAlly);
            if (perAlly > 0)
            {
                foreach (var ally in _encounter.PlayerParty)
                {
                    if (!ReferenceEquals(ally, actor) && ally.IsAlive && WardedByThisActor(ally, actor))
                    {
                        bonus += perAlly;
                    }
                }
            }

            int self = actor.Talents.Best(TalentEffectType.WardDamageBonusSelf);
            if (self > 0 && (WardedByThisActor(actor, actor) || actor.SelfWardGraceTurns > 0))
            {
                bonus += self;
            }

            if (spendingGift)
            {
                bonus += StatusEffects.EmpowermentWorth(actor);
            }

            // The per-round rally, for ANY actor that carries stacks: the
            // stacks are the state, the per-stack figure is the actor's own
            // authored rally (FightSession.Rounds). Uncapped here -- the cap is
            // on the stack count, applied as each stack is added.
            bonus += RallyAttackPercent(actor);

            return bonus;
        }

        // THROUGH IsWardedBy, not through WardedBy. With wards stacking,
        // "who has warded this combatant" has no single answer, and asking it
        // that way meant a free relic ward sitting at the head of the drain
        // order made Weight of Wool and Shatter both forget the ward the
        // player had actually paid for.
        private static bool WardedByThisActor(CombatantState wearer, CombatantState caster) =>
            StatusEffects.IsWardedBy(wearer, caster);

        // Applies a Ward to the ally the player picked, and to whoever The
        // Flock has widened it to. Returns how many landed, so the caller can
        // say so.
        //
        // `wearer` IS THE PICK, never a default: the Ward is SingleAlly
        // targeting now and CastSkill refuses one aimed at nobody, so this is
        // reached with a living squadmate (possibly the caster) every time.
        // `skill` and `resourceSpent` are how a ward says how many SHIELD
        // POINTS it is worth -- SkillResolution.Amount's Ward case is the one
        // place that decides, and the caster's WardReductionPercent talent
        // scales whatever it lands on.
        private int ApplyWard(CombatantState caster, CombatantState wearer, ResolvedSkill skill, int resourceSpent)
        {
            int points = SkillResolution.Amount(SkillEffect.Ward, caster, wearer, skill.Power, skill.FlatAmount,
                resourceSpent, ignoresDefense: false, type: DamageType.Physical, axis: skill.ScalingAxis,
                percentOfCasterMaxHealth: skill.PercentOfCasterMaxHealth);
            if (points <= 0) return 0;

            int turns = WardTurnsFor(skill);
            WardOne(caster, wearer, points, turns);
            int landed = 1;

            // The Flock. The spread is a FRACTION OF THE WARD'S OWN pool
            // rather than its own number, so deepening Fleece Ward deepens
            // what the flock gets too -- one strand tunes the construct, the
            // other decides how far it reaches. Unchanged in form by the
            // shield model: half of 50 points is as sensible as half of 50
            // percent was.
            int spread = caster.Talents.Best(TalentEffectType.WardSpreadsToAllies);
            if (spread <= 0) return landed;

            // FlockSpread never yields the wearer, so the share can never
            // land on top of the full ward it is a share of. That mattered
            // more when wards replaced each other; it is still worth keeping
            // as one rule in one place rather than a check at two call sites,
            // because a second pool on the pick would now quietly double what
            // the cast was worth to them.
            foreach (var other in FlockSpread(caster, wearer))
            {
                WardOne(caster, other, points * spread / 100, turns);
                landed++;
            }

            return landed;
        }

        // WHO THE FLOCK'S SHARE REACHES, owner's call 2026-09-15 (AUDIT #147).
        //
        // THE RULE: warding HIMSELF spreads nowhere; warding SOMEBODY ELSE
        // sends the share back to him. Nothing is auto-picked in either
        // direction -- the choice the talent makes is a consequence of the
        // choice the PLAYER made, which is the whole point of the picker this
        // shipped with. The rule it replaced ("the first living non-caster in
        // party order") was decided by the field formation, so a Move
        // silently redirected the ward and nothing on screen said so.
        //
        // ONE SMALL FUNCTION ON PURPOSE. This is the strand of the design most
        // likely to be retuned (most-hurt-first and lowest-fraction-first were
        // both on the table), and a rule with one home is one edit rather than
        // a hunt through the ward pipeline.
        //
        // WardSpreadsToWholeParty (the Flock's T2/T3 nodes) is NOT an
        // auto-pick and keeps its promise unchanged: everybody else on the
        // field, whoever the full ward went to.
        private IEnumerable<CombatantState> FlockSpread(CombatantState caster, CombatantState wearer)
        {
            if (caster.Talents.Has(TalentEffectType.WardSpreadsToWholeParty))
            {
                return _encounter.PlayerParty.Where(a => a.IsAlive && !ReferenceEquals(a, wearer));
            }

            return ReferenceEquals(wearer, caster) || !caster.IsAlive
                ? Enumerable.Empty<CombatantState>()
                : new[] { caster };
        }

        // Puts one more shield pool up.
        //
        // ALWAYS LANDS. Wards stack (owner, 2026-09-16), so there is no
        // refusal to report and no caller left that has to ask whether there
        // was -- StatusEffects.ApplyWard owns the rule and it is "add an
        // entry". The one thing this still does that ApplyWard does not is
        // floor the pool at 1, so a Flock share of a small ward is a thin
        // shield rather than nothing.
        private void WardOne(CombatantState caster, CombatantState wearer, int points, int turns)
        {
            RaiseWard(wearer, System.Math.Max(1, points), turns, caster);

            // Mending Fleece. Rides along with the ward rather than being its own
            // cast, so the sustain strand costs no extra action and no extra
            // wool -- it makes the thing he was already doing worth more.
            int regenPercent = caster.Talents.Best(TalentEffectType.WardAlsoAppliesRegen);
            if (regenPercent <= 0 || wearer.MaxHealth <= 0) return;

            int regenTurns = caster.Talents.Threshold(TalentEffectType.WardAlsoAppliesRegen);
            ApplyStatusTo(wearer, StatusEffectType.Regen,
                System.Math.Max(1, wearer.MaxHealth * regenPercent / 100),
                System.Math.Max(1, regenTurns), caster);
        }

        // Whether this cast will actually do something, checked BEFORE its cost
        // is taken.
        //
        // Shatter with nothing to detonate and a Gift with nobody to give to
        // both have to refuse rather than silently eat the wool and the turn.
        // That is the same courtesy the "can't pay for it" branch extends, and
        // it matters more here: both are conditional on board state the player
        // can misread.
        //
        // TAKES THE WHOLE PICK LIST since milestone C (plan 1.12), because
        // two of its refusals are about picks rather than about the one
        // target: Palace Passage refuses on EITHER ally being rooted, and it
        // has to ask that at commit as well as at pick time, since a pick and
        // a commit are different moments. Every other arm reads `target`, the
        // first of the list, exactly as it always did.
        //
        // `destinationSeat` is Palace Passage's empty-seat destination
        // (PLAN_BELLWETHER_KIT 3.6), -1 for every other cast and for the
        // two-ally form, whose second pick names the seat itself.
        private bool CanResolveSkill(CombatantState actor, ResolvedSkill skill,
            IReadOnlyList<CombatantState> targets, int destinationSeat, out string refusal)
        {
            refusal = null;
            var target = targets != null && targets.Count > 0 ? targets[0] : null;

            // STILL COOLING. First, because it is the cheapest question and the
            // one whose answer never depends on the board -- and because a
            // player told "no wards to shatter" about a skill they could not
            // have cast anyway has been told the wrong thing.
            int cooling = CooldownRemaining(actor, skill.Id);
            if (cooling > 0)
            {
                refusal = cooling == 1
                    ? $"{skill.DisplayName} is ready next turn."
                    : $"{skill.DisplayName} is ready in {cooling} turns.";
                return false;
            }

            // A PHYSICAL MOVE THE ACTOR MAY NOT MAKE (plan 1.10, milestone D).
            //
            // SECOND, AND THE POSITION IS THE PRECEDENCE RULE. It sits after
            // the cooldown -- still the cheapest, still board-independent --
            // and BEFORE every question about the target, so an actor whose
            // only remaining option is a charge it cannot make reads as "no
            // legal action" rather than "no target". Getting that order wrong
            // would tell a shackled player their target was wrong.
            //
            // THE SAME PREDICATE THE MENU GREYED THE ROW WITH, so a row that
            // looked pressable and a cast that spends nothing cannot come
            // apart, and the sentence the player reads is the same one either
            // way.
            if (!CombatActions.IsLegalFor(actor, skill, out string restricted))
            {
                refusal = restricted;
                return false;
            }

            // REQUIRES-STATUS (plan 1.1/2.5): a board-state refusal about the
            // TARGET, the exact shape "Shatter with no wards" already is --
            // conditional on something the player can misread, and refused
            // before a point of mana or a turn is spent on a cast that would
            // visibly detonate nothing. Ashen Reckoning is the one author
            // today (requiresStatus: "Poison"); the check reads generically
            // off whatever status a future skill names.
            if (skill.RequiresStatus.HasValue
                && (target == null || !target.Statuses.Any(s => s.Type == skill.RequiresStatus.Value)))
            {
                refusal = $"{target?.Name ?? "That target"} carries no {skill.RequiresStatus.Value} for {skill.DisplayName} to reclaim.";
                return false;
            }

            // NOBODY LEFT TO AIM AT, asked of the targeting rather than of
            // the three Gift effects by name. EligibleAllies runs the same
            // per-effect predicate the picker's plates and CastSkill's own
            // refusal read (AllyTargeting), so "the menu offered a cast with
            // no legal target" cannot happen for one reader and not another
            // -- and the mana refusal stays free, because a squad whose only
            // living allies carry a pool mana cannot touch produces an empty
            // list without a second copy of that rule.
            //
            // THE WARD CANNOT REACH THIS: the caster is always a candidate
            // for his own ward and it is his turn, so his list is never
            // empty. That is why the wording can stay the gift's.
            if (skill.Targeting == SkillTargeting.SingleAlly && EligibleAllies(actor, skill).Count == 0)
            {
                refusal = $"{actor.Name} has nobody to give it to.";
                return false;
            }

            switch (skill.Effect)
            {
                case SkillEffect.Enthrall:
                    if (!_encounter.OpponentsOf(actor).Any(enemy => enemy != null && enemy.IsAlive))
                    {
                        refusal = $"{skill.DisplayName} has nobody left to enthrall.";
                        return false;
                    }

                    return true;

                case SkillEffect.Shatter:
                    if (WardsCastBy(actor).Count == 0)
                    {
                        refusal = $"{actor.Name} has no wards out to shatter.";
                        return false;
                    }

                    return true;

                // BORROWED MOMENT WITH NOWHERE TO GO (plan 1.9 rule 5). The
                // same shape as Shatter above: conditional on board state the
                // player can misread, so it is refused before a point of mana
                // is spent rather than resolving into a visible nothing.
                //
                // ASKED THROUGH CanAdvanceInOrder, which the ally rack's own
                // plates are lit from -- so the menu cannot offer a pick this
                // refuses, and the two cannot drift.
                case SkillEffect.Hasten:
                    if (target == null || !CanAdvanceInOrder(target))
                    {
                        refusal = target == null
                            ? $"{skill.DisplayName} needs an ally to aim at."
                            : $"{target.Name} is already next in the order.";
                        return false;
                    }

                    return true;

                // PALACE PASSAGE (plan 1.12, 2.9; any seat since
                // PLAN_BELLWETHER_KIT M3). Every refusal before payment, all
                // about a board the player can misread.
                //
                // THROUGH THE ONE PLACEMENT RULE (CombatEncounter.CanPlaceAt),
                // the same one the resolution calls: Rooted is read off both
                // ends (traveller first, then the occupant of the seat, which
                // is the order the two-ally form always named them in), and an
                // empty seat has nobody to be rooted. Checked HERE rather than
                // only at pick time because a pick and a commit are different
                // moments -- a partner rooted in between must still refuse the
                // cast (1.12).
                case SkillEffect.SwapAllies:
                {
                    var traveller = target;
                    if (traveller == null || (targets.Count >= 2 && ReferenceEquals(targets[0], targets[1])))
                    {
                        refusal = $"{skill.DisplayName} needs two different allies.";
                        return false;
                    }

                    int seat = PassageDestination(targets, destinationSeat);
                    if (seat < 0)
                    {
                        refusal = $"{skill.DisplayName} needs a seat to send {traveller.Name} to.";
                        return false;
                    }

                    switch (_encounter.CanPlaceAt(traveller, seat, out var occupant))
                    {
                        case PlaceOutcome.Placed:
                            return true;
                        case PlaceOutcome.MemberRooted:
                            refusal = $"{traveller.Name} is rooted.";
                            return false;
                        case PlaceOutcome.OccupantRooted:
                            refusal = $"{occupant.Name} is rooted.";
                            return false;
                        case PlaceOutcome.NoSuchSeat:
                            refusal = seat >= 0 && seat < CombatEncounter.SeatsPerSide
                                ? $"{traveller.Name} already stands at the {SeatWord(seat)}."
                                : $"{skill.DisplayName} has no such seat to go to.";
                            return false;
                        default:
                            refusal = $"{skill.DisplayName} needs an ally on the field.";
                            return false;
                    }
                }

                // A PLAYER'S REPOSITION that could not move anyone is refused
                // before payment, like the Passage above -- through the same
                // placement rule the resolution will use. (A monster's
                // Reposition never reaches this: it pays nothing, and its
                // rooted fizzle is the resolution's own message.)
                case SkillEffect.Reposition:
                {
                    var outcome = _encounter.CanPlaceAt(target, skill.ToSeat - 1, out var occupant);
                    switch (outcome)
                    {
                        case PlaceOutcome.Placed:
                            return true;
                        case PlaceOutcome.MemberRooted:
                            refusal = $"{target.Name} is rooted.";
                            return false;
                        case PlaceOutcome.OccupantRooted:
                            refusal = $"{occupant.Name} is rooted.";
                            return false;
                        case PlaceOutcome.NoSuchSeat:
                            refusal = $"{target.Name} already stands at the {SeatWord(skill.ToSeat - 1)}.";
                            return false;
                        default:
                            refusal = $"{skill.DisplayName} needs an ally on the field.";
                            return false;
                    }
                }

                default:
                    return true;
            }
        }

        // Every living combatant currently wearing a ward this actor cast.
        private List<CombatantState> WardsCastBy(CombatantState caster) =>
            _encounter.PlayerParty
                .Where(c => c.IsAlive && WardedByThisActor(c, caster))
                .ToList();

        // GiftRecipient IS GONE (AUDIT #147, owner 2026-09-15). It used to
        // decide who a Gift landed on -- "the first living party member who
        // is not the caster", narrowed for Gift: Mana to "the ally missing
        // the most mana who can actually take it" -- and its own comment
        // already said what it was waiting for: "It needs a real target
        // picker the moment a third party slot exists."
        //
        // That picker is what shipped instead, so the engine no longer picks
        // at all: the player does, through the party plates, and the pick
        // arrives as CastSkill's `target`. WHO a gift MAY go to is still a
        // rule and still has one home -- AllyTargeting, read by the plates,
        // by CastSkill's refusal and by the bot alike. WHICH of them it
        // SHOULD go to is now a judgement rather than a rule, so the two
        // orderings this method used to encode moved to the only caller that
        // still has to choose without a hand on the mouse: Domain/Bot/
        // AllyTargetSelection.

        // Detonates every ward the caster has out. Each one throws a share of his
        // Attack at a random enemy.
        private void ResolveShatter(CombatantState caster)
        {
            var wearers = WardsCastBy(caster);
            int percent = caster.Talents.Best(TalentEffectType.ShatterDamagePercentOfAttack);
            int selfMultiplier = caster.Talents.Best(TalentEffectType.ShatterSelfWardMultiplier);
            bool appliesVulnerable = caster.Talents.Has(TalentEffectType.ShatterAppliesVulnerable);

            // COUNTED IN WEARERS, the same unit as the blasts below. Wards
            // stack, so one wearer can carry several of his (a Ward cast on
            // himself over a Runic conversion, say), but owner 2026-09-24:
            // Shatter breaks a wearer's WHOLE ward as one, "always 1
            // explosion" however many entries make it up. Counting entries
            // here (9ce7368b) made the line promise more blasts than the loop
            // throws.
            int shattered = wearers.Count;

            var summary = new StringBuilder(shattered == 1
                ? $"{caster.Name} shatters 1 ward!"
                : $"{caster.Name} shatters {shattered} wards!");

            foreach (var wearer in wearers)
            {
                var enemy = RandomLivingEnemy();
                if (enemy == null) break;

                int share = percent;

                // His OWN ward is worth triple, and detonating it leaves a
                // character with no defensive stats completely open. That trade
                // is the best decision in the path and is deliberately
                // uncushioned -- no smaller ward handed back, no rider.
                if (ReferenceEquals(wearer, caster) && selfMultiplier > 0)
                {
                    share = share * selfMultiplier / 100;
                }

                // Removed BEFORE the damage lands. A ward still on the books
                // while its own detonation resolved would be absorbed by itself
                // if the blast came back around, and would keep counting toward
                // Weight of Wool for the rest of the volley.
                wearer.Statuses.RemoveAll(s => s.Type == StatusEffectType.Shielded
                                               && ReferenceEquals(s.Source, caster));

                int damage = CombatMath.Scale(caster.Attack * share / 100);
                DealDamage(caster, enemy, damage, AttackTypeOf(caster), KillCredit.Attacker);
                RecordBeatAmount(System.Math.Max(damage, LargestAmountSoFar));
                SetStance(enemy, enemy.IsAlive ? Stances.Hurt : Stances.Defeated);
                summary.Append($" {enemy.Name} takes {damage}.");

                if (!enemy.IsAlive)
                {
                    summary.Append($" {enemy.Name} is defeated!");
                }
                else if (appliesVulnerable)
                {
                    ApplyStatusTo(enemy, StatusEffectType.Vulnerable,
                        ShatterVulnerablePercent, ShatterVulnerableTurns, caster);
                    summary.Append(" It is left wide open!");
                }
            }

            AppendMessage(summary.ToString());
        }

        // Shatter T3's rider. Numbers rather than authored fields because the
        // rule is a flag -- the talent says THAT it applies Vulnerable, and one
        // consistent strength for it keeps the node's own text honest.
        private const int ShatterVulnerablePercent = 30;

        // ONE affected turn, and it was authored 2 until plan D1 moved
        // Vulnerable onto the turn-end clock. Under the old turn-start
        // countdown a 2 exposed the target for exactly one of its turns,
        // because the entry was removed at the start of the second one before
        // its action ever happened. The number changed so the behaviour would
        // not; StatusDurationMigrationTests pins that it did not.
        private const int ShatterVulnerableTurns = 1;

        private CombatantState RandomLivingEnemy()
        {
            var living = _encounter.LivingEnemies.ToList();
            if (living.Count == 0) return null;
            return _rng == null ? living[0] : living[_rng.NextInt(0, living.Count)];
        }

        // The three Wool Gifts, resolved against the ally the player picked.
        //
        // The null guard stays even though CastSkill refuses a gift with no
        // target before this is reached: Domain's rule is one guard at the
        // seam plus graceful degradation, and this is the seam a future
        // caller would arrive at wrong.
        private void ResolveGift(CombatantState caster, ResolvedSkill skill, CombatantState ally)
        {
            if (ally == null) return;

            switch (skill.Effect)
            {
                case SkillEffect.GiftMana:
                {
                    int percent = caster.Talents.Best(TalentEffectType.GiftManaPercent);
                    int amount = System.Math.Max(1, ally.MaxMana * percent / 100);

                    // THE RETURN, NOT THE REQUEST. CombatMath.RestoreMana is
                    // measured for exactly this -- "returns how much actually
                    // landed, so a caller can say the true number (or say
                    // nothing) rather than announcing an amount it hoped for"
                    // -- and this line printed `amount` regardless. It clamps
                    // at the recipient's own maximum, so a gift to a nearly
                    // full ally said 24 and moved 5.
                    int landed = CombatMath.RestoreMana(ally, amount);

                    // The pool NAMES ITSELF, the same way the potion's line
                    // does (FightSession.Items). For today's roster that is
                    // the word "mana" it always printed.
                    string unit = (ally.PrimaryPool?.DisplayName ?? "").ToLowerInvariant();

                    AppendMessage(landed > 0
                        ? $"{caster.Name} presses wool into {ally.Name}'s hands - {landed} {unit} back."
                        : $"{caster.Name} presses wool into {ally.Name}'s hands - there was nothing left to give back.");
                    break;
                }

                case SkillEffect.GiftFury:
                {
                    int percent = caster.Talents.Best(TalentEffectType.GiftAttackBonusPercent);

                    // Generous duration, spent by the swing rather than by the
                    // clock -- see StatusEffectType.Empowered. It read the
                    // ward's own 999 until the wards became shields with real
                    // two-turn clocks; a gift is still the old shape, so it
                    // now says so with its own number instead of borrowing
                    // one that no longer means what it used to.
                    ApplyStatusTo(ally, StatusEffectType.Empowered,
                        System.Math.Max(1, percent), GiftFuryDurationTurns, caster);
                    AppendMessage($"{caster.Name} winds {ally.Name} up - their next swing lands {percent}% harder.");
                    break;
                }

                case SkillEffect.GiftHaste:
                {
                    if (!caster.Talents.Has(TalentEffectType.GiftAppliesImmediateTurn)
                        || !_encounter.PullToFront(ally))
                    {
                        return;
                    }

                    AppendMessage($"{caster.Name} shoves {ally.Name} forward - they go next.");

                    // NO SPARRING NOTE -- "forward" here is the turn order,
                    // not the battle line. See NoteDeliberateMove.
                    break;
                }
            }
        }

        // Turn-start bookkeeping the Lamb needs: his per-turn payout cap resets,
        // and Weight of Wool T3's grace period counts down.
        //
        // Called beside every other duration, so "a turn" means the same thing
        // here as it does for a status or a transform.
        private void TickLambTurnStart(CombatantState actor)
        {
            if (actor == null) return;

            // HIS entries only, and whether or not he holds the talent today:
            // an entry keyed on him can only exist because he was paid, and
            // the rule is per caster however many casters there are.
            _wardPayoutsThisTurn.RemoveWhere(payout => ReferenceEquals(payout.caster, actor));

            if (actor.SelfWardGraceTurns > 0)
            {
                actor.SelfWardGraceTurns--;
            }
        }

        // ---- the Black Ram -----------------------------------------------------

        // Sharp Horns T3: what the swing leaves behind.
        //
        // Permanent for the rest of the fight and stacking, which is the one rule
        // in this vocabulary that deliberately compounds -- it is a party
        // contribution, not a personal buff, and the whole point is that the wall
        // everybody has been chipping at gets softer for everybody. Floored at 0
        // rather than allowed negative: below zero it would start ADDING damage
        // through CombatMath's subtraction, a different mechanic than the one
        // authored.
        private void ApplyDefenseShred(CombatantState actor, CombatantState target, bool physicalMove)
        {
            int shred = actor?.Talents.Best(TalentEffectType.ShredDefenseOnHit) ?? 0;
            if (!physicalMove || shred <= 0 || target == null || !target.IsAlive) return;
            if (target.PhysicalDefense <= 0) return;

            int beforePhysical = target.PhysicalDefense;
            target.PhysicalDefense = System.Math.Max(0, target.PhysicalDefense - shred);
            int shredded = beforePhysical - target.PhysicalDefense;
            if (shredded > 0)
            {
                target.PermanentPhysicalDefenseShred += shredded;
                AppendMessage($"{actor.Name}'s horns leave {target.Name}'s guard {shredded} thinner - permanently.");
            }
        }

        // Trample T2: a kill spills onto whoever was standing next to it.
        private void ApplyKillSplash(CombatantState actor, CombatantState victim)
        {
            int percent = actor?.Talents.Best(TalentEffectType.KillSplashPercentOfAttack) ?? 0;
            if (percent <= 0) return;

            SplashOntoNeighbours(actor, victim, CombatMath.Scale(actor.Attack * percent / 100),
                $"{victim.Name} goes down hard");
        }

        // Explosive's on-kill splash -- the identical mechanism as
        // ApplyKillSplash just above, read from ModifierEffects instead of
        // Talents. A SEPARATE method rather than folding into ApplyKillSplash
        // -- see ModifierEffectType.OnKillSplashPercent's own comment for why
        // two independently-magnituded sources stay two calls rather than one
        // that silently sums them.
        private void ApplyModifierKillSplash(CombatantState actor, CombatantState victim)
        {
            int percent = actor?.ModifierEffects.Best(ModifierEffectType.OnKillSplashPercent) ?? 0;
            if (percent <= 0) return;

            SplashOntoNeighbours(actor, victim, CombatMath.Scale(actor.Attack * percent / 100),
                $"{victim.Name} goes down in a blast");
        }

        // Black Ram Mode's own splash: while the transform is running, every
        // landed hit spills onto the target's neighbours.
        //
        // A percent of the DAMAGE DEALT, where Trample's kill splash is a percent
        // of raw Attack. Different mechanics, and the difference is deliberate:
        // the kill splash fires once on a corpse and needs a figure that does not
        // depend on the overkill that produced it, while this fires on every
        // swing and should track how hard that swing actually landed.
        private void ApplyTransformSplash(CombatantState actor, CombatantState target, int damage)
        {
            var transformation = actor?.Transformation;
            if (transformation == null || transformation.SplashPercent <= 0 || damage <= 0) return;

            SplashOntoNeighbours(actor, target, System.Math.Max(1, damage * transformation.SplashPercent / 100),
                $"{transformation.DisplayName} hits wider than it looks");
        }

        // The shared half of both splashes: who counts as "either side", and
        // saying what happened.
        //
        // "ADJACENT" IS WHAT THE PLAYER SEES -- the monsters drawn either side
        // of the victim. That is rank, not list index, and the two stopped
        // agreeing when position became a function of rank: ranks compress
        // behind a corpse the instant it falls (CombatEncounter.LivingRankOf,
        // "death compresses the ranks behind the corpse with no bookkeeping to
        // keep in sync"; BeatFormation, "position is a per-beat function of
        // rank"). Walking raw indices therefore aimed the splash at a gap: with
        // three monsters, once the MIDDLE one died the splash was dead for the
        // rest of the fight even though the two survivors stood side by side.
        // Black Ram Mode's splash fires on every landed hit, so that was not a
        // corner.
        //
        // THE EPICENTRE STILL HOLDS ITS OWN RANK EVEN WHEN IT HAS JUST DIED,
        // which is why this counts ranks here rather than calling LivingRankOf:
        // a kill splash is always fired over a fresh corpse, and BeatFormation
        // says the same thing about the screen -- "a corpse holds its rank
        // until it has faded out". For a LIVING epicentre this is exactly
        // LivingRankOf; for the one that just fell it is the rank it is still
        // being drawn in. An older corpse holds nothing and is stepped over.
        //
        // Splash never chains -- it is computed from the original hit, never
        // recursively from its own kills, or one blow would cascade down a
        // whole row. Takes the SOURCE as well as the epicentre, so the splash
        // is credited to whoever caused it. Both callers already hold the
        // actor; without it the two widest damage sources in the game would
        // land in nobody's column and the ledger would quietly under-report
        // every Black Ram.
        private void SplashOntoNeighbours(CombatantState source, CombatantState epicentre, int splash, string cause)
        {
            if (epicentre == null || splash <= 0) return;

            // The enemy line as it is DRAWN: everyone holding a rank, in order.
            var line = new List<CombatantState>();
            int epicentreRank = -1;
            foreach (var enemy in _encounter.Enemies)
            {
                if (enemy == null) continue;
                if (!enemy.IsAlive && !ReferenceEquals(enemy, epicentre)) continue;

                if (ReferenceEquals(enemy, epicentre)) epicentreRank = line.Count;
                line.Add(enemy);
            }

            if (epicentreRank < 0) return;

            foreach (int neighbour in new[] { epicentreRank - 1, epicentreRank + 1 })
            {
                if (neighbour < 0 || neighbour >= line.Count) continue;

                var bystander = line[neighbour];
                if (!bystander.IsAlive) continue;

                DealDamage(source, bystander, splash, AttackTypeOf(source), KillCredit.Attacker);
                SetStance(bystander, bystander.IsAlive ? Stances.Hurt : Stances.Defeated);

                // AND THE VIEW IS TOLD SOMETHING LANDED ON THEM. The beat
                // recorded the neighbour's flinch and its log line and nothing
                // else, so a body that visibly took damage had no effect drawn
                // on it -- the same gap SplashTargets was added for when
                // ResolveDamageAll gave three rats one animation between them.
                // Additive, so a sweep that ALSO splashes keeps both lists.
                RecordSplashTargets(new[] { bystander });

                AppendMessage($"{cause} - {bystander.Name} takes {splash} from it!");

                if (!bystander.IsAlive)
                {
                    AppendMessage($"{bystander.Name} is defeated!");
                }
            }
        }

        // ---- the transform ------------------------------------------------------

        // Puts the caster under a Transform skill's grant, with Wrath's duration
        // bonus and Charge's party-wide shove folded in.
        private void EnterTransform(CombatantState actor, ResolvedSkill skill)
        {
            var grant = skill.Transform;
            if (actor == null || grant == null || !grant.IsAuthored) return;

            // Re-entering while already transformed refreshes rather than
            // stacking a second set of bonuses -- the same refresh-don't-compound
            // rule StatusEffects.Apply follows, and for the same reason: paying
            // the cost twice should never be strictly better than paying it once
            // and waiting.
            if (actor.Transformation != null)
            {
                Transformation.Exit(actor);
            }

            // THE ONE LINE THAT SIZES A TRANSFORM. Wrath's talent and the
            // Bellwether's Bell both add here, so each covers any Transform
            // skill, and they add to each other (plan 2.12).
            int turns = grant.turns + actor.Talents.Best(TalentEffectType.TransformDurationBonus)
                        + (HasRelic(actor, RelicEffect.BellwethersBell) ? FightTuning.BellwethersBellTransformTurns : 0);
            var transformation = Transformation.Enter(actor, grant.displayName, turns,
                grant.attackPercent, grant.speedPercent, grant.temporaryHealthPercent, grant.splashPercent,
                grant.spritePath, grant.hit);

            // AND THE VIEW IS TOLD ON THE BEAT, not left to read live state.
            // The whole round has already resolved by the time this beat is
            // drawn, so a stage that asked actor.Transformation would show the
            // Black Ram from the beat's first frame -- before the flash that is
            // supposed to hide the change. See CombatBeat.Forms.
            RecordForm(actor, transformation.SpritePath);

            // Speed changed, so the SCHEDULER has to be told. Without this the
            // plate would show the bonus and the turn queue would ignore it -- a
            // failure with no visible symptom other than the mode feeling weaker
            // than its numbers.
            _encounter.RefreshSpeed(actor);

            AppendMessage($"{actor.Name} becomes {transformation.DisplayName} for {transformation.TurnsRemaining} turns!");

            // Charge T3. Fires on ENTERING, which is why the rule is worded
            // "entering" rather than "casting".
            if (actor.Talents.Has(TalentEffectType.TransformPushesEveryEnemy))
            {
                foreach (var enemy in _encounter.LivingEnemies.ToList())
                {
                    _encounter.PushBack(enemy, 1);
                }

                AppendMessage("The charge scatters the line - every enemy is driven back!");
            }
        }

        // The transform's own turn-start tick: expire it, hold it, or make it
        // permanent.
        private void TickTransform(CombatantState actor)
        {
            var transformation = actor?.Transformation;
            if (transformation == null) return;

            // The capstone. Checked BEFORE the timer, so reaching the health gate
            // on the very turn the transform would have run out keeps it rather
            // than losing it by one tick.
            if (!transformation.IsPermanent
                && actor.Talents.IsBelowThreshold(TalentEffectType.TransformPermanentBelowHealth, actor))
            {
                transformation.IsPermanent = true;
                AppendMessage($"{actor.Name} stops changing back. This is what he is now.");
            }

            if (transformation.IsPermanent) return;

            // Wrath T3: wounded enough, and the timer simply does not run.
            if (actor.Talents.IsBelowThreshold(TalentEffectType.TransformHoldsBelowHealth, actor)) return;

            transformation.TurnsRemaining--;
            if (transformation.TurnsRemaining > 0) return;

            ExpireTransform(actor, transformation);
        }

        // How hard the stage kicks when a form runs out.
        //
        // A FLOOR, like every authored `shake`: ShakeStrength takes the larger
        // of the blow's own weight and this, and an exit lands no blow at all.
        // Under black_ram_mode's own 0.7 on purpose -- becoming the thing is
        // the bigger of the two events, and a revert that hit as hard as the
        // entry would make the mode read as costing what it granted.
        private const float TransformExitShake = 0.45f;

        // THE FORM RUNNING OUT IS AN EVENT, AND IT GETS ITS OWN BEAT.
        //
        // 651c8a79 shipped this as a quiet revert riding the post-playback
        // resync, on the argument that a transform expires at its holder's TURN
        // START -- which is not an action, so there was nothing to record the
        // change on. That argument was about the beat QUEUE rather than about
        // the player: a turn start is a moment, the queue can hold a beat for a
        // moment, and leaving the form is exactly as much of an event as
        // entering it was. Overruled by the owner, and this is the overrule.
        //
        // THE TWIN OF THE ENTRY ON STAGE: one beat on the holder, rooted
        // (Hold), no amount, the form cleared at its impact instant under the
        // same white silhouette flash, and a shake floor. What differs is the
        // pose -- IDLE rather than the entry's authored `victory`, because he
        // is not doing anything, he is being himself again, and `hurt` would
        // say the change cost him something it does not.
        //
        // AND NOT AN ACTION, which is where it stops being the entry's twin.
        // Owner, 2026-09-26: "a transform expiring should definitely not be an
        // action". It used to open through BeginBeat like the cast that
        // entered it, and BeginBeat is the action seam -- so a form running
        // out told a decaying pool the turn was not idle and read as a turn
        // taken to every IsAction reader. It opens through NewBeat with its
        // own cause instead, the way a status tick does, and takes no cast
        // stance (the idle below is the only pose it ever wore).
        //
        // THE EXIT HAPPENS BEFORE CommitBeat, which is what puts the temporary
        // health's disappearance on this beat rather than on whatever played
        // next: PreSnapshot was taken by NewBeat with the pool still granted
        // and Snapshot is taken by CommitBeat without it, so the bar drops on
        // the frame the ram walks back out.
        //
        // THE PATHS THAT DO NOT GET ONE, stated because they are the half that
        // is easy to get wrong:
        //   - the capstone's permanent form returns above without ever
        //     reaching here, so keeping the form punctuates nothing;
        //   - Wrath T3's hold returns above too, for the same reason;
        //   - a re-entry (EnterTransform) exits and re-enters inside ONE beat
        //     and records the new folder on it, so the swap is one event with
        //     one flash rather than a revert followed by a transformation;
        //   - nothing else in the game calls Transformation.Exit. A defeated
        //     holder keeps their Transformation, so there is no revert to
        //     flash over a death fade.
        private void ExpireTransform(CombatantState actor, Transformation transformation)
        {
            string name = transformation.DisplayName;

            NewBeat(BeatCause.TransformExpiry, actor, actor, StageApproach.Hold);
            SetStance(actor, Stances.Idle);
            RecordShake(TransformExitShake);

            // "" is the view's word for "back in your own art" -- see
            // RecordForm, which records an empty folder rather than omitting
            // the actor, so playback can tell a revert from a beat that
            // transformed nobody.
            RecordForm(actor, "");

            Transformation.Exit(actor);
            _encounter.RefreshSpeed(actor);
            AppendMessage($"{actor.Name} is no longer {name}.");

            CommitBeat();
        }

        // Wrath T2: a kill during the transform buys more of it, up to a total
        // extension the talent itself states.
        private void ExtendTransformOnKill(CombatantState actor)
        {
            var transformation = actor?.Transformation;
            if (transformation == null || transformation.IsPermanent) return;

            int perKill = actor.Talents.Best(TalentEffectType.TransformExtendOnKill);
            int cap = actor.Talents.Threshold(TalentEffectType.TransformExtendOnKill);
            if (perKill <= 0 || transformation.ExtensionsGranted >= cap) return;

            int granted = System.Math.Min(perKill, cap - transformation.ExtensionsGranted);
            transformation.ExtensionsGranted += granted;
            transformation.TurnsRemaining += granted;
            AppendMessage($"The kill feeds it - {transformation.DisplayName} holds for {granted} turn(s) longer.");
        }

        // ---- Provoke ------------------------------------------------------------

        // Lands the taunt on one enemy, or on all of them once Provoke T3 is
        // bought. Returns how many were actually provoked, so the caller can say
        // something honest when there was nobody left to provoke.
        private int ApplyProvoke(CombatantState actor, CombatantState target)
        {
            if (actor == null) return 0;

            bool everyone = actor.Talents.Has(TalentEffectType.ProvokeHitsEveryEnemy);
            var victims = everyone
                ? _encounter.LivingEnemies.ToList()
                : new List<CombatantState> { target ?? _encounter.FrontEnemy };

            int reduction = actor.Talents.Best(TalentEffectType.ProvokedDamageReductionPercent);
            int count = 0;

            foreach (var victim in victims)
            {
                if (victim == null || !victim.IsAlive) continue;

                // Duration 1 is nominal -- Provoked is spent by the turn it
                // redirects rather than counted down, so the number is only there
                // to satisfy ActiveStatus' own floor.
                ApplyStatusTo(victim, StatusEffectType.Provoked, reduction, 1, actor);
                count++;
            }

            return count;
        }
    }
}
