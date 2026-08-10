using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Stats
{
    // What a weapon or a spell rides: one grade per ability score, and the
    // damage multiplier that falls out of the wielder's actual scores.
    //
    // TEN IS NEUTRAL, the same anchor AbilityDerivation uses, and every
    // derivation here is a function of (score - 10) that returns exactly 1.0x
    // at 10 across the board. That is what makes this shippable without
    // rebalancing a thing: a combatant nobody has authored scores or grades
    // for multiplies by one, and every existing enemy, weapon and spell fights
    // exactly as it did.
    //
    // Below neutral it goes DOWN, and that is the point rather than a side
    // effect. A greatsword that scales S on Strength in the hands of someone
    // who dumped Strength should be a bad idea, not merely a missed bonus —
    // otherwise "pick the weapon that fits your build" has no wrong answer and
    // the decision stops being one. AbilityDerivation makes the same argument
    // about penalties mirroring bonuses.
    //
    // A plain serializable struct with public fields rather than a readonly
    // one, because Unity serialises fields and this rides on ItemDefinition —
    // same shape and same reason as AbilityScoreBlock and StatBlock.
    [Serializable]
    public struct ScalingProfile : IEquatable<ScalingProfile>
    {
        public ScalingGrade strength;
        public ScalingGrade dexterity;
        public ScalingGrade constitution;
        public ScalingGrade wisdom;
        public ScalingGrade intelligence;
        public ScalingGrade charisma;

        public ScalingProfile(ScalingGrade strength, ScalingGrade dexterity, ScalingGrade constitution,
            ScalingGrade wisdom, ScalingGrade intelligence, ScalingGrade charisma)
        {
            this.strength = strength;
            this.dexterity = dexterity;
            this.constitution = constitution;
            this.wisdom = wisdom;
            this.intelligence = intelligence;
            this.charisma = charisma;
        }

        // Scales on nothing. The default, and what every enemy and every
        // unauthored item carries.
        public static ScalingProfile None => default;

        // The scores at which this whole system is inert. Handed to a
        // combatant that has no real ability scores of its own, so a stray
        // grade cannot read a block of zeroes as "ten points below neutral"
        // and quarter the damage.
        public static AbilityScoreBlock NeutralScores => new AbilityScoreBlock(
            AbilityDerivation.NeutralScore, AbilityDerivation.NeutralScore, AbilityDerivation.NeutralScore,
            AbilityDerivation.NeutralScore, AbilityDerivation.NeutralScore, AbilityDerivation.NeutralScore);

        // However badly matched the wielder, an attack still lands for
        // something. Without a floor, S-on-Strength at Strength 0 multiplies
        // by exactly zero and a weapon becomes literally unusable rather than
        // merely wrong — and "unusable" is a state the damage pipeline has
        // spent a lot of care never producing (see every max(1, ...) in
        // CombatMath).
        public const float MinimumMultiplier = 0.25f;

        public ScalingGrade this[AbilityScore score]
        {
            get
            {
                switch (score)
                {
                    case AbilityScore.Strength: return strength;
                    case AbilityScore.Dexterity: return dexterity;
                    case AbilityScore.Constitution: return constitution;
                    case AbilityScore.Wisdom: return wisdom;
                    case AbilityScore.Intelligence: return intelligence;
                    case AbilityScore.Charisma: return charisma;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(score), score, "ScalingProfile has no field wired up for this AbilityScore.");
                }
            }
        }

        public ScalingProfile With(AbilityScore score, ScalingGrade grade)
        {
            var copy = this;
            switch (score)
            {
                case AbilityScore.Strength: copy.strength = grade; break;
                case AbilityScore.Dexterity: copy.dexterity = grade; break;
                case AbilityScore.Constitution: copy.constitution = grade; break;
                case AbilityScore.Wisdom: copy.wisdom = grade; break;
                case AbilityScore.Intelligence: copy.intelligence = grade; break;
                case AbilityScore.Charisma: copy.charisma = grade; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(score), score, "ScalingProfile has no field wired up for this AbilityScore.");
            }

            return copy;
        }

        // True when this scales on nothing at all, which is the overwhelming
        // majority of items and every enemy.
        public bool IsNeutral
        {
            get
            {
                foreach (AbilityScore score in AbilityScores.All)
                {
                    if (this[score] != ScalingGrade.None)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        // One score's own share of the bonus this profile is currently
        // granting — the exact number a UI line like "STR B: +45" attributes
        // to that one stat. Zero for a score this profile does not ride.
        public float BonusFor(AbilityScoreBlock scores, AbilityScore one)
        {
            var grade = this[one];
            return grade == ScalingGrade.None ? 0f : ScalingGrades.PerPoint(grade) * (scores[one] - AbilityDerivation.NeutralScore);
        }

        // The sum of every score's own share — MultiplierFor minus the 1.0
        // baseline. Split out so two profiles can combine as `1 + BonusA +
        // BonusB` without double-counting that baseline, which
        // `MultA * MultB` would.
        public float BonusFor(AbilityScoreBlock scores)
        {
            float total = 0f;
            foreach (AbilityScore score in AbilityScores.All)
            {
                total += BonusFor(scores, score);
            }

            return total;
        }

        // THE function. One multiplier on damage, summed across every score
        // this profile rides.
        //
        // Additive across stats rather than multiplicative: two B grades
        // should be worth about one A, not compound into something no one
        // predicted. A player can add the contributions up in their head,
        // which is the difference between a system they can plan around and
        // one they have to test.
        public float MultiplierFor(AbilityScoreBlock scores)
        {
            if (IsNeutral)
            {
                return 1f;
            }

            float total = 1f + BonusFor(scores);
            return total < MinimumMultiplier ? MinimumMultiplier : total;
        }

        // "STR A · DEX C", strongest grade first, for a reward card that has
        // one line to explain why this sword is different from that one.
        // Empty for a profile that scales on nothing, so the caller can drop
        // the line entirely rather than print a label with nothing after it.
        public string Describe()
        {
            var parts = new List<(AbilityScore Score, ScalingGrade Grade)>();
            foreach (AbilityScore score in AbilityScores.All)
            {
                if (this[score] != ScalingGrade.None)
                {
                    parts.Add((score, this[score]));
                }
            }

            if (parts.Count == 0)
            {
                return "";
            }

            parts.Sort((a, b) =>
            {
                int byGrade = ((int)b.Grade).CompareTo((int)a.Grade);
                return byGrade != 0 ? byGrade : ((int)a.Score).CompareTo((int)b.Score);
            });

            var text = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                {
                    text.Append("  ");
                }

                text.Append(AbilityScores.ShortName(parts[i].Score)).Append(' ').Append(ScalingGrades.Letter(parts[i].Grade));
            }

            return text.ToString();
        }

        public bool Equals(ScalingProfile other)
        {
            return strength == other.strength
                && dexterity == other.dexterity
                && constitution == other.constitution
                && wisdom == other.wisdom
                && intelligence == other.intelligence
                && charisma == other.charisma;
        }

        public override bool Equals(object obj) => obj is ScalingProfile other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)strength;
                hash = (hash * 397) ^ (int)dexterity;
                hash = (hash * 397) ^ (int)constitution;
                hash = (hash * 397) ^ (int)wisdom;
                hash = (hash * 397) ^ (int)intelligence;
                hash = (hash * 397) ^ (int)charisma;
                return hash;
            }
        }

        public override string ToString() => IsNeutral ? "no scaling" : Describe();
    }
}
