param(
    [Parameter(Position = 0)][string]$Kind,
    [Parameter(Position = 1)][string]$Id,
    [string]$Vs = "",
    [string]$Formation = "",
    [double]$Speed = 1,
    [string]$PreStatus = "",
    [string]$Skill = "",
    [string]$Crop = "",
    [double]$Seconds = 0,
    [string]$Aspect = "",
    [int]$Repeat = 1,
    [double]$Darkness = 0,
    [string]$Party = "",
    [string]$Target = "",
    [string]$Manifest = "",
    [string[]]$Compare = @(),
    [string]$AllowDiff = "",
    [string]$Out = "",
    [string]$Name = "",
    [int]$Prune = -1,
    [switch]$TrimFrames,
    [switch]$KeepFrames,
    [switch]$NoPreview,
    [switch]$Stop,
    [switch]$Determinism,
    [switch]$SkipSync,
    # "-Compare a b" leaves b to bind to -Kind (and a further word to -Id); both
    # are folded into -Compare below, as in tools/spell_feel.ps1.
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest = @()
)

# One GIF recorder for every art subject -- docs/PLAN_GIF_CAPTURE.md, phases A and B.
#
#   tools/gif.ps1 spell crownfall [-Vs treant] [-Formation full] [-Speed 1.5] [-PreStatus Poison]
#   tools/gif.ps1 spell court_of_whispers -Aspect 4:3     (1440x1080 canvas, whole frame)
#   tools/gif.ps1 spell court_of_whispers -Repeat 5 -Speed 1.5   (five casts in one fight, battle pacing between, one GIF)
#   tools/gif.ps1 spell court_of_whispers -Vs rat,treant,beetle   (a mixed formation, slot 1 first)
#   tools/gif.ps1 spell gilded_aegis -Party shawn,odette,bjorn -Target odette   (an ally spell on a squadmate)
#   tools/gif.ps1 spell mud_burst -Vs rat,treant,beetle -Target 3   (a single-target spell on enemy slot 3)
#   tools/gif.ps1 spell court_of_whispers -Darkness 0.9   (the fight dressed as Bell in the Fog's round darkness at that opacity)
#   tools/gif.ps1 attack beetle [-Skill barrel_roll]   (default: the plain attack)
#   tools/gif.ps1 relic toll_of_the_flock
#   tools/gif.ps1 status Poison [-Seconds 2]
#   tools/gif.ps1 fightend <normal|elite|boss> [-Vs rat] [-Speed 1]   (one blow from victory: the final-blow stop, the fall and the run-up to the Reckoning's wipe, docs/PLAN_REWARD_SCREENS.md C0)
#   tools/gif.ps1 reveal <nocache|cachetop|ghosts>   (the Reckoning's reveal on one R0 page, from the wipe to the settled row, docs/PLAN_REWARD_SCREENS.md C3-C4)
#   tools/gif.ps1 stance Enemies/beetle     (key-pose stills, 400 ms each; no Unity)
#   tools/gif.ps1 sheet blackglass_spear    (Resources/Spells frames at the layer's fps; no Unity)
#   tools/gif.ps1 still MainMenuCaptureTests/main_menu_no_save.png
#   tools/gif.ps1 -Manifest baseline        (tools/gif_manifests/<name>.json; fight kinds in one boot, still classes in a second)
#   tools/gif.ps1 -Compare <runA> <runB> [-AllowDiff <file>]
#   tools/gif.ps1 spell crownfall -Determinism      (two boots, then -Compare)
#   tools/gif.ps1 spell censer_of_embers -Vs treant -Seconds 25   (the fight plays on after the cast until the film is 25 s: a status filmed running out)
#   tools/gif.ps1 spell vipers_bite -Vs rat -Out docs/captures/spell_rework/after/vipers_bite -Name vipers_bite_eyes2
#                                  (copies anim.gif, anim_preview.gif, strip.png, timing.json to that folder as
#                                   <Name>_<vs>[_<speed>x][_<formation>][_<status>]_{full.gif,preview.gif,strip.png,timing.json}
#                                   so pruning cannot lose them; -Name defaults to the id)
#   tools/gif.py sheet --frames 6,12,20 --in rat=<gif|run> --in treant=<gif|run> --out <png>   (contact sheet, no Unity)
#   tools/gif.ps1 -Prune <days>                (deletes whole runs older than that)
#   tools/gif.ps1 -TrimFrames                  (deletes frames/ of every run but the newest two)
#
# FRAMES. A run's frames/ are thousands of 1080p PNGs (tens of GB over a day).
# Every finished request deletes frames/ of all but the newest two runs, which
# is what -Compare and -Determinism need; the GIFs, strips and JSON stay.
# -KeepFrames skips that for one request. Frames are regenerable.
#
# The normalizing, id checks, identity and encoding are tools/gif.py; this file
# owns the Unity launch. It runs in its own project copy, <project>-Capture, with
# its own Library, and claims only that copy: gates and captures never wait on
# each other. Unity is started through Start-UnityQuiet (hidden desktop).
#
# DEADLINES (plan 2.5). Startup runs from launch until the batch writes
# ready.json: 90 minutes when the copy has no Library yet (first import), else
# 10. Each scenario then has 60 s from the moment state.json names it. A timeout
# kills Unity and names the stage or scenario that ran out.
#
# Exit 0 only when THIS request's result.json echoes this request's id and says
# ok. A file already on disk is never evidence.

