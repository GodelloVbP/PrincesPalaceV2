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
        public void TheFirstLegEndsAtItsBoss()
        {
            // A LEG IS NOT A RUN, and this is the correction that matters: a leg
            // is eight steps and, since the D6/Phase 5B retune ("boss every
            // floor"), a boss falls every eight -- so leg 0 finishes on a BOSS,
            // with a forced elite already passed at its midpoint. Treating "no
            // choices left" as the end of the run would have stopped every
            // descent here. Was EliteFight (leg 0) then Boss (leg 1) before the
            // retune, when bosses only fell every sixteen steps.
            RunManager.StartRun(Seed);

            Assert.AreEqual(RoomType.Boss, WalkToTheEndOfTheLeg());
        }

        [Test]
        public void TheSecondLegEndsAtItsBossToo()
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

        // ---- the room after the leg boundary ------------------------------------
        //
        // PROBE for "after the leg's set piece, going into a fight it just hangs
        // and you cant do anything anymore". FightBootstrap returns a null
        // session when the roster is empty, which leaves an empty stage and no
        // way to act -- a hang. This walks a run over a leg boundary and asks
        // what the next room would field. Named for the elite it used to be
        // (leg 0 ended on one, before the D6/Phase 5B retune made every leg end
        // on its boss instead) -- what it actually exercises is AdvanceLeg's own
        // seam, which does not care which room forced the boundary.
        [Test]
        public void TheRoomAfterTheEliteFieldsAFight()
        {
            RunManager.StartRun(Seed);
            var save = SaveSlotManager.CurrentSave;

            // Finish the leg: AdvanceLeg does not require the walk to have
            // actually reached the forced room, which is what triggers
            // AdvanceLeg in FightBootstrap for a real player.
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

        // ---- what a new run does NOT take away ----------------------------------
        //
        // Character.cs claimed for a long time that level, exp and unspent
        // points were per-run and that StartRun reset all four, closing with
        // "there is a test whose whole job is to fail if it ever stops". There
        // was no such test, and StartRun has never touched the roster -- so the
        // stated invariant was inverted AND unguarded, which is the worst of
        // the four possible combinations: anyone reading it would price levels
        // as a within-run curve and design rewards ("start every run with two
        // relics") that cannot exist under it.
        //
        // This is that test, asserting the rule that is actually true and the
        // one the reward track is built on: LEVELS ARE META. Written against
        // the fields directly rather than through AddExperience, because what
        // is under test is what StartRun does to them, not how they got there.
        [Test]
        public void StartingARunKeepsEveryCharactersLevel()
        {
            RunManager.StartRun(Seed);

            var save = SaveSlotManager.CurrentSave;
            var character = save.roster[0];
            character.level = 7;
            character.exp = 250;
            character.unspentStatPoints = 3;
            SaveSlotManager.SaveCurrent();

            // The next descent. Same save, same roster, new run.
            RunManager.StartRun(Seed + 1);

            var after = SaveSlotManager.CurrentSave.roster[0];
            Assert.AreEqual(7, after.level,
                "starting a run reset the character's level -- levels are meta and survive a " +
                "descent, which is what the whole reward track hangs on");
            Assert.AreEqual(250, after.exp, "starting a run reset progress toward the next level");
            Assert.AreEqual(3, after.unspentStatPoints,
                "starting a run took away points that were earned and not yet placed");
        }

        // ---- level 30: a rest before every boss ----------------------------------

        private static void LevelTheSquadTo(int level)
        {
            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                character.level = level;
            }
        }

        [Test]
        public void AnUnlevelledSquadGetsNoGuaranteedRest()
        {
            RunManager.StartRun(Seed);

            Assert.IsFalse(RunManager.Run.restBeforeBoss,
                "a level-1 squad is getting a reward it has not earned");
        }

        [Test]
        public void ALevelThirtySquadDescendsWithTheRestGuaranteed()
        {
            // Squad levelled BEFORE the run starts, because the rule is fixed
            // at StartRun -- which is the point of the next test.
            RunManager.StartRun(Seed);
            LevelTheSquadTo(30);
            RunManager.StartRun(Seed);

            Assert.IsTrue(RunManager.Run.restBeforeBoss,
                "level 30 granted a rest before every boss and the descent did not take it");
        }

        // THE REASON THIS IS SNAPSHOT RATHER THAN READ LIVE.
        //
        // The map is not serialised -- RunManager regenerates it from the seed
        // whenever asked, which is what makes a reloaded descent identical. If
        // the generator read this rule from the squad each time, a character
        // levelling to 30 MID-DESCENT would change what it produces, and the
        // leg the player is standing in would reshape underneath them: rooms
        // already walked past turning into different rooms.
        [Test]
        public void LevellingMidDescentDoesNotReshapeTheLegUnderThePlayer()
        {
            RunManager.StartRun(Seed);
            var before = RunManager.Map.Nodes.Select(n => (n.Id, n.Type, n.Depth)).ToList();

            LevelTheSquadTo(30);

            // Drop the cache and ask again, exactly as a reload does.
            RunManager.ResetForTests();
            var after = RunManager.Map.Nodes.Select(n => (n.Id, n.Type, n.Depth)).ToList();

            CollectionAssert.AreEqual(before, after,
                "the map changed shape because a character levelled during the descent");
        }

        // ---- attrition: the party catches its breath between legs ------------------
        //
        // REPORTED AS "floor 1 after the first elite it already becomes so
        // difficult". The numbers said attrition rather than scaling: health
        // carries room to room, rest rooms are weight 6 of 90 so 62% of legs
        // contain none, levelling does not heal, and the shop that would sell a
        // potion is not built. The party crossed into leg 2 on whatever the
        // elite left them.

        private static void HurtTheSquad(int hp)
        {
            var run = RunManager.Run;
            run.currentHealth ??= new System.Collections.Generic.List<RunHealthEntry>();
            run.currentHealth.Clear();

            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                run.currentHealth.Add(new RunHealthEntry
                {
                    characterId = character.definitionId,
                    hp = hp,
                });
            }
        }

        private static int LowestHealth()
        {
            int lowest = int.MaxValue;
            foreach (var entry in RunManager.Run.currentHealth)
            {
                if (entry != null && entry.hp < lowest) lowest = entry.hp;
            }

            return lowest;
        }

        [Test]
        public void AdvancingALegRestoresTheParty()
        {
            RunManager.StartRun(Seed);
            HurtTheSquad(3);
            Assert.AreEqual(3, LowestHealth(), "the fixture did not hurt anybody");

            RunManager.AdvanceLeg();

            Assert.Greater(LowestHealth(), 3,
                "crossing into the next leg left the party on what the leg's fights took off them");

            foreach (var character in SaveSlotManager.CurrentSave.ActiveSquad())
            {
                int max = Content.ContentDatabase.EffectiveStats(character).maxHealth;
                var entry = RunManager.Run.currentHealth.Find(e => e.characterId == character.definitionId);
                Assert.AreEqual(max, entry.hp, $"{character.definitionId} did not come back to full");
            }
        }

        // The reported case exactly: walk leg 1 to its boss, cross over, and
        // check the party does not start leg 2 already spent. (Originally
        // walked to leg 1's ELITE, back when bosses only fell every sixteen
        // steps and leg 1 ended on the elite instead -- the D6/Phase 5B
        // retune moved the boss to every leg's end, so this now exercises
        // the same "party crosses a leg beat up" case against a boss.)
        [Test]
        public void TheLegAfterTheFirstBossStartsWhole()
        {
            RunManager.StartRun(Seed);
            WalkToTheEndOfTheLeg();
            Assert.AreEqual(RoomType.Boss, RunManager.CurrentNode.Type,
                "leg 1 did not end on its boss, so this is not the reported case");

            HurtTheSquad(1);
            RunManager.AdvanceLeg();

            Assert.Greater(LowestHealth(), 1,
                "the party begins leg 2 on the health the boss fight left them");
        }

        // A heal that only fired for a full squad would quietly skip anyone
        // knocked out -- the same rule the rest room follows, which is what
        // makes a rest the answer to a bad fight rather than a top up for
        // whoever survived it.
        [Test]
        public void EvenACharacterAtZeroComesBack()
        {
            RunManager.StartRun(Seed);
            HurtTheSquad(0);

            RunManager.AdvanceLeg();

            Assert.Greater(LowestHealth(), 0, "a downed character stayed down across the leg boundary");
        }

        // ---- levels 90 and 100: the second life and its refresh -------------------

        [Test]
        public void AnUnlevelledSquadHasNoSecondLife()
        {
            RunManager.StartRun(Seed);

            Assert.AreEqual(0, SquadTrack.SecondLivesLeft(RunManager.Run));
        }

        [Test]
        public void LevelNinetyGrantsExactlyOnePerDescent()
        {
            RunManager.StartRun(Seed);
            LevelTheSquadTo(90);

            Assert.AreEqual(1, SquadTrack.SecondLivesLeft(RunManager.Run));

            RunManager.Run.secondLivesUsed = 1;
            Assert.AreEqual(0, SquadTrack.SecondLivesLeft(RunManager.Run),
                "a spent second life is still on offer");
        }

        // Level 100 gives the charge back at every boss -- on ENTERING it,
        // because a charge handed back after the boss is dead is a charge for
        // the fight you already survived.
        [Test]
        public void LevelOneHundredGivesTheChargeBackOnEnteringABoss()
        {
            RunManager.StartRun(Seed);
            LevelTheSquadTo(100);
            RunManager.Run.secondLivesUsed = 1;

            WalkToTheBoss();

            Assert.AreEqual(0, RunManager.Run.secondLivesUsed,
                "entering the boss did not give the second life back");
            Assert.AreEqual(1, SquadTrack.SecondLivesLeft(RunManager.Run));
        }

        [Test]
        public void LevelNinetyAloneDoesNotRefreshAtTheBoss()
        {
            // Non-vacuity for the test above: the refresh has to be level 100's
            // doing and not something entering a boss does anyway.
            RunManager.StartRun(Seed);
            LevelTheSquadTo(90);
            RunManager.Run.secondLivesUsed = 1;

            WalkToTheBoss();

            Assert.AreEqual(1, RunManager.Run.secondLivesUsed,
                "a level-90 squad got its second life back without earning the refresh");
        }

        // Walks to the boss room, which is the last column of leg 2.
        private static void WalkToTheBoss()
        {
            int guard = 0;
            while (RunManager.CurrentNode.Type != RoomType.Boss && guard++ < 64)
            {
                if (RunManager.LegIsOver())
                {
                    RunManager.AdvanceLeg();
                    continue;
                }

                RunManager.MoveTo(RunManager.Choices().First().Id);
            }

            Assert.AreEqual(RoomType.Boss, RunManager.CurrentNode.Type, "never reached a boss");
        }

        // The other half, and the reason this is two tests rather than one:
        // the run itself IS discarded. If a future change starts resetting the
        // roster on StartRun, the test above catches it; if one stops clearing
        // the run, this catches that. They fail for different reasons.
        [Test]
        public void StartingARunStillDiscardsTheRunBeforeIt()
        {
            RunManager.StartRun(Seed);
            RunManager.Run.bossesKilled.Add("boss_a");
            RunManager.Run.roomsCleared = 5;

            RunManager.StartRun(Seed + 1);

            Assert.AreEqual(0, RunManager.Run.roomsCleared,
                "the previous descent's cleared rooms carried into the new one");
            Assert.IsEmpty(RunManager.Run.bossesKilled,
                "the previous descent's kills carried into the new one");
        }
    }
}
