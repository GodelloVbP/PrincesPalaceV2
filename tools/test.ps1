param(
    [Parameter(Position = 0)]
    [string]$Filter = "",
    [switch]$SkipSync,
    [switch]$List,
    [switch]$Full,
    [switch]$Changed,
    [switch]$Unity,
    [switch]$SelfCheck
)

# The FAST path. Runs a slice of the suite instead of all of it, and where it
# can, runs that slice without Unity at all.
#
# Measured, because the shape of the problem is not what it looks like.
# Re-measure rather than trust these numbers if it's been a while -- they
# drift as the suite grows, and they HAVE drifted three times already
# (335/316 originally; 655/424 at the workflow-standards restructure,
# 2026-08-01; 690/505 at the test-area overhaul, 2026-08-05). The numbers
# below are from the dotnet-host change, 2026-09-02, at 2384 EditMode and
# 650 PlayMode tests:
#
#     robocopy sync, both runners  ~39s    cold; ~1s when nothing moved
#     Unity boot + import          ~8s     a fixed floor, per platform
#     EditMode, all 2384 tests     ~11s
#     PlayMode, all 650 tests      ~181s   <- the whole cost
#     ---------------------------------
#     full parallel run            ~236s wall (PlayMode dominates; concurrent)
#
# The thing worth internalising: for an EditMode slice, essentially NONE of
# the cost was the tests. Booting Unity, syncing a whole project copy and
# importing it dwarfed the assertions many times over.
#
# So EditMode does not go through Unity any more. Domain is engine-free by
# asmdef (noEngineReferences: true, zero references), which means the suite
# over it compiles and runs under plain `dotnet test` against the SAME source
# files -- see tools/domain-tests/README.md. 154 of the 159 EditMode classes
# run there, 2349 of the 2384 tests, in ~4s warm against ~20s through Unity.
# The five that stay in Unity need JsonUtility, Application.dataPath or
# Resources.Load; tools/test.ps1 -List marks every class D (dotnet) or U
# (Unity), and the split is read off the csproj, not maintained here.
#
# PlayMode is untouched and always Unity: it needs a scene and a player loop.
# Its cost is spread across ~87 classes with no dominant hotspot, so carving
# it into fixed "fast" and "slow" suites would not help either (this was
# investigated for real -- see the -Split section of the 2026-08-05 test-area
# plan; deferred, not abandoned).
#
# `tools/test.ps1 -Changed` picks the right slice FOR you from whatever is
# sitting uncommitted, and the host split happens underneath it.
#
# Be honest about where the saving lands, though, because it is not uniform.
# A slice made only of D classes is ~4s. A slice with ANY PlayMode class is
# still PlayMode-bound, and every named area except `rng` has some: `combat`
# is ~110s either way, since 47 of its 122 classes are PlayMode and that is
# 93s of the run. Areas are cut by SUBJECT, not by host, so a Domain change
# in a subject that also has fight-screen tests correctly still runs them.
# The saving is real on a class-name slice, which is what most of the loop
# actually is, and it is nothing on `combat`. Do not read the 4s as the
# number for everything.
#
# This is for the edit-run-edit loop. Before committing, run the full thing:
#     powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1
# ...which REFUSES to run at all while a test file sits outside an area
# folder, or out of discovery's sight -- see tools/test_areas.ps1.
#
# Usage:
#     tools/test.ps1 wool            one class, host and platform auto-detected
#     tools/test.ps1 combat          a named area -- the folder its tests sit
#                                    in (also: hub, content, run, ui, art, rng)
#     tools/test.ps1 Wool,Spell      several, comma-separated
#     tools/test.ps1 -Changed        just what your uncommitted changes touch
#     tools/test.ps1 -Unity          force everything through Unity, no dotnet
#                                    host -- the answer to "is the fast host
#                                    lying to me?"
#     tools/test.ps1 -List           what is available (classes with their
#                                    host, areas, structure violations,
#                                    duplicate names, discovery blind spots)
#     tools/test.ps1 -List -SelfCheck
#                                    does the gate still refuse what it claims
#                                    to? Points discovery at
#                                    tools/test_areas_fixture -- a tree broken
#                                    eight ways on purpose, outside Assets/ so
#                                    Unity never compiles it -- and asserts
#                                    each refusal fires. Prints SELF-CHECK: ok
#                                    or names every miss, and exits non-zero.
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

