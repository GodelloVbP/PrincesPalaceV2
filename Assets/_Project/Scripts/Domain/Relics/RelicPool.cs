using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Relics
{
    // One relic as the draft sees it. A flattened view rather than a
    // ResolvedRelic so the pool rules can be tested without the content
    // pipeline, and so Core can hand over RelicDefinitions without Domain
    // learning about ScriptableObjects.
    public readonly struct RelicOption
    {
        public readonly string Id;
        public readonly RelicRarity Rarity;
        public readonly string UnlockedBy;

        // Mechanic (g), ACQUISITION GATE. See ResolvedRelic.
        // RequiresConvergenceAbility.
        public readonly bool RequiresConvergenceAbility;

        public RelicOption(string id, RelicRarity rarity, string unlockedBy = "",
                           bool requiresConvergenceAbility = false)
        {
            Id = id;
            Rarity = rarity;
            UnlockedBy = unlockedBy ?? "";
            RequiresConvergenceAbility = requiresConvergenceAbility;
        }

        public bool IsUnlockedFromTheStart => string.IsNullOrEmpty(UnlockedBy);
    }

    // Which relics may be offered, and which three actually are.
    //
    // Pure, engine-free, and randomness injected as a Func<int,int> the same
    // way ItemOfferTable and RarityTable already take it -- so every rule here
    // is testable without a scene, a save or a seed that happens to cooperate.
    public static class RelicPool
    {
        // Three, matching the item offer. One relic is not a decision and five
        // is a menu; the number is the same as the Reckoning's for the same
        // reason, and shared so they cannot drift apart in either direction.
        public const int OfferCount = 3;

        // Everything the player has actually earned the right to see.
        //
        // A relic with no `unlockedBy` is always in. A gated one needs its
        // achievement earned -- and the gate is checked against a set the
        // CALLER owns, so this has no opinion about where achievements live.
        // `partyHasConvergenceAbility` defaults to false, which is the safe
        // reading for every caller that has not been updated to compute it
        // yet -- a relic gated on mechanic (g) simply does not appear
        // rather than the gate silently failing open.
        public static List<RelicOption> Available(
            IEnumerable<RelicOption> all, ICollection<string> earnedAchievements,
            bool partyHasConvergenceAbility = false)
        {
            if (all == null) return new List<RelicOption>();

            return all
                .Where(r => r.IsUnlockedFromTheStart
                            || (earnedAchievements != null && earnedAchievements.Contains(r.UnlockedBy)))
                .Where(r => !r.RequiresConvergenceAbility || partyHasConvergenceAbility)
                .ToList();
        }

        // The three on offer at the start of a descent.
        //
        // Drawn WITHOUT REPLACEMENT -- being offered the same relic twice in
        // one draft reads as a bug, and with a pool this small it would happen
        // constantly. A pool smaller than OfferCount simply offers what it has
        // rather than padding with repeats.
        //
        // `nextIndex` is upper-bound-exclusive, matching every other injected
        // roll in Domain.
        public static List<RelicOption> Draft(
            IReadOnlyList<RelicOption> available, System.Func<int, int> nextIndex, int count = OfferCount)
        {
            var offer = new List<RelicOption>();
            if (available == null || available.Count == 0 || nextIndex == null) return offer;

            var remaining = new List<RelicOption>(available);
            int wanted = System.Math.Min(count, remaining.Count);

            for (int i = 0; i < wanted; i++)
            {
                int pick = nextIndex(remaining.Count);

                // A generator that hands back an out-of-range index must not
                // take the draft down with it -- clamped rather than trusted,
                // the same posture CombatMath takes with its own inputs.
                if (pick < 0) pick = 0;
                if (pick >= remaining.Count) pick = remaining.Count - 1;

                offer.Add(remaining[pick]);
                remaining.RemoveAt(pick);
            }

            return offer;
        }

        // How much weight a band carries in a draw. Steeply descending, so a
        // Godlike relic is a story rather than a Tuesday.
        //
        // Read off the ordinal deliberately: adding a band at the end of
        // RelicRarity gets a weight here and nothing else changes.
        public static int WeightOf(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common: return 100;
                case RelicRarity.Uncommon: return 45;
                case RelicRarity.Rare: return 18;
                case RelicRarity.UltraRare: return 6;
                case RelicRarity.Mythic: return 2;
                default: return 1;
            }
        }

        // A weighted draw over the same pool, for when the offer should favour
        // commons rather than treat a Godlike as equally likely.
        //
        // Separate from Draft rather than a flag on it: the unweighted version
        // is what tests and the glossary want, and a bool parameter that
        // silently changes the distribution is the kind of thing that gets
        // passed wrong once and never noticed.
        public static List<RelicOption> DraftWeighted(
            IReadOnlyList<RelicOption> available, System.Func<int, int> nextIndex, int count = OfferCount)
        {
            var offer = new List<RelicOption>();
            if (available == null || available.Count == 0 || nextIndex == null) return offer;

            var remaining = new List<RelicOption>(available);
            int wanted = System.Math.Min(count, remaining.Count);

            for (int i = 0; i < wanted; i++)
            {
                int total = remaining.Sum(r => WeightOf(r.Rarity));
                if (total <= 0) break;

                int roll = nextIndex(total);
                if (roll < 0) roll = 0;
                if (roll >= total) roll = total - 1;

                int running = 0;
                int picked = remaining.Count - 1;
                for (int j = 0; j < remaining.Count; j++)
                {
                    running += WeightOf(remaining[j].Rarity);
                    if (roll < running) { picked = j; break; }
                }

                offer.Add(remaining[picked]);
                remaining.RemoveAt(picked);
            }

            return offer;
        }
    }
}
