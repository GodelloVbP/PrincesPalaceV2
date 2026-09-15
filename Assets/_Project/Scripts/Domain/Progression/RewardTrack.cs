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

        // ---- PHASE 3 (docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md
        // §5, §7 phase 3): the reward grammar the 40-level combat stretch
        // authors against. APPENDED, same convention as the block above --
        // reordering any existing member relabels every already-authored
        // entry the next time content loads. --

        // SETS the primary pool's gainOnAttack to the authored value --
        // READ LIVE as the highest amount collected (RewardTrackDefinition.
        // UnlockedAmount), NOT summed, because the table this pays against
        // (docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md §4) authors
        // "15 to 20" then "20 to 25" -- two TOTALS, not two deltas. A talent
        // that added gain on top would still stack normally (Gain is a
        // plain field TrackDatabase.BuildPrimaryPool writes once); none
        // exists today. See ContentDatabase.BuildPrimaryPool for the read
        // site.
        FuryGainOnAttack,

        // SETS the primary pool's opening value for fights after collection
        // -- same SET-not-SUM reading as FuryGainOnAttack above, and the
        // same reason (the table says "opens at 25" then "opens at 50").
        // The pool DEFINITION's own startRule (Zero, for fury) is untouched;
        // this is an override applied where ContentDatabase.BuildPrimaryPool
        // would otherwise call ResourcePool's own StartingValue.
        FuryStartOfFight,

        // Every mana-costed skill in the collector's kit costs this many
        // less, floored at 1 -- SET, not summed (the table says "cost 1
        // less" then "cost 2 less"). Read at ContentDatabase.
        // ApplyRewardTrackSkillDeltas, which is where FightEncounterAdapter.
        // KitFor(Character, ...) builds the character's real kit -- so both
        // SkillResolution.CanAfford and the mana actually charged
        // (FightSession.ChargeSkillMana) see the same discounted number,
        // because both read skill.ManaCost off that one already-adjusted
        // ResolvedSkill rather than a second copy of the rule.
        SpellCostDelta,

        // ONE named skill's cost in ONE resource (mana or the collector's
        // signature resource), reduced by Amount, floored at 1 -- SUMMED
        // across every entry naming the same (SkillId, Resource) pair,
        // unlike the two SETs above (Shawn's Shear and Battering Ram are
        // each named once in the shipped table, but Bjorn's Slam pattern --
        // two SkillFlatDelta entries on one skill -- is the shape this
        // guards for). See TrackEntry.Resource and RewardTrackDefinition.
        // CollectedSkillCostDelta.
        SkillCostDelta,

        // ONE named skill's flatAmount, increased by Amount -- SUMMED across
        // every entry naming the same skill (Bjorn's Slam: two +5 nodes read
        // as +10 total). See RewardTrackDefinition.CollectedSkillFlatDelta.
        SkillFlatDelta,

        // How much raw damage one point of the collector's signature
        // resource absorbs -- SET, not summed (1, then 2). Replaces the old
        // boolean SignatureAbsorbs semantics; see IsSignatureReward and the
        // alias note on SignatureAbsorbs above (RewardTrackEntryResolver
        // rewrites an authored SignatureAbsorbs entry into this reward at
        // amount 1, so old content keeps resolving without a second read
        // path). SignatureAbsorbs itself is deprecated as of this addition
        // -- author SignatureAbsorbPerPoint directly in new content.
        SignatureAbsorbPerPoint,

        // A cosmetic payload with no combat number: a title, a plate rim, a
        // portrait frame, a plate emboss, a victory pose, or mastery --
        // exactly one per entry, selected by TrackEntry.IdentityKind.
        // Collected, never spent; see RewardTrackDefinition.CollectedIdentity
        // and Core/CharacterIdentity for the read model. No rendering yet
        // (phase 5).
        Identity,
    }

    // Which RESOURCE a SkillCostDelta entry discounts. Matched against the
    // skill's own authored cost fields at content-build time
    // (RewardTrackEntryResolver) -- "the resource must be one the skill
    // actually costs" -- so an author cannot discount mana on a skill that
    // only spends Wool by mistake.
    public enum TrackResourceTarget
    {
        Mana,
        Signature,
    }

    // The six cosmetic payloads an Identity entry can carry -- docs/
    // handoffs/progression_v2/PLAN_PROGRESSION_V2.md §4's identity stretch
    // (levels 31-40). Title/PlateRim/PlateEmboss read TrackEntry.
    // IdentityValue (a title string, or "silver"/"gold"); the other three
    // carry no value.
    public enum TrackIdentityKind
    {
        Title,
        PlateRim,
        PortraitFrame,
        PlateEmboss,
        VictoryPose,
        Mastery,
    }

    // WHAT KIND OF NODE A REWARD DRAWS AS, for the phase-3 node-kind
    // validator (RewardTrackNodeValidation) and, later, the track screen's
    // per-kind art. Derived from TrackReward alone -- see RewardTrack.KindOf
    // -- rather than authored a second time, so a node's kind can never
    // disagree with what it actually grants.
    public enum TrackNodeKind
    {
        Ability,
        Choice,
        Bump,
        Capability,
        Utility,
        Identity,
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

        // PHASE 3's two further selectors, beside Against and SkillId --
        // which resource a SkillCostDelta entry discounts, and which
        // cosmetic payload an Identity entry carries. Null/empty for every
        // entry that is not that kind, the same "unused selector reads as
        // nothing" convention Against/SkillId already follow.
        public readonly TrackResourceTarget? Resource;
        public readonly TrackIdentityKind? IdentityKind;
        public readonly string IdentityValue;

        public TrackEntry(TrackReward reward, int amount, DamageType? against = null,
            string skillId = null, string skillDisplayName = null, string resourceDisplayName = null,
            TrackResourceTarget? resource = null, TrackIdentityKind? identityKind = null, string identityValue = null)
        {
            Reward = reward;
            Amount = amount;
            Against = against;
            SkillId = skillId;
            SkillDisplayName = skillDisplayName;
            ResourceDisplayName = resourceDisplayName;
            Resource = resource;
            IdentityKind = identityKind;
            IdentityValue = identityValue;
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
        public const int MaxLevel = 100;

        // THE SHARED CADENCE. Twelve levels, the same on every track whatever
        // it pays at them -- docs/PLAN_REWARD_TRACKS.md §1 explains why this
        // stays fixed while the CONTENT at each one does not.
        public static readonly int[] MilestoneLevels =
        {
            10, 20, 25, 30, 40, 45, 50, 60, 70, 80, 90, 100,
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

        // PHASE 3's broader filler ban. Every one-shot capability above
        // still may not be filler, AND neither may any of the seven P3
        // kinds -- not because they are one-shot (four of them can be
        // collected more than once: SET-style kinds supersede rather than
        // duplicate, and SkillFlatDelta/SkillCostDelta genuinely sum), but
        // because every one of them either needs a level an author placed
        // on purpose (a fury-opening step, a title) or a skillId a filler
        // row has no field for at all (RawTrackFiller carries no skillId,
        // the same reason UnlockSkill was already filler-ineligible). This
        // is the check rule 3 actually runs -- IsOneShotCapability is kept
        // separately because HasUnlocked and the "appears at most once"
        // duplicate check upstream both still mean literally "one shot".
        public static bool IsFillerIneligible(TrackReward reward) =>
            IsOneShotCapability(reward)
            || reward == TrackReward.FuryGainOnAttack
            || reward == TrackReward.FuryStartOfFight
            || reward == TrackReward.SpellCostDelta
            || reward == TrackReward.SkillCostDelta
            || reward == TrackReward.SkillFlatDelta
            || reward == TrackReward.SignatureAbsorbPerPoint
            || reward == TrackReward.Identity;

        // THE SET ContentDatabase.BuildSignatureResource PAYS -- a reward of
        // any of these five kinds is meaningless on a character with no
        // signature resource, which is what both the resolver (at authoring
        // time) and ContentDatabase's own validation (at load time, for a
        // hand-authored asset that never passed through the resolver) refuse.
        // SignatureAbsorbPerPoint joins the original four in P3, replacing
        // SignatureAbsorbs' boolean (SignatureAbsorbs is aliased onto it by
        // the resolver -- see TrackReward.SignatureAbsorbPerPoint's own
        // header -- so an authored SignatureAbsorbs entry is checked under
        // this same rule by the time it reaches here).
        public static bool IsSignatureReward(TrackReward reward) =>
            reward == TrackReward.SignatureCapacity
            || reward == TrackReward.SignatureGainPerTurn
            || reward == TrackReward.SignatureGainOnDamageTaken
            || reward == TrackReward.SignatureAbsorbs
            || reward == TrackReward.SignatureAbsorbPerPoint;

        // PHASE 3's node-kind mapping (docs/handoffs/progression_v2/
        // PLAN_PROGRESSION_V2.md §7 phase 3's own list, read literally where
        // it names a reward and resolved where it names a wildcard):
        // UnlockSkill -> Ability; StatPoint -> Choice; every other numeric
        // grant/bump -> Bump; the three "always-on from here" rules
        // (SpellCostDelta, SignatureAbsorbPerPoint, FuryStartOfFight) ->
        // Capability; the two run-scoped unlocks -> Utility; Identity ->
        // Identity. FuryGainOnAttack sits with the numeric bumps (the
        // brief's own "Fury*" wildcard) even though FuryStartOfFight, named
        // explicitly right after it, does not -- see RewardTrackNodeValidation
        // for why that split earns its own paragraph rather than a single
        // "Fury* -> X" rule.
        public static TrackNodeKind KindOf(TrackReward reward)
        {
            switch (reward)
            {
                case TrackReward.UnlockSkill:
                    return TrackNodeKind.Ability;

                case TrackReward.StatPoint:
                    return TrackNodeKind.Choice;

                case TrackReward.Respec:
                case TrackReward.SecondLife:
                    return TrackNodeKind.Utility;

                case TrackReward.SpellCostDelta:
                case TrackReward.SignatureAbsorbPerPoint:
                case TrackReward.SignatureAbsorbs: // alias of SignatureAbsorbPerPoint -- see its own header
                case TrackReward.FuryStartOfFight:
                    return TrackNodeKind.Capability;

                case TrackReward.Identity:
                    return TrackNodeKind.Identity;

                case TrackReward.MaxHealth:
                case TrackReward.MaxMana:
                case TrackReward.ManaRegen:
                case TrackReward.ElementalDamagePercent:
                case TrackReward.SignatureCapacity:
                case TrackReward.SignatureGainPerTurn:
                case TrackReward.SignatureGainOnDamageTaken:
                case TrackReward.FuryGainOnAttack:
                case TrackReward.SkillCostDelta:
                case TrackReward.SkillFlatDelta:
                    return TrackNodeKind.Bump;

                default:
                    throw new System.ArgumentOutOfRangeException(nameof(reward), reward,
                        "RewardTrack.KindOf has no case for this TrackReward -- a P3 kind was added without saying which node kind it draws as.");
            }
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
    }
}
