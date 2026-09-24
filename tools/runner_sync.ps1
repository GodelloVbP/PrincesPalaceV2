# tools/runner_sync.ps1 -- mirror main into N isolated runner copies at once.
#
# Dot-sourced by run_tests_parallel.ps1. Never invoked directly.
#
# Pure ASCII, no BOM (CLAUDE.md's PowerShell gotcha).
#
# WHY THIS FILE EXISTS. The gate's sync used to cost ~53s for two copies
# (23s + 30s, one after the other), and robocopy was not the cost: a no-change
# /MIR of Assets/ is ~0.07s. The cost was Repair-Metas calling Get-FileHash
# twice per .meta across ~4,600 of them -- ~9,200 cmdlet invocations per
# runner, each paying PowerShell's per-call overhead to hash a 200-byte file.
# The comparison is now one compiled pass (length first, then bytes, in
# parallel), and the runners sync concurrently, each with its own robocopy.
#
# BEHAVIOUR IS UNCHANGED from the PowerShell loop it replaced:
#   - only a .meta that exists in BOTH trees and differs is touched;
#   - it is overwritten with main's bytes and stamped with the current time,
#     and so is its asset (when the asset is a file, not a folder), so Unity
#     reimports and re-binds it. Why both stamps: see Repair-Metas' header
#     comment in run_tests_parallel.ps1.
# Byte equality is what hash equality was standing in for.
#
# GUID CHECK. Same move for Assert-GuidsMatch: it ran Select-String twice per
# .meta. Get-GuidMismatches reads each file's first "guid:" line in the same
# compiled pass. Select-String is case-insensitive by default, and so is this.

if (-not ("PP.RunnerSync.MetaSync" -as [type])) {
    # C# 5 only: Add-Type in Windows PowerShell 5.1 compiles with the .NET
    # Framework csc, which has no string interpolation or expression bodies.
    Add-Type -Language CSharp -TypeDefinition @"
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PP.RunnerSync
{
    public static class MetaSync
    {
        static readonly Regex GuidLine = new Regex(@"^guid:\s*([0-9a-f]{32})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static string Trim(string p) { return p.TrimEnd('\\', '/'); }

        public static bool SameBytes(string a, string b)
        {
            FileInfo fa = new FileInfo(a);
            FileInfo fb = new FileInfo(b);
            if (!fa.Exists || !fb.Exists) return false;
            if (fa.Length != fb.Length) return false;
            byte[] x = File.ReadAllBytes(a);
            byte[] y = File.ReadAllBytes(b);
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) { if (x[i] != y[i]) return false; }
            return true;
        }

        // Returns the relative paths repaired, sorted.
        public static string[] Repair(string srcAssets, string dstAssets)
        {
            srcAssets = Trim(srcAssets);
            dstAssets = Trim(dstAssets);
            string[] metas = Directory.GetFiles(srcAssets, "*.meta", SearchOption.AllDirectories);
            ConcurrentBag<string> repaired = new ConcurrentBag<string>();
            ParallelOptions opts = new ParallelOptions();
            opts.MaxDegreeOfParallelism = Environment.ProcessorCount;
            Parallel.ForEach(metas, opts, src =>
            {
                string rel = src.Substring(srcAssets.Length + 1);
                string dst = Path.Combine(dstAssets, rel);
                if (!File.Exists(dst)) return;
                if (SameBytes(src, dst)) return;

                FileAttributes attrs = File.GetAttributes(dst);
                if ((attrs & FileAttributes.ReadOnly) != 0) File.SetAttributes(dst, attrs & ~FileAttributes.ReadOnly);
                File.Copy(src, dst, true);
                DateTime now = DateTime.Now;
                File.SetLastWriteTime(dst, now);
                string asset = dst.Substring(0, dst.Length - ".meta".Length);
                if (File.Exists(asset)) File.SetLastWriteTime(asset, now);
                repaired.Add(rel);
            });
            List<string> list = new List<string>(repaired);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list.ToArray();
        }

        static string ReadGuid(string metaPath)
        {
            foreach (string line in File.ReadLines(metaPath))
            {
                Match m = GuidLine.Match(line);
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }

        // "<relative> : main=<guid> <label>=<guid>" for every .meta present in
        // both trees whose guid differs. Sorted.
        public static string[] GuidMismatches(string srcAssets, string dstAssets, string label)
        {
            srcAssets = Trim(srcAssets);
            dstAssets = Trim(dstAssets);
            string[] metas = Directory.GetFiles(srcAssets, "*.meta", SearchOption.AllDirectories);
            ConcurrentBag<string> bad = new ConcurrentBag<string>();
            ParallelOptions opts = new ParallelOptions();
            opts.MaxDegreeOfParallelism = Environment.ProcessorCount;
            Parallel.ForEach(metas, opts, src =>
            {
                string rel = src.Substring(srcAssets.Length + 1);
                string dst = Path.Combine(dstAssets, rel);
                if (!File.Exists(dst)) return;
                string a = ReadGuid(src);
                string b = ReadGuid(dst);
                if (a != null && b != null && !string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                    bad.Add(rel + " : main=" + a + " " + label + "=" + b);
            });
            List<string> list = new List<string>(bad);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list.ToArray();
        }

        // Names every file under dirA/dirB (non-recursive, by pattern) that is
        // missing on one side or differs by a single byte. Empty = identical.
        public static string[] DiffDirs(string dirA, string dirB, string pattern)
        {
            List<string> diffs = new List<string>();
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(dirA)) foreach (string f in Directory.GetFiles(dirA, pattern)) names.Add(Path.GetFileName(f));
            if (Directory.Exists(dirB)) foreach (string f in Directory.GetFiles(dirB, pattern)) names.Add(Path.GetFileName(f));
            List<string> sorted = new List<string>(names);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string n in sorted)
            {
                string a = Path.Combine(dirA, n);
                string b = Path.Combine(dirB, n);
                if (!File.Exists(a)) { diffs.Add(n + " : missing in " + dirA); continue; }
                if (!File.Exists(b)) { diffs.Add(n + " : missing in " + dirB); continue; }
                if (!SameBytes(a, b)) diffs.Add(n + " : bytes differ");
            }
            return diffs.ToArray();
        }
    }
}
"@
}

