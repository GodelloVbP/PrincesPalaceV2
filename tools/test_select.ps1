# tools/test_select.ps1 -- which tests a change can affect, for the commit gate.
#
# Dot-sourced by run_tests_parallel.ps1 after test_areas.ps1 and
# playmode_shards.ps1. Never invoked directly. Pure ASCII, no BOM.
#
# THE MODEL. A changed production .cs file declares types. A test is selected
# when its file NAMES one of them -- an identifier match with comments
# stripped and string literals kept, so a type named in a string still counts.
# Test files are nodes of the same graph: a *TestBase in an area folder passes
# a reference on to the fixtures deriving from it for free. A Tests/**/Shared
# helper does not -- it costs a hop like production code, because Shared is
# cross-cutting by definition: TestGlobals names 17 production types and 65
# fixture files name it, so a free hop through it selected every fixture that
# resets the engine clock.
#
# ONE HOP ($SelectDepth = 1), NOT THE TRANSITIVE CLOSURE. Measured 2026-09-24
# over 528 production files: the unbounded closure of 505 of them reaches 70%
# or more of production and selects ~206 of the 227 PlayMode fixtures for
# nearly any file -- the full suite, slower to compute. Two hops: StageShake
# 1 -> 78 classes (it reaches FightController, which 73 PlayMode fixtures
# name), CombatMath 22 -> 225. One hop is the only depth that focuses at all;
# what it gives up (a test that reaches the change only through another
# production type or a Shared helper) is what the safety net is for.
#
# WHAT A NAME SCAN CANNOT SEE, and what the gate does instead (per file):
#   - not a .cs file (content JSON, art, fonts, resources)   -> the area map
#   - a HUB type changed: named by $SelectHubShare or more of the production
#     files, so one hop reaches a large indirect surface     -> names + area map
#   - a [SerializeField] line or a screen tree/ScreenRegistry changed: scenes
#     are built from these, and a scene is loaded by name, not by type
#                                                            -> names + area map
#   - a changed line WRITES process-wide engine state (Time.timeScale and
#     friends, EventSystem.current, PlayerPrefs): it leaks into whatever test
#     runs next, reference or not                            -> FULL suite
#   - comment/whitespace-only edit    -> nothing by name; the repo readers only
# Plus, on every non-empty selection: the EditMode classes that read the repo
# off disk (lints, pins, freshness checks). They are coupled by path, not by
# name, and they are cheap.
#
# What still escapes, stated rather than hidden: a test that reaches the change
# through a second production type; a MonoBehaviour exercised only because it
# sits in a scene a test loads; reflection that builds a type name at runtime;
# a test order-coupled to a fixture that did not run. The safety net
# (Get-FullRunPromotion: full after $LastGreenMaxCommits commits or
# $LastGreenMaxHours hours) and the map-gap report (Write-MapGapReport) exist
# for exactly those.

$SelectDepth = 1
$SelectHubShare = 0.05
$LastGreenMaxCommits = 5
$LastGreenMaxHours = 24
$LastFullGreenFile = Join-Path $PSScriptRoot ".last-full-green"

# WRITES to engine and process globals. One survives into the next test in
# the same Unity process whether or not that test names the writer.
$GlobalStatePattern = '\b(Time\.(timeScale|captureDeltaTime|captureFramerate|fixedDeltaTime|maximumDeltaTime)|Application\.targetFrameRate|QualitySettings\.vSyncCount|EventSystem\.current)\s*[-+*/]?=(?!=)|\bPlayerPrefs\.(Set|Delete)'
# Where scenes come from: a screen tree, or the registry/builder that emits it.
$ScenePathPattern = '^Assets/_Project/Scripts/(Domain/UiKit/Screens/|Editor/SceneBuilder/)'
# An EditMode test that reads the repo off disk. RepoTree and ContentDataFiles
# are the Shared helpers that do it for a fixture.
$RepoReadPattern = '\b(RepoTree|ContentDataFiles|Application\.dataPath|File\.ReadAll\w+|Directory\.(GetFiles|EnumerateFiles|GetDirectories))\b'

