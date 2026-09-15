using System.Collections.Generic;

namespace PrincesPalace.Domain.Progression
{
    // PHASE 3's node-kind validator (docs/handoffs/progression_v2/
    // PLAN_PROGRESSION_V2.md §7 phase 3). Pure over a materialised
    // RewardTrackDefinition -- no ContentDatabase, no character catalogue --
    // so it needs no per-track selector like RewardTrackEntryResolver's
    // rules 4/5 and is safe to call from BOTH the resolver (content-build
    // time, docs/PLAN_REWARD_TRACKS.md's established "mirrored" pattern) and
    // ContentDatabase.Validation (load time, for a hand-authored asset that
    // never passed through the resolver) as the SAME function rather than a
    // restated copy -- one implementation, two call sites, per CODE_STANDARDS
    // §10.
    //
    // GUARDED BEHIND THE CONTENT'S DECLARED CAP. reward_tracks.json today is
    // still the pre-P3, 100-level track (twelve fixed milestones, 87
    // interleaved filler levels) that docs/PLAN_REWARD_TRACKS.md shipped --
    // phase 4 replaces it with the 40-level content these rules are written
    // for. Applying combat-stretch rules ("no two neighbouring KINDS between
    // level 2 and 30") to that shape would refuse every existing track
    // outright, so this returns no errors at all while RewardTrack.MaxLevel
    // still reads LegacyMaxLevel. The guard reads the constant rather than a
    // per-track field because there is no such field: a RewardTrackDefinition
    // is always sized to RewardTrack.MaxLevel (see Build's fixed-size
    // allocation), which is one shared cap for every track in the game, not
    // a per-character one. When the phase-2 change lands and moves MaxLevel
    // off 100, these rules switch on for every track with no second edit
    // here -- and phase 4 is what deletes this guard once the legacy shape
    // is gone (see this file's own header note on "delete the old path").
    public static class RewardTrackNodeValidation
    {
        // The pre-P3 cap docs/PLAN_REWARD_TRACKS.md shipped. Not
        // RewardTrack.MaxLevel -- that constant is phase 2's to move, and
        // comparing against a COPY of its old value (rather than reading it
        // live and guessing what "old" means) is what lets this guard keep
        // working unmodified whatever phase 2 lands on, including if phase 2
        // lands on a value other than 40.
        private const int LegacyMaxLevel = 100;

        // Levels 3, 10 and 20 -- literal per the plan's own contract #5
        // ("the node after an ability at 3, 10, 20 is a choice"), not
        // derived from RewardTrack.MilestoneLevels. That array is phase 2's
        // EXP-curve milestone list and may hold entirely different numbers;
        // this rule is about the ability ladder's own fixed shape, a
        // decision the plan made once in its Phase 0 sign-off, independent
        // of where the milestone geometry lands.
        private static readonly int[] AbilityThenChoiceLevels = { 3, 10, 20 };

        // Where the combat stretch ends and the identity stretch begins
        // (docs' D3: "level 30 is the completion point"). A design constant
        // this phase owns, not a consequence of MaxLevel/MilestoneLevels.
        private const int CombatStretchTopLevel = 30;

        private const int ChoiceAmount = 4;

        // The five authored floors docs/handoffs/progression_v2/
        // PLAN_PROGRESSION_V2.md §7 phase 3 states by name. Every other Bump
        // kind (SignatureCapacity, SignatureGainOnDamageTaken, ManaRegen,
        // SkillCostDelta, SkillFlatDelta) has no stated floor and is not
        // checked here -- see this method's own call site for why adding
        // one would be guessing at a number nobody authored.
        private const int MinMaxHealth = 30;
        private const int MinMaxMana = 6;
        private const int MinElementalDamagePercent = 5;
        private const int MinFuryGainStep = 5;
        private const int MinSignatureGainPerTurn = 1;

        // THE PRODUCTION DOOR -- guarded behind RewardTrack.MaxLevel, so this
        // is a no-op against the pre-P3, 100-level content shipped today.
        public static List<string> Validate(string label, RewardTrackDefinition track)
        {
            if (track == null || RewardTrack.MaxLevel == LegacyMaxLevel) return new List<string>();

            return ValidateAgainstCap(label, track, RewardTrack.MaxLevel);
        }

