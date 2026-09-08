using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace PrincesPalace.Domain.Tests
{
    // Source-level enforcement of the rules the type system cannot state.
    //
    // Tier 3 of the plan's three tiers: T1 is "the API cannot express the
    // mistake", T2 is "one code path owns the concern", and this is T3 --
    // mechanised discipline. It exists because v1 proved that a helper you are
    // ENTITLED to bypass gets bypassed: NewUiRect reached 13 of 86 sites, and
    // nobody was being careless.
    //
    // Scans production code only. Tests legitimately use string literals as
    // fixtures, and linting them would just teach people to suppress the lint.
    public class UiKitLintTests
    {
        private const int MinimumFilesExpected = 40;

        private static string ScriptsRoot()
        {
            // Unity's batch-mode CWD is the project root. Walk up regardless, so
            // the test does not silently depend on that.
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate Assets/_Project/Scripts from the working directory.");
            return Path.Combine(dir.FullName, "Assets", "_Project", "Scripts");
        }

        private static List<string> ProductionFiles()
        {
            var root = ScriptsRoot();
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').Contains("/Tests/"))
                .ToList();

            // A lint that scans nothing passes everything. This is the guard
            // against a path change quietly turning every rule below into a
            // no-op that still reports green.
            Assert.Greater(files.Count, MinimumFilesExpected,
                $"Only {files.Count} production files found under {root} - the scan is not seeing the project, " +
                $"so every rule in this class would vacuously pass.");
            return files;
        }

        private static IEnumerable<(string File, int Line, string Text)> Matches(string pattern)
        {
            var rx = new Regex(pattern, RegexOptions.Compiled);
            foreach (var file in ProductionFiles())
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    // Skip comments - the rules are about code, and the reasons
                    // for the rules are written in prose right next to it.
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;
                    if (rx.IsMatch(line)) yield return (file, i + 1, line.Trim());
                }
            }
        }

        private static string Describe(IEnumerable<(string File, int Line, string Text)> hits) =>
            string.Join("\n", hits.Select(h => $"  {Path.GetFileName(h.File)}:{h.Line}  {h.Text}"));

        [Test]
        public void OnlyTheEmitterMayCreateGameObjects()
        {
            // SceneBuilder.cs is allowed the scene's ROOT FIXTURES - camera,
            // canvas, EventSystem - which are not UI widgets and have no rect
            // preamble. It is bounded below so that allowance cannot quietly
            // become "and also a few widgets".
            // CanvasCapture creates a temporary offscreen camera to render a
            // Canvas to PNG. Not a UI widget, no rect preamble, and it is
            // deleted in the same call that made it.
            // SoundController creates ONE persistent, hidden AudioSource host on
            // first use. Not a UI widget, no rect preamble, no parent in any
            // screen tree -- it is a service that has to outlive every scene, and
            // the alternative is an audio object hand-placed in each of them.
            // CursorController is the same shape and the same argument: one
            // hidden host, bootstrapped before the first scene, swapping the OS
            // cursor texture. It draws nothing inside any screen tree, so there
            // is no rect for the preamble this rule protects to apply to.
            // PlaytimeTracker is the same shape again: one hidden host,
            // bootstrapped before the first scene, ticking a save's playtime
            // total. No rect, no screen tree, nothing this rule protects.
            string[] allowed =
            {
                "UiEmitter.cs", "ScreenshotTool.cs", "SceneBuilder.cs", "CanvasCapture.cs",
                "SoundController.cs", "CursorController.cs", "PlaytimeTracker.cs",
            };

            var offenders = Matches(@"new\s+GameObject\s*\(")
                .Where(h => !allowed.Contains(Path.GetFileName(h.File)))
                .ToList();

            Assert.IsEmpty(offenders,
                "The rect preamble exists exactly once, in UiEmitter. v1 had 86 hand-rolled construction sites and a " +
                "helper that covered 13 of them.\n" + Describe(offenders));
        }

        [Test]
        public void SceneBuilderCreatesOnlyTheSceneRootFixtures()
        {
            var sites = Matches(@"new\s+GameObject\s*\(")
                .Where(h => Path.GetFileName(h.File) == "SceneBuilder.cs")
                .ToList();

            // Camera, GlobalVolume, Canvas, EventSystem. Raised from 3 when the
            // post-processing Volume was added - deliberately a NUMBER that has
            // to be edited, so growing the allowance is a visible decision
            // rather than something that drifts.
            Assert.LessOrEqual(sites.Count, 4,
                "SceneBuilder may create the camera, the global Volume, the canvas and the EventSystem, and nothing " +
                "else - every widget goes through UiEmitter.\n" + Describe(sites));
        }

        [Test]
        public void TheStringlyTypedSetFieldIdiom_IsNotReImported()
        {
            var offenders = Matches(@"\bSetField\s*\(\s*\w+\s*,\s*""").ToList();

            Assert.IsEmpty(offenders,
                "Wiring is direct field assignment now, not reflection over a field NAME. A typo in that string " +
                "produced no compile error and no runtime error in v1 - the field just stayed null.\n" + Describe(offenders));
        }

        [Test]
        public void LabelTextIsOnlyAssignedThroughUiText()
        {
            string[] allowed = { "UiText.cs", "UiEmitter.cs" };

            var offenders = Matches(@"\.text\s*=")
                .Where(h => !allowed.Contains(Path.GetFileName(h.File)))
                .ToList();

            Assert.IsEmpty(offenders,
                "Controllers set text via label.Set(UiStrings.X, args) so builder and runtime share one template. " +
                "Assigning .text directly is how v1's four drift pairs happened.\n" + Describe(offenders));
        }

        [Test]
        public void FromContent_IsNotUsedToSmuggleALiteral()
        {
            // FromContent exists for data (a character name out of JSON). An
            // inline literal there is authored UI copy dodging the manifest.
            var offenders = Matches(@"FromContent\s*\(\s*""").ToList();

            Assert.IsEmpty(offenders,
                "UiString.FromContent is for values that come from content data. Authored copy belongs in " +
                "UiStrings.\n" + Describe(offenders));
        }

        [Test]
        public void LegacyUnityUiTextComponent_IsNotUsed()
        {
            // v2 is TMP from day one. A single legacy Text would create a second
            // text type for call sites to pick wrongly, and UiText only extends
            // TMP_Text.
            var offenders = Matches(@"UnityEngine\.UI\.Text\b|<\s*Text\s*>|\bText\s+\w+\s*;")
                .Where(h => !Path.GetFileName(h.File).Equals("UiText.cs", StringComparison.Ordinal))
                .ToList();

            Assert.IsEmpty(offenders,
                "v2 uses TMP_Text exclusively.\n" + Describe(offenders));
        }

        [Test]
        public void TheLintItselfIsNotVacuous()
        {
            // Proves the machinery finds real code, so a green run above means
            // "no offenders" rather than "no files".
            var anyClass = Matches(@"\bclass\b").ToList();
            Assert.Greater(anyClass.Count, MinimumFilesExpected,
                "The scanner found production files but almost no code in them - the rules above would be vacuous.");
        }
        // ---- one token, one home ------------------------------------------------
        //
        // FightHudPalette is where a colour lives. A screen that restates one of
        // its values as its own literal is a second copy that nothing keeps in
        // step -- and this is not hypothetical: the system menu's four panes had
        // 22 of them between them, with #C8AAE638 appearing under FIVE different
        // local names. The design pass had already fixed exactly this once
        // ("the two hardcoded values are replaced"), and it grew straight back
        // the moment four screens were written in a row.
        //
        // A local NAME is fine and often better at the call site -- CardRim says
        // what it is for where Hairline says what it looks like. What is refused
        // is a local VALUE:
        //
        //     private const string CardRim = "#C8AAE638";        // no
        //     private const string CardRim = FightHudPalette.Hairline;   // yes
        [Test]
        public void NoScreenRestatesAColourThePaletteAlreadyOwns()
        {
            var palette = new Dictionary<string, string>();
            string paletteSource = File.ReadAllText(Path.Combine(ScriptsRoot(),
                "Domain", "UiKit", "FightHudPalette.cs"));

            foreach (Match m in Regex.Matches(paletteSource,
                         @"public const string (\w+)\s*=\s*""(#[0-9A-Fa-f]+)"""))
            {
                palette[m.Groups[2].Value.ToUpperInvariant()] = m.Groups[1].Value;
            }

            // A lint that scans nothing passes everything.
            Assert.Greater(palette.Count, 30,
                $"only {palette.Count} palette entries were parsed, so this rule is vacuous");

            var offenders = new List<string>();

            // SCOPED TO Domain/UiKit, where FightHudPalette is unambiguously
            // the authority and where a colour is a UiNode's colour.
            //
            // Narrowed after this rule found ItemStatLines.HeadingHex, which
            // holds the same hex as BackRowText and is NOT the same token: it
            // tints "VS. EQUIPPED" in an item tooltip, where BackRowText names
            // a combatant standing at the rear. Aliasing it would have made the
            // code say something untrue to satisfy a lint. That file owns a
            // coherent trio of its own -- gain, loss, heading -- as rich-text
            // tags inside a string rather than as node colours, which is a
            // different medium with a different palette.
            //
            // Said plainly because narrowing a rule to make it pass is exactly
            // how a rule stops meaning anything: the judgement here is that the
            // match was a coincidence of VALUE, not of ROLE.
            var screens = ProductionFiles()
                .Where(f => f.Replace(Path.DirectorySeparatorChar, '/')
                             .Contains("/Domain/UiKit/"))
                .ToList();

            Assert.Greater(screens.Count, 20,
                $"only {screens.Count} UiKit files were scanned, so this rule is vacuous");

            foreach (string file in screens)
            {
                if (Path.GetFileName(file) == "FightHudPalette.cs") continue;

                foreach (Match m in Regex.Matches(File.ReadAllText(file),
                             @"const string (\w+)\s*=\s*""(#[0-9A-Fa-f]+)"""))
                {
                    string hex = m.Groups[2].Value.ToUpperInvariant();
                    if (!palette.TryGetValue(hex, out string owner)) continue;

                    offenders.Add(
                        $"{Path.GetFileName(file)}: {m.Groups[1].Value} = \"{m.Groups[2].Value}\" " +
                        $"is FightHudPalette.{owner} - write `= FightHudPalette.{owner};` instead");
                }
            }

            CollectionAssert.IsEmpty(offenders,
                "these restate a colour the palette already owns -- " + string.Join("; ", offenders));
        }

        // ---- T9, docs/PLAN_BATTLE_SPEED.md: the battle-speed seam --------------
        //
        // Secondary guard only -- contract 10 ("Writers and readers") is
        // structural (the seam's own writer is the only place that assigns
        // it), so this exists as a backstop the way every rule in this file
        // is a backstop for something the type system does not enforce, not
        // because contract 10 is expected to be violated.
        [Test]
        public void BattleSpeedSeamsStayInsideTheFightFiles()
        {
            string[] allowed = { "FightBeatPlayer.cs", "FightBootstrap.cs", "SpellPerformancePlayer.cs" };

            var offenders = Matches(@"\bPlayerSpeedMultiplier\b|\bPlayerSpeedSource\b")
                .Where(h => !allowed.Contains(Path.GetFileName(h.File)))
                .ToList();

            Assert.IsEmpty(offenders,
                "PlayerSpeedMultiplier/PlayerSpeedSource belong to FightBeatPlayer/FightBootstrap/" +
                "SpellPerformancePlayer only (docs/PLAN_BATTLE_SPEED.md contract 10).\n" + Describe(offenders));
        }

        // The two HUB motion multipliers (TalentController, HubController --
        // as opposed to ReckoningController/RewardTrackController, both
        // reachable from inside the fight's own end-of-battle flow) have no
        // business appearing in a Fight file: a battle-speed row that read
        // one of those by mistake would silently change what "1x" means.
        [Test]
        public void TheHubMotionMultipliersStayOutOfEveryFightFile()
        {
            var fightFiles = ProductionFiles()
                .Where(f => Path.GetFileName(f).StartsWith("Fight", StringComparison.Ordinal))
                .ToList();

            Assert.Greater(fightFiles.Count, 10,
                $"only {fightFiles.Count} Fight*.cs files were scanned, so this rule would be vacuous");

            var rx = new Regex(@"TalentController\.MotionSpeedMultiplier|HubController\.MotionSpeedMultiplier",
                RegexOptions.Compiled);
            var offenders = new List<(string File, int Line, string Text)>();

            foreach (string file in fightFiles)
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) continue;
                    if (rx.IsMatch(lines[i])) offenders.Add((file, i + 1, lines[i].Trim()));
                }
            }

            CollectionAssert.IsEmpty(offenders,
                "a hub motion multiplier reached a Fight file -- " + Describe(offenders));
        }
    }
}
