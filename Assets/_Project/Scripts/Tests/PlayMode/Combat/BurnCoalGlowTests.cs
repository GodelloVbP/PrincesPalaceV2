using System.Collections;
using System.Collections.Generic;
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
    // The coal glow on a burning body (StageBurnGlow), through the real Fight
    // scene: shown while Burn is in the status snapshot, gone when it is not,
    // gone with the body, and drawn in whatever pose the figure is wearing.
    public class BurnCoalGlowTests
    {
        private FightController _fight;
        private CombatantState _hero;
        private CombatantState _foe;

        [SetUp]
        public void PlayBeatsFast() => FightBeatPlayer.BeatSpeedMultiplier = 60f;

        [TearDown]
        public void RestoreBeatSpeed() => FightBeatPlayer.BeatSpeedMultiplier = 1f;

        private GameObject Named(string name)
        {
            foreach (var transform in _fight.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (transform.name == name) return transform.gameObject;
            }

            return null;
        }

        private StageBurnGlow Glow(string name) => Named(name).GetComponent<StageBurnGlow>();

        private IEnumerator LoadFight(int foeHealth, int burnTurns)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            _hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            _foe = new CombatantState("Front", false, foeHealth, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { _hero }, new[] { _foe });

            var kit = PlayModeSparkFixture.Kit(DamageType.Physical);
            var enemyKit = new EnemyKit(
                new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(9)) { DamageVarianceRange = 0f };
            session.Begin();

            if (burnTurns > 0) _foe.Statuses.Add(new ActiveStatus(StatusEffectType.Burn, 1, burnTurns, _hero));

            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
            yield return null;
        }

        private IEnumerator WaitUntil(System.Func<bool> condition, string timeoutMessage)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline && !condition()) yield return null;
            Assert.IsTrue(condition(), timeoutMessage);
        }

        [UnityTest]
        public IEnumerator ABurningEnemyShowsTheGlowAndAnotherDoesNot()
        {
            yield return LoadFight(foeHealth: 5000, burnTurns: 3);

            var glow = Glow("Enemy0BurnGlow");
            Assert.IsNotNull(glow, "the enemy slot has no BurnGlow component");
            Assert.IsTrue(glow.Burning, "Burn is on the foe and the glow is not lit");
            yield return WaitUntil(() => glow.Visible, "a burning body never drew its glow");
            Assert.Greater(glow.Level, 0f);
        }

        [UnityTest]
        public IEnumerator ABodyWithNoBurnShowsNoGlow()
        {
            yield return LoadFight(foeHealth: 5000, burnTurns: 0);

            var glow = Glow("Enemy0BurnGlow");
            Assert.IsFalse(glow.Burning);
            Assert.IsFalse(glow.Visible, "a body with no Burn drew a glow");
            Assert.IsFalse(Glow("Party0BurnGlow").Visible);
        }

        [UnityTest]
        public IEnumerator TheGlowIsGoneOnceBurnHasLeftTheStatuses()
        {
            yield return LoadFight(foeHealth: 5000, burnTurns: 3);
            var glow = Glow("Enemy0BurnGlow");
            yield return WaitUntil(() => glow.Visible, "fixture: the glow never came on");

            _foe.Statuses.RemoveAll(s => s.Type == StatusEffectType.Burn);
            _fight.RefreshUi();

            Assert.IsFalse(glow.Burning, "Burn is gone and the glow is still asked to burn");
            yield return WaitUntil(() => !glow.Visible, "the glow outlived the status");
        }

        [UnityTest]
        public IEnumerator TheGlowGoesOutOnTheBeatBurnRunsOut()
        {
            yield return LoadFight(foeHealth: 5000, burnTurns: 1);
            var glow = Glow("Enemy0BurnGlow");
            yield return WaitUntil(() => glow.Visible, "fixture: the glow never came on");

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            yield return WaitUntil(() => !_foe.Statuses.Exists(s => s.Type == StatusEffectType.Burn),
                "fixture: a one-turn Burn outlasted a whole round");
            yield return WaitUntil(() => !glow.Burning && !glow.Visible,
                "Burn ran out of the snapshot and the body kept smouldering");
        }

        [UnityTest]
        public IEnumerator ABurningBodyThatDiesCarriesNoGlow()
        {
            yield return LoadFight(foeHealth: 1, burnTurns: 5);
            var glow = Glow("Enemy0BurnGlow");
            yield return WaitUntil(() => glow.Visible, "fixture: the glow never came on");

            Named("Verb0").GetComponent<Button>().onClick.Invoke();
            Named("EnemyPlate0").GetComponent<Button>().onClick.Invoke();

            yield return WaitUntil(() => !_foe.IsAlive && !glow.Burning,
                "the foe died but its glow was still burning");
            Assert.IsFalse(glow.Visible, "a corpse kept its embers");
        }

        [UnityTest]
        public IEnumerator TheGlowWearsThePoseTheFigureIsWearing()
        {
            yield return LoadFight(foeHealth: 5000, burnTurns: 3);

            var glow = Glow("Enemy0BurnGlow");
            var figure = Named("Enemy0Sprite").GetComponent<Image>();
            yield return WaitUntil(() => glow.Visible, "fixture: the glow never came on");

            var other = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f));
            figure.sprite = other;
            figure.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            yield return null;

            var image = glow.GetComponent<Image>();
            Assert.AreSame(other, image.sprite, "the silhouette stayed in the previous pose");
            Assert.AreEqual(-1f, image.rectTransform.localScale.x, "the silhouette is not mirrored like the figure");
        }

        [Test]
        public void TheBreathStaysBetweenTheTroughAndThePeakAndScalesWithTheRise()
        {
            for (float t = 0f; t < 20f; t += 0.37f)
            {
                float full = StageBurnGlow.AlphaAt(1f, t);
                Assert.GreaterOrEqual(full, StageBurnGlow.PeakAlpha * StageBurnGlow.TroughShare - 1e-5f);
                Assert.LessOrEqual(full, StageBurnGlow.PeakAlpha + 1e-5f);
                Assert.AreEqual(full / 2f, StageBurnGlow.AlphaAt(0.5f, t), 1e-5f);
                Assert.AreEqual(0f, StageBurnGlow.AlphaAt(0f, t), 0f);
            }
        }
    }
}
