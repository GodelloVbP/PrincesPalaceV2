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
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // HUNT 2026-09-11, FAMILY B for the three spell players (docs/hunt/
    // SCENARIOS.md rows B16, B17, B18) and C5: switched off mid-cast, and a
    // scene load mid-cast.
    //
    // WHAT THE EXISTING SUITE STOPS SHORT OF. SpellRendererAndClockTests
    // asserts that every renderer comes back whole when its cast ENDS -- the
    // ordinary path, where Release runs because the schedule ran out. Each of
    // these three classes also has an OnDisable that stands in for that when
    // the fight is abandoned instead, and none of the three had anything
    // asserting it. The difference matters because the disable path is the
    // one that runs when a player quits mid-cast, and the state it has to
    // leave behind is the state the NEXT fight starts from -- a renderer left
    // claimed is a renderer the next cast cannot have, and the pool is not
    // refilled.
    public class SpellPlayerDisableLifecycleTests
    {
        private FightController _fight;
        private float _clock;

        [SetUp]
        public void PlayFast()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            FightController.BreathSpeedMultiplier = 0f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            FightBeatPlayer.PlayerSpeedSource = () => 1f;
            FightController.BreathSpeedMultiplier = 1f;
            SpellPerformancePlayer.ClockOverride = null;
            Time.timeScale = 1f;
        }

        // The module's own clock, held and stepped by hand -- the shape
        // SpellRendererAndClockTests established, and for its reason: anything
        // asking "what is claimed at t" needs t to be a number rather than
        // whenever the next frame arrived.
        private void HoldTheClock()
        {
            _clock = Time.time;
            SpellPerformancePlayer.ClockOverride = () => _clock;
        }

        private void StepTo(float castSeconds)
        {
            _clock += FightBeatPlayer.Scaled(castSeconds);
            _fight.PerformancePlayerForTest.Tick(_clock);
        }

        private IEnumerator LoadFight()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foes = Enumerable.Range(0, 3)
                .Select(i => new CombatantState(i == 0 ? "Front" : $"Foe{i}", false, 5000, 10, 8, 4))
                .ToArray();
            var encounter = new CombatEncounter(new[] { hero }, foes);
            var enemyKits = foes
                .Select(f => new EnemyKit(new ResolvedEnemy(f.Name.ToLowerInvariant(), f.Name,
                    new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false))
                .ToList();

            var session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                enemyKits, new SeededRandom(5));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        private CombatantState Hero => _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);

        private List<CombatantState> Foes =>
            _fight.SessionForTest.Encounter.Enemies.Where(e => e != null && e.IsAlive).ToList();

        private List<SpellVfxPlayer> Effects =>
            _fight.GetComponentsInChildren<SpellVfxPlayer>(includeInactive: true)
                .Where(p => !_fight.GroundVfxPlayersForTest.Contains(p))
                .ToList();

        // The same frost flare the renderer fixture casts, so a failure here
        // and a failure there are about the same spell on the same stage.
        private CombatBeat FlareAt(CombatantState target) => new CombatBeat
        {
            Actor = Hero,
            Target = target,
            Vfx = new SpellPresentation
            {
                path = "Spells/frost_flare",
                seconds = 0.52f,
                impactFrame = 5,
                impactX = 0.5f,
                impactY = 0.18f,
            },
        };

        // AN EMITTER, AUTHORED HERE rather than borrowed from a shipped
        // spell: which authored moment of which shipped spell sheds a
        // particle is content that can be re-authored, and this row is about
        // the renderer rather than about any spell. Built the way
        // AnEmitterWithNoFramesDrawsNothingAndDoesNotStarveTheNextCast builds
        // its own, with a path that really does have frames and lives long
        // enough that the particles are still held when the disable lands.
        private CombatBeat ABurstAt(CombatantState target) => new CombatBeat
        {
            Actor = Hero,
            Target = target,
            Vfx = new SpellPresentation
            {
                layerFormat = SpellLayerRules.CurrentLayerFormat,
                layers = new[]
                {
                    new SpellLayer
                    {
                        id = "drops", render = "emitter", place = "target", at = "release",
                        emitter = new SpellEmitter
                        {
                            path = "Spells/frost_flare",
                            burst = 3, rate = 0f, window = 0f,
                            lifeMin = 30f, lifeMax = 30f, sizeMin = 1f, sizeMax = 1f,
                        },
                    },
                },
            },
        };

        // ---- B17: SpellVfxPlayer disabled mid-effect ------------------------------

        [UnityTest]
        public IEnumerator AnEffectPlayerSwitchedOffMidCastLeavesNoFrameAndNoDissolveBehind()
        {
            yield return LoadFight();
            HoldTheClock();

            _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            StepTo(0.1f);

            var drawing = Effects.FirstOrDefault(p => p.Image != null && p.Image.enabled);
            Assert.IsNotNull(drawing, "nothing drew, so a disable proves nothing");

            var image = drawing.Image;
            var dissolve = drawing.GetComponentsInChildren<Image>(includeInactive: true)
                .FirstOrDefault(i => i.name.EndsWith("Next"));
            Assert.IsNotNull(dissolve, "the effect player has no dissolve layer");

            // ASSERTED ON THE SAME FRAME, with no yield between the disable
            // and the reading, and that is not impatience. The performance
            // player is still live and still owns this member: its next
            // Update draws the cast's current frame straight back into a
            // member that has been switched off, so a frame of slack here
            // would be reading the module's redraw rather than OnDisable's
            // restoration. In the case this row is about -- an abandoned
            // fight -- both go down together.
            drawing.gameObject.SetActive(false);

            Assert.IsFalse(drawing.IsPlaying, "the player still reports itself playing");
            Assert.IsFalse(image.enabled, "the abandoned effect is still drawn");
            Assert.IsNull(image.sprite, "the member kept the last frame it drew");
            Assert.AreEqual(1f, image.color.a, 1e-4f, "the member kept a faded alpha");
            Assert.AreEqual(1f, image.rectTransform.localScale.x, 1e-4f, "the member kept its mirror");

            // THE DISSOLVE LAYER TOO, which is the half StoppingClearsThe-
            // DissolveLayerTooNotJustTheFrame pins for the explicit stop and
            // nothing pinned for the disable.
            Assert.IsFalse(dissolve.enabled, "the dissolve layer is still drawing a stopped spell's frame");
            Assert.IsNull(dissolve.sprite, "the dissolve layer kept its frame");
        }

        // ---- B16: SpellParticleRenderer disabled mid-cast -------------------------

        [UnityTest]
        public IEnumerator AParticleRendererSwitchedOffMidCastPutsEveryParticleBack()
        {
            yield return LoadFight();
            HoldTheClock();

            var particles = _fight.GetComponentInChildren<SpellParticleRenderer>(includeInactive: true);
            Assert.IsNotNull(particles, "the fight scene has no particle renderer");
            Assert.Greater(particles.Capacity, 0, "the particle pool is empty");

            // An authored burst, not one of the shipped spells: see ABurstAt.
            _fight.PlaySpellVfxForTest(ABurstAt(Foes[0]));

            // Walked forward until something is actually claimed rather than
            // guessed at: which authored moment sheds a particle is the
            // spell's business, not this test's.
            bool claimed = false;
            for (int step = 0; step < 40 && !claimed; step++)
            {
                StepTo(0.05f);
                claimed = Enumerable.Range(0, particles.Capacity).Any(particles.IsDrawing);
            }

            Assert.IsTrue(claimed, "no particle was ever drawn, so a disable proves nothing");

            particles.gameObject.SetActive(false);

            var stillHeld = Enumerable.Range(0, particles.Capacity).Where(particles.IsDrawing).ToList();
            CollectionAssert.IsEmpty(stillHeld,
                "particles were left claimed by an abandoned cast: " + string.Join(", ", stillHeld) +
                " -- the pool is never refilled, so the next fight is short by exactly this many");
        }

        // ---- B18: SpellPerformancePlayer disabled mid-performance -----------------

        [UnityTest]
        public IEnumerator ThePerformancePlayerSwitchedOffMidCastDropsTheCastAndKeepsTheClockSeam()
        {
            yield return LoadFight();
            HoldTheClock();

            var player = _fight.PerformancePlayerForTest;
            Assert.IsNotNull(player, "the fight scene has no performance player");

            // MEASURED FROM MID-CAST, not from before it: the bands are
            // sized on first use, so "free renderers" before anything has
            // ever cast is 0 out of 0 rather than a full pool.
            _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            StepTo(0.1f);

            int freeDuringCast = player.FreeEffectRenderers;
            Assert.IsTrue(Effects.Any(p => p.Image != null && p.Image.enabled),
                "nothing drew, so a disable proves nothing");

            var heldClock = SpellPerformancePlayer.ClockOverride;

            player.gameObject.SetActive(false);

            Assert.Greater(player.FreeEffectRenderers, freeDuringCast,
                "an abandoned performance kept its renderers -- every later cast in the next fight " +
                "is short by that many");

            // THE CLOCK SEAM IS NOT PER-CAST STATE. ClockOverride is a test
            // seam that outlives any one performance, and a teardown that
            // nulled it would silently un-pin every fixture that holds the
            // clock across a disable.
            Assert.AreSame(heldClock, SpellPerformancePlayer.ClockOverride,
                "OnDisable reset the clock override, which is a test seam rather than per-cast state");
        }

        // ---- C5: a scene load mid-cast --------------------------------------------

        [UnityTest]
        public IEnumerator ASceneLoadMidCastStillLeavesTheNextFightARendererToCastWith()
        {
            yield return LoadFight();
            HoldTheClock();

            _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            StepTo(0.1f);

            Assert.IsTrue(Effects.Any(p => p.Image != null && p.Image.enabled),
                "nothing drew, so the load abandons nothing");

            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;
            LogAssert.ignoreFailingMessages = false;

            // The clock override survives the load by design (see B18), so the
            // new scene's player is stepped by the same hand.
            yield return LoadFightIntoTheCurrentScene();

            _fight.PlaySpellVfxForTest(FlareAt(Foes[0]));
            StepTo(0.1f);

            Assert.IsTrue(Effects.Any(p => p.Image != null && p.Image.enabled),
                "the first cast of the next fight drew nothing -- the abandoned one took a renderer " +
                "with it, which is the property AnEmitterWithNoFramesDrawsNothingAndDoesNotStarve" +
                "TheNextCast states for a cast that ENDS and nothing stated for one that is abandoned");
        }

        // Re-binds a session onto whatever FightController is live now,
        // without loading a scene -- the second half of the C5 drive, where
        // the load has already happened.
        private IEnumerator LoadFightIntoTheCurrentScene()
        {
            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the second Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var enemyKit = new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0), false);

            var session = new FightSession(encounter, new List<PlayerKit> { PlayModeSparkFixture.Kit() },
                new List<EnemyKit> { enemyKit }, new SeededRandom(5));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }
    }
}
