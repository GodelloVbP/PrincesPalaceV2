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

    # The mob to look at, by its enemies.json id.
    [string]$Enemy = "",

    # Play the fight in the Editor instead of photographing it headlessly.
    [switch]$Launch,

    # lone fields one copy; full fields three, so a summon has a slot to fail
    # on and an all-target ability has something to hit.
    [ValidateSet("lone", "full")]
    [string]$Formation = "lone",

    # Skip the content rebuild. See Invoke-ContentBuild's own note on why the
    # default is to rebuild rather than to check.
    [switch]$NoBuild,

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

# ALWAYS BUILDS, rather than checking first, and that is a considered choice
# rather than laziness. Whether the tree is stale is one sha256 over ~450 source
# files -- but the only implementation of that hash lives in Domain
# (ContentInputHash), where ContentBuilder and ContentFreshnessTests share it,
# and a second one written in PowerShell would be a second definition of "the
# inputs" that agrees with the first exactly until the day somebody adds a
# source folder. The build is 14s warm. A duplicated hash is forever. -NoBuild
# is there for the case where the author knows.
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

# --- enemy mode -------------------------------------------------------------

# VALIDATED HERE, BEFORE ANY UNITY BOOT. A typo'd id is the single most likely
# thing to be wrong about this command, and finding out 15 seconds later from a
# fight against the wrong monster -- FightBootstrap falls back to its usual pick
# when the forced id is unknown -- is the worst possible way to learn it.
function Resolve-EnemyId {
    param([string]$Id)

    $jsonPath = Join-Path $Project "Assets\_Project\ContentData\enemies.json"
    if (-not (Test-Path $jsonPath)) {
        Write-Host "No enemies.json at $jsonPath."
        return $null
    }

    $file = Get-Content $jsonPath -Raw | ConvertFrom-Json
    $ids = @($file.enemies | ForEach-Object { $_.id })

    if ($ids -contains $Id) { return $Id }

    Write-Host "No enemy with id '$Id' in enemies.json. It knows:"
    foreach ($known in ($ids | Sort-Object)) {
        # Benched monsters are worth listing: they are valid ids, they resolve,
        # and "why can I not preview it" has a real answer ("active": false
        # means no asset is written for it) that a bare absence would not give.
        $active = @($file.enemies | Where-Object { $_.id -eq $known }).active
        $suffix = if ($active -eq $false) { "   (active: false -- no asset is generated for it)" } else { "" }
        Write-Host "  $known$suffix"
    }
    return $null
}

function Invoke-EnemyPreview {
    param([string]$Id)

    $resolved = Resolve-EnemyId -Id $Id
    if (-not $resolved) { return 2 }

    if (-not $NoBuild) {
        if (-not (Invoke-ContentBuild)) {
            Write-Host "content build failed -- not previewing against a tree that did not build."
            return 1
        }
    }

    if (-not $Launch) {
        return (Invoke-EnemyCapture -Id $resolved)
    }

    $lock = Get-UnityLockState -ProjectRoot $Project
    [void](Clear-StaleUnityLock -State $lock)

    $timeout = $TimeoutSeconds

    if ($lock.Held) {
        Write-Host "route: the Editor is open on this project."
    }
    else {
        # THE EDITOR IS STARTED FIRST AND THE REQUEST IS WRITTEN AFTERWARDS,
        # and that order cost a 20-minute Editor boot to learn: UNITY WIPES
        # Temp/ WHEN IT STARTS. A request file written into Temp/ before the
        # boot is deleted by the boot, so the watcher arms itself, finds
        # nothing, and the caller waits out its whole timeout against an Editor
        # that is sitting there perfectly healthy.
        #
        # Waiting for the lockfile is what makes "afterwards" well defined:
        # Unity writes it early, right after it has claimed (and cleared) Temp/,
        # and long before it finishes importing. The request then survives the
        # rest of the boot and the watcher picks it up on its first ticks.
        Write-Host "route: no Editor on this project, so one is started and asked once it has claimed the project."
        Write-Host "(a cold Editor boot is minutes, not seconds -- leaving it open is the fast route)"

        Start-Editor
        if (-not (Wait-ForLockfile -Seconds 300)) {
            Write-Host "the Editor never claimed the project (no Temp\UnityLockfile within 300s)."
            return 1
        }

        # A cold boot imports before it ticks, so the bounded wait has to cover
        # the Editor's whole startup, not just a request's round trip.
        $timeout = [Math]::Max($TimeoutSeconds, 900)
    }

    $payload = @{ action = "preview"; enemyId = $resolved; formation = $Formation; launch = $true }
    $answer = Invoke-EditorRequest -Payload $payload -Timeout $timeout

    Write-Host "  $($answer.State): $($answer.Message)"
    if ($answer.State -ne "ok") { return 1 }
    return 0
}

