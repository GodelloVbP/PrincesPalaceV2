using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Progression
{
    // What each level hands over.
    //
    // Two categories, and the split is the load-bearing idea here rather than
    // a tidiness one -- see RewardTrack's header for why.
    public enum TrackReward
    {
        // Nothing authored at this level yet. Not an error: a track's filler
        // mix is sized to exactly cover its filler levels, but a track under
        // construction (or the interim generated default, before this list
        // was full) can still have gaps.
        None = 0,

        // ---- THE ONE GRANT: a quantity, claimed once against the save's
        // watermark and stored (Character.unspentStatPoints). See IsGrant's
        // own comment for why only this one is. --
        StatPoint,

        // ---- EVERYTHING ELSE: a total summed over the entries at levels
        // <= the watermark, read live at its own site rather than stored --
        // docs/PLAN_REWARD_TRACKS.md §2. --
        MaxHealth,
        Respec,
        SecondLife,

        // APPENDED, never inserted, mirroring DamageType's own convention:
        // once a track is authored as content, its JSON stores this enum by
        // name (matched case-insensitively, ContentSchema.EnumBackedFields),
        // but a resolved RewardTrackDefinitionAsset still carries it as an
        // int, so reordering an existing member would relabel every already-
        // authored entry on the next load.
        SignatureCapacity,
        SignatureGainPerTurn,
        SignatureGainOnDamageTaken,
        SignatureAbsorbs,
        ElementalDamagePercent,
        MaxMana,
        ManaRegen,
        UnlockSkill,
    }

    // One level's worth of reward.
    public readonly struct TrackEntry
    {
        public readonly TrackReward Reward;

        // What the reward is worth. A count for the one grant (a stat point)
        // and for every accumulating kind (two max health, one wool a turn),
        // and for a pure on/off unlock the PARAMETER of the capability where
        // it has one -- SecondLife 1 means "one charge". Zero where the
        // capability has no number.
        public readonly int Amount;

        // The two selectors an authored entry can carry beyond reward and
        // amount (docs/PLAN_REWARD_TRACKS.md §4): which element an
        // ElementalDamagePercent entry scales, and which skill an UnlockSkill
        // entry grants.
        public readonly DamageType? Against;
        public readonly string SkillId;

        // The two captions baked onto the entry by the content resolver
        // before RewardTrackNames ever sees it. RewardTrackNames is Domain-
        // only and cannot itself look a skill id or a character's
        // signatureDisplayName up into words -- see that file's own header --
        // so the resolver bakes both here once, at content-build time, and
        // RewardTrackNames.Of reads them rather than looking anything up.
        public readonly string SkillDisplayName;
        public readonly string ResourceDisplayName;

        public TrackEntry(TrackReward reward, int amount, DamageType? against = null,
            string skillId = null, string skillDisplayName = null, string resourceDisplayName = null)
        {
            Reward = reward;
            Amount = amount;
            Against = against;
            SkillId = skillId;
            SkillDisplayName = skillDisplayName;
            ResourceDisplayName = resourceDisplayName;
        }

        public bool IsSomething => Reward != TrackReward.None;
    }

    // THE PART OF A REWARD TRACK THAT DOES NOT VARY BY CHARACTER.
    //
    // docs/PLAN_REWARD_TRACKS.md P3 split this file in two. WHICH REWARD sits
    // at which level is now per-character (RewardTrackDefinition, one instance
    // per track); WHERE THE MILESTONES FALL, and the pure arithmetic over a
    // level/claimedLevel pair the screen's whole state model runs on, is
    // shared by every track and stays here. See §1 of the plan for why the
    // cadence had to stay fixed even though the content did not: the screen
    // bakes milestone-ness into the built scene at nine separate places, and
    // if the levels varied per character every one of those would become a
    // runtime decision.
    //
    // GRANTS AND UNLOCKS ARE DIFFERENT THINGS, and keeping them apart removes a
    // whole class of bug rather than merely organising the enum.
    //
    //   THE GRANT is a quantity that has to be handed over exactly once, so it
    //   needs a watermark on the save (Character.claimedTrackLevel) recording
    //   how far the track has paid out, and a field to hand it into
    //   (Character.unspentStatPoints). StatPoint is the only one -- see
    //   IsGrant.
    //
    //   EVERYTHING ELSE is read live: a pure function of the level reached,
    //   answered fresh at its own site (RewardTrackDefinition.CollectedTotal /
    //   HasUnlocked) rather than accumulated into a save field anywhere. A
    //   capability like Respec is the purest case -- "is this unlocked" is
    //   "is level >= the level that grants it", stored nowhere -- but the
    //   same read-live idea now covers every accumulating kind too (wool
    //   capacity, elemental damage percent): storing a running copy would let
    //   it disagree with the definition after a retune, so nothing does.
    //
    // Engine-free, so everything here is pinnable from EditMode.
    public static class RewardTrack
    {
        // Where the track starts and ends. A character begins AT level 1, so
        // level 1 is not a reward -- the first thing a track pays is level 2.
        public const int StartingLevel = 1;

        // FORTY, down from a hundred, and the cut is a design decision rather
        // than a tuning one. Progression v2 (docs/handoffs/progression_v2/
        // PLAN_PROGRESSION_V2.md §0 D3, §3) prices a career at level 30 as
        // the completion point with 31-40 as an optional prestige stretch; a
        // hundred levels against the same total budget meant sixty of them
        // carried filler nobody designed. Everything that reads this follows:
        // level_curve.json holds exactly MaxLevel-1 rows and the resolver
        // refuses a table that does not, and LevelCurve.AddExperience stops a
        // character here rather than letting income carry them past the last
        // authored node.
        public const int MaxLevel = 40;

        // THE SHARED CADENCE. Ten levels, the same on every track whatever it
        // pays at them -- docs/PLAN_REWARD_TRACKS.md §1 explains why this
        // stays fixed while the CONTENT at each one does not.
        //
        // The early three (3, 5, 10) are new and are where the first hour
        // actually is: 3 is the first ability, typically fight 6 of run 1, and
        // a track whose first milestone sat at 10 had nothing to show for any
        // of it. 30 is completion; 35, 38 and 40 are the prestige stretch's
        // own three.
        public static readonly int[] MilestoneLevels =
        {
            3, 5, 10, 15, 20, 25, 30, 35, 38, 40,
        };

        // Whether `level` carries a MILESTONE rather than filler. Asked of
        // MilestoneLevels, which is the only thing that actually knows: the
        // reward KIND cannot answer it on its own, because the same kind
        // (MaxHealth, on the interim default track) is also handed out as
        // filler dozens of times.
        public static bool IsMilestone(int level)
        {
            foreach (int milestoneLevel in MilestoneLevels)
            {
                if (milestoneLevel == level) return true;
            }

            return false;
        }

        // A quantity handed over once, as against a capability or a total
        // that is simply read live off the watermark. THE ONLY GRANT IS
        // StatPoint (docs/PLAN_REWARD_TRACKS.md §2: "Nothing else is spent,
        // so nothing else needs storage -- and storing it is strictly worse,
        // because a stored copy can disagree with the definition after a
        // retune"). A second grant added here without noticing would be paid
        // once into a field nothing else expects and then paid again every
        // time CollectedTotal re-reads the same levels.
        public static bool IsGrant(TrackReward reward) => reward == TrackReward.StatPoint;

        // A CAPABILITY GRANTED ONCE -- the four kinds whose whole meaning is
        // that they happened.
        //
        // "GRANTED ONCE" IS NOT "HAS NO NUMBER", and this comment said the
        // latter until it was checked: SecondLife's Amount is its CHARGE
        // COUNT, both shipped tracks author it as 1, and SquadTrack.
        // SecondLivesLeft sums CollectedTotal(SecondLife, ...) across the
        // squad to get the ceiling. Author it as 0 -- which is what the old
        // wording told you to do -- and the level-90 milestone silently
        // grants nothing, with no resolver rule refusing it and nothing on
        // screen saying so. Respec, SignatureAbsorbs and UnlockSkill are the
        // three that genuinely carry no number; see TrackEntry.Amount, which
        // has always said "the PARAMETER of the capability where it has one".
        // These are the four an author may not place as FILLER
        // (docs/PLAN_REWARD_TRACKS.md §4's rule 3): a filler node's level is
        // computed rather than authored, and "you learn Lightning Bolt at
        // whichever level the interleave happens to put it" is not a design
        // decision anybody made.
        //
        // NOT "every kind but the one grant". A helper with that broader
        // meaning briefly gated the filler check here and refused MaxHealth
        // 10 x15, which is a filler row on BOTH shipped tracks -- MaxHealth
        // is not StatPoint either, so the broader test caught it too and
        // would have left levels 2-24 as nothing but stat points. This list
        // names the four one-shot kinds explicitly instead.
        public static bool IsOneShotCapability(TrackReward reward) =>
            reward == TrackReward.Respec
            || reward == TrackReward.SecondLife
            || reward == TrackReward.SignatureAbsorbs
            || reward == TrackReward.UnlockSkill;

        // THE SET ContentDatabase.BuildSignatureResource PAYS -- a reward of
        // any of these four kinds is meaningless on a character with no
        // signature resource, which is what both the resolver (at authoring
        // time) and ContentDatabase's own validation (at load time, for a
        // hand-authored asset that never passed through the resolver) refuse.
        public static bool IsSignatureReward(TrackReward reward) =>
            reward == TrackReward.SignatureCapacity
            || reward == TrackReward.SignatureGainPerTurn
            || reward == TrackReward.SignatureGainOnDamageTaken
            || reward == TrackReward.SignatureAbsorbs;

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
    }
}
