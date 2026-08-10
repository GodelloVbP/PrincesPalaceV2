using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Equipment
{
    // One candidate slot in the fixpoint: what it grants if it is ACTIVE,
    // and what it demands in order to become active. Engine-free and
    // content-free on purpose, same reason EquipmentLoadout is — this knows
    // nothing about ItemDefinition or ContentDatabase, only already-resolved
    // numbers (with RequirementCurve's tuning knob already applied), which is
    // what makes the whole fixpoint EditMode-testable.
    public readonly struct RequirementCandidate
    {
        public readonly EquipmentSlot Slot;
        public readonly AbilityScoreBlock Bonus;
        public readonly AbilityScoreBlock Requirement;

        public RequirementCandidate(EquipmentSlot slot, AbilityScoreBlock bonus, AbilityScoreBlock requirement)
        {
            Slot = slot;
            Bonus = bonus;
            Requirement = requirement;
        }
    }

    // The settled result of one resolve: the ability scores the LIVE set
    // actually produces, which slots are contributing, and which are inert
    // (their requirement is unmet even counting every OTHER active slot).
    public readonly struct RequirementResolution
    {
        public readonly AbilityScoreBlock Scores;
        public readonly IReadOnlyList<EquipmentSlot> LiveSlots;
        public readonly IReadOnlyList<EquipmentSlot> InertSlots;

        public RequirementResolution(AbilityScoreBlock scores, IReadOnlyList<EquipmentSlot> liveSlots, IReadOnlyList<EquipmentSlot> inertSlots)
        {
            Scores = scores;
            LiveSlots = liveSlots;
            InertSlots = inertSlots;
        }

        public bool IsLive(EquipmentSlot slot) => LiveSlots.Contains(slot);
    }

    // Which worn items are actually contributing their bonus, when some of
    // them REQUIRE ability scores that only other worn items grant.
    //
    // THE GREATEST FIXPOINT, not a single pass. Start from every candidate
    // active (the largest possible set) and repeatedly remove every item
    // whose requirement is not met by everyone ELSE currently active, until
    // a round removes nothing. This converges because the active set only
    // ever shrinks (bounded by the candidate count, 8 for a paperdoll) and
    // is ORDER-INDEPENDENT because bonuses are non-negative (enforced by
    // content validation, not here): a larger active set can only ever help
    // an item's requirement, never hurt it, so removing an entire round's
    // failures at once — rather than one at a time in some arbitrary order —
    // still lands on the same unique largest legal set. That is the
    // "greatest fixpoint" claim, and ShuffledInput_ConvergesToTheSameSet
    // is what proves it rather than just asserting it.
    //
    // AN ITEM NEVER SATISFIES ITS OWN REQUIREMENT: legality is checked
    // against the total MINUS this candidate's own bonus. Without that, a
    // helm requiring 15 STR that itself grants +5 STR would bootstrap itself
    // active at 10 base STR, which defeats the entire point of a
    // requirement — SelfSatisfaction_IsExcluded pins exactly this case.
    public static class RequirementResolver
    {
        public static RequirementResolution Resolve(AbilityScoreBlock floor, IReadOnlyList<RequirementCandidate> candidates)
        {
            candidates ??= Array.Empty<RequirementCandidate>();
            var active = new List<RequirementCandidate>(candidates);

            while (true)
            {
                var total = floor;
                foreach (var candidate in active)
                {
                    total += candidate.Bonus;
                }

                var illegal = new HashSet<EquipmentSlot>();
                foreach (var candidate in active)
                {
                    if (!MeetsRequirement(total, candidate.Bonus, candidate.Requirement))
                    {
                        illegal.Add(candidate.Slot);
                    }
                }

                if (illegal.Count == 0)
                {
                    var liveSet = new HashSet<EquipmentSlot>(active.Select(c => c.Slot));
                    var live = candidates.Where(c => liveSet.Contains(c.Slot)).Select(c => c.Slot).ToList();
                    var inert = candidates.Where(c => !liveSet.Contains(c.Slot)).Select(c => c.Slot).ToList();
                    return new RequirementResolution(total, live, inert);
                }

                active = active.Where(c => !illegal.Contains(c.Slot)).ToList();
            }
        }

        private static bool MeetsRequirement(AbilityScoreBlock totalWithSelf, AbilityScoreBlock selfBonus, AbilityScoreBlock requirement)
        {
            foreach (AbilityScore score in AbilityScores.All)
            {
                int withoutSelf = totalWithSelf[score] - selfBonus[score];
                if (withoutSelf < requirement[score])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
