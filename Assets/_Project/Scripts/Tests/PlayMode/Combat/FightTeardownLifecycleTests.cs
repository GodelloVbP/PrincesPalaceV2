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
    // HUNT 2026-09-11, FAMILY C (docs/hunt/SCENARIOS.md rows C1, C2, C3, C6)
    // plus A7 and A22: a scene load arriving while the stage is mid-flight,
    // and the two pooled things that survive it.
    //
    // C6 IS AUDIT #106, and the scenario row says to reproduce it rather than
    // work around it. The call chain is one long straight line with no branch
    // in it: LoadSceneAsync(Single) deactivates the outgoing scene ->
    // FightBeatPlayer.OnDisable -> EndFight -> Flush -> the finished callback
    // -> FightController.OnPlaybackFinished -> RefreshUi -> the status row ->
    // PaintBadge -> BeginAppearancePop -> StartCoroutine on a GameObject the
    // engine has already deactivated, which is a hard engine error rather
    // than a no-op. Cosmetic in the game and fatal in a test host: Unity
    // fails any test that logs an unexpected error, which is why four Fight
    // fixtures in this folder carry LogAssert.ignoreFailingMessages across
    // their scene swap and say so at the line.
    //
    // THE OTHER THREE ROWS RIDE ON THE SAME DRIVE. C1 (a popup mid-rise), C2
    // (a death fade mid-fade) and C3 (an animator mid-tween) all ask the same
    // question of the same moment -- does anything write a destroyed
    // transform, and is the next fight whole -- so they are one scene load
    // with all three in flight rather than three loads.
    public class FightTeardownLifecycleTests
    {
        private FightController _fight;
        private CombatantState _hero;
        private CombatantState _foe;

        [SetUp]
        public void PinTheClocks()
        {
            // 1x: every row here has to catch something MID-flight, and the
            // popup's whole life is 0.85s.
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
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
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            _hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            _foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { _hero }, new[] { _foe });
            var kit = PlayModeSparkFixture.Kit();
            var enemyKit = new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0, spritePath: "Enemies/golem"), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(11));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        private List<DamagePopup> Popups() =>
            _fight.GetComponentsInChildren<DamagePopup>(includeInactive: true).ToList();

        // ---- C1 + C2 + C3 + C6 ---------------------------------------------------

        [UnityTest]
        public IEnumerator LoadingAFightOverALiveOneWithEverythingInFlightLogsNothing()
        {
            // NO LogAssert.ignoreFailingMessages ANYWHERE IN THIS TEST, which
            // is the whole assertion: the framework fails on an unexpected
            // error, so the test IS the claim that the teardown is silent.
            yield return ABoundFight();

            // A REAL ROUND, PLAYING, and that is not decoration -- it is the
            // first half of #106's call chain. FightBeatPlayer.Flush fires
            // the finished callback it was handed by Play, so a fight that
            // never played a beat has nothing registered and tears down in
            // silence whether the bug is there or not. (The first draft of
            // this test did exactly that and passed against the bug.)
            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;

            Assert.IsTrue(_fight.IsBusy, "fixture: nothing is playing, so nothing is being abandoned");

            // THE BADGE THAT FIRES #106: a status applied with no repaint
            // after it. RefreshUi does not run while a round plays, so the
            // repaint the teardown itself triggers is the first one that has
            // ever seen this code -- and therefore the one that wants to pop
            // it in, on a panel the engine has already deactivated.
            StatusEffects.Apply(_hero.Statuses, StatusEffectType.Poison, 3, 3);

            // AND THE TWO OTHER TWEENS THESE ROWS ARE ABOUT, started by hand
            // so the moment of the load is chosen rather than raced: a popup
            // mid-rise (C1) and a death fade mid-fade (C2). The figure
            // mid-tween (C3) is the round's own lunge, already running.
            var popup = Popups().FirstOrDefault(p => p.IsFree) ?? Popups().FirstOrDefault();
            Assert.IsNotNull(popup, "the fight scene has no damage popups to abandon");
            popup.Play(new Vector2(0f, 0f), 12, false, DamageType.Physical, DamagePopupLifeForTest);

            Named("Enemy0Slot").GetComponent<StageDeathFade>().PlayIfNotAlready();

            yield return null;

            Assert.IsFalse(popup.IsFree, "fixture: the popup was already done, so nothing was abandoned");

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            LogAssert.NoUnexpectedReceived();

            // AND THE NEXT FIGHT IS WHOLE. The pool is created with the
            // scene, so this is not the leak Reclaim exists for -- it is the
            // other half of C1: whatever the abandoned rise left behind, the
            // new screen's numbers still work.
            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the second Fight scene has no FightController");

            var fresh = Popups();
            Assert.IsNotEmpty(fresh, "the second fight has no damage popups at all");
            CollectionAssert.IsEmpty(fresh.Where(p => !p.IsFree).ToList(),
                "the next fight opened with popups already claimed");
        }

        // The popup's own authored life, restated rather than reached for:
        // DamagePopup.LifeSeconds is internal to Core and PlayMode has no
        // InternalsVisibleTo grant. Long enough that these tests can catch a
        // rise in the middle of itself.
        private const float DamagePopupLifeForTest = 0.85f;

        // ---- A7 ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AReissuedPopupStartsFromTheTopOfItsOwnCurveRatherThanMidFade()
        {
            yield return ABoundFight();

            var popup = Popups().FirstOrDefault();
            Assert.IsNotNull(popup, "the fight scene has no damage popups");

            var label = popup.GetComponentInChildren<TMPro.TMP_Text>(includeInactive: true);
            Assert.IsNotNull(label, "the popup has no label to read");

            popup.Play(new Vector2(0f, 0f), 12, false, DamageType.Physical, DamagePopupLifeForTest);
            yield return null;

            var rect = (RectTransform)popup.transform;
            float startY = rect.anchoredPosition.y;

            // Wait until the rise is visibly under way AND the fade has
            // started, which is the state the pool can hand this popup back
            // in: six deep, and a round can easily land seven numbers.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (label.color.a > 0.95f && !popup.IsFree && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsFalse(popup.IsFree, "fixture: the popup finished before it could be re-issued");
            Assert.Less(label.color.a, 0.96f, "fixture: the popup never started fading");
            Assert.Greater(rect.anchoredPosition.y, startY, "fixture: the popup never rose");

            popup.Play(new Vector2(0f, 0f), 34, true, DamageType.Physical, DamagePopupLifeForTest);
            yield return null;

            Assert.That(label.color.a, Is.GreaterThan(0.95f),
                "the re-issued number came back part-faded -- it picked the interrupted curve up " +
                "rather than starting its own");
            Assert.That(rect.anchoredPosition.y, Is.LessThan(startY + 20f),
                "the re-issued number started part-way up the rise it inherited");
            Assert.That(rect.localScale.x, Is.LessThan(1f),
                "the re-issued number appeared already grown instead of punching in");

            deadline = Time.realtimeSinceStartup + 5f;
            while (!popup.IsFree && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsTrue(popup.IsFree,
                "the re-issued popup never came back to the pool -- two Rise coroutines were running " +
                "and the loser freed it while the winner kept it");
        }

        // ---- A22 -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator TwoPopsOnOneBadgeLeaveItAtItsRestScale()
        {
            yield return ABoundFight();

            var badge = Named("PcStatusBadge0_0");
            Assert.IsNotNull(badge, "the first PC plate has no status badge");
            var rect = (RectTransform)badge.transform;

            StatusEffects.Apply(_hero.Statuses, StatusEffectType.Poison, 3, 3);
            _fight.RefreshUi();
            yield return null;

            Assert.IsTrue(badge.activeSelf, "fixture: the badge never appeared, so nothing popped");

            // THE POP STARTS OVERSIZED (1.3) and settles down to 1 -- it is a
            // punch in, not a grow in, so "mid-pop" reads as a scale ABOVE
            // rest rather than below it.
            Assert.Greater(rect.localScale.x, 1.001f, "fixture: the first pop never started");

            // THE SECOND POP ON THE SAME SLOT. A status that leaves and comes
            // back is a code this row has not seen since its last repaint, so
            // it pops again -- landing on a badge whose previous pop is still
            // running.
            _hero.Statuses.Clear();
            _fight.RefreshUi();
            StatusEffects.Apply(_hero.Statuses, StatusEffectType.Poison, 3, 3);
            _fight.RefreshUi();
            yield return null;

            float deadline = Time.realtimeSinceStartup + 5f;
            while (rect.localScale.x > 1.001f && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.AreEqual(1f, rect.localScale.x, 0.001f,
                "the badge never reached its rest scale");

            // AND NOTHING IS STILL WRITING IT. One stopped pop left running
            // would keep lerping this rect from its own start scale.
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
                Assert.AreEqual(1f, rect.localScale.x, 0.001f,
                    $"the badge grew back to {rect.localScale.x:F3} on frame {frame} -- " +
                    "an interrupted appearance pop is still running");
            }
        }
    }
}