$ErrorActionPreference = "Stop"

$SourceProject = Split-Path $PSScriptRoot -Parent
$ProjectLeaf = Split-Path $SourceProject -Leaf
$CaptureProject = Join-Path (Split-Path $SourceProject -Parent) "$ProjectLeaf-Capture"
$GifPy = Join-Path $PSScriptRoot "gif.py"
$RunsRoot = Join-Path $PSScriptRoot "screenshots\gif\runs"

$StartupDeadlineFirstImport = 90 * 60
$StartupDeadlineWithLibrary = 10 * 60
# Frame capture runs the game well below real time: a five-card Reckoning
# reveal with a cache writes 300+ frames and outlasts a minute of wall time.
$ScenarioDeadline = 180
$StillClassDeadline = 180

function Get-PythonExe {
    foreach ($name in @("python3", "python")) {
        $cmd = Get-Command $name -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }
    throw "Python is required (tools/gif.py)."
}

function Invoke-Gif {
    param([string[]]$GifArgs)
    $py = Get-PythonExe
    & $py $GifPy @GifArgs | Out-Host
    return $LASTEXITCODE
}

if ($Stop) {
    Write-Host "-Stop is for the warm host, which arrives in phase C. Nothing is running."
    exit 2
}
if ($Compare.Count -gt 0) {
    $Compare = @(@($Compare) + @($Kind) + @($Id) + @($Rest) | ForEach-Object { $_ -split "," } | ForEach-Object { $_.Trim().Trim('"').Trim("'") } | Where-Object { $_ })
    if ($Compare.Count -ne 2) { Write-Host "-Compare takes two run directories."; exit 2 }
    $cmpArgs = @("compare", $Compare[0], $Compare[1])
    if ($AllowDiff) { $cmpArgs += @("--allow-diff", $AllowDiff) }
    exit (Invoke-Gif -GifArgs $cmpArgs)
}
if ($TrimFrames) {
    exit (Invoke-Gif -GifArgs @("trim"))
}
if ($Prune -ge 0) {
    $cut = (Get-Date).AddDays(-$Prune)
    $old = @(Get-ChildItem $RunsRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -lt $cut })
    foreach ($d in $old) { Remove-Item -LiteralPath $d.FullName -Recurse -Force }
    Write-Host "pruned $($old.Count) run(s) older than $Prune day(s)."
    exit 0
}
if (-not $Manifest -and (-not $Kind -or -not $Id)) {
    Write-Host "Usage: tools/gif.ps1 <spell|attack|relic|status|fightend|reveal|stance|sheet|still> <id> [-Vs <enemy>] [-Formation lone|full] [-Speed <x>]"
    Write-Host "                     [-PreStatus <type>] [-Skill <id>] [-Crop subject|stage|full] [-Seconds <s>] [-Determinism] [-KeepFrames] [-NoPreview]"
    Write-Host "                     [-Aspect 4:3] [-Repeat <n>] [-Darkness <0..1>] [-Out <folder> [-Name <prefix>]]"
    Write-Host "                     [-Party shawn,odette,bjorn] [-Target <ally|enemy slot>]"
    Write-Host "       tools/gif.ps1 -Manifest <name>   |   tools/gif.ps1 -Compare <runA> <runB> [-AllowDiff <file>]   |   tools/gif.ps1 -Prune <days>   |   tools/gif.ps1 -TrimFrames"
    exit 1
}

