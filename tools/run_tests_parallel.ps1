param(
    [switch]$SkipSync,
    [switch]$BuildContent,
    [switch]$BuildScenes,
    [switch]$NoScenes,
    # PlayMode runner copies, split by fixture. 1 is the pre-sharding shape
    # exactly: one PlayMode process in -TestRunner2, no -testFilter.
    [ValidateRange(1, 6)]
    [int]$Shards = 3,
    # THE COMMIT GATE: run only the tests the uncommitted change can affect
    # (tools/test_select.ps1), promoted to the full run by the resolver's
    # full-suite tier, an unmapped file, or the safety net. Never a quiet
    # subset: every promotion prints its reason.
    [switch]$Changed,
    # Resolve these paths instead of the working tree (forward slashes,
    # repo-relative). For checking what a change WOULD select; the gate
    # itself uses the working tree.
    [string[]]$ChangedPaths,
    # Print the selection and the shard plan, then stop before any sync.
    [switch]$DryRun
)

# SCENES ARE NOT BUILT UNLESS -BuildScenes IS PASSED. Building them on every
# run was tried and measured, not assumed away: the EditMode screen tests
# (FightScreenTests, MapScreenTests, HubScreenTests, etc.) already exercise
# UiAudit against a freshly-solved layout as part of the normal suite, so a
# scene rebuild bought this script nothing that the test run wasn't already
# checking. What it did buy was cost: a rebuild with NO source change still
# rewrites every fileID in all five scenes -- 165,849 lines out and the same
# 165,849 back, none of which mean anything -- which is why the sync-back
# stayed behind its own flag even while the build itself ran unconditionally.
#
# -BuildScenes now does both halves at once: build the scenes in the primary
# runner (so PlayMode tests against them and the generation sentinels run),
# AND copy them back to main and into the PlayMode runner. Use it whenever you
# changed a screen tree under Domain/UiKit/Screens/ or wiring in
# Editor/SceneBuilder/ScreenRegistry.cs -- PlayMode otherwise loads
# whatever scenes happen to already be on disk, which is the prior source's
# layout, not yours. Use it again before committing such a change, for the
# same reason.
#
# -NoScenes is no longer needed -- no build means nothing to opt out of -- but
# stays accepted as a no-op so muscle memory doesn't hard-fail.
if ($NoScenes) {
    Write-Host "-NoScenes is a no-op now: scenes are not built by default. Pass -BuildScenes if you need them."
}
$BuildScenesHere = $BuildScenes
$SyncScenesToMain = $BuildScenes

# Runs EditMode and PlayMode CONCURRENTLY against two separate isolated copies
# of the project.
#
# The serial predecessor (tools/run_tests.ps1, deleted at 5d46970d as part
# of AUDIT #136) ran them one after the other, and each paid a full Unity
# startup. They cannot share a project directory -- Unity takes an exclusive
# lock on Library/ -- so parallelism needs a second copy rather than a second
# process. Wall clock drops to roughly the slower of the two platforms.
#
# Every copy gets a divergent productName so Application.persistentDataPath
# (and therefore save_slot_*.json) can never collide with the real project OR
# with each other.
#
# PLAYMODE IS SHARDED (-Shards, default 3). PlayMode was 338s in one process
# while EditMode took 23s beside it, so the PlayMode process WAS the gate.
# Shard i runs in -TestRunner<i+1> (-TestRunner2, -TestRunner3, ...) on its
# own slice of fixtures -- tools/playmode_shards.ps1 says how they are cut and
# balanced. The -Bot<N> copies are tools/bot.ps1's and are never used here.
#
# -Changed IS THE COMMIT GATE. The same machinery on a selected slice: the
# EditMode runner gets a -testFilter of the selected EditMode classes, the
# PlayMode shards contiguous ranges of the selected PlayMode classes, one
# shard per ~60s of recorded fixture time. Every guard below still runs. It
# promotes itself to the full run, printing why, when: the safety net fires
# (tools/test_select.ps1's Get-FullRunPromotion), -BuildScenes is passed
# (every scene is rewritten, and a scene change forces the full suite), or
# the selection itself says full. A green FULL run records HEAD in
# tools/.last-full-green; a red one prints the map-gap report.

# DERIVED, never hardcoded. Both of these were literal v1 paths, so the harness
# would happily drive the wrong project -- and the editor version moved the
# moment ProjectVersion.txt did.
$SourceProject = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "unity_path.ps1")
# Test-RunnerFree -- see its header in tools/unity_lock.ps1. AUDIT #110 is the
# reason it exists: this script had no lock check at all, so a runner another
# session already had open was discovered only when Unity aborted into it.
. (Join-Path $PSScriptRoot "unity_lock.ps1")
$UnityExe = Get-UnityExe

