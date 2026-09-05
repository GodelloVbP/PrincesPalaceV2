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
