using NUnit.Framework;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // RewardTrackDefinition: a reward track materialised for one character.
    public class RewardTrackDefinitionTests
    {
        // THE DEFAULT TABLE'S TWO TOTALS: 13 Choice nodes at 4 stat points
        // each = 52; 14 Bump nodes at 30 max health each = 420. MaxHealth is
        // read via CollectedTotal rather than GrantedBetween -- it is not a
        // grant (RewardTrackTests.OnlyStatPointIsAGrant).
        //
        // The default track is an explicit 39-row table (see RawTrackLevel):
        // a Choice node is worth exactly 4 and a MaxHealth Bump at least 30,
        // both of which RewardTrackNodeValidation enforces on every
        // authored track. This is still the placeholder
        // for a character nobody designed one for, and every character on
        // the roster has a real track.
        [Test]
        public void TheDefaultTrackPaysFiftyTwoStatPointsAndFourHundredTwentyMaxHealth()
        {
            var track = RewardTrackDefinition.Default("bear");

            Assert.AreEqual(52, track.GrantedBetween(TrackReward.StatPoint, 1, RewardTrack.MaxLevel),
                "13 Choice nodes at 4 each");
            Assert.AreEqual(420, track.CollectedTotal(TrackReward.MaxHealth, RewardTrack.MaxLevel),
                "14 Bump nodes at 30 each");
        }

        // The generated default is not authored content and never passes
        // through RewardTrackEntryResolver, so nothing else checks it
        // against the node-kind rules every authored track must satisfy.
        // Checked here because the default is the obvious thing to copy
        // when authoring a new track, and one that could not itself survive
        // the validator would be a trap.
        [Test]
        public void TheDefaultTrackSatisfiesTheNodeRules()
        {
            var errors = RewardTrackNodeValidation.Validate("the generated default",
                RewardTrackDefinition.Default("bear"));

            CollectionAssert.IsEmpty(errors, string.Join(" | ", errors));
        }

        // Every level from 2 to MaxLevel pays something -- the format rule
        // RewardTrackEntryResolver enforces on authored content, asserted
        // here against the one track no resolver ever sees.
        [Test]
        public void TheDefaultTrackFillsEveryLevel()
        {
            var track = RewardTrackDefinition.Default("bear");

            for (int level = RewardTrack.StartingLevel + 1; level <= RewardTrack.MaxLevel; level++)
            {
                Assert.IsTrue(track.At(level).IsSomething, $"level {level} carries nothing");
            }
        }

        // A landmark the screen draws large with nothing on it is the failure
        // this stops -- the first validation rule,
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
                (RewardTrack.MaxLevel, new TrackEntry(TrackReward.SignatureCapacity, 5)),
            };

            var track = RewardTrackDefinition.Build("fixture", milestones);

            Assert.AreEqual(10, track.CollectedTotal(TrackReward.SignatureCapacity, RewardTrack.MaxLevel),
                "two SignatureCapacity entries did not sum once both were behind the watermark");
            Assert.AreEqual(5, track.CollectedTotal(TrackReward.SignatureCapacity, RewardTrack.MaxLevel - 1),
                "the top entry was collected before its level was reached");
        }

        // What has not been reached yet is not collected. SecondLife is the
        // one kind on the default track that can only ever appear at its
        // single milestone (level 25) -- rule 3
        // refuses a one-shot capability as filler, so unlike MaxHealth (which
        // also lands as filler well before level 10) there is no earlier
        // entry that could make this pass by accident.
        [Test]
        public void NothingIsCollectedAboveTheWatermark()
        {
            var track = RewardTrackDefinition.Default("bear");

            Assert.AreEqual(0, track.CollectedTotal(TrackReward.SecondLife, 24),
                "level 25's second life was collected before level 25 was reached");
            Assert.AreEqual(1, track.CollectedTotal(TrackReward.SecondLife, 25),
                "level 25's second life was not collected once it was reached");
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

            var track = RewardTrackDefinition.Build("fixture", entries);

            var atMilestone = track.CollectedElementalTotals(10);
            Assert.AreEqual(14, atMilestone[DamageType.Fire], "both filler entries plus the milestone");

            var beforeMilestone = track.CollectedElementalTotals(9);
            Assert.AreEqual(4, beforeMilestone[DamageType.Fire], "the milestone had not been reached yet");
        }

        // A new TrackReward compiles, resolves a card visual, and displays as
        // nothing -- the failure this walk exists to catch, over every kind at
        // once rather than trusting each caption template to have used its own
        // selector. The entry carries every optional field a caption might read
        // (Against/SkillDisplayName/ResourceDisplayName) so a template that
        // forgot to read its own selector cannot pass by accident on a blank
        // fallback.
        //
        // CardVisualKeyFor, NOT CardArtKeyFor: the card draws a
        // medallion for the nine kinds that have an honest one and a word for
        // the twelve that do not, so "has a card art key" is no longer the
        // question -- "has something to draw" is. See RewardTrackScreenTests
        // for the distinctness half of the same rule.
        [Test]
        public void EveryRewardKindResolvesACardVisualAndAName()
        {
            foreach (TrackReward reward in System.Enum.GetValues(typeof(TrackReward)))
            {
                if (reward == TrackReward.None) continue;

                Assert.IsNotEmpty(RewardTrackLayout.IconFor(reward), $"{reward} has no rail icon key");
                Assert.IsNotEmpty(RewardTrackLayout.CardVisualKeyFor(reward),
                    $"{reward} has neither a card medallion nor a card glyph");

                var entry = new TrackEntry(reward, 5, against: DamageType.Fire,
                    skillId: "fixture_skill", skillDisplayName: "Fixture Skill",
                    resourceDisplayName: "Fixture Resource");
                Assert.IsNotEmpty(RewardTrackNames.Of(entry), $"{reward} has no display name");
            }
        }
    }
}
