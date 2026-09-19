using System;

namespace PrincesPalace.Domain.Content
{
    // ONE LEVEL'S REWARD, exactly as typed into reward_tracks.json's
    // "levels" array. See docs/archive/PLAN_REWARD_TRACKS.md §4 for the authoring
    // format and RewardTrackEntryResolver for what each field is validated
    // against.
    //
    // EVERY LEVEL IS AUTHORED NOW. Until progression v2 phase 4 a track was
    // DESCRIBED rather than levelled: twelve milestone rows plus a "filler
    // mix" of (reward, amount, count) rows the game spread across the other
    // 87 levels by itself. That bought an author a short file and cost them
    // the ability to say what any one of those 87 levels pays -- which is
    // exactly what the 40-level track (PLAN_PROGRESSION_V2.md §4) is: a
    // table with a designed entry on every row, where "+5% Nature damage at
    // 7, 17 and 28" and "Slam +5 at 12 and 24" are placements, not counts.
    // A computed placement cannot express either, so the mix is gone and
    // with it the two rules that only existed to police it (a one-shot
    // capability may not be filler; a filler element must be one the
    // character can already deal). Both said the same thing -- "a level the
    // author did not choose must not carry a decision" -- and neither has
    // anything left to say now that the author chooses every level.
    [Serializable]
    public class RawTrackLevel
    {
        [ContentDoc("The level this entry pays, from 2 to RewardTrack.MaxLevel; every one of those levels must carry exactly one entry and no entry may name a level outside that range.")]
        public int level;

        [ContentDoc("Which TrackReward this level grants, matched case-insensitively against the enum member name.")]
        public string reward = "";

        [ContentDoc("The reward's magnitude -- a count for a grant (a stat point, max health), or an unlock's own parameter where it has one (SecondLife's charge count); 0 for an unlock with none (Respec).")]
        public int amount;

        [ContentDoc("The DamageType this reward is typed against, matched case-insensitively; only ElementalDamagePercent reads this, empty otherwise.")]
        public string against = "";

        [ContentDoc("The skill id this reward unlocks (UnlockSkill), or the one named skill a SkillCostDelta/SkillFlatDelta/SkillPowerDelta entry adjusts; empty otherwise.")]
        public string skillId = "";

        [ContentDoc("Which TrackResourceTarget a SkillCostDelta entry discounts, matched case-insensitively; only SkillCostDelta reads this, empty otherwise.")]
        public string resource = "";

        [ContentDoc("Which TrackIdentityKind an Identity entry carries, matched case-insensitively; only Identity reads this, empty otherwise.")]
        public string identityKind = "";

        [ContentDoc("The Identity entry's payload -- a title string for Title, or 'silver'/'gold' for PlateRim/PlateEmboss; unused by PortraitFrame/VictoryPose/Mastery and by every non-Identity reward.")]
        public string value = "";
    }

    // One character's reward track, exactly as typed into an element of
    // reward_tracks.json's top-level "tracks" array.
    [Serializable]
    public class RawRewardTrackEntry
    {
        [ContentDoc("The character this track belongs to; matches a characters.json id.")]
        public string characterId = "";

        [ContentDoc("One entry per level from 2 to RewardTrack.MaxLevel, in any order; see RawTrackLevel.")]
        public RawTrackLevel[] levels = Array.Empty<RawTrackLevel>();
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
