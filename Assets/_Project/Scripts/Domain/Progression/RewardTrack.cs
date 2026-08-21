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
        Favor,
        MaxHealth,
        SignatureAtFightStart,

        // ---- UNLOCKS: a capability, true forever once the level is reached --
        Respec,
        RelicSlot,
        RestBeforeBoss,
        OfferReroll,
        WiderOffer,
        TwoStartingRelics,
        ChosenStartingRelics,
        EliteRelicDrop,
        SecondLife,
        SecondLifeRefresh,
    }

    // One level's worth of reward.
    public readonly struct TrackEntry
    {
        public readonly TrackReward Reward;

        // What the reward is worth. A count for grants (one stat point, two
        // Favor), and for unlocks the PARAMETER of the capability where it has
        // one -- RelicSlot 2 means "a second slot", WiderOffer 4 means "four
        // items". Zero where the capability has no number.
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
    //   A GRANT is a quantity -- a stat point, two Favor. It has to be handed
    //   over exactly once, so it needs a watermark on the save
    //   (Character.claimedTrackLevel) recording how far the track has paid out.
    //
    //   An UNLOCK is a capability -- respec, a wider offer, a second life. It
    //   is a pure function of the level reached and is stored NOWHERE. Asking
    //   "is this unlocked" is asking "is level >= the level that grants it".
    //
    // The reason that matters: most of the unlocks are systems that do not
    // exist yet. If they were claimed once against a watermark, every character
    // who passed level 50 before the wider offer was built would have spent
    // their claim on a no-op and never get it. As a pure function of level,
    // there is nothing to miss -- the capability simply starts working the day
    // it is implemented, for everyone who has the level.
    //
    // Engine-free, so the whole table is pinnable from EditMode.
    public static class RewardTrack
    {
        // Where the track starts and ends. A character begins AT level 1, so
        // level 1 is not a reward -- the first thing the track pays is level 2.
        public const int StartingLevel = 1;
        public const int MaxLevel = 100;

        // THE MILESTONES. Tens, plus the two relic slots that are deliberately
        // off the tens: they are prerequisites rather than headline rewards,
        // and level 60's "start every run with two relics" cannot mean anything
        // until slot 2 exists at 25.
        private static readonly (int Level, TrackReward Reward, int Amount)[] Milestones =
        {
            (10,  TrackReward.Favor, 5),
            (20,  TrackReward.Respec, 0),
            (25,  TrackReward.RelicSlot, 2),
            (30,  TrackReward.RestBeforeBoss, 0),
            (40,  TrackReward.OfferReroll, 1),
            (45,  TrackReward.RelicSlot, 3),
            (50,  TrackReward.WiderOffer, 4),
            (60,  TrackReward.TwoStartingRelics, 2),
            (70,  TrackReward.ChosenStartingRelics, 0),
            (80,  TrackReward.EliteRelicDrop, 0),
            (90,  TrackReward.SecondLife, 1),
            (100, TrackReward.SecondLifeRefresh, 0),
        };

        // THE FILLER, as a mix rather than as a placement.
        //
        // Counts, not levels: "thirty stat points across the track" is the
        // design statement, and where each one lands is arithmetic. Retuning
        // the track is editing these numbers.
        //
        // INCOMPLETE ON PURPOSE. The design also calls for 15 max-HP nodes, 6
        // signature-at-fight-start and 2 extra offer rerolls, and none of those
        // reward kinds can be paid out yet -- there is no per-character bonus
        // health field, no fight-start signature hook, and no reroll. Adding
        // them here before they do anything would hand players a reward that
        // silently does nothing. The levels they will occupy read as None until
        // then, which is visible rather than quietly wrong.
        //
        // Gold nodes are absent for a different reason and a permanent-looking
        // one: RoomType.Shop is in the room table but the shop is not built, so
        // gold has no sink. See docs/HANDOVER_PROGRESSION_TRACK.md 4b.
        private static readonly (TrackReward Reward, int Amount, int Count)[] FillerMix =
        {
            (TrackReward.StatPoint, 1, 30),
            (TrackReward.Favor,     2, 20),
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
                case TrackReward.Favor:
                case TrackReward.MaxHealth:
                case TrackReward.SignatureAtFightStart:
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
            if (throughLevel > MaxLevel) throughLevel = MaxLevel;

            int total = 0;
            for (int level = afterLevel + 1; level <= throughLevel; level++)
            {
                var entry = Entries[level];
                if (entry.Reward == reward) total += entry.Amount;
            }

            return total;
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

        // The highest Amount reached for an unlock that comes in steps, or
        // `fallback` if none has. RelicSlot is the case this exists for: it
        // appears twice, and the answer to "how many slots" is the later of the
        // two once level 45 is passed.
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
        // clumped: 30 stat points and 20 Favor come out as S F S F S S F ...
        // rather than as thirty of one followed by twenty of the other.
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
        private static void Spread(TrackEntry[] entries, int[] fillerLevels, int fillerCount, TrackEntry[] mixed)
        {
            if (fillerCount <= 0 || mixed.Length == 0) return;

            int taken = 0;

            for (int i = 0; i < fillerCount && taken < mixed.Length; i++)
            {
                int before = i * mixed.Length / fillerCount;
                int after = (i + 1) * mixed.Length / fillerCount;

                if (after > before)
                {
                    entries[fillerLevels[i]] = mixed[taken++];
                }
            }
        }
    }
}
