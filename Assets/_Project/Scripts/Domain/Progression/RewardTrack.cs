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

        // How many relics the descent begins with. ONE KIND RATHER THAN TWO:
        // this was a RelicSlot pair at 25/45 and a separate TwoStartingRelics
        // at 60, on the assumption that holding a relic and being given one
        // were different capacities. They are not -- there is no slot cap in
        // the game to lift (AUDIT #50, #51) -- so the three milestones are one
        // escalating line: 2 at level 25, 3 at 45, 4 at 60.
        StartingRelics,

        RestBeforeBoss,
        OfferReroll,
        WiderOffer,
        ChosenStartingRelics,
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

        // THE MILESTONES. Tens, plus the two relic steps at 25 and 45 that are
        // deliberately off them.
        //
        // THE RELIC LINE IS THE TRACK'S ONE REAL CHAIN: 2 relics at 25, 3 at
        // 45, 4 at 60, and at 70 you stop being offered them at random and
        // pick. Everything else on the track is independent, and a track of a
        // hundred independent grants is a checklist rather than a tree -- the
        // same thing ContentDatabase.OrbCost's comment warns about for the
        // talent tree.
        //
        // 25 and 45 used to grant relic SLOTS, on the reasonable-sounding
        // theory that capacity and supply were separate rewards. There is no
        // capacity limit in the game to lift: RunSnapshot.relicIds is a list
        // whose own header states the design as "infinite slots per run". Both
        // levels were granting something the player already had. See AUDIT #50
        // and #51 for how far that got before anyone checked.
        private static readonly (int Level, TrackReward Reward, int Amount)[] Milestones =
        {
            (10,  TrackReward.Favor, 5),
            (20,  TrackReward.Respec, 0),
            (25,  TrackReward.StartingRelics, 2),
            (30,  TrackReward.RestBeforeBoss, 0),
            (40,  TrackReward.OfferReroll, 1),
            (45,  TrackReward.StartingRelics, 3),
            (50,  TrackReward.WiderOffer, 4),
            (60,  TrackReward.StartingRelics, 4),
            (70,  TrackReward.ChosenStartingRelics, 0),
            // A BAND'S WORTH OF POINTS, and 10 is not a round number picked
            // for looking generous -- it is exactly AbilityDerivation's
            // CharacterBand.
            //
            // That constant is where a point stops being worth a flat 20 max
            // health and starts being worth the SQUARE OF THE EXCESS, which
            // begins at 1 and does not overtake the flat rate for another eight
            // points. So ten points into one ability is not merely a big spend,
            // it is the most efficient one the game offers -- and the eleventh
            // is where it falls off a cliff. Pinned by
            // TheLevelEightyGrantIsExactlyOneBandOfPoints rather than left to
            // this paragraph.
            //
            // This level used to grant "elites always drop a relic". Cut as too
            // strong: relics are run-scoped and uncapped (AUDIT #51), elites
            // recur every 8 steps, and a guaranteed drop on each of them
            // compounds with 60's four starting relics and 70's picking them
            // into a run that is decided by its relic stack before the first
            // boss.
            //
            // A GRANT rather than an unlock, so it goes through the watermark
            // and can only be paid once -- which is also why it can be this
            // large without needing a cap somewhere.
            (80,  TrackReward.StatPoint, 10),
            (90,  TrackReward.SecondLife, 1),
            (100, TrackReward.SecondLifeRefresh, 0),
        };

        // What a descent with no track progress begins with. Named rather than
        // written as a bare 1 at the call sites, because it is the fallback
        // UnlockedAmount needs and the two must agree.
        public const int BaseStartingRelics = 1;

        // THE FILLER, as a mix rather than as a placement.
        //
        // Counts, not levels: "thirty stat points across the track" is the
        // design statement, and where each one lands is arithmetic. Retuning
        // the track is editing these numbers.
        //
        // INCOMPLETE ON PURPOSE. The design also calls for 15 max-HP nodes and
        // 6 signature-at-fight-start, and neither can be paid out yet -- there
        // is no per-character bonus health field and no fight-start signature
        // hook. Adding them here before they do anything would hand players a
        // reward that silently does nothing.
        //
        // The two extra offer rerolls ARE here, since level 40 built the
        // reroll. They are what makes RerollsPerRun's accumulate-rather-than-
        // supersede semantics observable: with only the milestone, summing and
        // taking the highest give the same answer and the distinction is
        // untested. The levels they will occupy read as None until
        // then, which is visible rather than quietly wrong.
        //
        // Gold nodes are absent for a different reason and a permanent-looking
        // one: RoomType.Shop is in the room table but the shop is not built, so
        // gold has no sink. See docs/HANDOVER_PROGRESSION_TRACK.md 4b.
        private static readonly (TrackReward Reward, int Amount, int Count)[] FillerMix =
        {
            (TrackReward.StatPoint,   1, 30),
            (TrackReward.Favor,       2, 20),
            (TrackReward.OfferReroll, 1, 2),
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

        // How many times a descent at `level` may reroll its item offer.
        //
        // ACCUMULATES rather than superseding, which is the opposite of
        // StartingRelics next door and the reason both have named accessors
        // instead of callers picking an UnlockedAmount/UnlockedTotal pair for
        // themselves. Getting it the wrong way round is silent: rerolls would
        // read 1 instead of 3, and nothing would look broken.
        //
        // A capability rather than a grant, so it is stored nowhere on the
        // character -- how many are LEFT is run state (RunSnapshot), and how
        // many you get is this.
        public static int RerollsPerRun(int level) =>
            UnlockedTotal(TrackReward.OfferReroll, level);

        // Every Amount for `reward` up to `level`, added together. For rewards
        // that stack; see UnlockedAmount for the ones where a later step
        // replaces an earlier one.
        public static int UnlockedTotal(TrackReward reward, int level)
        {
            if (level > MaxLevel) level = MaxLevel;

            int total = 0;
            for (int l = StartingLevel; l <= level; l++)
            {
                if (Entries[l].Reward == reward) total += Entries[l].Amount;
            }

            return total;
        }

        // How many relics a descent at `level` begins with.
        //
        // Its own named method rather than a raw UnlockedAmount call, because
        // the fallback is part of the answer: a character below level 25 starts
        // with one relic, not zero, and a caller passing the wrong fallback
        // would silently take the draft away.
        public static int StartingRelics(int level) =>
            UnlockedAmount(TrackReward.StartingRelics, level, BaseStartingRelics);

        // The highest Amount reached for an unlock that comes in steps, or
        // `fallback` if none has. StartingRelics is the case this exists for:
        // it appears three times, and the answer to "how many" is the latest
        // step passed rather than the first.
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

        // The earliest level a FILLER node of this reward may land on.
        //
        // A MILESTONE THAT INTRODUCES A CAPABILITY HAS TO COME FIRST. Level 40
        // is "you may now reroll the offer"; a filler reroll at level 12 would
        // hand the player the mechanic before the milestone announcing it, so
        // the milestone reads as a duplicate of something they already had.
        // The even spread put one at level 39, which is how this was found.
        //
        // Only UNLOCKS are gated. A grant's milestone is an extra helping
        // rather than an introduction -- level 80's ten stat points do not
        // introduce stat points, and gating on it would push all thirty filler
        // stat points past level 80.
        //
        // Reads the Milestones table rather than Entries or UnlockLevel,
        // because this runs DURING Build() and Entries is not assigned yet.
        private static int EarliestFillerLevel(TrackReward reward)
        {
            if (!IsUnlock(reward)) return 0;

            foreach (var milestone in Milestones)
            {
                if (milestone.Reward == reward) return milestone.Level;
            }

            return 0;
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

                if (after <= before) continue;

                int level = fillerLevels[i];

                // If the next reward in the sequence is not allowed this early,
                // SWAP it with the first later one that is, rather than
                // dropping it or leaving the slot empty. A swap keeps every
                // count intact and every slot used -- it only perturbs the
                // interleave locally, and the reward that was too early takes
                // the slot of the one that stood in for it.
                if (TooEarlyFor(mixed[taken].Reward, level))
                {
                    for (int j = taken + 1; j < mixed.Length; j++)
                    {
                        if (TooEarlyFor(mixed[j].Reward, level)) continue;

                        var swap = mixed[taken];
                        mixed[taken] = mixed[j];
                        mixed[j] = swap;
                        break;
                    }
                }

                // Still not allowed means nothing left in the sequence may land
                // this early. Leave the slot empty and try the next one; the
                // remaining rewards will place further down the track.
                if (TooEarlyFor(mixed[taken].Reward, level)) continue;

                entries[level] = mixed[taken++];
            }
        }

        private static bool TooEarlyFor(TrackReward reward, int level) =>
            level < EarliestFillerLevel(reward);
    }
}
