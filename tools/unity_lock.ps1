# tools/unity_lock.ps1 -- is a Unity Editor actually holding this project?
#
# Dot-sourced by build_content.ps1, preview.ps1, test.ps1,
# run_tests_parallel.ps1, graphics_tests.ps1, screenshot.ps1 and bot.ps1.
# Never invoked directly. Every tool that mirrors into or launches Unity in a
# runner copy takes it through Enter-RunnerClaim ("Runner claims", below).
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

# --- Runner claims: one session per runner copy, for the WHOLE of its use ----
#
# WHY THE HARNESS LOCKS AT ALL. Two sessions share the runner copies
# (WORKFLOW.md section 4). AUDIT #110 is what an unlocked launch costs: the
# second session's Unity dies on "another Unity instance is running with this
# project open" and the run reports off whatever the previous run left on
# disk. The first fix, Test-RunnerFree (this file, 2026-09-11 to 2026-09-26),
# read the process table once before the sync and refused on a held copy.
# That only sees a holder while its Unity.exe is running, and a run's use of a
# copy is longer than that: it mirrors main in (robocopy /MIR), clears output
# folders, launches Unity, and copies results and frames back out after Unity
# has exited. Before the sync and after the exit nothing names the copy, so a
# second session checking then saw "free" and mirrored over the first one's
# sync or cleared its frames. And the capture tools did not check at all:
# graphics_tests.ps1 (which preview.ps1 goes through) deleted whatever
# Temp\UnityLockfile it found on the belief that the copy was "ours alone" --
# on 2026-09-26 that was another session's live lock, mid-run -- and
# screenshot.ps1 and bot.ps1 mirrored in with, at best, a Test-Path.
#
# MEASURED, not assumed, during the 2026-09-11 runner audit:
#   - a Unity lockfile that merely EXISTS does not stop Unity. A hand-made
#     empty Temp\UnityLockfile was placed in a runner and a slice ran green
#     straight through it. So Test-Path is not the question.
#   - a lockfile HELD open exclusively does stop Unity, in 2s, with "Aborting
#     batchmode".
#
# THE CLAIM is a file at <runner>\.pp-runner-claim held OPEN by the claiming
# process with write sharing denied -- the same "held open exclusively"
# property that makes Unity's own lockfile work (MEASURED, above). Two
# consequences carry the design:
#   - it cannot go stale. Windows closes the handle when the process exits,
#     however it exits (crash, kill, reboot), so there is no debris to judge
#     and no staleness rule to get wrong. The file's CONTENTS (pid, tool,
#     since) only let a waiter say who it is waiting for; an unheld file with
#     old contents is simply free.
#   - nothing ever deletes it. A contender that cannot open it waits.
#
# WAITS, BOUNDED, rather than refusing. Test-RunnerFree refused because a
# wait on a stale lock hangs forever -- which a handle-held claim cannot be. The bound is PP_RUNNER_WAIT_SECONDS (default
# 1200: a full gate run with a cold import fits in it; a hung holder does not
# keep a waiter forever); 0 means refuse at once. Unity's own lock is still
# checked INSIDE the claim: a Unity.exe that certainly has the copy open is
# waited on too (an orphan whose script was killed, or one launched by hand),
# and a lockfile nothing holds is cleared by Clear-StaleUnityLock -- the one
# staleness rule, process table first, never the file's mere presence.
#
# BRANCHES ON .Certain, NOT .Held. .Held folds in the AMBIGUOUS case: a
# Unity.exe whose command line could not be read, usually a Hub window naming
# no project at all (AUDIT #94). Waiting on a runner because some unrelated
# Unity exists would make the harness unusable while the owner's Editor is
# open, which is most of the time. Ambiguity is reported and proceeded
# through; Unity's own Library lock is the backstop, and #110's per-run
# results names make a collision fail loudly rather than green.
#
# NESTED TOOLS. preview.ps1 claims the runner, then runs graphics_tests.ps1
# as a CHILD process, and static_pilot_qa.ps1 runs it in-process. Either
# would otherwise wait on its own caller until the bound ran out. So a claim
# whose recorded pid is this process or one of its ANCESTORS counts as
# already held (Owned = $false, releasing it is a no-op). The recorded pid is
# trustworthy because the file is only unreadable-for-write while its writer
# is alive to hold it. A sibling or unrelated session is never an ancestor.
#
# ORDER. A caller that needs several copies passes them all in one call; they
# are claimed in sorted path order and, on a timeout, every one already taken
# is let go -- two sessions each holding one runner and waiting on the
# other's is the deadlock that avoids.

$RunnerClaimFileName = ".pp-runner-claim"

function Get-RunnerWaitSeconds {
    $raw = $env:PP_RUNNER_WAIT_SECONDS
    $n = 0
    if ($raw -and [int]::TryParse($raw, [ref]$n) -and $n -ge 0) { return $n }
    return 1200
}

