# PlayMode tests WITH a real graphics device.
#
# The commit gate (run_tests_parallel.ps1) and test.ps1 both pass
# -nographics, where camera.Render() is a silent no-op and ReadPixels returns
# garbage -- so every test that reads back a rendered pixel self-skips there.
# This omits the flag, which is exactly what screenshot.ps1 already does and for
# the same reason.
#
# Runs against the isolated RUNNER copy, not the main project: the editor is
# usually open on main, and two Unity instances cannot share one project.
#
# Pure ASCII, no BOM -- CLAUDE.md's PowerShell gotcha applies here too.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/graphics_tests.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/graphics_tests.ps1 -Filter PrincesPalace.PlayModeTests
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/graphics_tests.ps1 -Filter ... -Label before
param([string]$Filter = "PrincesPalace.PlayModeTests.HitFlashPixelTests",
      [string]$Label = "")

$ErrorActionPreference = "Stop"

# Names this run's output for a fixture that writes a SERIES rather than one
# file -- StaticPilotStageCaptureTests reads PP_CAPTURE_LABEL to decide which
# folder its frames land in, so the same test records a "before" and an "after"
# and the two can be compared. A parameter rather than a pre-set environment
# variable because this script is normally invoked as `powershell -File ...`,
# which is a fresh process and inherits nothing the caller exported.
if ($Label -ne "") { $env:PP_CAPTURE_LABEL = $Label }

. (Join-Path $PSScriptRoot "unity_path.ps1")
$UnityExe = Get-UnityExe

$Project = Split-Path $PSScriptRoot -Parent
$Target = (Split-Path $Project -Parent) + "\" + (Split-Path $Project -Leaf) + "-TestRunner"
Write-Host "target: $Target"

if (-not (Test-Path $Target)) {
    Write-Host "No runner copy yet. Run run_tests_parallel.ps1 once to create it."
    exit 1
}

# A run that aborts on exit leaves its lockfile behind, and the NEXT run then
# refuses to start with "another Unity instance is running with this project
# open" -- which is untrue and costs a confusing five minutes every time. The
# runner copy is ours alone, so a lockfile here is always stale by definition.
$lock = Join-Path $Target "Temp\UnityLockfile"
if (Test-Path $lock) {
    Write-Host "clearing a stale lockfile from a previous run"
    Remove-Item $lock -Force -ErrorAction SilentlyContinue
}

# PER INVOCATION, for the reason test.ps1 states at its own $dotnetLog:
# $env:TEMP is per user, so two sessions running different runner copies
# shared these two names. Sharper here than a confusing printout -- the
# results XML is what the verdict is PARSED from, and the delete just
# below would take a concurrent run's results with it.
$results = Join-Path $env:TEMP "pp-gfx-results-$PID.xml"
$log = Join-Path $env:TEMP "pp-gfx-$PID.log"
if (Test-Path $results) { Remove-Item $results }

# Every path QUOTED, as run_tests_parallel.ps1 quotes its own. Start-Process
# joins the list into one command line and re-splits it on spaces, so the
# runner copy's "Prince's Palace-v2-TestRunner" arrived as two arguments and
# Unity refused the project path -- the call operator this replaced passed
# each element intact and never had the problem.
$unityArgs = @(
    "-projectPath", "`"$Target`"",
    "-batchmode", "-silent-crashes",
    "-runTests", "-testPlatform", "PlayMode",
    "-testFilter", $Filter,
    "-testResults", "`"$results`"",
    "-logFile", "`"$log`""
)

# Start-UnityQuiet (tools/unity_path.ps1) + WaitForExit, the same shape
# run_tests_parallel.ps1 and screenshot.ps1 use, and NOT the call operator
# this used to be. No -nographics above means this launches through
# Start-UnityQuiet's windowed path -- minimized, foreground-guarded for as
# long as Unity is alive. "& Unity.exe" came back
# while the run was still writing: a capture copied straight after it had 37
# of its 42 frames and no timing.json, and the results XML was read while it
# was half-written, which the [xml] cast turned into a failure on a run whose
# every test had passed. Waiting on the process itself makes both files
# complete by construction; the XML poll below stays as a belt for the
# moment Unity has been seen to exit a beat before the file lands.
$proc = Start-UnityQuiet -FilePath $UnityExe -ArgumentList $unityArgs
if (-not $proc.WaitForExit(1800 * 1000)) {
    Write-Host "Unity did not exit within 30 minutes; killing it"
    $proc.Kill()
    exit 1
}

# Unity has been seen to return from a clean, completed run a moment before
# its results file lands on disk -- the log said "Exiting with code 0 (Ok)"
# and the XML appeared afterwards, so the run was reported as a failure with
# nothing wrong with it. Waited out rather than trusted, and briefly: a run
# that genuinely produced nothing still reports within seconds.
for ($waited = 0; $waited -lt 30 -and -not (Test-Path $results); $waited++) {
    Start-Sleep -Seconds 1
}

if (-not (Test-Path $results)) {
    Write-Host "no results file; tail of log:"
    Get-Content $log -Tail 25
    exit 1
}

[xml]$xml = Get-Content $results
$failed = 0
foreach ($tc in $xml.SelectNodes("//test-case")) {
    Write-Host ("{0} : {1}" -f $tc.result, $tc.name)
    if ($tc.result -ne "Passed") {
        $failed = $failed + 1
        Write-Host $tc.InnerText
    }
}

if ($failed -gt 0) { exit 1 }
Write-Host "All graphics tests passed."

# STATED, not left to whatever the last native call set. A wrapper reading
# $LASTEXITCODE after "& graphics_tests.ps1" got Unity's own exit code, which
# is not zero on a passing batchmode run -- so tools/static_pilot_qa.ps1 threw
# away a capture that had in fact succeeded.
exit 0
