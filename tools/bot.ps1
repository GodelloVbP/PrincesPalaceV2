param(
    [int]$Runs = 200,
    [int]$Seed = 1,
    [string]$Archetypes = "RandomLegal,GreedyAggressive",
    [string]$Profiles = "Fresh",
    [int]$DepthCap = 40,
    [switch]$SkipSync
)

# Runs a balance-bot batch headlessly against the isolated PlayMode test
# copy (Prince's Palace-v2-TestRunner2, sibling folder), the same one
# run_tests_parallel.ps1 uses for PlayMode -- so the same throwaway-save
# harness (SaveSystem.RootOverride, RunManager.ResetForTests) already proven
# there applies here too, and there is no third project copy to keep in
# sync. Safe to run while Unity is open on main at the same time.
#
# Mirrors run_tests.ps1's shape (sync -> divergent productName -> batchmode
# launch), swapping -runTests for -executeMethod against
# PrincesPalace.Editor.Bot.BalanceBotRunner.RunFromCommandLine. That method
# lives in Assets/_Project/Scripts/Editor/Bot/BalanceBotRunner.cs.
#
# Pure ASCII, no BOM: CLAUDE.md's PowerShell gotcha applies here same as
# everywhere else in tools/ -- an em-dash inside a string breaks PS 5.1's
# parser, so plain "--" throughout.

$ErrorActionPreference = "Stop"

$SourceProject = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "unity_path.ps1")
$UnityExe = Get-UnityExe

$ProjectLeaf = Split-Path $SourceProject -Leaf
$ProjectParent = Split-Path $SourceProject -Parent
$ProductLeaf = ($ProjectLeaf -replace "[^A-Za-z0-9]", "")

$TestProject = "$ProjectParent\$ProjectLeaf-TestRunner2"
$Product = "${ProductLeaf}TestRunner2"

if (-not (Test-Path $TestProject)) {
    Write-Host "No $TestProject yet. Run tools/run_tests_parallel.ps1 once to create it."
    exit 1
}

# A concurrent run_tests.ps1/run_tests_parallel.ps1 (or another bot.ps1)
# against this SAME runner copy would clobber this batch mid-flight --
# WORKFLOW.md SS4 says check the lock, not clear it, because unlike
# graphics_tests.ps1's dedicated runner, TestRunner2 is shared with the
# PlayMode leg of the commit-gate script and a second live session. A lock
# here might be real, not stale, so this refuses rather than guessing.
$lock = Join-Path $TestProject "Temp\UnityLockfile"
if (Test-Path $lock) {
    Write-Host "$TestProject is locked (Temp\UnityLockfile present) -- another Unity run is using this copy. Wait for it to finish, or confirm it is stale before removing the lockfile by hand."
    exit 1
}

if (-not $SkipSync) {
    Write-Host "Syncing Assets/Packages/ProjectSettings into $TestProject..."
    robocopy "$SourceProject\Assets" "$TestProject\Assets" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    robocopy "$SourceProject\Packages" "$TestProject\Packages" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    robocopy "$SourceProject\ProjectSettings" "$TestProject\ProjectSettings" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null

    # Diverge the product name so Application.persistentDataPath (and thus
    # save_slot_*.json) never overlaps with the real project's save data or
    # with the EditMode runner's copy.
    $settingsPath = Join-Path $TestProject "ProjectSettings\ProjectSettings.asset"
    (Get-Content $settingsPath -Raw) -replace "productName: .*", "productName: $Product" |
        Set-Content $settingsPath -Encoding utf8
}

# The sha the batch measured, read HERE and passed in.
#
# The runner copy is a robocopy of Assets/Packages/ProjectSettings only, so
# there is no .git inside it and the Editor entry cannot ask. summary.json's
# commitSha is what lets a report be matched back to the code it measured,
# and a report that cannot say what it measured is a report nobody can act
# on -- so it travels as an argument rather than being looked up on the far
# side. Empty (and harmless) if git is unavailable.
$CommitSha = "unknown"
try { $CommitSha = (& git -C $SourceProject rev-parse --short HEAD) } catch { $CommitSha = "" }
# Never left EMPTY: an empty element in Start-Process -ArgumentList collapses
# and shifts every argument after it by one, which would silently hand
# -botOut's path to -botCommit and leave the batch writing nowhere.
if (-not $CommitSha) { $CommitSha = "unknown" }

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$RunnerOutDir = Join-Path $TestProject "bot-out"
$MainOutDir = Join-Path $SourceProject "reports\bot\$timestamp"

if (Test-Path $RunnerOutDir) { Remove-Item $RunnerOutDir -Recurse -Force }
New-Item -ItemType Directory -Path $RunnerOutDir -Force | Out-Null

$logPath = Join-Path $TestProject "bot-run.log"
if (Test-Path $logPath) { Remove-Item $logPath -Force }

Write-Host "`nRunning balance bot: $Runs runs/cell, seed $Seed, archetypes [$Archetypes], profiles [$Profiles], depth cap $DepthCap steps..."

$unityArgs = @(
    "-batchmode", "-nographics", "-silent-crashes",
    "-projectPath", "`"$TestProject`"",
    "-executeMethod", "PrincesPalace.Editor.Bot.BalanceBotRunner.RunFromCommandLine",
    "-botRuns", $Runs,
    "-botSeed", $Seed,
    "-botArchetypes", $Archetypes,
    "-botProfiles", $Profiles,
    "-botDepthCap", $DepthCap,
    "-botCommit", $CommitSha,
    "-botOut", "`"$RunnerOutDir`"",
    "-logFile", "`"$logPath`"",
    "-buildTarget", "StandaloneWindows64",
    "-quit"
)

$proc = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru -Wait -NoNewWindow

$summaryPath = Join-Path $RunnerOutDir "summary.json"
if (-not (Test-Path $summaryPath)) {
    Write-Host "No summary.json produced (Unity exit code $($proc.ExitCode)). Tail of log:"
    if (Test-Path $logPath) { Get-Content $logPath -Tail 60 | ForEach-Object { Write-Host $_ } }
    exit 1
}

Write-Host "Copying batch output back to $MainOutDir..."
New-Item -ItemType Directory -Path $MainOutDir -Force | Out-Null
robocopy $RunnerOutDir $MainOutDir /E /NFL /NDL /NJH /NJS /NP | Out-Null

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    Write-Host "No 'python' on PATH -- batch written to $MainOutDir, but the HTML report needs it. Run tools/bot_report.py by hand once python is available."
    exit 1
}

Write-Host "`nBuilding report..."
& python (Join-Path $PSScriptRoot "bot_report.py") $MainOutDir --out (Join-Path $MainOutDir "report.html")
$reportExit = $LASTEXITCODE

Write-Host "`nBatch: $MainOutDir"
Write-Host "Report: $(Join-Path $MainOutDir "report.html")"

exit $reportExit
