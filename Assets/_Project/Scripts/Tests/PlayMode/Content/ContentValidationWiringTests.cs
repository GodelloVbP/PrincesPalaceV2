using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;

namespace PrincesPalace.PlayModeTests
{
    // The other half of CatalogueCrossChecksTests (EditMode, AUDIT #77): those
    // prove each cross-catalogue predicate refuses its own broken fixture;
    // this proves ContentDatabase.ValidateContent() runs over the shipped,
    // Resources-loaded catalogue and finds nothing. PlayMode because only this
    // assembly can see Resources content -- see AchievementBossValidationTests'
    // header for why no test can build a synthetic catalogue instead.
    public class ContentValidationWiringTests
    {
        [Test]
        public void ShippedContentPassesEveryWholeCatalogueRule()
        {
            var errors = ContentDatabase.ValidateContent();

            Assert.IsEmpty(errors, "ValidateContent refuses shipped content: " + string.Join(" | ", errors));
        }

        [Test]
        public void TheRulesHadSomethingToCheck()
        {
            // An empty pass over empty catalogues would be vacuous: pin that
            // every catalogue the six id rules walk actually loaded, and that
            // at least one enemy ability and one talent reference exist for
            // them to resolve.
            Assert.IsNotEmpty(ContentDatabase.Characters);
            Assert.IsNotEmpty(ContentDatabase.Enemies);
            Assert.IsNotEmpty(ContentDatabase.Skills);
            Assert.IsNotEmpty(ContentDatabase.Items);
            Assert.IsNotEmpty(ContentDatabase.Talents);
            Assert.IsTrue(ContentDatabase.Enemies.Any(e => e.Data?.Abilities != null
                                                           && e.Data.Abilities.Any(a => !string.IsNullOrEmpty(a.SkillId))),
                "no enemy names an ability skill, so the enemy -> skill rule checked nothing");
        }
    }
}
