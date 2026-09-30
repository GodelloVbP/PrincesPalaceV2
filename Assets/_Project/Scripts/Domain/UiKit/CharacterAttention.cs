using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // What decides whether a character wants looking at (plan C20), as plain
    // values. Core reads them off the save and the run (CharacterAttentionFeed)
    // and this file owns the rule, so the strip chip, the rail row and the
    // map's hint can only ever agree.
    //
    // Gear is deliberately absent: an empty gear slot is a state, not a task,
    // and a pip on it would light on every fresh character.
    public readonly struct AttentionFacts
    {
        // Attribute points the character has earned and not placed.
        public readonly int UnspentPoints;

        // Track levels reached and not yet collected.
        public readonly int RewardsWaiting;

        // Whether the character's primary pool reads spell books at all
        // (SpellBooks.CanHold). A Fury user has slots on paper and never a use
        // for them.
        public readonly bool CanHoldBooks;

        // Book slots with nothing in them, and the run's books that no one has
        // placed. A pip needs both: an empty slot with no book to put in it
        // is not something the player can act on.
        public readonly int EmptyBookSlots;
        public readonly int UnassignedBooks;

        public AttentionFacts(int unspentPoints, int rewardsWaiting, bool canHoldBooks,
                              int emptyBookSlots, int unassignedBooks)
        {
            UnspentPoints = unspentPoints;
            RewardsWaiting = rewardsWaiting;
            CanHoldBooks = canHoldBooks;
            EmptyBookSlots = emptyBookSlots;
            UnassignedBooks = unassignedBooks;
        }
    }

    public static class CharacterAttention
    {
        // Whether one rail section carries a pip. SKILLS never does: it lists
        // what a character can do and asks nothing of them.
        public static bool Lights(CharacterSection section, AttentionFacts facts)
        {
            switch (section)
            {
                case CharacterSection.Loadout:
                    return facts.CanHoldBooks && facts.EmptyBookSlots > 0 && facts.UnassignedBooks > 0;
                case CharacterSection.Attributes:
                    return facts.UnspentPoints > 0;
                case CharacterSection.RewardTrack:
                    return facts.RewardsWaiting > 0;
                case CharacterSection.Skills:
                    return false;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(section), section, null);
            }
        }

        // The strip chip's pip, and the map's: any section lit. Derived from
        // Lights, never restated, so a new section cannot light its row and
        // leave the chip dark.
        public static bool Any(AttentionFacts facts)
        {
            foreach (var section in CharacterSections.All)
            {
                if (Lights(section, facts)) return true;
            }

            return false;
        }

        // How many characters want looking at: the number the map states.
        public static int CountLit(IEnumerable<AttentionFacts> characters)
        {
            int lit = 0;
            if (characters == null) return lit;

            foreach (var facts in characters)
            {
                if (Any(facts)) lit++;
            }

            return lit;
        }
    }
}
