using System;

namespace PrincesPalace.Domain.Stats
{
    // Every axis a combatant's damage can ride, combined. Three FIXED slots
    // rather than a list — no allocation, and the shape is exactly what a
    // combatant ever has: the spell tier's own scaling, the main hand, the
    // off hand. A basic Attack rides MainHand alone; a cast rides all three
    // (see the off-hand rule on ScalingSet's own callers — the off hand
    // contributes to a cast but never to a swing, which is why this is not
    // simply "sum whatever is equipped").
    //
    // Additive across slots for the same reason ScalingProfile is additive
    // across scores: `1 + BonusA + BonusB + BonusC` is a number a player can
    // add up in their head, and it is what keeps two profiles that both
    // happen to be neutral multiplying out to exactly 1x rather than
    // 1x * 1x * 1x rounding error.
    [Serializable]
    public readonly struct ScalingSet : IEquatable<ScalingSet>
    {
        public readonly ScalingProfile SpellTier;
        public readonly ScalingProfile MainHand;
        public readonly ScalingProfile OffHand;

        public ScalingSet(ScalingProfile spellTier, ScalingProfile mainHand, ScalingProfile offHand)
        {
            SpellTier = spellTier;
            MainHand = mainHand;
            OffHand = offHand;
        }

        // Rides nothing at all. The default, same meaning as
        // ScalingProfile.None.
        public static readonly ScalingSet None = default;

        // A lone profile poured into the MainHand slot, the other two left
        // neutral. This is what keeps every existing single-profile
        // assignment site compiling untouched — FightController.Encounter's
        // WeaponScaling/SkillScaling assignments and the seven ScalingProfile
        // assignments in CombatMathTests — since a single populated slot
        // combines to the exact same multiplier a bare ScalingProfile would
        // have produced on its own, regardless of which of the three slots
        // it happens to sit in.
        public static implicit operator ScalingSet(ScalingProfile profile) =>
            new ScalingSet(ScalingProfile.None, profile, ScalingProfile.None);

        // True only when every slot rides nothing — the overwhelming
        // majority of combatants.
        public bool IsNeutral => SpellTier.IsNeutral && MainHand.IsNeutral && OffHand.IsNeutral;

        // One score's own share of the bonus this WHOLE set is currently
        // granting, summed across every slot that rides it.
        public float BonusFor(AbilityScoreBlock scores, AbilityScore one) =>
            SpellTier.BonusFor(scores, one) + MainHand.BonusFor(scores, one) + OffHand.BonusFor(scores, one);

        // The sum of every slot's own BonusFor — MultiplierFor minus the 1.0
        // baseline, same relationship ScalingProfile.BonusFor has to its own
        // MultiplierFor.
        public float BonusFor(AbilityScoreBlock scores) =>
            SpellTier.BonusFor(scores) + MainHand.BonusFor(scores) + OffHand.BonusFor(scores);

        // THE function, same contract as ScalingProfile.MultiplierFor:
        // short-circuits to exactly 1f when every slot is neutral (so two
        // neutral sets multiply out to exactly 1f, never 0.999998f), and
        // floors everywhere else at ScalingProfile.MinimumMultiplier so no
        // combination of bad-fit slots can zero out a hit entirely.
        public float MultiplierFor(AbilityScoreBlock scores)
        {
            if (IsNeutral)
            {
                return 1f;
            }

            float total = 1f + BonusFor(scores);
            return total < ScalingProfile.MinimumMultiplier ? ScalingProfile.MinimumMultiplier : total;
        }

        public bool Equals(ScalingSet other) =>
            SpellTier.Equals(other.SpellTier) && MainHand.Equals(other.MainHand) && OffHand.Equals(other.OffHand);

        public override bool Equals(object obj) => obj is ScalingSet other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SpellTier.GetHashCode();
                hash = (hash * 397) ^ MainHand.GetHashCode();
                hash = (hash * 397) ^ OffHand.GetHashCode();
                return hash;
            }
        }
    }
}
