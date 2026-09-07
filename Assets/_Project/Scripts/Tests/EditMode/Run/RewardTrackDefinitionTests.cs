using NUnit.Framework;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // RewardTrackDefinition: a reward track materialised for one character.
    // docs/PLAN_REWARD_TRACKS.md P3.
    public class RewardTrackDefinitionTests
    {
        // THE DEFAULT TABLE'S TWO TOTALS: 40 filler stat points + level 80's
        // ten = 50; 47 filler MaxHealth at 2 each + 9 milestones at 15 each =
        // 229. MaxHealth is read via CollectedTotal rather than
        // GrantedBetween -- it is not a grant (RewardTrackTests.
        // OnlyStatPointIsAGrant).
        [Test]
        public void TheDefaultTrackPaysFiftyStatPointsAndTwoHundredTwentyNineMaxHealth()
        {
            var track = RewardTrackDefinition.Default("bear");

            Assert.AreEqual(50, track.GrantedBetween(TrackReward.StatPoint, 1, RewardTrack.MaxLevel),
                "40 filler singles plus level 80's ten");
            Assert.AreEqual(229, track.CollectedTotal(TrackReward.MaxHealth, RewardTrack.MaxLevel),
                "9 milestone nodes at 15 plus 47 filler nodes at 2");
        }

        // A landmark the screen draws large with nothing on it is the failure
        // this stops -- docs/PLAN_REWARD_TRACKS.md §4's first validation rule,
        // checked here against the generated default rather than left only to
        // the content resolver's authored-track validation (P2).
        [Test]
        public void EveryMilestoneLevelCarriesSomething()
        {
            var track = RewardTrackDefinition.Default("bear");

            foreach (int level in RewardTrack.MilestoneLevels)
            {
                Assert.IsTrue(track.At(level).IsSomething, $"milestone level {level} carries nothing");
            }
        }

        // CollectedTotal SUMS rather than replacing -- two SignatureCapacity 5
        // entries add up to 10 once both are behind the watermark, and only
        // the first counts while the watermark sits between them. Built via
        // RewardTrackDefinition.Build directly (an empty filler mix), rather
        // than through Default, so this is a fixture track and not the shipped
        // one.
        [Test]
        public void CollectedTotalSumsRatherThanReplacing()
        {
            var milestones = new (int Level, TrackEntry Entry)[]
            {
                (25, new TrackEntry(TrackReward.SignatureCapacity, 5)),
                (100, new TrackEntry(TrackReward.SignatureCapacity, 5)),
            };

            var track = RewardTrackDefinition.Build("fixture", milestones,
                System.Array.Empty<(TrackEntry Entry, int Count)>());

            Assert.AreEqual(10, track.CollectedTotal(TrackReward.SignatureCapacity, 100),
                "two SignatureCapacity entries did not sum once both were behind the watermark");
            Assert.AreEqual(5, track.CollectedTotal(TrackReward.SignatureCapacity, 99),
                "the level-100 entry was collected before level 100 was reached");
        }

        // What has not been reached yet is not collected. SecondLife is the
        // one kind on the default track that can only ever appear at its
        // single milestone (level 90) -- rule 3 in docs/PLAN_REWARD_TRACKS.md
        // §4 refuses a one-shot capability as filler, so unlike MaxHealth
        // (which also lands as filler well before level 10) there is no
        // earlier entry that could make this pass by accident.
        [Test]
        public void NothingIsCollectedAboveTheWatermark()
        {
            var track = RewardTrackDefinition.Default("bear");

            Assert.AreEqual(0, track.CollectedTotal(TrackReward.SecondLife, 89),
                "level 90's second life was collected before level 90 was reached");
            Assert.AreEqual(1, track.CollectedTotal(TrackReward.SecondLife, 90),
                "level 90's second life was not collected once it was reached");
        }

        // CollectedElementalTotals is ContentDatabase.Effective.ModifierEffects'
        // single-pass replacement for calling CollectedTotal(reward, against,
        // level) once per DamageType member -- pinned against a fixture built
        // the same way CollectedTotalSumsRatherThanReplacing is, three
        // Fire entries at levels 5, 7 and 10 (2 + 2 + 10) standing in for two
        // filler placements and one milestone.
        [Test]
        public void CollectedElementalTotalsSumsOneElementInOnePass()
        {
            var entries = new (int Level, TrackEntry Entry)[]
            {
                (5, new TrackEntry(TrackReward.ElementalDamagePercent, 2, DamageType.Fire)),
                (7, new TrackEntry(TrackReward.ElementalDamagePercent, 2, DamageType.Fire)),
                (10, new TrackEntry(TrackReward.ElementalDamagePercent, 10, DamageType.Fire)),
            };

            var track = RewardTrackDefinition.Build("fixture", entries,
                System.Array.Empty<(TrackEntry Entry, int Count)>());

            var atMilestone = track.CollectedElementalTotals(10);
            Assert.AreEqual(14, atMilestone[DamageType.Fire], "both filler entries plus the milestone");

            var beforeMilestone = track.CollectedElementalTotals(9);
            Assert.AreEqual(4, beforeMilestone[DamageType.Fire], "the milestone had not been reached yet");
        }

        // A new TrackReward compiles, resolves an art key, and displays as
        // nothing -- the failure this walk exists to catch, over all twelve
        // kinds at once rather than trusting each caption template to have
        // used its own selector. The entry carries every optional field a
        // caption might read (Against/SkillDisplayName/ResourceDisplayName)
        // so a template that forgot to read its own selector cannot pass by
        // accident on a blank fallback.
        [Test]
        public void EveryRewardKindResolvesAnArtKeyAndAName()
        {
            foreach (TrackReward reward in System.Enum.GetValues(typeof(TrackReward)))
            {
                if (reward == TrackReward.None) continue;

                Assert.IsNotEmpty(RewardTrackLayout.IconFor(reward), $"{reward} has no rail icon key");
                Assert.IsNotEmpty(RewardTrackLayout.CardArtKeyFor(reward), $"{reward} has no card art key");

                var entry = new TrackEntry(reward, 5, against: DamageType.Fire,
                    skillId: "fixture_skill", skillDisplayName: "Fixture Skill",
                    resourceDisplayName: "Fixture Resource");
                Assert.IsNotEmpty(RewardTrackNames.Of(entry), $"{reward} has no display name");
            }
        }
    }
}