. (Join-Path $PSScriptRoot "unity_path.ps1")
. (Join-Path $PSScriptRoot "unity_lock.ps1")
. (Join-Path $PSScriptRoot "runner_sync.ps1")

# The editor stage the log has reached, for a timeout report.
function Get-LaunchStage {
    param([string]$LogPath)
    if (-not (Test-Path -LiteralPath $LogPath)) { return "process started, no log yet" }
    $tail = @(Get-Content -LiteralPath $LogPath -Tail 400 -ErrorAction SilentlyContinue)
    $text = $tail -join "`n"
    $libMb = 0
    $lib = Join-Path $CaptureProject "Library"
    if (Test-Path -LiteralPath $lib) {
        $libMb = [int]((Get-ChildItem -LiteralPath $lib -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum / 1MB)
    }
    if ($text -match "Loaded scene") { return "PlayMode entered (scene loading)" }
    if ($text -match "Running tests for") { return "test runner starting, PlayMode about to be entered" }
    if ($text -match "Compiling Scripts|script compilation") { return "compile (Library $libMb MB)" }
    if ($text -match "Importing '|Start importing|Asset Pipeline Refresh") { return "import running (Library $libMb MB)" }
    return "process started (Library $libMb MB)"
}

function Sync-CaptureCopy {
    New-Item -ItemType Directory -Force -Path $CaptureProject | Out-Null
    foreach ($dir in @("Assets", "Packages", "ProjectSettings")) {
        robocopy "$SourceProject\$dir" "$CaptureProject\$dir" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy of $dir into the capture copy failed (exit $LASTEXITCODE)" }
    }
    [void][PP.RunnerSync.MetaSync]::Repair("$SourceProject\Assets", "$CaptureProject\Assets")
    $settings = Join-Path $CaptureProject "ProjectSettings\ProjectSettings.asset"
    $product = ($ProjectLeaf -replace "[^A-Za-z0-9]", "") + "Capture"
    Set-ProjectProductName -SettingsPath $settings -Product $product
}

# The fight scenarios of one request, in ONE Unity boot of GifBatchCaptureTests.
# Returns a failure message, or "" when the batch wrote this request's result.
function Invoke-FightBatch {
    param([string]$RunDir, [string]$RequestId, [bool]$HadLibrary, $Timing)

    $logPath = Join-Path $CaptureProject "gif.log"
    $resultsPath = Join-Path $CaptureProject "gif-results.xml"
    if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath -Force }
    if (Test-Path -LiteralPath $resultsPath) { Remove-Item -LiteralPath $resultsPath -Force }

    $env:PP_GIF_REQUEST = Join-Path $RunDir "unity_request.json"
    $UnityExe = Get-UnityExe
    $launched = [Diagnostics.Stopwatch]::StartNew()
    $proc = Start-UnityQuiet -FilePath $UnityExe -ArgumentList @(
        "-batchmode", "-silent-crashes", "-ppReferenceScreen",
        "-projectPath", "`"$CaptureProject`"",
        "-runTests", "-testPlatform", "PlayMode",
        "-testFilter", "PrincesPalace.PlayModeTests.GifBatchCaptureTests",
        "-testResults", "`"$resultsPath`"",
        "-logFile", "`"$logPath`"",
        "-buildTarget", "StandaloneWindows64")

    try {
        $startupLimit = if ($HadLibrary) { $StartupDeadlineWithLibrary } else { $StartupDeadlineFirstImport }
        $readyPath = Join-Path $RunDir "ready.json"
        $statePath = Join-Path $RunDir "state.json"
        $resultPath = Join-Path $RunDir "result.json"
        # A -Repeat series is that many casts plus the pacing between them, so its
        # deadline is that many scenarios' worth.
        $repeatByKey = @{}
        foreach ($sc in @((Get-Content -LiteralPath $env:PP_GIF_REQUEST -Raw | ConvertFrom-Json).scenarios)) {
            # A spell held on for -Seconds is filmed at a few seconds of battle per
            # real second at worst, so it earns one more scenario's worth per 5 s on top of the casts.
            $repeatByKey[$sc.key] = [math]::Max(1, [int]$sc.repeat) + [int][math]::Ceiling([double]$sc.seconds / 5)
        }
        $readyAt = $null
        $lastIndex = -1
        $scenarioStart = $null
        $failure = $null

        while ($true) {
            $done = $false
            if (Test-Path -LiteralPath $resultPath) {
                $r = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
                if ($r.requestId -eq $RequestId) { $done = $true }
            }
            if ($done) { break }
            if ($proc.HasExited) { $failure = "Unity exited (code $($proc.ExitCode)) without writing this request's result"; break }

            if ($null -eq $readyAt) {
                if (Test-Path -LiteralPath $readyPath) {
                    $ready = Get-Content -LiteralPath $readyPath -Raw | ConvertFrom-Json
                    if ($ready.requestId -eq $RequestId) {
                        $readyAt = $launched.Elapsed.TotalSeconds
                        $Timing.startupSeconds = [math]::Round($readyAt, 1)
                    }
                } elseif ($launched.Elapsed.TotalSeconds -gt $startupLimit) {
                    $failure = "STARTUP timed out after $([int]$startupLimit)s at stage: $(Get-LaunchStage -LogPath $logPath)"
                    break
                }
            } else {
                $state = $null
                if (Test-Path -LiteralPath $statePath) {
                    try { $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json } catch { $state = $null }
                }
                if ($state -and $state.requestId -eq $RequestId -and $state.index -ne $lastIndex) {
                    $lastIndex = $state.index
                    $scenarioStart = [Diagnostics.Stopwatch]::StartNew()
                } elseif ($scenarioStart -and $state -and $scenarioStart.Elapsed.TotalSeconds -gt ($ScenarioDeadline * $repeatByKey[$state.key])) {
                    $failure = "SCENARIO $($lastIndex + 1) (key $($state.key)) timed out after $($ScenarioDeadline * $repeatByKey[$state.key])s"
                    break
                }
            }
            Start-Sleep -Milliseconds 500
        }

        if ($failure) {
            Write-Host $failure
            $finished = @(Get-ChildItem $RunDir -Directory -ErrorAction SilentlyContinue |
                Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "result.json") } | ForEach-Object { $_.Name })
            Write-Host "scenarios already finished: $(if ($finished.Count) { $finished -join ', ' } else { 'none' })"
            Stop-UnityTree -Proc $proc
            Write-Host "log tail ($logPath):"
            if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Tail 40 | ForEach-Object { Write-Host "  $_" } }
        } else {
            # The result is written before the test ends; give Unity a moment to
            # exit by itself, then take it down rather than leave it holding the copy.
            [void]$proc.WaitForExit(60000)
        }
    } finally {
        Stop-UnityTree -Proc $proc
    }
    $Timing.unityTotalSeconds = [math]::Round($launched.Elapsed.TotalSeconds, 1)
    $env:PP_GIF_REQUEST = ""
    if (Test-Path -LiteralPath $logPath) { Copy-Item -LiteralPath $logPath -Destination (Join-Path $RunDir "unity.log") -Force }
    return $failure
}

# The `still` scenarios: the named PlayMode capture classes, in the Capture copy
# and in ONE boot (a semicolon list of class filters). They write PNGs under
# tools/screenshots/runtime of that copy; the folder is emptied first, and moved
# into the run directory the moment the process exits, so nothing else (a
# screenshot.ps1 run is a different copy) can clear it. Returns a failure
# message, or "" when Unity exited by itself.
function Invoke-StillBoot {
    param([string]$RunDir, [string[]]$Classes, [bool]$HadLibrary, $Timing)

    $runtimeOut = Join-Path $CaptureProject "tools\screenshots\runtime"
    if (Test-Path -LiteralPath $runtimeOut) { Remove-Item -LiteralPath $runtimeOut -Recurse -Force }
    $logPath = Join-Path $CaptureProject "gif-still.log"
    $resultsPath = Join-Path $CaptureProject "gif-still-results.xml"
    if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath -Force }
    if (Test-Path -LiteralPath $resultsPath) { Remove-Item -LiteralPath $resultsPath -Force }

    $filter = ($Classes | ForEach-Object { "PrincesPalace.PlayModeTests.$_" }) -join ";"
    $env:PP_GIF_REQUEST = ""
    $UnityExe = Get-UnityExe
    $launched = [Diagnostics.Stopwatch]::StartNew()
    $proc = Start-UnityQuiet -FilePath $UnityExe -ArgumentList @(
        "-batchmode", "-silent-crashes", "-ppReferenceScreen",
        "-projectPath", "`"$CaptureProject`"",
        "-runTests", "-testPlatform", "PlayMode",
        "-testFilter", "`"$filter`"",
        "-testResults", "`"$resultsPath`"",
        "-logFile", "`"$logPath`"",
        "-buildTarget", "StandaloneWindows64")

    try {
        $limit = $(if ($HadLibrary) { $StartupDeadlineWithLibrary } else { $StartupDeadlineFirstImport }) + $StillClassDeadline * $Classes.Count
        $failure = $null
        if (-not $proc.WaitForExit($limit * 1000)) {
            $failure = "STILL boot timed out after ${limit}s at stage: $(Get-LaunchStage -LogPath $logPath)"
            Write-Host $failure
            Stop-UnityTree -Proc $proc
            if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Tail 40 | ForEach-Object { Write-Host "  $_" } }
        }
    } finally {
        Stop-UnityTree -Proc $proc
    }
    $Timing.stillSeconds = [math]::Round($launched.Elapsed.TotalSeconds, 1)

    if (Test-Path -LiteralPath $runtimeOut) {
        Move-Item -LiteralPath $runtimeOut -Destination (Join-Path $RunDir "still_raw")
    } else {
        Write-Host "the still classes wrote nothing to tools\screenshots\runtime"
    }
    if (Test-Path -LiteralPath $resultsPath) { Copy-Item -LiteralPath $resultsPath -Destination (Join-Path $RunDir "still-results.xml") -Force }
    if (Test-Path -LiteralPath $logPath) { Copy-Item -LiteralPath $logPath -Destination (Join-Path $RunDir "unity-still.log") -Force }
    return $failure
}

# One request, at most two Unity boots (fight scenarios, then still classes).
# Returns @{ Ok; RunDir; Timing } and prints why on failure.
function Invoke-CaptureRun {
    $timing = [ordered]@{}
    $swAll = [Diagnostics.Stopwatch]::StartNew()

    $inv = [System.Globalization.CultureInfo]::InvariantCulture
    if ($Manifest) {
        $prepArgs = @("prepare", "--manifest=$Manifest")
    } else {
        $prepArgs = @("prepare", $Kind, $Id, "--vs=$Vs", "--formation=$Formation",
            "--speed=$($Speed.ToString($inv))", "--prestatus=$PreStatus", "--skill=$Skill",
            "--crop=$Crop", "--seconds=$($Seconds.ToString($inv))",
            "--aspect=$Aspect", "--repeat=$Repeat", "--darkness=$($Darkness.ToString($inv))",
            "--party=$Party", "--target=$Target")
    }
    $py = Get-PythonExe
    $prepOut = & $py $GifPy @prepArgs
    if ($LASTEXITCODE -ne 0) { return @{ Ok = $false } }
    $prep = ($prepOut | Select-Object -Last 1) | ConvertFrom-Json
    $runDir = $prep.runDir
    $requestId = $prep.requestId
    $timing.prepareSeconds = [math]::Round($swAll.Elapsed.TotalSeconds, 1)
    Write-Host "run $runDir (request $requestId): $($prep.scenarios) scenario(s), $($prep.unity) in a fight boot, still classes: $(@($prep.stillClasses).Count)"

    $failure = $null
    $needUnity = ($prep.unity -gt 0) -or (@($prep.stillClasses).Count -gt 0)
    if ($needUnity) {
        $claims = Enter-RunnerClaim -RunnerPaths @($CaptureProject) -Tool "gif.ps1"
        if ($null -eq $claims) { return @{ Ok = $false; RunDir = $runDir } }
        try {
            # The launch guard may wipe a Library of another editor; decide the
            # startup limit only after it has, or a full re-import gets the
            # with-Library limit and is killed mid-import.
            [void](Assert-LibraryProvenance -ProjectPath $CaptureProject -Tool (Get-LaunchingTool))
            $hadLibrary = Test-Path -LiteralPath (Join-Path $CaptureProject "Library\ArtifactDB")
            $sw = [Diagnostics.Stopwatch]::StartNew()
            if (-not $SkipSync) { Sync-CaptureCopy }
            $timing.syncSeconds = [math]::Round($sw.Elapsed.TotalSeconds, 1)
            # Fingerprint the tree Unity renders, not the source it was meant to mirror.
            & $py $GifPy stamp $runDir --root $CaptureProject | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "gif.py stamp failed (exit $LASTEXITCODE)" }
            $timing.hadLibrary = $hadLibrary

            if ($prep.unity -gt 0) {
                $failure = Invoke-FightBatch -RunDir $runDir -RequestId $requestId -HadLibrary $hadLibrary -Timing $timing
            }
            if (-not $failure -and @($prep.stillClasses).Count -gt 0) {
                $failure = Invoke-StillBoot -RunDir $runDir -Classes @($prep.stillClasses) -HadLibrary $hadLibrary -Timing $timing
            }
        } finally {
            Exit-RunnerClaim -Claims $claims
        }
    }

    if ($failure) { return @{ Ok = $false; RunDir = $runDir; Timing = $timing } }

    $finishArgs = @("finish", $runDir)
    if ($KeepFrames) { $finishArgs += "--keep-frames" }
    if ($NoPreview) { $finishArgs += "--no-preview" }
    if ($Out) { $finishArgs += @("--out", $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Out)) }
    if ($Name) { $finishArgs += @("--name", $Name) }
    $code = Invoke-Gif -GifArgs $finishArgs
    $timing.totalSeconds = [math]::Round($swAll.Elapsed.TotalSeconds, 1)
    Write-Host ("TIMING " + (($timing.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join " "))
    return @{ Ok = ($code -eq 0); RunDir = $runDir; Timing = $timing }
}

Start-FocusGuard
try {
    $first = Invoke-CaptureRun
    if (-not $first.Ok) { exit 1 }
    if ($Determinism) {
        $second = Invoke-CaptureRun
        if (-not $second.Ok) { exit 1 }
        Write-Host "DETERMINISM: the same scenario, two boots"
        exit (Invoke-Gif -GifArgs @("compare", $first.RunDir, $second.RunDir))
    }
    exit 0
} finally {
    Stop-FocusGuard
}
