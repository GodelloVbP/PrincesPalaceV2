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
    // HUNT 2026-09-11, scenarios A1 and A25 (docs/hunt/SCENARIOS.md): a second
    // round handed to playback while the first is still playing, and a second
    // verb pressed while the first is still resolving.
    //
    // THESE ARE THE SAME EVENT SEEN FROM TWO SIDES. A25 is the defence -- the
    // controller refuses a click while it is busy -- and A1 is what happens
    // when something gets past it anyway, which the beat player has to survive
    // because Play is also the ordinary start of the NEXT round. Play's own
    // header states the contract in full ("this playback supersedes the last
    // one, and the caller is about to be told when THIS one ends"), and
    // nothing asserted it: FightFlowTests.FlushReclaimsEveryInFlightPopup
    // covers the reclaim, not the double Play.
    public class FightBeatPlayerLifecycleTests
    {
        private FightController _fight;
        private FightBeatPlayer _beats;
        private CombatantState _hero;
        private CombatantState _foe;

        [SetUp]
        public void PlayFast()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 20f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            FightController.BreathSpeedMultiplier = 0f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            FightController.BreathSpeedMultiplier = 1f;
        }

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        private IEnumerator ABoundFight()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            _beats = _fight.GetComponentInChildren<FightBeatPlayer>(includeInactive: true);
            Assert.IsNotNull(_beats, "the Fight scene has no FightBeatPlayer");

            _hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            _foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { _hero }, new[] { _foe });
            var enemyKit = new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0, spritePath: "Enemies/golem"), false);

            var session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                new List<EnemyKit> { enemyKit }, new SeededRandom(11));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        private List<DamagePopup> Popups() =>
            _fight.GetComponentsInChildren<DamagePopup>(includeInactive: true).ToList();

        // ---- A1 ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ASecondRoundHandedToPlaybackSupersedesTheFirstAndOnlyOneEndingIsReported()
        {
            yield return ABoundFight();

            var session = _fight.SessionForTest;
            session.ExecuteAttack(_foe);
            var round = session.DrainBeats();
            Assert.IsNotEmpty(round, "fixture: the attack recorded no beats to play");

            var slot = (RectTransform)Named("Party0Slot").transform;
            var animator = slot.GetComponent<StageActorAnimator>();
            var home = animator.Home;

            int firstEnded = 0;
            int secondEnded = 0;

            _beats.Play(round, () => firstEnded++);
            yield return null;

            Assert.IsTrue(_beats.IsPlaying, "fixture: the first playback is already over");

            _beats.Play(round, () => secondEnded++);

            // THE SUPERSEDED CALLBACK IS NOT FIRED, which is Play's own
            // stated contract and the reason it nulls _onFinished BEFORE
            // superseding: the controller sets its busy flag around Play, so
            // an old callback firing here would clear it one frame after it
            // was set and hand the player a turn in the middle of the round
            // they had just started.
            Assert.AreEqual(0, firstEnded,
                "the superseded playback reported itself finished -- the controller's busy flag is " +
                "cleared a frame after it was set");

            float deadline = Time.realtimeSinceStartup + 15f;
            while (_beats.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(_beats.IsPlaying, "playback never finished");
            Assert.AreEqual(0, firstEnded, "the superseded playback reported itself finished, late");
            Assert.AreEqual(1, secondEnded, "the surviving playback reported its ending " + secondEnded + " times");

            // ONE HANDLE, NOT TWO, read off what a second live PlayBeats
            // would leave behind rather than off a field no test can see: the
            // figure back on its mark, and every popup back in the pool.
            //
            // WAITED FOR, NOT COUNTED IN FRAMES, since 2026-09-19: a struck
            // figure's reel now runs 1.505s (FightBeatPlayer's
            // RecoilDwellSeconds plus RecoilReturnSeconds) and deliberately
            // outlives the beat that caused it, so two frames after playback
            // ends is mid-reel rather than at rest. What this still proves is
            // the same thing it always did -- ONE animator is driving the
            // slot, and it finishes at home -- because a second, superseded
            // handle would either fight this one or park the figure somewhere
            // else when this one is done. In fact the wait strengthens it: a
            // superseded handle has a whole extra second to betray itself.
            deadline = Time.realtimeSinceStartup + 5f;
            while (animator.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            yield return null;

            Assert.That(Vector2.Distance(slot.anchoredPosition, home), Is.LessThan(0.01f),
                $"the attacker settled at {slot.anchoredPosition} rather than on its mark {home} -- " +
                "a superseded playback was still driving it");

            // A NUMBER FROM THE LAST BLOW OUTLIVES ITS OWN ROUND, on purpose
            // -- playback does not wait for a popup to finish rising, so
            // "claimed the frame playback ended" is the healthy state and not
            // the leak. What is a leak is one still claimed after its whole
            // life has passed, which is what a Rise stopped without a release
            // leaves behind: the pool is six deep and never refilled, so a
            // few abandoned rounds and the next fight shows no numbers at all.
            deadline = Time.realtimeSinceStartup + 5f;
            while (Popups().Any(p => !p.IsFree) && Time.realtimeSinceStartup < deadline) yield return null;

            var claimed = Popups().Where(p => !p.IsFree).Select(p => p.name).ToList();
            CollectionAssert.IsEmpty(claimed,
                "popups were left claimed long after the superseded round ended: " +
                string.Join(", ", claimed));
        }

        // ---- A25 -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator AVerbPressedWhileTheLastActionIsStillResolvingReachesNothing()
        {
            yield return ABoundFight();

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            Assert.IsTrue(_fight.IsBusy, "fixture: the swing resolved instantly, so nothing is interrupted");

            int after = _foe.CurrentHealth;

            // THE SECOND PRESS, mid-animation. The verb column is hidden
            // rather than merely dimmed while a round plays, so this is a
            // gamepad press or a stray click reaching a button that is on
            // screen for nobody -- onClick.Invoke bypasses interactable,
            // which is exactly the case the controller's own CanAct guard is
            // the last defence for.
            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(after, _foe.CurrentHealth,
                "a second action reached the session while the first was still playing");

            float deadline = Time.realtimeSinceStartup + 15f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(_fight.IsBusy, "playback never finished");

            int swings = _fight.RecentLogForTest.Count(line => line.Contains("Shawn attacks"));
            Assert.AreEqual(1, swings,
                "one press resolved " + swings + " swings: " + string.Join(" | ", _fight.RecentLogForTest));

            Assert.IsFalse(Named("TargetPrompt").activeSelf,
                "the target prompt survived the resolution it was answering");
        }
    }
}