function Read-RunnerClaimHolder {
    param([Parameter(Mandatory = $true)][string]$ClaimPath)
    $text = ""
    try {
        $fs = [System.IO.File]::Open($ClaimPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        try {
            $reader = New-Object System.IO.StreamReader($fs)
            $text = $reader.ReadToEnd()
        } finally { $fs.Dispose() }
    } catch { return @{} }
    $holder = @{}
    foreach ($line in ($text -split "`r?`n")) {
        if ($line -match '^(\w+)=(.*)$') { $holder[$Matches[1]] = $Matches[2] }
    }
    return $holder
}

# Is $HolderPid this process or one of its ancestors?
function Test-IsSelfOrAncestor {
    param([int]$HolderPid)
    $cur = $PID
    for ($i = 0; $i -lt 32 -and $cur -gt 0; $i++) {
        if ($cur -eq $HolderPid) { return $true }
        $row = Get-CimInstance Win32_Process -Filter "ProcessId = $cur" -ErrorAction SilentlyContinue
        if (-not $row) { return $false }
        $next = [int]$row.ParentProcessId
        if ($next -eq $cur) { return $false }
        $cur = $next
    }
    return $false
}

# One attempt at one runner. Returns a claim object, or $null when another
# process holds it ($Holder is then filled in for the message).
function Open-RunnerClaim {
    param(
        [Parameter(Mandatory = $true)][string]$RunnerPath,
        [Parameter(Mandatory = $true)][string]$Tool,
        [ref]$Holder
    )
    if (-not (Test-Path -LiteralPath $RunnerPath)) {
        New-Item -ItemType Directory -Force -Path $RunnerPath | Out-Null
    }
    $claimPath = Join-Path $RunnerPath $RunnerClaimFileName
    try {
        $fs = [System.IO.File]::Open($claimPath, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::Read)
    } catch [System.IO.IOException] {
        $h = Read-RunnerClaimHolder -ClaimPath $claimPath
        $holderPid = 0
        if ($h['pid'] -and [int]::TryParse($h['pid'], [ref]$holderPid) -and (Test-IsSelfOrAncestor -HolderPid $holderPid)) {
            return [PSCustomObject]@{ RunnerPath = $RunnerPath; ClaimPath = $claimPath; Stream = $null; Owned = $false }
        }
        $Holder.Value = $h
        return $null
    }
    $body = "pid=$PID`r`ntool=$Tool`r`nsince=$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))`r`n"
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($body)
    $fs.SetLength(0)
    $fs.Write($bytes, 0, $bytes.Length)
    $fs.Flush()
    return [PSCustomObject]@{ RunnerPath = $RunnerPath; ClaimPath = $claimPath; Stream = $fs; Owned = $true }
}

function Format-RunnerClaimHolder {
    param($Holder)
    if (-not $Holder -or -not $Holder['pid']) { return "another process (its claim file could not be read yet)" }
    return "$($Holder['tool']) (pid $($Holder['pid']), since $($Holder['since']))"
}

# Claims every runner in $RunnerPaths for this process, waiting up to
# $WaitSeconds in total. Returns the claims -- hand them to Exit-RunnerClaim
# in a finally -- or $null after saying why, in which case nothing is held.
# Process exit releases them too, so a forgotten Exit-RunnerClaim costs the
# rest of this process's lifetime, never a stale lock.
#
# An EMPTY $RunnerPaths is a run that needs no runner (a dotnet-only slice
# of test.ps1, say): it claims nothing and returns an empty set, never
# $null, so every caller's "$null -eq" refusal check passes it through.
# Without AllowEmptyCollection a Mandatory [string[]] refuses @() at
# binding and the dotnet-only fast loop died before running a single test
# (regression from 46ffce90). Blank entries are dropped for the same reason.
function Enter-RunnerClaim {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][AllowEmptyString()][AllowNull()][string[]]$RunnerPaths,
        [Parameter(Mandatory = $true)][string]$Tool,
        [int]$WaitSeconds = -1
    )
    if ($WaitSeconds -lt 0) { $WaitSeconds = Get-RunnerWaitSeconds }
    $deadline = (Get-Date).AddSeconds($WaitSeconds)
    $claims = @()

    foreach ($path in @($RunnerPaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)) {
        $announced = $false
        $lastNote = Get-Date
        while ($true) {
            $holder = $null
            $claim = Open-RunnerClaim -RunnerPath $path -Tool $Tool -Holder ([ref]$holder)
            $waitingOn = $null
            if ($claim) {
                # Inside the claim: is a Unity outside the protocol still in
                # there? See ".Certain, NOT .Held" above.
                $state = Get-UnityLockState -ProjectRoot $path
                if ($state.Certain) {
                    $waitingOn = "Unity.exe pid $($state.HolderPid), which has it open outside any claim"
                    Exit-RunnerClaim -Claims @($claim)
                } else {
                    [void](Clear-StaleUnityLock -State $state)
                    if ($state.Ambiguous) {
                        Write-Host "$path : a Unity.exe is running whose project could not be read (AUDIT #94). Proceeding; Unity's own Library lock is the backstop."
                    }
                    $claims += $claim
                    if ($announced) { Write-Host "runner released, claimed: $path" }
                    break
                }
            } else {
                $waitingOn = Format-RunnerClaimHolder $holder
            }

            if ((Get-Date) -ge $deadline) {
                Write-Host "RUNNER BUSY: $path is held by $waitingOn."
                Write-Host "  Waited ${WaitSeconds}s (PP_RUNNER_WAIT_SECONDS) and it was not released. Nothing was"
                Write-Host "  mirrored into it and no lock was touched. Re-run once that session finishes."
                Exit-RunnerClaim -Claims $claims
                return $null
            }
            if (-not $announced) {
                Write-Host "waiting for $path -- held by $waitingOn (up to ${WaitSeconds}s in total)"
                $announced = $true
                $lastNote = Get-Date
            } elseif (((Get-Date) - $lastNote).TotalSeconds -ge 60) {
                Write-Host "  still waiting for $path -- held by $waitingOn"
                $lastNote = Get-Date
            }
            Start-Sleep -Seconds 2
        }
    }
    return ,$claims
}

