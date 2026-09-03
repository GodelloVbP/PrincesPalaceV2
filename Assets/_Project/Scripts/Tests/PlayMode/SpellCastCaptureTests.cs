using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        public IEnumerator CaptureEverySpellShawnCastsAtAGiantRat()
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

            // A GIANT RAT, not the Bog Witch, and the swap is the point.
            //
            // Every placement bug this file exists to catch was reported
            // against the rat, and the rat is why: it is 675x306 of canvas,
            // most of it length, so its ground line and its middle are nowhere
            // near each other. A tall humanoid hides the difference -- an
            // effect placed by either rule lands somewhere on it.
            //
            // spritePath spelled out because that is what the stage reads to
            // find the art (see FightController.SpriteFolderFor); a synthetic
            // enemy without one poses as a fallback plate and the capture shows
            // a spell going off next to a grey rectangle.
            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var foe = new CombatantState("Giant Rat", false, 5000, 10, 8, 4);
            var encounter = new CombatEncounter(new[] { hero }, new[] { foe });
            var kit = PlayModeSparkFixture.Kit();
            var enemyKit = new EnemyKit(new ResolvedEnemy("rat", "Giant Rat", new StatBlock(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0, spritePath: "Enemies/rat"), false);

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

            yield return Shoot(fight, canvas, hero, foe, "mud_burst", "away");
            yield return Shoot(fight, canvas, foe, hero, "mud_burst", "back");

            // All three of Shawn's spells, and all three read from CONTENT.
            //
            // They used to be captured through a presentation this file built
            // by hand, which quietly made the pictures useless for the one
            // thing they are for: a capture of a fixture is a capture of the
            // fixture's anchor and the fixture's impact point, not of the
            // spell's. Both of the placement bugs the impact point exists to
            // fix were invisible here for exactly that reason, and both were
            // reported by a player instead.
            yield return Shoot(fight, canvas, hero, foe, "frost_flare", "frost");
            yield return Shoot(fight, canvas, hero, foe, "lightning_bolt", "bolt");
        }

        // One cast, sampled six times across its own length. The sample points
        // are fractions rather than seconds so they still land in the right
        // places if the spell's authored duration changes.
        private IEnumerator Shoot(FightController fight, Canvas canvas,
            CombatantState actor, CombatantState target,
            string skillId, string label)
        {
            var skill = ContentDatabase.Skills.FirstOrDefault(s => s != null && s.id == skillId);
            Assert.IsNotNull(skill, $"'{skillId}' is not in skills.json");
            Assert.IsFalse(string.IsNullOrEmpty(skill.vfx.path), $"'{skillId}' has no vfx path");

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

            // SAMPLED BETWEEN FRAMES AS WELL AS ON THEM. The dissolve happens
            // over the last 45% of each frame's time, so a sequence sampled at
            // frame boundaries would look exactly like the stepped one it
            // replaced -- the whole change is invisible at the moments a naive
            // capture picks.
            float[] at = { 0.06f, 0.22f, 0.29f, 0.50f, 0.57f, 0.74f, 0.86f, 0.97f };

            // THE SHIPPED PRESENTATION, with only its duration overridden.
            // Copy() rather than the asset's own object: a beat holding the
            // catalogue's instance would let this capture edit the content
            // every later test in the process reads.
            var presentation = skill.vfx.Copy();
            presentation.seconds = seconds;

            fight.PlaySpellVfxForTest(new CombatBeat
            {
                Actor = actor,
                Target = target,
                Vfx = presentation,
            });

            float started = Time.realtimeSinceStartup;
            for (int i = 0; i < at.Length; i++)
            {
                while (Time.realtimeSinceStartup - started < at[i] * seconds)
                {
                    yield return null;
                }

                CanvasCapture.RenderToFile(canvas,
                    Path.Combine(OutputDir, $"SpellCast_{label}_{i}.png"), 1920, 1080);
            }
        }
    }
}
