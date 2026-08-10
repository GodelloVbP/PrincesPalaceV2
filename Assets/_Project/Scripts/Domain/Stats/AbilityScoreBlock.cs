using System;

namespace PrincesPalace.Domain.Stats
{
    // A combatant's classic D&D-style ability scores. Deliberately a plain
    // serializable struct with no UnityEngine dependency, mirroring
    // StatBlock, so the domain layer stays engine-free and unit-testable
    // while Unity can still show it in the Inspector on a ScriptableObject.
    //
    // Values combine additively: a character's effective ability scores are
    // their definition's base block plus the block from every unlocked
    // talent, same as StatBlock.
    [Serializable]
    public struct AbilityScoreBlock : IEquatable<AbilityScoreBlock>
    {
        public int strength;
        public int dexterity;
        public int constitution;
        public int wisdom;
        public int intelligence;
        public int charisma;

        public AbilityScoreBlock(int strength, int dexterity, int constitution, int wisdom, int intelligence, int charisma)
        {
            this.strength = strength;
            this.dexterity = dexterity;
            this.constitution = constitution;
            this.wisdom = wisdom;
            this.intelligence = intelligence;
            this.charisma = charisma;
        }

        public static AbilityScoreBlock Zero => new AbilityScoreBlock(0, 0, 0, 0, 0, 0);

        public int this[AbilityScore score]
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
                        throw new ArgumentOutOfRangeException(nameof(score), score, "AbilityScoreBlock has no field wired up for this AbilityScore.");
                }
            }
        }

        // Same shape as ScalingProfile.With — a copy with exactly one score
        // replaced, so a line parser can build a block one authored line at
        // a time without a six-way switch of its own.
        public AbilityScoreBlock With(AbilityScore score, int value)
        {
            var copy = this;
            switch (score)
            {
                case AbilityScore.Strength: copy.strength = value; break;
                case AbilityScore.Dexterity: copy.dexterity = value; break;
                case AbilityScore.Constitution: copy.constitution = value; break;
                case AbilityScore.Wisdom: copy.wisdom = value; break;
                case AbilityScore.Intelligence: copy.intelligence = value; break;
                case AbilityScore.Charisma: copy.charisma = value; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(score), score, "AbilityScoreBlock has no field wired up for this AbilityScore.");
            }

            return copy;
        }

        // True when every score here is at least as high as `requirement`'s
        // corresponding score — the same per-score comparison
        // RequirementResolver and GetSpellTierForLevel's character-aware
        // overload both make, exposed here so a THIRD "does this meet that"
        // check (a skill's own cast-time requirement) does not have to
        // re-loop AbilityScores.All itself.
        public bool Meets(AbilityScoreBlock requirement)
        {
            foreach (AbilityScore score in AbilityScores.All)
            {
                if (this[score] < requirement[score])
                {
                    return false;
                }
            }

            return true;
        }

        public static AbilityScoreBlock operator +(AbilityScoreBlock a, AbilityScoreBlock b)
        {
            return new AbilityScoreBlock(
                a.strength + b.strength,
                a.dexterity + b.dexterity,
                a.constitution + b.constitution,
                a.wisdom + b.wisdom,
                a.intelligence + b.intelligence,
                a.charisma + b.charisma);
        }

        // The comparison a hover panel diffs two loadouts with — negative
        // where `b` grants more than `a` does, same sign convention `+`
        // already establishes.
        public static AbilityScoreBlock operator -(AbilityScoreBlock a, AbilityScoreBlock b)
        {
            return new AbilityScoreBlock(
                a.strength - b.strength,
                a.dexterity - b.dexterity,
                a.constitution - b.constitution,
                a.wisdom - b.wisdom,
                a.intelligence - b.intelligence,
                a.charisma - b.charisma);
        }

        // Clamps every score to at least `floor`. Useful once talents or
        // effects can subtract, so a stack of penalties can't drive an
        // ability score negative.
        public AbilityScoreBlock ClampedAtLeast(int floor)
        {
            return new AbilityScoreBlock(
                Math.Max(floor, strength),
                Math.Max(floor, dexterity),
                Math.Max(floor, constitution),
                Math.Max(floor, wisdom),
                Math.Max(floor, intelligence),
                Math.Max(floor, charisma));
        }

        public bool Equals(AbilityScoreBlock other)
        {
            return strength == other.strength
                && dexterity == other.dexterity
                && constitution == other.constitution
                && wisdom == other.wisdom
                && intelligence == other.intelligence
                && charisma == other.charisma;
        }

        public override bool Equals(object obj)
        {
            return obj is AbilityScoreBlock other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = strength;
                hash = (hash * 397) ^ dexterity;
                hash = (hash * 397) ^ constitution;
                hash = (hash * 397) ^ wisdom;
                hash = (hash * 397) ^ intelligence;
                hash = (hash * 397) ^ charisma;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"STR {strength}, DEX {dexterity}, CON {constitution}, WIS {wisdom}, INT {intelligence}, CHA {charisma}";
        }
    }
}
