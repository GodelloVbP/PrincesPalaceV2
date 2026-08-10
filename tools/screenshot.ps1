param(
    [string]$Panel,
    [switch]$All,
    [switch]$Runtime,
    [switch]$SkipSync,
    [string]$OutDir,
    [int]$TimeoutSeconds = 300
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
# Success is checked by whether the PNGs exist, NOT by $proc.ExitCode - that
# property is unreliable through this Start-Process -PassThru -NoNewWindow
# pattern and has come back empty on runs that demonstrably succeeded.

if (-not $Panel -and -not $All -and -not $Runtime) {
    Write-Host "Usage: tools/screenshot.ps1 -All  OR  -Panel <PanelName>  OR  -Runtime"
    Write-Host "  -Runtime captures the RUNNING game (animators ticking) via the PlayMode test runner."
    Write-Host "  -Panel/-All render Edit Mode, which never ticks Update() and so cannot show motion."
    Write-Host "  Panel names come from ScreenRegistry.All - see"
    Write-Host "  Assets/_Project/Scripts/Editor/SceneBuilder/ScreenRegistry.cs"
    exit 1
}

# Derived, never hardcoded - see run_tests_parallel.ps1 for what the hardcoded
# form did when this project was copied from v1.
$SourceProject = Split-Path $PSScriptRoot -Parent
$ProjectLeaf = Split-Path $SourceProject -Leaf
$ProjectParent = Split-Path $SourceProject -Parent
$TestProject = Join-Path $ProjectParent "$ProjectLeaf-TestRunner"
. (Join-Path $PSScriptRoot "unity_path.ps1")
$UnityExe = Get-UnityExe

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
        "-testFilter", "PrincesPalace.PlayModeTests.RuntimeScreenshotTests",
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

$produced = Get-ChildItem $OutDir -Filter *.png -ErrorAction SilentlyContinue
if (-not $produced -or $produced.Count -eq 0) {
    Write-Host "No screenshots were produced. Tail of log:"
    Get-Content $logPath -Tail 40 | ForEach-Object { Write-Host $_ }
    exit 1
}

$produced | ForEach-Object { Write-Host "  $($_.FullName)" }
