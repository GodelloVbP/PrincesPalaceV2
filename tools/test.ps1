param(
    [Parameter(Position = 0)]
    [string]$Filter = "",
    [switch]$SkipSync,
    [switch]$List,
    [switch]$Full,
    [switch]$Changed
)

# The FAST path. Runs a slice of the suite instead of all of it.
#
# Measured, because the shape of the problem is not what it looks like.
# Re-measure rather than trust these numbers if it's been a while --
# they drift as the suite grows, and they HAVE drifted twice already
# (originally 335/316 tests across 37 PlayMode classes; then 655/424 as of
# the workflow-standards restructure, 2026-08-01; the numbers below are from
# the test-area overhaul, 2026-08-05):
#
#     robocopy sync, both runners  ~0.3s   negligible
#     Unity boot + import          ~6s     a fixed floor, per platform
#     EditMode, all 690 tests      ~0.5s
#     PlayMode, all 505 tests      ~85-90s <- the whole cost
#     ---------------------------------
#     full parallel run            ~90-100s wall (PlayMode dominates; they run concurrently)
#
# Two things follow. EditMode is already AT the floor -- 690 tests cost half
# a second, so there is nothing to split there and never will be. And
# PlayMode's cost is spread across roughly 60 classes with no dominant
# hotspot, so carving it into fixed "fast" and "slow" suites would not help
# either (this was investigated for real -- see the -Split section of the
# 2026-08-05 test-area plan; deferred, not abandoned, as the saving from
# splitting PlayMode across both runner copies works out to ~15s against the
# machinery it costs).
#
# What does help is running only the classes you are actually working on, and
# only the PLATFORM they live on. WoolTests alone is ~12s against ~90-100s
# for the full run, and most of that 12s is Unity starting up. Better still,
# `tools/test.ps1 -Changed` picks the right slice FOR you from whatever is
# sitting uncommitted -- see its own section below.
#
# This is for the edit-run-edit loop. Before committing, run the full thing:
#     powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1
# ...which now REFUSES to run at all if any test class has drifted outside
# every area or out of discovery's sight -- see tools/test_areas.ps1.
#
# Usage:
#     tools/test.ps1 wool            one class, platform auto-detected
#     tools/test.ps1 combat          a named area (see $Areas below;
#                                    also: hub, content, run, ui, art, rng)
#     tools/test.ps1 Wool,Spell      several, comma-separated
#     tools/test.ps1 -Changed        just what your uncommitted changes touch
#     tools/test.ps1 -List           what is available (classes, areas,
#                                    ORPHANS, discovery blind spots)
#     tools/test.ps1 -Full           everything, same as run_tests_parallel

# Derived, not hardcoded - see the same block in run_tests_parallel.ps1 for
# what the hardcoded form did when this project was copied.
$SourceProject = Split-Path $PSScriptRoot -Parent
$ProjectLeaf = Split-Path $SourceProject -Leaf
$ProjectParent = Split-Path $SourceProject -Parent
$RunnerProduct = $ProjectLeaf -replace '[^A-Za-z0-9]',''
. (Join-Path $PSScriptRoot "unity_path.ps1")
$UnityExe = Get-UnityExe

$RunnerFor = @{
    EditMode = @{ Path = (Join-Path $ProjectParent "$ProjectLeaf-TestRunner");  Product = "${RunnerProduct}TestRunner" }
    PlayMode = @{ Path = (Join-Path $ProjectParent "$ProjectLeaf-TestRunner2"); Product = "${RunnerProduct}TestRunner2" }
}

# $Areas, Get-TestClasses, Get-AreaOrphans, Get-DiscoveryBlindSpots all live
# in test_areas.ps1 now -- shared with run_tests_parallel.ps1's orphan gate,
# so there is exactly one place that knows what an "area" is.
. (Join-Path $PSScriptRoot "test_areas.ps1")

$classes = Get-TestClasses

