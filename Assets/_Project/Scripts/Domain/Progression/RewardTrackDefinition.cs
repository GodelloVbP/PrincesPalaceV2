using System.Collections.Generic;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Progression
{
    // A reward track MATERIALISED FOR ONE CHARACTER: the level table an
    // author (or, for a character nobody has designed yet, Default) wrote
    // down, indexed for reading.
    //
    // RewardTrack.cs keeps only the parts of a track that do NOT vary by
    // character -- the shared cadence, the state arithmetic; this class
    // holds the part that does, which reward sits at which level. See
    // docs/PLAN_REWARD_TRACKS.md §3 for the fuller rationale for the split.
    //
    // AUTHORED, NOT DERIVED, since progression v2 phase 4. A track used to
    // be DESCRIBED -- ten milestones plus a "filler mix" of (reward,
    // amount, count) rows that InterleaveMix/Spread laid across the levels
    // nobody had named -- and both of those are gone with the mix: the
    // 40-level table (PLAN_PROGRESSION_V2.md §4) names a reward at every
    // level from 2 to MaxLevel, so there is nothing left to compute a
    // placement for and no second way for a level to acquire a reward. See
    // RawTrackLevel's own header for what the old shape could not express.
    // Every entry is a TrackEntry rather than a bare (TrackReward, int)
    // tuple, because TrackEntry carries the authored selectors -- Against,
    // SkillId, Resource, IdentityKind -- a track needs beyond reward and
    // amount.
    public sealed class RewardTrackDefinition
    {
        public readonly string CharacterId;

        private readonly TrackEntry[] _entries;

        private RewardTrackDefinition(string characterId, TrackEntry[] entries)
        {
            CharacterId = characterId;
            _entries = entries;
        }

        // ---- construction --------------------------------------------------

        // `levels` names a reward for each level it fills. A level nobody
        // names reads as TrackReward.None -- which an AUTHORED track can no
        // longer contain (RewardTrackEntryResolver refuses a file with a
        // gap) but a hand-built fixture deliberately can, so this stays
        // tolerant rather than throwing.
        //
        // Callers outside this file: the content resolver (via From below)
        // building an authored track from JSON, Default below, and the
        // fixtures in RewardTrackDefinitionTests / RewardTrackNodeValidationTests.
        public static RewardTrackDefinition Build(string characterId,
            (int Level, TrackEntry Entry)[] levels)
        {
            var entries = new TrackEntry[RewardTrack.MaxLevel + 1];

            foreach (var level in levels)
            {
                if (level.Level < RewardTrack.StartingLevel || level.Level > RewardTrack.MaxLevel) continue;
                entries[level.Level] = level.Entry;
            }

            return new RewardTrackDefinition(characterId, entries);
        }

        // THE GENERATED DEFAULT, for a character with no authored track.
        // RewardTrackDefinitionTests pins the two totals it must keep
        // producing. `characterId` is accepted rather than ignored so a
        // future per-character variant of "no track authored" (there is
        // none today) is one signature away rather than a breaking change
        // to every caller.
        //
        // WRITTEN OUT IN FULL, one row per level, since progression v2
        // phase 4 retired the filler mix. It pays only what a character
        // nobody has designed can certainly receive: health, stat points,
        // the two utilities, and the identity stretch. No signature reward
        // (the character may have no signature resource), no mana reward
        // (their primary pool may refuse mana), no UnlockSkill (there is no
        // skill to name), no elemental percentage (there is no attackType
        // to key it to) -- exactly the four gaps bear's authored track had
        // to work around before it existed, and the reason this is a
        // placeholder rather than a template.
        //
        // IT OBEYS THE NODE RULES AS WELL AS THE FORMAT. The kinds
        // alternate Bump/Choice across the combat stretch so no two
        // neighbours share one, the two utilities sit at 8 and 25 where the
        // real tracks put them, and 31-40 are Identity. Nothing calls
        // RewardTrackNodeValidation on it today (it never passes through
        // the resolver), but a default that could not survive the
        // validator would be a trap for whoever first authors a track by
        // copying it.
        //
        // Every character on the roster has a track, so this is reached
        // only by an id no row names -- it is still the rule, not
        // scaffolding.
        private static readonly (int Level, TrackEntry Entry)[] DefaultLevels =
        {
            (2,  new TrackEntry(TrackReward.MaxHealth, 30)),
            (3,  new TrackEntry(TrackReward.StatPoint, 4)),
            (4,  new TrackEntry(TrackReward.MaxHealth, 30)),
            (5,  new TrackEntry(TrackReward.StatPoint, 4)),
            (6,  new TrackEntry(TrackReward.MaxHealth, 30)),
            (7,  new TrackEntry(TrackReward.StatPoint, 4)),
            (8,  new TrackEntry(TrackReward.Respec, 0)),
            (9,  new TrackEntry(TrackReward.StatPoint, 4)),
            (10, new TrackEntry(TrackReward.MaxHealth, 30)),
            (11, new TrackEntry(TrackReward.StatPoint, 4)),
            (12, new TrackEntry(TrackReward.MaxHealth, 30)),
            (13, new TrackEntry(TrackReward.StatPoint, 4)),
            (14, new TrackEntry(TrackReward.MaxHealth, 30)),
            (15, new TrackEntry(TrackReward.StatPoint, 4)),
            (16, new TrackEntry(TrackReward.MaxHealth, 30)),
            (17, new TrackEntry(TrackReward.StatPoint, 4)),
            (18, new TrackEntry(TrackReward.MaxHealth, 30)),
            (19, new TrackEntry(TrackReward.StatPoint, 4)),
            (20, new TrackEntry(TrackReward.MaxHealth, 30)),
            (21, new TrackEntry(TrackReward.StatPoint, 4)),
            (22, new TrackEntry(TrackReward.MaxHealth, 30)),
            (23, new TrackEntry(TrackReward.StatPoint, 4)),
            (24, new TrackEntry(TrackReward.MaxHealth, 30)),
            (25, new TrackEntry(TrackReward.SecondLife, 1)),
            (26, new TrackEntry(TrackReward.MaxHealth, 30)),
            (27, new TrackEntry(TrackReward.StatPoint, 4)),
            (28, new TrackEntry(TrackReward.MaxHealth, 30)),
            (29, new TrackEntry(TrackReward.StatPoint, 4)),
            (30, new TrackEntry(TrackReward.MaxHealth, 30)),

            (31, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.Title, identityValue: "Contractor")),
            (32, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.PlateRim, identityValue: "silver")),
            (33, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.Title, identityValue: "Champion")),
            (34, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.PortraitFrame)),
            (35, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.PlateEmboss, identityValue: "silver")),
            (36, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.Title, identityValue: "Veteran")),
            (37, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.VictoryPose)),
            (38, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.PlateRim, identityValue: "gold")),
            (39, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.Title, identityValue: "Legend")),
            (40, new TrackEntry(TrackReward.Identity, 0, identityKind: TrackIdentityKind.Mastery)),
        };

        public static RewardTrackDefinition Default(string characterId) =>
            Build(characterId, DefaultLevels);

        // THE BRIDGE FROM CONTENT. ResolvedRewardTrack (Domain/Content) is
        // what RewardTrackEntryResolver hands back after validating and
        // captioning reward_tracks.json; this turns it into the same
        // materialised, hundred-entry shape Default produces, so a caller
        // (RewardTrackContentPinTests, Core/RewardTracks.For) can read an
        // authored track through the identical At/CollectedTotal/HasUnlocked
        // API regardless of whether it came from JSON or from Default. Kept
        // in Domain, deliberately -- Core/RewardTracks.For takes this one
        // call rather than reimplementing the conversion.
        public static RewardTrackDefinition From(ResolvedRewardTrack resolved)
        {
            var levels = new (int Level, TrackEntry Entry)[resolved.Levels.Length];
            for (int i = 0; i < resolved.Levels.Length; i++)
            {
                var row = resolved.Levels[i];
                levels[i] = (row.Level, new TrackEntry(row.Reward, row.Amount, row.Against, row.SkillId,
                    row.SkillDisplayName, row.ResourceDisplayName, row.Resource, row.IdentityKind, row.IdentityValue));
            }

            return Build(resolved.CharacterId, levels);
        }

        // ---- reads -----------------------------------------------------------

        // What reaching `level` hands over.
        public TrackEntry At(int level)
        {
            if (level < RewardTrack.StartingLevel || level > RewardTrack.MaxLevel)
            {
                return new TrackEntry(TrackReward.None, 0);
            }

            return _entries[level];
        }

        // The next level at or after `level` + 1 that pays anything, or 0 when
        // the track has nothing left.
        public int NextRewardLevel(int level)
        {
            int from = level < RewardTrack.StartingLevel ? RewardTrack.StartingLevel : level;

            for (int next = from + 1; next <= RewardTrack.MaxLevel; next++)
            {
                if (_entries[next].IsSomething) return next;
            }

            return 0;
        }

        // How much of `reward` the track owes for levels in
        // (afterLevel, throughLevel] -- half-open at the bottom, the same
        // watermark semantics the static table used. THE ONE GRANT IS
        // StatPoint (RewardTrack.IsGrant), so this is 0 for every other kind;
        // everything else is read through CollectedTotal instead, at its own
        // site, per docs/PLAN_REWARD_TRACKS.md §2.
        public int GrantedBetween(TrackReward reward, int afterLevel, int throughLevel)
        {
            if (!RewardTrack.IsGrant(reward)) return 0;

            if (afterLevel < RewardTrack.StartingLevel) afterLevel = RewardTrack.StartingLevel;
            return SumOver(reward, afterLevel, throughLevel);
        }

        // Every Amount for `reward` at levels <= claimedLevel, added together
        // -- READ LIVE, not accumulated into a save field. §2's model: a
        // stored copy can disagree with the definition after a retune, so
        // everything but the one grant is answered by walking the track fresh
        // each time.
        public int CollectedTotal(TrackReward reward, int claimedLevel) =>
            SumOver(reward, RewardTrack.StartingLevel - 1, claimedLevel);

        // The same, narrowed to entries authored against a specific element --
        // ElementalDamagePercent's read site, ContentDatabase.ModifierEffects,
        // asks this once per DamageType so a Fire node and an Ice node on the
        // same track never bleed into each other's total.
        public int CollectedTotal(TrackReward reward, DamageType against, int claimedLevel)
        {
            int throughLevel = claimedLevel > RewardTrack.MaxLevel ? RewardTrack.MaxLevel : claimedLevel;

            int total = 0;
            for (int level = RewardTrack.StartingLevel; level <= throughLevel; level++)
            {
                var entry = _entries[level];
                if (entry.Reward == reward && entry.Against == against) total += entry.Amount;
            }

            return total;
        }

        // Every ElementalDamagePercent total collected through claimedLevel,
        // grouped by which element it was authored against -- one pass over
        // the track rather than CollectedTotal(reward, against, level)
        // called once per DamageType member. ContentDatabase.Effective's
        // ModifierEffects is why this exists: it needs every element's total
        // at once, and walking the track eleven times (once per DamageType,
        // via Enum.GetValues -- itself a fresh array on every call) to
        // answer that was the eleven-times-slower way to ask a
        // single-pass question.
        public IReadOnlyDictionary<DamageType, int> CollectedElementalTotals(int claimedLevel)
        {
            int throughLevel = claimedLevel > RewardTrack.MaxLevel ? RewardTrack.MaxLevel : claimedLevel;
            var totals = new Dictionary<DamageType, int>();

            for (int level = RewardTrack.StartingLevel; level <= throughLevel; level++)
            {
                var entry = _entries[level];
                if (entry.Reward != TrackReward.ElementalDamagePercent || !entry.Against.HasValue) continue;

                totals.TryGetValue(entry.Against.Value, out int running);
                totals[entry.Against.Value] = running + entry.Amount;
            }

            return totals;
        }

        // Every skill id an UnlockSkill entry has handed over at or below
        // claimedLevel. ContentDatabase.AvailableSkillsFor's fifth route.
        //
        // EMPTY, NEVER NULL, on a track with nothing to give -- a fresh
        // Character has claimedTrackLevel 0, and SkillUnlockFilterTests'
        // TheSharedFunctionAgreesWithAvailableSkillsForAFreshLevelOneCharacter
        // rests on that returning nothing rather than throwing.
        public IReadOnlyList<string> SkillsCollected(int claimedLevel)
        {
            int throughLevel = claimedLevel > RewardTrack.MaxLevel ? RewardTrack.MaxLevel : claimedLevel;
            var skills = new List<string>();

            for (int level = RewardTrack.StartingLevel; level <= throughLevel; level++)
            {
                var entry = _entries[level];
                if (entry.Reward == TrackReward.UnlockSkill && !string.IsNullOrEmpty(entry.SkillId))
                {
                    skills.Add(entry.SkillId);
                }
            }

            return skills;
        }

        // PHASE 3: how much of a SkillCostDelta entry naming `skillId` and
        // `resource` has been collected -- SUMMED, unlike the SET-style
        // reads UnlockedAmount answers (see TrackReward.SkillCostDelta's own
        // header for why this one sums while FuryGainOnAttack does not).
        public int CollectedSkillCostDelta(string skillId, TrackResourceTarget resource, int claimedLevel)
        {
            int throughLevel = claimedLevel > RewardTrack.MaxLevel ? RewardTrack.MaxLevel : claimedLevel;

            int total = 0;
            for (int level = RewardTrack.StartingLevel; level <= throughLevel; level++)
            {
                var entry = _entries[level];
                if (entry.Reward == TrackReward.SkillCostDelta && entry.SkillId == skillId && entry.Resource == resource)
                {
                    total += entry.Amount;
                }
            }

            return total;
        }

        // PHASE 3: how much of a SkillFlatDelta entry naming `skillId` has
        // been collected -- SUMMED (Bjorn's Slam: two +5 nodes read as +10).
        public int CollectedSkillFlatDelta(string skillId, int claimedLevel)
        {
            int throughLevel = claimedLevel > RewardTrack.MaxLevel ? RewardTrack.MaxLevel : claimedLevel;

            int total = 0;
            for (int level = RewardTrack.StartingLevel; level <= throughLevel; level++)
            {
                var entry = _entries[level];
                if (entry.Reward == TrackReward.SkillFlatDelta && entry.SkillId == skillId)
                {
                    total += entry.Amount;
                }
            }

            return total;
        }

        // PHASE 4: how much of a SkillPowerDelta entry naming `skillId` has
        // been collected -- SUMMED, the same reading as SkillFlatDelta above
        // and for the same reason (two nodes on one skill are two steps of
        // one number, not two competing totals).
        public int CollectedSkillPowerDelta(string skillId, int claimedLevel)
        {
            int throughLevel = claimedLevel > RewardTrack.MaxLevel ? RewardTrack.MaxLevel : claimedLevel;

            int total = 0;
            for (int level = RewardTrack.StartingLevel; level <= throughLevel; level++)
            {
                var entry = _entries[level];
                if (entry.Reward == TrackReward.SkillPowerDelta && entry.SkillId == skillId)
                {
                    total += entry.Amount;
                }
            }

            return total;
        }

        // PHASE 3: every Identity entry collected at or below claimedLevel,
        // oldest first -- Core.CharacterIdentity's whole input. Level travels
        // with each entry because "which title is newest" is a question about
        // LEVEL, not about Amount (Identity entries carry no Amount at all).
        public IReadOnlyList<(int Level, TrackEntry Entry)> CollectedIdentity(int claimedLevel)
        {
            int throughLevel = claimedLevel > RewardTrack.MaxLevel ? RewardTrack.MaxLevel : claimedLevel;
            var items = new List<(int Level, TrackEntry Entry)>();

            for (int level = RewardTrack.StartingLevel; level <= throughLevel; level++)
            {
                var entry = _entries[level];
                if (entry.Reward == TrackReward.Identity) items.Add((level, entry));
            }

            return items;
        }

        // Every level from StartingLevel+1 to MaxLevel, paired with what it
        // pays -- RewardTrackNodeValidation's whole input. Not exposed as
        // the raw array: a caller gets read-only pairs rather than a way to
        // reach past MaxLevel or misread an index as a level.
        public IEnumerable<(int Level, TrackEntry Entry)> AllLevels()
        {
            for (int level = RewardTrack.StartingLevel + 1; level <= RewardTrack.MaxLevel; level++)
            {
                yield return (level, _entries[level]);
            }
        }

        // The first level that grants `reward`, or 0 if this track never does.
        public int UnlockLevel(TrackReward reward)
        {
            for (int level = RewardTrack.StartingLevel; level <= RewardTrack.MaxLevel; level++)
            {
                if (_entries[level].Reward == reward) return level;
            }

            return 0;
        }

        // Whether a character at `level` has this capability -- the only
        // question a pure on/off unlock (Respec, SecondLife) ever needs to
        // answer, and it needs no storage to do it.
        public bool HasUnlocked(TrackReward reward, int level)
        {
            if (!RewardTrack.IsOneShotCapability(reward)) return false;

            int unlockedAt = UnlockLevel(reward);
            return unlockedAt > 0 && level >= unlockedAt;
        }

        // The highest Amount reached for an unlock that comes in steps, or
        // `fallback` if none has -- SquadTrack.SecondLivesLeft's "how many
        // charges has this squad earned" question.
        public int UnlockedAmount(TrackReward reward, int level, int fallback)
        {
            int best = fallback;

            for (int l = RewardTrack.StartingLevel; l <= RewardTrack.MaxLevel && l <= level; l++)
            {
                var entry = _entries[l];
                if (entry.Reward == reward && entry.Amount > best) best = entry.Amount;
            }

            return best;
        }

        // Adds up `reward` over the levels in (afterLevel, throughLevel].
        // One loop for GrantedBetween and CollectedTotal, which differ only in
        // their bounds and in whether the caller has already gated on
        // IsGrant -- the arithmetic itself does not care which reward kind it
        // is summing.
        private int SumOver(TrackReward reward, int afterLevel, int throughLevel)
        {
            if (throughLevel > RewardTrack.MaxLevel) throughLevel = RewardTrack.MaxLevel;
            if (afterLevel < RewardTrack.StartingLevel - 1) afterLevel = RewardTrack.StartingLevel - 1;

            int total = 0;
            for (int level = afterLevel + 1; level <= throughLevel; level++)
            {
                var entry = _entries[level];
                if (entry.Reward == reward) total += entry.Amount;
            }

            return total;
        }

        // InterleaveMix AND Spread ARE GONE (progression v2 phase 4). They
        // were the whole of "described, then derived": a largest-deficit
        // interleave over the filler mix, then a Bresenham spread across
        // whichever levels no milestone had claimed. Nothing computes a
        // placement any more -- every level names its own reward, including
        // DefaultLevels above -- so keeping them would have left two ways
        // for a level to acquire a reward and only one of them reachable.
    }
}
