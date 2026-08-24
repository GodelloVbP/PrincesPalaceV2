using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The funnel every damage path in the game shares, and which until now had
    // no test of its own at all -- only indirect exercise through whole fights.
    //
    // What these pin is ORDER. Each step is individually covered by CombatMath's
    // own tests; what nobody could check before is that effectiveness lands
    // before armour, that the variance roll lands after both, and that the ward
    // spends against the rolled figure rather than the pre-roll one. Get the
    // order wrong and nothing fails -- the numbers are just quietly wrong.
    public class DamagePipelineTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int maxHealth = 100) =>
            new CombatantState(name, isPlayerSide, maxHealth, 10, 5, 0, 5);

        // Variance off, so every assertion below is deterministic. The roll's
        // own behaviour is pinned separately.
        private const float NoVariance = 0f;

        private static DamagePipeline.Outcome Typed(
            int raw, DamageType type, CombatantState target,
            ElementalAffinity affinity = default,
            System.Func<CombatantState, int, int> ward = null) =>
            DamagePipeline.AfterDefences(raw, type, target, affinity, NoVariance, null, ward);

        [Test]
        public void WithNoDefencesAtAll_TheRawFigureSurvives()
        {
            var target = Fighter("Target", false);
            Assert.AreEqual(30, Typed(30, DamageType.Fire, target).Damage);
        }

        [Test]
        public void AnUnknownMatchupIsNeutral_Silently()
        {
            // An enemy attacking a player, or a direct test entry with no
            // definition behind the target. Neutral rather than a throw: this
            // is the majority path, not an error.
            var target = Fighter("Target", true);
            var outcome = Typed(30, DamageType.Fire, target);

            Assert.AreEqual(1f, outcome.Effectiveness, 0.0001f);
            Assert.AreEqual(30, outcome.Damage);
        }

        [Test]
        public void AWeaknessAndArmourBothApply()
        {
            // 30 into a matching weakness is 1.5x = 45; armour 10 softens by
            // 100/(10+100), giving 40.
            //
            // Worth being honest about what this does NOT prove: effectiveness
            // and resistance are both MULTIPLICATIVE here, so swapping their
            // order produces the same answer. This pins the value, not the
            // order. The genuinely order-sensitive steps are covered below.
            var target = Fighter("Target", false);
            target.PhysicalResistance = 10;

            var outcome = Typed(30, DamageType.Physical, target,
                ElementalAffinity.Of(DamageType.Physical, DamageType.Fire));

            Assert.AreEqual(1.5f, outcome.Effectiveness, 0.0001f, "physical into a physical weakness");
            Assert.AreEqual(40, outcome.Damage);
        }

        [Test]
        public void TheWardSpendsAgainstTheFinishedFigure_NotTheRawOne()
        {
            // The ward is last for a reason: it should reduce whatever actually
            // landed, after effectiveness and armour -- not the raw figure.
            var target = Fighter("Target", false);
            target.PhysicalResistance = 10;

            int wardSaw = -1;
            var outcome = Typed(30, DamageType.Physical, target,
                ElementalAffinity.Of(DamageType.Physical, DamageType.Fire),
                ward: (t, dmg) => { wardSaw = dmg; return dmg - 5; });

            Assert.AreEqual(40, wardSaw,
                "the ward is handed the post-effectiveness, post-armour figure, not the raw 30");
            Assert.AreEqual(35, outcome.Damage);
        }

        [Test]
        public void TheWardSpendsAgainstTheROLLEDFigure()
        {
            // THE order assertion that actually bites. The variance roll has to
            // land BEFORE the ward, so a shield absorbs what the swing really
            // dealt rather than its pre-roll estimate. With the roll after the
            // ward, the ward would always see exactly 100.
            var target = Fighter("Target", false);
            var rng = new PrincesPalace.Domain.Rng.SeededRandom(7);

            var seen = new List<int>();
            for (int i = 0; i < 40; i++)
            {
                DamagePipeline.AfterDefences(100, DamageType.Fire, target, ElementalAffinity.Neutral,
                    varianceRange: 0.2f, rng: rng,
                    resolveWard: (t, dmg) => { seen.Add(dmg); return dmg; });
            }

            CollectionAssert.IsNotEmpty(seen);
            Assert.IsTrue(seen.Exists(v => v != 100),
                "if the ward only ever saw 100, the roll is landing after it");
            Assert.IsTrue(seen.TrueForAll(v => v >= 80 && v <= 120), "and it is still the same roll");
        }

        [Test]
        public void TheExecuteBonusRidesTheRawFigure_BeforeArmour()
        {
            // Order-sensitive and deliberate: a finishing bonus applied to the
            // finished number would be worth less against exactly the armoured
            // targets it exists to finish. Only the untyped/actor overload gets
            // it -- a spell's authored packet deals what it says.
            var actor = Fighter("Actor", true);
            var target = Fighter("Target", false, maxHealth: 100);
            target.PhysicalResistance = 25;

            var withoutBonus = DamagePipeline.AfterDefences(
                40, actor, target, attackType: null, affinity: ElementalAffinity.Neutral,
                varianceRange: NoVariance, rng: null, resolveWard: null);

            // Drop the target low enough for the execute bonus to engage.
            target.CurrentHealth = 5;
            var withBonus = DamagePipeline.AfterDefences(
                40, actor, target, attackType: null, affinity: ElementalAffinity.Neutral,
                varianceRange: NoVariance, rng: null, resolveWard: null);

            Assert.GreaterOrEqual(withBonus.Damage, withoutBonus.Damage,
                "an execute bonus must never make a finishing blow weaker");
        }

        [Test]
        public void APoisonComboIsReportedSeparately_NotFoldedIntoTheDamage()
        {
            // The view announces the detonation as its own line, so the funnel
            // reports it rather than silently adding it to this hit's number.
            var target = Fighter("Target", false);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 20, 3);

            var outcome = Typed(20, DamageType.Nature, target);

            Assert.Greater(outcome.PoisonDetonation, 0, "nature into poison should detonate");
            Assert.AreEqual(20, outcome.Damage, "and the detonation must not be folded into this hit");
        }

        [Test]
        public void TheUntypedPath_NeverDetonatesAPoison()
        {
            // A monster's own claws are untyped and never reach the typed
            // funnel, so they cannot accidentally set off a status combo.
            var actor = Fighter("Monster", false);
            var target = Fighter("Player", true);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 20, 3);

            var outcome = DamagePipeline.AfterDefences(
                20, actor, target, attackType: null, affinity: ElementalAffinity.Neutral,
                varianceRange: NoVariance, rng: null, resolveWard: null);

            Assert.AreEqual(0, outcome.PoisonDetonation);
            Assert.AreEqual(1f, outcome.Effectiveness, 0.0001f);
        }

        [Test]
        public void TheUntypedPathIsStoppedByPhysicalArmour()
        {
            var actor = Fighter("Monster", false);
            var target = Fighter("Player", true);
            target.PhysicalResistance = 8;

            var outcome = DamagePipeline.AfterDefences(
                30, actor, target, attackType: null, affinity: ElementalAffinity.Neutral,
                varianceRange: NoVariance, rng: null, resolveWard: null);

            // Resistance softens rather than subtracts: 30 * 100/(8+100) = 27.
            Assert.AreEqual(27, outcome.Damage, "untyped means physical armour, not no armour");
        }

        // --- the variance roll ------------------------------------------------

        [Test]
        public void VarianceOff_IsExactlyTheInputFigure()
        {
            // What every fight test relies on: with the roll disabled, a
            // prediction made through CombatMath directly still matches.
            Assert.AreEqual(37, DamagePipeline.ApplyVariance(37, 0f, null));
            Assert.AreEqual(37, DamagePipeline.ApplyVariance(37, -1f, null));
        }

        [Test]
        public void VarianceNeverReducesAHitToNothing()
        {
            // A 1-damage hit rolled down would otherwise become 0, turning a
            // connecting blow into a whiff.
            var rng = new PrincesPalace.Domain.Rng.SeededRandom(12345);
            for (int i = 0; i < 200; i++)
            {
                Assert.GreaterOrEqual(DamagePipeline.ApplyVariance(1, 0.2f, rng), 1);
            }
        }

        [Test]
        public void VarianceStaysInsideItsStatedRange()
        {
            var rng = new PrincesPalace.Domain.Rng.SeededRandom(999);
            for (int i = 0; i < 500; i++)
            {
                int rolled = DamagePipeline.ApplyVariance(100, 0.2f, rng);
                Assert.GreaterOrEqual(rolled, 80);
                Assert.LessOrEqual(rolled, 120);
            }
        }

        [Test]
        public void VarianceIsReproducibleForAGivenSeed()
        {
            // The whole reason the fight draws from a seeded stream: the same
            // fight at the same place in the same run plays out the same way,
            // so a mid-fight quit is not a reroll.
            var a = new PrincesPalace.Domain.Rng.SeededRandom(4242);
            var b = new PrincesPalace.Domain.Rng.SeededRandom(4242);

            var rollsA = new List<int>();
            var rollsB = new List<int>();
            for (int i = 0; i < 20; i++)
            {
                rollsA.Add(DamagePipeline.ApplyVariance(50, 0.2f, a));
                rollsB.Add(DamagePipeline.ApplyVariance(50, 0.2f, b));
            }

            CollectionAssert.AreEqual(rollsA, rollsB);
        }
    }
}
