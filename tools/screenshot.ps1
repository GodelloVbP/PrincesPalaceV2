param(
    [string]$Panel,
    [switch]$All,
    [switch]$Runtime,
    [switch]$SkipSync,
    [string]$OutDir,
    [int]$TimeoutSeconds = 300,

    # Which PlayMode capture class -Runtime drives. Defaults to the general
    # runtime shots; pass a class name to drive one of the screen-specific
    # capture tests instead, e.g. -RuntimeFilter MapCaptureTests.
    #
    # Those classes existed before this parameter did and nothing could reach
    # them: the filter below was a hardcoded single class, so a capture test
    # written for one screen could only be run by hand-assembling the Unity
    # command line. A test nothing can run is a test nobody runs.
    [string]$RuntimeFilter = "RuntimeScreenshotTests"
)

# Headless visual QA: renders screens from the SceneBuilder-generated scenes to
# PNG, without opening the Editor GUI. Runs against an isolated copy, so it is
# safe while Unity is open on the same project.
#
#   tools/screenshot.ps1 -All
#   tools/screenshot.ps1 -Panel MainMenuPanel
#
# There is deliberately NO list of panel names in this file. v1 kept one here
# AND one in ScreenshotTool's KnownPanels table, with a documented "nothing
# keeps these in sync" hazard. The single source of truth is
# Assets/_Project/Scripts/Editor/SceneBuilder/ScreenRegistry.cs - the same list
# that builds the scenes.
#
# Does NOT pass -nographics: a screenshot needs a real graphics device.
#
# Success is checked by whether the EXPECTED PNG exists, NOT by $proc.ExitCode -
# that property is unreliable through this Start-Process -PassThru -NoNewWindow
# pattern and has come back empty on runs that demonstrably succeeded.
#
# "Expected", emphatically, and that word is the whole of AUDIT #43. This used to
# ask whether the output directory contained ANY png, so five leftovers from an
# earlier -All run satisfied it: `-Panel ReckoningPanel` announced the capture,
# wrote nothing, listed the stale five and exited 0. Ignoring the exit code is
# still right for the documented reason; checking a different file than the one
# requested was never part of that trade.
#
# -All is checked the same way and by NAME, against ScreenRegistry. Clearing the
# directory first only rules out leftovers: four files out of five registered
# screens is still a non-zero count, and the screen that stopped rendering is the
# one thing this tool exists to catch.

if (-not $Panel -and -not $All -and -not $Runtime) {
    Write-Host "Usage: tools/screenshot.ps1 -All  OR  -Panel <PanelName>  OR  -Runtime"
    Write-Host "  -Runtime captures the RUNNING game (animators ticking) via the PlayMode test runner."
    Write-Host "  -Panel/-All render Edit Mode, which never ticks Update() and so cannot show motion."
    Write-Host "  Panel names come from ScreenRegistry.All - see"
    Write-Host "  Assets/_Project/Scripts/Editor/SceneBuilder/ScreenRegistry.cs"
    exit 1
}

# The valid panel names, DERIVED from ScreenRegistry rather than restated here.
#
# A second copy of this list is the one thing this file must not grow: v1 kept
# one here AND one in ScreenshotTool, with a documented "nothing keeps these in
# sync" hazard. Reading the registry's own source is not a copy - add a screen
# there and it is valid here on the same edit, with nothing to remember.
function Get-KnownPanelNames {
    param([string]$ProjectRoot)

    $registry = Join-Path $ProjectRoot "Assets\_Project\Scripts\Editor\SceneBuilder\ScreenRegistry.cs"
    if (-not (Test-Path $registry)) { return @() }

    $names = @()
    $found = Select-String -Path $registry -Pattern 'PanelName\s*=\s*"([^"]+)"' -AllMatches
    foreach ($line in $found) {
        foreach ($match in $line.Matches) { $names += $match.Groups[1].Value }
    }
    return $names
}

# Derived, never hardcoded - see run_tests_parallel.ps1 for what the hardcoded
# form did when this project was copied from v1.
$SourceProject = Split-Path $PSScriptRoot -Parent
$ProjectLeaf = Split-Path $SourceProject -Leaf
$ProjectParent = Split-Path $SourceProject -Parent
$TestProject = Join-Path $ProjectParent "$ProjectLeaf-TestRunner"
. (Join-Path $PSScriptRoot "unity_path.ps1")
$UnityExe = Get-UnityExe

