using System;

namespace PrincesPalace.Domain.Progression
{
    // What each level hands over.
    //
    // Two categories, and the split is the load-bearing idea here rather than
    // a tidiness one -- see RewardTrack's header for why.
    public enum TrackReward
    {
        // Nothing authored at this level yet. Not an error: the filler mix is
        // smaller than the number of filler levels until the rest of the
        // reward kinds exist to fill it.
        None = 0,

        // ---- GRANTS: a quantity, handed over once, accumulated on the save --
        StatPoint,
        MaxHealth,

        // ---- UNLOCKS: a capability, true forever once the level is reached --
        Respec,
        SecondLife,
    }

    // One level's worth of reward.
    public readonly struct TrackEntry
    {
        public readonly TrackReward Reward;

        // What the reward is worth. A count for grants (one stat point, two
        // max health), and for unlocks the PARAMETER of the capability where
        // it has one -- SecondLife 1 means "one charge". Zero where the
        // capability has no number.
        public readonly int Amount;

        public TrackEntry(TrackReward reward, int amount)
        {
            Reward = reward;
            Amount = amount;
        }

        public bool IsSomething => Reward != TrackReward.None;
    }

    // The level reward track: reach a level, get the reward.
    //
    // DESCRIBED, THEN DERIVED, the same construction TalentSkeleton uses and
    // for the same reason. The milestones are written down once, the filler is
    // written down once as a mix, and the hundred entries are computed from
    // those two statements. A hundred hand-authored rows is a table nothing
    // ties together: a level moved in one place and not another would not fail
    // to compile and would not look wrong.
    //
    // GRANTS AND UNLOCKS ARE DIFFERENT THINGS, and keeping them apart removes a
    // whole class of bug rather than merely organising the enum.
    //
    //   A GRANT is a quantity -- a stat point, two max health. It has to be
    //   handed over exactly once, so it needs a watermark on the save
    //   (Character.claimedTrackLevel) recording how far the track has paid out.
    //
    //   An UNLOCK is a capability -- respec, a second life. It is a pure
    //   function of the level reached and is stored NOWHERE. Asking "is this
    //   unlocked" is asking "is level >= the level that grants it".
    //
    // The reason that matters: an unlock can be a system that does not exist
    // yet. If it were claimed once against a watermark, every character who
    // passed its level before it was built would have spent their claim on a
    // no-op and never get it. As a pure function of level, there is nothing
    // to miss -- the capability simply starts working the day it is
    // implemented, for everyone who has the level.
    //
    // Engine-free, so the whole table is pinnable from EditMode.
    public static class RewardTrack
    {
        // Where the track starts and ends. A character begins AT level 1, so
        // level 1 is not a reward -- the first thing the track pays is level 2.
        public const int StartingLevel = 1;
        public const int MaxLevel = 100;