if (-not ("PP.TestSelect.RefGraph" -as [type])) {
    # C# 5: Windows PowerShell 5.1's Add-Type compiler. Single-quoted
    # here-string so PowerShell expands nothing inside it.
    Add-Type -Language CSharp -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PP.TestSelect
{
    public sealed class Closure
    {
        // file index -> the type through which it was reached ("" for a seed)
        public Dictionary<int, string> Via = new Dictionary<int, string>();
        // file index -> the file that declared that type (-1 for a seed)
        public Dictionary<int, int> From = new Dictionary<int, int>();
        // file index -> production hops from the change (0 for a seed)
        public Dictionary<int, int> Depth = new Dictionary<int, int>();
    }

    public sealed class RefGraph
    {
        public List<string> Rel = new List<string>();
        public List<string[]> Declares = new List<string[]>();
        public List<bool> IsTest = new List<bool>();
        // A test file that passes a reference on at no depth cost: every test
        // file except the Tests/**/Shared helpers (see Close).
        public List<bool> FreeHop = new List<bool>();
        public List<bool> ReadsRepo = new List<bool>();
        public Dictionary<string, List<int>> RefBy = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        public Dictionary<string, List<int>> DeclaredIn = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        public Dictionary<string, int> IndexOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private List<HashSet<string>> _idents = new List<HashSet<string>>();

        // Strings (verbatim, regular, char) and comments, in one alternation
        // so a "//" inside a string is not read as a comment.
        static readonly Regex Lexeme = new Regex(
            @"@""(?:[^""]|"""")*""|""(?:\\.|[^""\\\r\n])*""|'(?:\\.|[^'\\\r\n])*'|//[^\r\n]*|/\*[\s\S]*?\*/",
            RegexOptions.Compiled);
        static readonly Regex Ident = new Regex(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);
        static readonly Regex Ws = new Regex(@"\s+", RegexOptions.Compiled);
        static readonly Regex Decl = new Regex(@"\b(?:class|struct|interface|enum|record)\s+([A-Za-z_]\w*)", RegexOptions.Compiled);
        static readonly Regex Deleg = new Regex(@"\bdelegate\s+[\w<>\[\],.?\s]+?\s([A-Za-z_]\w*)\s*(?:<[^<>]*>)?\s*\(", RegexOptions.Compiled);
        // Extension methods: a caller writes x.Foo() and never names the
        // static class, so the method name is indexed as if it were a type.
        static readonly Regex Ext = new Regex(@"\bstatic\b[^;{}=()]*?\b([A-Za-z_]\w*)\s*(?:<[^<>()]*>)?\s*\(\s*this\s", RegexOptions.Compiled);
        static readonly Regex NsHeader = new Regex(@"\bnamespace\s+[\w.]+\s*$", RegexOptions.Compiled);
        static readonly HashSet<string> Keywords = new HashSet<string>(new[] {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
            "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
            "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
            "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
            "where", "unmanaged", "notnull", "record", "var" });

        public static string StripComments(string s)
        {
            return Lexeme.Replace(s, m => m.Value[0] == '/' ? " " : m.Value);
        }

        static string CodeOnly(string s)
        {
            return Lexeme.Replace(s, m => m.Value[0] == '/' ? " " : "\"\"");
        }

        // Comments dropped, whitespace outside literals reduced to token
        // separators, literals kept byte for byte. Equal => the edit changed
        // no code.
        public static string Normalize(string s)
        {
            var tokens = new List<string>();
            int last = 0;
            foreach (Match m in Lexeme.Matches(s))
            {
                AddWords(tokens, s.Substring(last, m.Index - last));
                if (m.Value[0] != '/') tokens.Add(m.Value);
                last = m.Index + m.Length;
            }
            AddWords(tokens, s.Substring(last));
            return string.Join(" ", tokens.ToArray());
        }

        static void AddWords(List<string> into, string segment)
        {
            foreach (var w in Ws.Split(segment)) if (w.Length > 0) into.Add(w);
        }

        // TOP-LEVEL types only (directly in a namespace or the file), plus
        // extension method names. A nested type is named from outside through
        // its outer type (Outer.Kind), which is indexed already; indexing
        // "Kind", "Entry" or "Tier" on their own matched every member of that
        // name in the codebase and joined unrelated files into one component.
        public static string[] DeclaredTypes(string text)
        {
            var code = CodeOnly(text);
            var hits = new List<KeyValuePair<int, string>>();
            foreach (Match m in Decl.Matches(code)) hits.Add(new KeyValuePair<int, string>(m.Index, m.Groups[1].Value));
            foreach (Match m in Deleg.Matches(code)) hits.Add(new KeyValuePair<int, string>(m.Index, m.Groups[1].Value));
            hits.Sort((a, b) => a.Key.CompareTo(b.Key));
            var names = new List<string>();
            var stack = new Stack<bool>();
            int nonNs = 0, seg = 0, h = 0;
            for (int i = 0; i <= code.Length; i++)
            {
                while (h < hits.Count && hits[h].Key <= i) { if (nonNs == 0) Push(names, hits[h].Value); h++; }
                if (i == code.Length) break;
                char ch = code[i];
                if (ch == '{')
                {
                    bool ns = NsHeader.IsMatch(code.Substring(seg, i - seg));
                    stack.Push(ns);
                    if (!ns) nonNs++;
                    seg = i + 1;
                }
                else if (ch == '}')
                {
                    if (stack.Count > 0 && !stack.Pop()) nonNs--;
                    seg = i + 1;
                }
                else if (ch == ';') seg = i + 1;
            }
            foreach (Match m in Ext.Matches(code)) Push(names, m.Groups[1].Value);
            return names.ToArray();
        }

        static void Push(List<string> names, string n)
        {
            if (!Keywords.Contains(n) && !names.Contains(n)) names.Add(n);
        }

        static string Platform(string rel)
        {
            if (rel.IndexOf("/Tests/EditMode/", StringComparison.Ordinal) >= 0) return "EditMode";
            if (rel.IndexOf("/Tests/PlayMode/", StringComparison.Ordinal) >= 0) return "PlayMode";
            return "";
        }

        public static RefGraph Build(string projectRoot, string scriptsRel, string repoReadPattern)
        {
            var g = new RefGraph();
            var rr = new Regex(repoReadPattern);
            var root = Path.Combine(projectRoot, scriptsRel.Replace('/', Path.DirectorySeparatorChar));
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            string prefix = projectRoot.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            foreach (var f in files)
            {
                var rel = f.Substring(prefix.Length).Replace('\\', '/');
                var text = File.ReadAllText(f);
                var stripped = StripComments(text);
                int i = g.Rel.Count;
                g.Rel.Add(rel);
                g.IndexOf[rel] = i;
                bool isTest = rel.IndexOf("/Scripts/Tests/", StringComparison.Ordinal) >= 0;
                g.IsTest.Add(isTest);
                g.FreeHop.Add(isTest && rel.IndexOf("/Shared/", StringComparison.Ordinal) < 0);
                g.ReadsRepo.Add(rr.IsMatch(stripped));
                var decl = DeclaredTypes(text);
                g.Declares.Add(decl);
                foreach (var d in decl)
                {
                    List<int> l;
                    if (!g.DeclaredIn.TryGetValue(d, out l)) { l = new List<int>(); g.DeclaredIn[d] = l; }
                    l.Add(i);
                }
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (Match m in Ident.Matches(stripped)) ids.Add(m.Value);
                g._idents.Add(ids);
            }
            // A type declared only in test files is visible only to test files
            // of the same platform (EditMode and PlayMode are separate
            // assemblies, and production sees neither). Without this, a test's
            // local Mathf shim was "referenced" by every production file that
            // calls UnityEngine.Mathf.
            var testOnly = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in g.DeclaredIn)
            {
                string plat = null;
                foreach (var f in kv.Value)
                {
                    var p = g.IsTest[f] ? Platform(g.Rel[f]) : "";
                    if (p == "") { plat = null; break; }
                    if (plat == null) plat = p; else if (plat != p) { plat = null; break; }
                }
                if (plat != null) testOnly[kv.Key] = plat;
            }
            for (int i = 0; i < g.Rel.Count; i++)
            {
                var myPlat = g.IsTest[i] ? Platform(g.Rel[i]) : "";
                foreach (var id in g._idents[i])
                {
                    if (!g.DeclaredIn.ContainsKey(id)) continue;
                    string need;
                    if (testOnly.TryGetValue(id, out need) && need != myPlat) continue;
                    List<int> l;
                    if (!g.RefBy.TryGetValue(id, out l)) { l = new List<int>(); g.RefBy[id] = l; }
                    l.Add(i);
                }
            }
            g._idents = null;
            return g;
        }

        public bool Mentions(int file, string type)
        {
            List<int> l;
            return RefBy.TryGetValue(type, out l) && l.Contains(file);
        }

        // Dependents of the seed types, as file index -> depth. A fixture or a
        // *TestBase in an area folder passes what it reached on at the SAME
        // depth, so a fixture that sees a changed type only through its base
        // class is found. Production files and the Tests/**/Shared helpers
        // each cost a hop: Shared is cross-cutting by definition (TestGlobals
        // names 17 production types and 65 fixture files name it), and a free
        // hop through it selected every fixture that resets the clock. A file
        // at depth maxDepth is included but not expanded.
        // maxDepth < 0 is the unbounded transitive closure. 0-1 BFS, so every
        // file is recorded at its smallest depth, with the trail that gave it.
        public Closure Close(string[] seedTypes, int[] seedFiles, int maxDepth)
        {
            var c = new Closure();
            var typeDepth = new Dictionary<string, int>(StringComparer.Ordinal);
            var dq = new LinkedList<Tuple<string, int, int>>();
            int origin = seedFiles.Length > 0 ? seedFiles[0] : -1;
            foreach (var f in seedFiles) { c.Via[f] = ""; c.From[f] = -1; c.Depth[f] = 0; }
            foreach (var t in seedTypes) dq.AddLast(Tuple.Create(t, origin, 0));
            while (dq.Count > 0)
            {
                var item = dq.First.Value;
                dq.RemoveFirst();
                int seen;
                if (typeDepth.TryGetValue(item.Item1, out seen) && seen <= item.Item3) continue;
                typeDepth[item.Item1] = item.Item3;
                List<int> refs;
                if (!RefBy.TryGetValue(item.Item1, out refs)) continue;
                foreach (var r in refs)
                {
                    int rd = FreeHop[r] ? item.Item3 : item.Item3 + 1;
                    int had;
                    if (c.Depth.TryGetValue(r, out had) && had <= rd) continue;
                    c.Depth[r] = rd;
                    c.Via[r] = item.Item1;
                    c.From[r] = item.Item2;
                    if (!FreeHop[r] && maxDepth >= 0 && rd >= maxDepth) continue;
                    foreach (var d in Declares[r])
                    {
                        var next = Tuple.Create(d, r, rd);
                        if (FreeHop[r]) dq.AddFirst(next); else dq.AddLast(next);
                    }
                }
            }
            return c;
        }
    }
}
'@
}

# --- graph and lookups (cached for the process) ------------------------------

$script:RefGraphCache = $null
$script:HubTypeCache = $null

function Get-RefGraph {
    if (-not $script:RefGraphCache) {
        $script:RefGraphCache = [PP.TestSelect.RefGraph]::Build($AreasProjectRoot, "Assets/_Project/Scripts", $RepoReadPattern)
    }
    return $script:RefGraphCache
}

# Test file (forward-slash RelPath) -> every class it declares. Built from the
# declaration SITES, not the index, so both halves of a partial fixture map.
function Get-ClassesByFile {
    param([hashtable]$Index)
    $null = Get-TestIndex
    $shared = $SharedFolder.ToLower()
    $map = @{}
    foreach ($name in $script:TestSiteCache.Keys) {
        if (-not $Index.ContainsKey($name)) { continue }
        foreach ($site in @($script:TestSiteCache[$name])) {
            if ($site.Area -eq $shared) { continue }
            $rel = $site.RelPath -replace '\\', '/'
            if (-not $map.ContainsKey($rel)) { $map[$rel] = @() }
            $map[$rel] += $name
        }
    }
    return $map
}

# Production type -> how many OTHER production files name it, for the types at
# or over $SelectHubShare of all production files.
function Get-HubTypes {
    if ($script:HubTypeCache) { return $script:HubTypeCache }
    $g = Get-RefGraph
    $prodCount = @(0..($g.Rel.Count - 1) | Where-Object { -not $g.IsTest[$_] }).Count
    $hubs = @{}
    foreach ($t in $g.DeclaredIn.Keys) {
        $decl = @($g.DeclaredIn[$t])
        if (@($decl | Where-Object { $g.IsTest[$_] }).Count -gt 0) { continue }
        if (-not $g.RefBy.ContainsKey($t)) { continue }
        $n = @($g.RefBy[$t] | Where-Object { -not $g.IsTest[$_] -and $decl -notcontains $_ }).Count
        if ($n -ge [Math]::Ceiling($SelectHubShare * $prodCount)) { $hubs[$t] = $n }
    }
    $script:HubTypeCache = $hubs
    return $hubs
}

# EditMode classes that read the repo off disk, directly or through an
# EditMode Shared helper that does.
function Get-RepoReaderClasses {
    param([hashtable]$ClassesByFile)
    $g = Get-RefGraph
    $helperTypes = @()
    for ($i = 0; $i -lt $g.Rel.Count; $i++) {
        if ($g.ReadsRepo[$i] -and $g.Rel[$i] -like '*/Tests/EditMode/Shared/*') { $helperTypes += $g.Declares[$i] }
    }
    $out = @()
    for ($i = 0; $i -lt $g.Rel.Count; $i++) {
        $rel = $g.Rel[$i]
        if ($rel -notlike '*/Tests/EditMode/*' -or $rel -like '*/Tests/EditMode/Shared/*') { continue }
        $reads = $g.ReadsRepo[$i]
        if (-not $reads) { foreach ($t in $helperTypes) { if ($g.Mentions($i, $t)) { $reads = $true; break } } }
        if ($reads -and $ClassesByFile.ContainsKey($rel)) { $out += $ClassesByFile[$rel] }
    }
    return @($out | Sort-Object -Unique)
}

# git with UTF-8 output. PowerShell's own capture of a native command decodes
# through the console code page, which would make every non-ASCII file look
# edited to the comment-only check.
function Invoke-GitText {
    param([string[]]$GitArgs)
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = "git"
    $quoted = @("-C", $AreasProjectRoot) + $GitArgs | ForEach-Object { '"' + ($_ -replace '"', '\"') + '"' }
    $psi.Arguments = $quoted -join ' '
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8
    $p = [Diagnostics.Process]::Start($psi)
    $out = $p.StandardOutput.ReadToEnd()
    $null = $p.StandardError.ReadToEnd()
    $p.WaitForExit()
    if ($p.ExitCode -ne 0) { return $null }
    return $out
}

# A file's text at a revision; '' is the working tree. $null when absent.
function Get-RevText {
    param([string]$Path, [string]$Rev)
    if (-not $Rev) {
        $full = Join-Path $AreasProjectRoot ($Path -replace '/', '\')
        if (Test-Path -LiteralPath $full -PathType Leaf) { return [IO.File]::ReadAllText($full) }
        return $null
    }
    return Invoke-GitText @("show", "${Rev}:$Path")
}

# Lines present on one side and not the other, trimmed. A set difference, not
# a diff: enough to ask "did a changed line write Time.timeScale".
function Get-ChangedLines {
    param([string]$Old, [string]$New)
    $a = New-Object 'System.Collections.Generic.HashSet[string]'
    $b = New-Object 'System.Collections.Generic.HashSet[string]'
    if ($Old) { foreach ($l in ($Old -split "`r?`n")) { $t = $l.Trim(); if ($t) { [void]$a.Add($t) } } }
    if ($New) { foreach ($l in ($New -split "`r?`n")) { $t = $l.Trim(); if ($t) { [void]$b.Add($t) } } }
    $out = @()
    foreach ($l in $b) { if (-not $a.Contains($l)) { $out += $l } }
    foreach ($l in $a) { if (-not $b.Contains($l)) { $out += $l } }
    return $out
}

function Get-AreaClasses {
    param([hashtable]$Index, [string[]]$Areas)
    $shared = $SharedFolder.ToLower()
    return @($Index.Keys | Where-Object { $Index[$_].Area -ne $shared -and $Areas -contains $Index[$_].Area })
}

# --- the selection ----------------------------------------------------------
#
# Returns:
#   Full        - [bool] run everything; FullReasons says why
#   FullReasons - [string[]]
#   Classes     - [string[]] selected test classes (both platforms)
#   Files       - one record per path: Path, Kind, Note, Classes, Areas, Trails
#   RepoReaders - [string[]] the always-on EditMode repo readers added
# -BaseRev/-TargetRev pick the two sides of the change: HEAD against the
# working tree by default; "<sha>^"/"<sha>" for one commit (the map-gap report).
function Resolve-GateSelection {
    param(
        [string[]]$Paths,
        [hashtable]$Index,
        [string]$BaseRev = "HEAD",
        [string]$TargetRev = ""
    )

    $g = Get-RefGraph
    $byFile = Get-ClassesByFile -Index $Index
    $hubs = Get-HubTypes
    $classMap = Get-TestClasses -Index $Index
    $fullReasons = @()
    $selected = @()
    $records = @()
    $anyLive = $false

    foreach ($path in $Paths) {
        $rec = [PSCustomObject]@{ Path = $path; Kind = ""; Note = ""; Classes = @(); Areas = @(); Trails = @{} }
        $records += $rec
        $mapRes = Resolve-ChangedPaths -Paths @($path) -Classes $classMap

        if ($mapRes.Ignored.Count -gt 0) { $rec.Kind = "ignored"; continue }
        if ($mapRes.FullSuite.Count -gt 0) {
            $rec.Kind = "full"; $rec.Note = "test infrastructure (the resolver's full-suite tier)"
            $fullReasons += "$path -- $($rec.Note)"; continue
        }
        if ($mapRes.Unmapped.Count -gt 0) {
            $rec.Kind = "full"; $rec.Note = "UNMAPPED: no `$PathAreas row in tools/test_areas.ps1 covers it"
            $fullReasons += "$path -- $($rec.Note)"; continue
        }

        $isCs = $path -match '^Assets/_Project/Scripts/.*\.cs$'
        if (-not $isCs) {
            $rec.Kind = "areas"; $rec.Note = "not C#: serialized/asset links are invisible to a name scan"
            $rec.Areas = @($mapRes.Areas)
            $rec.Classes = @(@(Get-AreaClasses -Index $Index -Areas $mapRes.Areas) + @($mapRes.Classes) | Sort-Object -Unique)
            $selected += $rec.Classes; $anyLive = $true
            continue
        }

        $new = Get-RevText -Path $path -Rev $TargetRev
        $old = Get-RevText -Path $path -Rev $BaseRev
        if ($null -ne $new -and $null -ne $old -and
            [PP.TestSelect.RefGraph]::Normalize($new) -eq [PP.TestSelect.RefGraph]::Normalize($old)) {
            # No test can see it by NAME -- but the repo readers read BYTES:
            # ContentFreshnessTests hashes Domain/Content/** comments and all,
            # so a comment edit there fails it without -BuildContent. They
            # still run (EditMode only), below.
            $rec.Kind = "nothing"; $rec.Note = "comment/whitespace-only edit: no test names it; the repo readers still run"
            $anyLive = $true
            continue
        }
        $anyLive = $true

        $lines = @(Get-ChangedLines -Old $old -New $new)
        $globalHits = @($lines | Where-Object { $_ -match $GlobalStatePattern })
        if ($globalHits.Count -gt 0) {
            $rec.Kind = "full"; $rec.Note = "a changed line writes process-wide engine state: $($globalHits[0])"
            $fullReasons += "$path -- $($rec.Note)"; continue
        }

        $seeds = @()
        if ($null -ne $new) { $seeds += [PP.TestSelect.RefGraph]::DeclaredTypes($new) }
        if ($null -ne $old) { $seeds += [PP.TestSelect.RefGraph]::DeclaredTypes($old) }
        $seeds = @($seeds | Sort-Object -Unique)
        $seedFiles = @()
        if ($g.IndexOf.ContainsKey($path)) { $seedFiles += $g.IndexOf[$path] }

        $closure = $g.Close([string[]]$seeds, [int[]]$seedFiles, $SelectDepth)
        foreach ($f in @($closure.Depth.Keys)) {
            $rel = $g.Rel[$f]
            if (-not $byFile.ContainsKey($rel)) { continue }
            $trail = @()
            $cur = $f
            for ($guard = 0; $guard -lt 20 -and $cur -ge 0; $guard++) {
                $via = $closure.Via[$cur]
                if (-not $via) { break }
                $trail += $via
                $cur = $closure.From[$cur]
            }
            $key = if ($trail.Count -gt 0) { $trail -join ' <- ' } else { "(the changed file itself)" }
            foreach ($c in $byFile[$rel]) {
                $rec.Classes += $c
                if (-not $rec.Trails.ContainsKey($key)) { $rec.Trails[$key] = @() }
                $rec.Trails[$key] += $c
            }
        }
        $rec.Kind = "names"
        $rec.Note = "declares " + $(if ($seeds.Count) { $seeds -join ', ' } else { "no type" })

        $why = @()
        $hubHit = @($seeds | Where-Object { $hubs.ContainsKey($_) })
        if ($hubHit.Count -gt 0) { $why += "hub type " + (($hubHit | ForEach-Object { "$_ (named by $($hubs[$_]) production files)" }) -join ', ') }
        if ($path -match $ScenePathPattern) { $why += "screen tree / scene wiring" }
        if (@($lines | Where-Object { $_ -match '\bSerializeField\b' }).Count -gt 0) { $why += "a [SerializeField] line changed" }
        if ($why.Count -gt 0) {
            $rec.Areas = @($mapRes.Areas)
            $rec.Classes += @(Get-AreaClasses -Index $Index -Areas $mapRes.Areas) + @($mapRes.Classes)
            $rec.Note += "; + area map ($($mapRes.Areas -join '+')) for " + ($why -join '; ')
        }
        $rec.Classes = @($rec.Classes | Sort-Object -Unique)
        $selected += $rec.Classes
    }

    $readers = @()
    if ($anyLive -and $fullReasons.Count -eq 0) {
        $readers = @(Get-RepoReaderClasses -ClassesByFile $byFile)
        $selected += $readers
    }

    return [PSCustomObject]@{
        Full        = ($fullReasons.Count -gt 0)
        FullReasons = @($fullReasons)
        Classes     = @($selected | Where-Object { $_ } | Sort-Object -Unique)
        Files       = @($records)
        RepoReaders = @($readers)
    }
}

# The reason trail: file -> kind -> (trail -> classes), then what is skipped.
function Write-GateSelection {
    param($Selection, [hashtable]$Index)

    $ignored = @($Selection.Files | Where-Object { $_.Kind -eq "ignored" })
    Write-Host "Changed files ($(@($Selection.Files).Count), $($ignored.Count) of them ignored):"
    foreach ($r in $Selection.Files) {
        switch ($r.Kind) {
            "ignored" { if ($ignored.Count -le 10) { Write-Host "  $($r.Path) -> ignored (cannot affect a test)" } }
            "nothing" { Write-Host "  $($r.Path) -> nothing: $($r.Note)" }
            "full"    { Write-Host "  $($r.Path) -> FULL: $($r.Note)" }
            "areas"   { Write-Host "  $($r.Path) -> areas $($r.Areas -join '+') ($(@($r.Classes).Count) classes): $($r.Note)" }
            "names"   {
                Write-Host "  $($r.Path) -> $(@($r.Classes).Count) classes; $($r.Note)"
                foreach ($k in ($r.Trails.Keys | Sort-Object)) {
                    $names = @($r.Trails[$k] | Sort-Object -Unique)
                    $shown = ($names | Select-Object -First 8) -join ', '
                    $more = if ($names.Count -gt 8) { ", +$($names.Count - 8) more" } else { "" }
                    Write-Host ("      name {0}: {1} -- {2}{3}" -f $k, $names.Count, $shown, $more)
                }
            }
        }
    }
    if ($ignored.Count -gt 10) {
        $top = @($ignored | ForEach-Object { ($_.Path -split '/')[0] } | Group-Object | Sort-Object Count -Descending | ForEach-Object { "$($_.Name) ($($_.Count))" })
        Write-Host "  + $($ignored.Count) ignored (docs, .meta, and anything outside Assets/Packages/ProjectSettings/tools): $($top -join ', ')"
    }
    if (@($Selection.RepoReaders).Count -gt 0) {
        Write-Host "  + $(@($Selection.RepoReaders).Count) EditMode repo readers (lints/pins that read files off disk; run on any change)"
    }
    if ($Selection.Full) { return }

    $shared = $SharedFolder.ToLower()
    $all = @($Index.Keys | Where-Object { $Index[$_].Area -ne $shared })
    $sel = @{}
    foreach ($c in $Selection.Classes) { $sel[$c] = $true }
    Write-Host ""
    Write-Host "By area (selected / all):"
    $skippedAreas = @()
    foreach ($a in $AreaNames) {
        $inArea = @($all | Where-Object { $Index[$_].Area -eq $a })
        $e = @($inArea | Where-Object { $Index[$_].Platform -eq "EditMode" })
        $p = @($inArea | Where-Object { $Index[$_].Platform -eq "PlayMode" })
        $es = @($e | Where-Object { $sel.ContainsKey($_) }).Count
        $ps = @($p | Where-Object { $sel.ContainsKey($_) }).Count
        $tag = ""
        if ($es + $ps -eq 0) { $tag = "  SKIPPED"; $skippedAreas += $a }
        Write-Host ("  {0,-8} EditMode {1,3}/{2,-3}  PlayMode {3,3}/{4,-3}{5}" -f $a, $es, $e.Count, $ps, $p.Count, $tag)
    }
    $skipped = @($all | Where-Object { -not $sel.ContainsKey($_) })
    $skE = @($skipped | Where-Object { $Index[$_].Platform -eq "EditMode" }).Count
    $skAreas = if ($skippedAreas.Count) { $skippedAreas -join ', ' } else { "none" }
    Write-Host ("Skipped {0} of {1} classes ({2} EditMode, {3} PlayMode) -- trusting the selection for those. Areas skipped entirely: {4}" -f $skipped.Count, $all.Count, $skE, ($skipped.Count - $skE), $skAreas)
}

# --- the safety net ---------------------------------------------------------

function Read-LastFullGreen {
    if (-not (Test-Path $LastFullGreenFile)) { return $null }
    try {
        $parts = ((Get-Content $LastFullGreenFile -Raw).Trim() -split '\s+')
        if ($parts.Count -lt 2 -or $parts[0] -notmatch '^[0-9a-f]{7,40}$') { return $null }
        $styles = [Globalization.DateTimeStyles]::AdjustToUniversal -bor [Globalization.DateTimeStyles]::AssumeUniversal
        $utc = [DateTime]::Parse($parts[1], [Globalization.CultureInfo]::InvariantCulture, $styles)
        return [PSCustomObject]@{ Sha = $parts[0]; Utc = $utc }
    } catch { return $null }
}

# HEAD at the time of a green FULL run, and when. Per machine, gitignored.
function Save-LastFullGreen {
    $sha = Invoke-GitText @("rev-parse", "HEAD")
    if (-not $sha) { return }
    $line = "{0} {1}" -f $sha.Trim(), [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", [Globalization.CultureInfo]::InvariantCulture)
    [IO.File]::WriteAllText($LastFullGreenFile, $line + "`n", (New-Object Text.ASCIIEncoding))
    Write-Host "Full run green: recorded $line -> $LastFullGreenFile"
}

# $null when a slice is allowed; otherwise the rule that fired, as a sentence.
function Get-FullRunPromotion {
    $rec = Read-LastFullGreen
    if (-not $rec) { return "no record of a green full run on this machine ($LastFullGreenFile missing or unreadable)" }
    $short = $rec.Sha.Substring(0, [Math]::Min(8, $rec.Sha.Length))
    & git -C $AreasProjectRoot merge-base --is-ancestor $rec.Sha HEAD 2>$null
    if ($LASTEXITCODE -ne 0) { return "the last full green ($short) is not an ancestor of HEAD" }
    $n = [int]((Invoke-GitText @("rev-list", "--count", "$($rec.Sha)..HEAD")).Trim())
    if ($n -gt $LastGreenMaxCommits) { return "$n commits since the last full green ($short); the limit is $LastGreenMaxCommits" }
    $hours = ([DateTime]::UtcNow - $rec.Utc).TotalHours
    if ($hours -gt $LastGreenMaxHours) { return ("the last full green ({0}) was {1:N1}h ago; the limit is {2}h" -f $short, $hours, $LastGreenMaxHours) }
    Write-Host ("Safety net: last full green {0}, {1} commit(s) and {2:N1}h ago -- a slice is allowed (full after {3} commits or {4}h)." -f $short, $n, $hours, $LastGreenMaxCommits, $LastGreenMaxHours)
    return $null
}

# --- map-gap feedback -------------------------------------------------------

# Class names out of NUnit result files: the failing cases' fixture segment.
function Get-FailingClasses {
    param([string[]]$ResultPaths, [hashtable]$Index)
    $out = @()
    foreach ($p in $ResultPaths) {
        if (-not (Test-Path $p)) { continue }
        $doc = [xml](Get-Content $p -Raw)
        foreach ($tc in $doc.SelectNodes("//test-case[@result='Failed']")) {
            $segs = (($tc.GetAttribute("fullname") -split '\(')[0]) -split '\.'
            for ($k = $segs.Count - 1; $k -ge 0; $k--) {
                if ($Index.ContainsKey($segs[$k])) { $out += $segs[$k]; break }
            }
        }
    }
    return @($out | Sort-Object -Unique)
}

# For each class that failed a FULL run: which changes since the last full
# green would a -Changed run have skipped it for? Those are the evidence that a
# selection rule or a $PathAreas row is too narrow. The selection is re-run per
# commit against the CURRENT reference graph -- what the gate would select
# today for that commit's files, not a replay of what it did then.
function Write-MapGapReport {
    param([string[]]$FailingClasses, [hashtable]$Index)

    Write-Host ""
    Write-Host "MAP-GAP REPORT -- $(@($FailingClasses).Count) failing class(es)"
    if (@($FailingClasses).Count -eq 0) { Write-Host "  (no failing class could be named from the results)"; return }
    $rec = Read-LastFullGreen
    if (-not $rec) {
        Write-Host "  No last-full-green record, so there is no window to search. Failing on a full run:"
        foreach ($c in $FailingClasses) { Write-Host "    $c ($($Index[$c].Area))" }
        return
    }

    $units = @()
    $revList = Invoke-GitText @("rev-list", "--reverse", "$($rec.Sha)..HEAD")
    $shas = @(($revList -split "`n") | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    foreach ($sha in $shas) {
        $subject = ((Invoke-GitText @("log", "-1", "--format=%s", $sha)) -split "`n")[0]
        $files = @(((Invoke-GitText @("diff", "--name-only", "$sha^", $sha)) -split "`n") | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        $units += [PSCustomObject]@{ Label = "$($sha.Substring(0, 8)) $subject"; Base = "$sha^"; Target = $sha; Files = $files }
    }
    $wt = @(Get-ChangedFiles)
    if ($wt.Count -gt 0) { $units += [PSCustomObject]@{ Label = "(uncommitted)"; Base = "HEAD"; Target = ""; Files = $wt } }
    $tail = if ($wt.Count) { " + the working tree" } else { "" }
    Write-Host "  Window: last full green $($rec.Sha.Substring(0, 8))..HEAD, $($shas.Count) commit(s)$tail"

    foreach ($u in $units) {
        $sel = Resolve-GateSelection -Paths $u.Files -Index $Index -BaseRev $u.Base -TargetRev $u.Target
        $u | Add-Member -NotePropertyName Sel -NotePropertyValue $sel -Force
    }
    $classMap = Get-TestClasses -Index $Index

    foreach ($c in $FailingClasses) {
        $area = if ($Index.ContainsKey($c)) { $Index[$c].Area } else { "?" }
        Write-Host ""
        Write-Host "  $c (area $area)"
        $gaps = 0
        foreach ($u in $units) {
            if ($u.Sel.Full) { continue }
            if (@($u.Sel.Classes) -contains $c) { continue }
            $live = @($u.Sel.Files | Where-Object { $_.Kind -eq "names" -or $_.Kind -eq "areas" })
            if ($live.Count -eq 0) { continue }
            $gaps++
            Write-Host "    skipped by $($u.Label):"
            foreach ($r in $live) {
                $areas = @((Resolve-ChangedPaths -Paths @($r.Path) -Classes $classMap).Areas)
                $mapText = if ($areas.Count -eq 0) { "none (a test file maps to its own classes)" }
                           elseif ($areas -contains $area) { "$($areas -join '+') (includes $area)" }
                           else { "$($areas -join '+') (EXCLUDES $area)" }
                Write-Host "      $($r.Path) -> $($r.Kind), $(@($r.Classes).Count) classes; area map: $mapText"
            }
        }
        if ($gaps -eq 0) {
            Write-Host "    every change in the window selected it or ran full: not a selection gap (a flake, or order-dependence on a fixture a slice did not run)"
        }
    }
    Write-Host ""
    Write-Host "  A change listed above is where the gate trusted a one-hop name scan or a map row that did not reach the class. Fix the rule, not the class."
}
