param(
    [switch]$SkipSync,
    [switch]$BuildContent,
    [switch]$BuildScenes
)

# Runs EditMode and PlayMode CONCURRENTLY against two separate isolated copies
# of the project.
#
# run_tests.ps1 runs them one after the other, and each pays a full Unity
# startup. They cannot share a project directory — Unity takes an exclusive
# lock on Library/ — so parallelism needs a second copy rather than a second
# process. Wall clock drops to roughly the slower of the two platforms.
#
# Both copies get a divergent productName so Application.persistentDataPath
# (and therefore save_slot_*.json) can never collide with the real project OR
# with each other.

# DERIVED, never hardcoded. Both of these were literal v1 paths, so the harness
# would happily drive the wrong project -- and the editor version moved the
# moment ProjectVersion.txt did.
$SourceProject = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "unity_path.ps1")
$UnityExe = Get-UnityExe

# Shares $Areas/Get-TestClasses/Get-AreaOrphans/Get-DiscoveryBlindSpots with
# tools/test.ps1, so the gate immediately below checks against the exact same
# definitions a slice would use.
. (Join-Path $PSScriptRoot "test_areas.ps1")

$ProjectLeaf = Split-Path $SourceProject -Leaf
$ProjectParent = Split-Path $SourceProject -Parent
$ProductLeaf = ($ProjectLeaf -replace "[^A-Za-z0-9]", "")

$Runners = @(
    @{ Platform = "EditMode"; Path = "$ProjectParent\$ProjectLeaf-TestRunner";  Product = "${ProductLeaf}TestRunner" }
    @{ Platform = "PlayMode"; Path = "$ProjectParent\$ProjectLeaf-TestRunner2"; Product = "${ProductLeaf}TestRunner2" }
)

function Repair-Metas {
    param($DestAssets)

    # /MIR skips a file whose size AND timestamp match the destination's, and
    # two .meta files for the same asset are the same size to the byte while
    # holding DIFFERENT GUIDs. When two isolated copies independently import
    # or generate the same new asset around the same moment, each invents its
    # own GUID, and from then on the mirror cannot tell the copies apart and
    # never corrects the odd one out.
    #
    # Copying the right bytes over is NOT enough on its own: robocopy
    # preserves the source timestamp, and Unity, seeing a .meta no newer than
    # the one its Library was built from, keeps the stale GUID mapping. So the
    # repaired file is stamped with the current time to force a reimport, and
    # its asset is stamped with it so the asset itself is re-bound.
    #
    # Only genuinely differing files are touched — stamping every .meta each
    # run would reimport the entire project every time.
    #
    # The symptom this prevents: a component plainly present in the scene file
    # and invisible to FindObjectsByType, because the scene was built against
    # the other runner's GUID — or, for generated content, a talent whose
    # prerequisite silently resolves to null because its OWN runner's copy of
    # the prerequisite kept a stale GUID the referencing asset no longer uses.
    # Cost an afternoon, twice; cost a 3-test PlayMode failure the third time,
    # the day the talent tree grew from 30 nodes to 150 and the odds of two
    # same-size .meta files landing on the same timestamp stopped being rare.
    Get-ChildItem -Path "$SourceProject\Assets" -Filter *.meta -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring("$SourceProject\Assets".Length + 1)
        $destination = Join-Path $DestAssets $relative
        if (-not (Test-Path $destination)) { return }
        if ((Get-FileHash $_.FullName).Hash -eq (Get-FileHash $destination).Hash) { return }

        Copy-Item $_.FullName $destination -Force
        $now = Get-Date
        (Get-Item $destination).LastWriteTime = $now
        $asset = $destination -replace '\.meta$', ''
        if (Test-Path $asset -PathType Leaf) { (Get-Item $asset).LastWriteTime = $now }
    }
}

