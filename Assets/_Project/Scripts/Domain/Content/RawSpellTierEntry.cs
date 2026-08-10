using System;

namespace PrincesPalace.Domain.Content
{
    // One row of the Skill power/cost curve, exactly as typed into
    // spells.json. Unlike RawEnemyEntry, nothing here is optional — a
    // progression table is exactly the kind of content where every step
    // deserves a deliberate, hand-picked number rather than a formula
    // guessing on the author's behalf.
    [Serializable]
    public class RawSpellTierEntry
    {
        // The character level this tier applies from. FightController uses
        // whichever tier has the highest level not exceeding the caster's
        // actual level, so tiers don't need to be a gapless 1..9 run to
        // work — but that's the intended shape.
        public int level;

        public string displayName;
        public int manaCost;
        public float powerMultiplier;

        // Which ability scores this tier's spell rides, as "<score> <grade>"
        // lines — see ScalingLineParser. A caster's Skill scales on its own
        // stats rather than on the sword in their hand, which is the point of
        // it being a separate axis: an Intelligence build casts harder while
        // a Strength build swings harder, and the same character can be good
        // at exactly one of them.
        //
        // Empty means no scaling, which is what every tier did before this
        // existed and is still a valid authoring choice.
        public string[] scalesWith = Array.Empty<string>();

        // What a character needs before THIS tier is the one their Skill
        // actually uses -- "<ability score> <amount>" lines, see
        // AbilityScoreLineParser. Unmet falls back to the highest tier whose
        // requirements ARE met (see ContentDatabase.GetSpellTierForLevel),
        // never to no spell at all. The level-1 tier must always have none —
        // content validation enforces it, since Skill has to be castable
        // from the very first fight.
        public string[] requires = Array.Empty<string>();
    }

    // JsonUtility can't deserialize a bare top-level JSON array, so
    // spells.json is one object with a "tiers" array inside it.
    [Serializable]
    public class RawSpellTierFile
    {
        public RawSpellTierEntry[] tiers = Array.Empty<RawSpellTierEntry>();
    }
}
