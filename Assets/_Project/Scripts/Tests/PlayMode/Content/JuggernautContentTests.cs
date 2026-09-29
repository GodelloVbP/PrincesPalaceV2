using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.PlayModeTests
{
    // The Juggernaut constellation as authored (docs/PLAN_BJORN_CONSTELLATIONS.md
    // section 4): Bjorn's path 2, twenty-one rows on the shared skeleton, and
    // what buying them does to a real fight's combatant and kit -- asked of
    // the built content and the built session, not of the JSON.
    public class JuggernautContentTests
    {
        private const string BjornId = "bear";
        private const int JuggernautPath = 2;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-juggernaut-" + System.Guid.NewGuid().ToString("N"));
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

        private static List<TalentDefinition> Path2() =>
            ContentDatabase.Talents
                .Where(t => t != null && t.Data.CharacterId == BjornId && t.Data.Column == JuggernautPath)
                .OrderBy(t => t.Data.Row)
                .ToList();

        private static TalentDefinition Node(string id)
        {
            var node = ContentDatabase.GetTalent(id);
            Assert.IsNotNull(node, "fixture: content has " + id);
            return node;
        }

        private static (List<string> strip, CombatantState bjorn) Fight(params string[] talentIds)
        {
            var character = new Character(BjornId);
            foreach (var id in talentIds) character.unlockedTalentIds.Add(id);

            var built = FightEncounterAdapter.Build(
                new List<string> { BjornId },
                ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList(),
                new Domain.Rng.SeededRandom(7),
                partyCharacters: new List<Character> { character });
            Assert.IsNotNull(built, "fixture: the fight built");

            built.Session.Begin();
            var bjorn = built.Party[0];
            var strip = built.Session.KitFor(bjorn).Skills.Select(s => s.Id).ToList();
            return (strip, bjorn);
        }

        private static int MaxHealthWith(params string[] talentIds)
        {
            var character = new Character(BjornId);
            foreach (var id in talentIds) character.unlockedTalentIds.Add(id);
            return ContentDatabase.EffectiveStats(character).maxHealth;
        }

        [Test]
        public void ThePathHasTwentyOneRows_OnEveryDistinctSlot()
        {
            var rows = Path2();

            Assert.AreEqual(21, rows.Count);
            CollectionAssert.AreEqual(Enumerable.Range(0, 21).ToList(), rows.Select(t => t.Data.Row).ToList(),
                "slots 0-20 exactly once each");
        }

        [Test]
        public void EveryRowIsReachableFromTheRoot()
        {
            var byId = Path2().ToDictionary(t => t.id);
            var root = byId.Values.Single(t => t.Data.Row == 0);

            foreach (var node in byId.Values.Where(t => t.Data.Row > 0))
            {
                Assert.IsNotEmpty(node.Data.Prerequisites, node.id + " has no prerequisite");
                foreach (var parent in node.Data.Prerequisites)
                {
                    Assert.IsTrue(byId.ContainsKey(parent) && byId[parent].Data.Row < node.Data.Row,
                        node.id + " names " + parent + ", which is not an earlier row of this path");
                }
            }

            Assert.AreEqual("bear_jug_root", root.id);
        }

        [Test]
        public void TheTwoGatedRowsCarryTheirGates()
        {
            Assert.AreEqual(9, Node("bear_jug_converge").Data.MinSpent);
            Assert.AreEqual(20, Node("bear_jug_cap").Data.MinSpent);
            Assert.AreEqual(3, Node("bear_jug_converge").Data.Prerequisites.Length);
            Assert.AreEqual(3, Node("bear_jug_cap").Data.Prerequisites.Length);
        }

        [TestCase("bear_jug_root", "second_wind")]
        [TestCase("bear_jug_gorge_1", "gorge")]
        [TestCase("bear_jug_converge", "unbroken")]
        [TestCase("bear_jug_cap", "bjorn_cursed_blood")]
        public void TheseNodesGrantTheirSkill(string talentId, string skillId)
        {
            Assert.AreEqual(skillId, Node(talentId).Data.GrantsSkillId);
            Assert.IsNotNull(ContentDatabase.GetSkill(skillId), "fixture: the skill is authored");
        }

        [Test]
        public void ABareBjorn_HasNoEngine_AndNoneOfTheJuggernautSkills()
        {
            var (strip, bjorn) = Fight();

            Assert.AreEqual(FuryEngineKind.None, bjorn.FuryEngine.Kind);
            foreach (var skill in new[] { "second_wind", "gorge", "unbroken", "bjorn_cursed_blood" })
            {
                CollectionAssert.DoesNotContain(strip, skill);
            }
        }

        [Test]
        public void TheRoot_ArmsTheEngine_PutsSecondWindOnTheStrip_AndSlowsItsCooldown()
        {
            var (strip, bjorn) = Fight("bear_jug_root");

            Assert.AreEqual(FuryEngineKind.Juggernaut, bjorn.FuryEngine.Kind);
            CollectionAssert.Contains(strip, "second_wind");
            Assert.AreEqual(4, bjorn.CooldownOverrides["second_wind"]);
        }

        [Test]
        public void TheWholePath_ArmsEverySeam_AndGivesEverySkill()
        {
            var (strip, bjorn) = Fight(Path2().Select(t => t.id).ToArray());

            Assert.AreEqual(FuryEngineKind.Juggernaut, bjorn.FuryEngine.Kind);
            Assert.AreEqual(3, bjorn.CrowdControl.Unyielding.CooldownTurns);
            Assert.AreEqual(20, bjorn.CrowdControl.Unyielding.FuryGain);
            Assert.AreEqual(30, bjorn.DelayedDamage.Percent);
            Assert.AreEqual(3, bjorn.DelayedDamage.Turns);
            Assert.IsTrue(bjorn.DelayedDamage.HealsReducePool);
            Assert.AreEqual(5, bjorn.ShortfallHealthPermille);
            Assert.IsTrue(bjorn.Talents.Has(TalentEffectType.CheatDeathOncePerFight));
            Assert.IsTrue(bjorn.Talents.Has(TalentEffectType.CheatDeathFillsPrimary));
            foreach (var skill in new[] { "second_wind", "gorge", "unbroken", "bjorn_cursed_blood" })
            {
                CollectionAssert.Contains(strip, skill);
            }
        }

        [Test]
        public void ThickBlood_RaisesMaxHealthByItsPercentOfTheFinishedFigure()
        {
            int bare = MaxHealthWith();

            Assert.AreEqual(340, bare, "fixture: the bare figure this test's literals are worked from");
            Assert.AreEqual(374, MaxHealthWith("bear_jug_root", "bear_jug_blood_1"), "+10%");
            Assert.AreEqual(408, MaxHealthWith("bear_jug_root", "bear_jug_blood_1", "bear_jug_blood_2", "bear_jug_blood_3"),
                "+20% in all, not 10 on 10");
        }

        [Test]
        public void TheGorgeRowIsAHeavyBlow_ThatHealsThirtyPercent()
        {
            var gorge = ContentDatabase.GetSkill("gorge").Data;

            Assert.AreEqual(SkillEffect.DamageSingle, gorge.Effect);
            Assert.AreEqual(30, gorge.ManaCost);
            Assert.AreEqual(30, gorge.LifestealPercent);
            Assert.IsTrue(gorge.PhysicalMove);
        }

        [Test]
        public void UnbrokenIsFiftyFuryForAFifteenPercentRegenAndTwoTurnsOfUnstoppable()
        {
            var unbroken = ContentDatabase.GetSkill("unbroken").Data;

            Assert.AreEqual(SkillEffect.Unbroken, unbroken.Effect);
            Assert.AreEqual(50, unbroken.ManaCost);
            Assert.AreEqual(2, unbroken.WindowTurns);
            Assert.AreEqual(15, unbroken.RegenPercentOfMaxHealth);
        }

        [Test]
        public void CursedBloodIsFiftyFuryOncePerFight()
        {
            var cursed = ContentDatabase.GetSkill("bjorn_cursed_blood").Data;

            Assert.AreEqual(SkillEffect.CursedBlood, cursed.Effect);
            Assert.AreEqual(50, cursed.ManaCost);
            Assert.AreEqual(2, cursed.WindowTurns);
            Assert.IsTrue(cursed.OncePerFight);
        }
    }
}
