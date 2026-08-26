using System;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // Everything between a raw damage figure and what the target actually
    // loses.
    //
    // ONE funnel, called by every damage path, because these steps have to
    // travel together. There are five places that deal damage and a sixth is
    // one feature away; each applying effectiveness by hand and remembering
    // resistance (and Protect/Vulnerable, and the poison combo, and the
    // variance roll, and the ward) separately is precisely how a suit of plate
    // ends up working against a sword and not against a spell -- and nothing
    // would fail, the number would just be quietly wrong.
    //
    // This is v1's FightController.AfterDefences, moved to Domain. It is the
    // highest-value extraction in the fight rebuild for one reason: every
    // damage path in the game shares it, and its COMPOSITION ORDER has never
    // had a test. The order below is load-bearing and each position is argued
    // in place.
    //
    // Ward resolution arrives as a delegate rather than being inlined: the
    // Fragile Lamb's engine payout, heal-on-spend and grace period are talent
    // rules, and they land in a later step. Injecting it means the funnel's
    // order is pinnable NOW, with a fake, which is the whole point.
    //
    // THE CANONICAL MITIGATION EQUATION. This is the ONLY place a defense
    // term is ever subtracted from a damage figure — CombatMath.
    // ComputeAttackDamage/ComputeSkillDamage return raw, unmitigated
    // figures (see their own headers), and CombatMath's old Mitigate/
    // ArmourSoftening/EffectiveDefense are gone rather than moved, because
    // having two mitigation steps in two files is exactly the bug this
    // consolidation fixes (a plain attack used to be softened once by the
    // old `Defense` field here in CombatMath and a SECOND time by
    // Physical/MagicalResistance in this file):
    //
    //   D_broad = target's PhysicalDefense or MagicalDefense, picked by
    //             damage type (IsPhysical(type) ? PDEF : MDEF; untyped
    //             damage counts as Physical)
    //     ... Last Stand below threshold:  D_broad *= (1 + bonus%/100)
    //     ... BreakShield broken:          D_broad = 0
    //     ... attacker penetration:        D_broad *= (1 - ignore%/100)
    //   D_typed = target.TypedResistance.For(type)  // never broken, never
    //             penetrated — see CombatMath.TotalDefense's own header
    //   D       = max(0, D_broad) + D_typed
    //   out     = max(1, damage * 100 / (100 + D))  // integer division
    //
    // CombatMath.BroadDefense computes D_broad (with the three modifiers
    // above, in that order); CombatMath.TotalDefense adds D_typed;
    // CombatMath.AfterResistance is the final `out` line. This file only
    // decides WHICH type and WHOSE talents apply — the arithmetic lives in
    // CombatMath so it can be pinned by CombatMathTests without a session.
    //
    // `ignoresDefense` (a skill flag, e.g. one of Shawn's lines) skips
    // D_broad ONLY — typed Resistance still applies. Matches today's
    // behaviour, where ignoresDefense skipped the old EffectiveDefense but
    // never skipped this pipeline's own resistance term.
    //
    // PHASE D1 DODGE. RollDodge is the FIRST thing either overload does —
    // before effectiveness, before resistance, before the poison combo,
    // before the variance roll — because a miss has to short-circuit
    // EVERYTHING below it, not just the damage number. It reuses THIS
    // funnel's own `rng` stream (the identical parameter the variance roll
    // already consumes further down) rather than a second independent draw,
    // and it is gated on `rng != null` the same way ApplyVariance already
    // is, which is what makes every read-only PREVIEW call (PreviewDamage/
    // PreviewSkill in FightSession.Enemies.cs, both of which pass rng:
    // null on purpose so a telegraph never touches the run's generator)
    // automatically skip dodge too, with no separate exemption to remember.
    //
    // WHY BAKED IN HERE rather than as a separate call callers make first:
    // this file's own header already explains why mitigation lives in ONE
    // funnel instead of at each damage path — a rule that has to be
    // remembered separately at N call sites eventually gets forgotten at
    // one of them, which is exactly the shape of bug a prior balance-
    // redesign phase hit (a multiplier removed from two of three real
    // damage entry points, silently missed the third). Putting RollDodge
    // INSIDE AfterDefences means any call to AfterDefences — today's six
    // real damage paths, or a seventh this codebase adds next year — gets
    // the dodge gate for free. A future direct RollDodge-then-AfterDefences
    // pattern at a new call site would reintroduce exactly that risk.
    //
    // WHAT DOES NOT ROUTE THROUGH HERE, ON PURPOSE, AND SO STAYS
    // UNDODGEABLE: splash/kill-splash (Trample, Explosive's
    // OnKillSplashPercent, the Black Ram transform splash, the Lucky Deck
    // relic) and Shatter all apply raw damage straight through DealDamage,
    // never AfterDefences — "the blow that splashes already paid its own
    // armour tax" (see FightSession.Relics.LuckyDeckSplash's own comment,
    // which states the same reasoning for skipping a second mitigation
    // pass). A Poison DoT tick is the same shape: StatusEffects.Tick calls
    // CombatMath.ApplyDamage directly, no pipeline, no target reaction —
    // the tick is not a swing the target is reacting to, it is the
    // continuing cost of a status that was already applied (and that
    // application, if it rode in on a landed hit, was already dodgeable at
    // THAT hit). See ModifierEffectType.DodgeChancePercent's own comment
    // for the fuller argument for why this split is correct rather than
    // arbitrary.
    public static class DamagePipeline
    {
        // +/-20% by default, so two swings that would otherwise deal the
        // identical number read as a roll rather than a rote calculation.
        // 0 disables the roll entirely, which is what the fight tests do so a
        // prediction made through CombatMath directly still matches.
        public const float DefaultVarianceRange = 0.2f;

        public readonly struct Outcome
        {
            public readonly int Damage;
            public readonly float Effectiveness;

            // Reported rather than folded in, because the view announces it
            // separately ("The poison detonates!") and the caller owns the
            // message. Zero when nothing detonated.
            public readonly int PoisonDetonation;

            // TRUE means the swing/cast missed outright — Damage is 0,
            // Effectiveness is the neutral 1f (there is nothing to be
            // effective OR ineffective against; no hit landed), and
            // PoisonDetonation is 0 because nothing landed to detonate
            // anything. A DISTINCT field rather than inferring a miss from
            // Damage == 0 — every real landed hit is already floored at 1
            // by CombatMath.AfterResistance, so Damage == 0 could never
            // legitimately mean anything else today, but a caller reading
            // this outcome should not have to know that floor exists, or
            // rely on it staying true, to tell "armour reduced this to
            // nearly nothing" apart from "nothing happened at all" — see
            // this file's own header, and ModifierEffectType.
            // DodgeChancePercent's comment, for why those two have to read
            // as different information to the player.
            public readonly bool IsMiss;

            public Outcome(int damage, float effectiveness, int poisonDetonation, bool isMiss = false)
            {
                Damage = damage;
                Effectiveness = effectiveness;
                PoisonDetonation = poisonDetonation;
                IsMiss = isMiss;
            }
        }

        // THE ONE ROLL every real damage path shares — see this class' own
        // header for the full argument on why it lives here instead of at
        // each call site. `attacker` is accepted (matching the plan's own
        // named signature) but UNUSED today: nothing in this codebase's
        // ModifierEffectType vocabulary grants an attacker "anti-dodge" or
        // accuracy rule yet, so this is a future-proofing parameter rather
        // than dead weight to be trimmed — the day an accuracy-style effect
        // exists, this is the one place it has to read from both sides
        // that already has both CombatantStates in hand.
        //
        // `rng == null` reads as "never dodge", the exact convention
        // ApplyVariance already uses for the same reason: every PREVIEW
        // call in this codebase passes rng: null specifically so a
        // telegraph cannot consume a draw from the run's own generator, and
        // dodge is exactly as much a "spends a roll" rule as variance is.
        public static bool RollDodge(CombatantState target, CombatantState attacker, SeededRandom rng)
        {
            if (rng == null || target?.ModifierEffects == null)
            {
                return false;
            }

            int dodgeChance = target.ModifierEffects.Best(ModifierEffectType.DodgeChancePercent);

            // Delegates to the same RandomOps.RollPercent every other chance
            // effect in this vocabulary rolls through -- see its own header
            // for the <=0/>=100 short-circuits, kept identical here so a
            // modifier authored (or scaled up) past 100% behaves the same
            // way any other over-100 percent chance already does in this
            // codebase, rather than inventing a second rule for one effect
            // type.
            return RandomOps.RollPercent(rng, dodgeChance);
        }

        // The typed path: a spell or skill whose damage type is authored.
        //
        // `affinity` comes from the target's own definition and is passed in
        // rather than looked up -- v1 reached into a live
        // CombatantState->EnemyDefinition dictionary here, which is exactly the
        // kind of view-owned lookup that kept this logic out of Domain.
        // Anything with no definition behind it is ElementalAffinity.Neutral,
        // silently, which is the sensible reading for an enemy attacking a
        // player or a direct test entry -- and is default(T), so the
        // "no answer" case cannot be forgotten at a call site the way a
        // nullable pair could be half-supplied.
        //
        // `attacker` is optional — only Sharp Horns' penetration needs it,
        // and the one caller with no attacker in hand (ResolveDamageInstances
        // now always has one, but the parameter defaults to null for any
        // future direct caller that genuinely has none) simply forfeits that
        // one term. `ignoresDefense` defaults to false, which is every caller
        // but an authored skill with the flag set.
        //
        // `dodgeAlreadyResolved` exists for exactly ONE caller:
        // ResolveDamageInstances, which resolves a spell with MULTIPLE
        // authored damage packets (frost_flare, lightning_bolt) through one
        // call to this method PER packet. A multi-element cast is one swing
        // the target either evades entirely or is hit by — nothing in this
        // combat model half-dodges a spell, taking its Fire packet but not
        // its Ice one — so ResolveDamageInstances rolls RollDodge itself
        // exactly ONCE for the whole cast and passes true here for every
        // packet in that same cast, rather than letting each packet roll
        // independently (which would make a two-packet spell effectively
        // harder to dodge than a one-packet spell for no designed reason).
        // Every OTHER caller leaves this false and gets the roll for free.
        public static Outcome AfterDefences(
            int raw,
            DamageType type,
            CombatantState target,
            ElementalAffinity affinity,
            float varianceRange,
            SeededRandom rng,
            Func<CombatantState, int, int> resolveWard,
            CombatantState attacker = null,
            bool ignoresDefense = false,
            bool dodgeAlreadyResolved = false)
        {
            if (!dodgeAlreadyResolved && RollDodge(target, attacker, rng))
            {
                return new Outcome(0, 1f, 0, isMiss: true);
            }

            float effectiveness = CombatMath.EffectivenessMultiplier(type, affinity);

            int result = CombatMath.AfterResistance(
                CombatMath.ApplyStatusEffects(CombatMath.ApplyEffectiveness(raw, effectiveness), target),
                CombatMath.TotalDefense(target, type, attacker, ignoresDefense));

            // Combo: a Nature or Poison hit landing on an already-Poisoned
            // target detonates it for bonus damage on top of this hit's own.
            // Lives HERE, in the one funnel every TYPED damage path already
            // goes through, rather than at each call site -- the untyped
            // enemy-attack overload never reaches this method at all, so a
            // monster's own claws can never accidentally detonate a status.
            int detonated = StatusCombos.DetonatePoisonIfMatched(target, type);

            // The variance roll lands here too, same funnel -- AFTER
            // effectiveness/resistance (a deterministic property of the
            // matchup) and BEFORE the ward (which should reduce whatever the
            // roll actually landed on, not the pre-roll figure).
            result = ApplyVariance(result, varianceRange, rng);

            // Magical Shield -- and the Fragile Lamb's Ward, which is the same
            // status -- spends here, in the one funnel every TYPED damage path
            // shares, same reasoning as the Protect/Vulnerable and resistance
            // handling above.
            result = resolveWard == null ? result : resolveWard(target, result);

            // Stalwart's flat physical reduction -- LAST, deliberately, and
            // PHYSICAL ONLY. See ApplyFlatPhysicalReduction's own comment for
            // why this sits after the ward rather than before it.
            result = ApplyFlatPhysicalReduction(result, target, type);

            return new Outcome(result, effectiveness, detonated);
        }

        // The untyped path: an attacker whose damage type comes from their own
        // definition, or who has none.
        //
        // A combatant with no authored attack type is untyped: nothing is
        // effective or ineffective against it, and it is stopped by physical
        // armour, which is the sensible reading of "hits things with whatever
        // it has". Still passes through Protect/Vulnerable -- this is the
        // enemy-attack path, the one place a player's own Protect status
        // actually has to matter.
        public static Outcome AfterDefences(
            int raw,
            CombatantState actor,
            CombatantState target,
            DamageType? attackType,
            ElementalAffinity affinity,
            float varianceRange,
            SeededRandom rng,
            Func<CombatantState, int, int> resolveWard,
            bool ignoresDefense = false)
        {
            // The execute bonus rides HERE, in the one overload that knows both
            // sides, rather than at the call sites that would otherwise each
            // have to remember it. Applied to the RAW figure before armour and
            // effectiveness for the same reason weapon scaling is: a bonus
            // applied to the finished number would be worth less against
            // exactly the armoured targets it is meant to finish.
            //
            // The typed overload deliberately does NOT get it. That one serves
            // a spell's authored damage instances, which deal exactly what they
            // say -- the whole premise of a fixed packet.
            raw = CombatMath.ApplyExecuteBonus(raw, actor, target);

            if (attackType.HasValue)
            {
                // The typed overload above rolls RollDodge itself — do not
                // roll here too. Two independent rolls for one swing would
                // not just waste a draw, it would silently change the odds
                // (this codebase has no notion of "roll twice, either dodges
                // it"), so this delegation is the ONLY correct way for the
                // typed branch to reach the funnel's single dodge check.
                return AfterDefences(raw, attackType.Value, target, affinity,
                                     varianceRange, rng, resolveWard,
                                     attacker: actor, ignoresDefense: ignoresDefense);
            }

            // The untyped tail never reaches the typed overload above, so it
            // is the other of the two places in this class that actually
            // owns a RollDodge call — never both for the same swing, see the
            // branch just above.
            if (RollDodge(target, actor, rng))
            {
                return new Outcome(0, 1f, 0, isMiss: true);
            }

            // Untyped: physical armour only, no effectiveness, no poison combo.
            // "Physical armour" now means the full D_broad + D_typed figure
            // read against DamageType.Physical, same as every typed path —
            // the only things genuinely missing here are effectiveness (there
            // is no type to be weak or resistant to) and the poison combo.
            int result = CombatMath.AfterResistance(
                CombatMath.ApplyStatusEffects(raw, target),
                CombatMath.TotalDefense(target, DamageType.Physical, actor, ignoresDefense));

            result = ApplyVariance(result, varianceRange, rng);
            result = resolveWard == null ? result : resolveWard(target, result);

            // Untyped damage IS physical damage (see this method's own
            // header), so Stalwart's flat reduction applies here too.
            result = ApplyFlatPhysicalReduction(result, target, DamageType.Physical);

            return new Outcome(result, 1f, 0);
        }

        // Stalwart's FlatPhysicalDamageReduction: a flat subtraction from
        // PHYSICAL damage only, applied as the LAST step of the funnel —
        // after the R/(R+100) mitigation curve, the variance roll, AND the
        // ward, not folded into any of them. Last on purpose: it is armour
        // PLATING, a fixed amount of harm that never reaches the wearer
        // regardless of how big or small the hit that produced this number
        // was, which is a different promise than "resists a percentage of
        // it" (PhysicalDefense) or "the next hit is cut by a percentage"
        // (Shielded/Ward) — both of those scale with the incoming number,
        // this deliberately does not.
        //
        // Floored at 1, the same damage floor every other step in this
        // pipeline already enforces (CombatMath.AfterResistance's own
        // `max(1, ...)`) — Stalwart softens a hit, it does not stop one.
        private static int ApplyFlatPhysicalReduction(int damage, CombatantState target, DamageType type)
        {
            if (type != DamageType.Physical || target?.ModifierEffects == null)
            {
                return damage;
            }

            int reduction = target.ModifierEffects.Best(ModifierEffectType.FlatPhysicalDamageReduction);
            if (reduction <= 0)
            {
                return damage;
            }

            int reduced = damage - reduction;
            return reduced < 1 ? 1 : reduced;
        }

        // Applied once, at the ONE funnel every damage path already shares --
        // never inside CombatMath, which has to stay pure so its own tests can
        // pin exact literals.
        //
        // Range and stream are parameters rather than v1's mutable statics: a
        // static that tests reach in and change is a static two tests can
        // disagree about.
        public static int ApplyVariance(int damage, float varianceRange, SeededRandom rng)
        {
            if (varianceRange <= 0f || damage <= 0 || rng == null)
            {
                return damage;
            }

            float roll = 1f + rng.NextFloat(-varianceRange, varianceRange);
            int rolled = Rounding.AwayFromZero(damage * roll);
            return rolled < 1 ? 1 : rolled;
        }
    }
}
