using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE RUN-UP, played back on the real Fight scene (docs/PLAN_REWARD_SCREENS.md
    // C0). RunUpScheduleTests pins every time on literals; this is only that the
    // controller plays the schedule: the final blow stops for its class without
    // touching Time.timeScale, the Reckoning's route waits for the last fade, the
    // sting lands on the kill, and a press jumps to the wipe and is swallowed.
    //
    // Pace is 1 throughout (BeatSpeedMultiplier 1, the fixture's player speed 1),
    // so the authored times are the wall times.
    public class FightRunUpTests
    {
        private FightController _fight;
        private FightSession _session;
        private FightBeatPlayer _player;
        private int _ended;
        private int _sceneLoads;
        private readonly List<(Sound sound, float time)> _sounds = new List<(Sound, float)>();
        private System.Action<bool> _onEnded;

        [SetUp]
        public void PaceOne()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            _sceneLoads = 0;
            _sounds.Clear();

            // FightBootstrap settles a won fight into whatever run is live; a
            // run another fixture left behind would be settled into here.
            RunManager.ResetForTests();
            Navigation.LoadOverride = _ => _sceneLoads++;
            SoundController.Observer = sound => _sounds.Add((sound, Time.time));
        }

        [TearDown]
        public void Restore()
        {
            if (_fight != null && _onEnded != null) _fight.FightEnded -= _onEnded;
            SoundController.Observer = null;
            Navigation.LoadOverride = null;
            FightSceneFixture.QuietForReuse(_fight);
            Navigation.Reset();

            // A controller raises a fight's end once and never clears it, so
            // the next test needs a scene whose controller has not.
            SharedScene.MarkDirty("a fight ended on this controller");
            SharedScene.AfterTest();
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Time.timeScale = 1f;
        }

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        // One blow from victory: a one-health foe against a hero who cannot miss
        // it, bound as a fight of `fightClass`, with the killing blow pressed
        // through the verbs a hand would press.
        private IEnumerator KillTheFoe(EncounterClass fightClass, FightRoundPresentation presentation = null)
        {
            yield return SharedScene.EnsureFight();

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
            _player = Object.FindAnyObjectByType<FightBeatPlayer>();

            var hero = new CombatantState("Shawn", true, 5000, 30, 10, 10);
            var foe = new CombatantState("Rat", false, 50000, 10, 5, 1);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("rat", "Rat", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false);

            _session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                new List<EnemyKit> { enemyKit }, new SeededRandom(9),
                isBossFight: fightClass == EncounterClass.Boss, isEliteFight: fightClass == EncounterClass.Elite);
            if (presentation != null) _session.RoundLimit = 10;
            _session.Begin();

            _ended = 0;
            _onEnded = _ => _ended++;
            _fight.FightEnded += _onEnded;
            _fight.Bind(_session, fightClass, null, presentation);
            yield return null;

            float openDeadline = Time.time + 10f;
            while (_fight.IsBusy && Time.time < openDeadline) yield return null;
            Assert.IsFalse(_fight.IsBusy, "fixture: Bind's opening playback never finished");

            foe.CurrentHealth = 1;
            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();
            yield return null;
        }

        // Runs until the blow's playback has finished, checking the engine clock
        // on every frame. Returns with the run-up (if any) just begun.
        private IEnumerator PlayTheBlow()
        {
            float deadline = Time.time + 20f;
            while (_player.IsPlaying && Time.time < deadline)
            {
                Assert.AreEqual(1f, Time.timeScale, "the hit-stop must not touch Time.timeScale");
                yield return null;
            }

            Assert.IsFalse(_player.IsPlaying, "fixture: the blow never finished playing");
        }

        // ---- the stop ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator ANormalKillStopsForPointTwoTwoAndTheClockIsUntouched()
        {
            yield return KillTheFoe(EncounterClass.Normal);
            yield return PlayTheBlow();

            Assert.IsTrue(_session.PlayerWon);
            Assert.AreEqual(0.22f, _player.FinalStopWall, 1e-4f);
        }

        [UnityTest]
        public IEnumerator AnEliteKillStopsForPointThree()
        {
            yield return KillTheFoe(EncounterClass.Elite);
            yield return PlayTheBlow();

            Assert.AreEqual(0.30f, _player.FinalStopWall, 1e-4f);
        }

        [UnityTest]
        public IEnumerator AnEliteEventFightStopsLikeANormalOne()
        {
            var event10 = new FightRoundPresentation(roundLimit: 10, label: "Toll", roundSfxPath: "Audio/Sfx/does_not_exist");
            yield return KillTheFoe(EncounterClass.Elite, event10);
            yield return PlayTheBlow();

            Assert.AreEqual(0.22f, _player.FinalStopWall, 1e-4f);
        }

        [UnityTest]
        public IEnumerator ABossKillStopsForPointFourSix()
        {
            yield return KillTheFoe(EncounterClass.Boss);
            yield return PlayTheBlow();

            Assert.AreEqual(0.46f, _player.FinalStopWall, 1e-4f);
        }

        // ---- the run-up ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheReckoningWaitsForTheFadeAndTheStingLandsOnTheKill()
        {
            yield return KillTheFoe(EncounterClass.Normal);
            yield return PlayTheBlow();

            float finished = Time.time;
            Assert.IsTrue(_fight.RunUpActive, "the blow's playback ended into the run-up");
            Assert.AreEqual(0, _ended, "the fight is not raised as ended until the run-up has run");
            Assert.IsTrue(_fight.IsBusy, "nothing can be pressed during the run-up");

            float deadline = Time.time + 5f;
            float washEarly = -1f;
            while (_ended == 0 && Time.time < deadline)
            {
                Assert.AreEqual(1f, Time.timeScale);
                if (washEarly < 0f && Time.time - finished >= 0.15f)
                {
                    washEarly = Named("VictoryWash").GetComponent<Image>().color.a;
                }

                yield return null;
            }

            Assert.Greater(washEarly, 0.02f, "the light is already turning 0.15 s after the stop, well before the fade's hold ends");

            float waited = Time.time - finished;
            Assert.AreEqual(1, _ended, "the run-up ended into the fight's end");
            Assert.IsFalse(_fight.RunUpActive);

            // The stop already elapsed inside the playback; what is left of the
            // 1.17 s is the fade's hold and fade (0.35 + 0.6), under the 1.3 cap.
            Assert.That(waited, Is.InRange(0.8f, 1.1f));

            var stings = _sounds.Where(s => s.sound == Sound.VictorySting).ToList();
            Assert.AreEqual(1, stings.Count, "the sting plays once");
            Assert.That(stings[0].time - finished, Is.InRange(0.25f, 0.5f),
                "the sting lands when the fade's hold ends, not at the end of the playback");

            Assert.AreEqual(0.15f, Named("VictoryWash").GetComponent<Image>().color.a, 0.01f);
            Assert.AreEqual(0.10f, Named("EnemyDim").GetComponent<Image>().color.a, 0.01f);
        }

        [UnityTest]
        public IEnumerator ABossEndsWithABiggerBrighterWashAndTheEnemySagging()
        {
            yield return KillTheFoe(EncounterClass.Boss);
            yield return PlayTheBlow();
            for (float t = 0f; t < 0.3f; t += Time.deltaTime) yield return null;

            Assert.IsTrue(_fight.GetComponentsInChildren<StageActorAnimator>(true)
                    .Any(a => a.transform.localEulerAngles.z > 0.5f && a.transform.localEulerAngles.z < 359.5f),
                "the enemy tips as it goes down");

            float deadline = Time.time + 5f;
            while (_ended == 0 && Time.time < deadline) yield return null;

            var wash = Named("VictoryWash");
            Assert.AreEqual(0.32f, wash.GetComponent<Image>().color.a, 0.01f);
            Assert.AreEqual(1.6f, wash.transform.localScale.x, 0.01f);
        }

        // The sag is the enemy's pose until it has gone: a recoil or a lunge
        // finishing its arc must not stand it back up before the fade.
        [UnityTest]
        public IEnumerator TheSagHoldsUntilTheWipe()
        {
            yield return KillTheFoe(EncounterClass.Boss);
            yield return PlayTheBlow();
            for (float t = 0f; t < 0.3f; t += Time.deltaTime) yield return null;

            var foe = _fight.GetComponentsInChildren<StageActorAnimator>(true)
                .FirstOrDefault(a => a.transform.localEulerAngles.z > 0.5f && a.transform.localEulerAngles.z < 359.5f);
            Assert.IsNotNull(foe, "the enemy is tipped by now");
            float lowest = foe.transform.localScale.y;

            float deadline = Time.time + 5f;
            while (_ended == 0 && Time.time < deadline)
            {
                float z = foe.transform.localEulerAngles.z;
                Assert.IsTrue(z > 0.5f && z < 359.5f, "the enemy has stood back up");
                Assert.LessOrEqual(foe.transform.localScale.y, lowest + 0.01f, "the enemy has risen back to full height");
                yield return null;
            }

            Assert.AreEqual(1, _ended);
        }

        [UnityTest]
        public IEnumerator APressDuringTheRunUpIsSwallowedAndOpensTheReckoningAtOnce()
        {
            yield return KillTheFoe(EncounterClass.Boss);
            yield return PlayTheBlow();
            yield return null;

            Assert.IsTrue(_fight.RunUpActive);
            float pressed = Time.time;
            ((IFightNavigationTarget)_fight).ConfirmFocus();

            Assert.AreEqual(0, _ended, "the press itself does not end the fight this frame");

            float deadline = Time.time + 2f;
            while (_ended == 0 && Time.time < deadline) yield return null;

            Assert.AreEqual(1, _ended);
            Assert.Less(Time.time - pressed, 0.25f, "a press jumps to the wipe");
            Assert.IsFalse(_fight.RunUpActive);
            Assert.AreEqual(0, _sceneLoads, "the press did not reach Continue or leave the fight");
            Assert.IsFalse(_fight.GetComponentsInChildren<StageDeathFade>(true).Any(f => f.Fading),
                "the fades completed");
            Assert.AreEqual(1, _sounds.Count(s => s.sound == Sound.VictorySting),
                "the sting is never skipped, and plays once");
        }

        [UnityTest]
        public IEnumerator ACancelDuringTheRunUpSkipsItToo()
        {
            yield return KillTheFoe(EncounterClass.Normal);
            yield return PlayTheBlow();
            yield return null;

            ((IFightNavigationTarget)_fight).OnBackPressed();

            float deadline = Time.time + 2f;
            while (_ended == 0 && Time.time < deadline) yield return null;

            Assert.AreEqual(1, _ended);
            Assert.AreEqual(0, _sceneLoads);
        }
    }
}
