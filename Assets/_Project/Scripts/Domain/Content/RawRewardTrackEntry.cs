using System;

namespace PrincesPalace.Domain.Content
{
    // One milestone reward, exactly as typed into reward_tracks.json's
    // "milestones" array. See docs/PLAN_REWARD_TRACKS.md §4 for the
    // authoring format and RewardTrackEntryResolver for what each field is
    // validated against.
    [Serializable]
    public class RawTrackMilestone
    {
        [ContentDoc("The milestone level this entry lands on; must be one of the track's twelve fixed milestone levels (10, 20, 25, 30, 40, 45, 50, 60, 70, 80, 90, 100), and every one of the twelve must be named exactly once.")]
        public int level;

        [ContentDoc("Which TrackReward this milestone grants, matched case-insensitively against the enum member name.")]
        public string reward = "";

        [ContentDoc("The reward's magnitude -- a count for a grant (a stat point, max health), or an unlock's own parameter where it has one (SecondLife's charge count); 0 for an unlock with none (Respec).")]
        public int amount;

        [ContentDoc("The DamageType this reward is typed against, matched case-insensitively; only ElementalDamagePercent reads this, empty otherwise.")]
        public string against = "";

        [ContentDoc("The skill id this reward unlocks; only UnlockSkill reads this, empty otherwise.")]
        public string skillId = "";
    }

    // One filler mix row, exactly as typed into reward_tracks.json's
    // "filler" array -- a KIND and a COUNT, not a level. See
    // docs/PLAN_REWARD_TRACKS.md §4, "where a filler node actually lands",
    // for how a count becomes a placement.
    [Serializable]
    public class RawTrackFiller
    {
        [ContentDoc("Which TrackReward this filler row grants, matched case-insensitively against the enum member name. A one-shot capability (an unlock) is refused here -- filler may only be a grant.")]
        public string reward = "";

        [ContentDoc("The reward's magnitude, paid at every filler level this row places.")]
        public int amount;

        [ContentDoc("The DamageType this reward is typed against, matched case-insensitively; only ElementalDamagePercent reads this, empty otherwise.")]
        public string against = "";

        [ContentDoc("How many of the track's 87 filler levels this row occupies. Every row's count in a track must sum to exactly 87.")]
        public int count;
    }

    // One character's reward track, exactly as typed into an element of
    // reward_tracks.json's top-level "tracks" array.
    [Serializable]
    public class RawRewardTrackEntry
    {
        [ContentDoc("The character this track belongs to; matches a characters.json id.")]
        public string characterId = "";

        [ContentDoc("The track's twelve milestone rewards, one per fixed milestone level; see RawTrackMilestone.")]
        public RawTrackMilestone[] milestones = Array.Empty<RawTrackMilestone>();

        [ContentDoc("The track's filler reward mix, spread evenly across its 87 non-milestone levels; see RawTrackFiller.")]
        public RawTrackFiller[] filler = Array.Empty<RawTrackFiller>();
    }

    // JsonUtility cannot deserialize a bare top-level array, so
    // reward_tracks.json is one object with a "tracks" array inside it.
    [Serializable]
    public class RawRewardTrackFile
    {
        [ContentDoc("Every authored character track; see RawRewardTrackEntry.")]
        public RawRewardTrackEntry[] tracks = Array.Empty<RawRewardTrackEntry>();
    }
}
