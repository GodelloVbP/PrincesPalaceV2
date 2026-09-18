# tools/build_content.ps1 -- regenerate Resources/Content in the MAIN project.
#
# Pure ASCII, no BOM -- CLAUDE.md's PowerShell gotcha.
#
# GENERATION, SEPARATED FROM VERIFICATION. run_tests_parallel.ps1 -BuildContent
# is the commit gate: it syncs main into two isolated copies, generates in the
# primary one, runs 3,400 tests, and syncs the generated tree back. That is the
# right shape for a commit and the wrong shape for an author who has just typed
# a row into enemies.json and wants to look at it -- four and a half minutes to
# find out whether a resolver liked the row.
#
# This does the generation half and nothing else: one batchmode Unity against
# main, in place, no runner copies, no tests, no sync-back to get wrong.
# Measured 2026-09-05 at 13.1s warm (docs/measurements/2026-09-step1-enemy.md).
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_content.ps1
#
# The Editor being open is not an error and not this script's problem to solve:
# Unity holds an exclusive lock on Library, so batchmode against an open
# project cannot run at all. tools/preview.ps1 picks the route by looking at
# the lockfile and sends the open-Editor case through PreviewRequestWatcher
# instead. Called directly with the Editor open, this refuses and says so.

$ErrorActionPreference = "Stop"

$Project = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "unity_path.ps1")
. (Join-Path $PSScriptRoot "unity_lock.ps1")
$UnityExe = Get-UnityExe

# Process-tree focus guard, for this script's WHOLE run -- see
# Start-FocusGuard's header in tools/unity_path.ps1 for why this replaced a
# per-Unity-launch watchdog. Everything from here to the end of the file is
# wrapped in try/finally so Stop-FocusGuard runs on every exit path;
# try/finally does not introduce a new variable scope in PowerShell, so
# nothing else in this script changes.
Start-FocusGuard
try {

$lock = Get-UnityLockState -ProjectRoot $Project
if ($lock.Held) {
    $who = if ($lock.HolderPid) { "pid $($lock.HolderPid)" } else { "a Unity.exe whose project could not be read" }
    Write-Host "The Unity Editor is open on $Project ($who)."
    Write-Host "Unity locks Library exclusively, so a batchmode build cannot run against it."
    Write-Host "Use the Editor route instead:"
    Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File tools/preview.ps1 -Build"
    exit 1
}

# A lockfile with no Unity behind it is debris from a crash or a killed run.
# Left in place it would make this script refuse forever with advice that is
# wrong, because there is no Editor to route to.
[void](Clear-StaleUnityLock -State $lock)

$log = Join-Path $Project "Temp\build_content.log"
New-Item -ItemType Directory -Force -Path (Split-Path $log -Parent) | Out-Null
if (Test-Path $log) { Remove-Item $log -Force }

# The SAME argument list run_tests_parallel.ps1's generation phase uses, down
# to -silent-crashes and the quoting. Every path QUOTED: Start-Process joins
# the list into one command line and re-splits on spaces, and this project's
# folder name has one in it.
$unityArgs = @(
    "-batchmode", "-nographics", "-silent-crashes",
    "-projectPath", "`"$Project`"",
    "-executeMethod", "GenerationRun.RunAll",
    "-logFile", "`"$log`"",
    "-ppSteps", "content",
    "-quit"
)

$start = Get-Date
Write-Host "building content in $Project ..."
$proc = Start-UnityQuiet -FilePath $UnityExe -ArgumentList $unityArgs
if (-not $proc.WaitForExit(900 * 1000)) {
    Write-Host "Unity did not exit within 15 minutes; killing it"
    $proc.Kill()
    exit 1
}
$elapsed = (Get-Date) - $start

$body = if (Test-Path $log) { Get-Content $log } else { @() }

# THE MARK TIMINGS, ALWAYS. They are the only reason anyone can say where a
# slow build's time went without bisecting for it, and they cost one grep.
$body | Select-String -Pattern "^\[GenerationRun\]|^\[ContentTiming\]" | ForEach-Object { Write-Host "  $_" }

# GATED ON THE SENTINEL, NOT THE EXIT CODE, for the reason
# run_tests_parallel.ps1 records: Unity exits non-zero on an untidy shutdown
# even when the method ran fine. Every builder logs BUILD-COMPLETE as its last
# act, so its ABSENCE is the honest signal. All three are checked, not just the
# last: they share one process now, so a generator that threw halfway leaves
# the earlier sentinels in the log.
$sentinels = @("ProceduralSpriteBaker", "PipelineBuilder", "ContentBuilder", "GenerationRun")
foreach ($sentinel in $sentinels) {
    if (-not ($body | Select-String -Pattern "BUILD-COMPLETE: $sentinel" -Quiet)) {
        Write-Host "content build did not reach BUILD-COMPLETE: $sentinel. Tail of $log :"
        $body | Select-Object -Last 30 | ForEach-Object { Write-Host "  $_" }
        exit 1
    }
}

# Same widened pattern the commit gate uses. "[ContentBuilder]" is the prefix
# every resolver failure wears, which is why the timing marks added alongside
# this script are prefixed [ContentTiming] instead.
$errors = $body | Select-String -Pattern "error CS|\[ContentBuilder\]|threw exception" | Select-Object -Unique -First 10
if ($errors) {
    Write-Host "content build FAILED:"
    $errors | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host ("content built in {0:N1}s -- Assets/_Project/Resources/Content is current" -f $elapsed.TotalSeconds)

# STATED, not left to whatever the last native call set: $LASTEXITCODE after a
# passing batchmode Unity is not zero, and a wrapper reading it would throw
# away a build that succeeded.
exit 0

} finally {
    Stop-FocusGuard
}
