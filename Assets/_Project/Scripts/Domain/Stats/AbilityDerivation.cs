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

        // TWO FAMILIES, and this is the whole shape of the file.
        //
        // These five derivations used to be linear in (score - 10) with small
        // per-point constants, because gear granted +1 to +4 and a character's
        // own scores ran 6 to 16. GearScaling changed the input by two orders
        // of magnitude: a full tier-10 set now grants 187 points of its style's
        // stat, against the 11 the old hand-authored steel set gave. Linear
        // constants big enough to matter at 187 are catastrophic at -3, which
        // is where Shawn's Strength actually sits — his Attack would go
        // negative before he found a single item.
        //
        // So the old constants did not move at all; a second segment was added
        // beyond them.
        //
        // POOLS (health, mana, attack) are quantities that should grow with
        // gear. They keep their original per-point rate inside CharacterBand
        // and grow QUADRATICALLY outside it. Squaring only what gear
        // contributes is what lets one curve be gentle where characters are
        // authored and enormous at the top: it compounds with the tier ladder
        // in the intended direction, so gear growing 9.3x across the ladder
        // makes a pool riding it grow about 87x, which is what puts the last
        // floor in six figures.
        //
        // RATES (speed, signature gain) are how OFTEN something happens, and
        // they grow as a SQUARE ROOT. A rate that scaled like a pool would not
        // read as power, it would break the scheduler: an actor at 40,000
        // Speed does not take more turns, it takes all of them. Both of these
        // are also consumed by systems with hard ceilings — SpeedScale clamps
        // its tick rate at 2.5x, and a signature pool holds 10 — so a runaway
        // input does not even buy anything, it just saturates and goes dead.
        //
        // Both families split at the SAME seam, CharacterBand, so every score
        // any character is authored with derives exactly what it always did.
        // Nothing on the character sheet changes; only what gear stacks on top
        // of it does.
        //
        // Numerators and denominators rather than floats, because this feeds
        // integer stat blocks and CLAUDE.md gotcha 5 is about exactly the
        // rounding drift that creeps in otherwise.

        // Health. 20 a point inside the character band, unchanged from
        // before, then 1.2 x excess^2 on gear. A full tier-10 Bulwark set is
        // 187 points of Constitution, landing a character around 40,000 HP.
        private const int HealthPerPoint = 20;
        private const int HealthNumerator = 12;
        private const int HealthDenominator = 10;

        // Attack, and every other number here is anchored to this one. The
        // old 1-per-2-points holds inside the band; beyond it, 0.0875 x
        // excess^2. Damage is (attack - defense) x DamageScale(10), so a
        // tier-10 Harness set's 187 Strength is about 2,650 Attack and about
        // 26,000 damage a swing. The heaviest skill in skills.json is power 4,
        // which puts an ultimate just past 100,000.
        private const int AttackPerPoint = 1;      // applied through FloorDiv2
        private const int AttackNumerator = 7;
        private const int AttackDenominator = 80;

        // Mana. 2 a point inside the band as before, then 0.05 x excess^2 --
        // deliberately the shallowest of the three pools. A tier-10 Wool set
        // reaches about 1,650, which is real progression without pretending
        // mana is a damage stat.
        //
        // WORTH KNOWING: SkillManaCost is a flat 10 and does not move with
        // this. At 1,650 mana that is 165 casts, so mana stops being a
        // constraint long before the last floor. Scaling costs is a content
        // decision rather than an arithmetic one, so it is named here and left.
        private const int ManaPerPoint = 2;
        private const int ManaNumerator = 1;
        private const int ManaDenominator = 20;

        // FLOOR division, not C# truncation. `(-1) / 2` is 0 in C#, which
        // would make DEX 9 and DEX 10 behave identically while DEX 8 and 9
        // differ — an asymmetry around the neutral point that reads as a bug
        // and gets "fixed" into a real one later. Penalties have to mirror
        // bonuses exactly.
        public static int FloorDiv2(int value)
        {
            return value >= 0 ? value / 2 : (value - 1) / 2;
        }

        // The same rule for any divisor. FloorDiv2 stays as it is because it
        // is public and called from elsewhere; this is what the piecewise
        // curves below use so they cannot round a penalty the other way.
        private static int FloorDiv(int value, int divisor)
        {
            return value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);
        }

        // The classic modifier: +1 per two points above neutral, -1 per two
        // below.
        public static int Modifier(int score)
        {
            return FloorDiv2(score - NeutralScore);
        }

        // How far from neutral a CHARACTER can be. The six scores are authored
        // against a 60-point budget, so nobody is more than a few points off
        // 10 and 10 is a generous ceiling on that.
        //
        // This is the seam between the two things that feed a score, and it is
        // the whole reason the curve is piecewise. Inside the band, the value
        // is a character's identity and moves LINEARLY at exactly the rates it
        // always did. Outside it, the value can only have come from gear, and
        // that part is squared.
        //
        // A single quadratic across the whole range was tried first and was
        // wrong in a way worth recording: squaring is tiny near neutral, so
        // Shawn's Constitution 16 fell from +120 health to +43 and his Wisdom
        // 14 from +8 mana to nothing. It made the endgame land correctly by
        // deleting the character sheet, and a 60-point budget nobody can feel
        // is not a budget.
        private const int CharacterBand = 10;

        // A POOL: linear within a character's own range, quadratic on whatever
        // gear piles on top.
        //
        // long internally because the excess reaches ~185 and a numerator of 12
        // puts the intermediate near half a million -- fine for int today, and
        // one maxTier raise away from not being.
        //
        // Sign is applied to the SQUARE rather than carried through it, so a
        // deficit mirrors a surplus exactly and the value is still zero at
        // neutral. Floored, not truncated, for the reason FloorDiv2 states.
        private static int Pool(int difference, int perPoint, int numerator, int denominator)
        {
            int inBand = difference;
            if (inBand > CharacterBand) inBand = CharacterBand;
            if (inBand < -CharacterBand) inBand = -CharacterBand;

            long total = (long)inBand * perPoint;

            int excess = difference - inBand;
            if (excess != 0)
            {
                long squared = (long)excess * excess;
                long scaled = (excess >= 0 ? squared : -squared) * numerator;
                total += scaled >= 0
                    ? scaled / denominator
                    : (scaled - denominator + 1) / denominator;
            }

            return (int)total;
        }

        // A RATE: the character's own curve inside the band, a square root on
        // whatever gear adds beyond it. Same seam as Pool, different shape
        // outside it.
        //
        // PIECEWISE FOR THE SAME REASON, and it was not optional. A bare
        // floor(sqrt(2|d|)) was tried first on the claim that it agreed with
        // floor(|d|/2) across the authored range. It does not: it agrees at
        // 4, 6, 8 and 9 and is a full point high at 1, 2 and 3, so Dexterity
        // 12 bought two Speed where it had always bought one. Splitting at the
        // band makes the in-band half exactly the old function rather than an
        // approximation of it that happens to match at the values someone
        // checked.
        private static int Rate(int difference, int bandDivisor, int numerator, int denominator)
        {
            int band = difference;
            if (band > CharacterBand) band = CharacterBand;
            if (band < -CharacterBand) band = -CharacterBand;

            // FloorDiv, not C# division. Writing this as -((-band) / divisor)
            // truncates toward zero, which makes DEX 9 derive 0 where it has
            // always derived -1 -- the precise asymmetry the FloorDiv2 comment
            // above exists to forbid, reintroduced by hand while generalising
            // it. Caught by the two tests that pin Shawn's spread.
            int value = FloorDiv(band, bandDivisor);

            int excess = difference - band;
            if (excess != 0)
            {
                int magnitude = excess >= 0 ? excess : -excess;
                int root = (int)Math.Sqrt(magnitude) * numerator / denominator;
                value += excess >= 0 ? root : -root;
            }

            return value;
        }

        public static int AttackBonus(AbilityScoreBlock scores)
        {
            int difference = scores.strength - NeutralScore;
            int band = difference > CharacterBand ? CharacterBand
                     : difference < -CharacterBand ? -CharacterBand : difference;

            // FloorDiv2 for the in-band half, so a character's own Strength
            // reads exactly as it always did rather than as a rounding of it.
            return FloorDiv2(band * AttackPerPoint)
                 + Pool(difference, 0, AttackNumerator, AttackDenominator);
        }

        // A RATE, not a pool: Speed is how often an actor acts, and
        // SpeedScale.TickRate clamps at 2.5x anyway.
        //
        // The clamp is reached at Speed 62.5, which the old linear curve hit at
        // about 113 gear points -- roughly TIER 7. Every point past that bought
        // nothing, so Dexterity gear went dead for the last three floors of the
        // ladder while still costing a slot. The root reaches +19 at tier 10,
        // which leaves the tick rate at about 1.6 and therefore keeps Leather
        // worth wearing the whole way up.
        public static int SpeedBonus(AbilityScoreBlock scores)
        {
            return Rate(scores.dexterity - NeutralScore, 2, 1, 1);
        }

        public static int MaxHealthBonus(AbilityScoreBlock scores)
        {
            return Pool(scores.constitution - NeutralScore, HealthPerPoint, HealthNumerator, HealthDenominator);
        }

        public static int MaxManaBonus(AbilityScoreBlock scores)
        {
            return Pool(scores.wisdom - NeutralScore, ManaPerPoint, ManaNumerator, ManaDenominator);
        }

        // Charisma feeds a character's signature resource — how fast their own
        // private gauge fills each turn.
        //
        // A RATE, and the most sharply bounded of the five, because the thing
        // it fills is TINY: Shawn's Wool holds 10 and gains 1 a turn. Under the
        // old floor(d/4) a tier-10 Court set granted +48 a turn into that pool,
        // which is not a bonus, it is a different game — the gauge would be
        // full before the first enemy acted.
        //
        // Root over 3 lands it at +6 at tier 10: still the largest
        // proportional swing of any derivation here, and about the most a
        // 10-capacity pool can absorb while the bank-or-spend decision stays a
        // decision.
        //
        // The divisor is 3 rather than 2 or 4 because it is the one that keeps
        // the authored range closest to the old floor(d/4) — a Charisma 16
        // character still gets exactly its +1. The agreement is approximate
        // either side of that, which a root against a quarter-step cannot
        // avoid, and it only matters for scores no character currently has.
        //
        // Still worth being honest that it does nothing at all for a character
        // with no signature resource, which today is four of the five. That is
        // the loose end behind Court and Regalia, and it is a content problem
        // rather than an arithmetic one.
        public static int SignatureGainBonus(AbilityScoreBlock scores)
        {
            return Rate(scores.charisma - NeutralScore, 4, 1, 3);
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
