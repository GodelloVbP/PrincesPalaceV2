# tools/unity_lock.ps1 -- is a Unity Editor actually holding this project?
#
# Dot-sourced by build_content.ps1 and preview.ps1. Never invoked directly.
#
# Pure ASCII, no BOM: CLAUDE.md's PowerShell gotcha applies here as everywhere
# else in tools/ -- PS 5.1 reads a BOM-less file as Windows-1252 and an em-dash
# inside a string is a parse error, so plain "--" throughout.
#
# WHY THIS IS NOT JUST Test-Path Temp\UnityLockfile:
#
# Unity writes that file when it opens a project and removes it when it closes
# cleanly. It does NOT remove it when it crashes, when the machine reboots
# under it, or when a batchmode run is killed -- and the file is zero bytes
# with no owner recorded in it, so its mere presence says "a Unity may have
# been here", not "a Unity is here now".
#
# Believing the file alone has two failure modes and they point opposite ways:
# a stale file makes every batch route refuse forever with advice ("use the
# Editor route") that is wrong, because there is no Editor; and no file at all
# while an Editor really is starting up makes a batch run collide with it. So
# the lockfile is treated as the CHEAP part of the question and the process
# table as the authority.
#
# The process table is read once and matched against the project path Unity was
# launched with. When a Unity.exe is running but its command line does not name
# a project we can match, the answer is HELD -- guessing "not mine, go ahead"
# there is how two editors end up taking turns on one Library.

$UnityLockProjectRoot = Split-Path $PSScriptRoot -Parent

function Get-UnityLockState {
    param([string]$ProjectRoot = $UnityLockProjectRoot)

    $lockPath = Join-Path $ProjectRoot "Temp\UnityLockfile"
    $lockExists = Test-Path $lockPath

    # Normalised for comparison: Unity's own command line quotes the path and
    # may or may not carry a trailing slash, and the project leaf here has an
    # apostrophe in it.
    $normalised = $ProjectRoot.TrimEnd('\', '/').ToLowerInvariant()

    $held = $false
    $holder = $null
    $ambiguous = $false

    $processes = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue)
    foreach ($p in $processes) {
        $cmd = $p.CommandLine
        if (-not $cmd) {
            # No command line to read (a permissions quirk, usually). Cannot
            # rule it out, so it counts against us.
            $ambiguous = $true
            continue
        }

        # TWO SHAPES, BOTH REAL. The Editor itself is launched
        # `-projectPath "C:\..."`; its own asset-import workers are launched
        # `"-projectPath" "C:\..."`, with the flag quoted too. A single
        # lazy-capture pattern cannot do both without also stopping at the
        # space inside "Prince's Palace-v2", so the quoted and unquoted forms
        # are two alternatives rather than one clever expression.
        if ($cmd -match '-projectPath"?\s+(?:"([^"]+)"|([^\s"]+))') {
            $candidate = if ($Matches[1]) { $Matches[1] } else { $Matches[2] }
            if ($candidate.TrimEnd('\', '/').ToLowerInvariant() -eq $normalised) {
                $held = $true
                $holder = $p.ProcessId
            }
        }
        else {
            $ambiguous = $true
        }
    }

    return [PSCustomObject]@{
        LockPath   = $lockPath
        LockExists = $lockExists
        Held       = ($held -or $ambiguous)
        Certain    = $held
        Ambiguous  = ($ambiguous -and -not $held)
        HolderPid  = $holder
        # A lockfile with nothing holding it. Safe to remove, and saying so is
        # the point -- silently removing a lockfile is how you find out the
        # hard way that it was not stale.
        Stale      = ($lockExists -and -not $held -and -not $ambiguous)
    }
}

# Removes a lockfile only when Get-UnityLockState has already established that
# nothing holds it, and says so on the way past. Callers pass the state object
# they already computed rather than re-reading the process table.
function Clear-StaleUnityLock {
    param([Parameter(Mandatory = $true)]$State)

    if (-not $State.Stale) { return $false }

    Write-Host "stale lockfile at $($State.LockPath) (no Unity.exe holds this project) -- removing it"
    Remove-Item $State.LockPath -Force -ErrorAction SilentlyContinue
    return $true
}

# Is a TestRunner copy free to be launched into? Returns $true when it is.
#
# WHY THE TEST HARNESS NEEDED THIS AT ALL. Neither tools/test.ps1 nor
# tools/run_tests_parallel.ps1 looked at a lock before launching Unity into a
# runner -- WORKFLOW.md section 1 told the HUMAN to check, and nothing in the
# tools did. AUDIT #110 is what that costs: two sessions share one pair of
# runner copies, the second one's Unity dies on "another Unity instance is
# running with this project open", and the run reports off whatever the
# previous run left on disk. #110's own fix (delete the results file before
# launching, refuse when it is absent afterwards) made that loud instead of
# silent, and this narrows the window that produces it in the first place.
#
# MEASURED, not assumed, during the 2026-09-11 runner audit:
#   - a lockfile that merely EXISTS does not stop Unity. A hand-made empty
#     Temp\UnityLockfile was placed in the runner and a slice ran green
#     straight through it. So Test-Path alone is not the question, which is
#     the same conclusion this file's header reaches for the Editor.
#   - a lockfile HELD open exclusively does stop Unity, in 2s, with the
#     "Aborting batchmode" fatal error above.
#
# REFUSES, DOES NOT WAIT. A wait needs a protocol -- how long, what if the
# holder never lets go, what if the file is debris -- and a wait on a stale
# lock hangs a run forever, which is the failure mode this file's header
# already argues against for the Editor. Refusing costs one re-run and reads
# as an instruction.
#
# BRANCHES ON .Certain, NOT .Held. .Held folds in the AMBIGUOUS case: a
# Unity.exe whose command line could not be read, which is usually a Hub
# window and names no project at all (AUDIT #94). Refusing a runner because
# some unrelated Unity exists would make this harness unusable while the
# owner's Editor is open, which is most of the time. Ambiguity is reported
# and then proceeded through: Unity's own Library lock is still the backstop,
# and #110's fix means a collision fails loudly rather than green.
function Test-RunnerFree {
    param(
        [Parameter(Mandatory = $true)][string]$RunnerPath,
        [string]$Label = "runner"
    )

    if (-not (Test-Path $RunnerPath)) { return $true }

    $state = Get-UnityLockState -ProjectRoot $RunnerPath
    if ($state.Certain) {
        Write-Host "$Label is HELD: Unity.exe pid $($state.HolderPid) has $RunnerPath open."
        Write-Host "  Two Unity instances cannot share one project copy, so this run would abort on"
        Write-Host "  'another Unity instance is running with this project open' and report nothing."
        Write-Host "  Another session is mid-run (WORKFLOW.md section 4). Wait for it and re-run."
        return $false
    }

    # Debris from a crash or a killed run. The runner copies are disposable and
    # nothing else writes here, so clearing it is safe -- but only AFTER the
    # process table has said nothing is behind it, which is the part
    # graphics_tests.ps1's unconditional delete skips.
    [void](Clear-StaleUnityLock -State $state)

    if ($state.Ambiguous) {
        Write-Host "$Label : a Unity.exe is running whose project could not be read (AUDIT #94). Proceeding; Unity's own Library lock is the backstop."
    }
    return $true
}