# Lets go of claims Enter-RunnerClaim returned. Safe on $null, on a nested
# (not-owned) claim and on one already released. Never deletes the claim
# file: an unheld file is free, and deleting it would race a contender that
# has just opened it.
function Exit-RunnerClaim {
    param($Claims)
    foreach ($c in @($Claims)) {
        if ($c -and $c.Owned -and $c.Stream) {
            try { $c.Stream.Dispose() } catch { }
            $c.Stream = $null
        }
    }
}

# --- Results files: only THIS run's Unity may produce this run's verdict ----
#
# AUDIT #110. A gate ignores Unity's exit code (run_tests_parallel.ps1's
# header says why), so the results XML is the only signal -- and a fixed
# name in a SHARED runner copy cannot say who wrote it. The incident: Unity
# refused to start because another session had taken -TestRunner2 between
# the lock check and the launch, the script read the test-results-PlayMode.xml
# an earlier, narrower run had left there, printed 39/39 and ended "All tests
# passed" having run 4% of the suite. Deleting the file before launch closes
# the stale-file half; it does not close the other half, where the session
# that took the copy writes the SAME fixed name while this run is still
# waiting on its other runners, and that file is read as this run's.
#
# So Unity is never handed the fixed name. Each run hands it a name no other
# process was ever given (New-RunResultsPath: the fixed name plus a run id of
# timestamp and this script's PID), and only a file at THAT path counts
# (Complete-RunResults). Once it is there it is moved onto the fixed name, so
# every reader that knows the historical file names -- the shard merge,
# coverage checks, docs/TESTING.md, a human -- still finds it where it always
# was, now guaranteed to be this run's.

function New-TestRunId {
    return ("{0}-{1}" -f (Get-Date -Format "yyyyMMddHHmmss"), $PID)
}

function New-RunResultsPath {
    param(
        [Parameter(Mandatory = $true)][string]$CanonicalPath,
        [Parameter(Mandatory = $true)][string]$RunId
    )
    $dir = Split-Path $CanonicalPath -Parent
    $leaf = [System.IO.Path]::GetFileNameWithoutExtension($CanonicalPath)
    return (Join-Path $dir "$leaf.run-$RunId.xml")
}

# Deletes an earlier run's output at a fixed name. A delete that FAILS is a
# refusal, not a warning: a file that survived here is exactly the one a
# later reader would take for this run's. Returns $true when the path is
# clear.
function Clear-StaleResults {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $true }
    try {
        Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
    } catch {
        Write-Host "Cannot delete the earlier run's $Path ($($_.Exception.Message)). Refusing: it would be read as this run's results."
        return $false
    }
    return (-not (Test-Path -LiteralPath $Path))
}

# $true when this run's Unity wrote $RunPath, which is then moved onto
# $CanonicalPath. $false when it did not (Unity never started, aborted, or
# crashed before writing) -- whatever sits at $CanonicalPath is then NOT this
# run's and must not be read as a verdict.
function Complete-RunResults {
    param(
        [Parameter(Mandatory = $true)][string]$RunPath,
        [Parameter(Mandatory = $true)][string]$CanonicalPath
    )
    if (-not (Test-Path -LiteralPath $RunPath)) { return $false }
    try {
        Move-Item -LiteralPath $RunPath -Destination $CanonicalPath -Force -ErrorAction Stop
    } catch {
        Write-Host "This run's results are at $RunPath but could not be moved onto $CanonicalPath ($($_.Exception.Message))."
        return $false
    }
    return $true
}
