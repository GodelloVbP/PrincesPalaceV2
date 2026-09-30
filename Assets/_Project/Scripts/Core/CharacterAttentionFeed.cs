using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Reads the plain values CharacterAttention decides on (plan C20) off the
    // save and the run. The rule lives in Domain; this is the only place that
    // knows where each value is kept, so the strip, the rail and the map's
    // hint cannot disagree about what a character is owed.
    public static class CharacterAttentionFeed
    {
        // `run` is null at camp, where no book exists and none can be placed.
        public static AttentionFacts For(Character character, RunSnapshot run)
        {
            if (character == null) return default;

            return new AttentionFacts(
                character.unspentStatPoints,
                RewardTrack.UnclaimedCount(character.level, character.claimedTrackLevel),
                ContentDatabase.CanHoldSpellBooks(character.definitionId),
                EmptyBookSlots(run, character.definitionId),
                run?.unassignedSpellBooks?.Count ?? 0);
        }

        // How many characters want looking at, over the whole roster: the
        // strip pips the same set, so the map's number is the number of pips.
        public static int CountLit(SaveData save, RunSnapshot run)
        {
            if (save?.roster == null) return 0;

            return CharacterAttention.CountLit(save.roster.Where(c => c != null).Select(c => For(c, run)));
        }

        private static int EmptyBookSlots(RunSnapshot run, string characterId)
        {
            var taken = new HashSet<int>();
            if (run?.learnedSpells != null)
            {
                foreach (var entry in run.learnedSpells)
                {
                    if (entry != null && entry.characterId == characterId) taken.Add(entry.slot);
                }
            }

            return System.Math.Max(0, SpellBooks.MaxSpellSlots - taken.Count);
        }
    }
}
