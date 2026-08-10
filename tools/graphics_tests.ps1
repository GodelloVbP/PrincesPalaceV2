# PlayMode tests WITH a real graphics device.
#
# The commit gate (run_tests.ps1, run_tests_parallel.ps1, test.ps1) all pass
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
param([string]$Filter = "PrincesPalace.PlayModeTests.HitFlashPixelTests")

$ErrorActionPreference = "Stop"

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

$results = Join-Path $env:TEMP "pp-gfx-results.xml"
$log = Join-Path $env:TEMP "pp-gfx.log"
if (Test-Path $results) { Remove-Item $results }

$unityArgs = @(
    "-projectPath", $Target,
    "-batchmode", "-silent-crashes",
    "-runTests", "-testPlatform", "PlayMode",
    "-testFilter", $Filter,
    "-testResults", $results,
    "-logFile", $log
)

& $UnityExe @unityArgs

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
