using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace.PlayModeTests
{
    // THE JOURNEY BACK OUT OF A SCRIPTABLE OBJECT.
    //
    // Content makes two hops: JSON is resolved into a ResolvedX and written to
    // an asset by ContentBuilder, and the asset is read back into a ResolvedX
    // by FightEncounterAdapter when a fight starts. The second hop is a
    // hand-written field-for-field copy, and its own comment used to call it
    // "mechanical" -- which is exactly the kind of copy where a missing line is
    // invisible.
    //
    // One WAS missing. `transform` was written onto the asset and never read
    // back, so every skill resolved for a fight -- the player's included --
    // arrived with a null grant and Black Ram Mode did nothing at all. Nothing
    // caught it: the transform tests build a ResolvedSkill by hand and pass the
    // grant in, which is the half of the journey that always worked.
    //
    // These run in PlayMode because the adapter lives in Core and an EditMode
    // Domain test cannot see it -- the same boundary that let the gap sit.
    public class ContentRoundTripTests
    {
        [Test]
        public void ATransformSkillKeepsItsGrantThroughTheAsset()
        {
            var transforms = ContentDatabase.Skills
                .Where(s => s != null && s.data.Effect == SkillEffect.Transform)
                .ToList();

            Assert.IsNotEmpty(transforms,
                "no Transform skill is authored, so this guards nothing. Delete it or author one.");

            foreach (var definition in transforms)
            {
                Assert.IsNotNull(definition.data.Transform,
                    $"'{definition.id}' lost its grant on the way INTO the asset (ContentBuilder).");

                var resolved = FightEncounterAdapter.Resolve(definition);

                Assert.IsNotNull(resolved.Transform,
                    $"'{definition.id}' lost its grant on the way OUT of the asset. The skill resolves, " +
                    "costs its resource, plays its animation, and grants nothing.");
                Assert.IsTrue(resolved.Transform.IsAuthored,
                    $"'{definition.id}' round-tripped an empty grant, which EnterTransform discards silently.");
            }
        }

        // A MONSTER'S SKILL IS STILL A SKILL, and has to survive the same two
        // hops. Checked on the golem specifically because it is the first one
        // authored this way, and its ability is the whole of what it does --
        // attackWeight 0, so a dropped ability means a boss that stands still.
        [Test]
        public void TheGolemsSlamSurvivesTheRoundTrip()
        {
            var golem = ContentDatabase.GetEnemy("golem");
            Assert.IsNotNull(golem, "the golem is gone from content");

            Assert.IsNotEmpty(golem.data.Abilities, "the golem has no abilities authored");

            var named = golem.data.Abilities[0].SkillId;
            var slam = ContentDatabase.GetSkill(named);

            Assert.IsNotNull(slam, $"the golem names '{named}', which no skill matches");
            Assert.IsFalse(slam.data.PlayerSelectable,
                "a monster's skill must never be offered on a player's strip");

            var resolved = FightEncounterAdapter.Resolve(slam);
            Assert.AreEqual(SkillEffect.DamageSingle, resolved.Effect);
            Assert.AreEqual("Spells/golem_boulder", resolved.Vfx.path,
                "the slam lost the art it is drawn with");
        }

        // The other direction of the same rule: nothing a monster owns may leak
        // onto a player's action strip, whatever its owner id happens to say.
        [Test]
        public void NoMonsterSkillIsOfferedToAnyCharacter()
        {
            var enemyIds = ContentDatabase.Enemies
                .Where(e => e != null)
                .Select(e => e.id)
                .ToHashSet();

            var leaked = ContentDatabase.Skills
                .Where(s => s != null && s.data.PlayerSelectable && enemyIds.Contains(s.data.CharacterId))
                .Select(s => s.id)
                .ToList();

            Assert.IsEmpty(leaked,
                "These skills are owned by a monster but marked player-selectable: " +
                string.Join(", ", leaked) + ".");
        }
    }
}