# BEFORE the sync and before Unity boots. ScreenshotTool already rejects an
# unknown panel properly - it names every valid one and exits 1 - but that
# verdict arrives about half a minute later, after a full /MIR robocopy, and
# then only inside a Unity log this script was throwing away.
#
# Case-sensitive on purpose: ScreenshotTool matches PanelName with ==, so
# 'mainmenupanel' is genuinely not a screen and saying so here beats letting it
# look accepted and fail later.
if ($Panel) {
    $known = Get-KnownPanelNames -ProjectRoot $SourceProject

    # An empty list means the pattern found nothing - ScreenRegistry has been
    # refactored, not that every panel is suddenly unknown. Fall through and let
    # Unity be the authority rather than rejecting names this script can no
    # longer recognise. The output check at the bottom still catches it.
    if ($known.Count -gt 0 -and $known -cnotcontains $Panel) {
        Write-Host "Unknown panel '$Panel'."
        Write-Host ""
        Write-Host "Known panels, from ScreenRegistry.All:"
        foreach ($name in $known) { Write-Host "  $name" }
        Write-Host ""
        Write-Host "Only TOP-LEVEL panels are capturable this way. A screen built inside"
        Write-Host "another screen's tree - the Reckoning, the character overlay - has no"
        Write-Host "entry of its own and cannot be rendered on its own. Use -Runtime."
        exit 1
    }
}

