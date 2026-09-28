using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // ITEM 5: every authored damaging skill reports a damage type through
    // FightHudModel.DetailForSkill; every non-damaging one reports none --
    // exercised against Shawn's REAL, content-authored skill strip
    // (skills.json), not synthetic ResolvedSkill fixtures the way
    // FightHudModelTests' DamageType cases (EditMode) already cover the
    // mechanism in isolation.
    public class SkillDamageTypeReachesHudTests
    {
        [Test]
        public void EveryAuthoredSkill_ReportsADamageTypeIffItDealsDamage()
        {
            var definition = ContentDatabase.Characters.FirstOrDefault(c => c != null && c.id == "sheep");
            Assert.IsNotNull(definition, "fixture: content still authors Shawn under id \"sheep\"");

            // Collected, not merely reached: everything on Shawn's strip
            // beyond Shear arrives from his reward track, so a level-20
            // character who has collected nothing has a one-skill strip and
            // the non-damaging branch below would never be exercised.
            var character = new Character(definition.id) { level = 20, claimedTrackLevel = 20 };
            var enemyId = ContentDatabase.Enemies.Select(e => e.id).FirstOrDefault();
            Assert.IsNotNull(enemyId, "fixture: content has at least one enemy");

            var built = FightEncounterAdapter.Build(
                new List<string> { character.definitionId },
                new List<string> { enemyId },
                new SeededRandom(1),
                partyCharacters: new List<Character> { character });
            Assert.IsNotNull(built, "fixture: a real fight must build");

            var session = built.Session;
            var hero = built.Party[0];

            var skills = ContentDatabase.AvailableSkillsFor(character)
                .Select(FightEncounterAdapter.Resolve)
                .ToList();
            Assert.IsNotEmpty(skills, "fixture: Shawn at level 20 must have unlocked real skills to check");

            // At least one damaging and one non-damaging skill in the fixture
            // set, or this test could pass by only ever exercising one branch.
            Assert.IsTrue(skills.Any(s => s.IsDamaging), "fixture check: Shawn's strip has a damaging skill");
            Assert.IsTrue(skills.Any(s => !s.IsDamaging), "fixture check: Shawn's strip has a non-damaging skill");

            foreach (var skill in skills)
            {
                var panel = FightHudModel.DetailForSkill(session, hero, skill);
                if (skill.IsDamaging)
                {
                    Assert.IsNotEmpty(panel.DamageType,
                        $"{skill.Id} deals damage (effect {skill.Effect}) and must report a damage type");
                }
                else
                {
                    Assert.AreEqual("", panel.DamageType,
                        $"{skill.Id} deals no damage (effect {skill.Effect}) and must report none");
                }
            }
        }
    }
}
