using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.PlayModeTests
{
    // The heart's roster and what a won boss fight settles when anchors died
    // in it (docs/PLAN_RUN_MAP.md 6; PLAN_ITEMS.md 5.4).
    public class HeartAnchorSettlementTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-heart-anchors-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => { };
            SaveData.TestSquadOfThreeEnabled = false;
        }

        [TearDown]
        public void Restore()
        {
            SaveData.TestSquadOfThreeEnabled = null;
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        // A realm with at least `anchors` anchor nodes (plain fight nodes are
        // promoted to anchors when the seed rolled fewer), the party stood in
        // the boss room.
        private static RealmNode OpenAHeart(int anchors)
        {
            RunManager.StartRun(4242);
            var map = RunManager.Map;
            foreach (var plain in map.Nodes.Where(n => n.Type == RoomType.Fight).ToList())
            {
                if (map.Nodes.Count(n => n.IsAnchor) >= anchors) break;
                plain.Type = RoomType.EliteFight;
            }

            Assert.GreaterOrEqual(map.Nodes.Count(n => n.IsAnchor), anchors, "fixture: not enough fight nodes to promote");
            var boss = map.Nodes.First(n => n.Type == RoomType.Boss);
            RunManager.Run.currentNodeId = boss.Id;
            return boss;
        }

        [Test]
        public void TheHeartRoster_IsEveryAnchorNotSlain_InFormationOrder_EachTheEnemyItsNodeRolls()
        {
            OpenAHeart(3);
            var run = RunManager.Run;
            var save = SaveSlotManager.CurrentSave;
            var anchorNodes = RunManager.Map.Nodes.Where(n => n.IsAnchor).ToList();

            var all = RunEncounter.For(save, run, RoomType.Boss).Anchors;

            CollectionAssert.AreEquivalent(anchorNodes.Select(n => n.Id), all.Select(a => a.NodeId).ToList());
            CollectionAssert.AreEqual(HeartAnchors.Formation(all).Select(a => a.NodeId).ToList(),
                all.Select(a => a.NodeId).ToList(), "already in formation order");
            foreach (var anchor in all)
            {
                var node = RunManager.Map.Node(anchor.NodeId);
                Assert.AreEqual(RunEncounter.Preview(run, node).EnemyIds[0], anchor.EnemyId,
                    "the heart fields the enemy its map node rolls");
            }

            run.slainAnchorIds.Add(anchorNodes[0].Id);
            var after = RunEncounter.For(save, run, RoomType.Boss).Anchors;

            Assert.AreEqual(all.Count - 1, after.Count);
            CollectionAssert.DoesNotContain(after.Select(a => a.NodeId).ToList(), anchorNodes[0].Id);
        }

        [Test]
        public void ANonBossRoomHasNoAnchors()
        {
            OpenAHeart(1);

            var roster = RunEncounter.For(SaveSlotManager.CurrentSave, RunManager.Run, RoomType.Fight);

            Assert.AreEqual(0, roster.Anchors.Count);
        }

        // A won boss fight with two anchors dead (one a Gold chip, one a Gear
        // chip) and a third queued: the opening of the fight is already over
        // (Begin settles it), so the leader rule is the only way it ended.
        private static FightSession OverWithTwoAnchorsDead(out RealmNode goldNode, out RealmNode gearNode, out int rawExp)
        {
            var bossNode = OpenAHeart(3);
            bossNode.Prize = PrizeKind.None;
            var run = RunManager.Run;

            // Formation order decides who stands in front: the first two are
            // fielded, the third is queued.
            var roster = RunEncounter.For(SaveSlotManager.CurrentSave, run, RoomType.Boss).Anchors;
            goldNode = RunManager.Map.Node(roster[0].NodeId);
            gearNode = RunManager.Map.Node(roster[1].NodeId);
            var queuedNode = RunManager.Map.Node(roster[2].NodeId);
            goldNode.Prize = PrizeKind.Gold;
            gearNode.Prize = PrizeKind.Item;
            queuedNode.Prize = PrizeKind.Gold;

            var session = RunOrchestrator.BuildFight().Session;
            var encounter = session.Encounter;
            Assert.IsTrue(session.IsBossFight);
            Assert.AreEqual(1, encounter.Reinforcements.Count, "fixture: one anchor is queued");

            rawExp = session.SourceFor(encounter.Leader).Source.ExpReward
                + session.SourceFor(encounter.Enemies[0]).Source.ExpReward
                + session.SourceFor(encounter.Enemies[1]).Source.ExpReward;

            encounter.Enemies[0].CurrentHealth = 0;
            encounter.Enemies[1].CurrentHealth = 0;
            encounter.Leader.CurrentHealth = 0;
            session.Begin();

            Assert.AreEqual(FightEndReason.LeaderFell, session.EndReason);
            Assert.IsTrue(session.Payout.HasValue, "fixture: the fight settled its payout at Begin");
            CollectionAssert.AreEquivalent(new[] { goldNode.Id, gearNode.Id }, session.FallenAnchorNodeIds.ToList(),
                "the session names the two dead anchors, not the queued one");
            return session;
        }

        // The boss win advances the leg, which clears slainAnchorIds with the
        // old realm; so the anchor half of the settlement is read straight off
        // the method that does it.
        [Test]
        public void TheDeadAnchorsAreSlainAndTheirGoldChipIsPaid_TheQueuedOneIsNeither()
        {
            var session = OverWithTwoAnchorsDead(out var goldNode, out var gearNode, out _);
            var run = RunManager.Run;
            int chipGold = RealmPrizes.ChipOf(run, goldNode).Amount;
            Assert.Greater(chipGold, 0, "fixture: the gold chip pays something");
            int goldBefore = run.gold;
            var chips = new Domain.Rewards.PageChips();

            var notices = (List<string>)typeof(RunOrchestrator).GetMethod("PayHeartAnchors",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { session, run, chips });

            CollectionAssert.AreEquivalent(new[] { goldNode.Id, gearNode.Id }, run.slainAnchorIds);
            CollectionAssert.AreEqual(new[] { Domain.Rewards.EncounterClass.Elite }, chips.Gear, "the dead Gear chip is collected");
            CollectionAssert.IsEmpty(chips.Relic);
            Assert.AreEqual(goldBefore + chipGold, run.gold, "the gold chip is credited; the Gear chip adds no gold");
            Assert.AreEqual(1, notices.Count, "one line, for the gold chip");
        }

        [Test]
        public void AWonBossFightPaysTheBossAndBothDeadAnchors_AndNoGoldForTheOneThatDiedWithTheBoss()
        {
            var session = OverWithTwoAnchorsDead(out var goldNode, out _, out int rawExp);
            var run = RunManager.Run;
            int chipGold = RealmPrizes.ChipOf(run, goldNode).Amount;
            int goldBefore = run.gold;

            RunOrchestrator.SettleFight(session, won: true);

            Assert.AreEqual(goldBefore + session.Payout.Value.Gold + chipGold, RunManager.Run.gold,
                "the fight's gold plus the dead gold-chip anchor's chip, and nothing for the queued anchor");
            CollectionAssert.AreEqual(
                new[] { Domain.Rewards.EncounterClass.Boss, Domain.Rewards.EncounterClass.Elite },
                session.SettledChips.Gear, "the boss's gear chip, then the dead anchor's");
            CollectionAssert.AreEqual(new[] { Domain.Rewards.EncounterClass.Boss }, session.SettledChips.Relic);
            Assert.GreaterOrEqual(session.Payout.Value.Experience, rawExp,
                "the payout covers the boss and both dead anchors (elite pay only raises it)");
        }
    }
}