# Process-tree focus guard, for this script's WHOLE run -- see
# Start-FocusGuard's header in tools/unity_path.ps1 for why this replaced a
# per-Unity-launch watchdog. Everything from here to the end of the file is
# wrapped in try/finally so Stop-FocusGuard runs on every exit path,
# including the various "exit 1"s below; try/finally does not introduce a new
# variable scope in PowerShell, so nothing else in this script changes.
Start-FocusGuard
try {

# Shares discovery, areas and the structural gate with tools/test.ps1, so the
# gate below checks against the exact same definitions a slice would use.
. (Join-Path $PSScriptRoot "test_areas.ps1")
. (Join-Path $PSScriptRoot "playmode_shards.ps1")
. (Join-Path $PSScriptRoot "test_select.ps1")

$ProjectLeaf = Split-Path $SourceProject -Leaf
$ProjectParent = Split-Path $SourceProject -Parent
$ProductLeaf = ($ProjectLeaf -replace "[^A-Za-z0-9]", "")

# The EditMode runner is FIRST and stays the primary: generation runs there.
# Label is the key every message and table uses; at one PlayMode shard it is
# plain "PlayMode" and its results file keeps its historical name.
#
# A -Changed slice can leave a platform with nothing to run: the EditMode copy
# is still listed when it has to generate (-BuildContent), with RunTests off,
# and dropped otherwise; zero PlayMode shards means no PlayMode copy at all.
function New-GateRunners {
    param([int]$PlayShards, [bool]$EditTests, [bool]$EditNeeded)
    $list = @()
    if ($EditTests -or $EditNeeded) {
        $list += @{ Platform = "EditMode"; Shard = 0; Label = "EditMode"; Path = "$ProjectParent\$ProjectLeaf-TestRunner"; Product = "${ProductLeaf}TestRunner"; Results = "test-results-EditMode.xml"; RunTests = $EditTests }
    }
    for ($i = 1; $i -le $PlayShards; $i++) {
        $n = $i + 1
        $label = if ($PlayShards -eq 1) { "PlayMode" } else { "PlayMode#$i" }
        $results = if ($PlayShards -eq 1) { "test-results-PlayMode.xml" } else { "test-results-PlayMode-shard$i.xml" }
        $list += @{ Platform = "PlayMode"; Shard = $i; Label = $label; Path = "$ProjectParent\$ProjectLeaf-TestRunner$n"; Product = "${ProductLeaf}TestRunner$n"; Results = $results; RunTests = $true }
    }
    return $list
}
$Runners = New-GateRunners -PlayShards $Shards -EditTests $true -EditNeeded $true

# --- worktree refusal --------------------------------------------------
# This script has no filter and no dotnet host for PlayMode -- every
# invocation builds BOTH $Runners paths above through Unity. From a linked
# worktree those paths land beside the WORKTREE, not beside the main repo
# (see Test-IsLinkedWorktree's header in test_areas.ps1), so there is no
# partial form of this run that is safe to allow: refuse outright, before
# paying for discovery or a sync. tools/test.ps1 remains usable from a
# worktree for named [D] dotnet-hosted classes.
if (Test-IsLinkedWorktree) {
    Write-Host "REFUSED: this is a linked worktree ($SourceProject)."
    Write-Host "run_tests_parallel.ps1 has no dotnet path for PlayMode and always runs BOTH platforms through Unity, so it would build sibling project copies beside the WORKTREE rather than beside the main repo:"
    foreach ($r in $Runners) { Write-Host "  [U] $($r.Label) -> $($r.Path)" }
    Write-Host ""
    Write-Host "Run named [D] dotnet-hosted classes only (tools/test.ps1 -List marks each [D]/[U]) from a worktree, or run this from the main tree."
    exit 1
}

# Elapsed-time stamps on every phase.
#
# Added because "the build is slow" could not be acted on: the generation
# phase was the obvious suspect and turned out not to be the cost at all.
# Every phase boundary prints its own duration now, so the next person with a
# slow run reads the answer instead of bisecting for it.
$Watch = [Diagnostics.Stopwatch]::StartNew()
$LastStamp = [TimeSpan]::Zero

function Stamp {
    param($What)
    $now = $Watch.Elapsed
    $delta = $now - $script:LastStamp
    $script:LastStamp = $now
    Write-Host ("  [{0,6:N1}s  +{1,5:N1}s] {2}" -f $now.TotalSeconds, $delta.TotalSeconds, $What)
}

# The sync machinery -- the compiled meta repair and GUID check, and the
# per-runner bodies run concurrently -- lives in tools/runner_sync.ps1, which
# also carries the "why the meta repair exists" history. The sharding plan
# and results merge live in tools/playmode_shards.ps1.
. (Join-Path $PSScriptRoot "runner_sync.ps1")

# Verifies every .meta under $SourceProject\Assets resolves to the SAME guid
# in $DestAssets, and fails the whole run loudly, naming the exact asset,
# if even one does not.
#
# Repair-Metas is a same-hash-fixes-itself pass; this is the check that
# Repair-Metas actually WORKED, added because BloodlustRelic_
# GrantsAnImmediateExtraTurnAfterAKillingBlow failed 4 times in one night,
# always on a fresh -BuildContent run, always passing on a sync-less rerun,
# with no exception anywhere in the PlayMode log -- a silent wrong-reference
# symptom, not a crash, which is exactly what a surviving guid mismatch
# looks like. ContentBuilder's RecreateFolder(ContentRoot) means EVERY
# content asset gets a freshly Unity-assigned guid on EVERY -BuildContent
# run, in BOTH runners independently (they build in parallel) -- so the
# blast radius for this class of bug is "the whole content tree", not an
# occasional new file. This check turns a maybe-related theory into a
# provable yes/no the next time it happens, instead of another "passed on
# rerun" shrug. See AUDIT.md's open item on this.
#
# Takes a fan-out result from $FanOutRunnerScript (the compare itself runs
# there, per runner, concurrently) and exits the run on any mismatch.
function Assert-GuidsMatch {
    param($Result)

    $mismatches = @($Result.Mismatches)
    if ($mismatches.Count -gt 0) {
        Write-Host "GUID MISMATCH after sync to $($Result.Label) ($($mismatches.Count) asset(s)) -- aborting rather than testing with a broken reference:"
        $mismatches | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" }
        exit 1
    }
}

# --- test-discovery gate ----------------------------------------------------
#
# Runs FIRST, before sync or any Unity boot, because a failure here costs
# nothing to report and everything to discover after the fact. This is the
# mandatory pre-commit script, so it is the one place a silently-uncovered
# test class has to get caught -- an area-based tools/test.ps1 slice can only
# ever be as trustworthy as the class list behind it.
#
# The duplicate-name check runs first because it is the one failure the other
# two cannot see: both classes are discovered, both are in an area, and the
# index simply keeps one of them. Nothing downstream reports anything odd --
# a run named for the loser boots the winner's platform and passes.
#
# The other two checks partition every .cs under Tests/ rather than overlapping:
# blind spots cover the files inside the eight folders (a [Test] whose class
# discovery could not see), structure covers everything outside them. Neither
# can report clean over what the other is looking at, which is the property
# that matters -- a suite quietly not running some of its own tests is what
# both exist to prevent.
#
# The structure check is what replaced the area-orphan gate when areas stopped
# being class-name regexes and became folders. It is a smaller claim and a
# stronger one: a class cannot fail to match a folder the way it could fail to
# match a regex, so the only remaining way into no-area limbo is to leave a
# file outside the area folders, and that is exactly what this refuses.
#
# No bypass flag, and none is planned. The fix is a git mv.
$discoveredIndex = Get-TestIndex

$duplicates = Get-DuplicateClassNames -Index $discoveredIndex
if ($duplicates.Count -gt 0) {
    Write-Host "DUPLICATE TEST CLASS NAME ($($duplicates.Count)) -- discovery is keyed by name, so one of these never runs under its own entry:"
    foreach ($d in $duplicates) { Write-Host "  $d" }
    Write-Host "`nRename one of the two. Every filter this repo builds -- Unity's -testFilter and dotnet's FullyQualifiedName -- is the bare class name and cannot tell them apart."
    exit 1
}

$blindSpots = Get-DiscoveryBlindSpots -Index $discoveredIndex
if ($blindSpots.Count -gt 0) {
    Write-Host "TEST DISCOVERY BLIND SPOT ($($blindSpots.Count)) -- a test file declares a class discovery never saw:"
    foreach ($b in $blindSpots) { Write-Host "  $b" }
    Write-Host "`nFix the class declaration, or widen the regex in tools/test_areas.ps1's Get-TestIndex if this is a legitimate new shape."
    exit 1
}

$violations = Get-StructuralViolations
if ($violations.Count -gt 0) {
    Write-Host "TEST FILE OUTSIDE ITS AREA ($($violations.Count)) -- a test's area is the folder it sits in, and these have none:"
    foreach ($v in $violations) { Write-Host "  $v" }
    Write-Host "`nMove the file with git mv. tools/test_areas.ps1's header says what belongs in each of the seven areas."
    exit 1
}

# --- what to run: the whole suite, or a -Changed slice ----------------------
#
# Decided here, before anything is synced or booted, so a promotion or a bad
# partition costs nothing. The class sets are the same discovery the gates
# above just used.
$sharedArea = $SharedFolder.ToLower()
$AllPlayClasses = @(Get-PlayModeFixtureClasses -Index $discoveredIndex)
$AllEditClasses = @($discoveredIndex.Keys | Where-Object {
    $discoveredIndex[$_].Platform -eq "EditMode" -and $discoveredIndex[$_].Area -ne $sharedArea
} | Sort-Object)
$EditClasses = $AllEditClasses
$PlayClasses = $AllPlayClasses
$Slice = $false

if ($ChangedPaths -and -not $Changed) {
    Write-Host "-ChangedPaths only means something with -Changed."
    exit 1
}

if ($Changed) {
    Write-Host ""
    Write-Host "-Changed: selecting the tests this change can affect (tools/test_select.ps1)"
    $fullWhy = @()
    $promotion = Get-FullRunPromotion
    if ($promotion) { $fullWhy += "safety net: $promotion" }
    if ($BuildScenes) { $fullWhy += "-BuildScenes rewrites every scene, and a scene change forces the full suite" }

    if ($ChangedPaths) {
        $changedList = @($ChangedPaths | ForEach-Object { $_ -replace '\\', '/' } | Where-Object { $_ })
        $others = @(Get-ChangedFiles | Where-Object { $changedList -notcontains $_ })
        Write-Host "Resolving the $($changedList.Count) path(s) given by -ChangedPaths, NOT the working tree: $($others.Count) other changed file(s) there are not considered."
    } else {
        $changedList = @(Get-ChangedFiles)
    }

    # Printed even when the run is promoted: the mapping is how a wrong map
    # gets noticed, and a promoted run is when nobody would look otherwise.
    $selection = Resolve-GateSelection -Paths $changedList -Index $discoveredIndex
    Write-GateSelection -Selection $selection -Index $discoveredIndex
    $fullWhy += @($selection.FullReasons)

    if ($fullWhy.Count -gt 0) {
        Write-Host ""
        Write-Host "FULL RUN, not a slice, because:"
        foreach ($w in $fullWhy) { Write-Host "  $w" }
    } else {
        $picked = @($selection.Classes)
        if ($BuildContent) {
            # The build rewrites Resources/Content, which the area map sends to
            # 'content'; the resolution above ran before that tree moved.
            $contentClasses = @(Get-AreaClasses -Index $discoveredIndex -Areas @("content"))
            Write-Host "+ area content ($($contentClasses.Count) classes): -BuildContent regenerates Resources/Content"
            $picked = @($picked + $contentClasses | Sort-Object -Unique)
        }
        if ($picked.Count -eq 0) {
            Write-Host ""
            Write-Host "Nothing to run: every changed file is ignored (docs, .meta, outside Assets/Packages/ProjectSettings/tools). No Unity was started."
            exit 0
        }
        $Slice = $true
        $EditClasses = @($AllEditClasses | Where-Object { $picked -contains $_ })
        $PlayClasses = @($AllPlayClasses | Where-Object { $picked -contains $_ })
    }
}

# One PlayMode shard per this many seconds of recorded fixture time in a
# slice: a shard costs a ~30s boot, so splitting 40s of tests three ways buys
# nothing but three boots.
$SliceSecondsPerShard = 60
$PlayShardCount = $Shards
if ($Slice) {
    $PlayShardCount = 0
    if ($PlayClasses.Count -gt 0) {
        $one = Get-PlayModeShardPlan -Index $discoveredIndex -Classes $PlayClasses -Shards 1
        $est = $one.Shards[0].Estimate
        $want = $Shards
        if ($one.Source -like "timings*") { $want = [int][Math]::Ceiling($est / $SliceSecondsPerShard) }
        $PlayShardCount = [Math]::Max(1, [Math]::Min($Shards, [Math]::Min($want, $PlayClasses.Count)))
        Write-Host ("Slice: {0} EditMode + {1} PlayMode classes. PlayMode est {2:N0} ({3}) -> {4} shard(s), one per ~{5}s, at most -Shards {6}." -f $EditClasses.Count, $PlayClasses.Count, $est, $one.Source, $PlayShardCount, $SliceSecondsPerShard, $Shards)
    } else {
        Write-Host "Slice: $($EditClasses.Count) EditMode classes, no PlayMode class -- no PlayMode copy is started."
    }
}

# --- the PlayMode shard plan ------------------------------------------------
#
# Planned here, before anything is synced or booted, so a bad partition costs
# nothing. A slice is planned exactly like the whole suite, over its own
# classes: contiguous ranges of THEIR run order, so a slice never runs a
# fixture after a predecessor the unsharded suite would not have given it
# (header of tools/playmode_shards.ps1) -- only after fewer of them.
$ShardPlan = $null
$FilterLimit = 24000
if ($PlayShardCount -gt 0 -and ($Slice -or $PlayShardCount -gt 1)) {
    $ShardPlan = Get-PlayModeShardPlan -Index $discoveredIndex -Classes $PlayClasses -Shards $PlayShardCount

    $partitionProblems = @(Test-ShardPartition -Classes $PlayClasses -Plan $ShardPlan -Index $discoveredIndex)
    if ($partitionProblems.Count -gt 0) {
        Write-Host "SHARD PARTITION IS WRONG ($($partitionProblems.Count)) -- refusing rather than running some classes twice or not at all:"
        foreach ($p in $partitionProblems) { Write-Host "  $p" }
        exit 1
    }

    # Windows caps a whole command line at 32,767 characters. Every
    # PlayMode class name joined is ~5.6K today, so one shard's filter is
    # nowhere near it; this refuses, naming the size, long before it is.
    foreach ($s in $ShardPlan.Shards) {
        if ($s.Classes.Count -eq 0) {
            Write-Host "SHARD $($s.Index) IS EMPTY -- $($PlayClasses.Count) classes cannot fill $PlayShardCount shards. Use fewer -Shards."
            exit 1
        }
        $s | Add-Member -NotePropertyName Filter -NotePropertyValue (Get-ShardFilter -Classes $s.Classes) -Force
        if ($s.Filter.Length -gt $FilterLimit) {
            Write-Host "SHARD $($s.Index)'s -testFilter is $($s.Filter.Length) chars, over this script's $FilterLimit cap (Windows' command-line limit is 32767). Use more -Shards, or move the filter to a file."
            exit 1
        }
    }
}

# The EditMode half of a slice, filtered the same way. The full run has no
# EditMode filter, exactly as before.
$EditFilter = $null
if ($Slice -and $EditClasses.Count -gt 0) {
    $EditFilter = Get-ShardFilter -Classes $EditClasses
    if ($EditFilter.Length -gt $FilterLimit) {
        Write-Host "The EditMode -testFilter is $($EditFilter.Length) chars, over this script's $FilterLimit cap. Run the full gate (drop -Changed)."
        exit 1
    }
}

$Runners = New-GateRunners -PlayShards $PlayShardCount -EditTests ($EditClasses.Count -gt 0) -EditNeeded ([bool]($BuildContent -or $BuildScenesHere))
$PlayRunners = @($Runners | Where-Object { $_.Platform -eq "PlayMode" })
# The merged PlayMode results live where the unsharded file always did.
$MergedPlayResults = if ($PlayRunners.Count -gt 0) { Join-Path $PlayRunners[0].Path "test-results-PlayMode.xml" } else { $null }

if ($ShardPlan) {
    Write-Host "PlayMode: $($PlayClasses.Count) fixture classes across $PlayShardCount shard(s), balanced by $($ShardPlan.Source):"
    foreach ($s in $ShardPlan.Shards) {
        $r = $PlayRunners[$s.Index - 1]
        Write-Host ("  shard {0}: {1,3} classes, est {2,6:N1}  -> {3}" -f $s.Index, $s.Classes.Count, $s.Estimate, $r.Path)
    }
}

if ($DryRun) {
    Write-Host ""
    $what = if ($Slice) { "SLICE" } else { "FULL" }
    Write-Host "DRY RUN ($what): stopping before the sync; no Unity started. Would run in:"
    foreach ($r in $Runners) {
        $note = if ($r.RunTests) { "" } else { " (generation only, no tests)" }
        Write-Host "  $($r.Label) -> $($r.Path)$note"
    }
    exit 0
}

# --- are the runners free? --------------------------------------------------
#
# EVERY one of them, and before the sync -- mirroring main into a copy another
# session's Unity has open is its own way to break a run, and this script
# generates content and scenes into one of them. Costs one process-table read
# each. Refuses rather than waits; see Test-RunnerFree's header for why.
foreach ($runner in $Runners) {
    if (-not (Test-RunnerFree -RunnerPath $runner.Path -Label "the $($runner.Label) runner")) { exit 1 }
}

$freshRunners = @($Runners | Where-Object { -not (Test-Path (Join-Path $_.Path "Library")) })
if ($SkipSync -and @($Runners | Where-Object { -not (Test-Path (Join-Path $_.Path "Assets")) }).Count -gt 0) {
    Write-Host "-SkipSync, but these runner copies do not exist yet -- run once without -SkipSync to create them:"
    $Runners | Where-Object { -not (Test-Path (Join-Path $_.Path "Assets")) } | ForEach-Object { Write-Host "  $($_.Path)" }
    exit 1
}

if (-not $SkipSync) {
    # Created the way tools/bot.ps1 creates its -Bot<N> copies: the mirror
    # below makes the folder, and the first Unity boot imports every asset.
    if ($freshRunners.Count -gt 0) {
        Write-Host ""
        Write-Host "No Library/ yet in $($freshRunners.Count) runner copy/copies -- THIS RUN PAYS A FULL ASSET IMPORT"
        Write-Host "in each of them before its tests start (several minutes, once). Later runs boot in ~13s:"
        $freshRunners | ForEach-Object { Write-Host "  $($_.Path)" }
        Write-Host ""
    }

    Write-Host "Syncing into $($Runners.Count) isolated test copies, concurrently..."
    $syncResults = Invoke-PerRunner -Runners $Runners -Script $SyncRunnerScript -Shared @{ Source = $SourceProject }
    $syncFailed = $false
    foreach ($res in $syncResults) {
        Write-Host ("    sync -> {0,-11} {1,5:N1}s  ({2} .meta repaired)" -f $res.Label, $res.Seconds, @($res.Repaired).Count)
        foreach ($f in @($res.Failed)) { Write-Host "    SYNC FAILED into $($res.Path): $f"; $syncFailed = $true }
    }
    if ($syncFailed) { exit 1 }
    Stamp "sync -> all $($Runners.Count) copies"

    # Verified, not assumed. A sync that silently leaves an old scene behind
    # produces dozens of NullReferenceExceptions from serialized fields that
    # "should" be wired, and the cause looks nothing like the symptom - it is
    # the same trap CLAUDE.md's gotcha #1 is about. Cheap to check, so check.
    #
    # ENUMERATED, NOT LISTED. This named four scenes by hand and there are five:
    # Talents.unity sat outside the guard, so the scene whose staleness produces
    # exactly the cascade above was the one nothing checked. The scene set
    # already has an owner -- SceneBuilder derives it from
    # ScreenRegistry.All.GroupBy(ScenePath) -- and a second copy of that list,
    # in a language that cannot see the first, is how it drifted. Reading the
    # directory cannot drift, and a sixth scene is covered the day it exists.
    $sceneDir = Join-Path $SourceProject "Assets\_Project\Scenes"
    $sceneFiles = @(Get-ChildItem -Path $sceneDir -Filter *.unity -File -ErrorAction SilentlyContinue)

    # A guard that checks nothing passes everything. An empty glob means the
    # path is wrong, not that the project has no scenes.
    if ($sceneFiles.Count -lt 1) {
        Write-Host "SCENE GUARD IS VACUOUS: no .unity files under $sceneDir - it would pass by finding nothing to check."
        exit 1
    }

    foreach ($runner in $Runners) {
        foreach ($src in $sceneFiles) {
            $rel = Join-Path "Assets\_Project\Scenes" $src.Name
            $dst = Join-Path $runner.Path $rel
            if (-not (Test-Path $dst)) { Write-Host "SYNC FAILED: $dst missing"; exit 1 }
            if ((Get-Item $dst).Length -ne $src.Length) {
                Write-Host "SYNC FAILED: $rel in $($runner.Path) is $((Get-Item $dst).Length) bytes, source is $($src.Length). Stale scene - aborting rather than testing the wrong build."
                exit 1
            }
        }
    }
}

# Content and scene generation are Editor-only methods and have to run BEFORE
# the test processes, in whichever copy will read the result.
#
# TWO BATCHMODE BOOTS, IN THE PRIMARY COPY ONLY. It used to be one boot per
# method per copy -- ten cold Unity starts, about three minutes before a test
# ran, which is long enough that the author was killing the run instead of
# waiting for it. A harness nobody is willing to wait for is not a harness.
#
# Both economies are safe for reasons worth stating, because both look
# dangerous:
#
#  - ONE COPY, not both. The secondary's generated output never survived
#    anyway: the sync-back below re-mirrors main over every secondary copy so
#    that all copies agree on asset GUIDs, and that mirror lands AFTER
#    generation. The secondary was building assets it was about to have
#    overwritten.
#
#  - FOUR METHODS IN ONE PROCESS. Nothing required separate ones; each builder
#    is a static method leaving results on disk. GenerationRun.RunAll refreshes
#    the AssetDatabase between them, which a process boundary used to do
#    implicitly.
#
# TmpBootstrap is the exception and stays its own boot: TMP_Settings.instance
# is a cached Resources.Load that stays null for the rest of the process that
# imported it, so a scene built in the same run dies inside
# TMP_FontAsset.CreateFontAsset with a bare NullReferenceException. Its own
# header records that; do not fold it in.
if ($BuildContent -or $BuildScenesHere) {
    $primaryRunner = $Runners[0]

    $steps = @()
    if ($BuildContent)    { $steps += "content" }
    if ($BuildScenesHere) { $steps += "scenes" }

    # Each phase is a name, the method to run, the extra args it needs, and the
    # sentinels its log must contain. Gating on the SENTINEL rather than the
    # exit code is deliberate and long-standing: Unity exits non-zero on an
    # untidy shutdown even when the method ran fine, which failed a perfectly
    # good ProceduralSpriteBaker run the first time exit codes were trusted.
    # Every builder logs "BUILD-COMPLETE: <name>" as its last act, so its
    # ABSENCE is the honest signal that the method did not finish.
    $expected = @("ProceduralSpriteBaker", "PipelineBuilder")
    if ($BuildContent)    { $expected += "ContentBuilder" }
    if ($BuildScenesHere) { $expected += "SceneBuilder" }
    $expected += "GenerationRun"

    # TmpBootstrap is checked here rather than run as its own Unity boot.
    #
    # It reads as a generator and is not one: it creates nothing, and its whole
    # body is "return if TMP_Settings.instance is already there, return if these
    # two files are on disk, otherwise throw with the fix". Both files have been
    # committed since 1bd5999, so every run of it since has been a ten-second
    # Unity start to confirm two files exist. PowerShell can confirm that in a
    # millisecond, and the guidance it printed is reproduced below verbatim.
    #
    # The safety net does not depend on this check: SceneBuilder.BuildAllScenes
    # opens by testing TMP_Settings.instance itself and refuses with the same
    # advice, so a genuinely missing bootstrap still fails loudly and early.
    $tmpFiles = @(
        "Assets\TextMesh Pro\Resources\TMP Settings.asset",
        "Assets\TextMesh Pro\Shaders\TMP_SDF.shader"
    )
    $missing = $tmpFiles | Where-Object { -not (Test-Path (Join-Path $primaryRunner.Path $_)) }
    if ($missing) {
        Write-Host "TextMeshPro's essential resources are missing, so no font asset can be built:"
        $missing | ForEach-Object { Write-Host "  $_" }
        Write-Host "  Fix by running: python tools/extract_tmp_essentials.py"
        exit 1
    }

    $phases = @(
        @{ Name = "GenerationRun"; Method = "GenerationRun.RunAll"; Extra = @("-ppSteps", ($steps -join ",")); Sentinels = $expected }
    )

    foreach ($phase in $phases) {
        Write-Host "Running $($phase.Method) in $($primaryRunner.Path)..."
        $log = Join-Path $primaryRunner.Path "gen.log"
        if (Test-Path $log) { Remove-Item $log -Force }

        # NOT $args -- that is an automatic variable, and assigning to it at
        # script scope quietly clobbers the script's own argument array.
        $unityArgs = @(
            "-batchmode", "-nographics", "-silent-crashes",
            "-projectPath", "`"$($primaryRunner.Path)`"",
            "-executeMethod", $phase.Method,
            "-logFile", "`"$log`""
        ) + $phase.Extra + @("-quit")

        $proc = Start-UnityQuiet -FilePath $UnityExe -ArgumentList $unityArgs
        $proc | Wait-Process -Timeout 900
        Stamp "unity boot: $($phase.Method)"

        # Every sentinel, not just the last one. Sharing a process means a
        # generator that threw halfway leaves the EARLIER generators' sentinels
        # in the log, so checking only for the final one would call a partial
        # run complete -- the same blind spot that once left stale scenes on
        # disk and a green suite testing them.
        $body = if (Test-Path $log) { Get-Content $log } else { @() }
        foreach ($sentinel in $phase.Sentinels) {
            if (-not ($body | Select-String -Pattern "BUILD-COMPLETE: $sentinel" -Quiet)) {
                Write-Host "$($phase.Method) did not reach BUILD-COMPLETE: $sentinel. Tail of its log:"
                $body | Select-Object -Last 25 | ForEach-Object { Write-Host "  $_" }
                exit 1
            }
        }

        # Widened to catch a thrown generator as well as a compile error.
        # "[SceneBuilder] FAILED" is what an audit refusal actually prints, and
        # it was sailing straight through.
        $pattern = "error CS|\[ContentBuilder\]|\[SceneBuilder\] FAILED|threw exception"
        $errors = $body | Select-String -Pattern $pattern | Select-Object -Unique -First 10
        if ($errors) {
            Write-Host "$($phase.Method) FAILED in $($primaryRunner.Path):"
            $errors | ForEach-Object { Write-Host "  $_" }
            exit 1
        }
    }

    # SYNC GENERATED ASSETS BACK TO MAIN, IMMEDIATELY.
    #
    # This is CLAUDE.md gotcha #1 and it has to be automatic. Content and
    # scenes are generated in the isolated copies, so main does not have them
    # until they are copied back -- and the very next run of this script mirrors
    # main OVER the copies, silently reverting everything that was just built.
    # Doing it here rather than by hand is the difference between "the suite is
    # green" and "the suite tested a stale scene".
    Stamp "generation done"
    Write-Host "Syncing generated content and scenes back to main..."
    $primary = $Runners[0].Path

    # THE STAMP GOES LAST, and it is excluded from the mirror above to make
    # that true. robocopy /MIR is not atomic: a reader looking at main halfway
    # through sees some of the new assets and some of the old. content_stamp.json
    # is what ContentFreshnessTests reads to decide the tree is trustworthy, so
    # if it arrived with the rest, a torn sync would be indistinguishable from a
    # finished one. Copied after every asset, its presence-with-a-matching-hash
    # means the whole sync completed. ContentBuilder writes it last inside the
    # runner for the same reason, one layer down.
    robocopy "$primary\Assets\_Project\Resources\Content" "$SourceProject\Assets\_Project\Resources\Content" /MIR /NFL /NDL /NJH /NJS /NP /XF content_stamp.json content_stamp.json.meta | Out-Null
    robocopy "$primary\Assets\_Project\Resources\Content" "$SourceProject\Assets\_Project\Resources\Content" content_stamp.json content_stamp.json.meta /NFL /NDL /NJH /NJS /NP | Out-Null

    # THE ONE COPY THAT IS GATED, and everything either side of it is not.
    #
    # Content, Rendering, Materials, Art and the .meta sweeps are all stable
    # across a rebuild -- run the suite twice with no source change and none of
    # them moves. Scenes are the exception: every fileID is reassigned, so this
    # single line is the whole of why a suite run would otherwise dirty the
    # tree. It runs when the intent is to commit the scenes, which is what
    # -BuildScenes has always meant.
    if ($SyncScenesToMain) {
        robocopy "$primary\Assets\_Project\Scenes" "$SourceProject\Assets\_Project\Scenes" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    }

    # Rendering/ and Resources/Materials/ are GENERATED by PipelineBuilder and
    # referenced by the built scenes by GUID, so a scene that comes back without
    # them points at nothing.
    #
    # Materials/ is on this list because it was NOT, and the consequence was
    # invisible: the generic "*.meta" sweep below copied UIHitFlash.mat.meta back
    # while nothing copied the .mat, so main held a GUID for an asset that did
    # not exist there. The hit flash then ran against a null material, which
    # falls back to the default UI shader -- whose white tint MULTIPLIES the
    # sprite and is therefore the identity. It worked in both runners and was
    # invisible in main, which is exactly the shape of bug this list exists to
    # prevent.
    robocopy "$primary\Assets\_Project\Rendering" "$SourceProject\Assets\_Project\Rendering" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    robocopy "$primary\Assets\_Project\Resources\Materials" "$SourceProject\Assets\_Project\Resources\Materials" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null

    # EditorBuildSettings names every scene the game can load. A scene built in
    # the runner and synced back is unreachable without it.
    robocopy "$primary\ProjectSettings" "$SourceProject\ProjectSettings" EditorBuildSettings.asset /NFL /NDL /NJH /NJS /NP | Out-Null
    # Art too: LoadSprite silently flips a texture's importer settings and
    # writes a fresh .meta for anything newly referenced (gotcha #3).
    #
    # /E, NOT /MIR, AND THAT DISTINCTION HAS ALREADY DESTROYED WORK.
    #
    # Every other folder above is GENERATED, so mirroring is right for them: an
    # asset the builder stopped emitting should disappear from main too. Art is
    # the one folder here that is HAND-AUTHORED, and the runner only ever ADDS
    # to it (importer settings, fresh .meta). It never legitimately deletes.
    #
    # With /MIR the deletion half was live anyway, against a snapshot of main
    # taken at the START of the run. So any art added to main WHILE a build was
    # running -- a download finishing, a file dropped into Art/Items -- was
    # absent from the runner and got mirrored out of existence at the end.
    # robocopy deletes permanently; it does not use the Recycle Bin. Fifteen
    # generated sheets went that way on 2026-08-18 and were only recoverable
    # because they still existed in the tool that made them.
    #
    # /E copies new and updated files and deletes nothing, which is the whole
    # of what this line was ever for.
    robocopy "$primary\Assets\_Project\Art" "$SourceProject\Assets\_Project\Art" /E /NFL /NDL /NJH /NJS /NP | Out-Null
    # New scripts' .meta files, so a GUID cannot regenerate later and orphan
    # every asset referencing it (gotcha #2). Only files main does not have.
    robocopy "$primary\Assets\_Project\Scripts" "$SourceProject\Assets\_Project\Scripts" *.meta /S /XC /XN /XO /NFL /NDL /NJH /NJS /NP | Out-Null
    robocopy "$primary\Assets\_Project\ContentData" "$SourceProject\Assets\_Project\ContentData" *.meta /S /XC /XN /XO /NFL /NDL /NJH /NJS /NP | Out-Null
    # ...and Resources', for the same reason. Scripts and ContentData were the
    # only two folders listed here, which quietly excluded every hand-authored
    # file under Resources -- audio_levels.json, music_layers.json, and any
    # future runtime-loaded table. Resources\Content is already fully mirrored
    # above; this covers everything beside it.
    robocopy "$primary\Assets\_Project\Resources" "$SourceProject\Assets\_Project\Resources" *.meta /S /XC /XN /XO /NFL /NDL /NJH /NJS /NP | Out-Null

    # Re-mirror the WHOLE Assets tree into every secondary copy, not just the
    # generated folders.
    #
    # Scenes reference scripts by GUID, and a GUID is assigned by whichever
    # Unity imports a .cs file first. Two copies importing the same NEW script
    # independently assign it two DIFFERENT GUIDs -- so a scene built in copy 1
    # references a guid that means nothing in copy 2, and the component comes
    # back as a missing script. The symptom is a controller that is plainly
    # in the scene and that FindObjectsByType cannot see, which looks like
    # anything but a GUID problem.
    #
    # Copying the .meta files along with everything else is what makes all
    # three copies agree on identity. CLAUDE.md gotcha #2, reached by a route
    # it does not mention. Followed by the same hash-based repair pass
    # the pre-test sync uses -- this robocopy hits the exact same same-size/
    # different-GUID blind spot Repair-Metas exists for, just for freshly
    # GENERATED content assets instead of freshly imported scripts.
    #
    # EVERY PLAYMODE SHARD COPY, CONCURRENTLY. Sharding made "the secondary"
    # plural: a shard copy skipped here would test last run's content and
    # scenes while its siblings tested this run's (gotcha #1, one shard wide).
    # Each runner's mirror, scene copy, meta repair and GUID compare run in
    # its own runspace -- $FanOutRunnerScript in tools/runner_sync.ps1. The
    # scenes come STRAIGHT FROM THE PRIMARY, because the mirror just
    # overwrote each copy's scenes with main's: PlayMode used to receive the
    # built scenes THROUGH main, which only worked while the sync-back was
    # unconditional. Skipping it would hand PlayMode the last COMMITTED
    # scenes while EditMode audited freshly built ones -- the platforms
    # testing different builds, silently.
    Stamp "sync generated assets back to main"
    $fanOut = Invoke-PerRunner -Runners @($Runners | Select-Object -Skip 1) -Script $FanOutRunnerScript -Shared @{
        Source = $SourceProject; Primary = $primary; CopyScenes = [bool]$BuildScenesHere
    }
    foreach ($res in $fanOut) {
        Write-Host ("    re-mirror -> {0,-11} {1,5:N1}s  ({2} .meta repaired)" -f $res.Label, $res.Seconds, @($res.Repaired).Count)
        if (@($res.Failed).Count -gt 0) {
            Write-Host "SYNC FAILED into $($res.Path): $(@($res.Failed) -join ', ')"
            exit 1
        }
        Assert-GuidsMatch -Result $res
    }
    Stamp "re-mirror + Repair-Metas + Assert-GuidsMatch -> $(@($fanOut).Count) copies"

    # The scenes each copy will test are the ones just built, byte for byte:
    # proved, not inferred from robocopy's exit code. Checked against the
    # primary (where they were built) and, since -BuildScenes also synced them
    # back, against main.
    if ($BuildScenesHere) {
        $builtScenes = Join-Path $primary "Assets\_Project\Scenes"
        $sceneDiffs = @()
        foreach ($other in @(@{ Label = "main"; Path = $SourceProject }) + @($Runners | Select-Object -Skip 1)) {
            $d = [PP.RunnerSync.MetaSync]::DiffDirs($builtScenes, (Join-Path $other.Path "Assets\_Project\Scenes"), "*")
            foreach ($x in $d) { $sceneDiffs += "$($other.Label): $x" }
        }
        if ($sceneDiffs.Count -gt 0) {
            Write-Host "BUILT SCENES DID NOT FAN OUT ($($sceneDiffs.Count)) -- a copy would test scenes other than the ones just built:"
            $sceneDiffs | ForEach-Object { Write-Host "  $_" }
            exit 1
        }
        Stamp "scenes identical in main and all $(@($Runners).Count - 1) other copies"
    }
}

$TestRunners = @($Runners | Where-Object { $_.RunTests })
$runningWhat = @($TestRunners | ForEach-Object { $_.Label }) -join ", "
$sliceTag = if ($Slice) { "SLICE" } else { "FULL SUITE" }
Write-Host "`nRunning the $sliceTag concurrently in: $runningWhat"
$script:LastStamp = $Watch.Elapsed

# Stale outputs from an earlier run must not be read as this run's: the
# merged file, every runner's own results file, and each PlayMode copy's
# profile CSV (the next balance is read from those).
$staleOutputs = @($MergedPlayResults | Where-Object { $_ })
$staleOutputs += @($Runners | ForEach-Object { Join-Path $_.Path $_.Results })
$staleOutputs += @($PlayRunners | ForEach-Object { Join-Path $_.Path "test-profile-PlayMode.csv" })
foreach ($stale in $staleOutputs) {
    if (Test-Path $stale) { Remove-Item $stale -Force }
}

$procs = @{}
$launchedAt = @{}
foreach ($runner in $TestRunners) {
    $resultsPath = Join-Path $runner.Path $runner.Results
    $runLogPath = Join-Path $runner.Path "test-run-$($runner.Platform).log"

    $unityArgs = @(
        "-batchmode", "-nographics", "-silent-crashes",
        "-projectPath", "`"$($runner.Path)`"",
        "-runTests", "-testPlatform", $runner.Platform
    )
    # A shard runs only its own fixtures, filtered the way tools/test.ps1
    # filters a slice. A full run at -Shards 1 has no filter, exactly as
    # before; a -Changed slice filters EditMode too.
    if ($ShardPlan -and $runner.Shard -gt 0) {
        $unityArgs += @("-testFilter", "`"$($ShardPlan.Shards[$runner.Shard - 1].Filter)`"")
    }
    if ($EditFilter -and $runner.Platform -eq "EditMode") {
        $unityArgs += @("-testFilter", "`"$EditFilter`"")
    }
    $unityArgs += @(
        "-testResults", "`"$resultsPath`"",
        "-logFile", "`"$runLogPath`"",
        "-buildTarget", "StandaloneWindows64"
    )

    $procs[$runner.Label] = Start-UnityQuiet -FilePath $UnityExe -ArgumentList $unityArgs
    $launchedAt[$runner.Label] = $Watch.Elapsed
}

$TestTimeoutSeconds = 1800
$timedOut = $false

# WAIT-PROCESS RETURNING IS NOT THE SAME AS UNITY HAVING EXITED. On timeout it
# writes an error and carries on, leaving batchmode Unity alive and holding an
# exclusive lock on that runner copy's Library. The next run of this script then
# cannot use the copy either, so one hung run poisons every run after it, and the
# symptom arrives one run later looking nothing like the hang that caused it.
#
# POLLED rather than Wait-Process, so each runner's own wall time and the
# machine's RAM under N concurrent Unitys are measured on the way: the summed
# working set of the processes this script launched (their import workers
# are not counted), sampled every 2s, and each one's OS-tracked peak.
$exitedAt = @{}
$peakWs = @{}
$peakSum = 0L
$deadline = $Watch.Elapsed.TotalSeconds + $TestTimeoutSeconds
while ($true) {
    $sum = 0L
    $alive = 0
    foreach ($label in @($procs.Keys)) {
        $p = $procs[$label]
        if (-not $p) { continue }
        try { $p.Refresh() } catch { }
        if ($p.HasExited) {
            if (-not $exitedAt.ContainsKey($label)) { $exitedAt[$label] = $Watch.Elapsed }
            continue
        }
        $alive++
        try {
            $sum += $p.WorkingSet64
            if (-not $peakWs.ContainsKey($label) -or $p.PeakWorkingSet64 -gt $peakWs[$label]) { $peakWs[$label] = $p.PeakWorkingSet64 }
        } catch { }
    }
    if ($sum -gt $peakSum) { $peakSum = $sum }
    if ($alive -eq 0 -or $Watch.Elapsed.TotalSeconds -ge $deadline) { break }
    Start-Sleep -Milliseconds 2000
}

foreach ($runner in $Runners) {
    $p = $procs[$runner.Label]
    if (-not $p) { continue }
    $p.Refresh()
    if ($p.HasExited) { continue }

    # THE EXACT PID WE LAUNCHED, AND ONLY WHILE IT STILL SAYS SO. Two sessions
    # share this machine and one of them may have an Editor open on the real
    # project, so a PID recycled onto somebody elses Unity must not be killed.
    # The command line still carries the -projectPath this script passed; that is
    # what is matched on, not the process name.
    $cim = Get-CimInstance Win32_Process -Filter "ProcessId = $($p.Id)" -ErrorAction SilentlyContinue
    if (-not $cim -or $cim.CommandLine -notlike "*$($runner.Path)*") {
        Write-Host "$($runner.Label) did not finish within $TestTimeoutSeconds s, and PID $($p.Id) no longer looks like the Unity this script started. Leaving it alone."
        $timedOut = $true
        continue
    }

    Write-Host "$($runner.Label) did not finish within $TestTimeoutSeconds s. Killing PID $($p.Id) so it stops holding $($runner.Path) for the next run."
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    $timedOut = $true
}
Stamp "tests finished"

$allPassed = $true

# A killed Unity may still have left a partial results file behind, so the
# timeout is failed on its own account rather than through the checks below.
if ($timedOut) { $allPassed = $false }
$shardResultPaths = @()
$allResultPaths = @()
foreach ($runner in $TestRunners) {
    $resultsPath = Join-Path $runner.Path $runner.Results
    $logPath = Join-Path $runner.Path "test-run-$($runner.Platform).log"
    $wall = ""
    if ($exitedAt.ContainsKey($runner.Label)) {
        $wall = "  wall {0:N1}s" -f ($exitedAt[$runner.Label] - $launchedAt[$runner.Label]).TotalSeconds
    }
    $mem = ""
    if ($peakWs.ContainsKey($runner.Label)) { $mem = "  peak {0:N0} MB" -f ($peakWs[$runner.Label] / 1MB) }

    if (-not (Test-Path $resultsPath)) {
        Write-Host "No results for $($runner.Label) ($resultsPath). Tail of $logPath :"
        Get-Content $logPath -Tail 40 -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
        $allPassed = $false
        continue
    }
    $allResultPaths += $resultsPath
    if ($runner.Platform -eq "PlayMode" -and $ShardPlan) { $shardResultPaths += $resultsPath }

    [xml]$results = Get-Content $resultsPath
    $root = $results.'test-run'
    Write-Host "$($runner.Label) -- Total: $($root.total)  Passed: $($root.passed)  Failed: $($root.failed)  Skipped: $($root.skipped)  Duration: $($root.duration)s$wall$mem"
    if ($PlayShardCount -gt 1 -and $runner.Platform -eq "PlayMode") { Write-Host "    $resultsPath" }

    foreach ($f in $results.SelectNodes("//test-case[@result='Failed']")) {
        Write-Host "`nFAILED ($($runner.Label)): $($f.fullname)"
        if ($f.failure -and $f.failure.message) { Write-Host $f.failure.message.InnerText }
    }

    if ([int]$root.failed -ne 0) { $allPassed = $false }
}

# ONE PlayMode verdict out of N shards: every shard must have reported (a
# missing one already failed the run above), the shards together must have
# run every class exactly once, and the merged file is what a reader of
# test-results-PlayMode.xml gets -- the whole suite (or slice), where it
# always was. A one-shard slice is filtered too, so it is checked the same way.
if ($ShardPlan) {
    if ($shardResultPaths.Count -eq $PlayRunners.Count) {
        $coverage = @(Test-ShardCoverage -Paths $shardResultPaths -Classes $PlayClasses)
        if ($coverage.Count -gt 0) {
            Write-Host "`nSHARD COVERAGE IS WRONG ($($coverage.Count)) -- the shards did not run the selected classes exactly once:"
            $coverage | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" }
            $allPassed = $false
        }
        if ($PlayShardCount -gt 1) {
            $sum = Merge-ShardResults -Paths $shardResultPaths -OutPath $MergedPlayResults
            Write-Host ("PlayMode ({0} shards combined) -- Total: {1}  Passed: {2}  Failed: {3}  Skipped: {4}" -f $PlayRunners.Count, $sum.total, $sum.passed, $sum.failed, $sum.skipped)
            Write-Host "    merged: $MergedPlayResults"
        }
    } else {
        Write-Host "PlayMode: only $($shardResultPaths.Count) of $($PlayRunners.Count) shards reported -- no merged results written."
    }
}
# A slice's EditMode filter, checked by class: every selected class must
# show up as a fixture. Only the class check -- the EditMode suite has
# TestCaseSource cases that share a display name, which the per-case
# duplicate check in Test-ShardCoverage was written for PlayMode and misreads.
if ($EditFilter) {
    $editRes = @($TestRunners | Where-Object { $_.Platform -eq "EditMode" } | ForEach-Object { Join-Path $_.Path $_.Results } | Where-Object { Test-Path $_ })
    if ($editRes.Count -eq 1) {
        $missing = @(Test-ShardCoverage -Paths $editRes -Classes $EditClasses | Where-Object { $_ -like "class *" })
        if ($missing.Count -gt 0) {
            Write-Host "`nEDITMODE FILTER MISSED ($($missing.Count)) -- selected classes the run never reached:"
            $missing | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" }
            $allPassed = $false
        }
    }
}
if ($peakSum -gt 0) {
    Write-Host ("Peak summed working set of the {0} test Unitys: {1:N0} MB (import workers not counted)" -f $procs.Count, ($peakSum / 1MB))
}

# The next run's balance, from this run's PlayMode profile CSVs -- sharded or
# not, but ONLY from a green run. A failing fixture's time is not its time:
# the first sharded run to go red spent 10s per case waiting out a
# "never finished" deadline, and balancing on that put 18 fixtures in one
# shard and 95 in another.
#
# A slice MERGES into the file rather than replacing it: it timed only its own
# fixtures, and the next full run still has to balance all of them.
if ($allPassed -and $PlayRunners.Count -gt 0) {
    $timed = Save-ShardTimings -CsvPaths @($PlayRunners | ForEach-Object { Join-Path $_.Path "test-profile-PlayMode.csv" }) -Merge:$Slice
    if ($timed -gt 0) { Write-Host "Fixture timings for the next balance: $timed fixtures -> $ShardTimingsFile" }
} elseif (-not $allPassed) {
    Write-Host "Fixture timings NOT updated (run was not green); the next balance uses $ShardTimingsFile as it was."
}

# The safety net's anchor and the map-gap evidence -- FULL runs only. A green
# slice proves nothing about what it skipped, so it never moves the anchor.
if (-not $Slice) {
    if ($allPassed) {
        Save-LastFullGreen
    } else {
        $failing = Get-FailingClasses -ResultPaths $allResultPaths -Index $discoveredIndex
        Write-MapGapReport -FailingClasses $failing -Index $discoveredIndex
    }
}

$totalWall = "{0:N0}s" -f $Watch.Elapsed.TotalSeconds
if ($allPassed) {
    if ($Slice) {
        Write-Host "`nAll tests passed -- a SLICE: $($EditClasses.Count) EditMode + $($PlayClasses.Count) PlayMode classes, $totalWall wall. The full run happens by itself after $LastGreenMaxCommits commits or $($LastGreenMaxHours)h."
    } else {
        Write-Host "`nAll tests passed (full suite, $totalWall wall)."
    }
    exit 0
}
Write-Host "`nSome tests failed."
exit 1

} finally {
    Stop-FocusGuard
}
