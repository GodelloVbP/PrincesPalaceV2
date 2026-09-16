using System;
using System.Collections.Generic;
using System.Linq;

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
        // A fresh application of a type ALREADY on the list refreshes
        // duration and takes the stronger magnitude, rather than stacking a
        // second entry. Additive stacking of two Poisons or two Protects is
        // exactly the silent compounding this project's other systems
        // (ScalingProfile's additive-not-multiplicative rule, SpeedScale's
        // hard ceiling) go out of their way to avoid — re-casting the same
        // status should never be strictly better than casting it once.
        //
        // REFUSES Shielded OUTRIGHT. A ward is a POOL of shield points and
        // obeys a different application rule from every other status (see
        // ApplyWard): a bigger pool replaces a smaller one and a smaller one
        // is refused, where this method would quietly keep the larger
        // magnitude AND the longer duration and report nothing. Throwing is
        // the cheap tier-1 version of that rule -- the API cannot express the
        // mistake -- and it is worth a throw rather than a silent redirect
        // because the caller also has a log line to write about the refusal,
        // which only it can word.
        public static void Apply(List<ActiveStatus> statuses, StatusEffectType type, int magnitude, int turns, CombatantState source = null)
        {
            if (type == StatusEffectType.Shielded)
            {
                throw new ArgumentException(
                    "A ward is a shield POOL with its own replacement rule -- call StatusEffects.ApplyWard, "
                    + "which reports whether the ward landed so the caller can say so.", nameof(type));
            }

            var existing = statuses.FirstOrDefault(s => s.Type == type);
            if (existing != null)
            {
                existing.Magnitude = Math.Max(existing.Magnitude, magnitude);
                existing.TurnsRemaining = Math.Max(existing.TurnsRemaining, Math.Max(1, turns));

                // Re-points the credit at whoever most recently paid for it,
                // but never CLEARS it: a source-less refresh (an enemy's
                // claws re-applying a Poison the mage originally landed) must
                // not quietly disown a status the mage's engine is being paid
                // for. Losing the attribution silently is the failure mode
                // worth guarding, since nothing about it would look wrong —
                // the wool income would just stop.
                if (source != null)
                {
                    existing.Source = source;
                }

                return;
            }

            statuses.Add(new ActiveStatus(type, magnitude, turns, source));
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
        // A WARD IS A SHIELD: a pool of shield POINTS sitting on one
        // character, carried on StatusEffectType.Shielded's Magnitude.
        // Incoming damage comes off the pool first and the remainder off
        // health, so a hit bigger than the pool carries the rest through and
        // a hit smaller than it leaves the pool standing with less in it.
        //
        // IT USED TO BE A PERCENTAGE off one hit -- `damage - damage *
        // Magnitude / 100`, spent whole by the first blow that landed, with
        // every ward applied at 999 turns so none of them ever expired on a
        // clock. That model is gone (AUDIT #152, owner's call 2026-09-16: a
        // ward IS a shield). Nothing anywhere reads a ward's Magnitude as a
        // percent any more, and the two readings must never coexist -- which
        // is why Apply refuses Shielded outright rather than leaving a second
        // door into this status open.
        //
        // WHERE A WARD SITS IN THE ORDER A HIT MEETS ITS DEFENCES, stated
        // HERE and nowhere else, because it is the step most likely to be
        // quietly re-ordered:
        //
        //   dodge -> effectiveness/Protect/Vulnerable/resistance
        //     -> variance roll -> flat plating (Stalwart)
        //     -> THE WARD POOL -> the signature pool (Wool) -> health
        //
        // The first five live in DamagePipeline.AfterDefences, which reaches
        // ConsumeWard through its `resolveWard` hook; the last two live in
        // CombatMath.ApplyDamageDetailed. Ward before Wool is the owner's
        // stated order, and it is the one that reads right: the thing you
        // spent a turn putting up is spent before the thing that comes back
        // on its own.

        // "For the rest of the fight", spelled as a duration -- The Golden
        // Fleece (TalentEffectType.WardsNeverExpire) and the relic wards that
        // promise to stand until something hits them. Comfortably above
        // StatusHud.SentinelTurns, which is what makes the badge read "for
        // the rest of the fight" rather than counting down from a number
        // nobody chose.
        public const int PermanentWardTurns = 999;

        // How many shield points this combatant is carrying, 0 for unwarded.
        public static int WardPoints(CombatantState combatant)
        {
            if (combatant == null) return 0;

            var ward = combatant.Statuses.FirstOrDefault(s => s.Type == StatusEffectType.Shielded);
            return ward?.Magnitude ?? 0;
        }

        // ONE WARD PER CHARACTER, AND THE BIGGER POOL WINS. A new ward
        // replaces a smaller (or equal) one outright -- pool, duration and
        // credit together -- and is REFUSED against a larger one, returning
        // false so the caller can say so rather than eating the cast in
        // silence.
        //
        // Equal replaces rather than refuses, deliberately: re-casting the
        // same ward on the same wearer is how its duration gets refreshed,
        // and a Fleece Ward that refused itself would be a skill that stops
        // working precisely when it is working.
        //
        // WARDS DO NOT STACK, which is the rule Apply runs for every other
        // status and for the same reason -- re-casting should never be
        // strictly better than casting once. What is different here is that a
        // pool CAN be partly spent, so Apply's "keep the stronger magnitude,
        // keep the longer duration" merge is not safe: it would hand back
        // points a hit had already eaten.
        public static bool ApplyWard(List<ActiveStatus> statuses, int points, int turns, CombatantState source = null)
        {
            if (statuses == null || points <= 0) return false;

            var existing = statuses.FirstOrDefault(s => s.Type == StatusEffectType.Shielded);
            if (existing != null)
            {
                if (existing.Magnitude > points) return false;

                statuses.Remove(existing);
            }

            statuses.Add(new ActiveStatus(StatusEffectType.Shielded, points, turns, source));
            return true;
        }

        // What a Ward did to a hit, for a caller that needs to say so.
        //
        // Absorbed and Broke are both here because two of the Lamb's nodes
        // make a ward's ABSORPTION visible in its own right -- Mending Fleece
        // T3 heals the wearer when a ward is spent, and the engine pays wool
        // for having been warded at all -- and neither is inferable from the
        // damage figure afterwards.
        public readonly struct WardOutcome
        {
            // What is left to take off health once the pool has eaten what it
            // can.
            public readonly int Damage;

            // Who put the ward there, or null if there was no ward. Non-null
            // even when the pool SURVIVED the hit, because the question the
            // engine asks is "was this combatant warded when it was struck",
            // not "did something break".
            public readonly CombatantState WardedBy;

            // How much the wearer was healed by the pool being emptied.
            public readonly int Healed;

            // How many points the pool took off this hit.
            public readonly int Absorbed;

            // Whether this hit emptied the pool, so the ward is gone.
            public readonly bool Broke;

            public WardOutcome(int damage, CombatantState wardedBy, int healed, int absorbed, bool broke)
            {
                Damage = damage;
                WardedBy = wardedBy;
                Healed = healed;
                Absorbed = absorbed;
                Broke = broke;
            }
        }

        // SPENDS SHIELD POINTS, and removes the ward when the pool runs out.
        //
        // Non-positive damage does not touch it -- nothing actually hit the
        // shield, so there is nothing to spend on it.
        public static WardOutcome ConsumeWard(CombatantState target, int damage)
        {
            if (target == null || damage <= 0)
            {
                return new WardOutcome(damage, null, 0, 0, false);
            }

            var ward = target.Statuses.FirstOrDefault(s => s.Type == StatusEffectType.Shielded);
            if (ward == null)
            {
                return new WardOutcome(damage, null, 0, 0, false);
            }

            // Whose talents decide what this ward does on the way out -- the
            // one who CAST it, not the one wearing it. A ward Shawn put on the
            // turtle heals by Shawn's Mending Fleece, and the turtle has no
            // say in it. Null for the Magical Shield relic's own shield, which
            // nobody sourced, and every Lamb rule below then reads as absent.
            var caster = ward.Source;
            var casterTalents = caster?.Talents ?? TalentEffectSet.Empty;

            int absorbed = Math.Min(ward.Magnitude, damage);
            ward.Magnitude -= absorbed;
            int through = damage - absorbed;

            // STILL STANDING. A partly-spent pool is the whole point of the
            // shield model: a small hit costs the ward what it was worth and
            // no more.
            //
            // The Golden Fleece does NOT get a branch here any more. It used
            // to mean "the ward never pops", which was the only thing
            // "permanent" could mean while a ward was one hit's worth of
            // percentage; against a pool that reading is literal immunity. It
            // means what it says on the tin instead -- the ward never times
            // out, because ApplyWard is handed PermanentWardTurns -- and the
            // pool drains like everybody else's.
            if (ward.Magnitude > 0)
            {
                return new WardOutcome(through, caster, 0, absorbed, false);
            }

            target.Statuses.Remove(ward);

            int healPercent = casterTalents.Best(TalentEffectType.WardHealsWhenSpent);
            int healed = 0;
            if (healPercent > 0 && target.MaxHealth > 0)
            {
                int before = target.CurrentHealth;
                CombatMath.Heal(target, target.MaxHealth * healPercent / 100);
                healed = target.CurrentHealth - before;
            }

            return new WardOutcome(through, caster, healed, absorbed, true);
        }

        // Who has warded this combatant, or null. The engine's question,
        // asked BEFORE a hit resolves -- by the time damage has landed the
        // pool may have been emptied and the answer is gone.
        public static CombatantState WardedBy(CombatantState combatant)
        {
            if (combatant == null)
            {
                return null;
            }

            foreach (var status in combatant.Statuses)
            {
                if (status.Type == StatusEffectType.Shielded && status.Source != null)
                {
                    return status.Source;
                }
            }

            return null;
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

        public readonly struct TickReport
        {
            // What reached HEALTH.
            public readonly int PoisonDamage;

            // And what the victim's signature pool ate before health was
            // touched. Reported SEPARATELY, and reported at all, because
            // PoisonDamage is measured as health lost: a tick a full Wool pool
            // absorbs outright reduces it to zero, and a zero reads as "the
            // poison did nothing" to every consumer. It did something -- it
            // cost the holder five points of armour -- and the fight's
            // bookkeeping (the absorbed ledger column, the pools' own
            // "this turn was not idle" flag) is owed all of it.
            public readonly int PoisonAbsorbed;

            public readonly int RegenHealed;
            public readonly IReadOnlyList<StatusEffectType> Expired;

            public TickReport(int poisonDamage, int poisonAbsorbed, int regenHealed,
                              IReadOnlyList<StatusEffectType> expired)
            {
                PoisonDamage = poisonDamage;
                PoisonAbsorbed = poisonAbsorbed;
                RegenHealed = regenHealed;
                Expired = expired;
            }

            public bool IsEmpty =>
                PoisonDamage == 0 && PoisonAbsorbed == 0 && RegenHealed == 0 && Expired.Count == 0;
        }

        // The statuses whose whole effect IS the turn they land on, and which
        // are therefore counted down by that turn rather than by the tick that
        // opens it. See Tick's own comment at the countdown for the argument.
        private static bool IsSpentByTheTurn(StatusEffectType type) =>
            type == StatusEffectType.Provoked
            || type == StatusEffectType.Stun
            || type == StatusEffectType.Feared;

        // The start-of-turn tick: Poison and Regen apply their Magnitude
        // through the SAME CombatMath.ApplyDamage/Heal every other source of
        // damage or healing uses — a status is not a special case the
        // pipeline has to know about twice — every status's duration counts
        // down by one of the HOLDER's own turns, and anything that reaches
        // zero is removed.
        public static TickReport Tick(CombatantState combatant)
        {
            int poisonDamage = 0;
            int poisonAbsorbed = 0;
            int regenHealed = 0;

            foreach (var status in combatant.Statuses)
            {
                if (status.Type == StatusEffectType.Poison && status.Magnitude > 0)
                {
                    int before = combatant.CurrentHealth;

                    // ApplyDamage's return IS the absorbed figure (see its own
                    // header -- "Returns how much a signature resource soaked
                    // before health was touched"), so the split costs a local
                    // and nothing else. Taken from the funnel rather than
                    // measured off SignaturePool.Current, which counts POOL
                    // POINTS and is not the same number whenever AbsorbPerPoint
                    // is anything but one.
                    poisonAbsorbed += CombatMath.ApplyDamage(combatant, status.Magnitude);
                    poisonDamage += before - combatant.CurrentHealth;
                }
                else if (status.Type == StatusEffectType.Regen && status.Magnitude > 0)
                {
                    int before = combatant.CurrentHealth;
                    CombatMath.Heal(combatant, status.Magnitude);
                    regenHealed += combatant.CurrentHealth - before;
                }

                // A STATUS SPENT BY THE TURN IS NOT COUNTED DOWN BY IT.
                //
                // This tick runs at the START of the holder's turn, before
                // the thing the status is supposed to change has happened --
                // so counting one down here expires it a beat before it could
                // do anything, and a one-turn application is silently dead
                // content. Provoked was exempted for exactly that reason
                // ("force one enemy to target Shawn on its next turn"), with
                // the note that authoring two turns to work around it would
                // leave a taunt lasting two enemy turns whenever the enemy
                // died to something else first.
                //
                // Stun and Feared have the identical shape and were NOT
                // exempted, which is the bug this predicate closes: both are
                // read by ResolveSkippedTurn, which runs AFTER GrantTurnStart
                // has already ticked. A one-turn Stun -- grapple's authored
                // duration, and Loaded Dice's -- expired at the top of the
                // very turn it existed to skip and cost nothing at all; a
                // Fear was one turn short of what it promised, which for
                // World Ender's Crown's authored 1 also meant nothing at all.
                //
                // Their countdown lives in ConsumeStun instead, beside
                // ConsumeProvoke, at the moment the skip is actually spent.
                if (!IsSpentByTheTurn(status.Type))
                {
                    status.TurnsRemaining--;
                }
            }

            var expired = combatant.Statuses.Where(s => s.TurnsRemaining <= 0).Select(s => s.Type).ToList();
            combatant.Statuses.RemoveAll(s => s.TurnsRemaining <= 0);

            return new TickReport(poisonDamage, poisonAbsorbed, regenHealed, expired);
        }
    }
}
