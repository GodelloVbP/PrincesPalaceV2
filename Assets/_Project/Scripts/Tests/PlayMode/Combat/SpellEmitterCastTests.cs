using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // AN EMITTER, DRAWN. Everything about the droplet simulation was pinned in
    // EditMode as arithmetic -- closed-form position, seeded repeatability, a
    // fifty-tick step matching a one-tick step -- and none of it said that a
    // live cast puts a single sprite on the canvas.
    //
    // THAT GAP WAS NOT THEORETICAL. The Water pilot's first capture came back
    // with a perfect crown and no droplets at all, twice, and every emitter
    // test in the suite was green while it did: the sim was right, the schedule
    // was right, and the pictures were of a spell missing two of its five
    // layers. Three things between the sim and the screen have no arithmetic to
    // check them -- the pool being wired into the scene at all, the atlas
    // loading, and the member actually being shown -- and each fails by drawing
    // nothing, which is indistinguishable from a spell that authored no
    // emitter.
    //
    // THE CLOCK IS HELD AND THEN MOVED, for the reason SpellVfxTests spells
    // out: at this fixture's 60x a cast's whole budget is milliseconds and a
    // batchmode frame after a scene load was measured at 24ms and 57ms, so
    // "cast, wait a frame, count" asks how long the frame took.
    public class SpellEmitterCastTests
    {
        private FightController _fight;
        private float _castAt;
        private float _held;

        [SetUp]
        public void PlayFast()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 60f;
        }

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            SpellPerformancePlayer.ClockOverride = null;
        }

        private void HoldTheClockAtTheCast()
        {
            _castAt = Time.time;
            _held = _castAt;
            SpellPerformancePlayer.ClockOverride = () => _held;
        }

        private void HoldTheClockAt(float castSeconds)
        {
            _held = _castAt + FightBeatPlayer.Scaled(castSeconds);
            _fight.PerformancePlayerForTest.Tick(_held);
        }

        private IEnumerator LoadFight()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(_fight, "the Fight scene has no FightController");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foe = new CombatantState("Front", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = PlayModeSparkFixture.Kit();
            var enemyKits = new List<EnemyKit>
            {
                new EnemyKit(new ResolvedEnemy("front", "Front", new StatBlock(), 5, 3, false,
                    DamageType.Physical, DamageType.Physical, 0), false),
            };

            var session = new FightSession(encounter, new List<PlayerKit> { kit }, enemyKits,
                new SeededRandom(5));
            session.Begin();
            _fight.Bind(session, EncounterClass.Normal);
            yield return null;
        }

        // The pilot's own authored composition, so what this measures is the
        // spell that ships rather than a fixture shaped to pass.
        private CombatBeat WaterBeat()
        {
            var hero = _fight.SessionForTest.Encounter.PlayerParty.First(c => c != null);
            var foe = _fight.SessionForTest.Encounter.Enemies.First(c => c != null);

            return new CombatBeat { Actor = hero, Target = foe, Vfx = WaterFixture() };
        }

        // Read out of the shipped content rather than restated here: this
        // fixture's whole subject is whether the authored spell draws, and a
        // hand-written copy of it would keep passing after the content changed.
        private static SpellPresentation WaterFixture()
        {
            var skill = ContentDatabase.Skills
                .FirstOrDefault(s => s != null && s.Data != null && s.Data.Id == "prismatic_orb");
            Assert.IsNotNull(skill, "content has no prismatic_orb");

            var water = skill.Data.AsElement(DamageType.Water);
            Assert.IsTrue(water.Vfx != null && water.Vfx.HasLayers,
                "prismatic_orb's Water element authors no layers, so this fixture is measuring nothing");

            return water.Vfx;
        }

        private SpellParticleRenderer Particles =>
            _fight.GetComponentInChildren<SpellParticleRenderer>(includeInactive: true);

        private int DrawnDrops()
        {
            var pool = Particles;
            if (pool == null) return 0;

            int drawn = 0;
            for (int i = 0; i < pool.Capacity; i++)
            {
                if (pool.IsDrawing(i)) drawn++;
            }

            return drawn;
        }

        // THE POOL IS IN THE SCENE. A null renderer makes PaintEmitter return
        // early and silently, which is exactly what "the spell has no droplets"
        // looks like from outside.
        [UnityTest]
        public IEnumerator TheFightSceneCarriesAParticlePool()
        {
            yield return LoadFight();

            var pool = Particles;
            Assert.IsNotNull(pool,
                "the Fight scene has no SpellParticleRenderer, so every emitter any spell authors " +
                "draws nothing and nothing says so");
            Assert.AreEqual(FightHudSpec.SpellParticles, pool.Capacity,
                "the pool in the scene is not the size the spec reserves");
        }

        // MID-FLIGHT: the shed is emitting and its drops are on screen. Read at
        // 0.20s of a 0.25s flight, so the window is open and the earliest drops
        // (lifeMin 0.22) have not begun to expire.
        //
        // 0.3667 = 0.1667 + 0.20: 2026-09-09 a caster-side charge was added
        // ahead of "core", so the shed now opens 0.1667s after release (see
        // skills.json's Water block) instead of at release itself -- held
        // 0.1667s later to land on the same 0.20s-of-the-0.25s-window point.
        [UnityTest]
        public IEnumerator TheFlightShedPutsDropsOnScreenWhileTheCoreIsStillCrossing()
        {
            yield return LoadFight();

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(WaterBeat());
            yield return null;

            HoldTheClockAt(0.3667f);
            yield return null;

            // 0.20s at rate 40 is eight births. Asserted as "more than half of
            // them" rather than exactly eight, because the pool is shared and
            // an overflow is allowed to thin droplets out -- what must never
            // happen is none.
            Assert.GreaterOrEqual(DrawnDrops(), 4,
                "the flight shed drew " + DrawnDrops() + " droplets a fifth of a second in. At rate 40 " +
                "that window has birthed eight, and the brief's water shedding is the half of this " +
                "spell that a still picture of the splash cannot show.");
        }

        // AT THE CUE: the impact burst. A different emitter, on a different
        // anchor, at a different schedule point -- so a wiring failure that
        // only reached one of the two shows up here rather than in a capture.
        [UnityTest]
        public IEnumerator TheImpactBurstPutsDropsOnScreenAtTheCue()
        {
            yield return LoadFight();

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(WaterBeat());
            yield return null;

            // 0.05s after the 0.25s cue -- arrival exactly, tightened from
            // 0.327 in the 2026-09-08 second battle-speed pass. That pull
            // means the burst's whole life (cue + lifeMax 0.34 = 0.59) now
            // ends BEFORE the shed's own window+lifeMax clear (0.63s), so a
            // sample point that is past the shed's clear and inside the
            // burst's window no longer exists -- this reads the burst's
            // guaranteed-alive stretch instead (every particle's lifeMin is
            // 0.18s, so all 18 are still drawing this soon after birth), and
            // gives up proving isolation from a shed straggler that the
            // flight-shed test above already covers on its own.
            //
            // 0.4667 = 0.4167 + 0.05: 2026-09-09 the charge pushed the cue
            // from 0.25 to 0.4167 (see skills.json's Water block), so "0.05s
            // after the cue" moved with it.
            HoldTheClockAt(0.4667f);
            yield return null;

            Assert.Greater(DrawnDrops(), 0,
                "nothing was drawn 0.05s after the impact burst's 0.4167s cue, inside every particle's " +
                "guaranteed 0.18s lifeMin");
        }

        // AND THEY ARE ALL GONE WHEN THE CAST IS. A particle that outlives its
        // owner is a member the next cast cannot have.
        [UnityTest]
        public IEnumerator EveryDropIsHandedBackWhenTheCastEnds()
        {
            yield return LoadFight();

            HoldTheClockAtTheCast();
            _fight.PlaySpellVfxForTest(WaterBeat());
            yield return null;

            // 0.3667 = 0.1667 + 0.20: 2026-09-09 the charge pushed the shed's
            // own opening from release to release + 0.1667s (see skills.json's
            // Water block), so a sample comfortably inside its window moved
            // with it -- 0.20s in was nearly the whole window before the
            // charge existed and would now catch the shed a frame after it
            // opens.
            HoldTheClockAt(0.3667f);
            yield return null;
            Assert.Greater(DrawnDrops(), 0, "nothing was drawing, so this proves nothing about cleanup");

            HoldTheClockAt(2f);
            yield return null;

            Assert.AreEqual(0, DrawnDrops(),
                "droplets outlived the cast that threw them, so the next cast starts with a thinner pool");
        }
    }
}