# Discovery, areas and the structural gate all live in test_areas.ps1 --
# shared with run_tests_parallel.ps1, so there is exactly one place that
# knows what an "area" is. An area is the folder a test file sits in.
. (Join-Path $PSScriptRoot "test_areas.ps1")

# -SelfCheck points discovery at tools/test_areas_fixture/ instead of the real
# Tests tree and asserts each refusal fires on the case built for it. Set
# BEFORE the discovery calls below, and it works because a dot-sourced
# function resolves $AreasTestsRoot through the caller's scope at CALL time --
# the same PS 5.1 scoping note the top of test_areas.ps1 makes for
# $PSScriptRoot, used deliberately here.
#
# Why a fixture rather than a unit test over the real tree: every one of these
# refusals is about a tree that is WRONG, and the real tree is (and must stay)
# right. Provoking one in place means editing Assets/, which the gate would
# then refuse for real, in a way that outlives the check if anything goes
# sideways. The fixture is the only tree that can safely be broken.
if ($SelfCheck) {
    $AreasTestsRoot = Join-Path $PSScriptRoot "test_areas_fixture"
}

$testIndex = Get-TestIndex
$classes = Get-TestClasses -Index $testIndex
$testAreas = Get-TestAreas -Index $testIndex
$testHosts = Get-TestHosts -Index $testIndex

