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
        [ContentDoc("The character level this tier applies from; the highest tier not exceeding the caster's level is used.")]
        public int level;

        [ContentDoc("The name shown for the Skill action at this tier.")]
        public string displayName;
        [ContentDoc("Mana cost of the Skill action at this tier.")]
        public int manaCost;
        [ContentDoc("The multiplier applied to the Skill action's power at this tier.")]
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
        [ContentDoc("'<ability score> <grade>' lines this tier's Skill scales on, alongside powerMultiplier.")]
        public string[] scalesWith = Array.Empty<string>();

        // What a character needs before THIS tier is the one their Skill
        // actually uses -- "<ability score> <amount>" lines, see
        // AbilityScoreLineParser. Unmet falls back to the highest tier whose
        // requirements ARE met (see ContentDatabase.GetSpellTierForLevel),
        // never to no spell at all. The level-1 tier must always have none —
        // content validation enforces it, since Skill has to be castable
        // from the very first fight.
        [ContentDoc("'<ability score> <amount>' lines gating whether this tier is the one used; unmet falls back to the highest tier that is met.")]
        public string[] requires = Array.Empty<string>();
    }

    // JsonUtility can't deserialize a bare top-level JSON array, so
    // spells.json is one object with a "tiers" array inside it.
    [Serializable]
    public class RawSpellTierFile
    {
        [ContentDoc("This file's tiers; see RawSpellTierEntry.")]
        public RawSpellTierEntry[] tiers = Array.Empty<RawSpellTierEntry>();
    }
}
