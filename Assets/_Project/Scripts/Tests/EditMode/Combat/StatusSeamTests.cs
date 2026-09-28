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
    // docs/CODE_STANDARDS.md "Comments" describes.
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
        // it to the combatant whose turn it is has to be spared that turn's
        // end, or every AtTurnEnd status loses the turn it was applied during
        // -- the exact off-by-one plan D1 exists to close. Behavioural rather
        // than the source scan it replaced: the scan pinned a line naming the
        // old set, and WHO emptied that set was the defect.
        [Test]
        public void TheSeamSparesWhatItAppliedOnTheWearersOwnTurn_AndNothingElse()
        {
            var hero = new Combat.CombatantState("Hero", true, 100, 10, 10, 50);
            var foe = new Combat.CombatantState("Foe", false, 100, 10, 10, 1);
            var session = new Combat.Session.FightSession(
                new Combat.CombatEncounter(new[] { hero }, new[] { foe }), null, null, new Rng.SeededRandom(1));
            Assert.AreSame(hero, session.Current, "fixture: the hero holds the turn");

            session.ApplyStatusToForTest(hero, Combat.StatusEffectType.Vulnerable, 25, 1, hero);
            session.ApplyStatusToForTest(foe, Combat.StatusEffectType.Vulnerable, 25, 1, hero);

            session.TickStatusesAtTurnEndForTest(hero);
            session.TickStatusesAtTurnEndForTest(foe);

            Assert.AreEqual(1, hero.Statuses.Count,
                "applied on the wearer's own turn, so that turn's end must not count");
            Assert.AreEqual(0, foe.Statuses.Count,
                "applied on somebody else's turn, so the wearer's next turn end is the first of its one");

            session.TickStatusesAtTurnEndForTest(hero);
            Assert.AreEqual(0, hero.Statuses.Count, "spared once, not twice");
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
