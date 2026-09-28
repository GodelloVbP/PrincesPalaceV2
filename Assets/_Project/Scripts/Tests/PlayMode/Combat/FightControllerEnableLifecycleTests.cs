using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // FAMILY B (docs/hunt/SCENARIOS.md rows B1 and B28): the fight screen
    // switched off and switched back on.
    //
    // B1, THE FINDING. Unity stops every coroutine on a component the moment
    // its GameObject goes inactive, and does not resume them on the way back
    // -- but the Coroutine OBJECT that started one stays non-null through all
    // of it. FightController.StageVisuals held exactly one such handle for the
    // idle breath and never nulled it, so StartIdleBreathing's "already
    // breathing" guard answered yes about a coroutine that had been dead since
    // the disable. Nothing else pushes a breath, and every stance in this game
    // is a single drawing, so the stage did not slow down or stutter -- it
    // stood perfectly still for the rest of the fight, which reads as a frozen
    // game rather than as a missing effect. The second half of the defect is
    // that Bind was the breath's ONLY caller, so even a nulled handle would
    // not have brought it back.
    //
    // WRITTEN TO THE EXPECTED STATE, not to the code (the scenario row says
    // so in as many words): the figures breathe again.
    //
    // SetActive RATHER THAN enabled = false, and the difference is the whole
    // mechanism: Unity leaves a disabled COMPONENT's coroutines running and
    // stops an inactive OBJECT's. Only the second is the case the handle
    // outlives.
    public class FightControllerEnableLifecycleTests
    {
        private FightController _fight;

        [SetUp]
        public void PlayFast()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 60f;

            // The breath runs on its own clock, unscaled by the beat
            // multiplier by design -- this collapses its ~2.8s real-time
            // cycle, the same line StageAnimationTests' own [SetUp] carries
            // and for the same reason.
            FightController.BreathSpeedMultiplier = 60f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightController.BreathSpeedMultiplier = 1f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
        }

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private CombatantState _foe;

        private IEnumerator ABoundFight()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            _foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { _foe });
            var kit = PlayModeSparkFixture.Kit();
            var enemyKit = new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0, spritePath: "Enemies/golem"), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(11));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        // MOVEMENT, NOT MAGNITUDE, and the difference is the whole test.
        //
        // StageAnimationTests.AnIdleFigureBreathesEvenThoughItsDrawingCannot
        // asks whether the figure is ever taller than its mark, which is the
        // right question for a stage that has just been stood up. It is the
        // WRONG question here: a breath coroutine that Unity stopped
        // mid-inhale leaves the figure parked at whatever swell it had
        // reached, so "taller than its mark" stays true forever about a stage
        // that is now frozen -- which is exactly what the first draft of this
        // test measured, and it passed against the bug. What a stopped breath
        // cannot do is CHANGE, so the span between the tallest and shortest
        // sample is what gets asserted.
        private IEnumerator BreathSpan(RectTransform slot, System.Action<float> report)
        {
            float tallest = slot.localScale.y;
            float shortest = slot.localScale.y;
            float deadline = Time.realtimeSinceStartup + 3f;

            while (Time.realtimeSinceStartup < deadline)
            {
                float height = slot.localScale.y;
                if (height > tallest) tallest = height;
                if (height < shortest) shortest = height;
                yield return null;
            }

            report(tallest - shortest);
        }

        // ---- B1 -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheStageBreathesAgainAfterTheFightScreenIsSwitchedBackOn()
        {
            yield return ABoundFight();

            var slot = (RectTransform)Named("Enemy0Slot").transform;
            var animator = slot.GetComponent<StageActorAnimator>();
            Assert.IsNotNull(animator, "the enemy slot has no animator, so nothing can breathe");

            float baseHeight = animator.BaseScale.y;

            // A quarter of a percent of the figure's height -- an order of
            // magnitude under the authored amplitude (BreathCurve) and an
            // order of magnitude over anything float noise can produce.
            float breathing = baseHeight * 0.0025f;

            float before = 0f;
            yield return BreathSpan(slot, span => before = span);
            Assert.Greater(before, breathing,
                "fixture: the stage was not breathing before the disable, so this proves nothing");

            _fight.gameObject.SetActive(false);
            yield return null;
            yield return null;

            _fight.gameObject.SetActive(true);
            yield return null;
            yield return null;

            float after = 0f;
            yield return BreathSpan(slot, span => after = span);

            Assert.Greater(after, breathing,
                $"the figure moved {after:F4} over three seconds after the screen came back, against " +
                $"{before:F4} before it: the breath's coroutine was stopped by the disable and its " +
                "handle was left non-null, so the restart took the 'already breathing' early return " +
                "and the stage is frozen at whatever swell it was parked on");
        }

        // ---- B28 ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheScreenComingBackDoesNotWireTheVerbsASecondTime()
        {
            // THE SHAPE THIS FAMILY IS ABOUT. The house pattern for a screen
            // is a Wire() guarded by a _wired bool called from OnEnable, with
            // no unsubscribe in OnDisable -- so the contract worth asserting
            // is "the listeners are added exactly once across N enables", and
            // the failure mode is one click resolving two actions. Asserted
            // through the SESSION rather than through the listener list,
            // which PlayMode cannot see (Core grants InternalsVisibleTo to the
            // Editor assembly and nothing else).
            yield return ABoundFight();

            for (int cycle = 0; cycle < 3; cycle++)
            {
                _fight.gameObject.SetActive(false);
                yield return null;
                _fight.gameObject.SetActive(true);
                yield return null;
            }

            int before = _foe.CurrentHealth;

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            float deadline = Time.realtimeSinceStartup + 10f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(_fight.IsBusy, "playback never finished");
            Assert.Less(_foe.CurrentHealth, before, "the click never reached the session at all");

            int attacks = _fight.RecentLogForTest.Count(line => line.Contains("Shawn attacks"));
            Assert.AreEqual(1, attacks,
                "one press on ATTACK resolved " + attacks + " swings -- the verb's listener was added " +
                "again on each enable: " + string.Join(" | ", _fight.RecentLogForTest));
        }
    }
}
