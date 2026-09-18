# tools/preview.ps1 -- from an authored row to something you can look at.
#
# Pure ASCII, no BOM -- CLAUDE.md's PowerShell gotcha.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/preview.ps1 -Build
#   ... -Enemy <id> [-Launch] [-Formation lone|full]
#   ... -Spell <id> [-Element <DamageType>] [-Launch]
#   ... -Character <id> [-Launch]
#
# Without -Launch every mode writes pictures into tools/screenshots/preview/
# and never opens a window; with it, the Fight scene is played in the Editor.
# What each mode photographs is in its own function's header below.
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

    # The spell to look at, by its skills.json id. Who casts it, how many
    # enemies it needs to be visible against and what has to be waived are all
    # decided by Scripts/Core/PreviewFight.cs, not here -- those are content
    # questions and PowerShell has no business answering them a second time.
    [string]$Spell = "",

    # Which element of a choice skill to cast, by the DamageType's own name and
    # case-insensitively (Earth, Water, Fire, Wind, ...). Empty keeps
    # PreviewFight's rule: the first element in authored order that draws
    # anything. Validated against THAT SKILL'S OWN elements[] before any Unity
    # boots, the same way -Spell is validated against skills.json -- with four
    # elements drawn, the alternative to this flag was reordering elements[],
    # capturing, and putting the order back (AUDIT #107).
    [string]$Element = "",

    # The character to look at, by its characters.json id. Fields them alone
    # so the stage, the map figure and the dossier are all unambiguously them.
    [string]$Character = "",

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

