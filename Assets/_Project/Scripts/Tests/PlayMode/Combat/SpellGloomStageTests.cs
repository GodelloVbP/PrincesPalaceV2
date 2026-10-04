using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
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
    // A spell's gloom on the real stage: the roundDarkness image shows the
    // larger of the event's darkness and the spell's, fades on the cast's own
    // clock, and is hidden again once both are zero. The curve itself is
    // SpellGloomTests; this is the wiring, driven on the held cast clock.
    public class SpellGloomStageTests
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

        private IEnumerator Load(FightRoundPresentation presentation)
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
            _fight.Bind(session, EncounterClass.Normal, null, presentation);
            yield return null;
        }

        private Image Darkness => _fight.GetComponentsInChildren<Image>(true).First(i => i.name == "RoundDarkness");

        private static FightRoundPresentation EventDark(float start) =>
            new FightRoundPresentation(roundLimit: 10, label: "Toll", roundSfxPath: "Audio/Sfx/does_not_exist",
                darknessStart: start, darknessStep: 0f, darknessFloor: start);

        private CastHandle CastCourt(out float startClock)
        {
            var definition = ContentDatabase.Skills.First(s => s != null && s.id == "court_of_whispers");
            var vfx = PreviewFight.PreviewPresentationOf(definition.Data);
            Assert.AreEqual(0.35f, vfx.gloom.strength, 1e-6f, "fixture: court_of_whispers authors its gloom");

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

        [UnityTest]
        public IEnumerator WithNoEventDarknessTheStageDarkensInTheWindUpAndIsHiddenAgainAfter()
        {
            yield return Load(FightRoundPresentation.None);
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Assert.IsFalse(Darkness.gameObject.activeSelf, "fixture: nothing darkens the stage yet");

            var handle = CastCourt(out float t0);

            yield return At(t0, 0.1f);
            Assert.IsTrue(Darkness.gameObject.activeSelf);
            Assert.AreEqual(0.175f, Darkness.color.a, 1e-3f, "halfway up the fade-in");

            yield return At(t0, 0.3f);
            Assert.AreEqual(0.35f, Darkness.color.a, 1e-3f, "held through the wind-up, to the payoff");

            yield return At(t0, 0.425f);
            Assert.AreEqual(0.175f, Darkness.color.a, 1e-3f, "lifting after the payoff");

            yield return At(t0, 10f);
            Assert.IsFalse(_fight.PerformancePlayerForTest.IsLive(handle));
            Assert.IsFalse(Darkness.gameObject.activeSelf, "hidden once the gloom is gone");
            Assert.AreEqual(0f, Darkness.color.a, 1e-6f);
        }

        // The stage reads the instant the layers were painted at: a Tick on a
        // held clock moves GloomAlpha even when the module's own clock has not.
        [UnityTest]
        public IEnumerator GloomAlphaFollowsTheClockTickWasGiven()
        {
            yield return Load(FightRoundPresentation.None);
            FightBeatPlayer.BeatSpeedMultiplier = 1f;

            CastCourt(out float t0);
            _fight.PerformancePlayerForTest.Tick(t0 + 0.3f);

            Assert.AreEqual(0.35f, _fight.PerformancePlayerForTest.GloomAlpha, 1e-3f,
                "GloomAlpha read the module's clock (still at the cast's birth) instead of Tick's");
        }

        [UnityTest]
        public IEnumerator AtOnePointFiveXTheGloomRunsOnTheFasterClock()
        {
            yield return Load(FightRoundPresentation.None);
            FightBeatPlayer.BeatSpeedMultiplier = 1.5f;

            CastCourt(out float t0);

            yield return At(t0, 0.1f);
            Assert.AreEqual(0.2625f, Darkness.color.a, 1e-3f, "0.1s real is 0.15s authored");

            yield return At(t0, 0.3f);
            Assert.AreEqual(0.14f, Darkness.color.a, 1e-3f, "0.3s real is 0.45s authored, lifting");
        }

        [UnityTest]
        public IEnumerator ABellInTheFogNightAtNinetyPercentIsUnchangedByTheSpell()
        {
            yield return Load(EventDark(0.9f));
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Assert.AreEqual(0.9f, Darkness.color.a, 1e-4f, "fixture: the event's darkness");

            CastCourt(out float t0);

            foreach (float t in new[] { 0.1f, 0.3f, 0.425f, 10f })
            {
                yield return At(t0, t);
                Assert.AreEqual(0.9f, Darkness.color.a, 1e-4f, $"event 0.9 is untouched at {t}s");
                Assert.IsTrue(Darkness.gameObject.activeSelf);
            }
        }

        [UnityTest]
        public IEnumerator AtTheEventFloorTheSpellShowsThenTheEventIsBack()
        {
            yield return Load(EventDark(0.15f));
            FightBeatPlayer.BeatSpeedMultiplier = 1f;

            CastCourt(out float t0);

            yield return At(t0, 0.3f);
            Assert.AreEqual(0.35f, Darkness.color.a, 1e-3f, "gloom is the larger");

            yield return At(t0, 10f);
            Assert.AreEqual(0.15f, Darkness.color.a, 1e-4f, "back to the event's own darkness");
            Assert.IsTrue(Darkness.gameObject.activeSelf);
        }
    }
}
