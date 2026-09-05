param(
    [switch]$SkipSync,
    [switch]$BuildContent,
    [switch]$BuildScenes,
    [switch]$NoScenes
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

# Shares discovery, areas and the structural gate with tools/test.ps1, so the
# gate below checks against the exact same definitions a slice would use.
. (Join-Path $PSScriptRoot "test_areas.ps1")

$ProjectLeaf = Split-Path $SourceProject -Leaf
$ProjectParent = Split-Path $SourceProject -Parent
$ProductLeaf = ($ProjectLeaf -replace "[^A-Za-z0-9]", "")

$Runners = @(
    @{ Platform = "EditMode"; Path = "$ProjectParent\$ProjectLeaf-TestRunner";  Product = "${ProductLeaf}TestRunner" }
    @{ Platform = "PlayMode"; Path = "$ProjectParent\$ProjectLeaf-TestRunner2"; Product = "${ProductLeaf}TestRunner2" }
)

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

    # docs/ is not a Unity folder and the runner has no use for it -- except
    # that a test now READS one of its files. ContentSchemaTests walks up from
    # the working directory to whatever holds Assets/_Project/Scripts and
    # compares docs/CONTENT_SCHEMA.md against what ContentSchema.Generate()
    # produces. Under `dotnet test` that walk lands in the real repo and the
    # test passes; under Unity it lands in this copy, which had no docs/ at
    # all, so the test could only ever fail here. 5.6 MB, mirrored once.
    robocopy "$SourceProject\docs" "$($Runner.Path)\docs" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null

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
# The two checks partition every .cs under Tests/ rather than overlapping:
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

$blindSpots = Get-DiscoveryBlindSpots -Index $discoveredIndex
if ($blindSpots.Count -gt 0) {
    Write-Host "TEST DISCOVERY BLIND SPOT ($($blindSpots.Count) file(s)) -- a [Test]/[UnityTest] exists here but its class was never discovered:"
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

if (-not $SkipSync) {
    Write-Host "Syncing into $($Runners.Count) isolated test copies..."
    foreach ($runner in $Runners) { Sync-Runner -Runner $runner; Stamp "sync -> $($runner.Platform)" }

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

        $proc = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru -NoNewWindow
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
    # until they are copied back — and the very next run of this script mirrors
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
    Stamp "sync generated assets back to main"
    foreach ($runner in $Runners | Select-Object -Skip 1) {
        robocopy "$SourceProject\Assets" "$($runner.Path)\Assets" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        Stamp "re-mirror main -> $($runner.Platform)"

        # STRAIGHT FROM THE PRIMARY, because the mirror above just overwrote
        # this runner's scenes with main's.
        #
        # PlayMode used to receive the built scenes THROUGH main, which only
        # worked because the sync-back was unconditional. Now that it is not,
        # skipping this would hand PlayMode the last COMMITTED scenes while
        # EditMode audited freshly built ones -- the two platforms testing
        # different builds, silently, which is worse than the staleness this
        # whole change is about.
        if ($BuildScenesHere) {
            robocopy "$primary\Assets\_Project\Scenes" "$($runner.Path)\Assets\_Project\Scenes" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
            Stamp "scenes -> $($runner.Platform)"
        }
        Repair-Metas -DestAssets "$($runner.Path)\Assets"
        Stamp "Repair-Metas $($runner.Platform)"
        Assert-GuidsMatch -Runner $runner
        Stamp "Assert-GuidsMatch $($runner.Platform)"
    }
}

Write-Host "`nRunning EditMode and PlayMode concurrently..."
$script:LastStamp = $Watch.Elapsed
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
