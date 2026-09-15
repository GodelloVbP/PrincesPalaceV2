using System;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // One milestone entry, resolved: the reward's kind and magnitude plus
    // the two selectors and two captions RewardTrackEntryResolver bakes on
    // -- see docs/PLAN_REWARD_TRACKS.md §4, "the two display names the
    // captions need".
    [Serializable]
    public sealed class ResolvedTrackMilestone
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

        // Empty except for UnlockSkill.
        public string SkillId = "";
        public string SkillDisplayName = "";

        // Empty except for the five signature-resource kinds.
        public string ResourceDisplayName = "";

        // PHASE 3: which resource a SkillCostDelta entry discounts (empty
        // except for that one kind), and which cosmetic payload an Identity
        // entry carries (empty except for that one). Same bool-pair
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
        public ResolvedTrackMilestone()
        {
        }

        public ResolvedTrackMilestone(int level, TrackReward reward, int amount, DamageType? against,
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

    // One filler mix row, resolved -- a kind, a magnitude and a count, never
    // a level (see RawTrackFiller).
    [Serializable]
    public sealed class ResolvedTrackFiller
    {
        public TrackReward Reward;
        public int Amount;
        public DamageType AgainstType;
        public bool HasAgainst;
        public int Count;

        // Empty except for the four signature-resource kinds -- and three of
        // those four CAN be filler (SignatureAbsorbs is the one-shot, refused
        // by rule 3), so the caption's resource name has to ride here as well
        // as on a milestone. Shawn's twelve `+1 WOOL CAPACITY` filler nodes
        // are the case: without this they caption "+1 SIGNATURE CAPACITY",
        // RewardTrackNames' blank-name fallback rather than the character's
        // own word for the resource.
        public string ResourceDisplayName = "";

        public DamageType? Against => HasAgainst ? (DamageType?)AgainstType : null;

        // For the serializer only.
        public ResolvedTrackFiller()
        {
        }

        public ResolvedTrackFiller(TrackReward reward, int amount, DamageType? against, int count,
            string resourceDisplayName)
        {
            Reward = reward;
            Amount = amount;
            HasAgainst = against.HasValue;
            AgainstType = against ?? default;
            Count = count;
            ResourceDisplayName = resourceDisplayName ?? "";
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
        public ResolvedTrackMilestone[] Milestones = Array.Empty<ResolvedTrackMilestone>();
        public ResolvedTrackFiller[] Filler = Array.Empty<ResolvedTrackFiller>();

        // Listed by the character's own roster order -- see
        // RewardTrackDefinitionAsset.SortOrder and
        // docs/PLAN_REWARD_TRACKS.md §4's touch-point table.
        public int SortOrder;

        // For the serializer only.
        public ResolvedRewardTrack()
        {
        }

        public ResolvedRewardTrack(string characterId, ResolvedTrackMilestone[] milestones,
            ResolvedTrackFiller[] filler, int sortOrder)
        {
            CharacterId = characterId ?? "";
            Milestones = milestones ?? Array.Empty<ResolvedTrackMilestone>();
            Filler = filler ?? Array.Empty<ResolvedTrackFiller>();
            SortOrder = sortOrder;
        }
    }
}
