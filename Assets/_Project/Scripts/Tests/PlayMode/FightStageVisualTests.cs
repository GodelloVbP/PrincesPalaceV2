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
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // The actors on the stage: which sprite, which way round, how far off the
    // floor, and what moves when a blow lands.
    //
    // These need a scene because every one of them is about a RectTransform or a
    // loaded Sprite. The rules BEHIND them -- who faces which way, where the
    // ground line is -- are Domain and already have EditMode tests; what is left
    // here is whether the view actually applies them.
    public class FightStageVisualTests
    {
        private FightController _fight;
        private CombatantState _hero;
        private CombatantState _front;

        [SetUp]
        public void PlayBeatsFast()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            StanceManifestLoader.Reset();
        }

        private GameObject Named(string name) =>
            _fight.GetComponentsInChildren<Transform>(includeInactive: true)
                  .FirstOrDefault(t => t.name == name)?.gameObject;

        private void Click(string name)
        {
            var go = Named(name);
            Assert.IsNotNull(go, $"no object named '{name}'");
            go.GetComponent<Button>().onClick.Invoke();
        }

        private IEnumerator LoadFight(SpriteFacing enemyFacing = SpriteFacing.Left)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight);

            _hero = new CombatantState("Shawn", true, 300, 30, 40, 0, 10);
            _front = new CombatantState("Front", false, 5000, 10, 8, 0, 4);

            var encounter = new CombatEncounter(new[] { _hero }, new[] { _front });
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null,
                new ResolvedSpellTier(1, "Spark", 6, 1.5f, 0), level: 4);
            var enemyKit = new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0, facing: enemyFacing), false);

            var session = new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(4));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        // ---- the slots exist and are populated ----------------------------------

        [UnityTest]
        public IEnumerator OnlyTheOccupiedSlotsAreShown()
        {
            yield return LoadFight();

            Assert.IsTrue(Named("Enemy0Slot").activeSelf, "one monster, one slot");
            Assert.IsFalse(Named("Enemy1Slot").activeSelf);
            Assert.IsFalse(Named("Enemy2Slot").activeSelf);
            Assert.IsTrue(Named("Party0Slot").activeSelf);
        }

        [UnityTest]
        public IEnumerator ANameplateNamesWhoeverIsStandingThere()
        {
            yield return LoadFight();

            Assert.AreEqual("Front", Named("Enemy0Nameplate").GetComponent<TMPro.TMP_Text>().text);
            Assert.AreEqual("Shawn", Named("Party0Nameplate").GetComponent<TMPro.TMP_Text>().text);
        }

        // ---- graceful degradation ------------------------------------------------

        [UnityTest]
        public IEnumerator ACombatantWithNoArtGetsThePlateRatherThanAWhiteQuad()
        {
            // The shipped v1 bug class: an Image with no sprite renders as a
            // SOLID WHITE RECTANGLE, not as nothing. Neither of these combatants
            // has authored battle art in this fixture, so both take this path.
            yield return LoadFight();

            var enemy = Named("Enemy0Sprite").GetComponent<Image>();

            Assert.IsNotNull(enemy.sprite, "a sprite-less Image is a white quad");
            Assert.IsTrue(enemy.enabled);
            Assert.AreEqual(Vector3.one, enemy.rectTransform.localScale,
                "the fallback is a UI frame, not a character - mirroring it would just reverse its bevel");
        }

        [UnityTest]
        public IEnumerator TheFallbackClearsAnyGroundingItInherited()
        {
            // A slot that fell back AFTER showing real art would otherwise keep
            // the last pose's drop -- the plate has no feet, and the branch
            // returns early, so nothing else would undo it.
            yield return LoadFight();

            var enemy = Named("Enemy0Sprite").GetComponent<Image>();

            Assert.AreEqual(Vector2.zero, enemy.rectTransform.offsetMin);
            Assert.AreEqual(Vector2.zero, enemy.rectTransform.offsetMax);
        }

        // ---- what a blow does ----------------------------------------------------

        [UnityTest]
        public IEnumerator ALandedBlowFlashesTheTarget()
        {
            yield return LoadFight();

            var flash = Named("Enemy0HitFlash").GetComponent<StageHitFlash>();
            Assert.IsNotNull(flash);

            Click("Verb0");
            Click("EnemyPlate0");
            yield return null;

            Assert.IsTrue(Named("Enemy0HitFlash").activeSelf,
                "the overlay is built inactive and has to wake itself - StartCoroutine on an inactive " +
                "GameObject is a hard error, not a no-op");
        }

        [UnityTest]
        public IEnumerator TheStageEndsTheRoundIdleRatherThanHoldingTheLastPose()
        {
            // A pose belongs to the blow that caused it. Left standing, an actor
            // would hold its attack frame until something else happened to
            // overwrite it, which reads as the animation having stuck.
            yield return LoadFight();

            Click("Verb0");
            Click("EnemyPlate0");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (_fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(_fight.IsBusy, "playback never finished");
            Assert.AreEqual(FightSession.Stances.Idle, _fight.StanceFor(_hero));
            Assert.AreEqual(FightSession.Stances.Idle, _fight.StanceFor(_front));
        }

        [UnityTest]
        public IEnumerator ADefeatedCombatantStaysDefeated()
        {
            // Read from IsAlive, not from the beat -- the round-end reset above
            // must not stand a corpse back up.
            yield return LoadFight();
            _front.CurrentHealth = 0;

            _fight.RefreshUi();

            Assert.AreEqual(FightSession.Stances.Defeated, _fight.StanceFor(_front));
        }

        // ---- the ground line -----------------------------------------------------

        [UnityTest]
        public IEnumerator TheManifestsGroundLineIsWhatDropsTheFigure()
        {
            // The single most load-bearing rule on this stage, and the one that
            // was a comment and nothing else in v1. A figure whose art clears
            // 40px below its feet must be pushed DOWN by exactly that, or it
            // hangs in the air -- the "golem flies upwards in its attack" report.
            yield return LoadFight();

            const float drop = 40f;
            StanceManifestLoader.Override(ManifestWithGroundLine("Actors/test", drop));

            Assert.AreEqual(drop, StanceManifestLoader.Manifest.GroundLineFor("Actors/test"), 0.001f,
                "the override did not take, so nothing below would be testing the real path");

            // An actor with no authored art takes the fallback branch, which
            // deliberately does NOT apply a drop. That the two branches disagree
            // is the point: grounding belongs to real art only.
            var enemy = Named("Enemy0Sprite").GetComponent<Image>();
            Assert.AreEqual(0f, enemy.rectTransform.offsetMin.y, 0.001f,
                "the plate has no feet to ground");
        }

        private static StanceManifest ManifestWithGroundLine(string actor, float groundLine)
        {
            var raw = new RawStanceManifest
            {
                actors = new List<RawStanceActor>
                {
                    new RawStanceActor { spritePath = actor, groundLine = groundLine },
                },
            };
            return new StanceManifest(raw);
        }
    }
}
