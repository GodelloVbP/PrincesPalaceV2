param(
    [int]$Runs = 200,
    [int]$Seed = 1,
    [string]$Archetypes = "RandomLegal,GreedyAggressive,GreedyDefensive,Lookahead2",
    [string]$Profiles = "Fresh",
    [int]$DepthCap = 40,
    [double]$ReplayShare = 0.1,
    [int]$InMemorySaves = 1,
    [int]$Shards = 0,
    [switch]$SkipSync
)

# Runs a balance-bot batch headlessly, across one or several Unity instances.
#
# Mirrors run_tests.ps1's shape (sync -> divergent productName -> batchmode
# launch), swapping -runTests for -executeMethod against
# PrincesPalace.Editor.Bot.BalanceBotRunner.RunFromCommandLine. That method
# lives in Assets/_Project/Scripts/Editor/Bot/BalanceBotRunner.cs.
#
# WHERE IT RUNS
#
#   -Shards 1   the isolated PlayMode test copy (-TestRunner2, sibling
#               folder), same as this script has always used. No new project
#               copy, no Library import to pay for.
#   -Shards N   N dedicated copies, -Bot1 .. -BotN. Unity refuses a second
#               batchmode instance against a project already open, so N
#               parallel processes means N project copies and there is no way
#               around that. -TestRunner2 is deliberately NOT one of them:
#               it is shared with the commit-gate script's PlayMode leg and a
#               second live session, and a sharded batch must not be the thing
#               that clobbers a test run.
#
# The default (0) resolves to min(4, processors / 2). Half the processors
# because a Unity instance is not single-threaded even in -nographics, and
# capped at 4 because of MEMORY, not CPU:
#
#   measured, this machine (16 logical processors, 16 GB), peak working set:
#     warm run, per instance          1.1 - 1.4 GB   (~5 GB for 4 shards)
#     first run against a new copy    2.1 GB         (~8 GB for 4 shards)
#
# The asset import is the tallest part of the curve, not the batch -- so the
# expensive number is the one you pay once. Four fits 16 GB with an Editor
# open; eight would not. Raise it deliberately, on a machine you have
# measured, rather than because more sounded faster: past the point where the
# shards start swapping, a batch gets SLOWER with more of them. This script
# prints each shard's peak working set at the end of every batch, so the
# number is never a guess.
#
# SCALING IS SUBLINEAR AND THAT IS FINE. Same 12,000-run batch, warm, this
# machine: 276.7s at 1 shard, 79.5s at 4 -- 3.48x, not 4x. Each shard runs at
# 23.9ms per run-play under four-way contention against 21.0ms solo, so the
# loss is 14% per shard rather than anything pathological.
#
# TIMING CHARACTERISTICS
#
#   Unity startup, warm Library     ~20s per instance, paid once, in parallel
#   Unity startup, first creation   10-20 minutes (a full asset import, and
#                                   slower still when several import at once)
#   a run-play                      ~11ms Fresh 2 archetypes, ~21ms over all
#                                   four archetypes and all three profiles
#
# Reference point: 1000 runs/cell x 4 archetypes x 3 profiles = 12,000 runs
# and 13,200 run-plays at the default replay share, in 79.5s on 4 warm
# shards. That is 9,057 runs a minute.
#
# Startup dominates a small batch and is invisible in a large one. Below
# roughly 2,000 runs a single shard is the better trade, because N shards pay
# N robocopy syncs and the same 20s boot each while splitting work that was
# already only a few seconds.
#
# OUTPUT
#
# Each shard writes runs.jsonl / content.json / batch.json / traces.jsonl into
# <batch>/shard-<i>/ (or, at -Shards 1, into <batch>/ itself). tools/bot_merge.py
# then computes the batch's one summary.json over all of them -- a median
# cannot be merged from N medians, see that script's header -- and
# tools/bot_report.py renders it.
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

