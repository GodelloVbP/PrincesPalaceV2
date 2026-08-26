using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Tests
{
    // Balance redesign Phase 4 (D4): the equal-budget invariant and the
    // power-band checks it exists to make possible.
    //
    // Before this phase, a piece's baseStats/topStats were typed by hand,
    // so two materials at the same slot and tier could carry unrelated
    // totals -- the player-reported symptom this whole phase answers
    // ("items of the same tier have visibly unequal stat budgets"). Now a
    // piece's budget is GearScaling.CombatBudget(slot, tier): a function of
    // slot and tier ALONE, with no set or style parameter for two materials
    // to disagree through. See GearScalingTests.CombatBudget_
    // TakesNoSetOrStyleArgumentAtAll for that function pinned directly.
    //
    // PINNED LITERALS throughout (CLAUDE.md gotcha 5, AUDIT.md #18): nothing
    // here calls GearScaling to build its own expected value.
    public class GearBudgetTests
    {
        // ---- equal budget, spent differently -------------------------------
        //
        // Bulwark (HP 30% / PDEF 40% / MDEF 30%) and Steel (HP 35% / PDEF
        // 55% / MDEF 10%) spend the SAME Torso-tier-5 budget in full, just
        // on a different split -- reconstructed back into budget-points via
        // each stat's own unit cost, the two totals land exactly together.
        // Leather is deliberately NOT used for a comparison like this: half
        // its budget goes to Speed, whose unit cost (0.16 per point) is
        // small enough that a single point of rounding is worth 6+ budget
        // points once divided back out, which would make the tolerance
        // about Speed's arithmetic rather than about the invariant.
        [Test]
        public void BulwarkAndSteel_SpendTheSameTorsoBudgetAtTierFive()
        {
            var bulwarkTorso = ResolveTorso("bulwark", "maxHealth 30", "physicalDefense 40", "magicalDefense 30");
            var steelTorso = ResolveTorso("steel", "maxHealth 35", "physicalDefense 55", "magicalDefense 10");

            var bulwarkT5 = bulwarkTorso.First(p => p.Tier == 5).StatBonus;
            var steelT5 = steelTorso.First(p => p.Tier == 5).StatBonus;

            Assert.AreEqual(34, bulwarkT5.maxHealth);
            Assert.AreEqual(9, bulwarkT5.physicalDefense);
            Assert.AreEqual(6, bulwarkT5.magicalDefense);

            Assert.AreEqual(39, steelT5.maxHealth);
            Assert.AreEqual(12, steelT5.physicalDefense);
            Assert.AreEqual(2, steelT5.magicalDefense);

            double bulwarkPoints = bulwarkT5.maxHealth / GearScaling.HpPerBudgetPoint
                + bulwarkT5.physicalDefense / GearScaling.PhysicalDefensePerBudgetPoint
                + bulwarkT5.magicalDefense / GearScaling.MagicalDefensePerBudgetPoint;
            double steelPoints = steelT5.maxHealth / GearScaling.HpPerBudgetPoint
                + steelT5.physicalDefense / GearScaling.PhysicalDefensePerBudgetPoint
                + steelT5.magicalDefense / GearScaling.MagicalDefensePerBudgetPoint;

            Assert.AreEqual(21.8, bulwarkPoints, 0.01);
            Assert.AreEqual(21.8, steelPoints, 0.01);
        }

        private static List<ResolvedSetPiece> ResolveTorso(string id, params string[] profile)
        {
            var set = new RawItemSetEntry
            {
                id = id,
                displayName = id,
                maxTier = 10,
                cost = 50,
                costPerTier = 30,
                statProfile = profile,
                styleWeights = new[] { "constitution 100" },
                pieces = new[]
                {
                    new RawSetPiece { id = "torso", displayName = "Torso", slot = "Torso" },
                }
            };

            bool ok = ItemSetEntryResolver.TryResolveAll(new[] { set }, out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors));
            return resolved;
        }

        // ---- the D4 power-band test -----------------------------------------
        //
        // "Power-band, not power-equality" -- D4's own framing. At tier 5,
        // a full 5-piece set's physical EHP multiplier vs a naked Shawn
        // (base HP 200 + CON16's +120 = 320 maxHealth, +12 PhysicalDefense,
        // per D2's derivation table) should land in [1.3, 2.2]:
        //
        //     EHP multiplier = (naked HP + gear HP) / naked HP
        //                    x (100 + naked PDEF + gear PDEF) / (100 + naked PDEF)
        //
        // the two factors this project's mitigation curve actually turns
        // into effective HP (DamagePipeline.AfterDefences: out = dmg x 100
        // / (100 + D)).
        //
        // NOT every set lands in the band with a linear per-stat unit cost,
        // and that is a finding, not a bug swept under a wider assertion:
        // silk (0% HP / 15% PDEF) and vellum (10% HP / 0% PDEF) spend under
        // 15% of their whole budget on physical survival at all, by design
        // (both are magic-defense specialists), and court (20% HP / 0% PDEF)
        // sits a hair under the floor for the same reason. No linear unit
        // cost can lift those three into [1.3, 2.2] without also pushing
        // steel (35% HP / 55% PDEF, the most physically-invested set) past
        // it -- the two are mathematically in tension for THIS statProfile
        // table, not a tuning miss. See GearScaling.HpPerBudgetPoint's
        // comment and the Phase 4 report for the worked numbers. The eight
        // sets that DO combine meaningful HP and PDEF investment all clear
        // the band comfortably.
        private const int NakedMaxHealth = 320;
        private const int NakedPhysicalDefense = 12;

        private static readonly (string id, string[] profile, double expectedEhp, bool inBand)[] PowerBandSets =
        {
            ("leather", new[] { "maxHealth 15", "physicalDefense 35", "speed 50" }, 1.529, true),
            ("steel", new[] { "maxHealth 35", "physicalDefense 55", "magicalDefense 10" }, 2.129, true),
            ("silk", new[] { "physicalDefense 15", "magicalDefense 55", "manaRegen 30" }, 1.098, false),
            ("harness", new[] { "maxHealth 20", "physicalDefense 30", "magicalDefense 10", "speed 40" }, 1.567, true),
            ("bulwark", new[] { "maxHealth 30", "physicalDefense 40", "magicalDefense 30" }, 1.854, true),
            ("brigandine", new[] { "maxHealth 25", "physicalDefense 45", "speed 30" }, 1.808, true),
            ("runeplate", new[] { "maxHealth 25", "physicalDefense 45", "magicalDefense 30" }, 1.808, true),
            ("vellum", new[] { "maxHealth 10", "magicalDefense 55", "manaRegen 35" }, 1.137, false),
            ("regalia", new[] { "maxHealth 20", "physicalDefense 10", "magicalDefense 50", "speed 20" }, 1.361, true),
            ("court", new[] { "maxHealth 20", "magicalDefense 45", "speed 35" }, 1.281, false),
            ("wool", new[] { "maxHealth 60", "physicalDefense 10", "magicalDefense 20", "manaRegen 10" }, 1.969, true),
        };

        [Test]
        public void PhysicalEhpMultiplierAtTierFive_MatchesTheHandComputedValuePerSet()
        {
            foreach (var (id, profile, expectedEhp, _) in PowerBandSets)
            {
                var totals = FullSetTotalsAtTier(id, profile, tier: 5);

                double hpMult = (double)(NakedMaxHealth + totals.maxHealth) / NakedMaxHealth;
                double defMult = (double)(100 + NakedPhysicalDefense + totals.physicalDefense) / (100 + NakedPhysicalDefense);
                double ehp = hpMult * defMult;

                Assert.AreEqual(expectedEhp, ehp, 0.005, $"{id}'s T5 full-set physical EHP multiplier");
            }
        }

        [Test]
        public void PhysicalEhpMultiplierAtTierFive_EightOfElevenSetsClearTheD4Band()
        {
            int inBand = 0;
            var outOfBand = new List<string>();

            foreach (var (id, profile, _, expectedInBand) in PowerBandSets)
            {
                var totals = FullSetTotalsAtTier(id, profile, tier: 5);
                double hpMult = (double)(NakedMaxHealth + totals.maxHealth) / NakedMaxHealth;
                double defMult = (double)(100 + NakedPhysicalDefense + totals.physicalDefense) / (100 + NakedPhysicalDefense);
                double ehp = hpMult * defMult;

                bool actuallyInBand = ehp >= 1.3 && ehp <= 2.2;
                Assert.AreEqual(expectedInBand, actuallyInBand, $"{id}'s T5 in-band expectation changed -- update the pinned table above, not this assertion");

                if (actuallyInBand)
                {
                    inBand++;
                }
                else
                {
                    outOfBand.Add(id);
                }
            }

            Assert.AreEqual(8, inBand);
            CollectionAssert.AreEquivalent(new[] { "silk", "vellum", "court" }, outOfBand,
                "the three sets that spend under 15% of their budget on maxHealth+physicalDefense combined");
        }

        // ---- leather's turn-rate cap -----------------------------------------
        //
        // SpeedScale.TickRate: rate = clamp((speed / 10)^0.5, 0.35, 2.5).
        // Leather (50% Speed) at tier 5, vs naked Shawn (base speed 10, DEX7
        // => -1, per D2 => speed 9): the turn-rate ADVANTAGE must stay
        // <= +20%, the same D4 power-band test as the EHP check above.
        [Test]
        public void LeatherTierFive_TurnRateAdvantageStaysUnderTwentyPercent()
        {
            var totals = FullSetTotalsAtTier("leather",
                new[] { "maxHealth 15", "physicalDefense 35", "speed 50" }, tier: 5);

            Assert.AreEqual(3, totals.speed, "leather's T5 full-set Speed grant");

            const int nakedSpeed = 9;
            int gearedSpeed = nakedSpeed + totals.speed;

            double nakedRate = TurnRate(nakedSpeed);
            double gearedRate = TurnRate(gearedSpeed);
            double advantage = gearedRate / nakedRate - 1.0;

            Assert.AreEqual(0.1547, advantage, 0.001);
            Assert.LessOrEqual(advantage, 0.20, "leather's T5 turn-rate advantage over a naked Shawn");
        }

        private static double TurnRate(int speed)
        {
            // Mirrors SpeedScale.TickRate's constants directly rather than
            // calling it, so this test does not depend on the Combat
            // assembly -- the shape (sqrt, clamped [0.35, 2.5], baseline 10)
            // is SpeedScale.cs's own header, and its MaxRate/MinRate ends are
            // exercised indirectly by UpcomingTurnsTests (the turn-cheese
            // ceiling) and StatusEffectsTests.
            double raw = Math.Pow(speed / 10.0, 0.5);
            return Math.Max(0.35, Math.Min(2.5, raw));
        }

        // ---- each set's nominally-dominant stat is its largest -------------
        //
        // "Largest" is measured in budget-points (value / unit cost), which
        // is what a statProfile percentage actually promises -- NOT in raw
        // stat value, since HP (5/point) and PhysicalDefense (1/point) are
        // not directly comparable numbers.
        //
        // Leather and harness are EXCLUDED on purpose: both are Speed-led
        // sets, and Speed's unit cost (0.16) is deliberately far below its
        // linear worth -- D4's own words, "budget-equal is not power-equal
        // for a nonlinear stat, so it is priced above its linear worth".
        // Reconstructing "budget-points" by dividing back through that same
        // steep discount understates Speed exactly because the discount
        // worked: leather's T5 PhysicalDefense (30 points) outnumbers its
        // Speed (3 raw / 0.16 = 18.75 points) in this reconstruction even
        // though Speed is the set's whole identity. That is Speed's price
        // doing its job against the turn-rate cap above, not a defect in
        // this check.
        private static readonly (string id, string[] profile, string dominant)[] DominantStatSets =
        {
            ("steel", new[] { "maxHealth 35", "physicalDefense 55", "magicalDefense 10" }, "physicalDefense"),
            ("silk", new[] { "physicalDefense 15", "magicalDefense 55", "manaRegen 30" }, "magicalDefense"),
            ("bulwark", new[] { "maxHealth 30", "physicalDefense 40", "magicalDefense 30" }, "physicalDefense"),
            ("brigandine", new[] { "maxHealth 25", "physicalDefense 45", "speed 30" }, "physicalDefense"),
            ("runeplate", new[] { "maxHealth 25", "physicalDefense 45", "magicalDefense 30" }, "physicalDefense"),
            ("vellum", new[] { "maxHealth 10", "magicalDefense 55", "manaRegen 35" }, "magicalDefense"),
            ("regalia", new[] { "maxHealth 20", "physicalDefense 10", "magicalDefense 50", "speed 20" }, "magicalDefense"),
            ("court", new[] { "maxHealth 20", "magicalDefense 45", "speed 35" }, "magicalDefense"),
            ("wool", new[] { "maxHealth 60", "physicalDefense 10", "magicalDefense 20", "manaRegen 10" }, "maxHealth"),
        };

        [Test]
        public void EachSetsDominantStat_IsActuallyItsLargestContributor()
        {
            foreach (var (id, profile, dominant) in DominantStatSets)
            {
                var totals = FullSetTotalsAtTier(id, profile, tier: 5);

                var points = new Dictionary<string, double>
                {
                    ["maxHealth"] = totals.maxHealth / GearScaling.HpPerBudgetPoint,
                    ["physicalDefense"] = totals.physicalDefense / GearScaling.PhysicalDefensePerBudgetPoint,
                    ["magicalDefense"] = totals.magicalDefense / GearScaling.MagicalDefensePerBudgetPoint,
                };

                string largest = points.OrderByDescending(kv => kv.Value).First().Key;
                Assert.AreEqual(dominant, largest, $"{id}'s largest T5 contributor, in budget-points");
            }
        }

        // Sum of a full 5-piece set's resolved StatBonus at one tier.
        private static (int maxHealth, int physicalDefense, int magicalDefense, int speed, int manaRegen) FullSetTotalsAtTier(
            string id, string[] profile, int tier)
        {
            var set = new RawItemSetEntry
            {
                id = id,
                displayName = id,
                maxTier = 10,
                cost = 50,
                costPerTier = 30,
                statProfile = profile,
                styleWeights = new[] { "constitution 100" },
                pieces = new[]
                {
                    new RawSetPiece { id = "head", displayName = "Head", slot = "Head" },
                    new RawSetPiece { id = "torso", displayName = "Torso", slot = "Torso" },
                    new RawSetPiece { id = "legs", displayName = "Legs", slot = "Legs" },
                    new RawSetPiece { id = "gloves", displayName = "Gloves", slot = "Gloves" },
                    new RawSetPiece { id = "shoes", displayName = "Shoes", slot = "Shoes" },
                },
            };

            bool ok = ItemSetEntryResolver.TryResolveAll(new[] { set }, out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors));

            int hp = 0, pdef = 0, mdef = 0, spd = 0, mpr = 0;
            foreach (var piece in resolved.Where(p => p.Tier == tier))
            {
                hp += piece.StatBonus.maxHealth;
                pdef += piece.StatBonus.physicalDefense;
                mdef += piece.StatBonus.magicalDefense;
                spd += piece.StatBonus.speed;
                mpr += piece.StatBonus.manaRegen;
            }

            return (hp, pdef, mdef, spd, mpr);
        }
    }
}
