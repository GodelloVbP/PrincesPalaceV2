using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace PrincesPalace.Domain.Tests
{
    // E4 (UiCountAudit) is OPT-IN per screen: ScreenDef.CountBindings is a
    // nullable delegate, and SceneBuilder.cs skips the check entirely when it
    // is null. That is the exact shape the bug-hunt (2026-09-10, finding 2)
    // flagged: a screen can hand-build `X.Select(...).ToArray()` off one node
    // list and never say whether the result is meant to agree with anything,
    // and nothing build-time notices either way.
    //
    // WHY A TEXT LINT AND NOT A REAL FIX. Making E4 mandatory (reflecting over
    // every bound array the way UiWiringSweep does) needs provenance -- which
    // DECLARED node list a bound array's elements actually came from -- and
    // that only exists inside ScreenRegistry.cs's own Wire steps, which this
    // suite (Domain-only, per docs/CODE_STANDARDS.md "Layering") cannot reference or
    // edit under this session's shared-tree rules. So this is the mechanical
    // half: every `.Select(...).ToArray()` assignment in ScreenRegistry.cs
    // must EITHER sit inside a screen that registers CountBindings, OR sit in
    // a chunk that carries a "NO CountBindings" comment explaining why not --
    // mirroring the two places (MainMenu, WireRewardTrack) that already do
    // this by hand. A site that does neither is silent opt-out by omission,
    // which is the bug.
    //
    // A DIAGNOSTIC, NOT A GUARANTEE, same caveat as ContentOwnershipLintTests:
    // this cannot tell whether a REGISTERED CountBinding actually checks the
    // right pair of lists, only that SOME binding or SOME justification
    // exists. ContentIsolationTests-style, this is the cheap net, not the
    // proof.
    //
    // TODAY'S FILE FAILS THIS CHECK ON 34 SITES -- the bug-hunt's own count
    // ("dozens" of unprotected `.Select().ToArray()` sites). Rather than
    // leave the lint red (which trains everyone to ignore it) or force a
    // ScreenRegistry.cs change outside this session's scope, every known
    // offender today is named in KnownOffenders below, each one an omission
    // to close, not a decision to defend. A NEW site added after this commit
    // gets no such exemption: it fails, naming itself, the way pre-commit's
    // own conventions are self-checked.
    public class UiCountAuditCoverageLintTests
    {
        // A boundary starts a new "chunk": either a top-level ScreenDef's
        // `PanelName = "XPanel"`, or one of the `private static ... Wire<Name>(`
        // methods a top-level screen calls out to for a sub-panel (Shop,
        // Party, the system menu's panes, ...). Coverage never carries across
        // a boundary -- a marker earns the sites bound INSIDE the same chunk,
        // never a chunk it merely precedes in the file.
        private static readonly Regex PanelBoundary = new Regex(
            @"PanelName\s*=\s*""(?<name>\w+)""", RegexOptions.Compiled);

        private static readonly Regex MethodBoundary = new Regex(
            @"private\s+static\s+\S.*?\bWire(?<name>\w*)\s*\(", RegexOptions.Compiled);

        // A screen opts IN by building a CountBindings list, or opts out with
        // a comment saying why one would be vacuous here -- see the header.
        // Either one, anywhere before the chunk's next boundary, covers every
        // `.ToArray()` site buffered since the chunk started.
        private static readonly Regex RegisteredMarker = new Regex(
            @"CountBindings\s*=\s*\(\)\s*=>\s*new\[\]", RegexOptions.Compiled);

        private static readonly Regex ExemptMarker = new Regex(@"NO CountBindings", RegexOptions.Compiled);

        // A one-line binding site: `some.field = <expr>.Select(<lambda>).ToArray();`.
        private static readonly Regex SingleLineSite = new Regex(
            @"^\s*(?<lhs>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+)\s*=.*\.Select\(.*\.ToArray\(\);\s*$",
            RegexOptions.Compiled);

        // The tail of a multi-line chain (`.Select(...)` opened several lines
        // up, `.ToArray();` closes it here) -- anything ending the statement
        // this way that ISN'T already a one-liner.
        private static readonly Regex ChainTail = new Regex(@"\.ToArray\(\);\s*$", RegexOptions.Compiled);

        private static readonly Regex AssignmentStart = new Regex(
            @"^(?<lhs>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+)\s*=", RegexOptions.Compiled);

        // `Count("ShopController.gearCards", ...)` registers "gearCards" --
        // the last dotted segment of the label -- as covered globally,
        // regardless of which chunk the Count(...) call itself sits in (the
        // registration and the array it protects are often in different
        // methods: WireShop's own arrays are registered from a CountBindings
        // block that lives in the Map screen's ScreenDef, because that is
        // where shopController's caller sits).
        private static readonly Regex CountLabel = new Regex(@"Count\(\s*""(?<label>[^""]+)""", RegexOptions.Compiled);

        // Every currently-unprotected site, named "<chunk>:<lhs>" so the four
        // methods that each declare their own local `controller.icons` don't
        // collide. Each one is a real gap the bug-hunt found, not a site this
        // test has decided is safe -- see the class header. Removing an entry
        // here (because ScreenRegistry.cs now registers or comments it) is
        // always welcome; adding one to silence a NEW site defeats the point
        // of this file and should be a CountBinding or a comment instead.
        private static readonly HashSet<string> KnownOffenders = new HashSet<string>
        {
            "WireShop:shop.packNames",
            "WireShop:shop.packMetas",
            "WireShop:shop.packPrices",
            "WireShop:shop.packSellOneButtons",
            "WireShop:shop.packSellAllButtons",
            "TalentPanel:talents.cloudImages",
            "TalentPanel:talents.dustImages",
            "TalentPanel:talents.shootingStarImages",
            "WireParty:controller.seatDragSources",
            "WireParty:controller.cardDragSources",
            "WireExits:controller.exitLabels",
            "WireDossier:controller.icons",
            "WireReckoning:controller.offerBurstRects",
            "WireReckoning:controller.icons",
            "WireReckoning:controller.offerRects",
            "WireGlossary:controller.icons",
            "WireRelicDraft:controller.icons",
        };

        private struct Site
        {
            public string Chunk;
            public int Line;
            public string Lhs;
        }

        [Test]
        public void EveryBindingSiteIsRegisteredOrExplicitlyExempted()
        {
            string path = Path.Combine(RepoTree.Root(), "Assets", "_Project", "Scripts",
                "Editor", "SceneBuilder", "ScreenRegistry.cs");
            Assert.IsTrue(File.Exists(path), $"expected to find {path}");

            string[] lines = File.ReadAllLines(path);
            Assert.Greater(lines.Length, 500, "ScreenRegistry.cs looks far shorter than expected -- " +
                                               "scanning the wrong file?");

            var registeredKeys = new HashSet<string>();
            foreach (string line in lines)
            {
                Match m = CountLabel.Match(line);
                if (m.Success) registeredKeys.Add(LastSegment(m.Groups["label"].Value));
            }

            var offenders = new List<Site>();
            var covered = new List<Site>();
            var buffer = new List<Site>();
            string chunk = "TOP";

            void Flush(bool coveredByComment)
            {
                foreach (Site site in buffer)
                {
                    if (coveredByComment || registeredKeys.Contains(LastSegment(site.Lhs)))
                    {
                        covered.Add(site);
                    }
                    else
                    {
                        offenders.Add(site);
                    }
                }
                buffer.Clear();
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                int lineNo = i + 1;

                Match panel = PanelBoundary.Match(raw);
                Match method = MethodBoundary.Match(raw);
                if (panel.Success || method.Success)
                {
                    Flush(false);
                    chunk = panel.Success ? panel.Groups["name"].Value : "Wire" + method.Groups["name"].Value;
                    continue;
                }

                if (RegisteredMarker.IsMatch(raw) || ExemptMarker.IsMatch(raw))
                {
                    Flush(true);
                    continue;
                }

                Match single = SingleLineSite.Match(raw);
                if (single.Success)
                {
                    buffer.Add(new Site { Chunk = chunk, Line = lineNo, Lhs = single.Groups["lhs"].Value });
                    continue;
                }

                if (ChainTail.IsMatch(raw) && !SingleLineSite.IsMatch(raw))
                {
                    string found = BackwardFindAssignmentStart(lines, i, out bool sawSelect);
                    if (found != null && sawSelect)
                    {
                        buffer.Add(new Site { Chunk = chunk, Line = lineNo, Lhs = found });
                    }
                }
            }

            Flush(false);

            var unexpected = new List<string>();
            var stillUsed = new HashSet<string>();
            foreach (Site site in offenders)
            {
                string key = $"{site.Chunk}:{site.Lhs}";
                if (KnownOffenders.Contains(key))
                {
                    stillUsed.Add(key);
                }
                else
                {
                    unexpected.Add($"  ScreenRegistry.cs:{site.Line}: '{site.Lhs}' in {site.Chunk} is neither " +
                                   "registered as a CountBinding nor covered by a \"NO CountBindings\" comment, " +
                                   "and is not a known offender. Add a CountBinding, add the justification " +
                                   "comment, or (if this really is a fresh gap) add it to KnownOffenders with a " +
                                   "reason.");
                }
            }

            Assert.IsEmpty(unexpected,
                $"UiCountAudit coverage lint: {unexpected.Count} NEW unprotected binding site(s):\n" +
                string.Join("\n", unexpected));

            var stale = new List<string>();
            foreach (string known in KnownOffenders)
            {
                if (!stillUsed.Contains(known)) stale.Add(known);
            }

            Assert.IsEmpty(stale,
                "KnownOffenders names site(s) that are no longer unprotected (fixed, renamed, or removed) -- " +
                "delete them from the allowlist so it keeps naming only real gaps:\n" + string.Join("\n", stale));
        }

        private static string LastSegment(string dotted)
        {
            int idx = dotted.LastIndexOf('.');
            return idx < 0 ? dotted : dotted.Substring(idx + 1);
        }

        // Scans upward from a `.ToArray();` chain tail to the statement that
        // opened it, the same "draw only as far as the fence" idea
        // UiWiringSweep's Members() uses GetEndProperty for -- here the fence
        // is INDENTATION: a nested lambda body sits deeper than the statement
        // that opens it, so the nearest earlier line at or below this line's
        // own indentation, that looks like an assignment, is the start.
        private static string BackwardFindAssignmentStart(string[] lines, int tailIndex, out bool sawSelect)
        {
            sawSelect = lines[tailIndex].Contains(".Select(");
            int tailIndent = IndentOf(lines[tailIndex]);

            for (int j = tailIndex - 1; j >= 0; j--)
            {
                string line = lines[j];
                if (line.Contains(".Select(")) sawSelect = true;

                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;

                if (IndentOf(line) <= tailIndent)
                {
                    Match m = AssignmentStart.Match(trimmed);
                    if (m.Success) return m.Groups["lhs"].Value;
                }
            }

            return null;
        }

        private static int IndentOf(string line)
        {
            int i = 0;
            while (i < line.Length && line[i] == ' ') i++;
            return i;
        }
    }
}
