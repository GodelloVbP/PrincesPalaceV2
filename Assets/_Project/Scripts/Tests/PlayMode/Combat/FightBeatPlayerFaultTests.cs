using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using Object = UnityEngine.Object;

namespace PrincesPalace.PlayModeTests
{
    // A BEAT THAT THROWS MUST NOT TAKE THE FIGHT WITH IT.
    //
    // FightController clears its busy flag only when playback reports, and its
    // watchdog (RescueAStrandedTurn) trusts FightBeatPlayer.IsPlaying. Before
    // PlayBeats gained its per-step guard and its finally, a throw anywhere
    // outside the impact block's own try/catch stopped the coroutine with
    // IsPlaying stuck true and the callback never fired: every verb dead
    // until the scene was left, and the watchdog built for it could not fire.
    //
    // Each test throws from a different kind of step -- the top of the
    // beat, the actor's opening pose, and a NESTED enumerator (CloseIn)
    // that Unity runs as a coroutine of its own -- and pins the same
    // invariant: the next beat still plays, IsPlaying goes false, and the
    // finish fires exactly once.
    //
    // Bare fixtures, no scene, the shape FightBeatPlayerFixtureTests set.
    public class FightBeatPlayerFaultTests
    {
        [UnityTest]
        public IEnumerator AThrowAtTheTopOfABeatSkipsThatBeatAndStillFinishesOnce()
        {
            var player = NewPlayer();
            var (actor, victim) = Fixture(player, throwingSlots: false, out _);

            var started = new List<int>();
            player.BeatStarted = (index, _) =>
            {
                started.Add(index);
                if (index == 0) throw new InvalidOperationException("beat-fault-top");
            };

            LogAssert.Expect(LogType.Exception, new Regex("beat-fault-top"));

            yield return PlayAndSettle(player, new[] { HoldBeat(actor, victim, "strike"), HoldBeat(actor, victim, "strike") });

            CollectionAssert.AreEqual(new[] { 0, 1 }, started,
                "the beat after the one that threw never started -- playback died with it");
        }

        [UnityTest]
        public IEnumerator AThrowFromTheOpeningStanceSkipsThatBeatAndStillFinishesOnce()
        {
            var player = NewPlayer();
            var (actor, victim) = Fixture(player, throwingSlots: false, out var log);

            LogAssert.Expect(LogType.Exception, new Regex("beat-fault-stance"));

            yield return PlayAndSettle(player, new[] { HoldBeat(actor, victim, "boom"), HoldBeat(actor, victim, "strike") });

            // The broken beat is abandoned but its outcome still lands: the
            // actor is put back to idle (AbandonBeat) rather than frozen in a
            // pose the next beat never clears.
            Assert.AreEqual("actor:idle", log[0],
                "the abandoned beat did not return its figures to idle: " + string.Join(", ", log));
            CollectionAssert.Contains(log, "actor:strike",
                "the beat after the one that threw never posed -- playback died with it");
        }

        [UnityTest]
        public IEnumerator AThrowInsideANestedStepSkipsThatBeatAndStillFinishesOnce()
        {
            var player = NewPlayer();

            // SlotFor throws, and on a Close beat the first thing to ask for a
            // slot is TravelFor inside CloseIn -- which PlayBeat yields as a
            // nested enumerator. Unguarded, its throw stops the child and
            // leaves the parent waiting on it forever.
            var (actor, victim) = Fixture(player, throwingSlots: true, out _);

            LogAssert.Expect(LogType.Exception, new Regex("beat-fault-slot"));
            LogAssert.Expect(LogType.Exception, new Regex("beat-fault-slot"));

            var started = new List<int>();
            player.BeatStarted = (index, _) => started.Add(index);

            yield return PlayAndSettle(player, new[] { CloseBeat(actor, victim), CloseBeat(actor, victim) });

            CollectionAssert.AreEqual(new[] { 0, 1 }, started,
                "the beat after the nested step threw never started -- playback died with it");
        }

        // ---- fixture ---------------------------------------------------------

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void CleanUp()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        private FightBeatPlayer NewPlayer()
        {
            var go = new GameObject("BeatPlayerUnderTest");
            _spawned.Add(go);

            var player = go.AddComponent<FightBeatPlayer>();

            // Fast, and pinned against whatever another fixture left in the
            // process-wide statics -- see FightBeatPhaseStanceTests.
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            return player;
        }

        private (CombatantState actor, CombatantState victim) Fixture(
            FightBeatPlayer player, bool throwingSlots, out List<string> log)
        {
            var stage = new GameObject("Stage", typeof(RectTransform)).GetComponent<RectTransform>();
            _spawned.Add(stage.gameObject);

            var actor = new CombatantState("Actor", true, 300, 30, 40, 10);
            var victim = new CombatantState("Victim", false, 100, 10, 8, 4);

            var recorded = new List<string>();
            log = recorded;

            player.WireStageForTest(
                combatant => throwingSlots
                    ? throw new InvalidOperationException("beat-fault-slot")
                    : (RectTransform)null,
                combatant => null);

            player.WireStancesForTest(
                (combatant, stance) =>
                {
                    if (stance == "boom") throw new InvalidOperationException("beat-fault-stance");
                    recorded.Add($"{(ReferenceEquals(combatant, actor) ? "actor" : "victim")}:{stance}");
                },
                (combatant, folder) => { },
                () => { });

            return (actor, victim);
        }

        private static CombatBeat HoldBeat(CombatantState actor, CombatantState victim, string strike)
        {
            var beat = new CombatBeat { Actor = actor, Target = victim, Amount = 5, Approach = StageApproach.Hold };
            beat.Stances[actor] = strike;
            return beat;
        }

        private static CombatBeat CloseBeat(CombatantState actor, CombatantState victim) =>
            new CombatBeat { Actor = actor, Target = victim, Amount = 5, Approach = StageApproach.Close };

        // Plays the beats out, then keeps watching for a while after the
        // finish so a second report -- the double-fire the finish path must
        // never make -- would be caught.
        private static IEnumerator PlayAndSettle(FightBeatPlayer player, CombatBeat[] beats)
        {
            int finished = 0;
            player.Play(beats, () => finished++);

            float deadline = Time.realtimeSinceStartup + 10f;
            while (finished == 0 && Time.realtimeSinceStartup < deadline) yield return null;

            for (int i = 0; i < 10; i++) yield return null;

            Assert.AreEqual(1, finished, "playback must report its finish exactly once");
            Assert.IsFalse(player.IsPlaying,
                "IsPlaying stayed true after playback ended -- the controller's watchdog trusts it");
        }
    }
}
