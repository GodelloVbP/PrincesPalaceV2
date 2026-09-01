using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;

namespace PrincesPalace.PlayModeTests
{
    // THE BOT ITSELF, PLAYED SMALL: twenty seeds through both archetypes on the
    // Fresh profile, capped at sixteen steps.
    //
    // What this is FOR is not balance -- balance is what tools/bot.ps1's batch
    // reports, and no number in it is a pass/fail. This is the gate that says
    // the batch's numbers are worth reading at all: every run terminates,
    // nothing throws, the same seed replays identically, and no run-level
    // invariant fires that is not a bug already on the books.
    //
    // Twenty seeds and a cap of sixteen rather than the batch's forty is a
    // deliberate budget: the whole class has to stay well under half a minute
    // beside the rest of the PlayMode suite, and depth is what the BATCH
    // measures. What this needs from a run is that it plays several rooms,
    // fights, settles and either dies or caps -- which sixteen steps is plenty
    // for on Fresh.
    public class BalanceBotSmokeTests
    {
        private const int Seeds = 20;
        private const int DepthCapSteps = 16;
        private const ulong FirstSeed = 1;

        private static readonly string[] Archetypes = { "RandomLegal", "GreedyAggressive" };

        // A PRE-EXISTING PRODUCTION FAULT, COUNTED RATHER THAN FAILED ON.
        //
        // A fight that spends a second-life charge can come back sitting on an
        // enemy turn nothing resolves -- FightController.RescueAStalledEnemyTurn
        // is the screen's repair for it and FightSettlementTests documents it as
        // "a pre-existing production fault these tests trip, not one they
        // cause". FightRunner mirrors that repair headlessly and records a
        // StalledEnemyTurn hit each time, so the batch report can say how often
        // the live game does it.
        //
        // Excluded from the assertion below, not from the count: failing here
        // would only mean this file gets an exclusion added the first time
        // somebody runs it, and asserting a literal count would fail the day
        // someone FIXES the bug. The number is printed instead, so it is in
        // front of whoever reads the run.
        private const string KnownStall = "StalledEnemyTurn";

        [TearDown]
        public void LeaveNoStaticsBehind()
        {
            // The bot's default. Restored explicitly because
            // TheInMemorySaveModeChangesNothingAboutHowARunPlays turns it off,
            // and a test that leaked "off" would make every later test in the
            // class write thousands of save files nobody reads.
            BotRunDriver.InMemorySaves = true;
            SaveSystem.InMemory = false;
            SaveSystem.ClearMemory();

            // BotRunDriver.PlayRun restores all of these in its own finally, so
            // this is belt and braces against a future edit that throws before
            // the try -- GlobalStateTests is the lint that would otherwise
            // catch it two hundred tests later.
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            RunManager.ResetForTests();
        }

        [Test]
        public void EverySmokeRunTerminatesAndTripsNothingButTheKnownStall()
        {
            var hitsByName = new Dictionary<string, List<string>>();
            int capped = 0;
            int died = 0;

            foreach (string archetype in Archetypes)
            {
                for (ulong seed = FirstSeed; seed < FirstSeed + Seeds; seed++)
                {
                    var result = BotRunDriver.PlayRun(seed, archetype, ProfilePresets.Fresh, DepthCapSteps);

                    // TERMINATION IS THE HEADLINE. A run that neither died nor
                    // capped came out of the loop through a guard, and a batch
                    // of those would report depth numbers measuring a hang.
                    Assert.IsTrue(result.Trace.Capped || result.Trace.DeathStep > 0,
                        $"seed {seed} / {archetype} ended without dying or reaching the cap: " +
                        $"{result.Trace.Fights.Count} fights, {result.Trace.Rooms.Count} rooms, " +
                        $"hits [{string.Join(" | ", result.Hits.Select(h => h.ToString()))}]");

                    if (result.Trace.Capped) capped++; else died++;

                    foreach (var hit in result.Hits)
                    {
                        if (!hitsByName.TryGetValue(hit.Name, out var where))
                        {
                            where = new List<string>();
                            hitsByName[hit.Name] = where;
                        }
                        where.Add($"seed {seed}/{archetype}: {hit.Detail}");
                    }
                }
            }

            int stalls = hitsByName.TryGetValue(KnownStall, out var stallRows) ? stallRows.Count : 0;
            UnityEngine.Debug.Log(
                $"[BalanceBotSmokeTests] {Seeds * Archetypes.Length} runs: {died} died, {capped} capped, " +
                $"{stalls} {KnownStall} hits (a known production fault -- see this class's own comment).");

            var unexpected = hitsByName
                .Where(kv => kv.Key != KnownStall)
                .Select(kv => $"{kv.Key} x{kv.Value.Count} (first: {kv.Value[0]})")
                .ToList();

            CollectionAssert.IsEmpty(unexpected,
                "the smoke seeds tripped a run-level invariant. Either the driver is asking the " +
                "orchestrator for something in the wrong order, or the game has a bug the batch " +
                "should be reporting -- both are worth reading before this is excluded.");
        }