# THE SAME VALIDATION Resolve-EnemyId DOES, and for the same reason: a typo
# costs a Unity boot to discover otherwise, and the list is right there in the
# file. What this deliberately does NOT do is decide anything about the
# preview -- which character can cast the spell, whether the effect is one the
# preview supports, what the formation should be. Those are answered once in
# PreviewFight.cs and read by both routes; a PowerShell copy would be a second
# opinion that agrees until the day it does not.
function Resolve-ContentId {
    param(
        [string]$Id,
        [string]$File,
        [string]$Collection,
        [string]$Noun
    )

    $jsonPath = Join-Path $Project "Assets\_Project\ContentData\$File"
    if (-not (Test-Path $jsonPath)) {
        Write-Host "No $File at $jsonPath."
        return $null
    }

    # $parsed, NOT $file. PowerShell variables are case-INSENSITIVE, so a
    # local called $file is the SAME variable as the [string]$File parameter:
    # assigning the parsed object to it silently replaced the filename with a
    # PSCustomObject, and the "no such id" message then printed the entire
    # contents of skills.json -- readme and all -- where the filename should
    # have been. Found the first time this ran for real.
    $parsed = Get-Content $jsonPath -Raw | ConvertFrom-Json
    $ids = @($parsed.$Collection | ForEach-Object { $_.id })

    if ($ids -contains $Id) { return $Id }

    Write-Host "No $Noun with id '$Id' in $File. It knows:"
    foreach ($known in ($ids | Sort-Object)) { Write-Host "  $known" }
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
# EVERY PP_PREVIEW_* VARIABLE IS SET HERE AND READ IN EXACTLY ONE TEST FILE,
# which PreviewEnvironmentLintTests enforces. They narrow a picture; they must
# never narrow a gate.
function Invoke-EnemyCapture {
    param([string]$Id)

    return (Invoke-PreviewCapture -Variable "PP_PREVIEW_IDS" -Value $Id -Prefix $Id)
}

# --- spell mode -------------------------------------------------------------
#
# Every question that could be answered wrongly here is answered in C# instead
# (PreviewFight.ForSpell): the caster, the formation, whether the effect is one
# a preview can honestly stand a fight up for. This script validates the id,
# builds, and then either asks the Editor to play it or asks the capture
# fixture to photograph it -- and prints whatever the answer was, refusal
# included, without paraphrasing it.
# THE SAME VALIDATION Resolve-ContentId DOES, one level down: an element is
# offered by ONE skill rather than by the file, so the list to print is that
# skill's elements[] and not a global vocabulary. Case-insensitive, and the
# CANONICAL spelling is what gets returned -- PreviewFight compares the name
# against DamageType.ToString() on the far side, and "wind" travelling all the
# way there to be refused would be a refusal about typing rather than about
# content.
#
# A skill that offers no elements at all is its own message: "it offers none"
# is a different mistake from "it does not offer that one" and sends the author
# somewhere different.
function Resolve-SpellElement {
    param(
        [string]$SkillId,
        [string]$Element
    )

    $jsonPath = Join-Path $Project "Assets\_Project\ContentData\skills.json"
    $parsed = Get-Content $jsonPath -Raw | ConvertFrom-Json
    $skill = @($parsed.skills | Where-Object { $_.id -eq $SkillId })[0]

    $offered = @()
    if ($skill.PSObject.Properties.Name -contains "elements") {
        $offered = @($skill.elements | ForEach-Object { $_.type })
    }

    if ($offered.Count -eq 0) {
        Write-Host "'$SkillId' offers no element choice at all, so -Element has nothing to pick from."
        return $null
    }

    foreach ($known in $offered) {
        if ($known -and $known.ToLowerInvariant() -eq $Element.ToLowerInvariant()) { return $known }
    }

    Write-Host "'$SkillId' does not offer element '$Element'. It offers:"
    foreach ($known in $offered) { Write-Host "  $known" }
    return $null
}

function Invoke-SpellPreview {
    param(
        [string]$Id,
        [string]$Element
    )

    $resolved = Resolve-ContentId -Id $Id -File "skills.json" -Collection "skills" -Noun "skill"
    if (-not $resolved) { return 2 }

    $chosenElement = ""
    if ($Element -ne "") {
        $chosenElement = Resolve-SpellElement -SkillId $resolved -Element $Element
        if (-not $chosenElement) { return 2 }
    }

    if (-not $NoBuild) {
        if (-not (Invoke-ContentBuild)) {
            Write-Host "content build failed -- not previewing against a tree that did not build."
            return 1
        }
    }

    if (-not $Launch) {
        # THE ELEMENT IS IN THE PREFIX TOO, matching what PreviewCaptureTests
        # names the files: four elements over one prefix would have each
        # capture overwrite the last.
        $prefix = "spell_$resolved"
        if ($chosenElement -ne "") { $prefix = "$prefix`_$($chosenElement.ToLowerInvariant())" }

        return (Invoke-PreviewCapture -Variable "PP_PREVIEW_SPELL" -Value $resolved -Prefix $prefix `
            -ExtraVariable "PP_PREVIEW_ELEMENT" -ExtraValue $chosenElement)
    }

    $timeout = Start-EditorIfNeeded
    if ($timeout -lt 0) { return 1 }

    $answer = Invoke-EditorRequest `
        -Payload @{ action = "spell"; skillId = $resolved; element = $chosenElement } -Timeout $timeout
    Write-Host "  $($answer.State): $($answer.Message)"
    if ($answer.State -ne "ok") { return 1 }
    return 0
}

# The half of Invoke-EnemyPreview that is about the EDITOR rather than about
# mobs, lifted out so -Spell does not grow its own copy of the "Unity wipes
# Temp/ on boot, so start it and wait for the lockfile before writing the
# request" sequence. Returns the timeout to wait with, or -1 on failure.
# --- character mode ---------------------------------------------------------
#
# Three pictures rather than one, because a character is three different
# drawings in three different places and each has its own way of being wrong:
# the map figure (walk stance, sized off its own aspect), the dossier portrait
# (a different image entirely from the battle art), and the fight stance sheet.
# The Step 0 baseline lost about two minutes to guessing which capture class
# fields a real squad; this asks for the character, not for a fixture.
function Invoke-CharacterPreview {
    param([string]$Id)

    $resolved = Resolve-ContentId -Id $Id -File "characters.json" -Collection "characters" -Noun "character"
    if (-not $resolved) { return 2 }

    if (-not $NoBuild) {
        if (-not (Invoke-ContentBuild)) {
            Write-Host "content build failed -- not previewing against a tree that did not build."
            return 1
        }
    }

    if (-not $Launch) {
        return (Invoke-PreviewCapture -Variable "PP_PREVIEW_CHARACTER" -Value $resolved `
            -Prefix "character_$resolved")
    }

    $timeout = Start-EditorIfNeeded
    if ($timeout -lt 0) { return 1 }

    $answer = Invoke-EditorRequest -Payload @{ action = "character"; characterId = $resolved } -Timeout $timeout
    Write-Host "  $($answer.State): $($answer.Message)"
    if ($answer.State -ne "ok") { return 1 }
    return 0
}

function Start-EditorIfNeeded {
    $lock = Get-UnityLockState -ProjectRoot $Project
    [void](Clear-StaleUnityLock -State $lock)

    if ($lock.Held) {
        Write-Host "route: the Editor is open on this project."
        return $TimeoutSeconds
    }

    Write-Host "route: no Editor on this project, so one is started and asked once it has claimed the project."
    Write-Host "(a cold Editor boot is minutes, not seconds -- leaving it open is the fast route)"

    Start-Editor
    if (-not (Wait-ForLockfile -Seconds 300)) {
        Write-Host "the Editor never claimed the project (no Temp\UnityLockfile within 300s)."
        return -1
    }

    return ([Math]::Max($TimeoutSeconds, 900))
}

# Invoke-EnemyCapture's machinery, with the environment variable and the
# filename prefix as parameters -- the sync, the fixture run and the copy-back
# are identical for every mode and were never about enemies.
function Invoke-PreviewCapture {
    param(
        [string]$Variable,
        [string]$Value,
        [string]$Prefix,

        # A SECOND variable that NARROWS a mode already asked for, rather than
        # selecting one -- -Element is the only one so far. ALWAYS SET, empty
        # included: an inherited value from an earlier shell would silently
        # photograph a different element than the command line named.
        [string]$ExtraVariable = "",
        [string]$ExtraValue = ""
    )

    $runner = (Split-Path $Project -Parent) + "\" + (Split-Path $Project -Leaf) + "-TestRunner"
    if (-not (Test-Path $runner)) {
        Write-Host "No runner copy at $runner. Run run_tests_parallel.ps1 once to create it."
        return 1
    }

    Write-Host "syncing main into the runner copy so it photographs the content you just built ..."
    robocopy "$Project\Assets" "$runner\Assets" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null

    Set-Item -Path "Env:$Variable" -Value $Value
    if ($ExtraVariable -ne "") { Set-Item -Path "Env:$ExtraVariable" -Value $ExtraValue }

    $out = Join-Path $Project "tools\screenshots\preview"
    Write-Host "capturing '$Value' -- pictures land in $out"

    & powershell -NoProfile -ExecutionPolicy Bypass `
        -File (Join-Path $PSScriptRoot "graphics_tests.ps1") `
        -Filter "PrincesPalace.PlayModeTests.PreviewCaptureTests" | Out-Host

    $exit = $LASTEXITCODE

    $runnerOut = Join-Path $runner "tools\screenshots\preview"
    if (Test-Path $runnerOut) {
        New-Item -ItemType Directory -Force -Path $out | Out-Null
        robocopy $runnerOut $out /NFL /NDL /NJH /NJS /NP | Out-Null
    }

    Get-ChildItem -Path $out -Filter "$Prefix*.png" -ErrorAction SilentlyContinue |
        ForEach-Object { Write-Host "  $($_.FullName)" }

    if ($exit -ne 0) { return 1 }
    return 0
}

function Start-Editor {
    . (Join-Path $PSScriptRoot "unity_path.ps1")
    $exe = Get-UnityExe
    $log = Join-Path $Project "Temp\preview_editor.log"
    # No -batchmode at all: this IS the interactive Editor, for -Launch to play
    # a fight in. Start-UnityQuiet's windowed path applies here too -- record
    # the caller's foreground window, open minimized, and keep it from
    # stealing focus for as long as this Editor session stays open.
    [void](Start-UnityQuiet -FilePath $exe -ArgumentList @("-projectPath", "`"$Project`"", "-logFile", "`"$log`""))
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

# -Element BELONGS TO -Spell AND TO NOTHING ELSE, and a flag that is silently
# ignored is worse than one that is refused: the author gets exactly the
# picture they would have got without it and no reason to doubt it.
if ($Element -ne "" -and $Spell -eq "") {
    Write-Host "-Element only means something with -Spell <id>; it picks which element of a choice skill is cast."
    exit 2
}

if ($Enemy -ne "") {
    exit (Invoke-EnemyPreview -Id $Enemy)
}

if ($Spell -ne "") {
    exit (Invoke-SpellPreview -Id $Spell -Element $Element)
}

if ($Character -ne "") {
    exit (Invoke-CharacterPreview -Id $Character)
}

if ($Build) {
    if (Invoke-ContentBuild) { exit 0 } else { exit 1 }
}

Write-Host "Nothing asked for. Modes:"
Write-Host "  -Build                          regenerate Resources/Content by whichever route is available"
Write-Host "  -Enemy <id> -Launch [-Formation lone|full]   build, then play a fight against that mob"
Write-Host "  -Spell <id> [-Element <type>] [-Launch]   build, then cast that spell once and photograph the impact"
Write-Host "  -Character <id> [-Launch]       build, then photograph them on the map, in the dossier and in a fight"
exit 2
