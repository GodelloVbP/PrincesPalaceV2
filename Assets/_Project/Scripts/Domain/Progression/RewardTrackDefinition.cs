using System.Collections.Generic;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Progression
{
    // A reward track MATERIALISED FOR ONE CHARACTER: the milestones and the
    // filler mix an author (or, for a character nobody has designed yet,
    // Default) described, computed once into a hundred entries.
    //
    // RewardTrack.cs keeps only the parts of a track that do NOT vary by
    // character -- the shared cadence, the state arithmetic; this class
    // holds the part that does, which reward sits at which level. See
    // docs/PLAN_REWARD_TRACKS.md §3 for the fuller rationale for the split.
    //
    // DESCRIBED, THEN DERIVED: twelve milestones and a filler mix are
    // written down, and the eighty-seven filler placements are computed by
    // InterleaveMix/Spread. Every entry is a TrackEntry rather than a bare
    // (TrackReward, int) tuple, because TrackEntry carries the two authored
    // selectors -- Against, SkillId -- a track needs beyond reward and
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

        // `milestones` names exactly the levels it wants to fill; every other
        // level from StartingLevel+1 upward is filler, and `fillerMix` is
        // spread across those levels in the order InterleaveMix computes.
        // Callers outside this file: the content resolver (docs/PLAN_REWARD_
        // TRACKS.md P4/P6) building an authored track from JSON, and Default
        // below, building the generated one.
        public static RewardTrackDefinition Build(string characterId,
            (int Level, TrackEntry Entry)[] milestones,
            (TrackEntry Entry, int Count)[] fillerMix)
        {
            var entries = new TrackEntry[RewardTrack.MaxLevel + 1];

            foreach (var milestone in milestones)
            {
                entries[milestone.Level] = milestone.Entry;
            }

            // Every level from 2 upward that no milestone claimed. Level 1 is
            // where a character starts, not somewhere they arrive.
            var fillerLevels = new int[RewardTrack.MaxLevel];
            int fillerCount = 0;
            for (int level = RewardTrack.StartingLevel + 1; level <= RewardTrack.MaxLevel; level++)
            {
                if (entries[level].Reward == TrackReward.None) fillerLevels[fillerCount++] = level;
            }

            var mixed = InterleaveMix(fillerMix);
            Spread(entries, fillerLevels, fillerCount, mixed);

            return new RewardTrackDefinition(characterId, entries);
        }

        // THE GENERATED DEFAULT, for a character with no authored track.
        // RewardTrackDefinitionTests.TheDefaultTrackPaysFiftyStatPoints-
        // AndTwoHundredTwentyNineMaxHealth pins the two totals (50 stat
        // points, 229 max health) this must keep producing. `characterId` is
        // accepted rather than ignored so a future per-character variant of
        // "no track authored" (there is none today) is one signature away
        // rather than a breaking change to every caller.
        //
        // THE SPINE -- Respec at 20, StatPoint 10 at 80, SecondLife at 90 --
        // is the same three levels on every track, authored or generated; the
        // other nine milestones fill with MaxHealth 15 so the track still pays
        // every level while a character has no flavour of their own yet.
        private static readonly (int Level, TrackEntry Entry)[] DefaultMilestones =
        {
            (10,  new TrackEntry(TrackReward.MaxHealth, 15)),
            (20,  new TrackEntry(TrackReward.Respec, 0)),
            (25,  new TrackEntry(TrackReward.MaxHealth, 15)),
            (30,  new TrackEntry(TrackReward.MaxHealth, 15)),
            (40,  new TrackEntry(TrackReward.MaxHealth, 15)),
            (45,  new TrackEntry(TrackReward.MaxHealth, 15)),
            (50,  new TrackEntry(TrackReward.MaxHealth, 15)),
            (60,  new TrackEntry(TrackReward.MaxHealth, 15)),
            (70,  new TrackEntry(TrackReward.MaxHealth, 15)),
            (80,  new TrackEntry(TrackReward.StatPoint, 10)),
            (90,  new TrackEntry(TrackReward.SecondLife, 1)),
            (100, new TrackEntry(TrackReward.MaxHealth, 15)),
        };

        private static readonly (TrackEntry Entry, int Count)[] DefaultFillerMix =
        {
            (new TrackEntry(TrackReward.StatPoint, 1), 40),
            (new TrackEntry(TrackReward.MaxHealth, 2), 47),
        };

        public static RewardTrackDefinition Default(string characterId) =>
            Build(characterId, DefaultMilestones, DefaultFillerMix);

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
            var milestones = new (int Level, TrackEntry Entry)[resolved.Milestones.Length];
            for (int i = 0; i < resolved.Milestones.Length; i++)
            {
                var m = resolved.Milestones[i];
                milestones[i] = (m.Level, new TrackEntry(m.Reward, m.Amount, m.Against, m.SkillId,
                    m.SkillDisplayName, m.ResourceDisplayName, m.Resource, m.IdentityKind, m.IdentityValue));
            }

            var fillerMix = new (TrackEntry Entry, int Count)[resolved.Filler.Length];
            for (int i = 0; i < resolved.Filler.Length; i++)
            {
                var f = resolved.Filler[i];
                fillerMix[i] = (new TrackEntry(f.Reward, f.Amount, f.Against, null, null,
                    f.ResourceDisplayName), f.Count);
            }

            return Build(resolved.CharacterId, milestones, fillerMix);
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

        // ---- construction helpers, moved verbatim from RewardTrack.cs -----------

        // The filler mix as an ORDER, with the kinds interleaved rather than
        // clumped. Largest-deficit selection, cross-multiplied to stay in
        // integers: at each step, hand the slot to whichever entry is furthest
        // behind the share its Count entitles it to. Ties go to declaration
        // order, so the sequence is fully determined by the table passed in.
        private static TrackEntry[] InterleaveMix((TrackEntry Entry, int Count)[] fillerMix)
        {
            int total = 0;
            foreach (var part in fillerMix) total += part.Count;

            var sequence = new TrackEntry[total];
            var emitted = new int[fillerMix.Length];

            for (int placed = 0; placed < total; placed++)
            {
                int best = -1;
                long bestDeficit = long.MinValue;

                for (int e = 0; e < fillerMix.Length; e++)
                {
                    if (emitted[e] >= fillerMix[e].Count) continue;

                    long deficit = (long)fillerMix[e].Count * (placed + 1) - (long)emitted[e] * total;
                    if (deficit > bestDeficit)
                    {
                        bestDeficit = deficit;
                        best = e;
                    }
                }

                if (best < 0) break;

                emitted[best]++;
                sequence[placed] = fillerMix[best].Entry;
            }

            return sequence;
        }

        // Places `mixed` evenly across the filler levels. Bresenham: slot i
        // takes the next reward when the running share crosses an integer
        // boundary -- see RewardTrack.cs's original comment for why this is
        // evenly spread rather than packed into the front.
        private static void Spread(TrackEntry[] entries, int[] fillerLevels, int fillerCount, TrackEntry[] mixed)
        {
            if (fillerCount <= 0 || mixed.Length == 0) return;

            int taken = 0;

            for (int i = 0; i < fillerCount && taken < mixed.Length; i++)
            {
                int before = i * mixed.Length / fillerCount;
                int after = (i + 1) * mixed.Length / fillerCount;

                if (after <= before) continue;

                entries[fillerLevels[i]] = mixed[taken++];
            }
        }
    }
}
