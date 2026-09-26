using System.Globalization;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.Domain.Stats
{
    // WHAT ONE ABILITY SCORE ACTUALLY DOES, in the exact numbers the game
    // reads -- the attributes panel's one job (owner item: "showing stat
    // effects"). Every figure below is READ off AbilityDerivation/ScalingSet
    // by calling the real method, never a restated constant, so a retuned
    // rate or divisor cannot leave the panel's own prose lying about it
    // (CLAUDE.md gotcha #5's twin -- a comment can drift from a formula the
    // same way a test's expected value can).
    //
    // CON/WIS's flat bonuses (HealthPerPoint, PhysicalDefensePerPoint, etc.)
    // are a straight line through zero at NeutralScore (AbilityDerivation's
    // own header), so the marginal gain from one point is the same at every
    // score -- reading it as a ONE-POINT delta (current block vs. current+1)
    // is both exact and stable.
    //
    // DEX/CHA are NOT that shape below the surface: SpeedBonus/
    // SignatureGainBonus divide (score - 10) by a divisor with C#'s
    // truncate-toward-zero integer division (AbilityDerivation's own
    // "FLOOR-DIVISION CONVENTION" comment), so a single point sometimes
    // moves the bonus by zero and sometimes by one depending on parity. A
    // one-point delta would print "+0" for roughly half of all scores, which
    // is technically exact but reads as "this stat does nothing" to a player
    // sitting on an even DEX. Stepping by the SCORE'S OWN DIVISOR instead
    // (two points for Speed, four for Signature Gain) lands on a whole
    // multiple of the divisor from any starting score, which always yields
    // exactly +1 -- still two real calls to the derivation function and a
    // printed delta, just spaced to match the rate the designer actually
    // authored instead of an arbitrary one-point probe.
    //
    // WIS's mana-regen clause is neither shape: ManaRegenBonus is the
    // character's WHOLE baseline rate, not a bonus over neutral (see its own
    // header), so it is read directly rather than differenced.
    //
    // STR/INT derive nothing here at all (AbilityDerivation's own header) --
    // both ride weapon/spell scaling instead, so their lines read the
    // character's live ScalingSet (ContentDatabase.EffectiveWeaponScaling /
    // EffectiveSkillScaling, resolved by the Core-layer caller and handed in
    // already resolved, since Domain cannot see ContentDatabase) through
    // ScalingSet.MultiplierFor -- the exact multiplier a swing or a cast
    // actually applies.
    public static class AbilityEffectDescriptions
    {
        // One line for any of the six scores, dispatched by which two extra
        // inputs it needs -- CON/WIS/DEX/CHA read only the score block,
        // STR/INT also need the character's current weapon/spell scaling.
        // The caller (CharacterDossierController) always has both in hand
        // already, so passing sets the panel does not use for a given score
        // costs nothing.
        public static string Describe(AbilityScore score, AbilityScoreBlock scores,
            ScalingSet weaponScaling, ScalingSet skillScaling)
        {
            switch (score)
            {
                case AbilityScore.Constitution: return Constitution(scores);
                case AbilityScore.Wisdom: return Wisdom(scores);
                case AbilityScore.Dexterity: return Dexterity(scores);
                case AbilityScore.Charisma: return Charisma(scores);
                case AbilityScore.Strength: return Strength(scores, weaponScaling);
                case AbilityScore.Intelligence: return Intelligence(scores, skillScaling);
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(score), score,
                        "AbilityEffectDescriptions has no line for this AbilityScore.");
            }
        }

        public static string Constitution(AbilityScoreBlock scores)
        {
            var bumped = scores.With(AbilityScore.Constitution, scores.constitution + 1);
            int health = AbilityDerivation.MaxHealthBonus(bumped) - AbilityDerivation.MaxHealthBonus(scores);
            int defense = AbilityDerivation.PhysicalDefenseBonus(bumped) - AbilityDerivation.PhysicalDefenseBonus(scores);
            return $"+{health} max HP and +{defense} physical defense per point above {AbilityDerivation.NeutralScore}";
        }

        public static string Wisdom(AbilityScoreBlock scores)
        {
            var bumped = scores.With(AbilityScore.Wisdom, scores.wisdom + 1);
            int mana = AbilityDerivation.MaxManaBonus(bumped) - AbilityDerivation.MaxManaBonus(scores);
            int defense = AbilityDerivation.MagicalDefenseBonus(bumped) - AbilityDerivation.MagicalDefenseBonus(scores);
            int regen = AbilityDerivation.ManaRegenBonus(scores);
            return $"+{mana} max mana, +{defense} magical defense per point above {AbilityDerivation.NeutralScore}; " +
                   $"mana regen {regen} per turn";
        }

        public static string Dexterity(AbilityScoreBlock scores)
        {
            var stepped = scores.With(AbilityScore.Dexterity, scores.dexterity + AbilityDerivation.SpeedDivisor);
            int perStep = AbilityDerivation.SpeedBonus(stepped) - AbilityDerivation.SpeedBonus(scores);
            return $"+{perStep} speed per {AbilityDerivation.SpeedDivisor} points above {AbilityDerivation.NeutralScore}";
        }

        public static string Charisma(AbilityScoreBlock scores)
        {
            var stepped = scores.With(AbilityScore.Charisma, scores.charisma + AbilityDerivation.SignatureGainDivisor);
            int perStep = AbilityDerivation.SignatureGainBonus(stepped) - AbilityDerivation.SignatureGainBonus(scores);
            return $"+{perStep} signature gain per {AbilityDerivation.SignatureGainDivisor} points above {AbilityDerivation.NeutralScore}";
        }

        public static string Strength(AbilityScoreBlock scores, ScalingSet weaponScaling)
        {
            return ScalingLine("weapon scaling", scores, weaponScaling, AbilityScore.Strength, allowUnarmedFallback: true);
        }

        public static string Intelligence(AbilityScoreBlock scores, ScalingSet skillScaling)
        {
            return ScalingLine("spell scaling", scores, skillScaling, AbilityScore.Intelligence, allowUnarmedFallback: false);
        }

        // Shared by Strength/Intelligence: the grade riding this score across
        // every slot of the set, plus the set's own whole-multiplier
        // (ScalingSet.MultiplierFor -- additive across slots, never a second
        // opinion computed here). CombatMath.ComputeAttackDamage's own
        // unarmed condition (neutral weapon scaling, real Strength) is
        // mirrored exactly rather than approximated, so the panel never
        // claims a fallback is riding when a real fight would not apply it.
        private static string ScalingLine(string label, AbilityScoreBlock scores, ScalingSet set,
            AbilityScore score, bool allowUnarmedFallback)
        {
            if (allowUnarmedFallback && set.IsNeutral && scores.strength > 0)
            {
                var fallback = CombatMath.UnarmedStrengthScaling;
                var fallbackGrade = GradeFor(fallback, score);
                return $"no weapon: unarmed {AbilityScores.ShortName(score)}-{ScalingGrades.Letter(fallbackGrade)} " +
                       $"(x{FormatMultiplier(fallback.MultiplierFor(scores))})";
            }

            if (set.IsNeutral)
            {
                return $"{label}: no scaling authored";
            }

            // Same "STR-B (x1.10)" shape as the unarmed line above: the
            // score's short name glued to its grade, the multiplier in
            // parentheses. A bare letter did not survive the UI font -- in
            // its squared face "D" and "0" are one glyph, so "spell scaling:
            // D x0.97" read as the number 0 followed by a second number
            // (QA 2026-09-26). "INT-D" can only be a grade.
            var grade = GradeFor(set, score);
            string multiplier = FormatMultiplier(set.MultiplierFor(scores));
            string shortName = AbilityScores.ShortName(score);
            if (grade == ScalingGrade.None)
            {
                // The set rides other scores but not this one: say so rather
                // than print "INT-—", and keep the multiplier, which is
                // still what the swing or cast applies.
                return $"{label}: none on {shortName} (x{multiplier})";
            }

            return $"{label}: {shortName}-{ScalingGrades.Letter(grade)} (x{multiplier})";
        }

        // InvariantCulture -- ":0.00" reads the CURRENT culture otherwise
        // (PoolTierResolution's own multiplier line hits the same thing),
        // and a comma decimal separator would read as a second number.
        private static string FormatMultiplier(float multiplier) =>
            multiplier.ToString("0.00", CultureInfo.InvariantCulture);

        // The strongest grade this one score carries across the set's three
        // slots -- same "max across slots" rule FightHudModel.ScalingLabel
        // applies across every score at once, narrowed to the one score a
        // STR/INT line is actually about.
        private static ScalingGrade GradeFor(ScalingSet set, AbilityScore score)
        {
            var grade = set.SpellTier[score];
            if (set.MainHand[score] > grade) grade = set.MainHand[score];
            if (set.OffHand[score] > grade) grade = set.OffHand[score];
            return grade;
        }
    }
}
