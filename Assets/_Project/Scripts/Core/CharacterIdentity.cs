using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace
{
    // ONE COLLECTED IDENTITY ITEM -- the level it was collected at (needed to
    // tell which Title is newest; Identity entries carry no Amount) plus the
    // kind and value TrackEntry already carries.
    public readonly struct CharacterIdentityItem
    {
        public readonly int Level;
        public readonly TrackIdentityKind Kind;
        public readonly string Value;

        public CharacterIdentityItem(int level, TrackIdentityKind kind, string value)
        {
            Level = level;
            Kind = kind;
            Value = value ?? "";
        }
    }

    // PHASE 3's read model for a character's collected Identity nodes
    // (docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §7 phase 3
    // package 1: "expose a small read model"). No rendering yet -- phase 5
    // is where a dossier/roster/victory screen actually draws any of this.
    //
    // Core rather than Domain, same reason RewardTracks itself is Core: it
    // needs RewardTracks.For(character) to find the track at all
    // (CODE_STANDARDS §1, content-layer concepts cannot reach Domain).
    // Domain owns the ARITHMETIC (RewardTrackDefinition.CollectedIdentity);
    // this owns only "whose track, and which title is shown".
    public static class CharacterIdentity
    {
        // Every Identity item this character has collected, oldest first --
        // read live off claimedTrackLevel, the same "nothing but the one
        // grant is stored" posture every other reward track total already
        // follows (docs/PLAN_REWARD_TRACKS.md §2).
        public static IReadOnlyList<CharacterIdentityItem> CollectedFor(Character character)
        {
            if (character == null) return System.Array.Empty<CharacterIdentityItem>();

            var track = RewardTracks.For(character);
            return track.CollectedIdentity(character.claimedTrackLevel)
                .Where(pair => pair.Entry.IdentityKind.HasValue)
                .Select(pair => new CharacterIdentityItem(pair.Level, pair.Entry.IdentityKind.Value, pair.Entry.IdentityValue))
                .ToList();
        }

        // The title shown right now -- the character's own selectedTitleTrackLevel
        // if it still names a collected Title, else the newest collected
        // Title, else null (nothing collected yet). Defaulting to newest
        // matches the plan's own words: "the newest is shown, earlier ones
        // selectable in the hub roster".
        public static CharacterIdentityItem? SelectedTitleFor(Character character)
        {
            if (character == null) return null;

            var titles = CollectedFor(character).Where(item => item.Kind == TrackIdentityKind.Title).ToList();
            if (titles.Count == 0) return null;

            if (character.selectedTitleTrackLevel > 0)
            {
                foreach (var title in titles)
                {
                    if (title.Level == character.selectedTitleTrackLevel) return title;
                }
            }

            // Newest = collected at the highest level. CollectedFor walks
            // levels ascending, so the last Title in the list is the newest.
            return titles[titles.Count - 1];
        }
    }
}
