using System;

namespace PrincesPalace.Domain.Stats
{
    // Turns the six ability scores into things combat actually reads.
    //
    // Until now STR/DEX/CON/WIS/INT/CHA were text on the Character Sheet and
    // nothing else: every character carried an identical 10/10/10/10/10/10,
    // no character definition ever authored them, and no field of them was
    // referenced anywhere under Domain/Combat. A talent or a pair of gloves
    // could move a number on a screen and change nothing about the game.
    //
    // THE DESIGN RULE, and the reason this can ship without rebalancing
    // anything: every derivation is a function of (score - 10), and returns
    // an exact zero at 10. A character nobody has authored scores for
    // therefore derives nothing at all, and plays exactly as it did before.
    // Differentiation is opt-in, per character, one at a time.
    //
    // Engine-free and pure, so the whole layer is unit-testable in EditMode
    // with no scene and no ScriptableObject — which is the point of Domain.
    //
    // Five derivations, not six. INTELLIGENCE DOES NOT LIVE HERE, and that is
    // a deliberate removal rather than an oversight — it used to be a sixth,
    // SkillPowerBonus, added flat onto whatever the caster's spell tier
    // supplied. That was a second, uncoordinated answer to the same question
    // ScalingProfile.SkillScaling already answers (see CombatMath.
    // ComputeSkillDamage): both moved Skill damage in response to
    // Intelligence, at the same time, with nobody deciding how they should
    // compose. WeaponScaling never had this problem — AttackBonus below sets
    // the character's baseline Attack, and a weapon's own grades multiply on
    // top of THAT, which is one clean layering. Skill had two competing
    // layers doing the same job. This file keeps the "ability scores set a
    // baseline" half; ScalingProfile keeps the "gear/spell rides a score"
    // half — the same split every other derivation here already respects.
    public static class AbilityDerivation
    {
        // The score at which every derivation is neutral.
        public const int NeutralScore = 10;

        // Granularities are deliberately not uniform, because the stats they
        // feed are not on comparable scales.
        //
        // Attack and Speed move by 1 per TWO points, so a single +1 STR item
        // (gloves_of_strength) grants no free stat on its own — it takes two
        // sources to move Attack, which is what stops ability scores from
        // quietly becoming a second, better stat block. Damage is
        // max(1, attack - defense) with no variance, so +1 Attack is a large
        // change; it should cost something.
        //
        // Health and Mana move by 2 per point, because they are
        // large-denomination pools where a single point should be felt.
        // HP is on a x10 scale now, so a point of Constitution has to move
        // with it or the six ability scores stop mattering at all.
        private const int HealthPerPoint = 20;
        private const int ManaPerPoint = 2;

        // FLOOR division, not C# truncation. `(-1) / 2` is 0 in C#, which
        // would make DEX 9 and DEX 10 behave identically while DEX 8 and 9
        // differ — an asymmetry around the neutral point that reads as a bug
        // and gets "fixed" into a real one later. Penalties have to mirror
        // bonuses exactly.
        public static int FloorDiv2(int value)
        {
            return value >= 0 ? value / 2 : (value - 1) / 2;
        }

        // The classic modifier: +1 per two points above neutral, -1 per two
        // below.
        public static int Modifier(int score)
        {
            return FloorDiv2(score - NeutralScore);
        }

        public static int AttackBonus(AbilityScoreBlock scores)
        {
            return Modifier(scores.strength);
        }

        public static int SpeedBonus(AbilityScoreBlock scores)
        {
            return Modifier(scores.dexterity);
        }

        public static int MaxHealthBonus(AbilityScoreBlock scores)
        {
            return (scores.constitution - NeutralScore) * HealthPerPoint;
        }

        public static int MaxManaBonus(AbilityScoreBlock scores)
        {
            return (scores.wisdom - NeutralScore) * ManaPerPoint;
        }

        // Charisma feeds a character's signature resource — how fast their
        // own private gauge fills each turn. Four points per step, the
        // coarsest granularity of the six, because the resource is small
        // (Shawn's Wool caps at 16 and gains 2 a turn) and a +1 there is
        // proportionally enormous next to +1 Attack.
        //
        // This is the softest of the six derivations and worth being honest
        // about: it does nothing at all for a character with no signature
        // resource. The alternatives all routed through an economy AUDIT.md
        // #2 describes as broken by two orders of magnitude, so this at
        // least affects the fight in front of you.
        public static int SignatureGainBonus(AbilityScoreBlock scores)
        {
            return FloorDiv4(scores.charisma - NeutralScore);
        }

        private static int FloorDiv4(int value)
        {
            return value >= 0 ? value / 4 : (value - 3) / 4;
        }

        // The three stat-block contributions as one block, so callers add
        // once rather than remembering which three of the six land here.
        // Defense is deliberately absent: no ability score feeds it, because
        // defense is already the stat that flat-subtraction damage is most
        // sensitive to and it belongs to gear and talents.
        public static StatBlock DerivedStats(AbilityScoreBlock scores)
        {
            return new StatBlock(
                MaxHealthBonus(scores),
                SpeedBonus(scores),
                AttackBonus(scores),
                0);
        }
    }
}