function Get-Guid {
    param($MetaPath)
    $line = Select-String -Path $MetaPath -Pattern "^guid:\s*([0-9a-f]{32})" -List
    if (-not $line) { return $null }
    return $line.Matches[0].Groups[1].Value
}

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
function Assert-GuidsMatch {
    param($Runner)

    $mismatches = @()
    Get-ChildItem -Path "$SourceProject\Assets" -Filter *.meta -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring("$SourceProject\Assets".Length + 1)
        $destination = Join-Path "$($Runner.Path)\Assets" $relative
        if (-not (Test-Path $destination)) { return }

        $srcGuid = Get-Guid $_.FullName
        $dstGuid = Get-Guid $destination
        if ($srcGuid -and $dstGuid -and $srcGuid -ne $dstGuid) {
            $mismatches += "$relative : main=$srcGuid $($Runner.Platform)=$dstGuid"
        }
    }

    if ($mismatches.Count -gt 0) {
        Write-Host "GUID MISMATCH after sync to $($Runner.Platform) ($($mismatches.Count) asset(s)) -- aborting rather than testing with a broken reference:"
        $mismatches | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" }
        exit 1
    }
}

function Sync-Runner {
    param($Runner)

    robocopy "$SourceProject\Assets" "$($Runner.Path)\Assets" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    Repair-Metas -DestAssets "$($Runner.Path)\Assets"

    robocopy "$SourceProject\Packages" "$($Runner.Path)\Packages" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    robocopy "$SourceProject\ProjectSettings" "$($Runner.Path)\ProjectSettings" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null

    $settingsPath = Join-Path $Runner.Path "ProjectSettings\ProjectSettings.asset"
    (Get-Content $settingsPath -Raw) -replace "productName: .*", "productName: $($Runner.Product)" |
        Set-Content $settingsPath -Encoding utf8
}

# --- test-discovery gate ----------------------------------------------------
#
# Runs FIRST, before sync or any Unity boot, because a failure here costs
# nothing to report and everything to discover after the fact. This is the
# mandatory pre-commit script, so it is the one place a silently-uncovered
# test class has to get caught -- an area-based tools/test.ps1 slice can only
# ever be as trustworthy as the class list behind it.
#
# Blind spots are checked BEFORE orphans, and that ordering matters: a class
# invisible to Get-TestClasses is also invisible to the orphan check, so a
# discovery gap would otherwise report as "no orphans" -- a clean bill of
# health for a suite that is quietly not running some of its own tests.
#
# No bypass flag, and none is planned. The fix is always a one-line pattern
# edit in tools/test_areas.ps1, and a bypass would just institutionalize the
# exact gap this exists to close.
$discoveredClasses = Get-TestClasses

$blindSpots = Get-DiscoveryBlindSpots -Classes $discoveredClasses
if ($blindSpots.Count -gt 0) {
    Write-Host "TEST DISCOVERY BLIND SPOT ($($blindSpots.Count) file(s)) -- a [Test]/[UnityTest] exists here but its class was never discovered:"
    foreach ($b in $blindSpots) { Write-Host "  $b" }
    Write-Host "`nFix the class declaration, or widen the regex in tools/test_areas.ps1's Get-TestClasses if this is a legitimate new shape."
    exit 1
}

$orphans = Get-AreaOrphans -ClassNames $discoveredClasses.Keys
if ($orphans.Count -gt 0) {
    Write-Host "TEST CLASS WITH NO AREA ($($orphans.Count)) -- invisible to every area-based tools/test.ps1 slice:"
    foreach ($o in $orphans) { Write-Host "  $o" }
    Write-Host "`nExtend a pattern in tools/test_areas.ps1's `$Areas so it matches this class."
    exit 1
}