        // THE MILESTONES. Tens, twelve of them.
        //
        // AN INTERIM TABLE, DELIBERATELY. docs/PLAN_REWARD_TRACKS.md P1 retired
        // eight over-arching reward kinds -- Favor, ExpFind, StartingRelics,
        // RestBeforeBoss, OfferReroll, WiderOffer, ChosenStartingRelics,
        // SecondLifeRefresh -- and the nine milestones that carried them are
        // filled with MaxHealth 15 rather than left empty, so the track still
        // pays every level while per-character content (P2-P7 of that plan)
        // is still to come. THIS TABLE IS THE EVENTUAL GENERATED DEFAULT: P4
        // lifts it verbatim into RewardTrackDefinition.Default, so the two
        // totals it produces -- 50 stat points, 229 max health -- are pinned
        // by RewardTrackTests.TheTrackStillPaysFiftyStatPointsAndTwoHundred-
        // TwentyNineMaxHealth and must not drift by accident.
        //
        // THE SPINE IS THE PART THAT IS NOT INTERIM: Respec at 20, StatPoint
        // at 80, SecondLife at 90 are the same three levels on every track,
        // authored or generated (see the design handoff's own reasoning for
        // why those three levels specifically should not be trivia).
        private static readonly (int Level, TrackReward Reward, int Amount)[] Milestones =
        {
            (10,  TrackReward.MaxHealth, 15),
            (20,  TrackReward.Respec, 0),
            (25,  TrackReward.MaxHealth, 15),
            (30,  TrackReward.MaxHealth, 15),
            (40,  TrackReward.MaxHealth, 15),
            (45,  TrackReward.MaxHealth, 15),
            (50,  TrackReward.MaxHealth, 15),
            (60,  TrackReward.MaxHealth, 15),
            (70,  TrackReward.MaxHealth, 15),
            // TEN IS A KEPT HISTORICAL VALUE, not a live formula link. This
            // used to be sized to exactly match AbilityDerivation's
            // CharacterBand -- the point past which a piecewise curve stopped
            // paying a flat rate and started paying the square of the excess,
            // making ten points into one ability the single most efficient
            // spend the game offered. AbilityDerivation's balance redesign
            // (Phase 2/D2) deleted that piecewise curve entirely: every
            // derivation is now a straight line through zero, with no band
            // edge and no efficiency cliff for this milestone to size itself
            // against. The number stays 10 anyway -- it is still a fine round
            // grant on its own terms -- but nothing in AbilityDerivation
            // computes or constrains it any more; if it ever needs to move,
            // move it here, not by hunting for a constant that no longer
            // exists.
            //
            // A GRANT rather than an unlock, so it goes through the watermark
            // and can only be paid once -- which is also why it can be this
            // large without needing a cap somewhere.
            (80,  TrackReward.StatPoint, 10),
            (90,  TrackReward.SecondLife, 1),
            (100, TrackReward.MaxHealth, 15),
        };

        // THE FILLER, as a mix rather than as a placement.
        //
        // Counts, not levels: "forty stat points across the track" is the
        // design statement, and where each one lands is arithmetic. Retuning
        // the track is editing these numbers.
        //
        // COMPLETE: these two counts sum to 87, which is exactly the number of
        // filler levels, so no level of the track pays nothing.
        //
        // ONLY TWO KINDS EXIST YET. The six over-arching kinds this filler used
        // to spread across (Favor, ExpFind, OfferReroll among them) were
        // retired in P1 of docs/PLAN_REWARD_TRACKS.md; per-character rewards
        // (elemental damage, mana, signature capacity, book skills) are P2-P7.
        // Until those land, a stat point or two max health is what the track
        // has to give -- which is also why this table is the generated
        // default's stand-in rather than a placeholder to be thrown away: see
        // the Milestones comment above.
        private static readonly (TrackReward Reward, int Amount, int Count)[] FillerMix =
        {
            (TrackReward.StatPoint,  1, 40),
            (TrackReward.MaxHealth,  2, 47),
        };

        private static readonly TrackEntry[] Entries = Build();

        // What reaching `level` hands over.
        public static TrackEntry At(int level)
        {
            if (level < StartingLevel || level > MaxLevel)
            {
                return new TrackEntry(TrackReward.None, 0);
            }

            return Entries[level];
        }

