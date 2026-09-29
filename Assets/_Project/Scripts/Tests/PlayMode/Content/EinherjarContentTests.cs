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
    // The Einherjar constellation as authored (docs/PLAN_BJORN_CONSTELLATIONS.md
    // section 3): Bjorn's path 1, twenty-one rows on the shared skeleton, and
    // what buying them does to a real fight's combatant and kit -- asked of
    // the built content and the built session, not of the JSON.
    public class EinherjarContentTests
    {
        private const string BjornId = "bear";
        private const int EinherjarPath = 1;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-einherjar-" + System.Guid.NewGuid().ToString("N"));
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

        private static List<TalentDefinition> Path1() =>
            ContentDatabase.Talents
                .Where(t => t != null && t.Data.CharacterId == BjornId && t.Data.Column == EinherjarPath)
                .OrderBy(t => t.Data.Row)
                .ToList();

        private static TalentDefinition Node(string id)
        {
            var node = ContentDatabase.GetTalent(id);
            Assert.IsNotNull(node, "fixture: content has " + id);
            return node;
        }

        // What Bjorn can press and what the built combatant carries, off a
        // real Build and a Begin (the fight-start pass arms the seams there).
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
            var rows = Path1();

            Assert.AreEqual(21, rows.Count);
            CollectionAssert.AreEqual(Enumerable.Range(0, 21).ToList(), rows.Select(t => t.Data.Row).ToList(),
                "slots 0-20 exactly once each");
        }

        [Test]
        public void EveryRowIsReachableFromTheRoot()
        {
            var byId = Path1().ToDictionary(t => t.id);
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

            Assert.AreEqual("bear_ein_root", root.id);
        }

        [Test]
        public void TheThreeGatedRowsCarryTheirGates()
        {
            Assert.AreEqual(9, Node("bear_ein_converge").Data.MinSpent);
            Assert.AreEqual(20, Node("bear_ein_cap").Data.MinSpent);
            Assert.AreEqual(3, Node("bear_ein_converge").Data.Prerequisites.Length);
            Assert.AreEqual(3, Node("bear_ein_cap").Data.Prerequisites.Length);
        }

        [TestCase("bear_ein_root", "hack")]
        [TestCase("bear_ein_converge", "rampage")]
        [TestCase("bear_ein_head_1", "headsplitter")]
        [TestCase("bear_ein_bers_1", "berserk")]
        public void TheseNodesGrantTheirSkill(string talentId, string skillId)
        {
            Assert.AreEqual(skillId, Node(talentId).Data.GrantsSkillId);
            Assert.IsNotNull(ContentDatabase.GetSkill(skillId), "fixture: the skill is authored");
        }

        [Test]
        public void ABareBjorn_HasNoEngine_NoHack_AndNoRampage()
        {
            var (strip, bjorn) = Fight();

            Assert.AreEqual(FuryEngineKind.None, bjorn.FuryEngine.Kind);
            CollectionAssert.DoesNotContain(strip, "hack");
            CollectionAssert.DoesNotContain(strip, "rampage");
            CollectionAssert.DoesNotContain(strip, "headsplitter");
            CollectionAssert.DoesNotContain(strip, "berserk");
        }

        [Test]
        public void TheRoot_ArmsTheEngine_AndPutsHackOnTheStrip()
        {
            var (strip, bjorn) = Fight("bear_ein_root");

            Assert.AreEqual(FuryEngineKind.Einherjar, bjorn.FuryEngine.Kind);
            CollectionAssert.Contains(strip, "hack");
        }

        [Test]
        public void TheWholePath_ArmsEverySeam_AndGivesEverySkill()
        {
            var (strip, bjorn) = Fight(Path1().Select(t => t.id).ToArray());

            Assert.AreEqual(FuryEngineKind.Einherjar, bjorn.FuryEngine.Kind);
            Assert.IsTrue(bjorn.Momentum.Enabled && bjorn.Momentum.ExtendedStackCap && bjorn.Momentum.IgnoresSmallHits);
            Assert.AreEqual(30, bjorn.BattleTrance.Percent);
            Assert.IsTrue(bjorn.BattleTrance.ProtectWhenTranceBreaks);
            Assert.IsNotNull(bjorn.TwinRampage);
            foreach (var skill in new[] { "hack", "rampage", "headsplitter", "berserk" })
            {
                CollectionAssert.Contains(strip, skill);
            }
        }

        [Test]
        public void HackIsTwoBlowsForNoFury_OnAThreeTurnCooldown()
        {
            var hack = ContentDatabase.GetSkill("hack").Data;

            Assert.AreEqual(SkillEffect.DamageSingle, hack.Effect);
            Assert.AreEqual(2, hack.HitCount);
            Assert.AreEqual(0, hack.ManaCost);
            Assert.AreEqual(3, hack.CooldownTurns);
        }

        [Test]
        public void BerserkIsAFormKeptUpByFury()
        {
            var berserk = ContentDatabase.GetSkill("berserk").Data;

            Assert.AreEqual(SkillEffect.Transform, berserk.Effect);
            Assert.AreEqual(50, berserk.ManaCost);
            Assert.AreEqual(50, berserk.Transform.defenseToAttackPercent);
            Assert.AreEqual(10, berserk.Transform.primaryDrainPerTurn);
            CollectionAssert.AreEquivalent(new[] { "Provoke", "Ward" }, berserk.Transform.forbidsEffects);
        }

        [Test]
        public void HeadsplitterSpendsEverythingAboveItsThirtyMinimum()
        {
            var head = ContentDatabase.GetSkill("headsplitter").Data;

            Assert.IsTrue(head.SpendsAllPrimary);
            Assert.AreEqual(30, head.ManaCost);
            Assert.AreEqual(1, head.Power);
            Assert.AreEqual(100, head.DamagePerMissingHealthPercent);
            Assert.AreEqual(50, head.RefundsSpentOnKillPercent);
        }
    }
}
