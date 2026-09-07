using System.Collections.Generic;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Progression
{
    // A reward track MATERIALISED FOR ONE CHARACTER: the milestones and the
    // filler mix an author (or, for a character nobody has designed yet,
    // Default) described, computed once into a hundred entries.
    //
    // docs/PLAN_REWARD_TRACKS.md P3. This is what RewardTrack.cs's static
    // table used to be, generalised so a track can differ per character --
    // RewardTrack.cs itself keeps only the parts that do NOT vary by
    // character (the shared cadence, the state arithmetic) and this class
    // holds the part that does (which reward sits at which level).
    //
    // DESCRIBED, THEN DERIVED, same construction as the static table it
    // replaces: twelve milestones and a filler mix are written down, and the
    // eighty-seven filler placements are computed by InterleaveMix/Spread,
    // moved here VERBATIM from RewardTrack.cs (CODE_STANDARDS.md's "no
    // renames during a pure-move split") -- only their parameter shapes
    // changed, from (TrackReward, int) tuples to TrackEntry, because TrackEntry
    // itself grew the two authored selectors (Against, SkillId) this package
    // adds.
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
        //
        // This is RewardTrack.cs's own interim table (docs/PLAN_REWARD_
        // TRACKS.md P1), moved here verbatim rather than retyped: P1's own
        // comment already said this table IS the eventual generated default,
        // and RewardTrackDefinitionTests.TheDefaultTrackPaysFiftyStatPoints-
        // AndTwoHundredTwentyNineMaxHealth pins the two totals (50 stat
        // points, 229 max health) this must keep producing. `characterId` is
        // accepted rather
        // than ignored so a future per-character variant of "no track
        // authored" (there is none today) is one signature away rather than a
        // breaking change to every caller.
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
            if (!RewardTrack.IsUnlock(reward)) return false;

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
