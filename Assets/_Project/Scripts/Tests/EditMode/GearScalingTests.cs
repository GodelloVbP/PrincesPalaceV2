using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The gear formula, pinned.
    //
    // PINNED LITERALS throughout. Nothing here recomputes
    // Base x SlotWeight x weight x TierGrowth^tier to build its own expected
    // value — that would assert only that the method is deterministic
    // (CLAUDE.md gotcha 5, AUDIT.md #18). These numbers are the ones the design
    // was agreed on, so a retune has to come here and say so out loud.
    public class GearScalingTests
    {
        // ---- the slot ladder ---------------------------------------------

        [Test]
        public void TheFiveArmourSlotsShareExactlyOneSet()
        {
            // Summing to 5 is what makes a full set worth Base x 5 whatever
            // the mix, and it is what lets the weights be read as percentages.
            double total =
                GearScaling.SlotWeight(EquipmentSlot.Torso) +
                GearScaling.SlotWeight(EquipmentSlot.Legs) +
                GearScaling.SlotWeight(EquipmentSlot.Head) +
                GearScaling.SlotWeight(EquipmentSlot.Shoes) +
                GearScaling.SlotWeight(EquipmentSlot.Gloves);

            Assert.AreEqual(5.0, total, 0.0001);
        }

        [Test]
        public void TheSlotSpreadIsFlatEnoughThatEveryDropIsWorthPickingUp()
        {
            // A torso is worth 1.32 gloves, not 3. With three offers a fight,
            // a steeper spread would make two of every three a consolation
            // prize.
            double torso = GearScaling.SlotWeight(EquipmentSlot.Torso);
            double gloves = GearScaling.SlotWeight(EquipmentSlot.Gloves);

            Assert.Less(torso / gloves, 1.6, "the chest has become the only drop that matters");
            Assert.Greater(torso, gloves, "and it should still be the best one");
        }

        [Test]
        public void ANonArmourSlotWeighsNothing()
        {
            // A necklace is not part of a material set and a weapon scales
            // through weapons.json. Zero makes a set that names one by mistake
            // grant nothing, which the resolver already refuses, rather than
            // inventing a value nobody authored.
            Assert.AreEqual(0.0, GearScaling.SlotWeight(EquipmentSlot.Necklace), 0.0001);
            Assert.AreEqual(0.0, GearScaling.SlotWeight(EquipmentSlot.Weapon1), 0.0001);
        }

        // ---- the tier curve ----------------------------------------------

        [Test]
        public void TenTiersAreWorthNineAndAThirdTimesTheFirst()
        {
            // The headline number of the whole ladder: 1.25^10.
            Assert.AreEqual(9.3132, GearScaling.TopMultiplier(10), 0.0005);
        }

        [TestCase(0, 0.0000)]
        [TestCase(1, 0.0301)]
        [TestCase(3, 0.1147)]
        [TestCase(5, 0.2468)]
        [TestCase(8, 0.5967)]
        [TestCase(10, 1.0000)]
        public void TheCurveAcceleratesRatherThanClimbingEvenly(int tier, double expected)
        {
            // Half the LADDER is under a quarter of the CURVE. That is the
            // whole point of the change and the thing to look at first if the
            // early floors ever feel flat.
            Assert.AreEqual(expected, GearScaling.CurveFraction(tier, 10), 0.0005);
        }

        [Test]
        public void BothEndsOfTheCurveAreExact()
        {
            // A set author writing "9 at tier 10" gets 9 at tier 10, and
            // raising maxTier stretches the curve rather than inflating the
            // tiers that already exist.
            Assert.AreEqual(0.0, GearScaling.CurveFraction(0, 10), 0.0001);
            Assert.AreEqual(1.0, GearScaling.CurveFraction(10, 10), 0.0001);
            Assert.AreEqual(1.0, GearScaling.CurveFraction(99, 10), 0.0001, "past the end is still the end");
        }

        // ---- what a real piece is worth ------------------------------------

        // The three the design was agreed against, by hand, before any of this
        // existed. Runeplate is STR 0.6 / INT 0.5, vellum INT 0.6 / WIS 0.5,
        // wool WIS 0.8 / CON 0.3.
        [TestCase(EquipmentSlot.Torso, 0.6, 3)]     // runeplate cuirass, STR
        [TestCase(EquipmentSlot.Torso, 0.5, 3)]     // runeplate cuirass, INT (2.5 rounds away from zero)
        [TestCase(EquipmentSlot.Head, 0.6, 2)]      // vellum hood, INT
        [TestCase(EquipmentSlot.Head, 0.5, 2)]      // vellum hood, WIS
        [TestCase(EquipmentSlot.Shoes, 0.8, 3)]     // wool booties, WIS
        [TestCase(EquipmentSlot.Shoes, 0.3, 1)]     // wool booties, CON
        public void ABaseTierPieceIsWorthWhatTheDesignSaid(EquipmentSlot slot, double weight, int expected)
        {
            Assert.AreEqual(expected, GearScaling.AtBaseTier(slot, weight));
        }

        [TestCase(EquipmentSlot.Torso, 0.6, 28)]
        [TestCase(EquipmentSlot.Torso, 0.5, 23)]
        [TestCase(EquipmentSlot.Shoes, 0.8, 27)]
        [TestCase(EquipmentSlot.Shoes, 0.3, 10)]
        public void ATopTierPieceIsWorthWhatTheDesignSaid(EquipmentSlot slot, double weight, int expected)
        {
            Assert.AreEqual(expected, GearScaling.AtTopTier(slot, weight, 10));
        }

        [Test]
        public void TheTopIsScaledFromTheUNROUNDEDBase()
        {
            // Wool's boots are 2.88 at tier 0. Rounding that to 3 and then
            // multiplying gives 28; scaling first and rounding once gives 27.
            // The difference is small here and compounds badly on a stat with
            // a fractional weight, which is why the order is fixed rather than
            // incidental.
            Assert.AreEqual(3, GearScaling.AtBaseTier(EquipmentSlot.Shoes, 0.8));
            Assert.AreEqual(27, GearScaling.AtTopTier(EquipmentSlot.Shoes, 0.8, 10),
                "the top was scaled from a rounded base");
        }

        // ---- what a piece demands ------------------------------------------

        [TestCase(1.0, 10, 93)]   // a pure style asks 10 of its one stat, 93 at the top
        [TestCase(0.7, 7, 65)]    // steel's constitution
        [TestCase(0.4, 4, 37)]    // steel's strength
        [TestCase(0.8, 8, 75)]    // wool's wisdom
        [TestCase(0.3, 3, 28)]    // wool's constitution
        public void ARequirementIsTheBaselineSplitByTheStylesOwnWeights(
            double weight, int atBase, int atTop)
        {
            Assert.AreEqual(atBase, GearScaling.RequirementAtBaseTier(weight));
            Assert.AreEqual(atTop, GearScaling.RequirementAtTopTier(weight, 10));
        }

        // Granted stats carry a slot weight and demands do not, so a set
        // unlocks all at once rather than gloves-first. That is enforced by
        // RequirementAtBaseTier taking no slot argument at all -- there was a
        // test here asserting it and it was a tautology, comparing one call to
        // an identical one.

        [Test]
        public void AFullSetIsWearable_ButOnePieceOfItAloneIsNot()
        {
            // THE CASE THAT WOULD BREAK THE GAME, and it cannot be reasoned
            // about from the numbers alone -- it depends on the resolver being
            // a GREATEST fixpoint. Starting from everything active and shrinking
            // means five pieces vouch for each other; starting from nothing and
            // growing would leave a full top-tier set entirely inert, because
            // no single piece qualifies on base scores.
            const double Weight = 1.0;
            const int MaxTier = 10;

            var slots = new[]
            {
                EquipmentSlot.Torso, EquipmentSlot.Legs, EquipmentSlot.Head,
                EquipmentSlot.Shoes, EquipmentSlot.Gloves,
            };

            int demand = GearScaling.RequirementAtTopTier(Weight, MaxTier);
            var candidates = slots.Select(slot => new RequirementCandidate(
                slot,
                Con(GearScaling.AtTopTier(slot, Weight, MaxTier)),
                Con(demand))).ToList();

            // A character with nothing invested: Shawn's authored Constitution.
            var floor = new AbilityScoreBlock(10, 10, 16, 10, 10, 10);

            var whole = RequirementResolver.Resolve(floor, candidates);
            CollectionAssert.IsEmpty(whole.InertSlots,
                $"a complete top-tier set demanding {demand} each went inert");

            var alone = RequirementResolver.Resolve(floor, new[] { candidates[0] });
            Assert.AreEqual(1, alone.InertSlots.Count,
                "one piece with nothing behind it should not qualify on base scores");
        }

        [Test]
        public void WearingTheWrongMaterialDoesNotMeetTheGate()
        {
            // Four pieces of a style that barely touches Constitution cannot
            // carry a Constitution-gated torso. This is the requirement doing
            // its job: committing to a material, not collecting the best
            // individual pieces.
            const int MaxTier = 10;

            var wool = new[] { EquipmentSlot.Legs, EquipmentSlot.Head, EquipmentSlot.Shoes, EquipmentSlot.Gloves }
                .Select(slot => new RequirementCandidate(
                    slot,
                    Con(GearScaling.AtTopTier(slot, 0.3, MaxTier)),
                    Con(GearScaling.RequirementAtTopTier(0.3, MaxTier))))
                .ToList();

            var bulwarkTorso = new RequirementCandidate(
                EquipmentSlot.Torso,
                Con(GearScaling.AtTopTier(EquipmentSlot.Torso, 1.0, MaxTier)),
                Con(GearScaling.RequirementAtTopTier(1.0, MaxTier)));

            var mixed = wool.Concat(new[] { bulwarkTorso }).ToList();
            var settled = RequirementResolver.Resolve(new AbilityScoreBlock(10, 10, 16, 10, 10, 10), mixed);

            CollectionAssert.Contains(settled.InertSlots, EquipmentSlot.Torso);
        }

        private static AbilityScoreBlock Con(int amount) =>
            new AbilityScoreBlock(0, 0, amount, 0, 0, 0);

        [Test]
        public void RoundingGoesAwayFromZeroRatherThanToEven()
        {
            // 2.5 must be 3, not 2. Math.Round and Mathf.RoundToInt both
            // default to banker's rounding, which would land two stats sitting
            // either side of a half in opposite directions -- CLAUDE.md
            // records a real test flake from exactly that.
            Assert.AreEqual(3, GearScaling.AtBaseTier(EquipmentSlot.Torso, 0.5),
                "4 x 1.25 x 0.5 is exactly 2.5 and must round up");
        }
    }
}