if (-not $SkipSync) {
    Write-Host "Syncing into $($Runners.Count) isolated test copies..."
    foreach ($runner in $Runners) { Sync-Runner -Runner $runner }

    # Verified, not assumed. A sync that silently leaves an old scene behind
    # produces dozens of NullReferenceExceptions from serialized fields that
    # "should" be wired, and the cause looks nothing like the symptom - it is
    # the same trap CLAUDE.md's gotcha #1 is about. Cheap to check, so check.
    foreach ($runner in $Runners) {
        # v2's scene set. Gameplay.unity was v1's single monolithic scene and
        # does not exist here -- the screens are separate scenes now.
        foreach ($rel in @("Assets\_Project\Scenes\MainMenu.unity", "Assets\_Project\Scenes\Hub.unity", "Assets\_Project\Scenes\Fight.unity", "Assets\_Project\Scenes\Map.unity")) {
            $src = Get-Item (Join-Path $SourceProject $rel)
            $dst = Join-Path $runner.Path $rel
            if (-not (Test-Path $dst)) { Write-Host "SYNC FAILED: $dst missing"; exit 1 }
            if ((Get-Item $dst).Length -ne $src.Length) {
                Write-Host "SYNC FAILED: $rel in $($runner.Path) is $((Get-Item $dst).Length) bytes, source is $($src.Length). Stale scene - aborting rather than testing the wrong build."
                exit 1
            }
        }
    }
}

# Content and scene generation are Editor-only menu methods and have to run
# BEFORE the test processes, in whichever copy will read the result. Both
# copies need it, so both are built - in parallel with each other.
if ($BuildContent -or $BuildScenes) {
    $methods = @()
    # Order matters. The sprite baker and the pipeline write ASSETS the scene
    # build then references by GUID, and TMP's bootstrap has to have run before
    # any label is emitted.
    $methods += "ProceduralSpriteBaker.BakeAll"
    $methods += "PipelineBuilder.BuildRenderPipeline"
    $methods += "TmpBootstrap.Bootstrap"
    if ($BuildContent) { $methods += "ContentBuilder.BuildDefaultContent" }
    if ($BuildScenes)  { $methods += "SceneBuilder.BuildAllScenes" }

    foreach ($method in $methods) {
        Write-Host "Running $method in both copies..."
        $jobs = foreach ($runner in $Runners) {
            Start-Process -FilePath $UnityExe -ArgumentList @(
                "-batchmode", "-nographics", "-silent-crashes",
                "-projectPath", "`"$($runner.Path)`"",
                "-executeMethod", $method,
                "-logFile", "`"$(Join-Path $runner.Path 'gen.log')`"",
                "-quit"
            ) -PassThru -NoNewWindow
        }

        $jobs | Wait-Process -Timeout 900

        # WHAT COUNTS AS SUCCESS IS THE SENTINEL, not the exit code.
        #
        # Unity exits non-zero on an untidy shutdown even when the method ran
        # fine -- checking the code alone failed a perfectly good
        # ProceduralSpriteBaker run the first time it was tried. Every builder
        # logs "BUILD-COMPLETE: <name>" as its last act, so its ABSENCE is the
        # honest signal that the method did not finish.
        #
        # This check did not exist at all before, and its absence was expensive:
        # the pattern scan below looked only for "error CS" and ContentBuilder
        # lines, so a -executeMethod that THREW -- BuildAllScenes failing an
        # audit -- was invisible. The script printed nothing, called the suite
        # green, and left every scene after the failing one stale on disk. A
        # whole session of "why has the screenshot not changed" traces here.
        foreach ($runner in $Runners) {
            $log = Join-Path $runner.Path "gen.log"
            $done = (Test-Path $log) -and (Select-String -Path $log -Pattern "BUILD-COMPLETE" -Quiet)
            if (-not $done) {
                Write-Host "$method did not finish in $($runner.Path). Tail of its log:"
                if (Test-Path $log) { Get-Content $log -Tail 25 | ForEach-Object { Write-Host "  $_" } }
                exit 1
            }
        }

        foreach ($runner in $Runners) {
            # Widened to catch a thrown generator as well as a compile error.
            # "[SceneBuilder] FAILED" is what an audit refusal actually prints,
            # and it was sailing straight through.
            $pattern = "error CS|\[ContentBuilder\]|\[SceneBuilder\] FAILED|threw exception"
            $errors = Get-Content (Join-Path $runner.Path "gen.log") | Select-String -Pattern $pattern | Select-Object -Unique -First 10
            if ($errors) {
                Write-Host "$method FAILED in $($runner.Path):"
                $errors | ForEach-Object { Write-Host "  $_" }
                exit 1
            }
        }
    }

    # SYNC GENERATED ASSETS BACK TO MAIN, IMMEDIATELY.
    #
    # This is CLAUDE.md gotcha #1 and it has to be automatic. Content and
    # scenes are generated in the isolated copies, so main does not have them
    # until they are copied back — and the very next run of this script mirrors
    # main OVER the copies, silently reverting everything that was just built.
    # Doing it here rather than by hand is the difference between "the suite is
    # green" and "the suite tested a stale scene".
    Write-Host "Syncing generated content and scenes back to main..."
    $primary = $Runners[0].Path
    robocopy "$primary\Assets\_Project\Resources\Content" "$SourceProject\Assets\_Project\Resources\Content" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    robocopy "$primary\Assets\_Project\Scenes" "$SourceProject\Assets\_Project\Scenes" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null

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
    robocopy "$primary\Assets\_Project\Art" "$SourceProject\Assets\_Project\Art" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
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
    # independently assign it two DIFFERENT GUIDs — so a scene built in copy 1
    # references a guid that means nothing in copy 2, and the component comes
    # back as a missing script. The symptom is a controller that is plainly
    # in the scene and that FindObjectsByType cannot see, which looks like
    # anything but a GUID problem.
    #
    # Copying the .meta files along with everything else is what makes all
    # three copies agree on identity. CLAUDE.md gotcha #2, reached by a route
    # it does not mention. Followed by the same hash-based repair pass
    # Sync-Runner uses — this robocopy hits the exact same same-size/
    # different-GUID blind spot Repair-Metas exists for, just for freshly
    # GENERATED content assets instead of freshly imported scripts.
    foreach ($runner in $Runners | Select-Object -Skip 1) {
        robocopy "$SourceProject\Assets" "$($runner.Path)\Assets" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        Repair-Metas -DestAssets "$($runner.Path)\Assets"
        Assert-GuidsMatch -Runner $runner
    }
}

