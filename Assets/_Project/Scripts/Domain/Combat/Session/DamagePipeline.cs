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

            public Outcome(int damage, float effectiveness, int poisonDetonation)
            {
                Damage = damage;
                Effectiveness = effectiveness;
                PoisonDetonation = poisonDetonation;
            }
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
        public static Outcome AfterDefences(
            int raw,
            DamageType type,
            CombatantState target,
            ElementalAffinity affinity,
            float varianceRange,
            SeededRandom rng,
            Func<CombatantState, int, int> resolveWard)
        {
            float effectiveness = CombatMath.EffectivenessMultiplier(type, affinity);

            int result = CombatMath.AfterResistance(
                CombatMath.ApplyStatusEffects(CombatMath.ApplyEffectiveness(raw, effectiveness), target),
                CombatMath.ResistanceAgainst(target, type));

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
            Func<CombatantState, int, int> resolveWard)
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
                return AfterDefences(raw, attackType.Value, target, affinity,
                                     varianceRange, rng, resolveWard);
            }

            // Untyped: physical armour only, no effectiveness, no poison combo.
            int result = CombatMath.AfterResistance(
                CombatMath.ApplyStatusEffects(raw, target),
                CombatMath.ResistanceAgainst(target, DamageType.Physical));

            result = ApplyVariance(result, varianceRange, rng);
            result = resolveWard == null ? result : resolveWard(target, result);

            return new Outcome(result, 1f, 0);
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
