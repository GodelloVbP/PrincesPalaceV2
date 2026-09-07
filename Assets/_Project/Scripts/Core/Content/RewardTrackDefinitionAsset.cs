using PrincesPalace.Domain.Content;
using UnityEngine;

namespace PrincesPalace.Content
{
    // A character's reward track: what levelling that character up to 100
    // actually hands over. Authored in
    // Assets/_Project/ContentData/reward_tracks.json and generated into an
    // asset by ContentBuilder -- never hand-edited under Resources/Content,
    // which ContentBuilder deletes wholesale on every build.
    //
    // READ IN EXACTLY ONE PLACE, Core.RewardTracks.For, which converts the
    // asset matching a character into the RewardTrackDefinition every read
    // site (max health, wool, elemental damage, the respec, the second life,
    // an unlocked spell) sums against their claimedTrackLevel. Nothing else
    // touches ContentDatabase.RewardTrackAssets, and nothing should: the
    // resolved record is a serialisation shape, not the thing that knows how
    // to read itself.
    //
    // Same shape as RelicDefinition: [SerializeField] private data with an
    // Editor-only setter, so `definition.Data.CharacterId = "x"` still
    // compiles (field writes, not whole-record replacement, are what stays
    // open -- see RelicDefinition's own comment) but nothing outside the
    // Editor assembly can swap the record itself.
    public class RewardTrackDefinitionAsset : ScriptableObject, IOrderedContent
    {
        [SerializeField] private ResolvedRewardTrack data = new ResolvedRewardTrack();

        public ResolvedRewardTrack Data => data;

        // Editor-only, and the only assignment to `data` anywhere.
        internal void SetData(ResolvedRewardTrack value) => data = value ?? new ResolvedRewardTrack();

        public string characterId => data != null ? data.CharacterId : "";

        // Listed by the character's own roster order (ResolvedCharacter.SortOrder,
        // captured onto this record by ContentBuilder.BuildRewardTracks),
        // not by any order authored in reward_tracks.json itself -- see
        // docs/PLAN_REWARD_TRACKS.md §4's touch-point table.
        public int SortOrder => data != null ? data.SortOrder : 0;
    }
}