# WHY THE META REPAIR EXISTS AT ALL (moved here with the code; this was
# Repair-Metas' header in run_tests_parallel.ps1):
#
# /MIR skips a file whose size AND timestamp match the destination's, and
# two .meta files for the same asset are the same size to the byte while
# holding DIFFERENT GUIDs. When two isolated copies independently import
# or generate the same new asset around the same moment, each invents its
# own GUID, and from then on the mirror cannot tell the copies apart and
# never corrects the odd one out.
#
# Copying the right bytes over is NOT enough on its own: robocopy
# preserves the source timestamp, and Unity, seeing a .meta no newer than
# the one its Library was built from, keeps the stale GUID mapping. So the
# repaired file is stamped with the current time to force a reimport, and
# its asset is stamped with it so the asset itself is re-bound.
#
# Only genuinely differing files are touched -- stamping every .meta each
# run would reimport the entire project every time.
#
# The symptom this prevents: a component plainly present in the scene file
# and invisible to FindObjectsByType, because the scene was built against
# the other runner's GUID -- or, for generated content, a talent whose
# prerequisite silently resolves to null because its OWN runner's copy of
# the prerequisite kept a stale GUID the referencing asset no longer uses.
# Cost an afternoon, twice; cost a 3-test PlayMode failure the third time,
# the day the talent tree grew from 30 nodes to 150 and the odds of two
# same-size .meta files landing on the same timestamp stopped being rare.

