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
    // The Sentinel constellation as authored (docs/PLAN_BJORN_CONSTELLATIONS.md
    // section 2): Bjorn's path 0, twenty-one rows on the shared skeleton, and
    // what buying them does to a real fight's combatant and kit -- asked of
    // the built content and the built session, not of the JSON.
    public class SentinelContentTests
    {
        private const string BjornId = "bear";
        private const int SentinelPath = 0;
        private const string BraceId = "placeholder_brawler_ward";
        private const string BellowId = "placeholder_brawler_provoke";

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-sentinel-" + System.Guid.NewGuid().ToString("N"));
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

        private static List<TalentDefinition> Path0() =>
            ContentDatabase.Talents
                .Where(t => t != null && t.Data.CharacterId == BjornId && t.Data.Column == SentinelPath)
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

        [Test]
        public void ThePathHasTwentyOneRows_OnEveryDistinctSlot()
        {
            var rows = Path0();

            Assert.AreEqual(21, rows.Count);
            CollectionAssert.AreEqual(Enumerable.Range(0, 21).ToList(), rows.Select(t => t.Data.Row).ToList(),
                "slots 0-20 exactly once each");
        }

        [Test]
        public void EveryRowIsReachableFromTheRoot()
        {
            var byId = Path0().ToDictionary(t => t.id);
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

            Assert.AreEqual("bear_sen_root", root.id);
        }

        [Test]
        public void TheTwoGatedRowsCarryTheirGates()
        {
            Assert.AreEqual(9, Node("bear_sen_converge").Data.MinSpent);
            Assert.AreEqual(20, Node("bear_sen_cap").Data.MinSpent);
            Assert.AreEqual(3, Node("bear_sen_converge").Data.Prerequisites.Length);
            Assert.AreEqual(3, Node("bear_sen_cap").Data.Prerequisites.Length);
        }

        [Test]
        public void TheRemovedPlaceholderRootIsGone()
        {
            Assert.IsNull(ContentDatabase.GetTalent("placeholder_brawler_ward_root"));
            Assert.IsNull(ContentDatabase.GetSkill("bear_bulwark"));
        }

        [TestCase("bear_sen_root", BraceId)]
        [TestCase("bear_sen_hold_1", "hold_the_line")]
        [TestCase("bear_sen_bellow_1", BellowId)]
        [TestCase("bear_sen_converge", "plant_the_shield")]
        [TestCase("bear_sen_bash_1", "shield_bash")]
        public void TheseNodesGrantTheirSkill(string talentId, string skillId)
        {
            Assert.AreEqual(skillId, Node(talentId).Data.GrantsSkillId);
            Assert.IsNotNull(ContentDatabase.GetSkill(skillId), "fixture: the skill is authored");
            Assert.AreEqual(999, ContentDatabase.GetSkill(skillId).Data.UnlockLevel,
                "a talent-granted skill is out of reach of levelling");
        }

        [Test]
        public void ABareBjorn_HasNoEngine_AndNoneOfTheSentinelSkills()
        {
            var (strip, bjorn) = Fight();

            Assert.AreEqual(FuryEngineKind.None, bjorn.FuryEngine.Kind);
            foreach (var skill in new[] { BraceId, BellowId, "hold_the_line", "plant_the_shield", "shield_bash" })
            {
                CollectionAssert.DoesNotContain(strip, skill);
            }
        }

        [Test]
        public void TheRoot_ArmsTheEngine_AndPutsBraceOnTheStrip()
        {
            var (strip, bjorn) = Fight("bear_sen_root");

            Assert.AreEqual(FuryEngineKind.Sentinel, bjorn.FuryEngine.Kind);
            CollectionAssert.Contains(strip, BraceId);
        }

        [Test]
        public void TheWholePath_ArmsEverySeam_AndPlantTheShieldTakesOverBracesButton()
        {
            var (strip, bjorn) = Fight(Path0().Select(t => t.id).ToArray());

            var shield = bjorn.PlantedShield;
            Assert.AreEqual(FuryEngineKind.Sentinel, bjorn.FuryEngine.Kind);
            Assert.IsTrue(shield.BreakShards);
            Assert.IsTrue(shield.ShortWaitAfterBash);
            Assert.IsTrue(shield.CoversParty);
            Assert.AreEqual(15, shield.ThornsPercent);
            Assert.AreEqual(15, shield.ReflectMagicPercent);
            Assert.IsTrue(shield.SilenceCasterOnSpellHit);
            Assert.IsTrue(shield.ReflectGrantsFury);
            Assert.IsTrue(shield.SlowsAttacker);
            Assert.IsTrue(shield.DisarmsOnBreak);
            Assert.AreEqual(30, bjorn.Talents.Best(TalentEffectType.IronRetortPercent));
            Assert.AreEqual(200, bjorn.Talents.Best(TalentEffectType.TargetPreferencePercent));

            foreach (var skill in new[] { BellowId, "hold_the_line", "plant_the_shield", "shield_bash" })
            {
                CollectionAssert.Contains(strip, skill);
            }

            CollectionAssert.DoesNotContain(strip, BraceId, "Plant the Shield replaces Brace's button");
        }

        [Test]
        public void ABjornWhoOwnsOnlyTheRoot_KeepsBrace_NotPlantTheShield()
        {
            var (strip, _) = Fight("bear_sen_root", "bear_sen_retort_1");

            CollectionAssert.Contains(strip, BraceId);
            CollectionAssert.DoesNotContain(strip, "plant_the_shield");
        }

        [Test]
        public void AFreshBjorn_HasNoDefenceAboveBase_SoTheIronRetortAddsNothing()
        {
            var (_, bjorn) = Fight("bear_sen_root", "bear_sen_retort_1", "bear_sen_retort_2", "bear_sen_retort_3");

            Assert.AreEqual(30, bjorn.Talents.Best(TalentEffectType.IronRetortPercent));
            Assert.AreEqual(0, CombatMath.IronRetortBonus(bjorn));
        }

        [Test]
        public void HoldTheLine_IsFortifiedForFortyFury()
        {
            var hold = ContentDatabase.GetSkill("hold_the_line").Data;

            Assert.AreEqual(SkillEffect.BuffParty, hold.Effect);
            Assert.AreEqual(40, hold.ManaCost);
            Assert.AreEqual(StatusEffectType.Fortified, hold.AppliesStatus);
            Assert.AreEqual(15, hold.StatusMagnitude);
            Assert.AreEqual(2, hold.StatusDuration);
        }

        [Test]
        public void BellowIsFreeOnAThreeTurnCooldown()
        {
            var bellow = ContentDatabase.GetSkill(BellowId).Data;

            Assert.AreEqual(SkillEffect.Provoke, bellow.Effect);
            Assert.AreEqual(0, bellow.ManaCost);
            Assert.AreEqual(3, bellow.CooldownTurns);
        }

        [Test]
        public void PlantTheShield_IsThirtyFury_AndNamesTheButtonItReplaces()
        {
            var plant = ContentDatabase.GetSkill("plant_the_shield").Data;

            Assert.AreEqual(SkillEffect.PlantShield, plant.Effect);
            Assert.AreEqual(30, plant.ManaCost);
            Assert.AreEqual(BraceId, plant.ReplacesSkillId);
        }

        [Test]
        public void ShieldBash_SpendsTheShield_AndAddsThirtyPercentOfWhatItSoaked_WithAOneTurnStun()
        {
            var bash = ContentDatabase.GetSkill("shield_bash").Data;

            Assert.AreEqual(SkillEffect.DamageSingle, bash.Effect);
            Assert.IsTrue(bash.PhysicalMove);
            Assert.AreEqual(20, bash.FlatAmount);
            Assert.AreEqual(30, bash.PlantedShieldBashPercent);
            Assert.AreEqual(StatusEffectType.Stun, bash.AppliesStatus);
            Assert.AreEqual(1, bash.StatusDuration);
        }

        [Test]
        public void TheFuryPoolsFlatGainsAreZero_SoOnlyAnEngineOrARiderPaysFury()
        {
            var fury = ContentDatabase.GetPool("fury").Data;

            Assert.AreEqual(0, fury.GainOnAttack);
            Assert.AreEqual(0, fury.GainOnDamageTaken);
        }
    }
}
