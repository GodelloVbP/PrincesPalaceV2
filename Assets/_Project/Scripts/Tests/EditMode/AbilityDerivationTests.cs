using NUnit.Framework;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Every expectation here is a PINNED LITERAL. Nothing in this file
    // recomputes a production formula, calls the method under test to build
    // its own expected value, or reaches for a constant from
    // AbilityDerivation -- all three are ways a test moves in lockstep with
    // the code and stops being able to fail (CLAUDE.md gotcha #5,
    // AUDIT.md #18).
    //
    // PHASE 2 REWRITE: the old piecewise curves (a flat rate inside a
    // character's authored range, quadratic/root beyond it, split at
    // CharacterBand) are gone along with AttackBonus, CharacterBand, Pool and
    // Rate. Every derivation is a single straight line through zero at score
    // 10, for every score at every value, forever -- so this file no longer
    // needs a separate "what does gear do out past 187 points" section. A
    // boundary table at a handful of scores says everything there is to say.
    public class AbilityDerivationTests
    {
        private static AbilityScoreBlock Scores(int str = 10, int dex = 10, int con = 10, int wis = 10, int intel = 10, int cha = 10)
        {
            return new AbilityScoreBlock(str, dex, con, wis, intel, cha);
        }

        // THE compatibility guarantee, and the reason this layer could be
        // added to a shipping game without rebalancing anything. Every
        // character in the game currently carries a flat 10 across the
        // board; if any of these stopped being zero, every derived stat
        // would silently change at once.
        //
        // ManaRegenBonus is the one deliberate exception -- see its own
        // header: it is not a (score - 10) bonus/penalty, it is the whole
        // baseline rate, so a neutral WIS 10 still derives floor(10/4) = 2,
        // not 0. DerivedStats can therefore no longer equal StatBlock.Zero at
        // an all-tens spread; it equals a block with ONLY manaRegen set.
        [Test]
        public void AllTens_DeriveExactlyZeroOnEveryStat_ExceptTheStandardManaRegenBaseline()
        {
            var neutral = Scores();

            Assert.AreEqual(0, AbilityDerivation.MaxHealthBonus(neutral));
            Assert.AreEqual(0, AbilityDerivation.PhysicalDefenseBonus(neutral));
            Assert.AreEqual(0, AbilityDerivation.MaxManaBonus(neutral));
            Assert.AreEqual(0, AbilityDerivation.MagicalDefenseBonus(neutral));
            Assert.AreEqual(0, AbilityDerivation.SpeedBonus(neutral));
            Assert.AreEqual(0, AbilityDerivation.SignatureGainBonus(neutral));
            Assert.AreEqual(2, AbilityDerivation.ManaRegenBonus(neutral), "floor(10 / 4) = 2");
            Assert.AreEqual(new StatBlock(0, 0, 0, manaRegen: 2), AbilityDerivation.DerivedStats(neutral));
        }

        // ---- the boundary table -------------------------------------------
        //
        // Six scores (0, 5, 9, 10, 11, 20), six derived stats. d = score - 10
        // in every case. HP/PDEF ride Constitution, Mana/MDEF ride Wisdom --
        // both linear multiplications, unambiguous at every d. Speed and
        // SignatureGain divide (by 2 and 4 respectively), which is where a
        // rounding convention actually matters below neutral -- see the two
        // FloorConvention tests further down for why these particular
        // literals are correct and not mathematical floor.

        [TestCase(0, -200)]
        [TestCase(5, -100)]
        [TestCase(9, -20)]
        [TestCase(10, 0)]
        [TestCase(11, 20)]
        [TestCase(20, 200)]
        public void MaxHealthBonus_IsPinnedAcrossTheBoundaryTable(int con, int expected)
        {
            Assert.AreEqual(expected, AbilityDerivation.MaxHealthBonus(Scores(con: con)));
        }

        [TestCase(0, -20)]
        [TestCase(5, -10)]
        [TestCase(9, -2)]
        [TestCase(10, 0)]
        [TestCase(11, 2)]
        [TestCase(20, 20)]
        public void PhysicalDefenseBonus_IsPinnedAcrossTheBoundaryTable(int con, int expected)
        {
            Assert.AreEqual(expected, AbilityDerivation.PhysicalDefenseBonus(Scores(con: con)));
        }

        [TestCase(0, -20)]
        [TestCase(5, -10)]
        [TestCase(9, -2)]
        [TestCase(10, 0)]
        [TestCase(11, 2)]
        [TestCase(20, 20)]
        public void MaxManaBonus_IsPinnedAcrossTheBoundaryTable(int wis, int expected)
        {
            Assert.AreEqual(expected, AbilityDerivation.MaxManaBonus(Scores(wis: wis)));
        }

        [TestCase(0, -20)]
        [TestCase(5, -10)]
        [TestCase(9, -2)]
        [TestCase(10, 0)]
        [TestCase(11, 2)]
        [TestCase(20, 20)]
        public void MagicalDefenseBonus_IsPinnedAcrossTheBoundaryTable(int wis, int expected)
        {
            Assert.AreEqual(expected, AbilityDerivation.MagicalDefenseBonus(Scores(wis: wis)));
        }

        // d/2 truncated toward zero: -10/2=-5, -5/2=-2, -1/2=0, 0/2=0, 1/2=0,
        // 10/2=5. See the FloorConvention test below for why 9 lands on 0
        // rather than -1.
        [TestCase(0, -5)]
        [TestCase(5, -2)]
        [TestCase(9, 0)]
        [TestCase(10, 0)]
        [TestCase(11, 0)]
        [TestCase(20, 5)]
        public void SpeedBonus_IsPinnedAcrossTheBoundaryTable(int dex, int expected)
        {
            Assert.AreEqual(expected, AbilityDerivation.SpeedBonus(Scores(dex: dex)));
        }

        // d/4 truncated toward zero: -10/4=-2, -5/4=-1, -1/4=0, 0/4=0, 1/4=0,
        // 10/4=2.
        [TestCase(0, -2)]
        [TestCase(5, -1)]
        [TestCase(9, 0)]
        [TestCase(10, 0)]
        [TestCase(11, 0)]
        [TestCase(20, 2)]
        public void SignatureGainBonus_IsPinnedAcrossTheBoundaryTable(int cha, int expected)
        {
            Assert.AreEqual(expected, AbilityDerivation.SignatureGainBonus(Scores(cha: cha)));
        }

        // floor(WIS / 4), NOT (WIS - 10) / 4 -- see ManaRegenBonus's own
        // header for why this one derivation is not measured against the
        // neutral score the way every other row in this table is.
        [TestCase(0, 0)]
        [TestCase(3, 0)]
        [TestCase(10, 2)]
        [TestCase(14, 3)]
        [TestCase(20, 5)]
        public void ManaRegenBonus_IsPinnedAcrossTheBoundaryTable(int wis, int expected)
        {
            Assert.AreEqual(expected, AbilityDerivation.ManaRegenBonus(Scores(wis: wis)));
        }

        // ---- the floor/truncation convention, pinned as its own fact ------
        //
        // AbilityDerivation.cs documents the choice at length: SpeedBonus and
        // SignatureGainBonus use C#'s native `/`, which TRUNCATES TOWARD ZERO
        // rather than taking the mathematical floor. The two conventions only
        // disagree on an odd difference below neutral, so score 9 (d = -1) is
        // the smallest case that actually distinguishes them: truncation
        // gives 0, mathematical floor would give -1.
        [Test]
        public void SpeedBonus_TruncatesTowardZero_RatherThanMathematicalFloor()
        {
            Assert.AreEqual(0, AbilityDerivation.SpeedBonus(Scores(dex: 9)),
                "d=-1 truncated toward zero is 0; mathematical floor would give -1");
            Assert.AreEqual(-1, AbilityDerivation.SpeedBonus(Scores(dex: 7)),
                "d=-3 truncated toward zero is -1; mathematical floor would give -2 -- " +
                "this is the exact value the plan's Shawn example pins");
        }

        [Test]
        public void SignatureGainBonus_TruncatesTowardZero_RatherThanMathematicalFloor()
        {
            Assert.AreEqual(0, AbilityDerivation.SignatureGainBonus(Scores(cha: 9)),
                "d=-1 truncated toward zero is 0; mathematical floor would give -1");
            Assert.AreEqual(-1, AbilityDerivation.SignatureGainBonus(Scores(cha: 5)),
                "d=-5 truncated toward zero is -1; mathematical floor would give -2");
        }

        // ---- Strength and Intelligence: zero, deliberately -----------------
        //
        // Not a boundary table -- there is no curve to have a boundary in.
        // Both scores derive nothing at all here; they ride weapon/spell
        // grades instead (Phase 3/D3, not yet wired). Extreme values included
        // specifically to rule out "it happens to be zero near 10" -- STR/INT
        // do not even have a private constant that could accidentally fire.
        //
        // manaRegen: 2, not StatBlock.Zero outright -- every fixture here
        // fields the default WIS 10, and ManaRegenBonus's floor(WIS / 4)
        // baseline is live at every score, STR/INT included.
        [TestCase(0)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(200)]
        public void StrengthDerivesNothing_AtAnyScore(int str)
        {
            Assert.AreEqual(new StatBlock(0, 0, 0, manaRegen: 2),
                AbilityDerivation.DerivedStats(Scores(str: str, dex: 10, con: 10, wis: 10, intel: 10, cha: 10)));
        }

        [TestCase(0)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(200)]
        public void IntelligenceDerivesNothing_AtAnyScore(int intel)
        {
            Assert.AreEqual(new StatBlock(0, 0, 0, manaRegen: 2),
                AbilityDerivation.DerivedStats(Scores(str: 10, dex: 10, con: 10, wis: 10, intel: intel, cha: 10)));
        }

        // ---- DerivedStats combines correctly, attack always 0 -------------
        //
        // The scores are independent inputs; moving one must not disturb
        // another's output. Cheap to assert, and the kind of thing a
        // copy-paste slip between near-identical methods breaks silently.
        [Test]
        public void DerivedStats_CombinesTheFourFeedingScoresAndTouchesNothingElse()
        {
            var block = AbilityDerivation.DerivedStats(Scores(str: 14, dex: 8, con: 16, wis: 20, intel: 20, cha: 20));

            Assert.AreEqual(120, block.maxHealth, "CON 16: (16-10)*20");
            Assert.AreEqual(12, block.physicalDefense, "CON 16: (16-10)*2");
            Assert.AreEqual(20, block.magicalDefense, "WIS 20: (20-10)*2");
            Assert.AreEqual(-1, block.speed, "DEX 8: (8-10)/2 truncated");
            Assert.AreEqual(0, block.attack, "Attack derives from no ability score until Phase 3 (weapon grades)");
            Assert.AreEqual(5, block.manaRegen, "WIS 20: floor(20 / 4)");
        }

        // Shawn's authored spread, pinned here so his identity is a fact the
        // suite protects rather than a number in a builder someone can
        // adjust without noticing what it does to him. Matches the plan's
        // worked example exactly: +120 HP, +12 PDEF, -1 Speed, +8 Mana,
        // +8 MDEF, +0 Signature Gain -- plus the standard mana-regen
        // baseline, WIS 14 -> floor(14 / 4) = 3.
        [Test]
        public void ShawnsSpread_DerivesTheProfileThePlanPins()
        {
            var shawn = Scores(str: 7, dex: 7, con: 16, wis: 14, intel: 6, cha: 10);

            Assert.AreEqual(120, AbilityDerivation.MaxHealthBonus(shawn), "CON 16: d=6, 6*20");
            Assert.AreEqual(12, AbilityDerivation.PhysicalDefenseBonus(shawn), "CON 16: d=6, 6*2");
            Assert.AreEqual(-1, AbilityDerivation.SpeedBonus(shawn), "DEX 7: d=-3, truncated toward zero");
            Assert.AreEqual(8, AbilityDerivation.MaxManaBonus(shawn), "WIS 14: d=4, 4*2");
            Assert.AreEqual(8, AbilityDerivation.MagicalDefenseBonus(shawn), "WIS 14: d=4, 4*2");
            Assert.AreEqual(0, AbilityDerivation.SignatureGainBonus(shawn), "CHA 10: d=0");
            Assert.AreEqual(3, AbilityDerivation.ManaRegenBonus(shawn), "WIS 14: floor(14 / 4)");

            var block = AbilityDerivation.DerivedStats(shawn);
            Assert.AreEqual(120, block.maxHealth);
            Assert.AreEqual(12, block.physicalDefense);
            Assert.AreEqual(-1, block.speed);
            Assert.AreEqual(8, block.magicalDefense);
            Assert.AreEqual(0, block.attack);
            Assert.AreEqual(3, block.manaRegen, "WIS 14: floor(14 / 4)");
        }

        // ---- signed, and symmetric now that the curve is a straight line --

        [Test]
        public void APenaltyMirrorsABonusExactly_BecauseTheCurveIsLinearNow()
        {
            // No band, no square, no root left to break the mirror -- a
            // straight line through zero is symmetric by construction. Still
            // worth pinning: it is the property the old piecewise curve had
            // to work hard for (see AbilityDerivationTests' Phase-1-era
            // history in git blame) and a future re-introduction of a curve
            // would break it silently otherwise.
            Assert.AreEqual(-AbilityDerivation.MaxHealthBonus(Scores(con: 16)),
                AbilityDerivation.MaxHealthBonus(Scores(con: 4)));
            Assert.AreEqual(-AbilityDerivation.PhysicalDefenseBonus(Scores(con: 16)),
                AbilityDerivation.PhysicalDefenseBonus(Scores(con: 4)));
            Assert.AreEqual(-AbilityDerivation.MaxManaBonus(Scores(wis: 16)),
                AbilityDerivation.MaxManaBonus(Scores(wis: 4)));
            Assert.AreEqual(-AbilityDerivation.MagicalDefenseBonus(Scores(wis: 16)),
                AbilityDerivation.MagicalDefenseBonus(Scores(wis: 4)));
        }
    }
}
