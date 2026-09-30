using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.PlayModeTests
{
    // THE SOURCE OF EACH SKILL, from the one predicate that also decides what a
    // fight offers (ContentDatabase.SkillSourcesFor). The CHARACTER tab groups
    // its SKILLS list by this, so a skill filed under the wrong route would sit
    // under a heading that names somewhere it did not come from.
    public class SkillSourcesTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-skill-sources-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        [TearDown]
        public void RestoreSaveRoot()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static string[] Filed(Character character, SkillSource source) =>
            ContentDatabase.SkillSourcesFor(character)
                .Where(e => e.Source == source)
                .Select(e => e.Skill.id)
                .ToArray();

        // Bjorn carries fourteen skills in content; thirteen are authored at an
        // unreachable level and handed out by talents, one is his by level. The old list counted
        // only the talent-granted ones and read "0 known".
        [Test]
        public void ABrandNewBjornHasHisInnateSlamAndNothingElse()
        {
            var bjorn = new Character { definitionId = "bear", level = 1 };

            CollectionAssert.AreEqual(new[] { "placeholder_brawler_slam" }, Filed(bjorn, SkillSource.Innate));
            Assert.AreEqual(1, ContentDatabase.SkillSourcesFor(bjorn).Count);
        }

        [Test]
        public void ATalentsSkillIsFiledUnderTalents()
        {
            var shawn = new Character { definitionId = "sheep", level = 1 };
            shawn.unlockedTalentIds.Add("sheep_ram_provoke_1");

            CollectionAssert.AreEqual(new[] { "provoke" }, Filed(shawn, SkillSource.Talent));
            CollectionAssert.DoesNotContain(Filed(shawn, SkillSource.Innate), "provoke");
        }

        [Test]
        public void ASkillTaughtOutrightIsFiledAsTaught()
        {
            var shawn = new Character { definitionId = "sheep", level = 1 };
            shawn.unlockedSkillIds.Add("headbutt");

            CollectionAssert.AreEqual(new[] { "headbutt" }, Filed(shawn, SkillSource.Taught));
        }

        [Test]
        public void ABookLearnedThisDescentIsFiledUnderSpellBooks()
        {
            RunManager.StartRun(4242UL);
            RunManager.Run.learnedSpells.Add(
                new LearnedSpellEntry { characterId = "sheep", skillId = "mud_burst", slot = 0 });

            var shawn = new Character { definitionId = "sheep", level = 1 };

            CollectionAssert.AreEqual(new[] { "mud_burst" }, Filed(shawn, SkillSource.SpellBook));
        }

        [Test]
        public void ATrackSkillIsFiledUnderTheRewardTrackOnceCollected()
        {
            var shawn = new Character { definitionId = "sheep", level = 1 };
            CollectionAssert.DoesNotContain(Filed(shawn, SkillSource.RewardTrack), "woolgathering",
                "nothing is collected yet, so the track has paid nothing");

            shawn.claimedTrackLevel = RewardTrack.MaxLevel;
            CollectionAssert.Contains(Filed(shawn, SkillSource.RewardTrack), "woolgathering");
        }

        // The union has one reader. Whatever the tab lists is what the fight
        // offers, in the same order.
        [Test]
        public void TheSourcedListIsTheSameSkillsInTheSameOrderAsAvailableSkillsFor()
        {
            var shawn = new Character { definitionId = "sheep", level = 5 };
            shawn.unlockedTalentIds.Add("sheep_ram_provoke_1");
            shawn.claimedTrackLevel = RewardTrack.MaxLevel;

            CollectionAssert.AreEqual(
                ContentDatabase.AvailableSkillsFor(shawn).Select(s => s.id).ToList(),
                ContentDatabase.SkillSourcesFor(shawn).Select(e => e.Skill.id).ToList());
            Assert.Greater(ContentDatabase.SkillSourcesFor(shawn).Count, 3, "fixture: the list is too short to prove an order");
        }
    }
}
