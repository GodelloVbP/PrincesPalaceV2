using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE CENSER'S STAGE-LEVEL POLISH ON THE REAL SCENE, on the held cast clock:
    // the afterimages hold renderers of their own, the stage is pushed toward the
    // target on the cue and eased back, and the fire at the feet lifts the Burn
    // coal glow of the body it burns. The arithmetic is SpellPolishFieldsTests;
    // this is the wiring.
    public class SpellCenserStageTests
    {
        private FightController _fight;
        private float _clock;

        [TearDown]
        public void Restore()
        {
            SpellPerformancePlayer.ClockOverride = null;
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            TestGlobals.ResetAll();
        }

        private IEnumerator Load()
        {
            yield return FightSceneFixture.LoadFight();

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foes = new[] { new CombatantState("Front", false, 5000, 10, 8, 4) };
            var encounter = new CombatEncounter(new[] { hero }, foes);
            var enemyKits = foes
                .Select(f => new EnemyKit(new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name,
                    new StatBlock(), 5, 3, false, DamageType.Physical, DamageType.Physical, 0), false))
                .ToList();

            var session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                enemyKits, new SeededRandom(5));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal, null, FightRoundPresentation.None);
            yield return null;
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
        }

        private CastHandle CastCenser(out float startClock)
        {
            var definition = ContentDatabase.Skills.First(s => s != null && s.id == "censer_of_embers");
            var vfx = PreviewFight.PreviewPresentationOf(definition.Data);
            Assert.AreEqual(0.025f, vfx.kick.strength, 1e-6f, "fixture: the censer authors its kick");

            var beat = new CombatBeat
            {
                Actor = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null),
                Target = _fight.SessionForTest.Encounter.Enemies.First(e => e != null),
                Vfx = vfx,
            };

            _clock = Time.time;
            SpellPerformancePlayer.ClockOverride = () => _clock;
            startClock = _clock;
            return _fight.PlaySpellVfxForTest(beat);
        }

        private IEnumerator At(float startClock, float engineSeconds)
        {
            _clock = startClock + engineSeconds;
            _fight.PerformancePlayerForTest.Tick(_clock);
            yield return null;
        }

        private float RackX() => _fight.StageShakesForTest.First(s => s != null).GetComponent<RectTransform>().anchoredPosition.x;

        private float MostStoked() => _fight.GetComponentsInChildren<StageBurnGlow>(true).Max(g => g.Stoke);

        [UnityTest]
        public IEnumerator EachAfterimageHoldsARendererOfItsOwnForAsLongAsTheLantern()
        {
            yield return Load();
            var player = _fight.PerformancePlayerForTest;

            CastCenser(out float t0);
            yield return At(t0, 0.5f);

            Assert.AreEqual(4, FightHudSpec.SpellLayerRenderers - player.FreeEffectRenderers,
                "the lantern and its three afterimages");

            yield return At(t0, 10f);
            Assert.AreEqual(FightHudSpec.SpellLayerRenderers, player.FreeEffectRenderers,
                "every one handed back when the cast clears");
        }

        [UnityTest]
        public IEnumerator TheStageIsPushedTowardTheTargetOnTheCueAndEasedBack()
        {
            yield return Load();
            float home = RackX();

            CastCenser(out float t0);
            yield return At(t0, 0.9f);
            Assert.AreEqual(home, RackX(), 0.01f, "nothing before the cue");

            yield return At(t0, 1.015f);
            Assert.AreEqual(home + 0.5f * 48f, RackX(), 0.6f, "half way up the rise");

            yield return At(t0, 1.03f);
            Assert.AreEqual(home + 48f, RackX(), 0.6f, "2.5% of 1920 toward the target, which stands to the right");

            yield return At(t0, 1.12f);
            Assert.AreEqual(home + 12f, RackX(), 0.6f, "a quarter of the push half way down");

            yield return At(t0, 1.5f);
            Assert.AreEqual(home, RackX(), 0.01f, "back on its mark");

            yield return At(t0, 10f);
            Assert.AreEqual(home, RackX(), 0.01f);
        }

        [UnityTest]
        public IEnumerator TheFireAtTheFeetLiftsTheBurnGlowOnlyWhileItLives()
        {
            yield return Load();

            CastCenser(out float t0);
            yield return At(t0, 0.5f);
            Assert.AreEqual(0f, MostStoked(), 0f, "nothing burns yet");

            yield return At(t0, 1.9f);
            Assert.Greater(MostStoked(), 0.4f, "the fire is up and flickering");

            yield return At(t0, 10f);
            Assert.AreEqual(0f, MostStoked(), 0f, "the fire is out");
        }
    }
}