if ($Shards -le 0) {
    $Shards = [Math]::Min(4, [Math]::Max(1, [int]([Environment]::ProcessorCount / 2)))
}

# ---- which project copies this batch will use -------------------------------

function New-Shard {
    param([int]$Index, [string]$Path, [string]$Product, [bool]$IsFresh)

    return [pscustomobject]@{
        Index   = $Index
        Path    = $Path
        Product = $Product
        IsFresh = $IsFresh
        Seed    = 0
        Runs    = 0
        OutDir  = ""
        Log     = ""
        Process = $null
        PeakMb  = 0
    }
}

$shardList = @()

if ($Shards -eq 1) {
    # UNCHANGED FROM BEFORE SHARDING EXISTED. One shard is the common case
    # (an iteration batch, the smoke-sized run), and making it create and
    # import a brand new project copy would turn a 30-second answer into a
    # ten-minute one the first time somebody ran it.
    $only = "$ProjectParent\$ProjectLeaf-TestRunner2"
    if (-not (Test-Path $only)) {
        Write-Host "No $only yet. Run tools/run_tests_parallel.ps1 once to create it."
        exit 1
    }
    $shardList += New-Shard -Index 1 -Path $only -Product "${ProductLeaf}TestRunner2" -IsFresh $false
}
else {
    for ($i = 1; $i -le $Shards; $i++) {
        $path = "$ProjectParent\$ProjectLeaf-Bot$i"
        $fresh = -not (Test-Path $path)
        $shardList += New-Shard -Index $i -Path $path -Product "${ProductLeaf}Bot$i" -IsFresh $fresh
    }
}

# ---- locks ------------------------------------------------------------------

# A concurrent run_tests.ps1/run_tests_parallel.ps1 (or another bot.ps1)
# against the SAME copy would clobber this batch mid-flight -- WORKFLOW.md SS4
# says check the lock, not clear it, because -TestRunner2 is shared with the
# PlayMode leg of the commit-gate script and a second live session. A lock
# here might be real, not stale, so this refuses rather than guessing. The
# -BotN copies are this script's own and should never be locked by anything
# else; if one is, the honest reading is still "somebody is using it".
$locked = @()
foreach ($shard in $shardList) {
    $lock = Join-Path $shard.Path "Temp\UnityLockfile"
    if (Test-Path $lock) { $locked += $shard.Path }
}

if ($locked.Count -gt 0) {
    Write-Host "Locked (Temp\UnityLockfile present) -- another Unity run is using these copies:"
    $locked | ForEach-Object { Write-Host "  $_" }
    Write-Host "Wait for it to finish, or confirm the lock is stale before removing it by hand."
    exit 1
}

# ---- sync -------------------------------------------------------------------

$fresh = @($shardList | Where-Object { $_.IsFresh })
if ($fresh.Count -gt 0) {
    Write-Host ""
    Write-Host "Creating $($fresh.Count) new project copy/copies. THE FIRST RUN AGAINST A NEW COPY"
    Write-Host "PAYS A FULL ASSET IMPORT -- expect several minutes before the batch itself starts."
    Write-Host "Every later run against it boots in about 20s."
}

if (-not $SkipSync) {
    foreach ($shard in $shardList) {
        Write-Host "Syncing Assets/Packages/ProjectSettings into $($shard.Path)..."
        robocopy "$SourceProject\Assets" "$($shard.Path)\Assets" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        robocopy "$SourceProject\Packages" "$($shard.Path)\Packages" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        robocopy "$SourceProject\ProjectSettings" "$($shard.Path)\ProjectSettings" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null

        # Diverge the product name so Application.persistentDataPath (and thus
        # save_slot_*.json) never overlaps with the real project's save data,
        # with the test runners', or with another shard's. The bot keeps its
        # save in RAM now, so nothing should reach that path at all -- this is
        # the belt to that braces.
        $settingsPath = Join-Path $shard.Path "ProjectSettings\ProjectSettings.asset"
        (Get-Content $settingsPath -Raw) -replace "productName: .*", "productName: $($shard.Product)" |
            Set-Content $settingsPath -Encoding utf8
    }
}

