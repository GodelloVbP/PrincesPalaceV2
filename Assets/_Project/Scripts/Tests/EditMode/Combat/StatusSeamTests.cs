using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace PrincesPalace.Domain.Tests
{
    // ONE PLACE APPLIES A STATUS DURING A FIGHT, and this is the lint that
    // keeps it that way (plan D6).
    //
    // StatusEffects.Apply is pure Domain and cannot reach anything a status
    // needs bookkeeping for. Chilled is the standing proof: the speed malus
    // lives in FightSession.SpeedBuffs' dictionary, ApplyChilled is the only
    // path that registers it, and FightSession.Skills.ApplySkillStatus called
    // StatusEffects.Apply directly -- so the first content row to author
    // `appliesStatus: Chilled` would have put a badge up and slowed nobody.
    // Winter's Rebuke is that row.
    //
    // A TIER-3 CHECK, and it says so. The tier-1 version would be a type that
    // cannot be constructed outside the session, which Domain's layering does
    // not allow for a static helper every test also calls. So: a source scan,
    // scoped by judgement, with a vacuity guard, exactly as
    // CODE_STANDARDS section 9 describes.
    public class StatusSeamTests
    {
        private const int MinimumFilesExpected = 40;

        // Where a direct StatusEffects.Apply call is legitimate, and why.
        //
        //   StatusEffects.cs       -- it is the definition.
        //   FightSession.Riders.cs -- it is the seam (RecordStatus).
        //   Fear.cs, Marks.cs      -- Domain facilities that are themselves the
        //                             one entry point for their own status, and
        //                             whose statuses carry no session-side
        //                             bookkeeping. Each has its own header
        //                             making that argument; a session path
        //                             reaches them through Fear.Apply and
        //                             Marks.Apply rather than through
        //                             StatusEffects.Apply.
        private static readonly string[] AllowedFiles =
        {
            "StatusEffects.cs", "FightSession.Riders.cs", "Fear.cs", "Marks.cs",
        };

        [Test]
        public void NoProductionCallerAppliesAStatusOutsideTheSeam()
        {
            var files = ProductionFiles();
            Assert.GreaterOrEqual(files.Count, MinimumFilesExpected,
                "vacuity guard: the scan found almost no production files, so it proved nothing");

            var strays = new List<string>();
            int callSites = 0;

            foreach (string path in files)
            {
                string name = Path.GetFileName(path);
                foreach (string line in File.ReadAllLines(path))
                {
                    if (!line.Contains("StatusEffects.Apply(")) continue;
                    if (line.TrimStart().StartsWith("//")) continue;

                    callSites++;
                    if (!AllowedFiles.Contains(name)) strays.Add($"{name}: {line.Trim()}");
                }
            }

            Assert.GreaterOrEqual(callSites, 2,
                "vacuity guard: the scan matched almost nothing, so the rule below is unproven");

            CollectionAssert.IsEmpty(strays,
                "a status applied outside FightSession.ApplyStatusTo skips whatever bookkeeping that status "
                + "owns -- Chilled's speed malus today, the next one tomorrow. Call ApplyStatusTo instead, "
                + "or argue for a new entry in AllowedFiles in the commit message");
        }

        // AND THE SEAM REACHES THE TURN-END EXEMPTION. A status applied through
        // it has to land in _statusesAppliedThisTurn, or every AtTurnEnd status
        // loses the turn it was applied during -- the exact off-by-one plan D1
        // exists to close.
        [Test]
        public void TheSeamRecordsWhatItApplied_SoTheTurnItLandedOnDoesNotCount()
        {
            string source = File.ReadAllText(
                ProductionFiles().Single(p => Path.GetFileName(p) == "FightSession.Riders.cs"));

            Assert.IsTrue(source.Contains("_statusesAppliedThisTurn.Add(applied)"),
                "RecordStatus must add the entry it applied to the turn-end exemption set");
        }

        private static List<string> ProductionFiles()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate Assets/_Project/Scripts from the working directory.");

            return Directory
                .GetFiles(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts"), "*.cs",
                    SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').Contains("/Tests/"))
                .ToList();
        }
    }
}
