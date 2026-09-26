using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;

namespace PrincesPalace.PlayModeTests
{
    // ValidateContent()'s achievement -> enemy rule, against the real
    // catalogue. Nothing in this tree called ValidateContent() from a test
    // before this file existed. The six cross-catalogue id rules beside it
    // now have the same split (AUDIT #77): CatalogueCrossChecksTests
    // (EditMode) for each rule's refusal, ContentValidationWiringTests for
    // the whole method over shipped content.
    //
    // Real content only, and that is a constraint rather than a choice:
    // ValidateContent() reads ContentDatabase's Resources-loaded catalogue,
    // which only a PlayMode test can see (this assembly references Core;
    // EditMode's Domain.Tests assembly references Domain alone). The two
    // bad-input branches -- a parameter naming no enemy, and one naming an
    // enemy that is not a boss -- are exercised as pure literals against
    // AchievementProgress.ValidateDefeatSpecificBossParameter in
    // AchievementTableTests instead, since that needs no Resources-loaded
    // content at all: constructing a synthetic AchievementDefinition here
    // would need AchievementDefinition.SetData, which is internal to the
    // Editor assembly and reachable from no test assembly in this tree.
    public class AchievementBossValidationTests
    {
        [Test]
        public void RealContentNamesOnlyRealBosses()
        {
            var errors = ContentDatabase.ValidateContent();

            var bossErrors = errors.Where(e => e.Contains("DefeatSpecificBoss")).ToList();

            Assert.IsEmpty(bossErrors,
                "real achievements.json names something ValidateContent's new rule rejects: " +
                string.Join(" | ", bossErrors));
        }

        [Test]
        public void TheForestWardenAchievementNamesARealBoss()
        {
            // Pinned rather than just "no errors": proves the new loop is
            // actually looking at forest_warden, not passing vacuously
            // because it never ran or _enemies was empty.
            var forestWarden = ContentDatabase.GetEnemy("forest_warden");

            Assert.IsNotNull(forestWarden,
                "forest_warden is missing from enemies.json, but first_forest_boss names it.");
            Assert.IsTrue(forestWarden.Data.IsBoss,
                "forest_warden is not flagged isBoss; first_forest_boss can never be earned.");
        }
    }
}