        // THE RULE LOGIC ALONE, parameterised by the cap it should enforce,
        // with no guard -- what a test calls directly. RewardTrackDefinition.
        // Build always sizes a track's entry array to the GLOBAL
        // RewardTrack.MaxLevel (today, 100; phase 2 moves it), so there is
        // no way to construct an actually-40-level RewardTrackDefinition
        // while that constant still reads 100. A fixture stops authoring
        // anything past its own intended cap instead (every level beyond it
        // reads TrackReward.None, which every rule below already skips), and
        // `declaredCap` is what tells this method where that fixture MEANT
        // to stop -- the same number rule F checks the fixture's own highest
        // rewarding level against.
        public static List<string> ValidateAgainstCap(string label, RewardTrackDefinition track, int declaredCap)
        {
            var errors = new List<string>();
            if (track == null) return errors;

            int topLevel = declaredCap;
            int combatTop = CombatStretchTopLevel < topLevel ? CombatStretchTopLevel : topLevel;

            TrackNodeKind? previousKind = null;
            int previousLevel = 0;
            int highestRewardingLevel = 0;

            foreach (var (level, entry) in track.AllLevels())
            {
                if (!entry.IsSomething) continue;
                highestRewardingLevel = level;

                var kind = RewardTrack.KindOf(entry.Reward);

                // RULE: no two neighbouring nodes between level 2 and 30
                // share a kind.
                if (level <= combatTop && previousKind.HasValue && previousLevel == level - 1
                    && previousKind.Value == kind)
                {
                    errors.Add($"{label}: levels {previousLevel} and {level} are both {kind} nodes -- no two " +
                               "neighbouring nodes between level 2 and 30 may share a kind.");
                }

                // RULE: an Ability at 3, 10 or 20 must be followed by a Choice.
                if (kind == TrackNodeKind.Ability && System.Array.IndexOf(AbilityThenChoiceLevels, level) >= 0
                    && level + 1 <= topLevel)
                {
                    var next = track.At(level + 1);
                    if (!next.IsSomething || RewardTrack.KindOf(next.Reward) != TrackNodeKind.Choice)
                    {
                        errors.Add($"{label}: level {level} is an Ability node but level {level + 1} is not a " +
                                   "Choice -- V1/V2/V3 must each be followed by a Choice.");
                    }
                }

                // RULE: any non-Identity kind above 30 is refused; Utility
                // above 30 is a strict subset of this (Utility is never
                // Identity), so it needs no separate check.
                if (level > CombatStretchTopLevel && kind != TrackNodeKind.Identity)
                {
                    errors.Add($"{label}: level {level} is above the combat stretch (30) and must be Identity, " +
                               $"found {kind}.");
                }

                // RULE: a Choice node's amount must be exactly 4.
                if (kind == TrackNodeKind.Choice && entry.Amount != ChoiceAmount)
                {
                    errors.Add($"{label}: level {level} is a Choice node worth {entry.Amount}, must be " +
                               $"{ChoiceAmount}.");
                }

                // RULE: the five authored Bump floors.
                if (kind == TrackNodeKind.Bump)
                {
                    CheckFloor(errors, label, level, entry.Reward, entry.Amount);
                }

                previousKind = kind;
                previousLevel = level;
            }

            // RULE: the track's highest rewarding level must equal the
            // declared cap (RewardTrack.MaxLevel, for the production door) --
            // always true by construction there (RewardTrackDefinition.Build
            // sizes every track to MaxLevel), stated here anyway as the
            // defensive floor for whatever authors a cap directly once phase
            // 4 lands one.
            if (highestRewardingLevel > 0 && highestRewardingLevel != topLevel)
            {
                errors.Add($"{label}: the track's highest rewarding level is {highestRewardingLevel}, not the " +
                           $"declared cap {topLevel}.");
            }

            // RULE: the milestone set matches RewardTrack.MilestoneLevels --
            // already enforced unconditionally by RewardTrackEntryResolver's
            // rule 1 (every one of RewardTrack.MilestoneLevels must carry
            // exactly one milestone entry, and no entry may name a level
            // that isn't in that list) and, for a hand-authored asset, by
            // ContentDatabase.Validation's equivalent mirror. Restated here
            // only as a comment, not a second implementation of the same
            // walk -- see this file's own header on why "mirrored" means one
            // function called twice, not the same rule typed twice.

            return errors;
        }

        private static void CheckFloor(List<string> errors, string label, int level, TrackReward reward, int amount)
        {
            int floor;
            switch (reward)
            {
                case TrackReward.MaxHealth: floor = MinMaxHealth; break;
                case TrackReward.MaxMana: floor = MinMaxMana; break;
                case TrackReward.ElementalDamagePercent: floor = MinElementalDamagePercent; break;
                case TrackReward.FuryGainOnAttack: floor = MinFuryGainStep; break;
                case TrackReward.SignatureGainPerTurn: floor = MinSignatureGainPerTurn; break;
                default: return; // no authored floor for this Bump kind
            }

            if (amount < floor)
            {
                errors.Add($"{label}: level {level} is a Bump ({reward}) of {amount}, below the floor of {floor}.");
            }
        }
    }
}
