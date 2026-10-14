using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // The realm wired into the run (docs/PLAN_RUN_MAP.md section 8): the
    // profile and strength frozen at realm open, the step contract,
    // the skip heal, the preview on the Fight stream, chips paid, and the
    // v8 -> v9 save migration. PlayMode only because it reads content and
    // writes saves; no scene is loaded.
    public class RealmRunTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-realm-tests-" + System.Guid.NewGuid().ToString("N"));
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

        private const ulong Seed = 4242;

        private static bool IsFightNode(RealmNode n) => n.Type == RoomType.Fight || n.Type == RoomType.EliteFight;

        // ---- realm open ----------------------------------------------------

        [Test]
        public void OpeningARealmSavesItsProfileAndAStrengthForEveryFight()
        {
            RunManager.StartRun(Seed);
            var run = RunManager.Run;

            Assert.AreEqual(0, run.realmOpenedAt);
            Assert.IsNotNull(run.realmProfile);
            Assert.AreEqual(RealmProfiles.Default(1).pocketCount, run.realmProfile.pocketCount);
            Assert.AreEqual("The Descent", run.realmName);
            Assert.AreEqual("The Descent", RealmOpening.NameOf(run));

            var fights = RunManager.Map.Nodes.Where(IsFightNode).ToList();
            CollectionAssert.AreEquivalent(fights.Select(n => n.Id), run.nodeStrength.Select(e => e.nodeId));
            Assert.IsTrue(run.nodeStrength.All(e => e.strength >= 1 && e.strength <= 3));
        }

        // A designed realm for floor 3 (legStartStep 16), authored the way a
        // realms.json row resolves.
        private static ResolvedRealm DesignedFloorThree() => new ResolvedRealm(3, "The Ashen Court", new RealmProfile
        {
            pocketCount = 3,
            pocketLengthMin = 1,
            pocketLengthMax = 3,
            forkCount = 2,
            anchorCount = 4,
            shopChance = 0.5f,
            waypointWeights = new List<RoomWeight> { new RoomWeight(RoomType.Fight, 40), new RoomWeight(RoomType.Event, 30) },
            prizeWeights = new List<PrizeWeight> { new PrizeWeight(PrizeKind.Relic, 5) },
        });

        [Test]
        public void ADesignedRealmsProfileAndNameAreSavedAtOpenAndSurviveReload()
        {
            var save = SaveSlotManager.CurrentSave;
            save.activeRun = new RunSnapshot { hasRun = true, runSeed = Seed, legStartStep = 16 };

            var map = RealmOpening.Open(save, save.activeRun, new List<ResolvedRealm> { DesignedFloorThree() });

            Assert.AreEqual("The Ashen Court", save.activeRun.realmName);
            Assert.AreEqual(3, save.activeRun.realmProfile.pocketCount);
            Assert.IsTrue(SaveSystem.Save(save, 0));

            var reloaded = SaveSystem.Load(0).activeRun;

            Assert.AreEqual("The Ashen Court", reloaded.realmName);
            Assert.AreEqual("The Ashen Court", RealmOpening.NameOf(reloaded));
            Assert.AreEqual(16, reloaded.realmOpenedAt);
            var profile = reloaded.realmProfile;
            Assert.AreEqual((3, 1, 3, 2, 4), (profile.pocketCount, profile.pocketLengthMin, profile.pocketLengthMax, profile.forkCount, profile.anchorCount));
            Assert.AreEqual(0.5f, profile.shopChance);
            CollectionAssert.AreEqual(
                new[] { new RoomWeight(RoomType.Fight, 40), new RoomWeight(RoomType.Event, 30) }, profile.waypointWeights);
            CollectionAssert.AreEqual(new[] { new PrizeWeight(PrizeKind.Relic, 5) }, profile.prizeWeights);
            Assert.AreEqual(map.Nodes.Count, RealmOpening.MapOf(reloaded).Nodes.Count,
                "the saved profile regenerates the realm it opened");
        }

        [Test]
        public void AnUndesignedFloorSavesTheDefaultNameAndAnOldSaveReadsAsIt()
        {
            var save = SaveSlotManager.CurrentSave;
            save.activeRun = new RunSnapshot { hasRun = true, runSeed = Seed, legStartStep = 24 };

            RealmOpening.Open(save, save.activeRun, new List<ResolvedRealm> { DesignedFloorThree() });

            Assert.AreEqual("The Descent", save.activeRun.realmName);
            Assert.AreEqual(RealmProfiles.Default(4).pocketCount, save.activeRun.realmProfile.pocketCount);

            var old = JsonUtility.FromJson<RunSnapshot>("{\"hasRun\":true,\"runSeed\":5,\"legStartStep\":0}");
            Assert.AreEqual("The Descent", RealmOpening.NameOf(old));
        }

        [Test]
        public void StrengthIsFrozen_LevellingMidRealmNeverRelabels()
        {
            RunManager.StartRun(Seed);
            var before = RunManager.Run.nodeStrength.Select(e => (e.nodeId, e.strength)).ToList();

            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                character.level = 60;
                character.ClaimTrackRewards(RewardTracks.For(character), character.level);
            }

            RunManager.ResetForTests();
            Assert.IsNotNull(RunManager.Map);
            CollectionAssert.AreEqual(before, RunManager.Run.nodeStrength.Select(e => (e.nodeId, e.strength)).ToList());
        }

        // The mix the real starting squad sees on floor 1, on fixed seeds. The
        // EV sheet fitted T1/T2 to 40/40/20 over floors 1-10 for a party that
        // keeps pace with the enemy curve, and expects floor 1 to skew easy
        // (64/32/4 at its reference party). T1/T2 are rescaled to the squad's
        // real weight, 5760; measured 60/35/4 over seeds 1-100. Pinned with a
        // tolerance, so a retune of the thresholds, the squad or the pool shows
        // here (docs/handoffs/run_map/EV_SHEET.md section 2).
        [Test]
        public void TheStartingSquadsFloorOneStrengthMixIsPinned()
        {
            var counts = new int[4];
            for (ulong seed = 1; seed <= 100; seed++)
            {
                RunManager.StartRun(seed);
                foreach (var entry in RunManager.Run.nodeStrength) counts[entry.strength]++;
            }

            int total = counts[1] + counts[2] + counts[3];
            Assert.Greater(total, 500, "vacuity: the sample weighed almost no fights");
            float s1 = counts[1] / (float)total, s2 = counts[2] / (float)total, s3 = counts[3] / (float)total;
            string mix = $"{s1:P0} / {s2:P0} / {s3:P0} over {total} fights";
            TestContext.WriteLine("floor-1 strength mix: " + mix);

            Assert.That(s1, Is.InRange(0.50f, 0.70f), mix);
            Assert.That(s2, Is.InRange(0.25f, 0.45f), mix);
            Assert.That(s3, Is.InRange(0.01f, 0.10f), mix);
        }

        // ---- the step contract ----------------------------------------------

        [Test]
        public void APocketRoomTakesItsRejoinStep_AndEveryMoveCountsARoom()
        {
            ulong seed = RealmPaths.SeedWhere(m => m.Pockets.Any(p => p.Rooms.Count >= 2), "a two-room pocket");
            RunManager.StartRun(seed);
            var map = RunManager.Map;
            var pocket = map.Pockets.First(p => p.Rooms.Count >= 2);
            var second = map.Node(pocket.Rooms[1]);

            RealmPaths.StandBefore(second);
            Assert.AreEqual(pocket.Stretch + 1, RunManager.CurrentNode.Offset, "fixture: standing in the pocket");
            int walked = RunManager.Run.roomsWalked;

            Assert.IsTrue(RunManager.MoveTo(second.Id));

            Assert.AreEqual(pocket.Stretch + 1, RunManager.Run.step, "a pocket room is at its rejoin offset");
            Assert.AreEqual(walked + 1, RunManager.Run.roomsWalked);
            Assert.Greater(RunManager.Run.roomsWalked, RunManager.Run.step, "a pocket adds rooms, not steps");
        }

        // ---- the skip heal ---------------------------------------------------

        [Test]
        public void WalkingPastAPocketHealsTheLivingEightPercent_AndTheCardSaysSo()
        {
            ulong seed = RealmPaths.SeedWhere(m => RealmPaths.FightPastAPocket(m) != null, "a fight past a pocket");
            RunManager.StartRun(seed);
            var map = RunManager.Map;
            var past = RealmPaths.FightPastAPocket(map);
            RealmPaths.StandBefore(past);

            var pocketHead = RunManager.Choices().First(n => n.Role == RealmNodeRole.Pocket);
            Assert.AreEqual(8, MapNodePreview.For(RunManager.Run, past).SkipHealPercent, "the spine choice's card");
            Assert.AreEqual(0, MapNodePreview.For(RunManager.Run, pocketHead).SkipHealPercent, "the pocket's card");

            var save = SaveSlotManager.CurrentSave;
            var squad = save.ActiveSquad();
            var run = RunManager.Run;
            run.currentHealth.Clear();
            var max = new Dictionary<string, int>();
            for (int i = 0; i < squad.Count; i++)
            {
                int m = ContentDatabase.EffectiveStats(squad[i]).maxHealth;
                max[squad[i].definitionId] = m;
                run.currentHealth.Add(new RunHealthEntry { characterId = squad[i].definitionId, hp = i == 0 ? 0 : m / 2 });
            }

            Assert.IsTrue(RunManager.MoveTo(past.Id));

            for (int i = 0; i < squad.Count; i++)
            {
                string id = squad[i].definitionId;
                int hp = run.currentHealth.First(e => e.characterId == id).hp;
                int expected = i == 0 ? 0 : max[id] / 2 + (max[id] * 8 + 99) / 100;
                Assert.AreEqual(expected, hp, i == 0 ? "the skip heal revived a downed member" : $"{id} healed wrong");
            }
        }

        [Test]
        public void EnteringThePocketPaysNoSkipHeal()
        {
            ulong seed = RealmPaths.SeedWhere(m => RealmPaths.FightPastAPocket(m) != null, "a fight past a pocket");
            RunManager.StartRun(seed);
            RealmPaths.StandBefore(RealmPaths.FightPastAPocket(RunManager.Map));
            var pocketHead = RunManager.Choices().First(n => n.Role == RealmNodeRole.Pocket);

            var run = RunManager.Run;
            var member = SaveSlotManager.CurrentSave.ActiveSquad()[0];
            run.currentHealth.Clear();
            run.currentHealth.Add(new RunHealthEntry { characterId = member.definitionId, hp = 10 });

            Assert.IsTrue(RunManager.MoveTo(pocketHead.Id));
            Assert.AreEqual(10, run.currentHealth[0].hp);
        }

        // ---- the preview is the fight (plan section 5) ------------------------

        [Test]
        public void EveryFightNodesPreviewIsTheRosterTheFightFields_On200Seeds()
        {
            int compared = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                RunManager.StartRun(seed);
                var run = RunManager.Run;
                var save = SaveSlotManager.CurrentSave;

                foreach (var node in RunManager.Map.Nodes.Where(n => IsFightNode(n) || n.Type == RoomType.Boss))
                {
                    var preview = RunEncounter.Preview(run, node).EnemyIds.ToList();

                    run.currentNodeId = node.Id;
                    run.step = run.legStartStep + node.Offset;
                    var fielded = RunEncounter.For(save, run, node.Type).EnemyIds.ToList();

                    CollectionAssert.AreEqual(fielded, preview, $"seed {seed} node {node.Id} ({node.Type})");
                    if (node.IsAnchor) Assert.AreEqual(1, preview.Count, $"seed {seed}: an anchor is one enemy");
                    compared++;
                }
            }

            Assert.Greater(compared, 1000, "vacuity: almost no fights compared");
        }

        // ---- chips --------------------------------------------------------

        [Test]
        public void AGoldChipBanksItsQuotedGold_AConsumableChipStocksItsQuotedPotions()
        {
            int paidGold = 0, paidPotions = 0;
            for (ulong seed = 1; seed <= 20 && (paidGold == 0 || paidPotions == 0); seed++)
            {
                RunManager.StartRun(seed);
                var run = RunManager.Run;
                var save = SaveSlotManager.CurrentSave;

                foreach (var node in RunManager.Map.Nodes.Where(n => n.Type == RoomType.Fight))
                {
                    var chip = RealmPrizes.ChipOf(run, node);
                    int gold = run.gold, earned = run.goldEarned;
                    int potions = save.stockpiledItems.Sum(e => e.count);

                    var notices = RealmPrizes.Pay(save, run, node);

                    if (chip.Kind == PrizeKind.Gold)
                    {
                        Assert.AreEqual(gold + chip.Amount, run.gold);
                        Assert.AreEqual(earned + chip.Amount, run.goldEarned, "chip gold is earned gold");
                        Assert.AreEqual(1, notices.Count);
                        paidGold++;
                    }
                    else
                    {
                        Assert.AreEqual(PrizeKind.Consumable, chip.Kind);
                        Assert.AreEqual(potions + chip.Amount, save.stockpiledItems.Sum(e => e.count));
                        Assert.AreEqual(chip.Amount, notices.Count, "one line a potion");
                        paidPotions++;
                    }
                }
            }

            Assert.Greater(paidGold, 0, "vacuity: no gold chip paid");
            Assert.Greater(paidPotions, 0, "vacuity: no consumable chip paid");
        }

        // ---- save 8 -> 9 ------------------------------------------------------

        // A v8 run as the old map left it: absolute step, an old node id, and
        // whatever was open in that room.
        private static SaveData AVersion8Save(int legStartStep, int step, bool resolved)
        {
            var save = SaveSlotManager.CurrentSave;
            save.version = 8;
            save.activeRun = new RunSnapshot
            {
                hasRun = true,
                runSeed = Seed,
                legStartStep = legStartStep,
                step = step,
                floor = RunDepth.FloorFor(legStartStep),
                currentNodeId = 7,
                clearedNodeIds = resolved ? new List<int> { 1, 4, 7 } : new List<int> { 1, 4 },
                gold = 33,
            };
            return save;
        }

        private static RealmMap MigratedMap(SaveData save) =>
            RealmMapGenerator.GenerateFor(Seed, save.activeRun.legStartStep, save.activeRun.realmProfile);

        private static void AssertPlacedAt(SaveData save, int offset)
        {
            var run = save.activeRun;
            var map = MigratedMap(save);
            var placed = map.Node(run.currentNodeId);

            Assert.AreEqual(SaveData.CurrentVersion, save.version);
            Assert.AreEqual(9, SaveData.CurrentVersion);
            Assert.AreEqual(run.legStartStep, run.realmOpenedAt, "the realm was not opened");
            Assert.IsNotEmpty(run.nodeStrength, "no strength was weighed");
            Assert.AreEqual(offset, placed.Offset, "the party stands at the wrong offset");
            Assert.IsTrue(RealmMap.OnSpine(placed), "the party was placed off the spine");
            Assert.IsFalse(placed.IsAnchor, "the party was placed on an anchor");
            Assert.AreEqual(run.legStartStep + offset, run.step);
            Assert.AreEqual(run.step, run.roomsWalked);
            Assert.AreEqual(33, run.gold, "gold is kept");

            var walked = map.Nodes.Where(n => RealmMap.OnSpine(n) && n.Offset >= 1 && n.Offset <= offset).ToList();
            Assert.AreEqual(offset, run.clearedNodeIds.Count, "one walked room per offset");
            CollectionAssert.IsSubsetOf(run.clearedNodeIds, walked.Select(n => n.Id));
            if (offset > 0) CollectionAssert.Contains(run.clearedNodeIds, placed.Id);

            CollectionAssert.AreEquivalent(
                map.Nodes.Where(n => n.IsAnchor && n.Offset <= offset).Select(n => n.Id), run.slainAnchorIds,
                "anchors behind the party must be slain, and only those");
        }

        [Test]
        public void Migration_ResolvedRoomAtOffsetFive_StandsThere()
        {
            var save = AVersion8Save(8, 13, resolved: true);
            Assert.IsTrue(save.Migrate());
            AssertPlacedAt(save, 5);
        }

        [Test]
        public void Migration_OpenFightAtOffsetFive_StandsBeforeIt()
        {
            var save = AVersion8Save(8, 13, resolved: false);
            Assert.IsTrue(save.Migrate());
            AssertPlacedAt(save, 4);
        }

        [Test]
        public void Migration_OpenEventAtOffsetFive_StandsBeforeItAndClosesIt()
        {
            var save = AVersion8Save(8, 13, resolved: false);
            save.activeRun.eventId = "petting_zoo";
            save.activeRun.eventNodeId = 7;
            save.activeRun.eventPageId = "start";
            save.activeRun.eventResult = "something";
            save.activeRun.pendingFight = "a_fight";
            save.activeRun.pendingShelf = "a_shelf";

            Assert.IsTrue(save.Migrate());

            AssertPlacedAt(save, 4);
            var run = save.activeRun;
            Assert.AreEqual("", run.eventId);
            Assert.AreEqual(-1, run.eventNodeId);
            Assert.AreEqual("", run.eventPageId);
            Assert.AreEqual("", run.eventResult);
            Assert.AreEqual("", run.pendingFight);
            Assert.AreEqual("", run.pendingShelf);
        }

        [Test]
        public void Migration_OpenShopAtOffsetFive_StandsBeforeItAndDropsTheShelf()
        {
            var save = AVersion8Save(8, 13, resolved: false);
            save.activeRun.shopNodeId = 7;
            save.activeRun.shopStock = new List<ShopStockEntry> { new ShopStockEntry() };

            Assert.IsTrue(save.Migrate());

            AssertPlacedAt(save, 4);
            Assert.AreEqual(-1, save.activeRun.shopNodeId);
            Assert.IsEmpty(save.activeRun.shopStock);
        }

        [Test]
        public void Migration_TheBossRoom_IsNeverSkipped()
        {
            var save = AVersion8Save(8, 16, resolved: false);
            Assert.IsTrue(save.Migrate());

            AssertPlacedAt(save, RealmMap.SpineLength - 1);
            RunManager.ResetForTests();
            Assert.IsTrue(RunManager.Choices().Any(n => n.Type == RoomType.Boss), "the boss is not the next choice");
        }

        [Test]
        public void Migration_AStepBelowItsLegStart_StandsAtTheEntry()
        {
            var save = AVersion8Save(16, 3, resolved: true);
            Assert.IsTrue(save.Migrate());

            AssertPlacedAt(save, 0);
            Assert.AreEqual(MigratedMap(save).Entry.Id, save.activeRun.currentNodeId);
        }

        [Test]
        public void Migration_OffsetZero_StandsAtTheEntryWithNothingWalked()
        {
            var save = AVersion8Save(8, 8, resolved: false);
            Assert.IsTrue(save.Migrate());

            AssertPlacedAt(save, 0);
            Assert.IsEmpty(save.activeRun.clearedNodeIds, "the entry is not a room");
        }

        // The guard the lazy open relies on: a v9 field missing from a save
        // keeps its initialiser through JsonUtility.
        [Test]
        public void ASnapshotWithoutRealmFieldsReadsAsNotOpened()
        {
            var run = JsonUtility.FromJson<RunSnapshot>("{\"hasRun\":true,\"runSeed\":5,\"legStartStep\":0}");
            Assert.AreEqual(-1, run.realmOpenedAt);
            Assert.IsNotNull(run.slainAnchorIds);
        }
    }
}