if ($List) {
    Write-Host "`nTest classes by platform:`n"
    foreach ($platform in @("EditMode", "PlayMode")) {
        $names = $classes.Keys | Where-Object { $classes[$_] -eq $platform } | Sort-Object
        Write-Host "  $platform ($($names.Count))"
        foreach ($n in $names) { Write-Host "    $n" }
        Write-Host ""
    }
    Write-Host "Named areas:`n"
    foreach ($area in $Areas.Keys | Sort-Object) {
        $hits = ($classes.Keys | Where-Object { $_ -match $Areas[$area] } | Sort-Object) -join ", "
        Write-Host "  $area"
        Write-Host "    $hits`n"
    }

    # A class matching no area is invisible to every area-based run, and a
    # file whose class discovery never even saw is invisible to the orphan
    # check ITSELF -- so both are reported here, and both fail
    # run_tests_parallel.ps1 outright (see that script's own gate). -List
    # stays report-only and exits 0 even when either list is non-empty; it
    # is the diagnosis, not the enforcement.
    $orphans = Get-AreaOrphans -ClassNames $classes.Keys
    Write-Host "ORPHANS (no area):"
    if ($orphans) {
        foreach ($o in $orphans) { Write-Host "  $o" }
    } else {
        Write-Host "  (none)"
    }
    Write-Host ""

    $blindSpots = Get-DiscoveryBlindSpots -Classes $classes
    Write-Host "Discovery blind spots (a [Test]/[UnityTest] file whose class was never discovered):"
    if ($blindSpots) {
        foreach ($b in $blindSpots) { Write-Host "  $b" }
    } else {
        Write-Host "  (none)"
    }
    Write-Host ""

    Write-Host "Changed-file path map (for -Changed), first match wins:`n"
    foreach ($entry in $PathAreas) {
        Write-Host "  $($entry.Pattern)  ->  $($entry.Areas -join '+')"
    }
    Write-Host ""

    Write-Host "Usage: tools/test.ps1 <class-or-area>   e.g. tools/test.ps1 wool"
    Write-Host "       tools/test.ps1 -Changed          just the areas affected by uncommitted changes"
    exit 0
}

# Renamed from the automatic $args below -- shadowing it silently worked via
# splatting but is fragile, and this block was already being touched.
function Invoke-FullSuite {
    Write-Host "Running the FULL suite (tools/run_tests_parallel.ps1)..."
    $forwardArgs = @()
    if ($SkipSync) { $forwardArgs += "-SkipSync" }
    & (Join-Path $PSScriptRoot "run_tests_parallel.ps1") @forwardArgs
    exit $LASTEXITCODE
}

if ($Full) {
    Invoke-FullSuite
}

if ($Changed -and $Filter) {
    Write-Host "Pick one: -Changed or a positional filter, not both."
    exit 1
}

# --- -Changed: map uncommitted files to areas/classes ----------------------
#
# Falls into the SAME pipeline every other selection uses below -- this only
# ever decides what goes into `$wanted`, never how it is synced or run.
$changedWanted = $null
if ($Changed) {
    $changedFiles = Get-ChangedFiles
    if (-not $changedFiles) {
        Write-Host "No uncommitted changes."
        exit 0
    }

    $resolution = Resolve-ChangedPaths -Paths $changedFiles -Classes $classes

    Write-Host "Changed files:`n"
    foreach ($line in $resolution.Mapping) { Write-Host "  $line" }
    Write-Host ""

    # Unmapped fails LOUDLY rather than silently running a subset -- the same
    # reasoning as the area-orphan gate, and for the same reason: a -Changed
    # run that quietly tests less than the change actually touched is worse
    # than one that refuses to guess. Exit code 2, distinct from a genuine
    # test failure (1), so a caller can tell "fix the map" apart from
    # "fix the code".
    if ($resolution.Unmapped.Count -gt 0) {
        Write-Host "UNMAPPED ($($resolution.Unmapped.Count)) -- no `$PathAreas entry covers these:"
        foreach ($u in $resolution.Unmapped) { Write-Host "  $u" }
        Write-Host "`nAdd an entry in tools/test_areas.ps1's `$PathAreas, or run tools/test.ps1 -Full."
        exit 2
    }

    if ($resolution.FullSuite.Count -gt 0) {
        Write-Host "$($resolution.FullSuite.Count) changed file(s) affect test infrastructure itself:"
        foreach ($f in $resolution.FullSuite) { Write-Host "  $f" }
        Write-Host "Running the FULL suite instead of a slice -- a change here must not be validated through its own filtering.`n"
        Invoke-FullSuite
    }

    $changedWanted = @()
    foreach ($area in $resolution.Areas) {
        $pattern = $Areas[$area]
        $changedWanted += $classes.Keys | Where-Object { $_ -match $pattern }
    }
    $changedWanted += $resolution.Classes

    if (-not $changedWanted) {
        Write-Host "Changes map to no tests."
        exit 0
    }
}

if (-not $Changed -and -not $Filter) {
    Invoke-FullSuite
}

# --- resolve what was asked for into concrete classes -----------------------
$wanted = @()
if ($Changed) {
    $wanted = $changedWanted
} else {
    foreach ($term in $Filter -split ',') {
        $term = $term.Trim()
        if (-not $term) { continue }

        if ($Areas.ContainsKey($term.ToLower())) {
            $pattern = $Areas[$term.ToLower()]
            $wanted += $classes.Keys | Where-Object { $_ -match $pattern }
            continue
        }

        # Otherwise treat it as a substring of a class name, case-insensitively,
        # so "wool" finds WoolTests without anyone typing the suffix or the
        # namespace.
        $hits = $classes.Keys | Where-Object { $_ -like "*$term*" }
        if (-not $hits) {
            Write-Host "No test class or area matches '$term'."
            Write-Host "Try: tools/test.ps1 -List"
            exit 1
        }
        $wanted += $hits
    }
}

