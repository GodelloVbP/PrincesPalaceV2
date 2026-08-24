using NUnit.Framework;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.Tests
{
    public class DifficultyCurveTests
    {
        // PINNED LITERALS. The curve is the design decision; recomputing
        // 1.077^step here would assert only that the method is deterministic
        // (CLAUDE.md gotcha 5, AUDIT.md #18).
        //
        // Step 8 is one tier, 80 is the whole gear ladder.
        [TestCase(0, 1.000f)]
        [TestCase(8, 1.810f)]
        [TestCase(16, 3.277f)]
        [TestCase(40, 19.437f)]
        [TestCase(80, 377.795f)]
        public void HealthMultiplier_TracksThePlayersDamage(int step, float expected)
        {
            Assert.AreEqual(expected, DifficultyCurve.HealthMultiplier(step), expected * 0.001f);
        }

        [TestCase(0, 1.000f)]
        [TestCase(8, 1.500f)]
        [TestCase(16, 2.250f)]
        [TestCase(80, 57.711f)]
        public void AttackMultiplier_TracksThePlayersHealth(int step, float expected)
        {
            Assert.AreEqual(expected, DifficultyCurve.AttackMultiplier(step), expected * 0.001f);
        }

        // GEOMETRIC, and this replaces an assertion that said the opposite.
        //
        // It used to read "TheCurveIsLinear_NotCompounding", and it was right
        // for the game it was written in: a fully honed top-tier set was worth
        // about 4x a starting one, so a compounding dungeon outran the player.
        // GearScaling and AbilityDerivation together now put a geared
        // character's damage at 374x across the ladder. A straight line
        // reaching 5.4x is no longer a difficulty curve.
        [Test]
        public void TheCurveCompounds_RatherThanClimbingInAStraightLine()
        {
            float atForty = DifficultyCurve.HealthMultiplier(40);
            float atEighty = DifficultyCurve.HealthMultiplier(80);

            Assert.AreEqual(atForty * atForty, atEighty, atEighty * 0.001f,
                "twice the depth should be the SQUARE of the climb, not twice it");
        }

        // THE TWO RATES ARE DIFFERENT, and that is the substance of the retune.
        // A single multiplier cannot keep both halves of a fight honest,
        // because the player's own two axes grow at very different speeds.
        [Test]
        public void HealthClimbsFasterThanAttack()
        {
            Assert.Greater(DifficultyCurve.HealthMultiplier(80), DifficultyCurve.AttackMultiplier(80) * 5f,
                "enemy health has to track the player's damage, which outruns their health by a long way");
        }

        [Test]
        public void StepZero_ChangesNothing()
        {
            Assert.AreEqual(1f, DifficultyCurve.HealthMultiplier(0), 0.0001f);
            Assert.AreEqual(1f, DifficultyCurve.AttackMultiplier(0), 0.0001f);
            Assert.AreEqual(37, DifficultyCurve.ScaleHealth(37, 0));
            Assert.AreEqual(37, DifficultyCurve.ScaleAttack(37, 0));
            Assert.AreEqual(37, DifficultyCurve.ScaleHealth(37, -12), "A negative step is the surface, not a discount");
        }

        [TestCase(100, 8, 181)]
        [TestCase(90, 80, 34001)]
        [TestCase(350, 80, 132228)]
        public void ScaleHealth_LandsWhereTheCurveSays(int amount, int step, int expected)
        {
            Assert.AreEqual(expected, DifficultyCurve.ScaleHealth(amount, step));
        }

        [TestCase(3, 80, 173)]
        [TestCase(9, 80, 519)]
        [TestCase(10, 8, 15)]
        public void ScaleAttack_LandsWhereTheCurveSays(int amount, int step, int expected)
        {
            Assert.AreEqual(expected, DifficultyCurve.ScaleAttack(amount, step));
        }

        [Test]
        public void Scaling_NeverRegressesAsTheRunGoesDeeper()
        {
            foreach (int amount in new[] { 1, 12, 240, 3000 })
            {
                for (int step = 1; step <= 120; step++)
                {
                    Assert.GreaterOrEqual(DifficultyCurve.ScaleHealth(amount, step),
                        DifficultyCurve.ScaleHealth(amount, step - 1),
                        $"{amount} health got weaker between step {step - 1} and {step}");
                    Assert.GreaterOrEqual(DifficultyCurve.ScaleAttack(amount, step),
                        DifficultyCurve.ScaleAttack(amount, step - 1),
                        $"{amount} attack got weaker between step {step - 1} and {step}");
                }
            }
        }

        // `step` comes off a save and nothing else bounds it. An unclamped
        // multiplication would overflow into a NEGATIVE enemy — one with
        // negative health, which every combat check would read as already
        // dead. Compounding reaches that far sooner than a straight line did:
        // 7.7% a step passes two billion around step 300.
        [Test]
        public void AnAbsurdStep_IsClampedRatherThanOverflowing()
        {
            int atCeiling = DifficultyCurve.ScaleHealth(1000, DifficultyCurve.MaxScaledStep);

            Assert.Greater(atCeiling, 0);
            Assert.AreEqual(atCeiling, DifficultyCurve.ScaleHealth(1000, 999999));
            Assert.Greater(DifficultyCurve.ScaleHealth(int.MaxValue / 1000, 999999), 0);
            Assert.Greater(DifficultyCurve.ScaleAttack(int.MaxValue / 1000, 999999), 0);
        }

        // Reward and threat ride the SAME curve. If pay lagged difficulty,
        // deep fights would be worse value per fight and the optimal play in
        // an endless dungeon would be to farm shallow rooms forever.
        [Test]
        public void RewardsRideTheSameCurveAsTheThreat()
        {
            foreach (int step in new[] { 0, 8, 16, 40, 100 })
            {
                Assert.AreEqual(DifficultyCurve.ScaleHealth(250, step), DifficultyCurve.ScaleReward(250, step),
                    $"pay and threat disagree at step {step}");
            }
        }

        // THE INVARIANT THE WHOLE RETUNE EXISTS FOR, stated in the terms it
        // was designed in: a fight should take about as many swings deep as it
        // does at the surface. The rates were measured off AbilityDerivation
        // to make this true, so if either drifts this is what says so.
        //
        // A golem holds 350 and defends at 8. A fully-geared character's ATTACK
        // is 16 at tier 0 and 2,646 at tier 10 — base 7 plus the figures
        // AbilityDerivationTests pins, not recomputed here. Damage is
        // (attack - defense) x CombatMath.DamageScale.
        [Test]
        public void AGolemTakesAboutTheSameNumberOfSwingsAtEveryDepth()
        {
            const int GolemHealth = 350;
            const int GolemDefense = 8;
            const int PlayerAttackAtTierZero = 16;
            const int PlayerAttackAtTierTen = 2753;

            float atSurface = Swings(GolemHealth, GolemDefense, PlayerAttackAtTierZero, 0);
            float atDepth = Swings(GolemHealth, GolemDefense, PlayerAttackAtTierTen, 80);

            Assert.AreEqual(atSurface, atDepth, atSurface * 0.6f,
                $"a golem takes {atSurface:0.0} swings at the surface and {atDepth:0.0} at step 80 - " +
                $"one of the two rates in DifficultyCurve is wrong");
        }

        private static float Swings(int health, int defense, int playerAttack, int step)
        {
            int hit = System.Math.Max(1, playerAttack - DifficultyCurve.ScaleAttack(defense, step)) * 10;
            return DifficultyCurve.ScaleHealth(health, step) / (float)hit;
        }

        // THE MISSING HALF OF THE INVARIANT ABOVE. AGolemTakesAboutTheSame...
        // pins player-attacks-enemy; nothing pinned enemy-attacks-player, which
        // is exactly the gap a user report walked through: Dungeon Warden's
        // authored attack (9, before this) scaled to 2.25x at step 16 -- the
        // EARLIEST a boss room can ever appear (DescentMapGenerator.
        // StepsPerBoss=16) -- for 160 damage against Shawn's starting Defense
        // (4, no ability-score bonus; see AbilityDerivation's own header on
        // why Defense is gear-only). That is exactly half of his starting
        // effective max health (320 = base 200 + CON 16's +120) in one
        // unmitigated plain swing, from a boss with no special ability at
        // all. The Hollow Choir's authored attack (12) was worse: 72%.
        //
        // 35% is not a tuned number, it is a sanity ceiling -- comfortably
        // above what a genuinely tense early boss hit should cost (roughly a
        // quarter of a health bar) and comfortably below "half your health
        // bar from one swing nothing warned you about."
        [TestCase(6, "warden")]
        [TestCase(7, "hollow_choir")]
        public void ABossDoesNotDevastateAStartingPlayerAtItsOwnEarliestStep(int bossAttack, string id)
        {
            const int EarliestBossStep = 16;
            const int StartingPlayerDefense = 4;
            const int StartingPlayerMaxHealth = 320;

            int scaledAttack = DifficultyCurve.ScaleAttack(bossAttack, EarliestBossStep);
            int damage = System.Math.Max(1, scaledAttack - StartingPlayerDefense) * 10;

            Assert.LessOrEqual(damage, StartingPlayerMaxHealth * 0.35f,
                $"{id}'s own earliest legal boss fight (step {EarliestBossStep}) deals {damage} against a " +
                $"starting player's {StartingPlayerMaxHealth} HP -- more than 35% in one unmitigated swing.");
        }
    }
}
