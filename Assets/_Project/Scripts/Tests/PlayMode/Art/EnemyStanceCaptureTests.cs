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
using PrincesPalace.Domain.Stage;

namespace PrincesPalace.PlayModeTests
{
    // Photographs a monster's whole stance kit, standing on the real stage.
    //
    // WHAT NO OTHER TEST HERE ANSWERS. EnemyFightableTests proves a monster can
    // be fought; FightStageVisualTests proves the stage applies facing and the
    // ground line to a fixture. Neither looks at a specific creature's art, and
    // the failures that matter for new art are all visual: a stance that loads
    // as null and shows the fallback plate, a figure hovering because its
    // ground line is wrong, a pose that jumps sideways between frames because
    // the frames were cropped independently.
    //
    // So this asserts the two halves a test CAN check -- every authored stance
    // resolves to a real sprite, and every stance of an actor shares one canvas
    // -- and then writes a contact sheet for the half it cannot, which is
    // whether the creature looks right.
    //
    // Graphics device only for the capture: tools/screenshot.ps1 -Runtime
    // -RuntimeFilter EnemyStanceCaptureTests. The assertions run headless.
    public class EnemyStanceCaptureTests
    {
        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        // The kits this file was written for, and their stances -- read from
        // content where the fight drives them and named here where a skill
        // does, because a skill's stance is a string on a ScriptableObject and
        // "which stances does this monster have" has no other home.
        //
        // The Forest Warden earns its place alongside the other two: its six
        // drawings arrived on six DIFFERENT canvases each (a per-frame crop
        // plus a uniform pad, which is not a registration) and were
        // recomposited onto one, so it is the one kit on the roster whose
        // one-canvas promise was made by hand rather than by the slicer.
        private static readonly Dictionary<string, string[]> Kits = new Dictionary<string, string[]>
        {
            ["beetle"] = new[] { "idle", "attack", "turtle_up", "shell_closed", "hurt", "defeated" },
            ["treant"] = new[] { "idle", "attack", "trunk_slam", "cast", "hurt", "defeated" },
            ["forest_warden"] = new[] { "idle", "attack", "attack_roar", "attack_charge", "hurt", "defeated" },
        };

        [SetUp]
        public void PlayBeatsFast() => FightBeatPlayer.BeatSpeedMultiplier = 8f;

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            StanceManifestLoader.Reset();
        }

        private static void Click(FightController fight, string name)
        {
            var go = fight.GetComponentsInChildren<Transform>(includeInactive: true)
                .FirstOrDefault(t => t.name == name)?.gameObject;
            Assert.IsNotNull(go, $"no object named '{name}' in the fight scene");
            go.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        // A stance that fails to load is INVISIBLE at runtime -- the stage
        // falls through to its nameplate and nothing logs. The same silent
        // failure EnemySpriteImportPostprocessor exists to stop.
        [Test]
        public void EveryAuthoredStanceResolvesToADrawing()
        {
            var missing = new List<string>();

            foreach (var (id, stances) in Kits.Select(k => (k.Key, k.Value)))
            {
                foreach (string stance in stances)
                {
                    if (Resources.Load<Sprite>($"Enemies/{id}/{stance}") == null)
                    {
                        missing.Add($"Enemies/{id}/{stance}");
                    }
                }
            }

            Assert.IsEmpty(missing,
                "these stances load nothing, so the stage shows a nameplate instead of a monster: " +
                string.Join(", ", missing));
        }

        // ONE CANVAS PER ACTOR, which is what makes StanceManifest's single
        // authored ground line mean anything: the stage sizes each slot to the
        // sprite it is showing (FightController.StageVisuals), so drawings of
        // different sizes move the figure by the difference as it changes pose.
        // Every one of these kits was composited onto its shared canvas by the
        // slicer's ground-band anchor -- this is that delivery asserted rather
        // than trusted.
        [Test]
        public void EveryStanceOfAnActorSharesOneCanvas()
        {
            foreach (var (id, stances) in Kits.Select(k => (k.Key, k.Value)))
            {
                var sizes = new Dictionary<string, Vector2>();

                foreach (string stance in stances)
                {
                    var sprite = Resources.Load<Sprite>($"Enemies/{id}/{stance}");
                    if (sprite != null) sizes[stance] = sprite.rect.size;
                }

                Assert.IsNotEmpty(sizes, $"{id} has no drawings at all");

                var distinct = sizes.Values.Distinct().ToList();
                Assert.AreEqual(1, distinct.Count,
                    $"{id}'s stances span {distinct.Count} canvas sizes, so it changes size and position as it " +
                    $"changes pose: {string.Join(", ", sizes.Take(8).Select(p => $"{p.Key}={p.Value}"))}");
            }
        }

        // And the ground line has to be authored at all, or the figure stands
        // on its own canvas bottom -- which for a padded canvas is 12px of
        // nothing, and reads as hovering.
        [Test]
        public void EveryKitIsInTheStanceManifest()
        {
            foreach (string id in Kits.Keys)
            {
                Assert.IsTrue(StanceManifestLoader.Manifest.HasActor($"Enemies/{id}"),
                    $"Enemies/{id} has no StanceManifest entry, so it falls back to the canvas bottom");
            }
        }

        // ---- the whole roster ------------------------------------------------
        //
        // EveryStanceEveryEnemyCanReachResolvesToFrames USED TO LIVE HERE and
        // moved to EnemyArtCompletenessTests, beside the canvas and manifest
        // sweeps that ask the same question of the same set. It was the one
        // rule in this file that had nothing to do with photographing a kit --
        // it swept every enemy in the catalogue -- and leaving it here meant a
        // completeness check sitting in a capture fixture, where a reader
        // looking for "what proves new art is finished" would not find it.
        // Nothing about it changed in the move.

        // ---- the picture ------------------------------------------------------

        [UnityTest]
        public IEnumerator CaptureBothKitsOnTheStage()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter EnemyStanceCaptureTests");
            }

            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the fight scene has no controller");

            // THE REAL CONTENT, resolved the way a real room resolves it, so
            // the shot shows what the game would actually field -- stats,
            // facing, ground line and all -- rather than a fixture wearing the
            // right sprite folder.
            var beetle = ContentDatabase.Enemies.FirstOrDefault(e => e.id == "beetle");
            var treant = ContentDatabase.Enemies.FirstOrDefault(e => e.id == "treant");
            Assert.IsNotNull(beetle, "beetle is not in the content database");
            Assert.IsNotNull(treant, "treant is not in the content database");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);
            var one = new CombatantState(beetle.data.DisplayName, false, 5000, 0, 4, 5);
            var two = new CombatantState(treant.data.DisplayName, false, 5000, 0, 6, 3);

            var encounter = new CombatEncounter(new[] { hero }, new[] { one, two });
            var session = new FightSession(encounter,
                new List<PlayerKit> { null },
                new List<EnemyKit>
                {
                    new EnemyKit(FightEncounterAdapter.Resolve(beetle), false),
                    new EnemyKit(FightEncounterAdapter.Resolve(treant), false),
                },
                new Domain.Rng.SeededRandom(4));

            session.Begin();
            fight.Bind(session, EncounterClass.Normal);

            // Long enough for the stage to settle -- the fly-in, the plates,
            // the shadows. Both figures are mid-breath by then, and where in
            // the breath is deterministic (BreathCurve.PhaseFor off the slot
            // index), so the shot does not differ run to run.
            yield return new WaitForSecondsRealtime(1.2f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, "Enemies_beetle_treant.png");
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[EnemyStanceCapture] wrote {path}");
        }
    }
}