# --- -List -SelfCheck: does the gate still refuse what it claims to? --------
#
# No Pester, no assertion library: one table of expectations, each a name and
# a predicate over the three lists the gate produces. It reports every miss,
# not the first, because a regex change usually breaks more than one at once
# and one refusal at a time is a slow way to find that out.
#
# The counts are asserted alongside the contents so an over-firing refusal
# fails here too -- a check that only ever asks "did it complain about X"
# passes a function that complains about everything.
function Invoke-SelfCheck {
    param([hashtable]$Index)

    $structural = @(Get-StructuralViolations)
    $duplicates = @(Get-DuplicateClassNames -Index $Index)
    $blindSpots = @(Get-DiscoveryBlindSpots -Index $Index)

    $expectations = @(
        @{ Name = "[TestCase]-only fixture is discovered"
           Ok   = { $Index.ContainsKey("TestCaseOnlyFixtureTests") } }
        @{ Name = "a .cs file directly in a platform folder, in no area, is refused"
           Ok   = { ($structural | Where-Object { $_ -match 'EditMode/LooseFixtureTests\.cs is not in an area folder' }).Count -eq 1 } }
        @{ Name = "a ninth folder beside the eight is refused"
           Ok   = { ($structural | Where-Object { $_ -match 'PlayMode/Rogue/ is not an area folder' }).Count -eq 1 } }
        @{ Name = "a folder nested in an area folder is refused"
           Ok   = { ($structural | Where-Object { $_ -match 'Run/Nested/ is nested inside an area folder' }).Count -eq 1 } }
        @{ Name = "a .cs file two folders deep is refused"
           Ok   = { ($structural | Where-Object { $_ -match 'DepthTwoFixtureTests\.cs is more than one folder deep' }).Count -eq 1 } }
        @{ Name = "a [TestCase]-only suite in Shared/ is refused"
           Ok   = { ($structural | Where-Object { $_ -match 'SharedSuiteFixtureTests\.cs carries a \[TestCase\]' }).Count -eq 1 } }
        @{ Name = "a suite in Shared/ writing [NUnit.Framework.Test] in full is refused"
           Ok   = { ($structural | Where-Object { $_ -match 'QualifiedAttrFixtureTests\.cs carries a \[Test\]' }).Count -eq 1 } }
        @{ Name = "a commented-out [Test] in Shared/ is NOT refused"
           Ok   = { ($structural | Where-Object { $_ -match 'CommentedOutAttrFixtureTests' }).Count -eq 0 } }
        @{ Name = "the internal helper beside it is NOT a blind spot"
           Ok   = { ($blindSpots | Where-Object { $_ -match 'CommentedOutAttrFixtureHelpers' }).Count -eq 0 } }
        @{ Name = "a generic fixture is refused"
           Ok   = { ($structural | Where-Object { $_ -match "generic fixture 'class GenericFixtureTests" }).Count -eq 1 } }
        @{ Name = "a public nested fixture is refused"
           Ok   = { ($structural | Where-Object { $_ -match "'NestedInnerFixtureTests' nested inside another type" }).Count -eq 1 } }
        @{ Name = "a brace inside a string literal is not nesting"
           Ok   = { ($structural | Where-Object { $_ -match "'OuterFixtureTests' nested" }).Count -eq 0 } }
        @{ Name = "one class name in two files is refused"
           Ok   = { ($duplicates | Where-Object { $_ -match '^DuplicatedFixtureTests is declared by 2 files' }).Count -eq 1 } }
        @{ Name = "an internal fixture beside a public one is a blind spot"
           Ok   = { ($blindSpots | Where-Object { $_ -match 'class InternalOnlyFixtureTests is declared here' }).Count -eq 1 } }
        @{ Name = "the public fixture beside it is NOT a blind spot"
           Ok   = { ($blindSpots | Where-Object { $_ -match 'class InternalFixtureTests is' }).Count -eq 0 } }
        @{ Name = "nothing else is refused (8 structural, 1 duplicate, 1 blind spot)"
           Ok   = { $structural.Count -eq 8 -and $duplicates.Count -eq 1 -and $blindSpots.Count -eq 1 } }
    )

    $misses = @()
    foreach ($e in $expectations) {
        if (-not (& $e.Ok)) { $misses += $e.Name }
    }

    if ($misses.Count -eq 0) {
        Write-Host "SELF-CHECK: ok ($($expectations.Count) expectations over tools/test_areas_fixture)"
        return 0
    }

    Write-Host "SELF-CHECK FAILED ($($misses.Count) of $($expectations.Count)) -- the gate no longer refuses what it claims to:"
    foreach ($m in $misses) { Write-Host "  MISS: $m" }
    Write-Host "`nWhat the fixture produced:"
    Write-Host "  structural ($($structural.Count)):"
    foreach ($v in $structural) { Write-Host "    $v" }
    Write-Host "  duplicates ($($duplicates.Count)):"
    foreach ($v in $duplicates) { Write-Host "    $v" }
    Write-Host "  blind spots ($($blindSpots.Count)):"
    foreach ($v in $blindSpots) { Write-Host "    $v" }
    Write-Host "`nEither a refusal in tools/test_areas.ps1 stopped working, or a case in"
    Write-Host "tools/test_areas_fixture/ was changed without its expectation here."
    return 1
}

if ($SelfCheck) {
    if (-not $List) {
        Write-Host "-SelfCheck is a -List mode. Run: tools/test.ps1 -List -SelfCheck"
        exit 1
    }
    exit (Invoke-SelfCheck -Index $testIndex)
}

