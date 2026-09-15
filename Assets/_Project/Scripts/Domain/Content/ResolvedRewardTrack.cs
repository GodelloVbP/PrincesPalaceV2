using System;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // One LEVEL's reward, resolved: the reward's kind and magnitude plus the
    // selectors and the two captions RewardTrackEntryResolver bakes on --
    // see docs/PLAN_REWARD_TRACKS.md §4, "the two display names the captions
    // need".
    //
    // ONE RECORD FOR EVERY LEVEL. There used to be two -- a milestone
    // (which carried a level) and a filler row (which carried a count and
    // let the game pick the level) -- and progression v2 phase 4 retired the
    // second when every level became authored; see RawTrackLevel's own
    // header for why a computed placement could not express the 40-level
    // table.
    [Serializable]
    public sealed class ResolvedTrackLevel
    {
        public int Level;
        public TrackReward Reward;
        public int Amount;

        // THE PAIR THAT REPLACES A NULLABLE. Unity does not serialize
        // DamageType?, and Physical has a valid zero -- the same trade
        // ResolvedSkill's Status/HasStatus pair already makes. Against below
        // is the nullable reading every consumer should use.
        public DamageType AgainstType;
        public bool HasAgainst;

        // Empty except for UnlockSkill and the three Skill*Delta kinds.
        public string SkillId = "";
        public string SkillDisplayName = "";

        // Empty except for the five signature-resource kinds.
        public string ResourceDisplayName = "";

        // Which resource a SkillCostDelta entry discounts (empty except for
        // that one kind), and which cosmetic payload an Identity entry
        // carries (empty except for that one). Same bool-pair
        // nullable-replacement as Against/HasAgainst above -- Unity does not
        // serialize TrackResourceTarget?/TrackIdentityKind?, and both enums
        // have a valid zero member.
        public TrackResourceTarget ResourceType;
        public bool HasResource;
        public TrackIdentityKind IdentityKindType;
        public bool HasIdentityKind;
        public string IdentityValue = "";

        public DamageType? Against => HasAgainst ? (DamageType?)AgainstType : null;
        public TrackResourceTarget? Resource => HasResource ? (TrackResourceTarget?)ResourceType : null;
        public TrackIdentityKind? IdentityKind => HasIdentityKind ? (TrackIdentityKind?)IdentityKindType : null;

        // For the serializer only.
        public ResolvedTrackLevel()
        {
        }

        public ResolvedTrackLevel(int level, TrackReward reward, int amount, DamageType? against,
            string skillId, string skillDisplayName, string resourceDisplayName,
            TrackResourceTarget? resource = null, TrackIdentityKind? identityKind = null, string identityValue = null)
        {
            Level = level;
            Reward = reward;
            Amount = amount;
            HasAgainst = against.HasValue;
            AgainstType = against ?? default;
            SkillId = skillId ?? "";
            SkillDisplayName = skillDisplayName ?? "";
            ResourceDisplayName = resourceDisplayName ?? "";
            HasResource = resource.HasValue;
            ResourceType = resource ?? default;
            HasIdentityKind = identityKind.HasValue;
            IdentityKindType = identityKind ?? default;
            IdentityValue = identityValue ?? "";
        }
    }

    // One character's reward track, resolved -- the shape
    // RewardTrackDefinitionAsset stores.
    //
    // DELIBERATELY NOT `TrackEntry`/`RewardTrack` from Domain/Progression --
    // this record shares only TrackReward (the enum) with that package, not
    // its entry struct or its arithmetic. RewardTrackDefinition.From is what
    // turns this into a RewardTrackDefinition, the shape every read site
    // actually walks; ContentDatabase.Validation also reads it directly,
    // for the loaded-catalogue checks that must catch a hand-edited asset a
    // resolver never saw.
    [Serializable]
    public sealed class ResolvedRewardTrack
    {
        public string CharacterId = "";
        public ResolvedTrackLevel[] Levels = Array.Empty<ResolvedTrackLevel>();

        // Listed by the character's own roster order -- see
        // RewardTrackDefinitionAsset.SortOrder and
        // docs/PLAN_REWARD_TRACKS.md §4's touch-point table.
        public int SortOrder;

        // For the serializer only.
        public ResolvedRewardTrack()
        {
        }

        public ResolvedRewardTrack(string characterId, ResolvedTrackLevel[] levels, int sortOrder)
        {
            CharacterId = characterId ?? "";
            Levels = levels ?? Array.Empty<ResolvedTrackLevel>();
            SortOrder = sortOrder;
        }
    }
}
