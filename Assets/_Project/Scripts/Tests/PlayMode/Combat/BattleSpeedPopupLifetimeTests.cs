using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // T7, docs/PLAN_BATTLE_SPEED.md: a popup's whole life is
    // FightBeatPlayer.Scaled(0.85s) of GAME time from its own activation,
    // for a hit and for a miss, at player 1/3 and 4/3 -- plus a pause inside
    // that window not counting against it, and Flush reclaiming one early.
    //
    // A SYNTHETIC BEAT PLAYED DIRECTLY, not a click-driven combat resolution.
    // DamagePopupColorTests' own click flow proves a HIT reaches the popup;
    // reaching a MISS the same way needs a guaranteed dodge, and nothing in
    // this project's PlayMode suite has a proven recipe for one (no seeded
    // RNG or stat setup anywhere forces it). CombatBeat.Missed is a plain
    // field FightSession never has to set for this fixture to be honest
    // about what it is testing -- FightBeatPlayer.Play(beats, onFinished) is
    // the same public entry a real fight already calls, and every delegate
    // it drives (PaintFormation/PaintVitals/PaintTurnOrder/SlotFor/
    // AnimatorFor) is wired by the same real Bind() every other fixture
    // here uses. What is bypassed is FightSession's OWN resolution of who
    // was hit -- exactly the one thing this test needs to control.
    //
    // REAL TIME, NOT A HELD CLOCK. DamagePopup.Rise measures its own life in
    // Time.deltaTime, not SpellPerformancePlayer's clock -- so this fixture
    // lets frames actually pass rather than overriding a clock nothing here
    // reads.
    public class BattleSpeedPopupLifetimeTests
    {
        private FightController _fight;
        private FightBeatPlayer _beats;

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            TestGlobals.ResetAll();
        }

        private IEnumerator LoadAndBind()
        {
            yield return FightSceneFixture.LoadFight();

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");
            _beats = _fight.GetComponentInChildren<FightBeatPlayer>(includeInactive: true);

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                new List<EnemyKit> { enemyKit }, new SeededRandom(11));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        private CombatantState Hero => _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
        private CombatantState Foe => _fight.SessionForTest.Encounter.Enemies.First(e => e != null);

        private IEnumerator WaitForActivePopup(System.Action<DamagePopup> onActive)
        {
            // Generous: at player 1/3 every stage of the beat leading up to
            // the impact -- windup, hold, settle -- is ALSO divided by the
            // same Pace, so the popup can be several real seconds behind
            // Play() returning, not just one.
            float deadline = Time.realtimeSinceStartup + 10f;

            while (Time.realtimeSinceStartup < deadline)
            {
                var popup = _beats.Popups.FirstOrDefault(p => p != null && !p.IsFree);
                if (popup != null)
                {
                    onActive(popup);
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("fixture: no popup went active for this beat within the deadline");
        }

        [UnityTest]
        public IEnumerator APopupIsReclaimedWhenItsBeatSays()
        {
            yield return LoadAndBind();

            var cases = new (float playerSpeed, bool missed)[]
            {
                (1f / 3f, false),
                (1f / 3f, true),
                (4f / 3f, false),
                (4f / 3f, true),
            };

            bool checkedThePause = false;

            foreach (var (playerSpeed, missed) in cases)
            {
                FightBeatPlayer.PlayerSpeedSource = () => playerSpeed;
                FightBeatPlayer.AdoptPlayerSpeedForTest();

                string because = $"player {playerSpeed}, missed {missed}";

                var beat = new CombatBeat
                {
                    Actor = Hero,
                    Target = Foe,
                    Amount = missed ? 0 : 17,
                    Missed = missed,
                    DamageType = DamageType.Physical,
                };

                bool finished = false;
                _beats.Play(new List<CombatBeat> { beat }, () => finished = true);

                DamagePopup popup = null;
                yield return WaitForActivePopup(p => popup = p);

                float activatedAt = Time.time;

                // T7's own extra clause, checked once rather than across
                // every matrix cell: a pause inside the window must not
                // count against it. Time.deltaTime is already zero while
                // timeScale is zero, which is the whole mechanism -- this
                // confirms Time.time itself (what activation and reclaim
                // are both measured against) genuinely does not move.
                if (!checkedThePause)
                {
                    checkedThePause = true;
                    Time.timeScale = 0f;
                    float pausedAt = Time.time;
                    yield return null;
                    yield return null;
                    yield return null;
                    Assert.AreEqual(pausedAt, Time.time, 0f,
                        "game time moved while the fight was paused -- a pause would count against the popup's window");
                    Time.timeScale = 1f;
                }

                // Scaled(0.85) as a LITERAL, per CODE_STANDARDS Sec8 -- not
                // read off DamagePopup.LifeSeconds (internal, and PlayMode
                // has no InternalsVisibleTo grant regardless), and not
                // recomputed from anything else this test could also get
                // wrong the same way.
                float expectedLife = FightBeatPlayer.Scaled(0.85f);
                // Generous rather than exactly "one frame": batchmode frame
                // pacing is uneven (SpellPerformancePlayer's own header cites
                // a single frame measured at 24ms and another at 57ms), and
                // this test polls once per frame on TOP of whatever frame the
                // engine itself is landing on.
                float tolerance = Mathf.Max(Time.smoothDeltaTime * 6f, 0.1f);

                float deadline = Time.realtimeSinceStartup + expectedLife * 4f + 3f;
                while (!popup.IsFree && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.IsTrue(popup.IsFree, because + ": the popup was never reclaimed");

                float actualLife = Time.time - activatedAt;
                Assert.That(actualLife, Is.InRange(expectedLife - tolerance, expectedLife + tolerance),
                    because + $": expected life {expectedLife}, measured {actualLife}");

                Assert.IsTrue(finished, because + ": the single-beat playback never reached its onFinished");
            }
        }

        // Flush's OWN reclaim, separate from natural expiry -- a playback
        // superseded (or a fight abandoned) must not leave a popup running
        // past the moment that happened.
        [UnityTest]
        public IEnumerator FlushReclaimsALivePopupEarly()
        {
            yield return LoadAndBind();

            FightBeatPlayer.PlayerSpeedSource = () => 1f / 3f;
            FightBeatPlayer.AdoptPlayerSpeedForTest();

            var beat = new CombatBeat { Actor = Hero, Target = Foe, Amount = 5, DamageType = DamageType.Physical };
            _beats.Play(new List<CombatBeat> { beat }, null);

            DamagePopup popup = null;
            yield return WaitForActivePopup(p => popup = p);

            Assert.IsFalse(popup.IsFree, "fixture: the popup was already free before Flush ran");

            _beats.Flush();

            Assert.IsTrue(popup.IsFree, "Flush did not reclaim a live popup");
        }
    }
}
