using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace PrincesPalace.Domain.Tests
{
    // A test that flips a global must put it back.
    //
    // Tier 3 of the same ladder UiKitLintTests states -- T1 is "the API cannot
    // express the mistake", T2 is "one code path owns the concern", T3 is
    // mechanised discipline. This is T3 because the other two are not available
    // here: the globals are public static fields on production types that the
    // game itself writes, so no type can refuse the assignment, and NUnit has
    // no per-test hook that can safely own the concern (see TestGlobals'
    // header for why an automatic reset is the wrong shape -- it would land
    // after a fixture's [SetUp] and null the throwaway save root out from under
    // it).
    //
    // What makes it worth mechanising rather than writing down: the discipline
    // is currently PERFECT -- this rule found zero offenders the day it was
    // written. That is the argument for it, not against. A rule added while the
    // code already obeys it costs nothing and never has to be paid off; a rule
    // written down in a document and enforced by memory is the one that decays,
    // and the 383 PlayMode tests share one process, so the first forgotten
    // teardown is silent and lands on somebody else's test.
    //
    // SCANS TESTS, not production -- the mirror image of UiKitLintTests, and
    // for the same reason it says: scan the code the rule is actually about.
    public class GlobalStateLintTests
    {
        private const int MinimumTestFilesExpected = 100;

        // Every global a test can flip, with the write that arms it and the
        // restore that disarms it. Keep in step with TestGlobals -- adding one
        // here without adding its reset there leaves a global nothing cleans.
        private static readonly (string Name, string Write, string Restore)[] Globals =
        {
            ("SaveSystem.RootOverride",
                @"SaveSystem\.RootOverride\s*=",
                @"SaveSystem\.RootOverride\s*=\s*null"),

            ("Navigation.LoadOverride",
                @"Navigation\.LoadOverride\s*=",
                @"Navigation\.Reset\(\)|Navigation\.LoadOverride\s*=\s*null"),

            ("Navigation.QuitOverride",
                @"Navigation\.QuitOverride\s*=",
                @"Navigation\.Reset\(\)|Navigation\.QuitOverride\s*=\s*null"),

            ("FightBeatPlayer.BeatSpeedMultiplier",
                @"BeatSpeedMultiplier\s*=",
                @"BeatSpeedMultiplier\s*=\s*1f"),

            ("FightBeatPlayer.PlayerSpeedSource",
                @"PlayerSpeedSource\s*=",
                @"PlayerSpeedSource\s*=\s*\(\s*\)\s*=>\s*1f"),

            // The adopted factor has no direct assignment a test could write
            // (docs/PLAN_BATTLE_SPEED.md contract 10 -- AdoptPlayerSpeed and
            // its ForTest twin are the only writers), so the WRITE this
            // watches is the call that pushes a just-changed source into it.
            // The restore it demands is the sanctioned one: TestGlobals.
            // ResetAll, which re-pins the source AND re-adopts in the same
            // two lines -- there is no OTHER honest way to put this back.
            ("FightBeatPlayer.PlayerSpeedMultiplier",
                @"AdoptPlayerSpeedForTest\s*\(\)",
                @"TestGlobals\.ResetAll\(\)"),

            ("ReckoningController.SpeedMultiplier",
                @"ReckoningController\.SpeedMultiplier\s*=",
                @"ReckoningController\.SpeedMultiplier\s*=\s*1f"),

            ("RewardTrackController.SpeedMultiplier",
                @"RewardTrackController\.SpeedMultiplier\s*=",
                @"RewardTrackController\.SpeedMultiplier\s*=\s*1f"),

            ("TalentController.MotionSpeedMultiplier",
                @"TalentController\.MotionSpeedMultiplier\s*=",
                @"TalentController\.MotionSpeedMultiplier\s*=\s*1f"),

            ("HubController.MotionSpeedMultiplier",
                @"HubController\.MotionSpeedMultiplier\s*=",
                @"HubController\.MotionSpeedMultiplier\s*=\s*1f"),

            ("FightController.BreathSpeedMultiplier",
                @"BreathSpeedMultiplier\s*=",
                @"BreathSpeedMultiplier\s*=\s*1f"),

            ("RequirementCurve.Percent",
                @"RequirementCurve\.Percent\s*=",
                @"RequirementCurve\.Percent\s*=\s*RequirementCurve\.DefaultPercent"),

            // Written by the menu rather than by the test, usually -- but a
            // fixture that opens the menu has stopped the clock just as surely
            // as one that assigns it, and the restore is the same line either
            // way.
            ("Time.timeScale",
                @"Time\.timeScale\s*=",
                @"Time\.timeScale\s*=\s*1f"),

            // A spell effect's whole scaled lifetime is 13ms at test speed, so
            // a test that wants to see one on screen holds this rather than
            // racing a frame against it. Left held, every later test in the
            // process gets a spell that never finishes.
            ("SpellPerformancePlayer.ClockOverride",
                @"SpellPerformancePlayer\.ClockOverride\s*=",
                @"SpellPerformancePlayer\.ClockOverride\s*=\s*null"),

            ("RequirementCurve.GearRequirementsEnabled",
                @"GearRequirementsEnabled\s*=",
                @"GearRequirementsEnabled\s*=\s*false"),
        };

        private static List<string> TestFiles()
        {
            // Unity's batch-mode CWD is the project root. Walk up anyway, so
            // this does not silently depend on that -- same as UiKitLintTests.
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts", "Tests")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate Assets/_Project/Scripts/Tests from the working directory.");

            var files = Directory
                .GetFiles(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts", "Tests"), "*.cs",
                    SearchOption.AllDirectories)
                .ToList();

            // A lint that scans nothing passes everything.
            Assert.Greater(files.Count, MinimumTestFilesExpected,
                $"Only {files.Count} test files found - the scan is not seeing the suite, " +
                $"so every rule in this class would vacuously pass.");

            return files;
        }

        [Test]
        public void AFileThatFlipsAGlobal_AlsoPutsItBack()
        {
            var offenders = new List<string>();

            foreach (string file in TestFiles())
            {
                // TestGlobals IS the restore, so it trivially satisfies every
                // rule. FightSceneFixture pins PlayerSpeedSource to its own
                // shipped default and adopts it -- ESTABLISHING the known-good
                // state every battle-speed timing fixture loads through,
                // never flipping one away from default -- so it has nothing
                // to pair with a restore any more than TestGlobals does.
                // Both named here so a reader does not wonder whether either
                // exemption was an oversight.
                if (Path.GetFileName(file) == "TestGlobals.cs" || Path.GetFileName(file) == "FightSceneFixture.cs") continue;

                string source = File.ReadAllText(file);

                foreach (var (name, write, restore) in Globals)
                {
                    if (!Regex.IsMatch(source, write)) continue;
                    if (Regex.IsMatch(source, restore)) continue;
                    if (source.Contains("TestGlobals.ResetAll()")) continue;

                    offenders.Add(
                        $"{Path.GetFileName(file)} writes {name} and never restores it - " +
                        $"add the restore to [TearDown], or call TestGlobals.ResetAll()");
                }
            }

            CollectionAssert.IsEmpty(offenders,
                "a global left flipped outlives the test that flipped it for the whole process -- " +
                string.Join("; ", offenders));
        }

        [Test]
        public void EveryRuleInTheTable_StillMatchesSomething()
        {
            // The vacuity guard the rule above cannot provide for itself. A
            // regex that matches nothing passes silently, so a typo in one
            // entry would retire that global's protection without any sign.
            //
            // A failure here has exactly two honest fixes and the message says
            // both: the pattern is broken, or the global is genuinely no longer
            // written by any test and the rule should be DELETED along with its
            // reset in TestGlobals. Neither of them is "leave it".
            var sources = TestFiles().Select(File.ReadAllText).ToList();
            var dead = Globals
                .Where(g => !sources.Any(s => Regex.IsMatch(s, g.Write)))
                .Select(g => g.Name)
                .ToList();

            CollectionAssert.IsEmpty(dead,
                "these rules match no test file at all, so they protect nothing - fix the pattern, " +
                "or delete the rule and its reset in TestGlobals: " + string.Join(", ", dead));
        }
    }
}