Write-Host "`nRunning EditMode and PlayMode concurrently..."
$procs = @{}
foreach ($runner in $Runners) {
    $resultsPath = Join-Path $runner.Path "test-results-$($runner.Platform).xml"
    if (Test-Path $resultsPath) { Remove-Item $resultsPath -Force }

    $procs[$runner.Platform] = Start-Process -FilePath $UnityExe -ArgumentList @(
        "-batchmode", "-nographics", "-silent-crashes",
        "-projectPath", "`"$($runner.Path)`"",
        "-runTests", "-testPlatform", $runner.Platform,
        "-testResults", "`"$resultsPath`"",
        "-logFile", "`"$(Join-Path $runner.Path "test-run-$($runner.Platform).log")`"",
        "-buildTarget", "StandaloneWindows64"
    ) -PassThru -NoNewWindow
}

$procs.Values | Wait-Process -Timeout 1800

$allPassed = $true
foreach ($runner in $Runners) {
    $resultsPath = Join-Path $runner.Path "test-results-$($runner.Platform).xml"
    $logPath = Join-Path $runner.Path "test-run-$($runner.Platform).log"

    if (-not (Test-Path $resultsPath)) {
        Write-Host "No results for $($runner.Platform). Tail of log:"
        Get-Content $logPath -Tail 40 | ForEach-Object { Write-Host $_ }
        $allPassed = $false
        continue
    }

    [xml]$results = Get-Content $resultsPath
    $root = $results.'test-run'
    Write-Host "$($runner.Platform) -- Total: $($root.total)  Passed: $($root.passed)  Failed: $($root.failed)  Skipped: $($root.skipped)  Duration: $($root.duration)s"

    foreach ($f in $results.SelectNodes("//test-case[@result='Failed']")) {
        Write-Host "`nFAILED: $($f.fullname)"
        if ($f.failure -and $f.failure.message) { Write-Host $f.failure.message.InnerText }
    }

    if ([int]$root.failed -ne 0) { $allPassed = $false }
}

if ($allPassed) { Write-Host "`nAll tests passed."; exit 0 }
Write-Host "`nSome tests failed."
exit 1