if (-not $OutDir) { $OutDir = Join-Path $SourceProject "tools\screenshots" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

if (-not $SkipSync) {
    Write-Host "Syncing into the isolated capture copy..."
    robocopy "$SourceProject\Assets" "$TestProject\Assets" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    robocopy "$SourceProject\Packages" "$TestProject\Packages" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    robocopy "$SourceProject\ProjectSettings" "$TestProject\ProjectSettings" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
}

$logPath = Join-Path $TestProject "screenshot.log"

# Stale-output guard: a leftover PNG from a previous run must never be mistaken
# for this run's output.
if ($Runtime) {
    # Drives RuntimeScreenshotTests through the PlayMode TEST RUNNER - a
    # different mechanism from the Editor entering Play Mode, which is the thing
    # that hangs in this environment. Still omits -nographics: no device, no
    # pixels.
    $runtimeOut = Join-Path $OutDir "runtime"
    $runnerOut = Join-Path $TestProject "tools\screenshots\runtime"
    New-Item -ItemType Directory -Force -Path $runtimeOut | Out-Null
    Get-ChildItem $runtimeOut -Filter *.png -ErrorAction SilentlyContinue | Remove-Item -Force
    Get-ChildItem $runnerOut -Filter *.png -ErrorAction SilentlyContinue | Remove-Item -Force

    $resultsPath = Join-Path $TestProject "test-results-runtime-screenshot.xml"
    if (Test-Path $resultsPath) { Remove-Item $resultsPath -Force }

    Write-Host "Capturing the RUNNING game to $runtimeOut ..."
    $proc = Start-Process -FilePath $UnityExe -ArgumentList @(
        "-batchmode", "-silent-crashes",
        "-projectPath", "`"$TestProject`"",
        "-runTests", "-testPlatform", "PlayMode",
        "-testFilter", "PrincesPalace.PlayModeTests.$RuntimeFilter",
        "-testResults", "`"$resultsPath`"",
        "-logFile", "`"$logPath`"",
        "-buildTarget", "StandaloneWindows64"
    ) -PassThru -NoNewWindow

    if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
        Write-Host "Timed out after ${TimeoutSeconds}s - killing Unity (PID $($proc.Id))."
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Get-Content $logPath -Tail 40 | ForEach-Object { Write-Host $_ }
        exit 1
    }

    # THE RESULTS FILE, WHICH THIS BRANCH ALREADY DELETES AND USED TO NEVER
    # READ. -Runtime does not render screens directly; it drives a PlayMode
    # test class that renders them, so "did any png appear" is the weaker
    # question here than it is anywhere else in this script -- a fixture that
    # captured three shots and then threw on the fourth leaves three files
    # behind, and three is not zero. That is the exact argument AUDIT #43
    # settled for -Panel and -All (see this file's header), applied to the
    # branch it was not applied to.
    #
    # Deliberately one-directional: a missing results file is NOT a failure
    # here, because the png check below is what has always governed and a
    # -runTests run that wrote no XML at all already fails on it. This can
    # only add a refusal where NUnit recorded one.
    if (Test-Path $resultsPath) {
        [xml]$runtimeResults = Get-Content $resultsPath
        $failedCount = [int]$runtimeResults.'test-run'.failed
        if ($failedCount -ne 0) {
            Write-Host "$failedCount runtime capture test(s) FAILED -- the pictures below, if any, are from a run that did not finish:"
            foreach ($f in $runtimeResults.SelectNodes("//test-case[@result='Failed']")) {
                Write-Host "  FAILED: $($f.fullname)"
                if ($f.failure -and $f.failure.message) { Write-Host "    $($f.failure.message.InnerText)" }
            }
            exit 1
        }
    }

    $produced = Get-ChildItem $runnerOut -Filter *.png -ErrorAction SilentlyContinue
    if (-not $produced -or $produced.Count -eq 0) {
        Write-Host "No runtime captures were produced. Tail of log:"
        Get-Content $logPath -Tail 40 | ForEach-Object { Write-Host $_ }
        exit 1
    }

    Copy-Item "$runnerOut\*.png" $runtimeOut -Force
    Get-ChildItem $runtimeOut -Filter *.png | ForEach-Object { Write-Host "  $($_.FullName)" }
    exit 0
}

if ($All) {
    Get-ChildItem $OutDir -Filter *.png -ErrorAction SilentlyContinue | Remove-Item -Force
    $unityArgs = @("-executeMethod", "ScreenshotTool.CaptureFromArgs", "-out", "`"$OutDir`"")
    Write-Host "Capturing every registered screen to $OutDir ..."
} else {
    $outPath = Join-Path $OutDir "$Panel.png"
    if (Test-Path $outPath) { Remove-Item $outPath -Force }
    $unityArgs = @("-executeMethod", "ScreenshotTool.CaptureFromArgs", "-panel", $Panel, "-out", "`"$OutDir`"")
    Write-Host "Capturing $Panel to $outPath ..."
}

$proc = Start-Process -FilePath $UnityExe -ArgumentList (@(
    "-batchmode", "-silent-crashes",
    "-projectPath", "`"$TestProject`"",
    "-logFile", "`"$logPath`""
) + $unityArgs) -PassThru -NoNewWindow

if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
    Write-Host "Timed out after ${TimeoutSeconds}s - killing Unity (PID $($proc.Id))."
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Get-Content $logPath -Tail 40 | ForEach-Object { Write-Host $_ }
    exit 1
}

# EVERY FILE THAT WAS ASKED FOR, checked by NAME.
#
# Counting pngs in the directory is what let a capture that never happened pass.
# Clearing the directory first fixes only the stale-leftover case: with five
# screens registered and one of them silently failing to render, four files is
# still a non-zero count and still read as success. A screen that quietly stops
# rendering is precisely what this tool exists to notice, so the question has to
# be "is each expected file here", not "is anything here".
if ($All) {
    $expected = @(Get-KnownPanelNames -ProjectRoot $SourceProject)
} else {
    $expected = @($Panel)
}

$produced = @()
$missing = @()

if ($expected.Count -eq 0) {
    # -All, and ScreenRegistry could not be parsed: the same refactor case the
    # pre-check defers on, and the same reasoning. Naming what is missing is not
    # available, so fall back to the weaker question rather than inventing an
    # expectation - it still fails the run that produced nothing.
    $produced = @(Get-ChildItem $OutDir -Filter *.png -ErrorAction SilentlyContinue)
    if ($produced.Count -eq 0) {
        $missing = @("every screen (could not read ScreenRegistry to say which)")
    }
} else {
    foreach ($name in $expected) {
        $path = Join-Path $OutDir "$name.png"
        if (Test-Path $path) {
            $produced += Get-Item $path
        } else {
            $missing += $name
        }
    }
}

if ($missing.Count -gt 0) {
    if ($expected.Count -gt 0) {
        Write-Host "Missing $($missing.Count) of $($expected.Count) expected capture(s):"
    } else {
        Write-Host "No captures were produced:"
    }
    foreach ($name in $missing) { Write-Host "  $name" }

    # ScreenshotTool's own verdict FIRST. It names the unknown panel and lists
    # every valid one, which is the answer; 40 lines of Unity boot noise with
    # that sentence somewhere inside it is not.
    $reasons = @(Select-String -Path $logPath -Pattern '\[ScreenshotTool\]' -ErrorAction SilentlyContinue)
    if ($reasons.Count -gt 0) {
        foreach ($reason in $reasons) { Write-Host "  $($reason.Line.Trim())" }
    } else {
        Write-Host "Nothing from ScreenshotTool in the log. Tail:"
        Get-Content $logPath -Tail 40 | ForEach-Object { Write-Host $_ }
    }
    exit 1
}

$produced | ForEach-Object { Write-Host "  $($_.FullName)" }
