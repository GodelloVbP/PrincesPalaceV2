using System.Collections.Generic;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Equipment
{
    // What simulating an equip actually changes.
    //
    // Deltas follow StatBlock/AbilityScoreBlock's own `-` convention:
    // NEGATIVE MEANS THE CANDIDATE IS WORSE on that field.
    //
    // The two slot lists are the part that cannot be got by subtracting two
    // stat blocks, and they are the reason this is a struct rather than a pair
    // of blocks. Requirements are met from everything ELSE worn, so displacing
    // one piece can break a second piece that was leaning on it -- and the
    // other way, a candidate that grants scores can wake something already worn
    // but dormant. Neither shows up in a net total: a swap can read as +4 CON
    // while quietly switching off the boots.
    public readonly struct ItemComparison
    {
        public readonly StatBlock StatDelta;
        public readonly AbilityScoreBlock ScoreDelta;

        // Slots that were LIVE before the swap and are not after, because
        // whatever their requirement leaned on got displaced.
        public readonly IReadOnlyList<EquipmentSlot> NewlyInertSlots;

        // The other direction: inert before, live after.
        public readonly IReadOnlyList<EquipmentSlot> NewlyLiveSlots;

        // False if the candidate would be inert the moment it is worn -- its
        // own requirement unmet even with everything else this swap settles.
        // Reported separately from the two lists on purpose: cascade means
        // SOMETHING ELSE moved, and folding the candidate's own slot into it
        // would make every unwearable item look like it broke something.
        public readonly bool CandidateIsLive;

        public ItemComparison(StatBlock statDelta, AbilityScoreBlock scoreDelta,
            IReadOnlyList<EquipmentSlot> newlyInertSlots,
            IReadOnlyList<EquipmentSlot> newlyLiveSlots,
            bool candidateIsLive)
        {
            StatDelta = statDelta;
            ScoreDelta = scoreDelta;
            NewlyInertSlots = newlyInertSlots ?? new List<EquipmentSlot>();
            NewlyLiveSlots = newlyLiveSlots ?? new List<EquipmentSlot>();
            CandidateIsLive = candidateIsLive;
        }

        public static ItemComparison None => new ItemComparison(
            StatBlock.Zero, AbilityScoreBlock.Zero,
            new List<EquipmentSlot>(), new List<EquipmentSlot>(), candidateIsLive: false);

        // Whether anything at all moved. A swap with nothing to say should
        // print nothing rather than an empty heading.
        public bool IsEmpty =>
            StatDelta.Equals(StatBlock.Zero)
            && ScoreDelta.Equals(AbilityScoreBlock.Zero)
            && NewlyInertSlots.Count == 0
            && NewlyLiveSlots.Count == 0;
    }
}
