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
    // WHAT IT NO LONGER DOES, and why the file is shorter for it. This used to
    // carry three assertions of its own -- every authored stance resolves,
    // every stance of an actor shares one canvas, every kit is in the
    // StanceManifest -- each run over a hand-written dictionary of three
    // monsters. EnemyArtCompletenessTests already asks all three questions of
    // EVERY enemy in the catalogue, headless, and has since Step 1. Two copies
    // of one rule where one covers three mobs and the other covers all of them
    // is not defence in depth; it is a smaller rule that quietly reports green
    // about a smaller set, and the smaller one is the one sitting in the file
    // an author opens when they add art. So the assertions are gone and the
    // sweep is the only home for them.
    //
    // What is left is the half a test cannot check: whether the creature LOOKS
    // right. Over the whole roster now rather than three named kits, three to
    // a stage because that is what the stage holds.
    //
    // Graphics device only: tools/screenshot.ps1 -Runtime -RuntimeFilter
    // EnemyStanceCaptureTests. Headless, it self-skips -- the assertions that
    // used to justify running it headless live next door now.
    public class EnemyStanceCaptureTests
    {
        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "runtime"));

        // EVERY MONSTER WITH ART, and every stance it can reach, both read from
        // content.
        //
        // This was a dictionary naming three ids and their six stances each,
        // written when three mobs had art. The roster has grown past it, and a
        // hand-kept list in a capture fixture fails in the quietest possible
        // way: the mob added after it is simply never photographed, and there
        // is no missing picture to notice because nobody knows to look for one.
        // The Step 0 baseline paid the other half of that bill -- adding a mob
        // meant editing this file's literals before it could be seen at all.
        //
        // StancesReachableBy is EnemyArtCompletenessTests' walk, borrowed
        // rather than restated: the four the fight drives plus whatever every
        // ability the mob can draw asks for. A second copy of that rule here is
        // exactly how the photograph would come to show a smaller kit than the
        // sweep checks.
        private static Dictionary<string, string[]> Kits() =>
            ContentDatabase.Enemies
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.data.SpritePath))
                .OrderBy(e => e.id, System.StringComparer.Ordinal)
                .ToDictionary(
                    e => e.id,
                    e => EnemyArtCompletenessTests.StancesReachableBy(e).OrderBy(x => x).ToArray());

        // What the stage holds at once (FightHudSpec.StageSlotsPerSide), so a
        // roster of any size is photographed in full rather than truncated to
        // whatever fits in one shot.
        private const int PerStage = 3;

        [SetUp]
        public void PlayBeatsFast() => FightBeatPlayer.BeatSpeedMultiplier = 8f;

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            StanceManifestLoader.Reset();
        }

        // ---- the picture ------------------------------------------------------

        [UnityTest]
        public IEnumerator CaptureEveryDrawnMonsterOnTheStage()
        {
            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. Run: tools/screenshot.ps1 -Runtime -RuntimeFilter EnemyStanceCaptureTests");
            }

            var kits = Kits();
            Assert.IsNotEmpty(kits, "no enemy in content has art, so this capture is vacuous");

            Directory.CreateDirectory(OutputDir);

            var ids = kits.Keys.ToList();
            for (int start = 0; start < ids.Count; start += PerStage)
            {
                var batch = ids.Skip(start).Take(PerStage).ToList();
                yield return CaptureStage(batch);

                // THE STANCES EACH ONE CAN REACH, logged beside the picture.
                // A photograph shows one pose; what an author needs when they
                // look at it is which OTHER poses that mob has, because a
                // typo'd ability stance is a monster that vanishes for exactly
                // one turn and there is no shot of that turn to take.
                foreach (string id in batch)
                {
                    Debug.Log($"[EnemyStanceCapture] {id} can reach: {string.Join(", ", kits[id])}");
                }
            }
        }

        // One stageful. THE REAL CONTENT, resolved the way a real room
        // resolves it, so the shot shows what the game would actually field --
        // stats, facing, ground line and all -- rather than a fixture wearing
        // the right sprite folder.
        private IEnumerator CaptureStage(List<string> ids)
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight, "the fight scene has no controller");

            var definitions = ids
                .Select(id => ContentDatabase.Enemies.FirstOrDefault(e => e.id == id))
                .Where(e => e != null)
                .ToList();

            Assert.IsNotEmpty(definitions, "none of " + string.Join(", ", ids) + " is in the content database");

            var hero = new CombatantState("Shawn", true, 300, 30, 40, 10);

            // Health high enough that nothing dies inside the window, speed
            // spread so the stage settles in a fixed order -- the same
            // arrangement this fixture always used, now over a variable list.
            var foes = definitions
                .Select((e, i) => new CombatantState(e.data.DisplayName, false, 5000, 0, 4 + i, 5 - i))
                .ToList();

            var session = new FightSession(
                new CombatEncounter(new[] { hero }, foes),
                new List<PlayerKit> { null },
                definitions.Select(e => new EnemyKit(FightEncounterAdapter.Resolve(e), false)).ToList(),
                new Domain.Rng.SeededRandom(4));

            session.Begin();
            fight.Bind(session, EncounterClass.Normal);

            // Long enough for the stage to settle -- the fly-in, the plates,
            // the shadows. Every figure is mid-breath by then, and where in
            // the breath is deterministic (BreathCurve.PhaseFor off the slot
            // index), so the shot does not differ run to run.
            yield return new WaitForSecondsRealtime(1.2f);

            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                .FirstOrDefault(c => c.isRootCanvas);
            Assert.IsNotNull(canvas);

            string path = Path.Combine(OutputDir, "Enemies_" + string.Join("_", definitions.Select(e => e.id)) + ".png");
            CanvasCapture.RenderToFile(canvas, path);
            Assert.IsTrue(File.Exists(path));
            Debug.Log($"[EnemyStanceCapture] wrote {path}");
        }
    }
}
