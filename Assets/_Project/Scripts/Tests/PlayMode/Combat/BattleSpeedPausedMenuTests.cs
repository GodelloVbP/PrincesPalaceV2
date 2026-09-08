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
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // T6, docs/PLAN_BATTLE_SPEED.md, contract 4: the row stepped while the
    // system menu holds the clock, mid-beat, through the REAL menu and the
    // real OptionsController.Step -- not a synthetic source swap.
    //
    // NOT SYNTHETIC (unlike BattleSpeedSpellClockTests): contract 4 is about
    // three real production pieces agreeing -- FightBeatPlayer only adopting
    // once per beat, SystemMenuController freezing the clock, and
    // OptionsController.Step writing through GameSettings -- so this drives
    // all three for real, in the Fight scene the game actually opens the
    // menu in (ScreenRegistry.cs:126 wires a real SystemMenuController there,
    // lockedForFight: true).
    //
    // THE STRUCTURAL ASSERTION, not a re-derived wall-clock prediction:
    // proving "the interrupted beat's impact lands when its ORIGINAL pace
    // predicts" by computing FightBeatPlayer.Scaled(...) and checking a
    // measured time against it would recompute the very formula under test
    // (CODE_STANDARDS.md Sec8's tautology rule -- a test that used the same
    // arithmetic on both sides would still pass if that arithmetic were
    // wrong). What actually GUARANTEES the impact lands on schedule is that
    // FightBeatPlayer.PlayerSpeedMultiplier -- the one number Scaled/Unscaled
    // read for the rest of the beat's remaining waits -- does not change
    // between the beat's own top and its own end. That is what this checks,
    // directly, at the moment the step happens.
    public class BattleSpeedPausedMenuTests
    {
        private FightController _fight;
        private FightBeatPlayer _beats;
        private readonly List<(int beat, float pace, float time)> _beatLog = new List<(int, float, float)>();
        private float _savedBattleSpeed;

        [SetUp]
        public void SaveBattleSpeed() => _savedBattleSpeed = GameSettings.BattleSpeed;

        [TearDown]
        public void Restore()
        {
            GameSettings.SetBattleSpeed(_savedBattleSpeed);
            Time.timeScale = 1f;
            TestGlobals.ResetAll();
        }

        private GameObject Named(string name)
        {
            foreach (var t in _fight.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (t.name == name) return t.gameObject;
            }

            return null;
        }

        private void Click(string name) => Named(name).GetComponent<Button>().onClick.Invoke();

        private IEnumerator LoadRealFightAtDisplay(float startDisplay)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            // T6's own instruction: the production source, installed
            // explicitly rather than trusted to whatever FightBootstrap did
            // a moment earlier for its own placeholder fight (which this
            // fixture immediately overwrites with its own session anyway).
            FightBeatPlayer.PlayerSpeedSource = () => BattleSpeed.Nearest(GameSettings.BattleSpeed).Multiplier;

            GameSettings.SetBattleSpeed(startDisplay);

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
            _beats = _fight.GetComponentInChildren<FightBeatPlayer>(includeInactive: true);
            _beats.BeatStarted = (i, pace) => _beatLog.Add((i, pace, Time.time));

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                new List<EnemyKit> { enemyKit }, new SeededRandom(7));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheBeatFinishesOnThePaceItStarted()
        {
            // Display 1 ("1x", multiplier 1/1.5) is where beat 0 opens;
            // display 2 ("2x", multiplier 2/1.5) is where the row lands
            // mid-pause -- two rows apart and far enough that a mixed-up
            // adoption cannot be mistaken for measurement noise.
            yield return LoadRealFightAtDisplay(1f);

            Click("Verb0");
            Click("EnemyPlate0");

            // Beat 0's own top.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_beatLog.Count < 1 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.GreaterOrEqual(_beatLog.Count, 1, "fixture: beat 0 never started");

            float paceAtBeat0Top = _beatLog[0].pace;
            Assert.AreEqual(1f / 1.5f, paceAtBeat0Top, 1e-4f,
                "fixture: beat 0 did not adopt the display-1 row it opened at");

            // Give beat 0 real time to be IN FLIGHT -- past its own
            // top, short of its own end -- rather than pausing on the frame
            // it started. At this pace (0.667x) the whole beat runs well
            // over a second of real time, so 0.15s is comfortably inside it.
            float settleUntil = Time.realtimeSinceStartup + 0.15f;
            while (Time.realtimeSinceStartup < settleUntil) yield return null;

            Assert.AreEqual(1, _beatLog.Count,
                "fixture: beat 1 already started before the pause -- too slow to catch beat 0 mid-flight");
            Assert.IsTrue(_beats.IsPlaying, "fixture: beat 0 already finished before the pause");

            var menu = Object.FindAnyObjectByType<SystemMenuController>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the Fight scene has no SystemMenuController");
            menu.Open();
            menu.Select(SystemMenuTab.Options);
            yield return null;
            yield return null;

            float pausedAt = Time.time;

            var options = Object.FindAnyObjectByType<OptionsController>(FindObjectsInactive.Include);
            Assert.IsNotNull(options, "the menu has no OptionsController");

            // Display 1 -> display 2 is two steps on the shipped table
            // (0.5, 1, 1.5, 2).
            options.Step("battlespeed", +1);
            options.Step("battlespeed", +1);
            Assert.AreEqual(2f, GameSettings.BattleSpeed, 0f, "fixture: stepping twice did not reach display 2");

            // THE CLAIM: the setting moved, but the beat already in flight
            // did not feel it -- nothing re-adopts until this beat's own
            // top, which has already happened.
            Assert.AreEqual(paceAtBeat0Top, FightBeatPlayer.PlayerSpeedMultiplier, 0f,
                "stepping the row while paused retroactively changed the beat already in flight");

            // And the pause holds game time -- the mechanism
            // contract 4 leans on (SystemMenuController.cs:225-237).
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(pausedAt, Time.time, 0f, "game time moved while the menu was open");

            menu.Close();

            // Beat 0 finishes and beat 1 (the enemy's reply) opens.
            deadline = Time.realtimeSinceStartup + 10f;
            while (_beatLog.Count < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.GreaterOrEqual(_beatLog.Count, 2, "fixture: beat 1 (the enemy's reply) never started");

            Assert.AreEqual(paceAtBeat0Top, _beatLog[0].pace, 0f,
                "beat 0's own recorded pace changed after the fact");
            Assert.AreEqual(2f / 1.5f, _beatLog[1].pace, 1e-4f,
                "beat 1 did not adopt the new row (display 2) at its own top");
        }
    }
}