if ($List) {
    Write-Host "`nTest classes by platform. [D] runs under dotnet (tools/domain-tests,"
    Write-Host "no Unity); [U] needs Unity. See tools/domain-tests/README.md.`n"
    foreach ($platform in @("EditMode", "PlayMode")) {
        $names = $classes.Keys | Where-Object { $classes[$_] -eq $platform } | Sort-Object
        $dCount = ($names | Where-Object { $testHosts[$_] -eq "dotnet" }).Count
        Write-Host "  $platform ($($names.Count); $dCount on dotnet)"
        foreach ($n in $names) {
            $tag = if ($testHosts[$n] -eq "dotnet") { "D" } else { "U" }
            Write-Host ("    [$tag] {0,-10} {1}" -f $testAreas[$n], $n)
        }
        Write-Host ""
    }
    Write-Host "Named areas -- the folder each class's file sits in:`n"
    foreach ($area in $AreaNames) {
        $hits = ($classes.Keys | Where-Object { $testAreas[$_] -eq $area } | Sort-Object) -join ", "
        Write-Host "  $area"
        Write-Host "    $hits`n"
    }

    # Structure and discovery, reported here and REFUSED by
    # run_tests_parallel.ps1 (see that script's own gate). A file outside an
    # area folder is invisible to every area-based run; a file whose class
    # discovery never saw is invisible to everything, including the structure
    # check itself. -List stays report-only and exits 0 even when either list
    # is non-empty; it is the diagnosis, not the enforcement.
    $violations = Get-StructuralViolations
    Write-Host "Structure (every test file exactly one folder deep in an area; nothing testable in Shared):"
    if ($violations) {
        foreach ($v in $violations) { Write-Host "  $v" }
    } else {
        Write-Host "  (ok)"
    }
    Write-Host ""

    $duplicates = Get-DuplicateClassNames -Index $testIndex
    Write-Host "Duplicate class names (two files, one name -- discovery keeps one):"
    if ($duplicates) {
        foreach ($d in $duplicates) { Write-Host "  $d" }
    } else {
        Write-Host "  (none)"
    }
    Write-Host ""

    $blindSpots = Get-DiscoveryBlindSpots -Index $testIndex
    Write-Host "Discovery blind spots (a test file declaring a class discovery never saw):"
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
        $changedWanted += $classes.Keys | Where-Object { $testAreas[$_] -eq $area }
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

        if ($AreaNames -contains $term.ToLower()) {
            $area = $term.ToLower()
            $wanted += $classes.Keys | Where-Object { $testAreas[$_] -eq $area }
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

# --- split by HOST ---------------------------------------------------------
# Everything the dotnet project compiles goes there; the rest goes to Unity.
# -Unity forces the whole slice through Unity instead, which is the way to
# check the fast host against the slow one when a result looks wrong.
$dotnetWanted = @()
$unityWanted = @()
foreach ($c in $wanted) {
    if (-not $Unity -and $testHosts[$c] -eq "dotnet") { $dotnetWanted += $c } else { $unityWanted += $c }
}

$platforms = $unityWanted | ForEach-Object { $classes[$_] } | Sort-Object -Unique

Write-Host "Matched $($wanted.Count) class(es): $($wanted -join ', ')"
if ($dotnetWanted) {
    Write-Host "  dotnet ($($dotnetWanted.Count)): $($dotnetWanted -join ', ')"
}
if ($unityWanted) {
    Write-Host "  Unity  ($($unityWanted.Count)): $($unityWanted -join ', ')  [$($platforms -join ', ')]"
}
if ($Unity -and $wanted) { Write-Host "  (-Unity: the dotnet host was skipped on purpose)" }
Write-Host ""

# --- run: the dotnet host ---------------------------------------------------
# Started FIRST and left running while Unity boots, so on a mixed slice the
# two overlap instead of queueing. There is no project copy and no lock here:
# `dotnet test` reads the same source files in place, so it cannot collide
# with a concurrent Unity run in either TestRunner.
#
# Not --no-build: the point of running it is to test what is on disk right
# now, and an incremental rebuild of a changed Domain file is ~2s.
#
# VSTest's filter is substring-based, so ".<Class>." is the same anchoring
# trick the Unity regex below uses, for the same reason.
$dotnetJob = $null
# PER INVOCATION, not one fixed name. This was "domain-tests-run.log"
# flat, and $env:TEMP is per USER -- so two sessions (WORKFLOW.md section 4
# plans for exactly two) overwrote each other's transcript. The verdict
# survived that, because it comes from the job's own exit code; two other
# things did not. The summary line PRINTED was whichever run wrote last,
# so a green slice could report a red one's "Failed! - Failed: 1" and send
# the reader hunting a failure that was not theirs. Worse, $totalRun is
# parsed out of that same line, and it is what the "No tests actually ran"
# guard below tests -- a neighbouring run's count defeats the one check
# that catches a typo'd filter reading as a pass.
$dotnetLog = Join-Path $env:TEMP "domain-tests-run-$PID.log"
if ($dotnetWanted) {
    $dotnetFilter = ($dotnetWanted | ForEach-Object { "FullyQualifiedName~.$_." }) -join "|"
    $solutionDir = Join-Path $PSScriptRoot "domain-tests"
    $dotnetJob = Start-Job -ScriptBlock {
        param($Dir, $TestFilter, $LogPath)
        & dotnet test $Dir --nologo --filter $TestFilter 2>&1 | Out-File -FilePath $LogPath -Encoding utf8
        return $LASTEXITCODE
    } -ArgumentList $solutionDir, $dotnetFilter, $dotnetLog
}

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
        # Not a Unity folder, but ContentSchemaTests reads docs/CONTENT_SCHEMA.md
        # out of whatever tree it finds Assets/_Project/Scripts in -- which,
        # under Unity, is this copy. Same line and same reason as Sync-Runner
        # in run_tests_parallel.ps1.
        robocopy "$SourceProject\docs" "$($runner.Path)\docs" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null

        $settingsPath = Join-Path $runner.Path "ProjectSettings\ProjectSettings.asset"
        (Get-Content $settingsPath -Raw) -replace "productName: .*", "productName: $($runner.Product)" |
            Set-Content $settingsPath -Encoding utf8
    }
}

