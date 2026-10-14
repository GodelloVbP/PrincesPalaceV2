using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // Who pays what (docs/PLAN_ITEMS.md 1.1, 1.2): a room's reward pages come
    // from its map chip alone, so the chip the card showed is what pays.
    public class ChipPagesTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-chip-pages-" + System.Guid.NewGuid().ToString("N"));
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

        // A fresh run between two wins inside one test.
        private void Reset()
        {
            Restore();
            UseAThrowawaySaveRoot();
        }

        private sealed class Outcome
        {
            public PrizeKind Quoted;
            public IReadOnlyList<PendingPage> Pages;
            public int GoldGained;
            public int PotionsGained;
            public int BooksGained;
        }

        // `leaderOnly` ends a boss fight by the leader rule: the anchors still
        // standing die with it and pay nothing.
        private static void Win(FightSession session, bool leaderOnly = false)
        {
            if (!leaderOnly)
            {
                foreach (var enemy in session.Encounter.Enemies) enemy.CurrentHealth = 0;
            }

            if (session.Encounter.Leader != null) session.Encounter.Leader.CurrentHealth = 0;
            session.Begin();
            Assert.IsTrue(session.Payout.HasValue, "fixture: the fight is won");
        }

        // A won fight at the `index`th node of `type` in the seed's realm,
        // with `prize` stamped on it (null keeps what the generator rolled).
        private static Outcome WinAt(ulong seed, RoomType type, int index, PrizeKind? prize)
        {
            RunManager.StartRun(seed);
            var node = RunManager.Map.Nodes.Where(n => n.Type == type).ElementAt(index);
            if (prize.HasValue) node.Prize = prize.Value;
            var run = RunManager.Run;
            var save = SaveSlotManager.CurrentSave;
            run.currentNodeId = node.Id;

            var outcome = new Outcome { Quoted = RealmPrizes.ChipOf(run, node).Kind };
            int gold = run.gold;
            int potions = save.stockpiledItems.Sum(e => e.count);
            int books = run.unassignedSpellBooks.Count;
            int step = run.step;

            var session = RunOrchestrator.BuildFight().Session;
            Win(session);

            RunOrchestrator.SettleFight(session, won: true);
            outcome.Pages = RunOrchestrator.SettleRewardPages(save, session, step, node.Id);
            outcome.GoldGained = RunManager.Run.gold - gold - session.Payout.Value.Gold;
            outcome.PotionsGained = save.stockpiledItems.Sum(e => e.count) - potions;
            outcome.BooksGained = RunManager.Run.unassignedSpellBooks.Count - books;
            return outcome;
        }

        private static int Count(Outcome o, RewardPageKind kind) => o.Pages.Count(p => p.Kind == kind);

        [Test]
        public void AGearChipQueuesExactlyOneGearPageWithOneChoice()
        {
            var o = WinAt(4242, RoomType.Fight, 0, PrizeKind.Item);

            Assert.AreEqual(1, o.Pages.Count);
            Assert.AreEqual(1, Count(o, RewardPageKind.Gear));
            Assert.AreEqual(1, o.Pages[0].Choices);
            Assert.AreEqual(3, o.Pages[0].Cards.Count, "k + 2 cards");
        }

        [Test]
        public void ARelicChipQueuesExactlyOneRelicPageAndNoGearPage()
        {
            var o = WinAt(4242, RoomType.EliteFight, 0, PrizeKind.Relic);

            Assert.AreEqual(1, o.Pages.Count);
            Assert.AreEqual(1, Count(o, RewardPageKind.Relic));
            Assert.AreEqual(1, o.Pages[0].Choices);
        }

        [Test]
        public void AnAnchorGearChipQueuesOneGearPageOfTheAnchorClass()
        {
            var o = WinAt(4242, RoomType.EliteFight, 0, PrizeKind.Item);

            Assert.AreEqual(1, o.Pages.Count);
            Assert.AreEqual("anchor_gear", o.Pages[0].SourceId);
        }

        [TestCase(PrizeKind.Gold)]
        [TestCase(PrizeKind.Consumable)]
        [TestCase(PrizeKind.Book)]
        [TestCase(PrizeKind.None)]
        public void GoldConsumableBookAndEmptyChipsQueueNoPage(PrizeKind kind)
        {
            var o = WinAt(4242, RoomType.Fight, 0, kind);

            Assert.AreEqual(0, o.Pages.Count);
        }

        [Test]
        public void AGoldChipPaysItsGoldAndAConsumableChipItsPotions()
        {
            var gold = WinAt(4242, RoomType.Fight, 0, PrizeKind.Gold);
            Assert.Greater(gold.GoldGained, 0);
            Assert.AreEqual(0, gold.PotionsGained);

            Reset();
            var potions = WinAt(4242, RoomType.Fight, 0, PrizeKind.Consumable);
            Assert.Greater(potions.PotionsGained, 0);
        }

        [Test]
        public void ABookChipPaysOneUnassignedBook_WithNoPageAndNoChanceGate()
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                Reset();
                var o = WinAt(seed, RoomType.Fight, 0, PrizeKind.Book);

                Assert.AreEqual(1, o.BooksGained, "seed " + seed);
                Assert.AreEqual(0, o.Pages.Count);
            }
        }

        [Test]
        public void AFightWithAnotherChipPaysNoBook_TheHiddenRollIsGone()
        {
            for (ulong seed = 1; seed <= 12; seed++)
            {
                Reset();
                var o = WinAt(seed, RoomType.Fight, 0, PrizeKind.Gold);

                Assert.AreEqual(0, o.BooksGained, "seed " + seed);
            }
        }

        [Test]
        public void AWonBossPaysAGearPageAndARelicPage_InThatOrder()
        {
            RunManager.StartRun(4242);
            var boss = RunManager.Map.Nodes.First(n => n.Type == RoomType.Boss);
            boss.Prize = PrizeKind.None;
            RunManager.Run.currentNodeId = boss.Id;
            var save = SaveSlotManager.CurrentSave;
            int step = RunManager.Run.step;

            var session = RunOrchestrator.BuildFight().Session;
            Assert.IsTrue(session.IsBossFight);
            Win(session, leaderOnly: true);
            RunOrchestrator.SettleFight(session, won: true);
            var pages = RunOrchestrator.SettleRewardPages(save, session, step, boss.Id);

            CollectionAssert.AreEqual(new[] { RewardPageKind.Gear, RewardPageKind.Relic }, pages.Select(p => p.Kind).ToList());
            CollectionAssert.AreEqual(new[] { "boss_gear", "boss_relic" }, pages.Select(p => p.SourceId).ToList());
            CollectionAssert.AreEqual(new[] { 1, 1 }, pages.Select(p => p.Choices).ToList());
        }

        // The card's quote is the payout: for every fight and anchor node of
        // several realms, the quoted kind decides what pages and rewards
        // arrive.
        [Test]
        public void TheChipTheMapQuotedIsWhatThePagesAndRewardsPay()
        {
            var seen = new HashSet<PrizeKind>();
            for (ulong seed = 1; seed <= 3; seed++)
            {
                RunManager.StartRun(seed);
                var targets = new List<(RoomType type, int index)>();
                foreach (var type in new[] { RoomType.Fight, RoomType.EliteFight })
                {
                    int count = RunManager.Map.Nodes.Count(n => n.Type == type);
                    for (int i = 0; i < count; i++) targets.Add((type, i));
                }

                foreach (var (type, index) in targets)
                {
                    Reset();
                    var o = WinAt(seed, type, index, null);
                    seen.Add(o.Quoted);
                    string at = $"seed {seed} {type} #{index} quoted {o.Quoted}";

                    Assert.AreEqual(o.Quoted == PrizeKind.Item ? 1 : 0, Count(o, RewardPageKind.Gear), at);
                    Assert.AreEqual(o.Quoted == PrizeKind.Relic ? 1 : 0, Count(o, RewardPageKind.Relic), at);
                    Assert.AreEqual(o.Quoted == PrizeKind.Book ? 1 : 0, o.BooksGained, at);
                    if (o.Quoted == PrizeKind.Gold) Assert.Greater(o.GoldGained, 0, at);
                    if (o.Quoted == PrizeKind.Consumable) Assert.Greater(o.PotionsGained, 0, at);
                }

                Reset();
            }

            Assert.That(seen, Is.SupersetOf(new[] { PrizeKind.Gold, PrizeKind.Item }), "the fixture exercised more than one kind");
        }
    }
}
