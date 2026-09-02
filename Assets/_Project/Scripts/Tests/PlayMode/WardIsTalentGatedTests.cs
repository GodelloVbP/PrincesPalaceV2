using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.PlayModeTests
{
    // A SKILL AUTHORED OUTSIDE THE LEVEL LADDER IS NOT IN THE KIT UNTIL
    // SOMETHING GRANTS IT.
    //
    // skills.json marks a skill "granted rather than earned" by authoring it
    // at unlockLevel 999 -- Ward, Shatter, Wail, the three Gifts, Provoke,
    // Headbutt and Black Ram Mode are all reached through a talent's
    // grantsSkillId and by no other route. ContentDatabase.AvailableSkillsFor
    // has always known that. It also had no production caller: the fight
    // built its strip in FightEncounterAdapter.KitFor from a second,
    // hand-rolled query that filtered on characterId alone, so every one of
    // those skills was on the strip of every character from level 1 -- along
    // with Golden Fleece, seven levels early.
    //
    // The balance bot is what found it, from the outside: a level-2 run with
    // an empty talentIds list recorded fleece_ward, gift_haste, gift_fury,
    // gift_mana, headbutt, provoke and shatter in skillsUsed, and the
    // defensive policy spent 41% of its deep-fight turns warding with a Ward
    // it had never bought.
    //
    // Ward is the case pinned here because it is the one the bot leaned on
    // hardest, but the assertion is about the RULE -- the last test walks
    // every 999 skill, so the next one authored is covered the day it lands.
    public class WardIsTalentGatedTests
    {
        private const string SheepId = "sheep";
        private const string WardSkillId = "fleece_ward";
        private const string WardTalentId = "sheep_lamb_ward_1";

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-wardgate-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static List<string> OneEnemy() =>
            ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList();

        // What this character can actually press, asked of the built session
        // rather than of the content -- the whole point is that the two used
        // to disagree.
        private static List<string> StripOf(Character character)
        {
            var built = FightEncounterAdapter.Build(
                new List<string> { character.definitionId },
                OneEnemy(),
                new Domain.Rng.SeededRandom(7),
                partyCharacters: new List<Character> { character });

            Assert.IsNotNull(built, "fixture: the fight built");

            var kit = built.Session.KitFor(built.Party[0]);
            Assert.IsNotNull(kit, "fixture: the party member has a kit");

            return kit.Skills.Select(s => s.Id).ToList();
        }

        // Ward used to have no cooldown at all -- castable every turn, and
        // with its talent strand refunding the wool, free every turn too.
        // 2 is the shortest real wait SkillEntryResolver allows (it refuses
        // 1 as indistinguishable from 0 -- see SkillEntryResolver).
        [Test]
        public void WardHasARealCooldown()
        {
            var ward = ContentDatabase.GetSkill(WardSkillId);
            Assert.IsNotNull(ward, "fixture: content still has " + WardSkillId);
            Assert.AreEqual(2, ward.cooldownTurns,
                "Ward is meant to be castable every other turn, not every turn");
        }

        [Test]
        public void WithoutTheTalent_ThereIsNoWardOnTheStrip()
        {
            Assert.IsNotNull(ContentDatabase.GetSkill(WardSkillId),
                "fixture: content still has " + WardSkillId);

            var bare = new Character(SheepId);
            CollectionAssert.DoesNotContain(StripOf(bare), WardSkillId,
                "a character who never bought the Fragile Lamb ward node is carrying Ward");
        }

        [Test]
        public void WithTheTalent_WardIsOnTheStrip()
        {
            var talent = ContentDatabase.GetTalent(WardTalentId);
            Assert.IsNotNull(talent, "fixture: content still has " + WardTalentId);
            Assert.AreEqual(WardSkillId, talent.grantsSkillId,
                "fixture: " + WardTalentId + " is still the node that grants Ward");

            var warded = new Character(SheepId);
            warded.unlockedTalentIds.Add(WardTalentId);

            CollectionAssert.Contains(StripOf(warded), WardSkillId,
                "buying the ward node did not put Ward on the strip");
        }

        // THE LEVEL LADDER, the other half of the same filter. Golden Fleece
        // is authored at unlockLevel 8; a level-1 character had it.
        [Test]
        public void ASkillAboveTheCharactersLevel_IsNotOnTheStrip()
        {
            var early = ContentDatabase.Skills.FirstOrDefault(s =>
                s != null && s.characterId == SheepId && s.playerSelectable
                && s.unlockLevel > 1 && s.unlockLevel < 999);

            Assert.IsNotNull(early, "fixture: sheep still has a skill unlocked above level 1");

            var fresh = new Character(SheepId) { level = 1 };
            CollectionAssert.DoesNotContain(StripOf(fresh), early.id,
                early.id + " is authored at level " + early.unlockLevel + " and a level 1 "
                + "character is carrying it");
        }

        // THE RULE, not the instance. Every skill the content marks as
        // granted-rather-than-earned must be absent from a character who has
        // bought nothing -- so the next one authored at 999 is covered on the
        // day it is written rather than the day somebody notices.
        [Test]
        public void NoGrantedSkillReachesACharacterWhoHasBoughtNothing()
        {
            var granted = ContentDatabase.Skills
                .Where(s => s != null && s.characterId == SheepId && s.unlockLevel >= 999)
                .Select(s => s.id)
                .ToList();

            CollectionAssert.IsNotEmpty(granted,
                "fixture: sheep still has skills authored outside the level ladder");

            var strip = StripOf(new Character(SheepId));

            var leaked = granted.Where(strip.Contains).ToList();
            CollectionAssert.IsEmpty(leaked,
                "these are authored at unlockLevel 999 and reached a character with no "
                + "talents and no taught skills: " + string.Join(", ", leaked));
        }
    }
}
