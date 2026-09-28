using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // Pure rules over a combatant's status list: how a new application
    // stacks with an existing one, what Protect/Vulnerable does to a damage
    // figure, and what a turn-start tick does to Poison/Regen and every
    // status's remaining duration.
    //
    // Engine-free and pure, same as CombatMath and ResourcePool — the
    // rules about which skills apply which statuses live in Content and
    // Core, not here.
    //
    // CHILLED (Slow) IS HERE NOW; HASTE STILL IS NOT. For a long time neither
    // was, on purpose — every other status in this file rides a hook the
    // combat pipeline already had: Poison/Regen are CombatMath.ApplyDamage/
    // Heal on a schedule, Protect/Vulnerable are one more multiplier next to
    // EffectivenessMultiplier, Stun reuses BreakShield's turn-skip. A
    // speed-modifying status has no such hook lying around: it means
    // reaching into TurnOrder's charge RATE, which SpeedScale spent real,
    // documented care tuning a sub-linear curve and a hard ceiling for
    // specifically to make "eternal turn cheese" impossible.
    //
    // Chilled's own pass (item-modifier plan, Phase D2) deliberately did NOT
    // retrofit a read inside SpeedScale.TickRate or TurnOrder itself — that
    // would have meant a signature change rippling to every caller of a
    // function the codebase already calls "tuned with real care" for a
    // single new status. Instead Chilled rides FightSession.SpeedBuffs.cs's
    // EXISTING relic-driven speed-buff bookkeeping (GrantSpeedMalusPercent/
    // RevokeSpeedBuff), which already solved "grant a percent-of-true-base
    // malus and revert it exactly on expiry" for Lucky Deck's bespoke slow.
    // Chilled is that same malus, just granted/revoked from a status
    // (Magnitude/TurnsRemaining, refresh-not-stack, ticked by Tick below)
    // instead of from a bare relic proc — see FightSession.SpeedBuffs'
    // header for the dictionary-widening this required and why. SpeedScale
    // and TurnOrder are UNCHANGED by any of this: Speed is still just an
    // int, Chilled only ever moves that int by the same flat-points-handed-
    // over arithmetic every speed relic already used.
    //
    // Haste (a status that speeds someone up) still has no member and no
    // hook — nothing in this pass needed one, and the day it does, it reads
    // as GrantSpeedPercent's mirror the exact same way Chilled reads as
    // GrantSpeedMalusPercent's.
    public static class StatusEffects
    {
        // APPLIES A STATUS, and what a second application does is
        // StackPolicyOf's answer, not this method's opinion.
        //
        // IT USED TO BE ONE RULE FOR EVERY MEMBER: merge, taking the stronger
        // magnitude and the longer duration, on the argument that additive
        // stacking is the silent compounding this project's other systems
        // (ScalingProfile's additive-not-multiplicative rule, SpeedScale's
        // hard ceiling) go out of their way to avoid. The owner's answer on
        // 2026-09-20 was the opposite -- "the DoTs and everything can stack of
        // course" -- and it is the better model for the statuses whose
        // magnitude is a quantity: three poisons from three casters ARE three
        // poisons, each with its own strength, its own remaining ticks and its
        // own source, and a player can add them up. See StackingPolicy for
        // where the line falls and why a restriction stays on the old rule.
        //
        // RETURNS THE ENTRY it applied or refreshed. The caller needs it for
        // the same reason ApplyWard's caller does: a status on the AtTurnEnd
        // clock does not age at the end of the wearer's turn it was applied
        // during, and the only way to say "this one, not that one" about two
        // instances with the same magnitude and the same clock is to hold the
        // entry.
        //
        // REFUSES Shielded OUTRIGHT, still. Wards stack like everything else
        // on the Stack side now, but ApplyWard stays their one entry point for
        // a reason this method cannot serve: it is the only place that knows a
        // ward of non-positive points is a no-op rather than an entry.
        // Throwing is the tier-1 version of that rule (CODE_STANDARDS section
        // 9) -- the API cannot express the mistake -- and it costs nothing.
        public static ActiveStatus Apply(List<ActiveStatus> statuses, StatusEffectType type, int magnitude, int turns, CombatantState source = null)
        {
            if (type == StatusEffectType.Shielded)
            {
                throw new ArgumentException(
                    "A ward is a shield POOL with its own no-op rule -- call StatusEffects.ApplyWard, which "
                    + "is the one place a shield goes up.", nameof(type));
            }

            if (StackPolicyOf(type) == StackingPolicy.Refresh)
            {
                var existing = statuses.FirstOrDefault(s => s.Type == type);
                if (existing != null)
                {
                    existing.Magnitude = Math.Max(existing.Magnitude, magnitude);
                    existing.TurnsRemaining = Math.Max(existing.TurnsRemaining, Math.Max(1, turns));

                    // Re-points the credit at whoever most recently paid for it,
                    // but never CLEARS it: a source-less refresh (an enemy's
                    // claws re-applying a status the mage originally landed) must
                    // not quietly disown a status the mage's engine is being paid
                    // for. Losing the attribution silently is the failure mode
                    // worth guarding, since nothing about it would look wrong —
                    // the wool income would just stop.
                    if (source != null)
                    {
                        existing.Source = source;
                    }

                    return existing;
                }
            }

            var added = new ActiveStatus(type, magnitude, turns, source);
            statuses.Add(added);
            return added;
        }

        // WHAT A SECOND APPLICATION DOES, per member. Total by construction --
        // the listed arm is the closed set of restrictions and single-spend
        // tokens, and the default arm is everything whose magnitude is a
        // quantity, so a new damaging or percentage member joins the stacking
        // side without an edit here.
        // StatusEffectsTests.EveryStatusTypeAnswersStackPolicy walks
        // Enum.GetValues so neither arm can go stale silently.
        //
        // THE LINE IS WHAT THE MAGNITUDE MEANS, not how old the status is:
        //
        //   Refresh -- the status is a binary restriction (Stun, Feared,
        //   Rooted, Provoked) or a token spent whole on one occurrence
        //   (Empowered, Marked). "Two of it" has no meaning beyond duration:
        //   a turn cannot be skipped twice, and Marks.ConsumeMark spends one
        //   mark whatever is underneath it. Feared is here although it carries
        //   a percent, because that percent is Fear.VulnerablePercent -- one
        //   authored constant whose own header exists to stop two appliers
        //   disagreeing about it -- and summing it across instances would be a
        //   balance change nobody asked for.
        //
        //   Stack -- the magnitude is a quantity (Poison, Regen, Shielded) or
        //   an additive percentage (Protect, Vulnerable, Chilled) a player can
        //   add up, and DamageTakenMultiplier and WardPoints already sum across
        //   entries today.
        public static StackingPolicy StackPolicyOf(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Stun:
                case StatusEffectType.Feared:
                case StatusEffectType.Rooted:
                case StatusEffectType.Provoked:
                case StatusEffectType.Empowered:
                case StatusEffectType.Marked:
                    return StackingPolicy.Refresh;
                default:
                    return StackingPolicy.Stack;
            }
        }

        // DOES THIS STATUS HAVE A MAGNITUDE OF ITS OWN? The third table beside
        // DurationClock and StackPolicyOf, and the one an AUTHOR has to know
        // about: a content row that names a status is required to supply a
        // positive magnitude, and for a gate rather than a quantity there is
        // no honest number to supply.
        //
        // IT WAS NOT A TABLE UNTIL MILESTONE D, and the cost was visible in
        // content before it was noticed: `grapple` authored
        // `statusMagnitude: 1` for a Stun, a number nothing reads, purely to
        // satisfy the required-and-positive rule. Velvet Shackles' Rooted made
        // the second one, which is the project's own two-use bar for turning a
        // workaround into a rule (docs/CODE_STANDARDS.md 10).
        //
        // NOT THE SAME LINE StackPolicyOf DRAWS, although it nearly is.
        // Empowered refreshes rather than stacking and yet carries a real
        // magnitude (CombatMath reads it off the entry), and Feared refreshes
        // and carries Fear.VulnerablePercent. "Two of it means nothing" and
        // "it has no number at all" are different claims, so they are
        // different tables rather than one read twice.
        //
        // THROWS on an unhandled member, like DurationClock: a silent default
        // here would either force a meaningless number onto an author or let a
        // real magnitude be dropped, and both look like nothing.
        // StatusEffectsTests.EveryStatusTypeAnswersCarriesMagnitude walks
        // Enum.GetValues so a new member fails a test rather than a fight.
        public static bool CarriesMagnitude(StatusEffectType type)
        {
            switch (type)
            {
                // A quantity or a percentage, read off the entry.
                case StatusEffectType.Poison:
                case StatusEffectType.Regen:
                case StatusEffectType.Protect:
                case StatusEffectType.Vulnerable:
                case StatusEffectType.Chilled:
                case StatusEffectType.Shielded:
                case StatusEffectType.Empowered:
                case StatusEffectType.Feared:
                case StatusEffectType.Burn:
                case StatusEffectType.Thorned:
                case StatusEffectType.Bleed:
                    return true;

                // A gate or a token. It is on or it is not, and the only
                // number it has is how long it lasts.
                case StatusEffectType.Stun:
                case StatusEffectType.Provoked:
                case StatusEffectType.Rooted:
                case StatusEffectType.Marked:
                    return false;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type,
                        "StatusEffects.CarriesMagnitude has no answer for this status -- an author would "
                        + "either be made to invent a number it does not use or allowed to drop one it does.");
            }
        }

        // EVERY LIVE INSTANCE of one type on one combatant, in the order they
        // were applied. ONE WALK, so a magnitude reader, the HUD badge and a
        // consumer cannot disagree about what is on the board -- the same
        // argument WardsInDrainOrder already makes for the ward subset.
        public static IEnumerable<ActiveStatus> InstancesOf(CombatantState combatant, StatusEffectType type)
        {
            if (combatant == null) yield break;

            foreach (var status in combatant.Statuses)
            {
                if (status.Type == type) yield return status;
            }
        }

        // THE EFFECTIVE MAGNITUDE of one type: the sum of its live instances,
        // 0 for a type the combatant does not carry.
        //
        // Every caller that used to read FirstOrDefault(...)?.Magnitude wants
        // this instead. Reading the first instance of three is the exact bug
        // stacking introduces, and it is a quiet one -- it reports a third of
        // the slow, a third of the poison, and looks entirely correct doing it.
        public static int MagnitudeOf(CombatantState combatant, StatusEffectType type)
        {
            int total = 0;
            foreach (var status in InstancesOf(combatant, type)) total += status.Magnitude;
            return total;
        }

        // ONE TYPE'S WHOLE STATE ON ONE COMBATANT, for a reader that draws one
        // badge however many instances are underneath it.
        //
        // Deliberately the same shape as WardSummary, which solved this exact
        // problem for the one status that already stacked. Wards keep their own
        // summary rather than folding into this one because their badge answers
        // a different question -- "how much is left", not "how much longer" --
        // and NeverExpires has no analogue for anything else.
        public readonly struct StatusSummary
        {
            public readonly StatusEffectType Type;

            // The sum of the live instances' magnitudes: the poison per turn,
            // the percent of slow, the points of regen.
            public readonly int Magnitude;

            public readonly int Instances;

            // Turns left on the instance that lapses first -- the next moment
            // the number above changes on its own.
            public readonly int SoonestTurns;

            public bool Any => Instances > 0;

            public StatusSummary(StatusEffectType type, int magnitude, int instances, int soonestTurns)
            {
                Type = type;
                Magnitude = magnitude;
                Instances = instances;
                SoonestTurns = soonestTurns;
            }
        }

        public static StatusSummary SummariseStatus(CombatantState combatant, StatusEffectType type)
        {
            int magnitude = 0;
            int instances = 0;
            int soonest = 0;

            foreach (var status in InstancesOf(combatant, type))
            {
                magnitude += status.Magnitude;
                instances++;
                if (instances == 1 || status.TurnsRemaining < soonest) soonest = status.TurnsRemaining;
            }

            return new StatusSummary(type, magnitude, instances, soonest);
        }

        // How many of `combatants` carry a status applied by `source`.
        //
        // The mage path's engine, and — counting only Provoked — the Black
        // Ram's Provoke T3 income. One function rather than two because the
        // question is identical and the only difference is which types count,
        // which the caller already knows.
        public static int CountAfflictedBy(IEnumerable<CombatantState> combatants, CombatantState source,
            StatusEffectType? onlyType = null)
        {
            if (source == null || combatants == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var combatant in combatants)
            {
                if (combatant == null || !combatant.IsAlive)
                {
                    continue;
                }

                foreach (var status in combatant.Statuses)
                {
                    if (!ReferenceEquals(status.Source, source))
                    {
                        continue;
                    }

                    if (onlyType.HasValue && status.Type != onlyType.Value)
                    {
                        continue;
                    }

                    count++;
                    break;
                }
            }

            return count;
        }

        // Who this combatant has been provoked BY, or null if it is free to
        // pick its own target. A dead provoker does not hold a taunt — the
        // enemy would otherwise be locked onto a corpse and skip its turn.
        public static CombatantState ProvokedBy(CombatantState combatant)
        {
            if (combatant == null)
            {
                return null;
            }

            foreach (var status in combatant.Statuses)
            {
                if (status.Type == StatusEffectType.Provoked && status.Source != null && status.Source.IsAlive)
                {
                    return status.Source;
                }
            }

            return null;
        }

        // How much less damage `attacker` deals to `target` because it was
        // provoked BY that target, as a 0-1 multiplier. Exactly 1 for every
        // pair that has nothing to do with a taunt, which is nearly all of
        // them.
        //
        // Deliberately NOT folded into DamageTakenMultiplier, which is a
        // property of the TARGET alone and is read by every damage path. This
        // one is a property of the PAIR: a provoked enemy hits Shawn softly
        // and everyone else exactly as hard as before. Merging them would
        // have made the reduction apply to splash and AOE landing on the rest
        // of the party too.
        public static float ProvokedDamageMultiplier(CombatantState attacker, CombatantState target)
        {
            if (attacker == null || target == null)
            {
                return 1f;
            }

            foreach (var status in attacker.Statuses)
            {
                if (status.Type == StatusEffectType.Provoked && ReferenceEquals(status.Source, target))
                {
                    return Math.Max(MinimumDamageTakenMultiplier, 1f - status.Magnitude / 100f);
                }
            }

            return 1f;
        }

        // Feared reads as Stun here too -- see StatusEffectType.Feared's own
        // header. ResolveSkippedTurn's own ConsumeStun call only ever
        // removes the Stun entry, so a Feared status keeps returning true
        // here (and keeps skipping the holder's turn) for as many of its
        // own turns as it has left, rather than being spent on the first.
        public static bool HasStun(List<ActiveStatus> statuses)
        {
            return statuses.Any(s => s.Type == StatusEffectType.Stun || s.Type == StatusEffectType.Feared);
        }

        // Rooted is queried, never consumed -- unlike Stun it decays by turn
        // count (StatusEffects.Tick's generic countdown already handles it,
        // the same way Chilled needs no special case there), so there is no
        // ConsumeRooted to sit beside ConsumeStun.
        public static bool HasRooted(List<ActiveStatus> statuses)
        {
            return statuses.Any(s => s.Type == StatusEffectType.Rooted);
        }

        // SPENDS ONE SKIPPED TURN, and it is also where a skip status's
        // duration is counted down -- see Tick's own exemption for why the
        // turn-start tick cannot be that place.
        //
        // The two halves differ because the two statuses promise different
        // things. A Stun's whole promise is "the next turn", so it goes
        // outright however many turns it was authored with (that has always
        // been true; the authored duration was only ever decorative). A Fear
        // promises a fixed number of the holder's own turns, so it counts one
        // off and stays until it runs out -- which is exactly what
        // StatusEffectType.Feared's header already claimed and what the
        // turn-start tick was quietly getting wrong by one.
        public static void ConsumeStun(List<ActiveStatus> statuses)
        {
            statuses.RemoveAll(s => s.Type == StatusEffectType.Stun);

            var fear = statuses.FirstOrDefault(s => s.Type == StatusEffectType.Feared);
            if (fear == null) return;

            fear.TurnsRemaining--;
            if (fear.TurnsRemaining <= 0) statuses.Remove(fear);
        }

        // The same, for a taunt: spent by the turn it redirected. Called
        // after the provoked combatant has actually acted — see the note in
        // Tick on why this cannot be a duration.
        public static void ConsumeProvoke(List<ActiveStatus> statuses)
        {
            statuses.RemoveAll(s => s.Type == StatusEffectType.Provoked);
        }

        // ONE FACILITY FOR "remove the first entry of this type and report
        // whether there was one" (plan 1.8), generalising the shape
        // Marks.ConsumeMark already had so Crownfall and Ashen Reckoning need
        // no bespoke consume of their own. The caller keys its bonus off the
        // RETURN VALUE and the spent entry's own Magnitude/TurnsRemaining,
        // never off a separate presence read followed by a second call --
        // Marks.ConsumeMark's own header already argued this for the mark,
        // and it holds for every other spendable status just as well.
        //
        // REMOVES EXACTLY ONE ENTRY, not every one of the type. Poison stacks
        // (StackPolicyOf), and StatusCombos.SpendPoisonIfMatched calls this in
        // a LOOP to eat a whole pile one instance at a time -- a caller that
        // wants every instance of a type asks for that itself, the same way
        // Tick already loops the list rather than this method looping for it.
        public static bool TrySpend(List<ActiveStatus> statuses, StatusEffectType type, out ActiveStatus spent)
        {
            spent = statuses?.FirstOrDefault(s => s.Type == type);
            if (spent == null) return false;

            statuses.Remove(spent);
            return true;
        }

        // However far Protect stacks, a combatant can never be made
        // literally unhittable by a buff — the same floor
        // ScalingProfile.MinimumMultiplier and every max(1, ...) in
        // CombatMath already hold every other damage-reducing source to.
        public const float MinimumDamageTakenMultiplier = 0.1f;

        // Protect and Vulnerable combine into ONE multiplier, additively —
        // the same "a player can add the contributions up in their head"
        // rule ScalingProfile.MultiplierFor uses, for the same reason: two
        // stacked Vulnerables should read as a bigger number, not a
        // surprise product.
        public static float DamageTakenMultiplier(List<ActiveStatus> statuses)
        {
            float total = 1f;
            foreach (var status in statuses)
            {
                if (status.Type == StatusEffectType.Protect)
                {
                    total -= status.Magnitude / 100f;
                }
                else if (status.Type == StatusEffectType.Vulnerable || status.Type == StatusEffectType.Feared)
                {
                    // Feared's Vulnerable half rides the same additive term
                    // Vulnerable itself uses -- see StatusEffectType.Feared's
                    // own header.
                    total += status.Magnitude / 100f;
                }
            }

            return Math.Max(MinimumDamageTakenMultiplier, total);
        }

        // ===== WARDS =====================================================
        //
        // A WARD IS A SHIELD: a pool of shield POINTS, carried on
        // StatusEffectType.Shielded's Magnitude. Incoming damage comes off the
        // pool before it comes off health, so a hit bigger than the pool
        // carries the rest through and a hit smaller than it leaves the pool
        // standing with less in it.
        //
        // A WARD'S CLOCK TICKS AT THE END OF THE WEARER'S OWN TURN, and it is
        // the only duration in the game that does -- every other status counts
        // down at the START of its holder's turn, in Tick. The difference is
        // one turn of visibility, and it is the whole reason for the
        // exception: a ward counted down at turn start is applied during turn
        // N and taken at the start of N+1, so control never returns to the
        // player with the ward still up. They could not see the badge, and
        // Shatter -- "detonate the wards you have out" -- could not reach a
        // ward cast the turn before (AUDIT #153, owner's answer 2026-09-16).
        //
        // THE TURN IT WENT UP DOES NOT COUNT. A ward raised during the
        // wearer's own turn N is exempt from that turn's end tick, so a
        // one-turn ward covers the enemy phase after N and stands through the
        // whole of N+1, going at the end of it. FightSession decides the
        // exemption (it is the only thing that knows whose turn it is) and
        // hands the spared entries to TickAtTurnEnd, which spends them.
        //
        // A WARD RAISED ON SOMEBODY ELSE'S TURN IS NOT EXEMPTED, and that is
        // the same rule rather than an exception to it: the turn it went up
        // on was not the wearer's, so the wearer's next turn end is already
        // the first of their N. A two-turn ward Odette puts on Shawn stands
        // through Shawn's next two turns, exactly like one he raises himself.
        //
        // N turns therefore means "standing through the wearer's next N
        // turns", which is what an author says out loud -- the same spelling
        // rule FightSession.Cooldowns already follows for a cooldown.

        // WARDS STACK (owner, 2026-09-16). A character may carry SEVERAL ward
        // entries at once; their shield TOTAL is the sum of the live ones. A
        // new ward is a new entry -- it never replaces, refreshes or refuses
        // an existing one. This replaced a one-ward-per-character rule that
        // shipped for a matter of hours: the bigger pool won and a thinner
        // ward was refused out loud, which made The Flock's half-strength
        // share bounce off anybody already covered and made every free relic
        // ward a coin flip against whatever the player had spent a turn on.
        //
        // DAMAGE DRAINS THE ENTRY THAT EXPIRES SOONEST FIRST, then the next,
        // and an entry emptied by a hit is removed. Soonest-first is the only
        // order that does not waste shield: spending the pool that was about
        // to lapse anyway keeps the durable one for the hit after this. Ties
        // break by age, oldest first, so the order is total and two repaints
        // of the same board can never disagree. An entry that never expires
        // (see NeverExpires) sorts LAST however few turns its counter says,
        // because it is not going anywhere.
        //
        // IT USED TO BE A PERCENTAGE off one hit -- `damage - damage *
        // Magnitude / 100`, spent whole by the first blow that landed, with
        // every ward applied at 999 turns so none of them ever expired on a
        // clock. That model is gone (AUDIT #152). Nothing anywhere reads a
        // ward's Magnitude as a percent any more, and the two readings must
        // never coexist -- which is why Apply throws on Shielded rather than
        // leaving a second door into this status open.
        //
        // WHERE A WARD SITS IN THE ORDER A HIT MEETS ITS DEFENCES, stated
        // HERE and nowhere else, because it is the step most likely to be
        // quietly re-ordered:
        //
        //   dodge -> effectiveness/Protect/Vulnerable/resistance
        //     -> variance roll -> flat plating (Stalwart)
        //     -> THE WARD POOLS -> the signature pool (Wool) -> health
        //
        // The first five live in DamagePipeline.AfterDefences, which reaches
        // ConsumeWard through its `resolveWard` hook; the last two live in
        // CombatMath.ApplyDamageDetailed. Ward before Wool is the owner's
        // stated order, and it is the one that reads right: the thing you
        // spent a turn putting up is spent before the thing that comes back
        // on its own.

        // WHOSE WARDS IGNORE THE CLOCK: The Golden Fleece belongs to the
        // CASTER, not to the wearer (owner, 2026-09-16). A ward Odette put on
        // Shawn expires on schedule even though Shawn holds the capstone; a
        // Flock share Shawn spreads onto an ally is Shawn's work and never
        // expires, even though the ally holds nothing.
        //
        // That is why this reads Source rather than the holder, and why it is
        // evaluated at every tick rather than baked into the duration when the
        // ward went up: buying the capstone mid-run has to make the wards
        // already out permanent, and an authored 999 could not be taken back
        // if it were ever lost.
        public static bool NeverExpires(ActiveStatus ward)
        {
            if (ward == null || ward.Type != StatusEffectType.Shielded) return false;

            var talents = ward.Source?.Talents;
            return talents != null && talents.Has(TalentEffectType.WardsNeverExpire);
        }

        // Every live ward on this combatant, in the order damage drains them.
        //
        // ONE IMPLEMENTATION OF THE ORDER, so ConsumeWard, the HUD summary and
        // every test read the same sequence rather than three sorts that agree
        // until one of them is edited.
        public static IReadOnlyList<ActiveStatus> WardsInDrainOrder(CombatantState combatant)
        {
            if (combatant == null) return Array.Empty<ActiveStatus>();

            return combatant.Statuses
                .Select((status, index) => (status, index))
                .Where(pair => pair.status.Type == StatusEffectType.Shielded)
                .OrderBy(pair => NeverExpires(pair.status) ? int.MaxValue : pair.status.TurnsRemaining)
                .ThenBy(pair => pair.index)
                .Select(pair => pair.status)
                .ToList();
        }

        // How many shield points this combatant is carrying in total, 0 for
        // unwarded. The number the badge draws.
        public static int WardPoints(CombatantState combatant)
        {
            if (combatant == null) return 0;

            int total = 0;
            foreach (var status in combatant.Statuses)
            {
                if (status.Type == StatusEffectType.Shielded) total += status.Magnitude;
            }

            return total;
        }

        // The whole ward state of one combatant as one value, for a HUD that
        // draws ONE badge however many entries are underneath it.
        //
        // Here rather than in StatusHud because the list is this file's, and a
        // second walk of it living next to the tooltip text is exactly how the
        // badge and the damage funnel would come to disagree about what a
        // character is carrying.
        public readonly struct WardSummary
        {
            public readonly int Points;

            // How many separate entries make up that total.
            public readonly int Entries;

            // Turns left on the entry that will lapse first, or 0 when every
            // entry is permanent. Read together with AllPermanent, never
            // alone: a 0 here means "nothing is on a clock", not "it goes now".
            public readonly int SoonestTurns;

            public readonly bool AllPermanent;

            public bool Any => Entries > 0;

            public WardSummary(int points, int entries, int soonestTurns, bool allPermanent)
            {
                Points = points;
                Entries = entries;
                SoonestTurns = soonestTurns;
                AllPermanent = allPermanent;
            }
        }

        public static WardSummary SummariseWards(CombatantState combatant)
        {
            int points = 0;
            int entries = 0;
            int soonest = 0;
            bool anyExpiring = false;

            foreach (var ward in WardsInDrainOrder(combatant))
            {
                points += ward.Magnitude;
                entries++;

                if (NeverExpires(ward)) continue;

                // Drain order is soonest-first among the expiring ones, so the
                // first one that reaches here IS the soonest.
                if (!anyExpiring)
                {
                    anyExpiring = true;
                    soonest = ward.TurnsRemaining;
                }
            }

            return new WardSummary(points, entries, soonest, entries > 0 && !anyExpiring);
        }

        // PUTS UP ANOTHER WARD. Always a new entry: wards stack, and nothing
        // here reads, merges with or refuses what is already on the combatant.
        //
        // Non-positive points are the one no-op, and it is a guard rather than
        // a rule -- a shield of nothing is an authoring mistake
        // (SkillEntryResolver refuses a sizeless Ward) or a Flock share of a
        // ward that was already nothing.
        // RETURNS THE ENTRY IT PUT UP, or null for the non-positive no-op.
        // The caller needs it: a ward does not count the wearer's turn it was
        // raised on, and the only way to say "this one, not that one" about
        // two pools with the same size and the same clock is to hold the
        // entry itself.
        public static ActiveStatus ApplyWard(List<ActiveStatus> statuses, int points, int turns,
            CombatantState source = null)
        {
            if (statuses == null || points <= 0) return null;

            var ward = new ActiveStatus(StatusEffectType.Shielded, points, turns, source);
            statuses.Add(ward);
            return ward;
        }

        // WHAT ONE WARD ENTRY DID TO THIS HIT. A hit that reaches through two
        // pools produces two of these, in drain order.
        //
        // Per-entry rather than one summed answer because both of the Lamb's
        // nodes that read a ward's absorption are keyed to WHO CAST IT --
        // Mending Fleece heals by the caster's talent, and the wool engine
        // pays the caster -- and a total cannot say whose.
        public readonly struct WardHit
        {
            // Who put that entry up, or null for the relic wards nobody
            // sourced.
            public readonly CombatantState Caster;

            public readonly int Absorbed;

            // Whether this hit emptied that entry, so it is gone.
            public readonly bool Broke;

            // What the wearer was healed by that entry breaking.
            public readonly int Healed;

            public WardHit(CombatantState caster, int absorbed, bool broke, int healed)
            {
                Caster = caster;
                Absorbed = absorbed;
                Broke = broke;
                Healed = healed;
            }
        }

        // What every ward on the target did to one hit.
        public readonly struct WardOutcome
        {
            // What is left to take off health once the pools have eaten what
            // they can.
            public readonly int Damage;

            // Who owned the entry that took the FIRST bite -- the head of the
            // drain order. Null when nothing was warded, and null when the
            // entry that absorbed first was an unsourced relic ward.
            //
            // Kept because "was this combatant warded when it was struck" is
            // still a question with one answer for the overwhelmingly common
            // single-ward case. Anything that has to be right about SEVERAL
            // casters reads Hits instead.
            public readonly CombatantState WardedBy;

            // Totals across every entry this hit touched.
            public readonly int Healed;
            public readonly int Absorbed;

            // Whether any entry was emptied by this hit.
            public readonly bool Broke;

            public readonly IReadOnlyList<WardHit> Hits;

            public WardOutcome(int damage, int healed, int absorbed, bool broke, IReadOnlyList<WardHit> hits)
            {
                Damage = damage;
                Healed = healed;
                Absorbed = absorbed;
                Broke = broke;
                Hits = hits ?? Array.Empty<WardHit>();
                WardedBy = Hits.Count > 0 ? Hits[0].Caster : null;
            }
        }

        private static readonly WardOutcome NothingHappened =
            new WardOutcome(0, 0, 0, false, Array.Empty<WardHit>());

        // SPENDS SHIELD POINTS, soonest-to-lapse pool first, and removes each
        // entry as it runs out.
        //
        // Non-positive damage does not touch anything -- nothing hit the
        // shield, so there is nothing to spend on it.
        //
        // `heal` is where a breaking ward's Mending Fleece heal lands: null
        // (every pure-Domain caller) heals through CombatMath.Heal and
        // measures it, as it always did; FightSession passes its heal funnel
        // without heal triggers (HealWithoutTriggers), so the heal is booked
        // and Cursed Blood can convert it (plan 4b). See HealSink.
        public static WardOutcome ConsumeWard(CombatantState target, int damage, HealSink heal = null)
        {
            if (target == null || damage <= 0)
            {
                return new WardOutcome(Math.Max(0, damage), 0, 0, false, Array.Empty<WardHit>());
            }

            var wards = WardsInDrainOrder(target);
            if (wards.Count == 0)
            {
                return new WardOutcome(damage, 0, 0, false, Array.Empty<WardHit>());
            }

            int remaining = damage;
            int totalAbsorbed = 0;
            int totalHealed = 0;
            bool anyBroke = false;
            var hits = new List<WardHit>();

            foreach (var ward in wards)
            {
                if (remaining <= 0) break;

                int absorbed = Math.Min(ward.Magnitude, remaining);
                if (absorbed <= 0) continue;

                ward.Magnitude -= absorbed;
                remaining -= absorbed;
                totalAbsorbed += absorbed;

                if (ward.Magnitude > 0)
                {
                    hits.Add(new WardHit(ward.Source, absorbed, broke: false, healed: 0));
                    continue;
                }

                target.Statuses.Remove(ward);
                anyBroke = true;

                int healed = HealForBrokenWard(target, ward.Source, heal);
                totalHealed += healed;
                hits.Add(new WardHit(ward.Source, absorbed, broke: true, healed));
            }

            return new WardOutcome(remaining, totalHealed, totalAbsorbed, anyBroke, hits);
        }

        // Mending Fleece T3, by the CASTER's talent and not the wearer's. A
        // ward Shawn put on the turtle heals by Shawn's node, and the turtle
        // has no say in it. Null source (the relic wards) reads as absent.
        private static int HealForBrokenWard(CombatantState wearer, CombatantState caster, HealSink heal)
        {
            var talents = caster?.Talents ?? TalentEffectSet.Empty;

            int healPercent = talents.Best(TalentEffectType.WardHealsWhenSpent);
            if (healPercent <= 0 || wearer.MaxHealth <= 0) return 0;

            return (heal ?? MeasuredHeal)(wearer, wearer.MaxHealth * healPercent / 100);
        }

        // WHERE A HEAL THIS CLASS CAUSES ACTUALLY LANDS, and what it restored.
        // The heal twin of DotPacketSink: this class is pure Domain and cannot
        // reach the session's funnel, so the session hands it in.
        // FightSession passes HealWithoutTriggers -- the one funnel every
        // in-fight heal goes through, where Cursed Blood converts and Ignore
        // Pain T3 pays down its pool (plan 4b), minus the heal-triggered
        // relics these paths never fired. Returns the health restored.
        public delegate int HealSink(CombatantState target, int amount);

        // The pure default: heal, clamped at max health, and measure it.
        private static int MeasuredHeal(CombatantState target, int amount)
        {
            int before = target.CurrentHealth;
            CombatMath.Heal(target, amount);
            return target.CurrentHealth - before;
        }

        // THE END-OF-TURN CLOCK: counts down every AtTurnEnd status the actor
        // whose turn is ending carries, removes what ran out, and reports the
        // types that went -- one entry per INSTANCE removed, so a caller can
        // tell two chills lapsing from one.
        //
        // IT USED TO BE TickWardsAtTurnEnd, and wards were the only thing on
        // this clock. They are not any more: Protect, Vulnerable, Chilled,
        // Rooted and Marked moved here on 2026-09-20 (plan D1) because a
        // status counted down at turn START is removed before the action of
        // its last counted turn ever happens -- a "turns: 2" Vulnerable
        // exposed its bearer for one turn, not two. The whole argument the
        // WARDS header above makes for a ward's visibility is the same
        // argument for every standing modifier, so there is now one turn-end
        // clock rather than a ward exception.
        //
        // `spared` is the entries applied during THIS WEARER'S turn that is
        // ending, which do not count it -- see the WARDS header for why, and
        // FightSession.Riders for who decides what goes in it. An entry found
        // there is SPENT from it here: skipped once, then aged like anything
        // else at the wearer's next turn end. Spent BEFORE the Golden Fleece
        // check so a never-expiring ward does not linger in the collection.
        // Null means "nothing was spared", which is most turns.
        //
        // A ward whose caster holds The Golden Fleece is skipped outright:
        // that capstone is a stopped clock, not a bigger number, so there is
        // nothing here for it to count. NeverExpires answers false for every
        // non-ward, so the check costs nothing for the rest.
        public static IReadOnlyList<StatusEffectType> TickAtTurnEnd(CombatantState wearer,
            ICollection<ActiveStatus> spared = null)
        {
            if (wearer == null) return Array.Empty<StatusEffectType>();

            List<StatusEffectType> expired = null;

            // Materialised, because removing from the list below would
            // otherwise invalidate the walk.
            foreach (var status in wearer.Statuses.ToList())
            {
                if (DurationClock(status.Type) != StatusClock.AtTurnEnd) continue;
                if (spared != null && spared.Remove(status)) continue;
                if (NeverExpires(status)) continue;

                status.TurnsRemaining--;
                if (status.TurnsRemaining > 0) continue;

                wearer.Statuses.Remove(status);
                (expired ??= new List<StatusEffectType>()).Add(status.Type);
            }

            return expired ?? (IReadOnlyList<StatusEffectType>)Array.Empty<StatusEffectType>();
        }

        // Somebody who has warded this combatant, or null. The engine's
        // question, asked BEFORE a hit resolves.
        //
        // WITH STACKING THIS IS "SOMEBODY", NOT "THE ONE": it answers with the
        // head of the drain order's source. Anything asking about a PARTICULAR
        // caster -- Weight of Wool, Shatter's eligibility, the self-ward grace
        // period -- asks IsWardedBy instead, which cannot be fooled by a relic
        // ward sitting in front.
        public static CombatantState WardedBy(CombatantState combatant)
        {
            foreach (var ward in WardsInDrainOrder(combatant))
            {
                if (ward.Source != null) return ward.Source;
            }

            return null;
        }

        // Whether `caster` has a ward standing on `wearer`.
        public static bool IsWardedBy(CombatantState wearer, CombatantState caster)
        {
            if (wearer == null || caster == null) return false;

            foreach (var status in wearer.Statuses)
            {
                if (status.Type == StatusEffectType.Shielded && ReferenceEquals(status.Source, caster))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsWarded(CombatantState combatant)
        {
            return combatant != null && combatant.Statuses.Any(s => s.Type == StatusEffectType.Shielded);
        }

        // Spends Gift: Fury on the swing that is happening now, and reports
        // how much more it is worth. Zero for the overwhelming majority of
        // swings, which carry no gift.
        //
        // Consume-and-remove rather than a passive multiplier: "your next
        // attack" is one swing whenever it arrives, not a window that decays.
        // This is the shape Shielded used to share and no longer does -- a
        // ward is a pool that drains and runs a real clock, a gift is still
        // all-or-nothing on one swing.
        public static int ConsumeEmpowerment(CombatantState attacker)
        {
            if (attacker == null)
            {
                return 0;
            }

            var gift = attacker.Statuses.FirstOrDefault(s => s.Type == StatusEffectType.Empowered);
            if (gift == null)
            {
                return 0;
            }

            attacker.Statuses.Remove(gift);
            return gift.Magnitude;
        }

        // What Gift: Fury is worth WITHOUT spending it. The read-only half of
        // ConsumeEmpowerment, and the only thing a preview may ask: a skill
        // card that burned the gift by being looked at would be a worse bug
        // than the stale number that made this necessary. Same FirstOrDefault
        // as above deliberately, so the two can never name different gifts.
        public static int EmpowermentWorth(CombatantState attacker) =>
            attacker?.Statuses.FirstOrDefault(s => s.Type == StatusEffectType.Empowered)?.Magnitude ?? 0;

        // WHAT A DAMAGING STATUS'S DAMAGE IS MADE OF, and null for the ten
        // that deal none.
        //
        // Keyed on the status TYPE because that is what the element is a
        // property of: a Poison tick is poison damage whoever applied it and
        // whatever they happen to swing. It is emphatically NOT the holder's
        // or the source's attack type -- the source is usually long dead by
        // the time a tick lands, which is the same fact
        // FightSession.Ledger's DealStatusTickPacket already states about
        // the ledger.
        //
        // ONE HOME, because two things read it and they must not disagree:
        // the tick's damage popup and the tick's hit flash both colour
        // themselves through FightHudPalette.ForDamageType. A second
        // damaging status (a burn, a bleed) gets both for free by answering
        // here; nothing in the view learns its name.
        //
        // NOT A CONTENT FIELD. StatusEffectType is a Domain enum and skills
        // .json only names members of it, so there is no Raw*Entry to grow
        // and nothing for docs/CONTENT_SCHEMA.md (which is generated from the
        // types) to say about this.
        //
        // NULLABLE rather than defaulting to Physical: "this status deals no
        // damage" and "this status deals untyped damage" are different
        // answers, and a caller that has to tell them apart should not have
        // to know that Physical is the enum's zero.
        public static DamageType? ElementOf(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Poison: return DamageType.Poison;
                case StatusEffectType.Burn: return DamageType.Fire;
                case StatusEffectType.Thorned: return DamageType.Nature;
                case StatusEffectType.Bleed: return DamageType.Physical;

                // Everything else on the list changes a number, skips a turn
                // or absorbs a hit. None of them deal damage of their own, so
                // none of them have an element to report -- and a new member
                // that DOES has to be added here or
                // StatusEffectsTests.EveryStatusTypeAnswersElementOf fails
                // rather than silently painting its ticks Physical red.
                default: return null;
            }
        }

        // HOW MUCH DEFENCE A DAMAGING STATUS'S TICK MEETS (plan 1.5/D4). Only
        // ever asked of a type ElementOf answers -- a status with no element
        // has no tick to mitigate -- so this is not walked by a vacuity-
        // guarded "every member answers" test the way DurationClock is.
        public static StatusMitigation MitigationOf(StatusEffectType type)
        {
            switch (type)
            {
                // Preserved exactly: Poison has never taken a defence term of
                // any kind, and this plan does not touch that.
                case StatusEffectType.Poison: return StatusMitigation.None;

                // The new DoTs (plan 1.5): affinity only, no flat defense, no
                // ward, no Protect/Vulnerable, no variance.
                case StatusEffectType.Burn:
                case StatusEffectType.Thorned:
                    return StatusMitigation.AffinityOnly;

                // A cut meets armour (Bellwether kit 3.2): defence and
                // affinity, on a hit's curve.
                case StatusEffectType.Bleed:
                    return StatusMitigation.Armoured;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type,
                        "StatusEffects.MitigationOf was asked about a status with no element to mitigate -- "
                        + "only a damaging status (ElementOf answers non-null) should ever reach here.");
            }
        }

        // ONE DAMAGING STATUS FIGURE, MITIGATED AND DEALT -- the half of a
        // tick both StatusEffects.Tick's turn-start pass and FightSession's
        // post-action retaliation hook (plan 1.11) need identically, so a
        // status cannot be mitigated one way on the clock and another way off
        // it. `magnitude` is the raw stored snapshot (ActiveStatus.Magnitude);
        // this applies MitigationOf(type) -- affinity only, for the new DoTs,
        // nothing at all for Poison -- rounds away from zero and floors at 1,
        // the same shape CombatMath.ApplyEffectiveness already uses for a
        // typed hit. `holder` is read only by Armoured, for its defence.
        private static int MitigatedTickAmount(int magnitude, StatusEffectType type, DamageType element,
            ElementalAffinity affinity, CombatantState holder)
        {
            switch (MitigationOf(type))
            {
                case StatusMitigation.None:
                    return magnitude;
                case StatusMitigation.AffinityOnly:
                {
                    float multiplier = CombatMath.EffectivenessMultiplier(element, affinity);
                    return CombatMath.ApplyEffectiveness(magnitude, multiplier);
                }
                case StatusMitigation.Armoured:
                {
                    // The order a typed hit uses (DamagePipeline.AfterDefences):
                    // affinity first, then the defence curve. No attacker, so
                    // no penetration term; the source is usually gone by now.
                    float multiplier = CombatMath.EffectivenessMultiplier(element, affinity);
                    int afterAffinity = CombatMath.ApplyEffectiveness(magnitude, multiplier);
                    return CombatMath.AfterResistance(afterAffinity,
                        CombatMath.TotalDefense(holder, element, null, ignoresDefense: false));
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type,
                        "StatusEffects.MitigatedTickAmount has no arithmetic for this mitigation kind.");
            }
        }

        // ONE ROW PER DAMAGING STATUS TYPE PER TICK (plan D5, closing AUDIT
        // #188). A holder carrying three Poison instances and one Burn
        // reports two rows -- Poison's summed across its three, Burn's alone
        // -- never four, and never one row that tries to average two
        // elements together. `ToHealth` and `Absorbed` are the same split
        // PoisonDamage/PoisonAbsorbed used to keep: what left the pool
        // versus what left health, so a signature pool eating a tick whole
        // is still counted rather than reading as "nothing happened".
        //
        // `Thrown` is the third figure: the mitigated total SENT at the holder,
        // before anything ate it. It differs from ToHealth + Absorbed when a
        // sink turned a packet aside (Kinship), a Phoenix Egg shell took it, a
        // spike cap shrank it, or it was overkill. It is what the pools hear,
        // because the funnel's rule for the pools is "the blow was thrown".
        public readonly struct TickRow
        {
            public readonly StatusEffectType Status;
            public readonly DamageType Element;
            public readonly int ToHealth;
            public readonly int Absorbed;
            public readonly int Thrown;

            public TickRow(StatusEffectType status, DamageType element, int toHealth, int absorbed, int thrown = 0)
            {
                Status = status;
                Element = element;
                ToHealth = toHealth;
                Absorbed = absorbed;
                Thrown = thrown;
            }
        }

        // WHERE ONE INSTANCE'S MITIGATED TICK IS LANDED, returning what a
        // signature pool absorbed of it (CombatMath.ApplyDamage's own answer).
        // Null means CombatMath.ApplyDamage itself -- the pure path every
        // Domain-only caller and test takes. FightSession supplies its damage
        // funnel instead (FightSession.Ledger.DealStatusTickPacket), so Kinship,
        // the Phoenix Egg, the ledger and the death settlement hear a tick the
        // way they hear every other packet, while the mitigation above stays
        // exactly this file's (no DamagePipeline, as before).
        public delegate int DotPacketSink(CombatantState holder, int amount, DamageType element);

        public readonly struct TickReport
        {
            // One entry per damaging status TYPE this pass touched, never per
            // instance -- StackPolicyOf already sums instances of one type
            // into one badge (D3), and the report follows the same rule so a
            // consumer cannot tell three Poisons and one big one apart, which
            // is exactly the point.
            public readonly IReadOnlyList<TickRow> Rows;

            public readonly int RegenHealed;
            public readonly IReadOnlyList<StatusEffectType> Expired;

            public TickReport(IReadOnlyList<TickRow> rows, int regenHealed,
                              IReadOnlyList<StatusEffectType> expired)
            {
                Rows = rows;
                RegenHealed = regenHealed;
                Expired = expired;
            }

            public bool IsEmpty => Rows.Count == 0 && RegenHealed == 0 && Expired.Count == 0;
        }

        // THE SHARED HALF OF A TICK: sum, mitigate and deal ONE damaging
        // status type's live instances to its holder, per instance (so
        // absorption and the spike cap see the same sequence a turn-start
        // tick always has), WITHOUT touching duration at all.
        //
        // `instances` IS NOT ALWAYS "whatever is on the combatant right now".
        // Thorned's post-action retaliation (plan 1.11/2.12) fires AFTER the
        // SAME turn's opening tick may already have removed the instance that
        // paid for it -- Thorned is AtTick like every other DoT (1.4), so its
        // final tick and its removal are the same pass, at that turn's own
        // START, before the actor has even acted. FightSession snapshots the
        // live instances before calling StatusEffects.Tick and hands that
        // snapshot back here for the retaliation, which is what lets "the
        // tick removed it" and "this turn still owes a retaliation" both be
        // true without Thorned needing a clock of its own. The instance
        // OBJECTS themselves outlive their removal from the list -- only the
        // list forgets them -- so their Magnitude is exactly the stored
        // snapshot 1.5 promises, read however long after application.
        //
        // Returns null when nothing of this type was live (or none of it had
        // a positive Magnitude) -- the empty case, told apart from "dealt
        // zero" the same way every other Tick-adjacent report does.
        //
        // `sink` lands each instance (see DotPacketSink); null is the pure
        // CombatMath path. Still ONE CALL PER INSTANCE either way, so Last
        // Stand's spike cap and a soaking pool see the same sequence as ever.
        // ToHealth is MEASURED off health, never trusted from the sink.
        public static TickRow? ApplyDotDamage(CombatantState combatant, StatusEffectType type,
            IEnumerable<ActiveStatus> instances, ElementalAffinity affinity, DotPacketSink sink = null)
        {
            var element = ElementOf(type);
            if (combatant == null || element.HasValue == false) return null;

            bool any = false;
            int toHealth = 0;
            int absorbed = 0;
            int thrown = 0;

            foreach (var status in instances)
            {
                if (status.Type != type || status.Magnitude <= 0) continue;
                any = true;

                int amount = MitigatedTickAmount(status.Magnitude, type, element.Value, affinity, combatant);
                int before = combatant.CurrentHealth;
                absorbed += sink != null
                    ? sink(combatant, amount, element.Value)
                    : CombatMath.ApplyDamage(combatant, amount);
                toHealth += before - combatant.CurrentHealth;
                thrown += amount;
            }

            return any ? new TickRow(type, element.Value, toHealth, absorbed, thrown) : (TickRow?)null;
        }

        // WHEN THIS STATUS'S COUNTER MOVES. The one table, replacing the
        // IsSpentByTheTurn predicate plus the separate ward arrangement that
        // stood before 2026-09-20 -- see StatusClock's own header for what each
        // family means and what the old two-and-a-half-family shape got wrong.
        //
        // THROWS on an unhandled member rather than defaulting. There is no
        // safe default: falling into AtTick would tick a standing modifier down
        // a turn early and look like nothing at all, which is precisely the bug
        // this table exists to close. StatusEffectsTests
        // .EveryStatusTypeAnswersDurationClock walks Enum.GetValues, so a new
        // member fails a test rather than a fight.
        public static StatusClock DurationClock(StatusEffectType type)
        {
            switch (type)
            {
                // The tick that deals or heals IS the countdown, and an entry
                // reaching zero goes in that same pass: N authored = N ticks.
                case StatusEffectType.Poison:
                case StatusEffectType.Regen:
                case StatusEffectType.Burn:
                case StatusEffectType.Thorned:
                case StatusEffectType.Bleed:
                    return StatusClock.AtTick;

                // Spent, not aged. ConsumeStun (Stun and Feared),
                // ConsumeProvoke, ConsumeEmpowerment each move their own
                // counter at the moment the effect is actually spent, so the
                // turn a Stun promised always happens before the count moves.
                case StatusEffectType.Stun:
                case StatusEffectType.Feared:
                case StatusEffectType.Provoked:
                case StatusEffectType.Empowered:
                    return StatusClock.AtUse;

                // Standing modifiers, aged at the END of the bearer's turn and
                // exempt from the end of a turn they were applied during. N
                // authored = N of the bearer's turns fully covered, the
                // modifier intact through the final affected action.
                case StatusEffectType.Protect:
                case StatusEffectType.Vulnerable:
                case StatusEffectType.Chilled:
                case StatusEffectType.Rooted:
                case StatusEffectType.Marked:
                case StatusEffectType.Shielded:
                    return StatusClock.AtTurnEnd;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type,
                        "StatusEffects.DurationClock has no family for this status -- a duration with no "
                        + "clock would either never expire or expire a turn early, and both look like "
                        + "nothing on the badge.");
            }
        }

        // The start-of-turn tick: every AtTick damaging status deals its
        // Magnitude through the shared ApplyDotDamage half (Poison and the
        // new DoTs alike -- a status is not a special case the pipeline has
        // to know about twice), Regen heals through the HealSink, and the
        // AtTick family's duration counts down by one of the HOLDER's own
        // turns, anything reaching zero being removed in the same pass.
        //
        // ONLY THE AtTick FAMILY. It used to be every status except three
        // hand-listed exemptions and wards; the AtUse family has no clock at
        // all and the AtTurnEnd family is counted by TickAtTurnEnd. See
        // DurationClock.
        //
        // EVERY INSTANCE TICKS, GROUPED INTO ONE ROW PER TYPE (plan D5). A
        // holder carrying three Poison instances and one Burn takes all four
        // in this one pass and reports two rows -- Poison's sum, Burn's
        // alone -- never four rows and never one row averaging two elements.
        //
        // `affinity` is the HOLDER's own (CombatMath.EffectivenessMultiplier's
        // second argument) and defaults to Neutral, which is a no-op for
        // Poison (MitigationOf(Poison) == None reads it never) and for every
        // caller that predates the new DoTs -- FightSession is the one
        // caller that must pass the real figure, because it is the only
        // layer that can look one up (AffinityOf reads the session's own
        // enemy-kit table, which this pure-Domain class cannot reach).
        //
        // THREE HALVES, and FightSession calls them one at a time rather than
        // through this method: DamagingTypesDue, then ApplyDotDamage per type
        // (with its funnel as the sink, and each row's beat opened BEFORE the
        // row lands, so a line the funnel says -- Kinship, an egg hatching --
        // sits on the tick's own beat), then TickRegenAndDurations. This
        // method is those three in that order with the pure sink, which is
        // what every Domain-only caller wants.
        public static TickReport Tick(CombatantState combatant, ElementalAffinity affinity = default)
        {
            var rows = new List<TickRow>();

            foreach (var type in DamagingTypesDue(combatant))
            {
                var row = ApplyDotDamage(combatant, type,
                    combatant.Statuses.Where(s => s.Type == type), affinity);
                if (row.HasValue) rows.Add(row.Value);
            }

            var (regenHealed, expired) = TickRegenAndDurations(combatant);
            return new TickReport(rows, regenHealed, expired);
        }

        // The damaging AtTick types the holder carries, in first-carried
        // order, read ONCE before anything lands -- so a type whose last
        // instance a death or a hatch changes mid-tick is still its own row.
        public static List<StatusEffectType> DamagingTypesDue(CombatantState combatant)
        {
            var types = new List<StatusEffectType>();
            foreach (var status in combatant.Statuses)
            {
                if (DurationClock(status.Type) != StatusClock.AtTick) continue;
                if (!ElementOf(status.Type).HasValue) continue;
                if (!types.Contains(status.Type)) types.Add(status.Type);
            }
            return types;
        }

        // The rest of the turn-start tick, AFTER every damaging row: Regen
        // heals, and the AtTick family counts down and expires.
        //
        // `heal` is where each Regen instance's heal lands: null heals through
        // CombatMath.Heal (MeasuredHeal), the pure behaviour; FightSession
        // passes its funnel (HealWithoutTriggers), so a Regen tick is booked
        // once and converted under Cursed Blood (plan 4b), while heal-triggered
        // relics (World Ender's Crown) still never hear it, as before.
        public static (int regenHealed, List<StatusEffectType> expired) TickRegenAndDurations(
            CombatantState combatant, HealSink heal = null)
        {
            int regenHealed = 0;

            foreach (var status in combatant.Statuses)
            {
                if (DurationClock(status.Type) != StatusClock.AtTick) continue;

                if (status.Type == StatusEffectType.Regen && status.Magnitude > 0)
                {
                    regenHealed += (heal ?? MeasuredHeal)(combatant, status.Magnitude);
                }

                status.TurnsRemaining--;
            }

            var expired = combatant.Statuses
                .Where(s => DurationClock(s.Type) == StatusClock.AtTick && s.TurnsRemaining <= 0)
                .Select(s => s.Type)
                .ToList();
            combatant.Statuses.RemoveAll(
                s => DurationClock(s.Type) == StatusClock.AtTick && s.TurnsRemaining <= 0);

            return (regenHealed, expired);
        }
    }
}
