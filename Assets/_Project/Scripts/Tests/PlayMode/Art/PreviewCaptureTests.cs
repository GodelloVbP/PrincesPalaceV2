using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.PlayModeTests
{
    // THE PICTURES tools/preview.ps1 -Enemy <id> writes, and nothing else.
    //
    // [Explicit], so it never runs as part of a slice or the commit gate --
    // NUnit will not pick an explicit fixture up unless something names it,
    // and preview.ps1 names it through graphics_tests.ps1 -Filter. Two reasons
    // it has to be kept apart, and they pull the same way:
    //
    //   THIS IS THE ONLY CODE IN THE REPO THAT READS PP_PREVIEW_IDS. A
    //   verification fixture that read an environment variable would test a
    //   different thing depending on the shell it was started from, which is
    //   the one property a gate cannot have. PreviewEnvironmentLintTests
    //   asserts that exactly one test file mentions the name.
    //
    //   Screenshots are an addition, not the gate. What actually protects new
    //   monster art on a machine with no GPU is EnemyArtCompletenessTests,
    //   headless, over every enemy. This is here so a human can look.
    //
    // Two kinds of picture per id:
    //
    //   <id>_stances.png       -- a contact sheet of every pose the mob can
    //                             reach, composited from the sprites
    //                             themselves. Needs no graphics device: it is
    //                             pixels copied, not a scene rendered.
    //   <id>_turn<n>_<ability>.png
    //                          -- the mob on the real stage, one frame per turn
    //                             of its showcase, named for what it did. This
    //                             is the half that needs a device.
    public class PreviewCaptureTests
    {
        // NAMED ONCE. The lint test finds this file by looking for the literal
        // below, so it lives here and in the two scripts that set it, and
        // nowhere else.
        private const string IdsVariable = "PP_PREVIEW_IDS";

        private static string OutputDir =>
            Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "tools", "screenshots", "preview"));

        private static string[] RequestedIds()
        {
            string raw = System.Environment.GetEnvironmentVariable(IdsVariable) ?? "";
            return raw.Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
        }

        [SetUp]
        public void PlayBeatsFast() => FightBeatPlayer.BeatSpeedMultiplier = 8f;

        [TearDown]
        public void Restore()
        {
            FightBeatPlayer.BeatSpeedMultiplier = 1f;
            StanceManifestLoader.Reset();
        }

        // The contact sheet. Deliberately built from the sprite pixels rather
        // than by posing the mob on the stage six times: every stance appears
        // at its authored size, side by side, which is the arrangement that
        // makes a mis-registered drawing obvious at a glance -- and it is the
        // one shot that can be produced on a machine that cannot render.
        [Test, Explicit("Written by tools/preview.ps1 -Enemy <id>.")]
        public void CaptureStanceSheets()
        {
            var ids = RequestedIds();
            Assert.IsNotEmpty(ids, $"{IdsVariable} is empty; run this through tools/preview.ps1 -Enemy <id>.");

            Directory.CreateDirectory(OutputDir);

            foreach (string id in ids)
            {
                var enemy = ContentDatabase.Enemies.FirstOrDefault(e => e != null && e.id == id);
                Assert.IsNotNull(enemy, $"'{id}' is not in the content database -- rebuild content first.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(enemy.data.SpritePath),
                    $"'{id}' has no spritePath, so there is nothing to photograph.");

                var stances = EnemyArtCompletenessTests.StancesReachableBy(enemy).OrderBy(s => s).ToList();
                var sprites = new List<(string Stance, Sprite Sprite)>();

                foreach (string stance in stances)
                {
                    var sprite = Resources.Load<Sprite>($"{enemy.data.SpritePath}/{stance}");
                    if (sprite != null) sprites.Add((stance, sprite));
                }

                Assert.IsNotEmpty(sprites, $"'{id}' resolved none of its stances: {string.Join(", ", stances)}");

                string path = Path.Combine(OutputDir, $"{id}_stances.png");
                WriteContactSheet(sprites, path);
                Debug.Log($"[PreviewCapture] wrote {path} ({sprites.Count} stances: " +
                          string.Join(", ", sprites.Select(s => s.Stance)) + ")");
            }
        }

        // One frame per turn of the showcase, on the real stage, through the
        // same buttons a player presses -- so what is photographed is the
        // fight, not a fixture wearing the right sprite folder.
        [UnityTest, Explicit("Written by tools/preview.ps1 -Enemy <id>.")]
        public IEnumerator CaptureShowcaseTurns()
        {
            var ids = RequestedIds();
            Assert.IsNotEmpty(ids, $"{IdsVariable} is empty; run this through tools/preview.ps1 -Enemy <id>.");

            if (!CanvasCapture.IsSupported)
            {
                Assert.Ignore("No graphics device. tools/preview.ps1 runs this through graphics_tests.ps1, " +
                              "which omits -nographics for exactly this reason.");
            }

            Directory.CreateDirectory(OutputDir);

            foreach (string id in ids)
            {
                yield return SceneManager.LoadSceneAsync("Fight", LoadSceneMode.Single);
                yield return null;
                yield return null;

                var fight = Object.FindAnyObjectByType<FightController>();
                Assert.IsNotNull(fight, "the fight scene has no controller");

                var hero = ContentDatabase.Characters.FirstOrDefault(
                    c => c != null && !string.IsNullOrWhiteSpace(c.data.BattleSpritePath))
                    ?? ContentDatabase.Characters.FirstOrDefault();
                Assert.IsNotNull(hero, "no characters in content");

                // The SAME fight -Launch shows: FightBootstrap's placeholder,
                // its fixed seed, one copy of the mob. A different construction
                // here would make the photograph evidence about something the
                // author cannot reproduce by pressing play.
                var built = FightEncounterAdapter.Build(
                    new List<string> { hero.id },
                    new List<string> { id },
                    new Domain.Rng.SeededRandom(20260810),
                    relicIds: null,
                    depthStep: 0);

                Assert.IsNotNull(built?.Session, $"'{id}' could not be built into an encounter");

                var showcase = new EnemyShowcase(Debug.Log);
                built.Session.Showcase = showcase;
                built.Session.Begin();
                fight.Bind(built.Session, EncounterClass.Normal);

                yield return new WaitForSecondsRealtime(1.2f);

                var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)
                    .FirstOrDefault(c => c.isRootCanvas);
                Assert.IsNotNull(canvas);

                int turns = EnemyShowcase.ScriptLength(SourcePoolFor(built.Session));

                for (int turn = 1; turn <= turns; turn++)
                {
                    // One swing, driven through the button a player would press
                    // -- the same seam EnemyFightableTests uses.
                    var attack = fight.GetComponentsInChildren<Button>(includeInactive: true)
                        .FirstOrDefault(b => b.name == "Verb0");
                    if (attack == null) break;
                    attack.onClick.Invoke();
                    yield return null;

                    var target = fight.GetComponentsInChildren<Button>(includeInactive: true)
                        .FirstOrDefault(b => b.name == "EnemyPlate0" && b.gameObject.activeInHierarchy);
                    if (target != null) target.onClick.Invoke();

                    float deadline = Time.realtimeSinceStartup + 12f;
                    while (fight.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;

                    // NAMED FOR WHAT THE MOB ACTUALLY DID, read back off the
                    // showcase rather than guessed from the authored order --
                    // an entry whose prerequisite was unmet was skipped, and a
                    // filename claiming otherwise would be the exact lie the
                    // skip-and-report rule exists to prevent.
                    string label = showcase.Played.Count >= turn ? showcase.Played[turn - 1] : "unknown";
                    string path = Path.Combine(OutputDir, $"{id}_turn{turn}_{Slug(label)}.png");
                    CanvasCapture.RenderToFile(canvas, path);
                    Debug.Log($"[PreviewCapture] wrote {path}");

                    if (built.Session.IsOver) break;
                }

                if (showcase.Skipped.Count > 0)
                {
                    Debug.LogWarning($"[PreviewCapture] '{id}' could not show: " +
                                     string.Join(", ", showcase.Skipped));
                }
            }
        }

        // The pool the showcase is walking, so the turn count is the kit's
        // length rather than a number picked here.
        private static IReadOnlyList<EnemyAbility> SourcePoolFor(FightSession session)
        {
            var enemy = session.Encounter.LivingEnemies.FirstOrDefault();
            return enemy == null ? null : session.SourceFor(enemy)?.Abilities;
        }

        private static string Slug(string label)
        {
            var kept = label.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray();
            return new string(kept).Trim('_');
        }

        // Sprites laid out left to right on one strip, each on the actor's own
        // canvas, on a flat dark ground so an alpha edge is visible.
        private static void WriteContactSheet(List<(string Stance, Sprite Sprite)> sprites, string path)
        {
            const int Gap = 8;
            int cellWidth = sprites.Max(s => (int)s.Sprite.rect.width);
            int cellHeight = sprites.Max(s => (int)s.Sprite.rect.height);
            int width = sprites.Count * cellWidth + (sprites.Count + 1) * Gap;
            int height = cellHeight + 2 * Gap;

            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var ground = new Color(0.06f, 0.06f, 0.08f, 1f);
            var background = Enumerable.Repeat(ground, width * height).ToArray();
            sheet.SetPixels(background);

            for (int i = 0; i < sprites.Count; i++)
            {
                var sprite = sprites[i].Sprite;
                var rect = sprite.rect;

                Color[] pixels;
                try
                {
                    pixels = sprite.texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
                }
                catch (UnityException)
                {
                    // isReadable is set by StanceSpriteImporter on import, so
                    // this is a texture that came in some other way. Leaving
                    // its cell as flat ground is more useful than failing the
                    // whole sheet -- the gap in the strip IS the report.
                    continue;
                }

                int x = Gap + i * (cellWidth + Gap);
                int y = Gap;
                for (int py = 0; py < rect.height; py++)
                {
                    for (int px = 0; px < rect.width; px++)
                    {
                        var source = pixels[py * (int)rect.width + px];
                        if (source.a <= 0f) continue;
                        sheet.SetPixel(x + px, y + py, Color.Lerp(ground, source, source.a));
                    }
                }
            }

            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }
    }
}
