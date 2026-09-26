using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The attributes panel's six effect lines. Every expected string is a
    // PINNED LITERAL, hand-derived from the formulas' own published rates
    // (AbilityDerivation's HealthPerPoint=20/PhysicalDefensePerPoint=2/
    // ManaPerPoint=2/MagicalDefensePerPoint=2/SpeedDivisor=2/
    // SignatureGainDivisor=4, ScalingGrades.PerPoint), never the production
    // method run a second time to build its own expectation (CLAUDE.md
    // gotcha #5).
    public class AbilityEffectDescriptionsTests
    {
        // One fixed character: STR 14, DEX 16, CON 18, WIS 20, INT 12, CHA 13.
        private static AbilityScoreBlock Scores() =>
            new AbilityScoreBlock(strength: 14, dexterity: 16, constitution: 18, wisdom: 20, intelligence: 12, charisma: 13);

        [Test]
        public void Constitution_ReadsMaxHealthAndPhysicalDefensePerPoint()
        {
            // CON 18 is +8 over neutral; the rate is flat (20/point,
            // 2/point), so the one-point delta at 18->19 equals the rate at
            // every score.
            Assert.AreEqual("+20 max HP and +2 physical defense per point above 10",
                AbilityEffectDescriptions.Constitution(Scores()));
        }

        [Test]
        public void Wisdom_ReadsManaAndMagicalDefensePerPoint_PlusTheAbsoluteRegenRate()
        {
            // Mana/Magical Defense: flat 2/point, same reasoning as CON.
            // Regen is WIS 20 -> floor(20/4) = 5, read directly rather than
            // differenced (AbilityDerivation.ManaRegenBonus is the whole
            // baseline rate, not a bonus over neutral).
            Assert.AreEqual("+2 max mana, +2 magical defense per point above 10; mana regen 5 per turn",
                AbilityEffectDescriptions.Wisdom(Scores()));
        }

        [Test]
        public void Dexterity_ReadsOnePointOfSpeedPerTwoPointsOfDexterity()
        {
            // DEX 16 -> 18 (a two-point step, SpeedDivisor): SpeedBonus goes
            // (16-10)/2=3 -> (18-10)/2=4, a delta of exactly 1.
            Assert.AreEqual("+1 speed per 2 points above 10",
                AbilityEffectDescriptions.Dexterity(Scores()));
        }

        [Test]
        public void Charisma_ReadsOnePointOfSignatureGainPerFourPointsOfCharisma()
        {
            // CHA 13 -> 17 (a four-point step, SignatureGainDivisor):
            // SignatureGainBonus goes (13-10)/4=0 -> (17-10)/4=1, a delta of
            // exactly 1.
            Assert.AreEqual("+1 signature gain per 4 points above 10",
                AbilityEffectDescriptions.Charisma(Scores()));
        }

        [Test]
        public void Strength_WithAWeaponEquipped_ReadsStrGradeAndTheSetsWholeMultiplier()
        {
            // Grade A on Strength, riding the main hand alone: PerPoint(A) =
            // 0.070, four points over neutral -> 1 + 0.070*4 = 1.28.
            var weaponScaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.A);

            Assert.AreEqual("weapon scaling: STR-A (x1.28)",
                AbilityEffectDescriptions.Strength(Scores(), weaponScaling));
        }

        [Test]
        public void Strength_WithNoWeapon_ReadsTheUnarmedStrengthFallback()
        {
            // Neutral weapon scaling and real Strength -> CombatMath's own
            // unarmed fallback applies: grade B, PerPoint(B) = 0.050, four
            // points over neutral -> 1 + 0.050*4 = 1.20.
            Assert.AreEqual("no weapon: unarmed STR-B (x1.20)",
                AbilityEffectDescriptions.Strength(Scores(), ScalingSet.None));
        }

        [Test]
        public void Intelligence_ReadsItsOwnSkillScalingGradeAndMultiplier()
        {
            // Grade S on Intelligence, riding the spell tier slot: PerPoint(S)
            // = 0.100, two points over neutral (INT 12) -> 1 + 0.100*2 = 1.20.
            var skillScaling = new ScalingSet(
                ScalingProfile.None.With(AbilityScore.Intelligence, ScalingGrade.S),
                ScalingProfile.None,
                ScalingProfile.None);

            Assert.AreEqual("spell scaling: INT-S (x1.20)",
                AbilityEffectDescriptions.Intelligence(Scores(), skillScaling));
        }

        [Test]
        public void Intelligence_BelowNeutral_NamesTheScoreBesideItsGrade_SoDCannotReadAsZero()
        {
            // QA 2026-09-26: INT 8 on a D spell printed "spell scaling: D
            // x0.97", and the UI font draws D and 0 alike. PerPoint(D) =
            // 0.015, two points UNDER neutral -> 1 - 0.015*2 = 0.97.
            var scores = new AbilityScoreBlock(strength: 12, dexterity: 10, constitution: 14, wisdom: 12, intelligence: 8, charisma: 10);
            var skillScaling = new ScalingSet(
                ScalingProfile.None.With(AbilityScore.Intelligence, ScalingGrade.D),
                ScalingProfile.None,
                ScalingProfile.None);

            Assert.AreEqual("spell scaling: INT-D (x0.97)",
                AbilityEffectDescriptions.Intelligence(scores, skillScaling));
        }

        [Test]
        public void Intelligence_WhenTheSpellRidesOnlyOtherScores_SaysNoneOnIntAndKeepsTheCastMultiplier()
        {
            // Spell rides WIS at C: PerPoint(C) = 0.030, WIS 20 is ten over
            // neutral -> 1 + 0.030*10 = 1.30. INT carries no grade, so the
            // line must not print a dash where the letter goes.
            var skillScaling = new ScalingSet(
                ScalingProfile.None.With(AbilityScore.Wisdom, ScalingGrade.C),
                ScalingProfile.None,
                ScalingProfile.None);

            Assert.AreEqual("spell scaling: none on INT (x1.30)",
                AbilityEffectDescriptions.Intelligence(Scores(), skillScaling));
        }

        [Test]
        public void Intelligence_WithNoSpellScaling_NeverFallsBackToTheUnarmedStrengthLine()
        {
            // INT has no equivalent of the unarmed-STR fallback -- a caster
            // with nothing scaling their casts just scales nothing.
            Assert.AreEqual("spell scaling: no scaling authored",
                AbilityEffectDescriptions.Intelligence(Scores(), ScalingSet.None));
        }

        [Test]
        public void Describe_DispatchesEveryScoreToItsOwnFunction()
        {
            var scores = Scores();
            var weaponScaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.A);
            var skillScaling = new ScalingSet(
                ScalingProfile.None.With(AbilityScore.Intelligence, ScalingGrade.S),
                ScalingProfile.None,
                ScalingProfile.None);

            Assert.AreEqual(AbilityEffectDescriptions.Constitution(scores),
                AbilityEffectDescriptions.Describe(AbilityScore.Constitution, scores, weaponScaling, skillScaling));
            Assert.AreEqual(AbilityEffectDescriptions.Strength(scores, weaponScaling),
                AbilityEffectDescriptions.Describe(AbilityScore.Strength, scores, weaponScaling, skillScaling));
            Assert.AreEqual(AbilityEffectDescriptions.Intelligence(scores, skillScaling),
                AbilityEffectDescriptions.Describe(AbilityScore.Intelligence, scores, weaponScaling, skillScaling));
        }
    }
}
