using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // The three R0 reveal pages (ReckoningPageFixtures) are what the reveal is
    // judged on (tools/gif.ps1 reveal, ReckoningCaptureTests), so each must
    // stay the page it is named for: the plain deal, a cache with a top-tier
    // ceremony, and a five-card row with a ghost on every card.
    public class ReckoningRevealFixtureTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-reveal-fixture-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            SaveData.TestSquadOfThreeEnabled = true;
        }

        [TearDown]
        public void Restore()
        {
            SaveData.TestSquadOfThreeEnabled = null;
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static RevealSchedule ScheduleOf(PendingPage page) =>
            RevealSchedule.For(ReckoningPageFixtures.RevealCardsOf(SaveSlotManager.CurrentSave, page), page.Choices, 1f,
                page.SourceClass);

        [Test]
        public void TheNoCachePage_TakesTheDealPath()
        {
            var page = ReckoningPageFixtures.NoCachePage()[0];
            var schedule = ScheduleOf(page);

            Assert.IsFalse(schedule.HasCache);
            Assert.IsEmpty(schedule.Beats.Where(b => b.Kind == RevealBeatKind.Seal).ToArray());
            Assert.AreEqual(page.Cards.Count, schedule.Beats.Count(b => b.Kind == RevealBeatKind.Deal));
            Assert.AreEqual(page.Cards.Count, schedule.Beats.Count(b => b.Kind == RevealBeatKind.Flip),
                "dealt face-down, flipped one by one");
        }

        [Test]
        public void TheCacheTopTierPage_HasACacheAndATopTierCeremony()
        {
            var schedule = ScheduleOf(ReckoningPageFixtures.CacheTopTierPages()[0]);

            Assert.IsTrue(schedule.HasCache);
            Assert.IsTrue(schedule.HasTopLiveUpgrade);
            Assert.IsNotEmpty(schedule.Beats
                .Where(b => b.Kind == RevealBeatKind.Anticipate && b.Style == LandStyle.Burst).ToArray());
        }

        [Test]
        public void TheFiveGhostsPage_HasAGhostOnEveryCard()
        {
            var cards = ReckoningPageFixtures.FiveGhostsPages()[0].Cards;
            var save = SaveSlotManager.CurrentSave;

            Assert.AreEqual(5, cards.Count);
            for (int i = 0; i < cards.Count; i++)
                Assert.IsNotNull(RewardCardFace.OfGear(save, cards[i]).GhostKey, $"card {i} has no ghost");
        }
    }
}
