using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.PlayModeTests
{
    // ONE SKILL-UNLOCK FILTER, NOT TWO.
    //
    // ContentDatabase.AvailableSkillsFor (the in-run kit, a real Character
    // record) and FightEncounterAdapter.KitFor(CharacterDefinition, ...)
    // (the no-Character-record route dev-forced/preview fights use) both
    // need "owned by this character, player-selectable, reachable by
    // level" -- and until ContentDatabase.SkillsUnlockedByLevel existed, the
    // second of those restated the first by hand. The last time that
    // happened the hand-rolled copy dropped the level filter entirely and
    // handed a level-2 run the whole talent tree (KitFor's own header has
    // the balance-bot story). This is the test that would have caught it:
    // both routes are asked the same question, at every reachable level, and
    // required to agree.
    public class SkillUnlockFilterTests
    {
        // The highest UnlockLevel actually authored below the talent-grant
        // sentinel. Includes the 999-levelled talent-granted skills
        // (Provoke, Headbutt, ...) on purpose -- SkillsUnlockedByLevel does
        // not special-case them, so a level that reaches 999 is exactly as
        // real an input as one that reaches 6.
        private static int HighestFiniteUnlockLevel()
        {
            return ContentDatabase.Skills
                .Where(s => s != null && s.Data.UnlockLevel < int.MaxValue)
                .Select(s => s.Data.UnlockLevel)
                .DefaultIfEmpty(1)
                .Max();
        }

        [Test]
        public void KitForDefinitionAgreesWithTheSharedFunctionAtEveryLevel()
        {
            int maxLevel = HighestFiniteUnlockLevel();
            Assert.Greater(maxLevel, 1, "no skill unlocks above level 1 -- this test would pass vacuously.");

            foreach (var definition in ContentDatabase.Characters)
            {
                if (definition == null) continue;

                for (int level = 1; level <= maxLevel; level++)
                {
                    var fromKitFor = FightEncounterAdapter.SkillIdsForDefinitionForTest(definition, level);
                    var fromSharedFunction = ContentDatabase.SkillsUnlockedByLevel(definition.id, level)
                        .Select(s => s.id)
                        .ToList();

                    CollectionAssert.AreEqual(fromSharedFunction, fromKitFor,
                        $"'{definition.id}' at level {level}: KitFor(CharacterDefinition, ...) and " +
                        "SkillsUnlockedByLevel disagree, which means one of them stopped calling the other.");
                }
            }
        }

        [Test]
        public void TheSharedFunctionAgreesWithAvailableSkillsForAFreshLevelOneCharacter()
        {
            foreach (var definition in ContentDatabase.Characters)
            {
                if (definition == null) continue;

                // A brand new Character record: level 1, no talents taken, no
                // unlockedSkillIds, no run (so no learned books either), and
                // claimedTrackLevel 0. Every extra route AvailableSkillsFor
                // unions in contributes nothing here, so its answer should be
                // exactly the base predicate's.
                //
                // THE TRACK ROUTE IS THE ONE WORTH NAMING, because it is the
                // newest and the only one that could quietly start
                // contributing: it reads SkillsCollected(claimedTrackLevel),
                // and a fresh character's watermark is 0, which is below the
                // first level any track pays. An implementer who reads the
                // track off `level` instead of the watermark breaks this test
                // and nothing else -- which is precisely why
                // docs/PLAN_REWARD_TRACKS.md §3k made it a stated invariant of
                // P4 rather than an accident that happened to hold.
                var fresh = new Character(definition.id);

                var fromSharedFunction = ContentDatabase.SkillsUnlockedByLevel(definition.id, 1)
                    .Select(s => s.id)
                    .ToList();
                var fromAvailableSkillsFor = ContentDatabase.AvailableSkillsFor(fresh)
                    .Select(s => s.id)
                    .ToList();

                CollectionAssert.AreEqual(fromSharedFunction, fromAvailableSkillsFor,
                    $"'{definition.id}': a fresh level-1 Character sees a different skill list through " +
                    "AvailableSkillsFor than SkillsUnlockedByLevel returns, with nothing to account for the gap.");
            }
        }

        // A LITERAL, not just "the two sides agree with each other" -- two
        // calls into the same code compared to each other proves nothing
        // about either being right. Pinned against Shawn (skills.json's
        // "sheep"): shear is his whole starting kit, and nothing else he
        // owns is reachable by level at all -- the book spells are bookOnly,
        // and everything else on his row is at 999, handed over by his
        // reward track or by a talent.
        [Test]
        public void ShawnsLevelOneKitIsExactlyShear()
        {
            var sheep = ContentDatabase.GetCharacter("sheep");
            Assert.IsNotNull(sheep, "'sheep' (Shawn) is missing from characters.json.");

            var ids = ContentDatabase.SkillsUnlockedByLevel("sheep", 1).Select(s => s.id).ToList();

            CollectionAssert.AreEqual(new[] { "shear" }, ids);
        }

        // THE LADDER IS GONE (progression v2 phase 4, contract 6). This test
        // used to assert that levelling to 6 added Woolgathering and
        // Battering Ram; both are at 999 now and arrive from the track's own
        // UnlockSkill nodes instead. What replaces it is the assertion that
        // makes the removal real rather than assumed: levelling to the cap
        // adds NOTHING to anybody's kit, on every character in content, so
        // there is no second route left for a track node to race.
        [Test]
        public void LevellingToTheCapAddsNothingToAnybodysKit()
        {
            foreach (var definition in ContentDatabase.Characters)
            {
                var atOne = ContentDatabase.SkillsUnlockedByLevel(definition.id, 1)
                    .Select(s => s.id).ToList();
                var atCap = ContentDatabase.SkillsUnlockedByLevel(definition.id, RewardTrack.MaxLevel)
                    .Select(s => s.id).ToList();

                CollectionAssert.AreEqual(atOne, atCap,
                    $"'{definition.id}': levelling still hands over a skill, so the unlockLevel ladder " +
                    "survives beside the reward track.");
            }
        }
    }
}
