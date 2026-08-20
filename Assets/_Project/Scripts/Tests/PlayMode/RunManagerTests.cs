using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Dungeon;

namespace PrincesPalace.PlayModeTests
{
    // The descent, between scenes.
    //
    // PlayMode rather than EditMode only because it writes save files, and
    // SaveSystem is Core. None of it needs a scene -- there is no scene loaded
    // in any of these.
    public class RunManagerTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-run-tests-" + System.Guid.NewGuid().ToString("N"));
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

        private const ulong Seed = 12345;

        [Test]
        public void ThereIsNoRunUntilOneIsStarted()
        {
            Assert.IsFalse(RunManager.HasRun);
            Assert.IsNull(RunManager.Map);
            Assert.IsNull(RunManager.CurrentNode);
        }

        [Test]
        public void StartingARunPutsThePartyAtTheEntry()
        {
            RunManager.StartRun(Seed);

            Assert.IsTrue(RunManager.HasRun);
            Assert.IsNotNull(RunManager.CurrentNode);
            Assert.AreEqual(RoomType.Entry, RunManager.CurrentNode.Type);
            Assert.AreEqual(0, RunManager.CurrentNode.Depth);
        }

        [Test]
        public void TheMapIsRegeneratedFromTheSeed_NotSerialised()
        {
            // A seed and a generator reproduce a map exactly; a serialised map is
            // a second representation that can drift from the generator that made
            // it. Dropping the cache and asking again has to give the same map.
            RunManager.StartRun(Seed);
            var first = RunManager.Map.Nodes.Select(n => (n.Id, n.Type, n.Depth)).ToList();

            RunManager.ResetForTests();
            var second = RunManager.Map.Nodes.Select(n => (n.Id, n.Type, n.Depth)).ToList();

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void ADifferentSeedIsADifferentDescent()
        {
            RunManager.StartRun(Seed);
            var first = RunManager.Map.Nodes.Select(n => n.Type).ToList();

            RunManager.StartRun(Seed + 1);
            var second = RunManager.Map.Nodes.Select(n => n.Type).ToList();

            Assert.AreNotEqual(first, second, "two seeds produced identical rooms - the cache was not invalidated");
        }

        [Test]
        public void MovingIsRefusedToAnywhereNotActuallyReachable()
        {
            // The map is the authority on adjacency. A UI that offered a wrong
            // choice would otherwise teleport the party across the descent.
            RunManager.StartRun(Seed);
            int entry = RunManager.CurrentNode.Id;

            Assert.IsFalse(RunManager.MoveTo(9999), "a node that does not exist");

            var unreachable = RunManager.Map.Nodes
                .FirstOrDefault(n => n.Id != entry && !RunManager.Map.CanMove(entry, n.Id));
            if (unreachable != null)
            {
                Assert.IsFalse(RunManager.MoveTo(unreachable.Id), "a real node that is not adjacent");
            }

            Assert.AreEqual(entry, RunManager.CurrentNode.Id, "nothing moved");
        }

        [Test]
        public void MovingToARealChoiceAdvancesTheStep()
        {
            RunManager.StartRun(Seed);
            var choice = RunManager.Choices().First();

            Assert.IsTrue(RunManager.MoveTo(choice.Id));

            Assert.AreEqual(choice.Id, RunManager.CurrentNode.Id);
            Assert.AreEqual(choice.Depth, RunManager.Run.step, "the step is the room's own depth");
        }

        [Test]
        public void ClearingARoomIsIdempotent()
        {
            RunManager.StartRun(Seed);

            RunManager.ClearCurrentRoom();
            RunManager.ClearCurrentRoom();

            Assert.AreEqual(1, RunManager.Run.clearedNodeIds.Count);
        }

        [Test]
        public void GoldAccumulatesAcrossRooms()
        {
            RunManager.StartRun(Seed);

            RunManager.BankPayout(30);
            RunManager.BankPayout(12);

            Assert.AreEqual(42, RunManager.Run.gold);
        }

        [Test]
        public void ALosingPayoutBanksNothing()
        {
            RunManager.StartRun(Seed);

            RunManager.BankPayout(0);

            Assert.AreEqual(0, RunManager.Run.gold);
        }

        [Test]
        public void ARunSurvivesBeingReadBackFromDisk()
        {
            // The whole point of persisting on every move: hub -> fight -> hub is
            // three scenes, and a run that only lived in memory would not survive
            // the player quitting between rooms.
            RunManager.StartRun(Seed);
            var choice = RunManager.Choices().First();
            RunManager.MoveTo(choice.Id);
            RunManager.BankPayout(25);

            // Drop every cache and come back to it cold.
            SaveSlotManager.Forget();
            RunManager.ResetForTests();

            Assert.IsTrue(RunManager.HasRun);
            Assert.AreEqual(choice.Id, RunManager.CurrentNode.Id);
            Assert.AreEqual(25, RunManager.Run.gold);
        }

        [Test]
        public void EndingARunLeavesNothingBehind()
        {
            RunManager.StartRun(Seed);
            RunManager.BankPayout(99);

            RunManager.EndRun();

            Assert.IsFalse(RunManager.HasRun);
            Assert.IsNull(RunManager.CurrentNode);

            SaveSlotManager.Forget();
            Assert.IsFalse(RunManager.HasRun, "and it stays ended after a reload");
        }

        // Walks the leg the way the fight's auto-advance does.
        private static RoomType WalkToTheEndOfTheLeg()
        {
            int guard = 0;
            while (!RunManager.LegIsOver() && guard++ < 64)
            {
                RunManager.MoveTo(RunManager.Choices().First().Id);
            }

            Assert.Less(guard, 64, "the walk never terminated");
            return RunManager.CurrentNode.Type;
        }

        [Test]
        public void TheFirstLegEndsAtAnElite_NotABoss()
        {
            // A LEG IS NOT A RUN, and this is the correction that matters: a leg
            // is eight steps and bosses fall every sixteen, so leg 0 finishes on
            // an ELITE. Treating "no choices left" as the end of the run would
            // have stopped every descent here.
            RunManager.StartRun(Seed);

            Assert.AreEqual(RoomType.EliteFight, WalkToTheEndOfTheLeg());
        }

        [Test]
        public void TheSecondLegEndsAtTheBoss()
        {
            RunManager.StartRun(Seed);
            WalkToTheEndOfTheLeg();

            RunManager.AdvanceLeg();

            Assert.AreEqual(RoomType.Boss, WalkToTheEndOfTheLeg());
        }

        [Test]
        public void AdvancingALegKeepsTheRunsOwnSeed()
        {
            // Only the start step moves. A fresh seed per leg would make a
            // resumed run a different dungeon below the join.
            RunManager.StartRun(Seed);
            ulong seed = RunManager.Run.runSeed;

            RunManager.AdvanceLeg();

            Assert.AreEqual(seed, RunManager.Run.runSeed);
            Assert.AreEqual(DescentMapGenerator.DefaultLegLength, RunManager.Run.legStartStep);
            Assert.AreEqual(RoomType.Entry, RunManager.CurrentNode.Type, "and it opens at the new leg's entry");
        }

        [Test]
        public void AFreshLegForgetsWhatTheLastOneCleared()
        {
            // Cleared ids are node ids from the PREVIOUS map, and node ids are
            // reused across legs -- carrying them would grey out rooms the party
            // has never seen.
            RunManager.StartRun(Seed);
            RunManager.ClearCurrentRoom();

            RunManager.AdvanceLeg();

            CollectionAssert.IsEmpty(RunManager.Run.clearedNodeIds);
        }
        // ---- step is absolute, and a node's depth is not -------------------------
        //
        // THE BUG THIS EXISTS FOR: MoveTo wrote `run.step = target.Depth`, and
        // Depth is a node's column WITHIN ITS LEG. AdvanceLeg writes the
        // absolute legStartStep into the same field, the difficulty curve
        // compounds per step and RunDepth reads it -- so the two disagreed the
        // moment a run left its first leg, and ONLY then, which is why it
        // survived. A real save read step 1 against legStartStep 8: nine rooms
        // deep, being scaled for the second.
        [Test]
        public void MovingOnTheSecondLegCountsFromTheLegsStart()
        {
            RunManager.StartRun(Seed);
            RunManager.AdvanceLeg();

            var run = RunManager.Run;
            Assert.AreEqual(DescentMapGenerator.DefaultLegLength, run.legStartStep,
                "the fixture did not actually reach the second leg");

            var choices = RunManager.Choices();
            CollectionAssert.IsNotEmpty(choices, "the second leg offered nowhere to go");

            var target = choices[0];
            Assert.IsTrue(RunManager.MoveTo(target.Id), "the move was refused");

            Assert.AreEqual(run.legStartStep + target.Depth, run.step,
                $"the party stands at column {target.Depth} of a leg starting at {run.legStartStep}, " +
                $"so it is {run.legStartStep + target.Depth} rooms deep - step says {run.step}");
        }

        // The premise, pinned: on the FIRST leg the two are identical, which is
        // exactly why nothing caught this. Without this line the test above
        // could be "fixed" by making legStartStep zero forever and still pass.
        [Test]
        public void OnTheFirstLegDepthAndStepAgree()
        {
            RunManager.StartRun(Seed);
            Assert.AreEqual(0, RunManager.Run.legStartStep);

            var target = RunManager.Choices()[0];
            RunManager.MoveTo(target.Id);

            Assert.AreEqual(target.Depth, RunManager.Run.step,
                "on leg 1 the absolute step and the column are the same number");
        }

        // ---- a run does not survive the process ---------------------------------
        //
        // THE BUG THIS EXISTS FOR: the rule was enforced by a
        // RuntimeInitializeOnLoadMethod that runs BEFORE ANY SCENE, when
        // SaveSlotManager.CurrentSlot is still its default 0. So it could only
        // ever settle slot 0, and a descent left in any other slot came back
        // alive -- dropping the player straight back into the fight they had
        // quit, every launch. Found in a real save: slot 5 holding a live run
        // mid-fight, which by this rule is a state that cannot exist at rest.
        [Test]
        public void ARunLeftInAnotherSlotIsSettledWhenThatSlotIsOpened()
        {
            SaveSlotManager.CurrentSlot = 4;
            SaveSlotManager.Forget();

            RunManager.StartRun(Seed);
            RunManager.Run.bossesKilled.Add("boss_a");
            SaveSlotManager.SaveCurrent();
            Assert.IsTrue(RunManager.HasRun, "the fixture did not leave a run behind");

            // A fresh process: the cache is dropped and the boot check runs
            // against slot 0, exactly as it does for real.
            SaveSlotManager.Forget();
            SaveSlotManager.CurrentSlot = 0;
            RunManager.ResetForTests();

            // ...and the player opens slot 5 again.
            SaveSlotManager.CurrentSlot = 4;
            SaveSlotManager.Forget();
            RunManager.SettleOnOpening();

            Assert.IsFalse(RunManager.HasRun,
                "the run in slot 5 survived the process, so opening it drops the player back into " +
                "the fight they quit");
        }

        // And settled rather than discarded: opening the slot must still pay
        // out what the descent earned, or a crash silently costs the player
        // every ember its bosses owed them.
        [Test]
        public void SettlingOnOpeningStillPaysWhatTheRunEarned()
        {
            SaveSlotManager.CurrentSlot = 4;
            SaveSlotManager.Forget();

            RunManager.StartRun(Seed);
            RunManager.Run.bossesKilled.Add("boss_a");
            RunManager.Run.roomsCleared = 5;

            int before = SaveSlotManager.CurrentSave.EmberTotal();
            RunManager.SettleOnOpening();

            Assert.Greater(SaveSlotManager.CurrentSave.EmberTotal(), before,
                "the abandoned run was discarded rather than settled - its bosses paid nothing");
        }

        // ---- the room after the elite -------------------------------------------
        //
        // PROBE for "after the elite, going into a fight it just hangs and you
        // cant do anything anymore". FightBootstrap returns a null session when
        // the roster is empty, which leaves an empty stage and no way to act --
        // a hang. This walks a run over the leg boundary the elite sits on and
        // asks what the next room would field.
        [Test]
        public void TheRoomAfterTheEliteFieldsAFight()
        {
            RunManager.StartRun(Seed);
            var save = SaveSlotManager.CurrentSave;

            // Finish the elite: the leg's last room, which is what triggers
            // AdvanceLeg in FightBootstrap.
            RunManager.AdvanceLeg();

            var run = RunManager.Run;
            var report = $"legStartStep={run.legStartStep} step={run.step} floor={run.floor} " +
                         $"node={run.currentNodeId} squad=[{string.Join(",", save.ActiveSquadIds())}] " +
                         $"health=[{string.Join(",", (run.currentHealth ?? new System.Collections.Generic.List<RunHealthEntry>()).Select(h => h.characterId + ":" + h.hp))}]";

            var choices = RunManager.Choices();
            Assert.IsNotEmpty(choices, "the new leg offered nowhere to go. " + report);

            RunManager.MoveTo(choices[0].Id);
            run = RunManager.Run;

            var roster = RunEncounter.For(save, run, PrincesPalace.Domain.Dungeon.RoomType.Fight);

            Assert.IsFalse(roster.IsEmpty,
                $"the room after the elite fields nothing, so the stage comes up empty and the " +
                $"player can do nothing. party={roster.PartyIds?.Count ?? 0} " +
                $"enemies={roster.EnemyIds?.Count ?? 0}. {report}");
        }


    }
}
