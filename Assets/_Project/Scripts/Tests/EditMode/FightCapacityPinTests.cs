using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The seam between "how many the HUD reserves" and "how many content can
    // actually produce".
    //
    // FightHudSpec's capacities are deliberately NOT derived from content: they
    // are design decisions about the layout, and deriving them would make the
    // screen's shape change whenever someone authored a skill. The cost of that
    // choice is that content can silently outgrow the reservation -- a ninth
    // skill on a character would simply never get a row, with nothing anywhere
    // saying why.
    //
    // These pins are that cost paid. They read the real catalogs off disk rather
    // than through Resources, so they stay EditMode and sub-second.
    public class FightCapacityPinTests
    {
        // An unlockLevel this high means "not by level at all" -- the skill is
        // granted by a talent instead.
        private const int TalentGrantedSentinel = 999;

        private static string ContentDataRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "ContentData")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate Assets/_Project/ContentData from the working directory.");
            return Path.Combine(dir.FullName, "Assets", "_Project", "ContentData");
        }

        private static string Read(string file)
        {
            string path = Path.Combine(ContentDataRoot(), file);
            Assert.IsTrue(File.Exists(path), $"{file} is missing - this pin would otherwise pass vacuously.");
            return File.ReadAllText(path);
        }

        // ---- the guaranteed set -------------------------------------------------

        [Test]
        public void NoCharacterUnlocksMoreSkillsByLevelThanTheSubmenuReserves()
        {
            // The set every character is GUARANTEED to hold: skills that unlock
            // by level alone. A skill authored at the sentinel level is granted
            // by a talent instead, and is counted by the recording below --
            // a harder question with a worse answer.
            var entries = SkillRows(Read("skills.json"), "skills");

            Assert.IsNotEmpty(entries,
                "skills.json parsed to zero entries - the pin is not reading the catalog, so it would pass whatever content did.");

            var byOwner = entries
                .Where(e => e.UnlockLevel < TalentGrantedSentinel)
                .GroupBy(e => e.Owner, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count());

            Assert.IsNotEmpty(byOwner, "no skill unlocks by level at all - the level field is not being read.");

            var over = byOwner.Where(p => p.Value > FightSubmenuLayout.MaxRows)
                .Select(p => $"{p.Key} has {p.Value}")
                .ToList();

            Assert.IsEmpty(over,
                $"the submenu reserves {FightSubmenuLayout.MaxRows} rows, and a skill past that gets no row at all, " +
                $"silently: {string.Join(", ", over)}.");
        }

        // RECORDED, NOT RESOLVED -- the posture AUDIT.md takes, for the same
        // reason: how many abilities a finished build may carry is a design
        // decision, and it belongs to the author.
        //
        // The finding: a character holds ONE talent root, so the worst case is
        // level-unlocked skills plus everything a single root grants. For the
        // sheep that is 5 + 6 = 11 down the Fragile Lamb path, against 8 rows.
        // Three of a fully-built Lamb's skills would have nowhere to go.
        //
        // Raising MaxRows is not a free fix. Eleven rows reach y 399 and the
        // dialogue bark's box begins at 378, so the column would run into the
        // combat log. The real options are a scrolling column, a second page, or
        // fewer granted skills, and all three are design calls.
        //
        // Pinned to the exact number so it cannot drift unnoticed in either
        // direction: content growing makes it worse, and a fix makes it pass a
        // different way and deletes this test.
        [Test]
        public void AFullyBuiltRootCanOutgrowTheSubmenu_Recorded()
        {
            var skills = SkillRows(Read("skills.json"), "skills");
            var grants = TalentGrants(Read("talents.json"), "talents");

            Assert.IsNotEmpty(grants, "no talent grants a skill - this recording is measuring nothing.");

            var worst = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var owner in skills.Select(s => s.Owner).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                int byLevel = skills.Count(s => s.UnlockLevel < TalentGrantedSentinel
                                                && string.Equals(s.Owner, owner, StringComparison.OrdinalIgnoreCase));

                int mostFromOneRoot = grants
                    .Where(g => string.Equals(g.Owner, owner, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(g => g.Root, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Count())
                    .DefaultIfEmpty(0)
                    .Max();

                worst[owner] = byLevel + mostFromOneRoot;
            }

            int overall = worst.Values.DefaultIfEmpty(0).Max();
            string who = worst.OrderByDescending(p => p.Value).First().Key;

            // 11 -> 12 on 2026-08-22, and the direction is the bad one: Shawn
            // gained Mud Burst at level 3, so his levelled kit is six skills
            // rather than five and the worst case is now four MORE than the
            // eight rows that can be drawn. Removing the other four characters
            // did not move this -- the worst case was always his.
            Assert.AreEqual(12, overall,
                $"the worst-case simultaneous skill count moved (now {overall}, on '{who}'). If it went UP, more " +
                $"skills are invisible than before. If it went DOWN, check whether it now fits in " +
                $"{FightSubmenuLayout.MaxRows} rows, and if it does, replace this recording with a real bound.");

            Assert.Greater(overall, FightSubmenuLayout.MaxRows,
                "this recording exists because the worst case does NOT fit. If it does now, delete it and assert the " +
                "bound properly in the test above.");
        }

        // ---- the other two capacities ---------------------------------------------

        [Test]
        public void NoSignatureResourceHoldsMorePointsThanThereArePips()
        {
            string json = Read("characters.json");
            var maxima = IntValues(json, "signatureMax");

            // Not IsNotEmpty: a signature resource is optional and today only
            // one character has one. Zero is a legitimate state, and the field
            // guard below is what stops a RENAME turning this pin into a no-op
            // that still reports green.
            Assert.IsTrue(json.Contains("signature"),
                "characters.json mentions no signature resource at all - either the field was renamed, in which case " +
                "this pin now measures nothing, or the feature was removed and the pin should go with it.");

            var over = maxima.Where(m => m > FightHudSpec.WoolPips).ToList();

            Assert.IsEmpty(over,
                $"the party plate draws {FightHudSpec.WoolPips} pips, and points past that are invisible: " +
                $"{string.Join(", ", over)}. Fix by raising FightHudSpec.WoolPips (and re-checking the pip row still " +
                $"fits inside the plate) or by lowering the resource's maximum.");
        }

        [Test]
        public void OnePlatePerStageSlot()
        {
            // The third capacity, and the one with a visible failure rather than
            // a silent one: an enemy standing on the stage with no plate cannot
            // be targeted through the column the design points the player at.
            var ids = StringValues(Read("enemies.json"), "id");

            Assert.IsNotEmpty(ids, "enemies.json parsed to zero entries - this pin is not reading the catalog.");
            Assert.AreEqual(FightHudSpec.StageSlotsPerSide, FightHudSpec.EnemyPlates,
                "v1 tied these together and so does FightHudSpec; if they ever diverge, a monster on the stage has " +
                "no plate to be targeted through.");
        }

        // ---- reading the catalogs ------------------------------------------------

        private readonly struct SkillRow
        {
            public readonly string Owner;
            public readonly int UnlockLevel;
            public SkillRow(string owner, int unlockLevel) { Owner = owner; UnlockLevel = unlockLevel; }
        }

        private readonly struct Grant
        {
            public readonly string Owner;
            public readonly string Root;
            public Grant(string owner, string root) { Owner = owner; Root = root; }
        }

        private static List<SkillRow> SkillRows(string json, string arrayField)
        {
            var rows = new List<SkillRow>();
            foreach (var block in Blocks(json, arrayField))
            {
                var owner = StringValues(block, "characterId").FirstOrDefault();
                if (string.IsNullOrEmpty(owner)) continue;

                var levels = IntValues(block, "unlockLevel");
                rows.Add(new SkillRow(owner, levels.Count > 0 ? levels[0] : 1));
            }
            return rows;
        }

        // A talent id reads `<character>_<root>_<strand>_<tier>`, so the root a
        // grant belongs to falls straight out of its own name. Guarded: an id
        // that does not fit the convention is reported rather than silently
        // bucketed, which would make every grant look like one path.
        private static List<Grant> TalentGrants(string json, string arrayField)
        {
            var grants = new List<Grant>();
            foreach (var block in Blocks(json, arrayField))
            {
                var granted = StringValues(block, "grantsSkillId").FirstOrDefault();
                if (string.IsNullOrEmpty(granted)) continue;

                var owner = StringValues(block, "characterId").FirstOrDefault() ?? "";
                var id = StringValues(block, "id").FirstOrDefault() ?? "";
                var parts = id.Split('_');

                Assert.GreaterOrEqual(parts.Length, 3,
                    $"talent id '{id}' does not follow <character>_<root>_<strand>, so its root cannot be read.");

                grants.Add(new Grant(owner, parts[1]));
            }
            return grants;
        }

        // One string per RECORD of the named array. A brace scan rather than a
        // parser, which is exact enough here because these catalogs are arrays
        // of records -- but it has to start inside the named array, not at the
        // file root, or a wrapper object hands back the whole file as one
        // "record" and every count collapses to 1. That is not hypothetical: it
        // is what the first version of this did, and the recording below caught
        // it by asserting a number rather than a bound.
        private static IEnumerable<string> Blocks(string json, string arrayField)
        {
            int field = json.IndexOf("\"" + arrayField + "\"", StringComparison.Ordinal);
            int open = field < 0 ? json.IndexOf('[') : json.IndexOf('[', field);
            if (open < 0) yield break;

            int depth = 0;
            int start = -1;
            for (int i = open + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '{' || c == '[')
                {
                    if (c == '{' && depth == 0) start = i;
                    depth++;
                }
                else if (c == '}' || c == ']')
                {
                    if (depth == 0) yield break;  // the array closed
                    depth--;
                    if (depth == 0 && c == '}' && start >= 0)
                    {
                        yield return json.Substring(start, i - start + 1);
                        start = -1;
                    }
                }
            }
        }

        private static List<string> StringValues(string json, string field)
        {
            var rx = new Regex("\"" + Regex.Escape(field) + "\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.Compiled);
            return rx.Matches(json).Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        }

        private static List<int> IntValues(string json, string field)
        {
            var rx = new Regex("\"" + Regex.Escape(field) + "\"\\s*:\\s*(-?\\d+)", RegexOptions.Compiled);
            return rx.Matches(json).Cast<Match>()
                .Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
                .ToList();
        }
    }
}
