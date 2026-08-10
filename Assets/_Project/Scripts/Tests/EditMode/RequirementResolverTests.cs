using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // The requirement fixpoint: which worn items are actually contributing,
    // when some of them require ability scores only other worn items grant.
    //
    // Expected sets are PINNED against a single Resolve() call — never
    // re-derived by re-running the loop in the test itself (CLAUDE.md
    // gotcha #5), which would make the test a tautology.
    public class RequirementResolverTests
    {
        private static AbilityScoreBlock Str(int amount) => new AbilityScoreBlock(amount, 0, 0, 0, 0, 0);

        private static readonly AbilityScoreBlock Floor = Str(10);

        [Test]
        public void NoCandidates_ResolvesToJustTheFloor()
        {
            var result = RequirementResolver.Resolve(Floor, new List<RequirementCandidate>());

            Assert.AreEqual(Floor, result.Scores);
            CollectionAssert.IsEmpty(result.LiveSlots);
            CollectionAssert.IsEmpty(result.InertSlots);
        }

        [Test]
        public void ACandidateWithNoRequirement_IsAlwaysLive()
        {
            var candidate = new RequirementCandidate(EquipmentSlot.Torso, Str(3), AbilityScoreBlock.Zero);
            var result = RequirementResolver.Resolve(Floor, new[] { candidate });

            CollectionAssert.Contains(result.LiveSlots, EquipmentSlot.Torso);
            Assert.AreEqual(13, result.Scores.strength);
        }

        // THE rule that makes requirements mean anything: a helm requiring
        // 15 STR that itself grants +5 STR must not bootstrap itself active
        // at floor 10. Without excluding its own bonus, 10 + 5 = 15 would
        // satisfy its own requirement and the gate would do nothing.
        [Test]
        public void SelfSatisfaction_IsExcluded()
        {
            var helm = new RequirementCandidate(EquipmentSlot.Head, Str(5), Str(15));
            var result = RequirementResolver.Resolve(Floor, new[] { helm });

            CollectionAssert.Contains(result.InertSlots, EquipmentSlot.Head);
            CollectionAssert.IsEmpty(result.LiveSlots);
            Assert.AreEqual(Floor, result.Scores, "An inert item contributes nothing, including no bonus");
        }

        // A ring grants Strength a pair of gloves needs; the gloves' own
        // Strength is what the helm needs on top of that. All three hold up
        // together — this is the case a naive one-item-at-a-time check would
        // also get right, which is why the COLLAPSE test below is the one
        // that actually proves the fixpoint.
        [Test]
        public void ARingGlovesHelmChain_AllStayLiveWhenTheChainHolds()
        {
            var ring = new RequirementCandidate(EquipmentSlot.Necklace, Str(5), AbilityScoreBlock.Zero);
            var gloves = new RequirementCandidate(EquipmentSlot.Gloves, Str(5), Str(15));
            var helm = new RequirementCandidate(EquipmentSlot.Head, Str(5), Str(20));

            var result = RequirementResolver.Resolve(Floor, new[] { ring, gloves, helm });

            CollectionAssert.AreEquivalent(new[] { EquipmentSlot.Necklace, EquipmentSlot.Gloves, EquipmentSlot.Head }, result.LiveSlots);
            CollectionAssert.IsEmpty(result.InertSlots);
            Assert.AreEqual(25, result.Scores.strength, "10 floor + 5 ring + 5 gloves + 5 helm");
        }

        // Remove the ring link and the WHOLE chain collapses in one Resolve
        // call, not just the ring itself — this is what a naive single pass
        // (check each candidate once against the full set, remove failures,
        // stop) would get WRONG: a single pass would still find the gloves
        // legal (10 floor + 5 helm = 15, exactly enough with the helm still
        // counted), leaving the helm as the only failure, when the truth is
        // that once the ring is gone the gloves cannot hold either and the
        // helm has nothing left to lean on.
        [Test]
        public void RemovingTheRingLink_CascadesToCollapseTheWholeChain()
        {
            var gloves = new RequirementCandidate(EquipmentSlot.Gloves, Str(5), Str(15));
            var helm = new RequirementCandidate(EquipmentSlot.Head, Str(5), Str(20));

            var result = RequirementResolver.Resolve(Floor, new[] { gloves, helm });

            CollectionAssert.IsEmpty(result.LiveSlots, "With no ring, neither the gloves nor the helm can hold");
            CollectionAssert.AreEquivalent(new[] { EquipmentSlot.Gloves, EquipmentSlot.Head }, result.InertSlots);
            Assert.AreEqual(Floor, result.Scores);
        }

        // The greatest-fixpoint claim rests on this: the SAME candidates in
        // any order converge to the SAME live set. Order independence is not
        // incidental — it is what makes "remove every failure each round"
        // equivalent to "the unique largest self-consistent set" rather than
        // just one arbitrary valid answer among several.
        [Test]
        public void ShuffledInput_ConvergesToTheSameLiveSet()
        {
            var ring = new RequirementCandidate(EquipmentSlot.Necklace, Str(5), AbilityScoreBlock.Zero);
            var gloves = new RequirementCandidate(EquipmentSlot.Gloves, Str(5), Str(15));
            var helm = new RequirementCandidate(EquipmentSlot.Head, Str(5), Str(20));
            var unreachable = new RequirementCandidate(EquipmentSlot.Legs, Str(1), Str(99));

            var orderings = new List<List<RequirementCandidate>>
            {
                new List<RequirementCandidate> { ring, gloves, helm, unreachable },
                new List<RequirementCandidate> { helm, unreachable, ring, gloves },
                new List<RequirementCandidate> { unreachable, gloves, helm, ring },
                new List<RequirementCandidate> { gloves, ring, unreachable, helm },
            };

            var liveSets = orderings
                .Select(order => new HashSet<EquipmentSlot>(RequirementResolver.Resolve(Floor, order).LiveSlots))
                .ToList();

            var expected = new HashSet<EquipmentSlot> { EquipmentSlot.Necklace, EquipmentSlot.Gloves, EquipmentSlot.Head };
            foreach (var liveSet in liveSets)
            {
                CollectionAssert.AreEquivalent(expected, liveSet);
            }
        }

        // Eight slots, each requiring exactly the cumulative bonus of every
        // slot before it — the deepest chain a real paperdoll can produce.
        // Proves convergence holds at the full depth, not just a 3-link toy.
        [Test]
        public void AnEightDeepChain_FullyConverges()
        {
            var order = new[]
            {
                EquipmentSlot.Head, EquipmentSlot.Necklace, EquipmentSlot.Torso, EquipmentSlot.Legs,
                EquipmentSlot.Shoes, EquipmentSlot.Gloves, EquipmentSlot.Weapon1, EquipmentSlot.Weapon2,
            };

            var candidates = new List<RequirementCandidate>();
            for (int i = 0; i < order.Length; i++)
            {
                // Slot i requires exactly the floor plus every earlier
                // slot's own +2 STR — its own bonus never counts toward it.
                candidates.Add(new RequirementCandidate(order[i], Str(2), Str(10 + 2 * i)));
            }

            var result = RequirementResolver.Resolve(Floor, candidates);

            CollectionAssert.AreEquivalent(order, result.LiveSlots);
            CollectionAssert.IsEmpty(result.InertSlots);
            Assert.AreEqual(10 + 2 * order.Length, result.Scores.strength);
        }

        // Non-negative bonuses are what content validation is supposed to
        // guarantee (Phase 3b), not something this resolver enforces itself
        // — so this is a defensive guard, not a correctness claim: even a
        // candidate that somehow carries a negative bonus must not make
        // Resolve loop forever. The active set only ever shrinks each round
        // it does not converge, which bounds termination regardless of sign.
        [Test]
        public void ANegativeBonusCandidate_StillTerminates()
        {
            var cursed = new RequirementCandidate(EquipmentSlot.Head, Str(-3), AbilityScoreBlock.Zero);

            Assert.DoesNotThrow(() => RequirementResolver.Resolve(Floor, new[] { cursed }));
        }
    }
}