# Runs $Script once per runner, all at once, each in its own runspace of THIS
# process (so the compiled type above is visible to every one of them), and
# returns their outputs in runner order. Runspaces rather than Start-Job: a job
# is a whole new powershell.exe that would have to recompile the helper.
#
# $Script receives ($Runner, $Shared) and must RETURN its result rather than
# Write-Host it -- a runspace's host output goes nowhere. An exception inside
# one is rethrown here with the runner's path on it.
function Invoke-PerRunner {
    param(
        [Parameter(Mandatory = $true)][array]$Runners,
        [Parameter(Mandatory = $true)][scriptblock]$Script,
        [hashtable]$Shared = @{}
    )

    $pool = [runspacefactory]::CreateRunspacePool(1, [Math]::Max(1, $Runners.Count))
    $pool.Open()
    $jobs = @()
    try {
        foreach ($r in $Runners) {
            $ps = [powershell]::Create()
            $ps.RunspacePool = $pool
            [void]$ps.AddScript($Script.ToString()).AddArgument($r).AddArgument($Shared)
            $jobs += [pscustomobject]@{ Runner = $r; PS = $ps; Handle = $ps.BeginInvoke() }
        }
        $out = @()
        foreach ($j in $jobs) {
            $result = $j.PS.EndInvoke($j.Handle)
            if ($j.PS.Streams.Error.Count -gt 0) {
                throw "runner $($j.Runner.Path): $($j.PS.Streams.Error[0])"
            }
            $out += , ($result | Select-Object -Last 1)
        }
        return $out
    }
    finally {
        foreach ($j in $jobs) { $j.PS.Dispose() }
        $pool.Close()
        $pool.Dispose()
    }
}

# The per-runner body of the pre-test sync. Same four mirrors and the same
# productName rewrite Sync-Runner always did, in the same order, now timed.
$SyncRunnerScript = {
    param($Runner, $Shared)
    $src = $Shared.Source
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $codes = @{}

    robocopy "$src\Assets" "$($Runner.Path)\Assets" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    $codes["Assets"] = $LASTEXITCODE
    $repaired = [PP.RunnerSync.MetaSync]::Repair("$src\Assets", "$($Runner.Path)\Assets")

    robocopy "$src\Packages" "$($Runner.Path)\Packages" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    $codes["Packages"] = $LASTEXITCODE
    robocopy "$src\ProjectSettings" "$($Runner.Path)\ProjectSettings" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    $codes["ProjectSettings"] = $LASTEXITCODE
    # docs/: ContentSchemaTests reads docs/CONTENT_SCHEMA.md out of the copy.
    # See Sync-Runner's comment history in run_tests_parallel.ps1.
    robocopy "$src\docs" "$($Runner.Path)\docs" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    $codes["docs"] = $LASTEXITCODE

    $settingsPath = Join-Path $Runner.Path "ProjectSettings\ProjectSettings.asset"
    (Get-Content $settingsPath -Raw) -replace "productName: .*", "productName: $($Runner.Product)" |
        Set-Content $settingsPath -Encoding utf8

    # robocopy: 0-7 is success of some kind, 8+ means something failed to copy.
    $failed = @($codes.Keys | Where-Object { $codes[$_] -ge 8 } | ForEach-Object { "$_ (robocopy exit $($codes[$_]))" })
    return [pscustomobject]@{
        Path     = $Runner.Path
        Label    = $Runner.Label
        Seconds  = $sw.Elapsed.TotalSeconds
        Repaired = @($repaired)
        Failed   = $failed
    }
}

# The per-runner body of the post-generation fan-out: re-mirror main, then the
# primary's freshly built scenes (main only has them when -BuildScenes synced
# them back, and the mirror just overwrote this copy's), then the meta repair
# and the GUID check. The caller decides what a mismatch means.
$FanOutRunnerScript = {
    param($Runner, $Shared)
    $src = $Shared.Source
    $sw = [Diagnostics.Stopwatch]::StartNew()

    robocopy "$src\Assets" "$($Runner.Path)\Assets" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    $code = $LASTEXITCODE
    $sceneCode = 0
    if ($Shared.CopyScenes) {
        robocopy "$($Shared.Primary)\Assets\_Project\Scenes" "$($Runner.Path)\Assets\_Project\Scenes" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        $sceneCode = $LASTEXITCODE
    }
    $repaired = [PP.RunnerSync.MetaSync]::Repair("$src\Assets", "$($Runner.Path)\Assets")
    $mismatches = [PP.RunnerSync.MetaSync]::GuidMismatches("$src\Assets", "$($Runner.Path)\Assets", $Runner.Label)

    $failed = @()
    if ($code -ge 8) { $failed += "Assets (robocopy exit $code)" }
    if ($sceneCode -ge 8) { $failed += "Scenes (robocopy exit $sceneCode)" }
    return [pscustomobject]@{
        Path       = $Runner.Path
        Label      = $Runner.Label
        Seconds    = $sw.Elapsed.TotalSeconds
        Repaired   = @($repaired)
        Mismatches = @($mismatches)
        Failed     = $failed
    }
}
