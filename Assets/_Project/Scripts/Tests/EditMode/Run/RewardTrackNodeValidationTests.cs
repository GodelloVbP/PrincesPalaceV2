using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.Tests
{
    // PHASE 3's node-kind validator (docs/handoffs/progression_v2/
    // PLAN_PROGRESSION_V2.md §7 phase 3). Most cases go through
    // ValidateAgainstCap rather than Validate, because a fixture that only
    // authors levels 2 to 5 would otherwise fail the "highest rewarding
    // level equals the cap" rule for a reason that has nothing to do with
    // what it is testing -- `declaredCap` is how a short fixture says where
    // it meant to stop. The production door (Validate) is exercised on its
    // own below, against a full-length fixture.
    public class RewardTrackNodeValidationTests
    {
        private const string Label = "fixture";

        private static RewardTrackDefinition Build(params (int Level, TrackEntry Entry)[] entries) =>
            RewardTrackDefinition.Build("fixture", entries);

        private static string Join(List<string> errors) => string.Join(" | ", errors);

        // ---- the production door runs the rules, unguarded ----
        //
        // Phase 3 shipped Validate switched off behind a
        // MaxLevel == 100 check, and the test that stood here asserted
        // exactly that no-op. Phase 4 removed the guard with the
        // hundred-level content it existed for, so what has to be pinned
        // now is the opposite: the door a real build goes through actually
        // refuses a broken track.

        [Test]
        public void Validate_RefusesABrokenTrack()
        {
            // Two neighbouring StatPoint (Choice) nodes -- rule A.
            var track = Build(
                (2, new TrackEntry(TrackReward.StatPoint, 4)),
                (3, new TrackEntry(TrackReward.StatPoint, 4)));

            var errors = RewardTrackNodeValidation.Validate(Label, track);

            Assert.IsNotEmpty(errors, "the production door let two neighbouring Choice nodes through");
            StringAssert.Contains("neighbouring", Join(errors));
        }

        [Test]
        public void Validate_AcceptsAConformingTrack()
        {
            var errors = RewardTrackNodeValidation.Validate(Label, ConformingFixture());

            CollectionAssert.IsEmpty(errors, Join(errors));
        }

        // ---- a minimal conforming 40-level fixture accepts ----

        [Test]
        public void AMinimalConforming40LevelTrack_IsAccepted()
        {
            var track = ConformingFixture();

            var errors = RewardTrackNodeValidation.ValidateAgainstCap(Label, track, declaredCap: 40);

            Assert.AreEqual(0, errors.Count, Join(errors));
        }

        // A hand-built combat stretch that satisfies every rule at once:
        // no two neighbours share a kind (2-30), V1/V2/V3 (3/10/20) each
        // followed by a Choice, every Choice is worth 4, every Bump reward
        // used here has no stated floor (ManaRegen), and levels 31-40 are
        // all Identity.
        private static RewardTrackDefinition ConformingFixture()
        {
            var entries = new List<(int Level, TrackEntry Entry)>
            {
                (2, new TrackEntry(TrackReward.ManaRegen, 1)),
                (3, new TrackEntry(TrackReward.UnlockSkill, 0, skillId: "v1")),
                (4, new TrackEntry(TrackReward.StatPoint, 4)),
                (5, new TrackEntry(TrackReward.ManaRegen, 1)),
                (6, new TrackEntry(TrackReward.StatPoint, 4)),
                (7, new TrackEntry(TrackReward.ManaRegen, 1)),
                (8, new TrackEntry(TrackReward.Respec, 0)),
                (9, new TrackEntry(TrackReward.StatPoint, 4)),
                (10, new TrackEntry(TrackReward.UnlockSkill, 0, skillId: "v2")),
                (11, new TrackEntry(TrackReward.StatPoint, 4)),
                (12, new TrackEntry(TrackReward.ManaRegen, 1)),
                (13, new TrackEntry(TrackReward.StatPoint, 4)),
                (14, new TrackEntry(TrackReward.ManaRegen, 1)),
                (15, new TrackEntry(TrackReward.SpellCostDelta, 1)),
                (16, new TrackEntry(TrackReward.StatPoint, 4)),
                (17, new TrackEntry(TrackReward.ManaRegen, 1)),
                (18, new TrackEntry(TrackReward.StatPoint, 4)),
                (19, new TrackEntry(TrackReward.ManaRegen, 1)),
                (20, new TrackEntry(TrackReward.UnlockSkill, 0, skillId: "v3")),
                (21, new TrackEntry(TrackReward.StatPoint, 4)),
                (22, new TrackEntry(TrackReward.ManaRegen, 1)),
                (23, new TrackEntry(TrackReward.StatPoint, 4)),
                (24, new TrackEntry(TrackReward.ManaRegen, 1)),
                (25, new TrackEntry(TrackReward.SecondLife, 1)),
                (26, new TrackEntry(TrackReward.SignatureAbsorbPerPoint, 2)),
                (27, new TrackEntry(TrackReward.StatPoint, 4)),
                (28, new TrackEntry(TrackReward.ManaRegen, 1)),
                (29, new TrackEntry(TrackReward.StatPoint, 4)),
                (30, new TrackEntry(TrackReward.UnlockSkill, 0, skillId: "v4")),
            };

            for (int level = 31; level <= 40; level++)
            {
                entries.Add((level, new TrackEntry(TrackReward.Identity, 0)));
            }

            return RewardTrackDefinition.Build("fixture", entries.ToArray());
        }

        // ---- each rule refuses a minimal offending fixture ----

        [Test]
        public void AdjacentNodesOfOneKind_AreRejected()
        {
            var track = Build(
                (2, new TrackEntry(TrackReward.StatPoint, 4)),
                (3, new TrackEntry(TrackReward.StatPoint, 4)));

            var errors = RewardTrackNodeValidation.ValidateAgainstCap(Label, track, 40);

            StringAssert.Contains("levels 2 and 3", Join(errors));
            StringAssert.Contains("no two", Join(errors));
        }

        [Test]
        public void AnAbilityAt3NotFollowedByAChoice_IsRejected()
        {
            var track = Build(
                (3, new TrackEntry(TrackReward.UnlockSkill, 0, skillId: "v1")),
                (4, new TrackEntry(TrackReward.ManaRegen, 1)));

            var errors = RewardTrackNodeValidation.ValidateAgainstCap(Label, track, 40);

            StringAssert.Contains("level 3 is an Ability", Join(errors));
            StringAssert.Contains("not a Choice", Join(errors));
        }

        [Test]
        public void ANonIdentityKindAbove30_IsRejected()
        {
            var track = Build((35, new TrackEntry(TrackReward.MaxHealth, 40)));

            var errors = RewardTrackNodeValidation.ValidateAgainstCap(Label, track, 40);

            StringAssert.Contains("level 35", Join(errors));
            StringAssert.Contains("must be Identity", Join(errors));
        }

        [Test]
        public void UtilityAbove30_IsRejected()
        {
            var track = Build((35, new TrackEntry(TrackReward.Respec, 0)));

            var errors = RewardTrackNodeValidation.ValidateAgainstCap(Label, track, 40);

            StringAssert.Contains("level 35", Join(errors));
            StringAssert.Contains("must be Identity", Join(errors));
        }

        [Test]
        public void AChoiceWorthOtherThanFour_IsRejected()
        {
            var track = Build((4, new TrackEntry(TrackReward.StatPoint, 5)));

            var errors = RewardTrackNodeValidation.ValidateAgainstCap(Label, track, 40);

            StringAssert.Contains("level 4 is a Choice node worth 5", Join(errors));
        }

        [Test]
        public void AMaxHealthBumpUnderThirty_IsRejected()
        {
            var track = Build((2, new TrackEntry(TrackReward.MaxHealth, 29)));

            var errors = RewardTrackNodeValidation.ValidateAgainstCap(Label, track, 40);

            StringAssert.Contains("below the floor of 30", Join(errors));
        }

        [Test]
        public void AMaxManaBumpUnderSix_IsRejected()
        {
            var track = Build((2, new TrackEntry(TrackReward.MaxMana, 5)));

            var errors = RewardTrackNodeValidation.ValidateAgainstCap(Label, track, 40);

            StringAssert.Contains("below the floor of 6", Join(errors));
        }

        [Test]
        public void AnElementalDamagePercentBumpUnderFive_IsRejected()
        {
            var track = Build((2, new TrackEntry(TrackReward.ElementalDamagePercent, 4)));

            var errors = RewardTrackNodeValidation.ValidateAgainstCap(Label, track, 40);

            StringAssert.Contains("below the floor of 5", Join(errors));
        }

        [Test]
        public void AFuryGainStepUnderFive_IsRejected()
        {
            var track = Build((2, new TrackEntry(TrackReward.FuryGainOnAttack, 4)));

            var errors = RewardTrackNodeValidation.ValidateAgainstCap(Label, track, 40);

            StringAssert.Contains("below the floor of 5", Join(errors));
        }

        [Test]
        public void AWoolPerTurnBumpUnderOne_IsRejected()
        {
            var track = Build((2, new TrackEntry(TrackReward.SignatureGainPerTurn, 0)));

            var errors = RewardTrackNodeValidation.ValidateAgainstCap(Label, track, 40);

            StringAssert.Contains("below the floor of 1", Join(errors));
        }

        [Test]
        public void ABumpWithNoStatedFloor_IsNeverRejectedOnMagnitude()
        {
            // SignatureCapacity has no authored floor -- 1 must be legal.
            // declaredCap 2, not 40, so rule F's cap check does not also
            // fire and muddy what this test is isolating.
            var track = Build((2, new TrackEntry(TrackReward.SignatureCapacity, 1)));

            var errors = RewardTrackNodeValidation.ValidateAgainstCap(Label, track, 2);

            Assert.AreEqual(0, errors.Count, Join(errors));
        }

        [Test]
        public void AHighestLevelOtherThanTheDeclaredCap_IsRejected()
        {
            var track = Build((35, new TrackEntry(TrackReward.Identity, 0)));

            var errors = RewardTrackNodeValidation.ValidateAgainstCap(Label, track, declaredCap: 40);

            StringAssert.Contains("highest rewarding level is 35", Join(errors));
            StringAssert.Contains("declared cap 40", Join(errors));
        }
    }
}
