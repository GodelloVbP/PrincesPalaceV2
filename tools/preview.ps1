# tools/preview.ps1 -- from an authored row to something you can look at.
#
# Pure ASCII, no BOM -- CLAUDE.md's PowerShell gotcha.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/preview.ps1 -Build
#
# ONE COMMAND, TWO ROUTES, AND THE AUTHOR PICKS NEITHER.
#
# Unity locks a project's Library exclusively, so how content gets regenerated
# depends entirely on whether the Editor happens to be open -- which is not a
# thing anyone should have to think about mid-edit, and is exactly the sort of
# environmental detail that turns a 15-second job into a five-minute one when
# you get it wrong. So this looks at Temp\UnityLockfile (and, because that file
# outlives a crash, at the process table) and picks:
#
#   Editor closed -> tools/build_content.ps1, batchmode, in place.
#   Editor open   -> a request file that Editor/PreviewRequestWatcher.cs picks
#                    up on its update tick. That watcher's header carries the
#                    whole protocol; this is the other half of it.
#
# WHAT THIS IS NOT: the commit gate. It never runs the full suite. Verification
# stays run_tests_parallel.ps1 and is a separate decision from "show me the
# thing I just authored" -- coupling them is what made the fast loop slow.
param(
    # Regenerate the content assets and stop. The other modes (-Enemy, and in
    # Step 2 -Spell/-Character) build first and then show something.
    [switch]$Build,

    # Bounded, because an Editor that is compiling can legitimately take a
    # while and an Editor that has crashed will never answer at all. On expiry
    # the last state seen is reported rather than a bare "timed out".
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

$Project = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "unity_lock.ps1")

# --- the Editor-open route --------------------------------------------------
#
# Writes Temp/pp_request.<guid>.json and then MOVES it onto
# Temp/pp_request.json without overwriting, so a second concurrent request
# fails here rather than silently replacing one the Editor is halfway through.
function Invoke-EditorRequest {
    param(
        [Parameter(Mandatory = $true)][hashtable]$Payload,
        [int]$Timeout = 180
    )

    $requestId = [guid]::NewGuid().ToString()
    $Payload["requestId"] = $requestId

    $temp = Join-Path $Project "Temp"
    New-Item -ItemType Directory -Force -Path $temp | Out-Null

    $staging = Join-Path $temp "pp_request.$requestId.json"
    $target = Join-Path $temp "pp_request.json"
    $resultFile = Join-Path $temp "pp_result.json"

    ($Payload | ConvertTo-Json -Compress) | Set-Content -Path $staging -Encoding ascii

    try {
        # NOT Move-Item, which overwrites happily on -Force and whose failure
        # mode without it is a non-terminating error. File.Move throws on an
        # existing destination, which is the guarantee wanted here.
        [System.IO.File]::Move($staging, $target)
    }
    catch {
        Remove-Item $staging -Force -ErrorAction SilentlyContinue
        Write-Host "A preview request is already in flight ($target exists)."
        Write-Host "Wait for it, or delete that file if you are sure the Editor is not acting on it."
        return [PSCustomObject]@{ State = "failed"; Message = "concurrent request" }
    }

    Write-Host "asked the open Editor (request $requestId); waiting up to ${Timeout}s ..."

    $deadline = (Get-Date).AddSeconds($Timeout)
    $lastState = "no answer yet"
    $announced = ""

    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 300

        if (-not (Test-Path $resultFile)) { continue }

        try { $result = Get-Content $resultFile -Raw | ConvertFrom-Json } catch { continue }

        # A RESULT THAT IS NOT OURS IS NOT A RESULT. Temp/ survives a preview
        # that timed out, so the file sitting there may answer somebody else's
        # question entirely.
        if ($result.requestId -ne $requestId) { continue }

        $lastState = $result.state

        if ($result.state -eq "busy") {
            # Said once, not every 300ms.
            if ($announced -ne "busy") {
                Write-Host "  editor busy: $($result.message)"
                $announced = "busy"
            }
            continue
        }

        return [PSCustomObject]@{ State = $result.state; Message = $result.message }
    }

    # Our request may still be sitting there unread -- an Editor that never
    # answered is an Editor that will act on it whenever it wakes up, which is
    # not what the author who gave up wants.
    if (Test-Path $target) { Remove-Item $target -Force -ErrorAction SilentlyContinue }

    return [PSCustomObject]@{ State = "timeout"; Message = "no answer within ${Timeout}s (last state: $lastState)" }
}

# --- route ------------------------------------------------------------------

function Invoke-ContentBuild {
    $lock = Get-UnityLockState -ProjectRoot $Project
    [void](Clear-StaleUnityLock -State $lock)

    if ($lock.Held) {
        Write-Host "route: the Editor is open on this project, so the build goes through it."
        $answer = Invoke-EditorRequest -Payload @{ action = "build" } -Timeout $TimeoutSeconds
        Write-Host "  $($answer.State): $($answer.Message)"
        return ($answer.State -eq "ok")
    }

    Write-Host "route: no Editor on this project, so batchmode."

    # | Out-Host, AND IT IS NOT COSMETIC. A native call's stdout lands in the
    # CALLING FUNCTION'S pipeline, so without this every line build_content.ps1
    # printed -- the Mark timings included -- was swallowed into this
    # function's return value, and the caller's `if` then saw a non-empty array
    # and read it as success no matter what the build did. Out-Host puts the
    # child's output where the author can see it and leaves the boolean below
    # as the only thing this function returns.
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "build_content.ps1") | Out-Host
    return ($LASTEXITCODE -eq 0)
}

if ($Build) {
    if (Invoke-ContentBuild) { exit 0 } else { exit 1 }
}

Write-Host "Nothing asked for. Modes:"
Write-Host "  -Build            regenerate Resources/Content by whichever route is available"
exit 2
