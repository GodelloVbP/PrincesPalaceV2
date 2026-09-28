using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.Domain.Tests
{
    public class DifficultyCurveTests
    {
        // PINNED LITERALS. The curve is the design decision; recomputing
        // 1.075^step here would assert only that the method is deterministic
        // (CLAUDE.md gotcha 5, AUDIT.md #18).
        //
        // PHASE 5B (D6) RETUNE: 77 -> 75 permille. Step 8 is one leg/floor,
        // 80 is the whole gear ladder.
        [TestCase(0, 1.000f)]
        [TestCase(8, 1.783f)]
        [TestCase(16, 3.181f)]
        [TestCase(40, 18.044f)]
        [TestCase(80, 325.595f)]
        public void HealthMultiplier_TracksThePlayersDamage(int step, float expected)
        {
            Assert.AreEqual(expected, DifficultyCurve.HealthMultiplier(step), expected * 0.001f);
        }

        // PHASE 5B (D6) RETUNE: 52 -> 38 permille -- the attack rate moved
        // further than health's did, because enemy DEFENSE came off the
        // depth curve entirely in the same retune (see ScaleAttack's own
        // header) and the old 52 was tuned for a world where a growing
        // defense partly absorbed it.
        [TestCase(0, 1.000f)]
        [TestCase(8, 1.348f)]
        [TestCase(16, 1.816f)]
        [TestCase(80, 19.760f)]
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
        // character's power at orders of magnitude across the ladder. A
        // straight line is no longer a difficulty curve.
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

        // PHASE 5B (D6) RETUNE literals, hand-floored from the pinned
        // multipliers above -- never the production Math.Pow rerun
        // (CLAUDE.md gotcha 5).
        [TestCase(100, 8, 178)]
        [TestCase(90, 80, 29303)]
        [TestCase(350, 80, 113958)]
        public void ScaleHealth_LandsWhereTheCurveSays(int amount, int step, int expected)
        {
            Assert.AreEqual(expected, DifficultyCurve.ScaleHealth(amount, step));
        }

        [TestCase(3, 80, 59)]
        [TestCase(9, 80, 177)]
        [TestCase(10, 8, 13)]
        public void ScaleAttack_LandsWhereTheCurveSays(int amount, int step, int expected)
        {
            Assert.AreEqual(expected, DifficultyCurve.ScaleAttack(amount, step));
        }

        // A ROUND-LIMITED FIGHT'S ENEMY ATTACK rides 53 permille, a room
        // fight's the attack rate's 38. Hand-floored literals: 33 x 1.053^17 =
        // 79.40, 33 x 1.053^33 = 181.41, 33 x 1.038^17 = 62.21, 33 x 1.038^33
        // = 112.99 (33 is the Bellwether's authored attack; steps 17 and 33 are
        // where the bot meets it on floors 3 and 5).
        [TestCase(33, 17, 10, 79)]
        [TestCase(33, 33, 10, 181)]
        [TestCase(33, 33, 1, 181)]
        [TestCase(33, 0, 10, 33)]
        public void ScaleEnemyAttack_WithARoundLimit_RidesTheRoundLimitedRate(int amount, int step, int roundLimit, int expected)
        {
            Assert.AreEqual(expected, DifficultyCurve.ScaleEnemyAttack(amount, step, roundLimit));
        }

        [TestCase(33, 17, 62)]
        [TestCase(33, 33, 112)]
        [TestCase(10, 8, 13)]
        public void ScaleEnemyAttack_WithNoRoundLimit_IsTheAttackRate(int amount, int step, int expected)
        {
            Assert.AreEqual(expected, DifficultyCurve.ScaleEnemyAttack(amount, step, 0));
            Assert.AreEqual(expected, DifficultyCurve.ScaleEnemyAttack(amount, step, -1), "a negative limit is no limit");
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
        // 7.5% a step passes two billion well before step 300.
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
        // was designed in: a fight should take roughly the same ORDER OF
        // MAGNITUDE of swings deep as it does at the surface -- not exactly
        // the same count. §P's own non-boss TTK bands say trash attrition is
        // meant to DRIFT UP across floors by design (rat 2.6 @F1 -> 3.6 @F5),
        // so this is deliberately a wide sanity bound, not the tight §P
        // literal pin -- that pin is BalanceSheetTests' job (Phase 5D), which
        // asserts the exact per-row bands against real content and the real
        // weapon-driven damage pipeline. This test only has to catch
        // DifficultyCurve's own two rates drifting apart by an order of
        // magnitude.
        //
        // ROUTED THROUGH CombatMath.AfterResistance (fixed 2026-08-26) --
        // `Swings` used to hand-roll the mitigation division itself
        // (`Math.Max(1, raw * 100 / (100 + defense))`), deliberately NOT
        // going through CombatMath.ComputeAttackDamage, because that function
        // still multiplied by CombatMath.DamageScale (x5) at the time this
        // was written, and this file has no authority over the weapon model
        // that constant belonged to. ComputeAttackDamage no longer scales at
        // all (see its own header), so that reason is gone -- but this test
        // still constructs no real weapon/CombatantState (that stays Phase
        // 3/4's job), so it now calls the real AfterResistance function
        // directly instead of duplicating its formula by hand, which is the
        // "route through the actual production function" this file can offer
        // without adopting the weapon model wholesale.
        //
        // PHASE 5B (D6): the golem's own defense is now UNSCALED at every
        // depth (defense no longer rides DifficultyCurve at all) -- only its
        // HEALTH pool (via ScaleHealth) changes with step. `playerRawDamage`
        // stands in for the player's own weapon-driven progression, which
        // this file has no authority to compute (that is Phase 3/4's
        // WeaponDamageTests/WeaponEntryResolverTests); the same two
        // magnitude-only literals from before the D3 weapon-model rewrite
        // are kept here because this test only needs a "grows a lot" input,
        // not an exact figure -- neither was ever on the DamageScale path,
        // so neither needed to change when that bug was fixed.
        [Test]
        public void AGolemTakesAboutTheSameOrderOfMagnitudeOfSwingsAtEveryDepth()
        {
            const int GolemHealth = 350;
            const int GolemDefense = 8;
            const int PlayerRawDamageAtTheSurface = 16;
            const int PlayerRawDamageAtTheDepth = 2753;

            float atSurface = Swings(GolemHealth, GolemDefense, PlayerRawDamageAtTheSurface, 0);
            float atDepth = Swings(GolemHealth, GolemDefense, PlayerRawDamageAtTheDepth, 80);

            // A full 100% relative band (i.e. up to 2x either way) -- wide on
            // purpose, see the header above. Anything past that means one of
            // the two permille rates is off by more than a rounding error.
            Assert.AreEqual(atSurface, atDepth, atSurface * 1.0f,
                $"a golem takes {atSurface:0.0} swings at the surface and {atDepth:0.0} at step 80 - " +
                $"one of the two rates in DifficultyCurve has drifted an order of magnitude");
        }

        // The canonical mitigation equation (D1/DamagePipeline's own header),
        // via the real CombatMath.AfterResistance rather than a hand-rolled
        // copy of its formula -- see the header above for why this no longer
        // needs to dodge CombatMath.ComputeAttackDamage. Defense is passed
        // through UNSCALED, per D6.
        private static float Swings(int health, int defense, int playerRawDamage, int step)
        {
            int hit = CombatMath.AfterResistance(playerRawDamage, defense);
            return DifficultyCurve.ScaleHealth(health, step) / (float)hit;
        }

        // THE MISSING HALF OF THE INVARIANT ABOVE. AGolemTakesAboutTheSame...
        // pins player-attacks-enemy; this pins enemy-attacks-player, which is
        // exactly the gap a user report walked through under the OLD (pre-D6)
        // cadence and formula: Dungeon Warden's authored attack scaled to
        // 2.25x at step 16 -- the EARLIEST a boss room could appear under the
        // old StepsPerBoss=16 -- for a devastating hit against a starting
        // player's flat `Defense` stat, back when that single subtractive
        // stat existed.
        //
        // PHASE 5B (D6) REWRITE: the earliest a boss can appear is now step 8
        // (DescentMapGenerator: boss forced at step ≡ 0 mod 8), `Defense` no
        // longer exists (split into PhysicalDefense/MagicalDefense), and
        // mitigation is the canonical percentage equation, not the old
        // subtractive one. `bossAttack`/`startingPlayerDefense` are still
        // hand-authored magnitude stand-ins (this file has no authority over
        // real enemy or gear content, D5/D4's job respectively) — the two
        // pairs below are chosen to sit in the same neighbourhood as §P's own
        // "Noob @F1" profile (PDEF 34, MDEF 8) and its earliest-boss row
        // (hollow_choir @ step 8), without asserting this test is that pin.
        //
        // 35% is not a tuned number, it is a sanity ceiling -- comfortably
        // above what a genuinely tense early boss hit should cost (roughly a
        // quarter of a health bar) and comfortably below "half your health
        // bar from one swing nothing warned you about."
        [TestCase(24, 8, "hollow_choir (Arcane, meets Magical Defense)")]
        [TestCase(40, 34, "forest_warden (Physical, meets Physical Defense)")]
        public void ABossDoesNotDevastateAStartingPlayerAtItsOwnEarliestStep(
            int bossAttack, int startingPlayerDefense, string id)
        {
            const int EarliestBossStep = 8;
            const int StartingPlayerMaxHealth = 350;

            int scaledAttack = DifficultyCurve.ScaleAttack(bossAttack, EarliestBossStep);
            int damage = CombatMath.AfterResistance(scaledAttack, startingPlayerDefense);

            Assert.LessOrEqual(damage, StartingPlayerMaxHealth * 0.35f,
                $"{id}'s own earliest legal boss fight (step {EarliestBossStep}) deals {damage} against a " +
                $"starting player's {StartingPlayerMaxHealth} HP -- more than 35% in one unmitigated swing.");
        }
    }
}