# --- run: Unity -------------------------------------------------------------
# Anchored on the class segment (".<Class>."), so a preset for "combat" cannot
# also drag in an unrelated class that merely has "Fight" in a METHOD name.
# Unity matches -testFilter against the full namespace.class.method.
$procs = @{}
foreach ($platform in $platforms) {
    $runner = $RunnerFor[$platform]
    # $unityWanted, NOT $wanted: anything the dotnet host already took must
    # not be handed to Unity as well, or a mixed slice runs the shared classes
    # twice and pays the whole EditMode cost this change exists to remove.
    $onThis = $unityWanted | Where-Object { $classes[$_] -eq $platform }
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

# --- collect: the dotnet host ----------------------------------------------
# Read AFTER Unity so a mixed slice overlapped the two rather than serialising
# them. The summary line is parsed out of the run log rather than a results
# file: `dotnet test` prints "Passed!  - Failed: 0, Passed: N, ..." and the
# exit code is the authority on pass/fail, so a parse that misses only costs
# the count, never the verdict.
if ($dotnetJob) {
    $dotnetExit = Receive-Job -Job $dotnetJob -Wait -AutoRemoveJob
    $dotnetOut = if (Test-Path $dotnetLog) { Get-Content $dotnetLog -Raw } else { "" }

    $summary = ($dotnetOut -split "`r?`n" | Where-Object { $_ -match "^(Passed|Failed)!\s" } | Select-Object -First 1)
    if ($summary -match "Passed:\s*(\d+)") { $totalRun += [int]$Matches[1] }
    if ($summary -match "Failed:\s*(\d+)") { $totalRun += [int]$Matches[1] }

    if ($summary) {
        Write-Host "dotnet -- $($summary.Trim())"
    } else {
        Write-Host "dotnet -- no summary line in $dotnetLog"
    }

    if ($dotnetExit -ne 0) {
        $allPassed = $false
        # The failure detail, and only that: a full `dotnet test` transcript is
        # mostly restore/build noise nobody reads.
        $dotnetOut -split "`r?`n" |
            Where-Object { $_ -match "^\s*(Failed|Error Message|Stack Trace|Assert\.|  Expected|  But was|error CS)" } |
            Select-Object -First 60 |
            ForEach-Object { Write-Host $_ }
        Write-Host "Full transcript: $dotnetLog"
    }
}

# A filter that matches a class but selects no tests is almost always a typo
# that would otherwise read as a pass.
#
# Gated on $allPassed so it cannot MASK a real failure with a wrong diagnosis:
# a dotnet host that failed to compile prints no summary line, so $totalRun
# stays 0 and this would otherwise report "no tests ran" over a build error.
# Both exit 1 either way; only the message the reader acts on differs.
if ($allPassed -and $totalRun -eq 0) {
    Write-Host "`nNo tests actually ran. The filter matched a class but no test inside it."
    exit 1
}

if ($allPassed) {
    Write-Host "`nPassed. This was a SLICE -- run tools/run_tests_parallel.ps1 before committing."
    exit 0
}

Write-Host "`nSome tests failed."
exit 1
