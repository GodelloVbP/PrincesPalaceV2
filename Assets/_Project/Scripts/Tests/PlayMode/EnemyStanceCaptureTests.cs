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
    // resolves to a real sprite, and every frame of an actor shares one canvas
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

        // The two kits this file was written for, and their stances -- read
        // from content where the fight drives them and named here where a skill
        // does, because a skill's stance is a string on a ScriptableObject and
        // "which stances does this monster have" has no other home.
        private static readonly Dictionary<string, string[]> Kits = new Dictionary<string, string[]>
        {
            ["beetle"] = new[] { "idle", "attack", "turtle_up", "shell_closed", "hurt", "defeated" },
            ["treant"] = new[] { "idle", "attack", "trunk_slam", "cast", "hurt", "defeated" },
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

        // A stance folder that fails to load is INVISIBLE at runtime -- the
        // stage falls through to its nameplate and nothing logs. The same
        // silent failure EnemySpriteImportPostprocessor exists to stop, checked
        // for the two kits that postprocessor has never seen before.
        [Test]
        public void EveryAuthoredStanceResolvesToRealFrames()
        {
            var missing = new List<string>();

            foreach (var (id, stances) in Kits.Select(k => (k.Key, k.Value)))
            {
                foreach (string stance in stances)
                {
                    var frames = Resources.LoadAll<Sprite>($"Enemies/{id}/{stance}");
                    if (frames == null || frames.Length == 0)
                    {
                        missing.Add($"Enemies/{id}/{stance}");
                    }
                }
            }

            Assert.IsEmpty(missing,
                "these stance folders load nothing, so the stage shows a nameplate instead of a monster: " +
                string.Join(", ", missing));
        }

        // ONE CANVAS PER ACTOR, which is what makes StanceManifest's single
        // authored ground line mean anything: the stage sizes each slot to the
        // sprite it is showing (FightController.StageVisuals), so frames of
        // different sizes move the figure by the difference. Both these kits
        // arrived cropped per frame and were re-delivered onto a shared canvas
        // by tools/pad_actor_frames.py -- this is that delivery asserted rather
        // than trusted.
        [Test]
        public void EveryFrameOfAnActorSharesOneCanvas()
        {
            foreach (var (id, stances) in Kits.Select(k => (k.Key, k.Value)))
            {
                var sizes = new Dictionary<string, Vector2>();

                foreach (string stance in stances)
                {
                    foreach (var frame in Resources.LoadAll<Sprite>($"Enemies/{id}/{stance}"))
                    {
                        sizes[$"{stance}/{frame.name}"] = frame.rect.size;
                    }
                }

                Assert.IsNotEmpty(sizes, $"{id} has no frames at all");

                var distinct = sizes.Values.Distinct().ToList();
                Assert.AreEqual(1, distinct.Count,
                    $"{id}'s frames span {distinct.Count} canvas sizes, so it changes size and position as it " +
                    $"animates: {string.Join(", ", sizes.Take(8).Select(p => $"{p.Key}={p.Value}"))}");
            }
        }

        // And the ground line has to be authored at all, or the figure stands
        // on its own canvas bottom -- which for a padded canvas is 12px of
        // nothing, and reads as hovering.
        [Test]
        public void BothKitsAreInTheStanceManifest()
        {
            foreach (string id in Kits.Keys)
            {
                Assert.IsTrue(StanceManifestLoader.Manifest.HasActor($"Enemies/{id}"),
                    $"Enemies/{id} has no StanceManifest entry, so it falls back to the canvas bottom");
            }
        }

        // ---- the whole roster, not just the two kits above --------------------

        // EVERY POSE A MONSTER CAN REACH, resolved the way the stage resolves
        // it. This is the question "are the animations actually in" asked of
        // content rather than of a folder listing, and it covers two failures
        // that look identical in play and have different causes:
        //
        //   - a stance folder that is missing or imported as a plain Texture,
        //     which FightController answers with its no-art nameplate;
        //   - a skill whose authored `stance` names a pose its caster does not
        //     have, which is a one-character typo in JSON that no build step
        //     reads and that only shows up as a monster VANISHING for exactly
        //     the turn it casts.
        //
        // The four the fight drives on its own (idle/attack/hurt/defeated) plus
        // whatever every ability it can draw asks for -- StanceFor's rule,
        // restated here because it is private and one line: the skill's own
        // stance if it authored one, "cast" otherwise.
        [Test]
        public void EveryStanceEveryEnemyCanReachResolvesToFrames()
        {
            var missing = new List<string>();
            int probed = 0;

            foreach (var enemy in ContentDatabase.Enemies)
            {
                // No art authored at all is a deliberate state -- the stage
                // shows a nameplate and says so. Only a monster that HAS a
                // sprite folder is promising anything.
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.spritePath)) continue;

                var wanted = new HashSet<string> { "idle", "attack", "hurt", "defeated" };

                foreach (var ability in enemy.abilities ?? System.Array.Empty<RawEnemyAbility>())
                {
                    if (ability == null || string.IsNullOrWhiteSpace(ability.skillId)) continue;

                    var skill = ContentDatabase.Skills.FirstOrDefault(s => s.id == ability.skillId);
                    Assert.IsNotNull(skill,
                        $"{enemy.id} draws on skill '{ability.skillId}', which is not in the catalogue");

                    wanted.Add(string.IsNullOrEmpty(skill.stance) ? "cast" : skill.stance);
                }

                // The legacy single-action trio poses as a cast.
                if (!string.IsNullOrWhiteSpace(enemy.skillName)) wanted.Add("cast");

                foreach (string stance in wanted)
                {
                    probed++;
                    if (StanceAnimationLibrary.Resolve(enemy.spritePath, stance).IsEmpty)
                    {
                        missing.Add($"{enemy.id}:{stance}");
                    }
                }
            }

            Assert.Greater(probed, 0, "no enemy has art, so this pin is vacuous");
            Assert.IsEmpty(missing,
                "these monsters can reach a pose they have no art for, and will show a nameplate " +
                "(or nothing) on the turn they do: " + string.Join(", ", missing));
        }

        // ---- the pose nobody was watching -------------------------------------

        // A CORPSE HAS TO FINISH FALLING. FightBeatPlayer steps frames for the
        // beat's actor only, which is right for a swing and wrong for a death:
        // the thing that dies is the TARGET, so a body held frame 0 of its
        // defeated pose and then faded.
        //
        // Every kit that predates the Beetle hid this, because their defeated
        // art opens already collapsed -- frame 0 is a body on the floor. The
        // Beetle's runs from standing through the flip onto its back, so it
        // died by standing still, and that is what a player reported.
        //
        // Sampled MID-FADE rather than after, for the reason FightController's
        // own FrameFor comment gives: everything ends back on frame 0 either
        // way, so the only place an animation is visible is while it runs.
        [UnityTest]
        public IEnumerator ADefeatedBodyStepsThroughItsOwnFrames()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight);

            var beetle = ContentDatabase.Enemies.FirstOrDefault(e => e.id == "beetle");
            Assert.IsNotNull(beetle, "beetle is not in the content database");

            int frames = StanceAnimationLibrary.Resolve(beetle.spritePath, "defeated").FrameCount;
            Assert.Greater(frames, 1,
                "the beetle's defeated pose is a single frame, so this pin proves nothing");

            // One swing kills it, and Shawn goes first.
            var hero = new CombatantState("Shawn", true, 300, 30, 400, 20);
            var doomed = new CombatantState(beetle.displayName, false, 1, 0, 1, 1);

            var encounter = new CombatEncounter(new[] { hero }, new[] { doomed });
            var session = new FightSession(encounter,
                new List<PlayerKit> { null },
                new List<EnemyKit> { new EnemyKit(FightEncounterAdapter.Resolve(beetle), false) },
                new Domain.Rng.SeededRandom(4));

            session.Begin();
            fight.Bind(session, EncounterClass.Normal);
            yield return null;

            // THROUGH THE BUTTONS, not through the session. The death
            // animation is driven from FadeTheFallen, which only runs while
            // FightBeatPlayer is playing a beat -- so calling
            // session.ExecuteAttack directly resolves the combat and skips the
            // entire presentation layer this test is about. That mistake is
            // worth a comment because the test still goes red when it is made,
            // and it goes red pointing at the code under test.
            Click(fight, "Verb0");
            Click(fight, "EnemyPlate0");

            // Watched rather than waited out: the highest frame the body ever
            // reaches is the assertion, and it is only true for an instant.
            int highest = 0;
            for (float t = 0f; t < 5f; t += Time.deltaTime)
            {
                highest = Mathf.Max(highest, fight.FrameFor(doomed));
                if (highest >= frames - 1) break;
                yield return null;
            }

            Assert.IsFalse(doomed.IsAlive, "fixture: the swing should have killed it");

            Assert.AreEqual(frames - 1, highest,
                $"the body never got past frame {highest} of {frames}. A defeated pose that does not " +
                "step is a death animation nobody sees -- and for a kit whose first frame is still " +
                "standing, it is a monster that dies by not moving.");
        }

        // THE SAME BUG ONE POSE OVER. "Only the beat's actor animates" was the
        // real rule, and it is wrong twice: for the body that dies (above) and
        // for the body that is merely hit. A six-frame flinch held frame 0 and
        // looked like a monster that had not noticed.
        //
        // Survives the blow deliberately -- a kill would route the pose to
        // defeated and test the other fix instead.
        [UnityTest]
        public IEnumerator AStruckBodyStepsThroughItsFlinch()
        {
            yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var fight = Object.FindAnyObjectByType<FightController>();
            Assert.IsNotNull(fight);

            var beetle = ContentDatabase.Enemies.FirstOrDefault(e => e.id == "beetle");
            Assert.IsNotNull(beetle);

            int frames = StanceAnimationLibrary.Resolve(beetle.spritePath, "hurt").FrameCount;
            Assert.Greater(frames, 1, "the beetle's hurt pose is a single frame, so this pin proves nothing");

            var hero = new CombatantState("Shawn", true, 300, 30, 20, 20);
            var struck = new CombatantState(beetle.displayName, false, 9000, 0, 1, 1);

            var encounter = new CombatEncounter(new[] { hero }, new[] { struck });
            var session = new FightSession(encounter,
                new List<PlayerKit> { null },
                new List<EnemyKit> { new EnemyKit(FightEncounterAdapter.Resolve(beetle), false) },
                new Domain.Rng.SeededRandom(4));

            session.Begin();
            fight.Bind(session, EncounterClass.Normal);
            yield return null;

            Click(fight, "Verb0");
            Click(fight, "EnemyPlate0");

            int highest = 0;
            for (float t = 0f; t < 5f; t += Time.deltaTime)
            {
                highest = Mathf.Max(highest, fight.FrameFor(struck));
                if (highest >= frames - 1) break;
                yield return null;
            }

            Assert.IsTrue(struck.IsAlive, "fixture: it was supposed to survive and flinch, not die");
            Assert.AreEqual(frames - 1, highest,
                $"the struck body never got past frame {highest} of {frames} -- it takes the hit " +
                "without moving.");
        }

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
            var one = new CombatantState(beetle.displayName, false, 5000, 0, 4, 5);
            var two = new CombatantState(treant.displayName, false, 5000, 0, 6, 3);

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
            // the shadows.
            //
            // NOT long enough for the idle to have moved, because nothing
            // animates an idle: no code path steps frames for a stance the
            // beat player is not driving, so both kits' six-frame breathing
            // sits on frame 0 forever. This comment used to claim the wait was
            // for exactly that, which was wishful. When an idle loop exists,
            // this is where it would start being worth waiting for.
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