# ---- splitting the seed range -----------------------------------------------

# EVENLY, WITH THE REMAINDER ON THE EARLY SHARDS, and contiguously: shard i
# gets a block of seeds, not every Nth seed. Contiguous blocks are what make
# the merged runs.jsonl come out in the same ORDER a single shard would have
# written it, cell by cell -- which is what makes the -Shards 1 and -Shards 2
# summaries identical rather than merely equivalent (buildDiversity breaks
# ties by completion order).
$base = [int][Math]::Floor($Runs / $shardList.Count)
$extra = $Runs % $shardList.Count
$nextSeed = $Seed

foreach ($shard in $shardList) {
    $count = $base
    if ($shard.Index -le $extra) { $count = $count + 1 }
    $shard.Seed = $nextSeed
    $shard.Runs = $count
    $nextSeed = $nextSeed + $count
}

$shardList = @($shardList | Where-Object { $_.Runs -gt 0 })
if ($shardList.Count -eq 0) {
    Write-Host "-Runs $Runs split across $Shards shards left every shard with nothing to do."
    exit 1
}

# ---- the sha the batch measured, read HERE and passed in ---------------------

# The runner copies are a robocopy of Assets/Packages/ProjectSettings only, so
# there is no .git inside them and the Editor entry cannot ask. summary.json's
# commitSha is what lets a report be matched back to the code it measured, and
# a report that cannot say what it measured is a report nobody can act on --
# so it travels as an argument rather than being looked up on the far side.
$CommitSha = "unknown"
try { $CommitSha = (& git -C $SourceProject rev-parse --short HEAD) } catch { $CommitSha = "" }
# Never left EMPTY: an empty element in Start-Process -ArgumentList collapses
# and shifts every argument after it by one, which would silently hand
# -botOut's path to -botCommit and leave the batch writing nowhere.
if (-not $CommitSha) { $CommitSha = "unknown" }

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$MainOutDir = Join-Path $SourceProject "reports\bot\$timestamp"
New-Item -ItemType Directory -Path $MainOutDir -Force | Out-Null

Write-Host ""
Write-Host "Running balance bot: $Runs runs/cell, seed $Seed, archetypes [$Archetypes], profiles [$Profiles],"
Write-Host "depth cap $DepthCap steps, replay share $ReplayShare, across $($shardList.Count) shard(s)."

# ---- launch ------------------------------------------------------------------