$wanted = $wanted | Sort-Object -Unique
$platforms = $wanted | ForEach-Object { $classes[$_] } | Sort-Object -Unique

Write-Host "Matched $($wanted.Count) class(es): $($wanted -join ', ')"
Write-Host "Platform(s): $($platforms -join ', ')`n"

# --- sync ------------------------------------------------------------------
# Only the copies actually about to run. Syncing the other one is ~0.15s, but
# it also risks clobbering generated content in a runner this invocation is
# not going to rebuild.
if (-not $SkipSync) {
    foreach ($platform in $platforms) {
        $runner = $RunnerFor[$platform]
        robocopy "$SourceProject\Assets" "$($runner.Path)\Assets" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        robocopy "$SourceProject\Packages" "$($runner.Path)\Packages" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        robocopy "$SourceProject\ProjectSettings" "$($runner.Path)\ProjectSettings" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null

        $settingsPath = Join-Path $runner.Path "ProjectSettings\ProjectSettings.asset"
        (Get-Content $settingsPath -Raw) -replace "productName: .*", "productName: $($runner.Product)" |
            Set-Content $settingsPath -Encoding utf8
    }
}

# --- run -------------------------------------------------------------------
# Anchored on the class segment (".<Class>."), so a preset for "combat" cannot
# also drag in an unrelated class that merely has "Fight" in a METHOD name.
# Unity matches -testFilter against the full namespace.class.method.
$procs = @{}
foreach ($platform in $platforms) {
    $runner = $RunnerFor[$platform]
    $onThis = $wanted | Where-Object { $classes[$_] -eq $platform }
    $pattern = ".*\.(" + (($onThis | ForEach-Object { [regex]::Escape($_) }) -join "|") + ")\..*"

    $resultsPath = Join-Path $runner.Path "test-results-$platform.xml"
    # Hoisted rather than inlined into the argument list: a Join-Path with its
    # own double quotes, inside a subexpression, inside a double-quoted string
    # is a parse error in PowerShell 5.1.
    $runLogPath = Join-Path $runner.Path "test-run-$platform.log"
    if (Test-Path $resultsPath) { Remove-Item $resultsPath -Force }

    $procs[$platform] = Start-Process -FilePath $UnityExe -ArgumentList @(
        "-batchmode", "-nographics", "-silent-crashes",
        "-projectPath", "`"$($runner.Path)`"",
        "-runTests", "-testPlatform", $platform,
        "-testFilter", "`"$pattern`"",
        "-testResults", "`"$resultsPath`"",
        "-logFile", "`"$runLogPath`"",
        "-buildTarget", "StandaloneWindows64"
    ) -PassThru -NoNewWindow
}

$procs.Values | Wait-Process -Timeout 1800

$allPassed = $true
$totalRun = 0
foreach ($platform in $platforms) {
    $runner = $RunnerFor[$platform]
    $resultsPath = Join-Path $runner.Path "test-results-$platform.xml"
    $logPath = Join-Path $runner.Path "test-run-$platform.log"

    if (-not (Test-Path $resultsPath)) {
        Write-Host "No results for $platform. Tail of log:"
        Get-Content $logPath -Tail 40 | ForEach-Object { Write-Host $_ }
        $allPassed = $false
        continue
    }

    [xml]$results = Get-Content $resultsPath
    $root = $results.'test-run'
    $totalRun += [int]$root.total
    Write-Host "$platform -- Total: $($root.total)  Passed: $($root.passed)  Failed: $($root.failed)  Skipped: $($root.skipped)  Duration: $($root.duration)s"

    foreach ($f in $results.SelectNodes("//test-case[@result='Failed']")) {
        Write-Host "`nFAILED: $($f.fullname)"
        if ($f.failure -and $f.failure.message) { Write-Host $f.failure.message.InnerText }
    }

    if ([int]$root.failed -ne 0) { $allPassed = $false }
}

# A filter that matches a class but selects no tests is almost always a typo
# that would otherwise read as a pass.
if ($totalRun -eq 0) {
    Write-Host "`nNo tests actually ran. The filter matched a class but no test inside it."
    exit 1
}

if ($allPassed) {
    Write-Host "`nPassed. This was a SLICE -- run tools/run_tests_parallel.ps1 before committing."
    exit 0
}

Write-Host "`nSome tests failed."
exit 1