# --- the picture route ------------------------------------------------------
#
# Runs the [Explicit] PreviewCaptureTests fixture through graphics_tests.ps1,
# which is the script that already knows how to boot Unity WITHOUT -nographics
# (camera.Render() is a silent no-op under that flag and ReadPixels returns
# garbage, so every capture in the project self-skips there).
#
# PP_PREVIEW_IDS IS SET HERE AND READ IN EXACTLY ONE TEST FILE, which
# PreviewEnvironmentLintTests enforces. The variable narrows a picture; it must
# never narrow a gate.
function Invoke-EnemyCapture {
    param([string]$Id)

    # graphics_tests.ps1 runs against the PRIMARY runner copy, not main -- the
    # Editor is usually open on main and two Unity instances cannot share one
    # project. So main's freshly built content has to get there first, which is
    # the same mirror run_tests_parallel.ps1 does.
    $runner = (Split-Path $Project -Parent) + "\" + (Split-Path $Project -Leaf) + "-TestRunner"
    if (-not (Test-Path $runner)) {
        Write-Host "No runner copy at $runner. Run run_tests_parallel.ps1 once to create it."
        return 1
    }

    Write-Host "syncing main into the runner copy so it photographs the content you just built ..."
    robocopy "$Project\Assets" "$runner\Assets" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null

    $env:PP_PREVIEW_IDS = $Id

    $out = Join-Path $Project "tools\screenshots\preview"
    Write-Host "capturing '$Id' -- pictures land in $out"

    & powershell -NoProfile -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot "graphics_tests.ps1") `
        -Filter "PrincesPalace.PlayModeTests.PreviewCaptureTests" | Out-Host

    if ($LASTEXITCODE -ne 0) { return 1 }

    # The runner writes into ITS OWN tools/screenshots. Bringing them back is
    # what makes "the pictures are in tools/screenshots/preview" true from where
    # the author is standing.
    $runnerOut = Join-Path $runner "tools\screenshots\preview"
    if (Test-Path $runnerOut) {
        New-Item -ItemType Directory -Force -Path $out | Out-Null
        robocopy $runnerOut $out /NFL /NDL /NJH /NJS /NP | Out-Null
    }

    Get-ChildItem -Path $out -Filter "$Id*.png" -ErrorAction SilentlyContinue |
        ForEach-Object { Write-Host "  $($_.FullName)" }

    return 0
}

function Start-Editor {
    . (Join-Path $PSScriptRoot "unity_path.ps1")
    $exe = Get-UnityExe
    $log = Join-Path $Project "Temp\preview_editor.log"
    Start-Process -FilePath $exe -ArgumentList @("-projectPath", "`"$Project`"", "-logFile", "`"$log`"") | Out-Null
    Write-Host "started the Editor; its log will be $log"
}

function Wait-ForLockfile {
    param([int]$Seconds)

    $lockPath = Join-Path $Project "Temp\UnityLockfile"
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $lockPath) {
            Write-Host "  the Editor has claimed the project; sending the request"
            return $true
        }
        Start-Sleep -Milliseconds 500
    }

    return $false
}

# --- dispatch ---------------------------------------------------------------

if ($Enemy -ne "") {
    exit (Invoke-EnemyPreview -Id $Enemy)
}

if ($Build) {
    if (Invoke-ContentBuild) { exit 0 } else { exit 1 }
}

Write-Host "Nothing asked for. Modes:"
Write-Host "  -Build                          regenerate Resources/Content by whichever route is available"
Write-Host "  -Enemy <id> -Launch [-Formation lone|full]   build, then play a fight against that mob"
exit 2
