using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    // A spell in flight, frame by frame.
    //
    // SpellVfxTests pins WHERE the box lands, which is the half that can be
    // asserted. It cannot see whether the sequence inside that box reads as a
    // spell -- whether the glyph turns, whether the lance arrives before the
    // impact, whether a mirrored cast looks mirrored or merely wrong. That is a
    // picture, and every placement bug this file's siblings record was found by
    // looking at one.
    //
    // Six shots across a single cast, at the real speed rather than the 60x the
    // behaviour tests run at: the point is the timing.
    public class SpellCastCaptureTests
    {
        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            Time.captureFramerate = 0;
        }

        [UnityTest]
        public IEnumerator CaptureMudBurstCrossingTheStage()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("Headless: camera.Render() is a no-op. " +
                              "Run: tools/screenshot.ps1 -Runtime -RuntimeFilter SpellCastCaptureTests");
                yield break;
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the fight scene has no controller");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 0, 10);
            var foe = new CombatantState("Bog Witch", false, 5000, 10, 8, 0, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = new PlayerKit("shawn", CharacterRole.Tank, null, null, null,
                new ResolvedSpellTier(1, "Spark", 6, 1.5f, 0), level: 4);
            var enemyKit = new EnemyKit(new ResolvedEnemy("witch", "Bog Witch", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0), false);

            fight.Bind(new FightSession(encounter, new List<PlayerKit> { kit },
                new List<EnemyKit> { enemyKit }, new SeededRandom(5)), EncounterClass.Normal);
            yield return null;

            Directory.CreateDirectory(OutputDir);
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas, "the fight scene has no root canvas");

            // NO Time.captureFramerate HERE, and that is the one thing this
            // capture cannot borrow from its siblings. SpellVfxPlayer advances
            // its frames on WaitForSecondsRealtime -- deliberately, so a spell
            // cannot outlast the turn that cast it when a test runs the fight at
            // 60x -- and real time does not care what the capture framerate is.
            // Set, it froze every shot on the second frame of the sequence and
            // produced six identical pictures of a glyph.

            yield return Shoot(fight, canvas, hero, foe, "away");
            yield return Shoot(fight, canvas, foe, hero, "back");
        }

        // One cast, sampled six times across its own length. The sample points
        // are fractions rather than seconds so they still land in the right
        // places if the spell's authored duration changes.
        private IEnumerator Shoot(FightController fight, Canvas canvas,
            CombatantState actor, CombatantState target, string label)
        {
            // SLOWED DOWN FOR THE CAMERA, and it has to be. Writing a 1920x1080
            // PNG takes longer than a frame of the real 0.78s cast, so six shots
            // paced against the real duration all landed inside the first two
            // frames -- six pictures of a glyph, from a sequence that has a
            // lance and a burst in it.
            //
            // Stretching the cast rather than stepping the coroutine by hand
            // keeps this a picture of the SHIPPING sequence: same frames, same
            // order, same easing on the flight, only slower.
            const float seconds = 7f;
            float[] at = { 0.05f, 0.30f, 0.55f, 0.72f, 0.85f, 0.97f };

            fight.PlaySpellVfxForTest(new CombatBeat
            {
                Actor = actor,
                Target = target,
                VfxPath = "Spells/mud_burst",
                VfxSeconds = seconds,
                VfxImpactFrame = 11,
                VfxFromCaster = true,
            });

            float started = Time.realtimeSinceStartup;
            for (int i = 0; i < at.Length; i++)
            {
                while (Time.realtimeSinceStartup - started < at[i] * seconds)
                {
                    yield return null;
                }

                CanvasCapture.RenderToFile(canvas,
                    Path.Combine(OutputDir, $"MudBurst_{label}_{i}.png"), 1920, 1080);
            }
        }
    }
}