        // THE OPTIMISATION HAS TO BE INVISIBLE TO THE GAME, and this is what
        // says so.
        //
        // BotRunDriver.InMemorySaves keeps the slot in a dictionary instead of
        // writing save_slot_0.json 28 times a run, which is 91% of a batch's
        // wall clock (see BotPhaseTimers' phase table). Persistence is not
        // supposed to influence anything a run does -- but "supposed to" is a
        // belief, and the whole value of the balance numbers rests on it, so
        // the same seed is played BOTH ways and the two RunTrace.Hash() values
        // have to be the same string.
        //
        // A hash rather than a spot-check on depth: Hash() covers every fight,
        // every turn, every offer and every room the run touched, so a
        // divergence anywhere in the run fails this, not just one that happens
        // to move the death step.
        [Test]
        public void TheInMemorySaveModeChangesNothingAboutHowARunPlays()
        {
            for (ulong seed = FirstSeed; seed < FirstSeed + 5; seed++)
            {
                foreach (string archetype in Archetypes)
                {
                    BotRunDriver.InMemorySaves = true;
                    var inMemory = BotRunDriver.PlayRun(seed, archetype, ProfilePresets.Fresh, DepthCapSteps);

                    BotRunDriver.InMemorySaves = false;
                    var onDisk = BotRunDriver.PlayRun(seed, archetype, ProfilePresets.Fresh, DepthCapSteps);

                    Assert.AreEqual(onDisk.Trace.Hash(), inMemory.Trace.Hash(),
                        $"seed {seed} / {archetype} played differently with the save in RAM than with it " +
                        "on disk. Persistence is not supposed to be able to change a run at all -- if it " +
                        "can, the in-memory mode is not the only thing that is wrong.");
                }
            }

            BotRunDriver.InMemorySaves = true;
        }

        [Test]
        public void TheSameSeedArchetypeAndProfileReplaysToTheSameTraceHash()
        {
            // Fewer seeds than the run above on purpose: this doubles the work
            // per seed, and non-determinism is a property of the machinery
            // rather than of a particular seed -- five seeds through both
            // archetypes exercises every draw the run makes (map, roster,
            // fight, offer, draft) as thoroughly as twenty would.
            for (ulong seed = FirstSeed; seed < FirstSeed + 5; seed++)
            {
                foreach (string archetype in Archetypes)
                {
                    var first = BotRunDriver.PlayRun(seed, archetype, ProfilePresets.Fresh, DepthCapSteps);
                    var second = BotRunDriver.PlayRun(seed, archetype, ProfilePresets.Fresh, DepthCapSteps);

                    Assert.AreEqual(first.Trace.Hash(), second.Trace.Hash(),
                        $"seed {seed} / {archetype} played differently the second time. Every draw a run " +
                        "makes is derived from (seed, stream, step, node), so a mismatch means something " +
                        "unseeded leaked into the loop -- which would make every batch number irreproducible.");
                }
            }
        }
    }
}
