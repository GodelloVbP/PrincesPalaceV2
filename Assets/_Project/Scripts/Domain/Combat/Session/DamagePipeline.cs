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
    // THAT hit). See ModifierEffectType.DodgeRating's own comment
    // for the fuller argument for why this split is correct rather than
    // arbitrary.
    //
    // CRITICAL HITS (CritRules) are decided HERE too, for the dodge's reason:
    // one funnel means no damage path can forget them. Right after the dodge
    // (a miss spends no crit draw and cannot crit) the crit is decided -- the
    // caller's `crit` when it hands one in (an authored enemy crit, or a
    // multi-packet cast's single decision), otherwise one RollCrit on the same
    // `rng` -- and the multiplier rides the OUTGOING amount, before
    // effectiveness and defense. `rng == null` (every preview) never rolls one;
    // an authored `crit: true` still applies there, because it is certain.
    // Everything listed above as not routing through here (splash, Shatter,
    // DoT ticks, relic packets with no rng) cannot crit either.
    public static class DamagePipeline
    {
        // THE WARD HOOK'S SHAPE. It hears WHO struck and WITH WHAT as well as
        // whom and how hard: the Sentinel's planted shield (plan 4a,
        // FightSession.PlantedShield) answers the blow that broke it or
        // struck it -- break shards at the attacker, Thornwall's return on a
        // physical hit, Spellbreaker's reflect on a magic one -- and the ward
        // pool is the one step of this funnel that knows how much of the hit
        // the shield ate. Every other ward ignores the last two arguments.
        // `attacker` is null exactly when AfterDefences was given none; the
        // untyped overload passes DamageType.Physical, the type its own
        // header says untyped damage is.
        //
        // `incoming` is the blow as it arrived, BEFORE the target's defences:
        // the outgoing figure after the attacker's own crit (and, on the
        // untyped path, the execute bonus), ahead of effectiveness, Protect/
        // Vulnerable, resistance, variance and plating. The Sentinel's Fury
        // engine is measured on it ("raw incoming", plan section 2), so his
        // defence investment never cuts his income; the ward step is where
        // it is handed over because it is the one step that already hears
        // who struck, and the one every real hit reaches.
        public delegate int WardResolver(CombatantState target, int damage, CombatantState attacker, DamageType type,
            int incoming);

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
            // DodgeRating's comment, for why those two have to read
            // as different information to the player.
            public readonly bool IsMiss;

            // TRUE when this hit was a CRITICAL HIT -- rolled here for a party
            // member, or authored on an enemy ability and handed in. Damage
            // already includes the multiplier; this is reported so the view
            // can present it (CombatBeat.Crit / BeatTargetResult.Crit). Never
            // true on a miss.
            public readonly bool IsCrit;

            public Outcome(int damage, float effectiveness, int poisonDetonation, bool isMiss = false,
                           bool isCrit = false)
            {
                Damage = damage;
                Effectiveness = effectiveness;
                PoisonDetonation = poisonDetonation;
                IsMiss = isMiss;
                IsCrit = isCrit && !isMiss;
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

            // DodgeRating is a RATING through CombatMath's dodge curve, NOT
            // a direct percent -- see ModifierEffectType.DodgeRating's own
            // header for the full "why" and CombatMath.DodgePercentFrom's
            // own header for the formula (100 * R / (R + 100), the
            // designer's spec: "100 dodge = 50% chance to dodge"). This
            // used to be a straight percent read off the modifier, which
            // let a maxed Swift item (base 12 x up to ~18.6x combined
            // tier/rift scaling) exceed 100 outright -- a guaranteed,
            // unavoidable dodge, flagged as a real balance problem and
            // fixed by inserting this curve conversion right here, between
            // reading the raw rating and rolling against it.
            int dodgeRating = target.ModifierEffects.Best(ModifierEffectType.DodgeRating);
            int dodgeChance = CombatMath.DodgePercentFrom(dodgeRating);

            // Delegates to the same RandomOps.RollPercent every other chance
            // effect in this vocabulary rolls through -- see its own header
            // for the <=0/>=100 short-circuits. The >=100 short-circuit is
            // dead code for THIS caller specifically now (DodgePercentFrom
            // can never return 100 for a finite rating -- see its own
            // header), kept anyway so RollDodge stays exactly as defensive
            // as every other RollPercent caller rather than assuming its
            // one upstream conversion can never change.
            return RandomOps.RollPercent(rng, dodgeChance);
        }

        // THE CRIT ROLL -- see CritRules for the whole rule. Same seam and same
        // conventions as RollDodge above: the session's own `rng` stream,
        // through RandomOps.RollPercent, and `rng == null` (every read-only
        // preview) never crits, so a telegraph cannot spend a draw. Enemies
        // answer 0 from CritRules.ChanceFor and therefore never consume one
        // either; an enemy crit is AUTHORED and handed in, never rolled.
        //
        // `target` lets the chance read who is being hit (a wounded target);
        // the result is also left on the attacker (LastHitWasCrit) so the Fury
        // engine can pay a crit's bonus when the pools hear this same blow.
        public static bool RollCrit(CombatantState attacker, SeededRandom rng, CombatantState target = null)
        {
            if (rng == null) return false;

            bool crit = RandomOps.RollPercent(rng, CritRules.ChanceFor(attacker, target));
            if (attacker != null) attacker.LastHitWasCrit = crit;
            return crit;
        }

        // WHETHER THIS HIT CRITS, as the funnel decides it.
        //
        // `crit` is the caller's call when it has one: true for an AUTHORED
        // crit (guaranteed, no draw, applies in a preview too), false for a
        // hit that must not crit. null means "decide here": roll it -- UNLESS
        // the caller already resolved the swing (`dodgeAlreadyResolved`),
        // because then it owns the crit call too, exactly as it owns the dodge
        // (a multi-packet spell rolls once for the whole cast and hands the
        // result to every packet; a rider on a landed blow does not crit).
        private static bool DecideCrit(bool? crit, bool dodgeAlreadyResolved, CombatantState attacker,
            SeededRandom rng, CombatantState target)
        {
            if (crit.HasValue)
            {
                // A decided call (authored, or a rider that must not crit) is
                // still a fact about this blow: leaving the last roll's answer
                // behind would let a splash inherit it.
                if (rng != null && attacker != null) attacker.LastHitWasCrit = crit.Value;
                return crit.Value;
            }

            return !dodgeAlreadyResolved && RollCrit(attacker, rng, target);
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
            WardResolver resolveWard,
            CombatantState attacker = null,
            bool ignoresDefense = false,
            bool dodgeAlreadyResolved = false,
            Func<CombatantState, CombatantState, DamageType, int> resolveDetonation = null,
            bool? crit = null)
        {
            if (!dodgeAlreadyResolved && RollDodge(target, attacker, rng))
            {
                return new Outcome(0, 1f, 0, isMiss: true);
            }

            // THE CRIT, right after the dodge and before everything else: a
            // miss cannot crit (and spends no crit draw), and the multiplier
            // rides the OUTGOING amount, before effectiveness and defense --
            // see CritRules' header for why that side of the armour.
            bool isCrit = DecideCrit(crit, dodgeAlreadyResolved, attacker, rng, target);
            if (isCrit)
            {
                raw = CritRules.Apply(raw, attacker);
            }

            // Jo-Sun's Book of Anatomy: the attacker's own bonus against a
            // matched weakness, if any -- see CombatMath.
            // EffectivenessMultiplier's own header.
            float effectiveness = CombatMath.EffectivenessMultiplier(
                type, affinity, attacker?.WeaknessMultiplierBonusPercent ?? 0);

            int result = CombatMath.AfterResistance(
                CombatMath.ApplyStatusEffects(CombatMath.ApplyEffectiveness(raw, effectiveness), target),
                CombatMath.TotalDefense(target, type, attacker, ignoresDefense));

            // Combo: a Nature or Poison hit landing on an already-Poisoned
            // target detonates it for bonus damage on top of this hit's own.
            // Lives HERE, in the one funnel every TYPED damage path already
            // goes through, rather than at each call site -- the untyped
            // enemy-attack overload never reaches this method at all, so a
            // monster's own claws can never accidentally detonate a status.
            //
            // HANDED IN, like resolveWard beside it, and for the identical
            // reason: it MUTATES. It spends a status and takes health, and
            // this method is what every telegraph preview also runs through.
            // A preview leaves rng, resolveWard and now this null, so it
            // cannot spend anything -- which is the contract PreviewDamage
            // and PreviewSkill have always claimed and, until this parameter
            // existed, could not keep. Null here means "no combo", never
            // "combo with nothing to report".
            int detonated = resolveDetonation == null ? 0 : resolveDetonation(attacker, target, type);

            // The variance roll lands here too, same funnel -- AFTER
            // effectiveness/resistance (a deterministic property of the
            // matchup) and BEFORE the ward (which should reduce whatever the
            // roll actually landed on, not the pre-roll figure).
            result = ApplyVariance(result, varianceRange, rng);

            // Stalwart's flat physical reduction -- PHYSICAL ONLY, and just
            // BEFORE the ward. See ApplyFlatPhysicalReduction's own comment
            // for why it moved from after it.
            result = ApplyFlatPhysicalReduction(result, target, type);

            // THE WARD POOL IS THE LAST THING BETWEEN THIS NUMBER AND THE
            // WEARER -- Magical Shield, the Fragile Lamb's Ward and every
            // other shield are one status and one pool (StatusEffects' own
            // WARDS header owns the full order). It spends here, in the one
            // funnel every TYPED damage path shares, same reasoning as the
            // Protect/Vulnerable and resistance handling above, and what it
            // returns can legitimately be ZERO: a pool that ate the whole hit
            // is not a hit that was merely softened.
            result = resolveWard == null ? result : resolveWard(target, result, attacker, type, raw);

            return new Outcome(result, effectiveness, detonated, isCrit: isCrit);
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
            WardResolver resolveWard,
            bool ignoresDefense = false,
            Func<CombatantState, CombatantState, DamageType, int> resolveDetonation = null,
            bool? crit = null)
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
                                     attacker: actor, ignoresDefense: ignoresDefense,
                                     resolveDetonation: resolveDetonation, crit: crit);
            }

            // The untyped tail never reaches the typed overload above, so it
            // is the other of the two places in this class that actually
            // owns a RollDodge call — never both for the same swing, see the
            // branch just above.
            if (RollDodge(target, actor, rng))
            {
                return new Outcome(0, 1f, 0, isMiss: true);
            }

            // The crit, in the same position the typed overload puts it.
            bool isCrit = DecideCrit(crit, dodgeAlreadyResolved: false, actor, rng, target);
            if (isCrit)
            {
                raw = CritRules.Apply(raw, actor);
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

            // Untyped damage IS physical damage (see this method's own
            // header), so Stalwart's flat reduction applies here too -- and
            // in the same place the typed overload puts it, just ahead of the
            // ward.
            result = ApplyFlatPhysicalReduction(result, target, DamageType.Physical);
            result = resolveWard == null ? result : resolveWard(target, result, actor, DamageType.Physical, raw);

            return new Outcome(result, 1f, 0, isCrit: isCrit);
        }

        // Stalwart's FlatPhysicalDamageReduction: a flat subtraction from
        // PHYSICAL damage only, applied after the R/(R+100) mitigation curve
        // and the variance roll and JUST BEFORE the ward, not folded into any
        // of them. It is armour PLATING, a fixed amount of harm that never
        // reaches the wearer regardless of how big or small the hit that
        // produced this number was, which is a different promise than
        // "resists a percentage of it" (PhysicalDefense).
        //
        // IT USED TO RUN AFTER THE WARD, and had to move when the ward became
        // a shield POOL. Two things broke at once in the old order. The floor
        // of 1 below fires on any number at or under the reduction, so a hit
        // the pool swallowed whole -- a 0 -- came back out of this method as
        // a 1, which is a fully-absorbed blow that still hurt. And a pool
        // paying for damage that plating was about to erase anyway is the
        // wearer's shield being spent on nothing: plating is a property of
        // the armour and the ward is the last thing between the finished
        // number and the wearer (StatusEffects' own WARDS header states the
        // whole order). The old comment's argument for going last does not
        // survive the model change either -- it rested on the ward being "a
        // percentage of the incoming number", and a pool is not.
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