        // A quantity handed over once, as against a capability that is simply
        // true from some level onward. The distinction the watermark depends
        // on: only grants are claimed.
        public static bool IsGrant(TrackReward reward)
        {
            switch (reward)
            {
                case TrackReward.StatPoint:
                case TrackReward.MaxHealth:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsUnlock(TrackReward reward) =>
            reward != TrackReward.None && !IsGrant(reward);

        // How much of `reward` the track owes for levels in
        // (afterLevel, throughLevel]. Half-open at the bottom because that is
        // what a watermark means: everything up to and including afterLevel has
        // already been paid.
        //
        // Returns 0 rather than throwing on a reversed or out-of-range pair --
        // a save can hold anything, and "you are owed nothing" is the right
        // answer to "you have already claimed past where you are".
        public static int GrantedBetween(TrackReward reward, int afterLevel, int throughLevel)
        {
            if (!IsGrant(reward)) return 0;

            if (afterLevel < StartingLevel) afterLevel = StartingLevel;
            return SumOver(reward, afterLevel, throughLevel);
        }

        // The next level at or after `level` + 1 that pays anything, or 0 when
        // the track has nothing left.
        //
        // Skips the filler gaps, which is the point: "what do I get next" is a
        // question about the next REWARD, and a track with unauthored levels in
        // it would otherwise answer "nothing, at level 38".
        public static int NextRewardLevel(int level)
        {
            int from = level < StartingLevel ? StartingLevel : level;

            for (int next = from + 1; next <= MaxLevel; next++)
            {
                if (Entries[next].IsSomething) return next;
            }

            return 0;
        }

        // Whether `level` carries a MILESTONE rather than filler.
        //
        // Asked of the Milestones table, which is the only thing that actually
        // knows. The reward KIND cannot answer it: level 10 is +15 max health
        // and level 80 is ten stat points, and both of those kinds are also
        // handed out as filler dozens of times each. A screen that guessed
        // from the kind drew level 10 as an ordinary node -- which is how this
        // method came to exist.
        public static bool IsMilestone(int level)
        {
            foreach (var milestone in Milestones)
            {
                if (milestone.Level == level) return true;
            }

            return false;
        }

        // HOW ONE NODE READS, given where the player is and how far the track
        // has paid. The screen's whole state model, in one pure function.
        //
        // HERE WINS OVER WAITING, and that ordering is the decision rather than
        // an accident of the if-chain. The player's own node is the one thing
        // on a hundred-node rail they need to find at a glance, so it draws
        // pale even when its own reward is uncollected -- and nothing is hidden
        // by that, because IsWaiting below is asked separately for the pulsing
        // ring and for whether a click claims. A node can be both; only its
        // disc has to pick one.
        public static TrackNodeState StateOf(int level, int playerLevel, int claimedLevel)
        {
            if (level == playerLevel) return TrackNodeState.Here;
            if (level > playerLevel) return TrackNodeState.ToCome;

            return level > claimedLevel ? TrackNodeState.Waiting : TrackNodeState.Collected;
        }

        // Whether this level has been reached and not yet paid -- which is what
        // a click on it collects, and what the pulsing ring advertises.
        //
        // NOT `StateOf(...) == Waiting`, deliberately: the player's own node
        // answers Here and would come back false, and it is exactly the node
        // most likely to be holding an uncollected reward.
        public static bool IsWaiting(int level, int playerLevel, int claimedLevel) =>
            level <= playerLevel && level > claimedLevel && level >= StartingLevel + 1;

        // How many levels a press of "collect everything" would settle.
        //
        // CLAMPED AT ZERO rather than returning a negative for a watermark that
        // has run ahead of the level -- a hand-edited save can hold anything,
        // and "nothing to collect" is the right answer to "you have already
        // been paid past where you are".
        public static int UnclaimedCount(int playerLevel, int claimedLevel)
        {
            if (playerLevel > MaxLevel) playerLevel = MaxLevel;
            if (claimedLevel < StartingLevel) claimedLevel = StartingLevel;

            int owed = playerLevel - claimedLevel;
            return owed < 0 ? 0 : owed;
        }

        // The first level that grants `reward`, or 0 if the track never does.
        public static int UnlockLevel(TrackReward reward)
        {
            for (int level = StartingLevel; level <= MaxLevel; level++)
            {
                if (Entries[level].Reward == reward) return level;
            }

            return 0;
        }

        // Whether a character at `level` has this capability. THE ONLY QUESTION
        // an unlock ever needs to answer, and it needs no storage to do it.
        public static bool HasUnlocked(TrackReward reward, int level)
        {
            if (!IsUnlock(reward)) return false;

            int unlockedAt = UnlockLevel(reward);
            return unlockedAt > 0 && level >= unlockedAt;
        }

        // Every Amount for `reward` up to `level`, added together. For rewards
        // that stack; see UnlockedAmount for the ones where a later step
        // replaces an earlier one.
        //
        // The whole track rather than a watermark range, so the bottom bound is
        // one BELOW StartingLevel -- SumOver is half-open there, and level 1 has
        // to be included in principle even though nothing is authored on it.
        public static int UnlockedTotal(TrackReward reward, int level) =>
            SumOver(reward, StartingLevel - 1, level);

        // Adds up `reward` over the levels in (afterLevel, throughLevel].
        //
        // ONE LOOP FOR TWO CALLERS. GrantedBetween and UnlockedTotal were the
        // same six lines with different bounds and opposite guards -- one
        // refusing anything that is not a grant, the other used exclusively for
        // an unlock that stacks. What differs between them is the guard and the
        // range, which is what each now supplies; the arithmetic is here once.
        private static int SumOver(TrackReward reward, int afterLevel, int throughLevel)
        {
            if (throughLevel > MaxLevel) throughLevel = MaxLevel;
            if (afterLevel < StartingLevel - 1) afterLevel = StartingLevel - 1;

            int total = 0;
            for (int level = afterLevel + 1; level <= throughLevel; level++)
            {
                var entry = Entries[level];
                if (entry.Reward == reward) total += entry.Amount;
            }

            return total;
        }

        // The highest Amount reached for an unlock that comes in steps, or
        // `fallback` if none has.
        public static int UnlockedAmount(TrackReward reward, int level, int fallback)
        {
            int best = fallback;

            for (int l = StartingLevel; l <= MaxLevel && l <= level; l++)
            {
                var entry = Entries[l];
                if (entry.Reward == reward && entry.Amount > best) best = entry.Amount;
            }

            return best;
        }

        // ---- construction -------------------------------------------------------

        private static TrackEntry[] Build()
        {
            var entries = new TrackEntry[MaxLevel + 1];

            foreach (var milestone in Milestones)
            {
                entries[milestone.Level] = new TrackEntry(milestone.Reward, milestone.Amount);
            }

            // Every level from 2 upward that no milestone claimed. Level 1 is
            // where a character starts, not somewhere they arrive.
            var fillerLevels = new int[MaxLevel];
            int fillerCount = 0;
            for (int level = StartingLevel + 1; level <= MaxLevel; level++)
            {
                if (entries[level].Reward == TrackReward.None) fillerLevels[fillerCount++] = level;
            }

            var mixed = InterleaveMix();
            Spread(entries, fillerLevels, fillerCount, mixed);

            return entries;
        }

        // The filler mix as an ORDER, with the kinds interleaved rather than
        // clumped: 40 stat points and 47 max health come out as S H S H H S H ...
        // rather than as forty of one followed by forty-seven of the other.
        //
        // Largest-deficit selection, cross-multiplied to stay in integers: at
        // each step, hand the slot to whichever entry is furthest behind the
        // share its Count entitles it to. Ties go to declaration order, so the
        // sequence is fully determined by the table above.
        private static TrackEntry[] InterleaveMix()
        {
            int total = 0;
            foreach (var part in FillerMix) total += part.Count;

            var sequence = new TrackEntry[total];
            var emitted = new int[FillerMix.Length];

            for (int placed = 0; placed < total; placed++)
            {
                int best = -1;
                long bestDeficit = long.MinValue;

                for (int e = 0; e < FillerMix.Length; e++)
                {
                    if (emitted[e] >= FillerMix[e].Count) continue;

                    long deficit = (long)FillerMix[e].Count * (placed + 1) - (long)emitted[e] * total;
                    if (deficit > bestDeficit)
                    {
                        bestDeficit = deficit;
                        best = e;
                    }
                }

                if (best < 0) break;

                emitted[best]++;
                sequence[placed] = new TrackEntry(FillerMix[best].Reward, FillerMix[best].Amount);
            }

            return sequence;
        }

        // Places `mixed` evenly across the filler levels.
        //
        // EVENLY ACROSS THE WHOLE TRACK, not packed into the front. There are
        // more filler levels than there are rewards to put in them until the
        // remaining reward kinds exist, and filling from level 2 upward would
        // leave the back half of the track empty -- which is the half a player
        // grinds hardest for. Bresenham: slot i takes the next reward when the
        // running share crosses an integer boundary.
        //
        // NO EARLY-LEVEL GATING HERE ANY MORE. This used to swap a filler
        // reward past the milestone that first introduced it -- level 40's
        // "you may now reroll the offer" could not be pre-empted by a filler
        // reroll at level 12. Both remaining filler kinds are GRANTS
        // (StatPoint, MaxHealth), and a grant's milestone is an extra helping
        // rather than an introduction, so there is nothing left to gate: see
        // RewardTrack.IsUnlock and the Milestones comment above.
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