foreach ($shard in $shardList) {
    $shard.OutDir = Join-Path $shard.Path "bot-out"
    if (Test-Path $shard.OutDir) { Remove-Item $shard.OutDir -Recurse -Force }
    New-Item -ItemType Directory -Path $shard.OutDir -Force | Out-Null

    $shard.Log = Join-Path $shard.Path "bot-run.log"
    if (Test-Path $shard.Log) { Remove-Item $shard.Log -Force }

    $unityArgs = @(
        "-batchmode", "-nographics", "-silent-crashes",
        "-disable-assembly-updater",
        "-projectPath", "`"$($shard.Path)`"",
        "-executeMethod", "PrincesPalace.Editor.Bot.BalanceBotRunner.RunFromCommandLine",
        "-botRuns", $shard.Runs,
        "-botSeed", $shard.Seed,
        "-botArchetypes", $Archetypes,
        "-botProfiles", $Profiles,
        "-botDepthCap", $DepthCap,
        "-botReplayShare", $ReplayShare,
        "-botInMemorySaves", $InMemorySaves,
        "-botShard", "$($shard.Index)/$($shardList.Count)",
        "-botCommit", $CommitSha,
        "-botOut", "`"$($shard.OutDir)`"",
        "-logFile", "`"$($shard.Log)`"",
        "-buildTarget", "StandaloneWindows64",
        "-quit"
    )

    Write-Host "  shard $($shard.Index): seeds $($shard.Seed)..$($shard.Seed + $shard.Runs - 1) in $($shard.Path)"
    $shard.Process = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru -NoNewWindow
}

# ---- wait, sampling memory on the way ----------------------------------------

# PEAK WORKING SET, SAMPLED, because it cannot be read after the fact: the
# Process object throws once the process has exited. Five seconds is coarse
# enough to cost nothing and fine enough to catch the import spike, which is
# the tallest part of the curve and the number that decides how many shards
# this machine can hold.
$running = @($shardList)
while ($running.Count -gt 0) {
    foreach ($shard in $shardList) {
        if ($shard.Process.HasExited) { continue }
        try {
            $shard.Process.Refresh()
            $mb = [Math]::Round($shard.Process.PeakWorkingSet64 / 1MB)
            if ($mb -gt $shard.PeakMb) { $shard.PeakMb = $mb }
        }
        catch {
            # Exited between HasExited and Refresh. Not a finding.
        }
    }

    $running = @($shardList | Where-Object { -not $_.Process.HasExited })
    if ($running.Count -gt 0) { Start-Sleep -Seconds 5 }
}

# ---- collect -----------------------------------------------------------------

$failed = @()
foreach ($shard in $shardList) {
    # runs.jsonl, not summary.json: a shard writes FACTS and bot_merge.py
    # computes the one summary for the whole batch.
    $runsPath = Join-Path $shard.OutDir "runs.jsonl"
    if (-not (Test-Path $runsPath)) {
        $failed += $shard
        continue
    }

    # At one shard the files go straight into the batch directory, which is
    # the layout every batch before sharding had and the one bot_merge.py
    # falls back to.
    $dest = if ($shardList.Count -eq 1) { $MainOutDir } else { Join-Path $MainOutDir "shard-$($shard.Index)" }
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    robocopy $shard.OutDir $dest /E /NFL /NDL /NJH /NJS /NP | Out-Null
}

if ($failed.Count -gt 0) {
    Write-Host ""
    foreach ($shard in $failed) {
        Write-Host "Shard $($shard.Index) produced no runs.jsonl (Unity exit code $($shard.Process.ExitCode)). Tail of its log:"
        if (Test-Path $shard.Log) { Get-Content $shard.Log -Tail 40 | ForEach-Object { Write-Host $_ } }
    }
    exit 1
}

Write-Host ""
Write-Host "Peak working set per shard:"
foreach ($shard in $shardList) {
    Write-Host "  shard $($shard.Index): $($shard.PeakMb) MB"
}

# ---- merge and report ---------------------------------------------------------

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    Write-Host "No 'python' on PATH -- the shard files are in $MainOutDir, but summary.json and the HTML both need it. Run tools/bot_merge.py then tools/bot_report.py by hand once python is available."
    exit 1
}

Write-Host ""
Write-Host "Merging shards..."
& python (Join-Path $PSScriptRoot "bot_merge.py") $MainOutDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Building report..."
& python (Join-Path $PSScriptRoot "bot_report.py") $MainOutDir --out (Join-Path $MainOutDir "report.html")
$reportExit = $LASTEXITCODE

# The item-offer tables ride beside the report: same shards, no summary.json
# involved (it reads rooms[].offers straight off runs.jsonl). Its exit code
# is not folded into the report's -- a batch whose offers page failed still
# has a report worth reading.
Write-Host ""
Write-Host "Building offers page..."
& python (Join-Path $PSScriptRoot "bot_offers.py") $MainOutDir --out (Join-Path $MainOutDir "offers.html")

Write-Host ""
Write-Host "Batch: $MainOutDir"
Write-Host "Report: $(Join-Path $MainOutDir "report.html")"
Write-Host "Offers: $(Join-Path $MainOutDir "offers.html")"

exit $reportExit
