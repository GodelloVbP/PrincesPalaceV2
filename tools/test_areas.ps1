# tools/test_areas.ps1 -- single source of truth for test discovery and areas.
#
# Dot-sourced by test.ps1 and run_tests_parallel.ps1. Never invoked directly.
# Pure ASCII, no BOM: CLAUDE.md's PowerShell gotcha applies here same as
# everywhere else in tools/ -- an em-dash inside a string breaks PS 5.1's
# parser and points the error at an unrelated line, so plain "--" throughout.
#
# $PSScriptRoot inside a DOT-SOURCED file resolves to THIS file's own folder
# in PowerShell 5.1, not the caller's -- so this computes its own paths and
# callers pass nothing.

$AreasProjectRoot = Split-Path $PSScriptRoot -Parent
$AreasTestsRoot = Join-Path $AreasProjectRoot "Assets\_Project\Scripts\Tests"

# ---------------------------------------------------------------------------
# AN AREA IS A FOLDER.
#
# Tests/EditMode/<Area>/ and Tests/PlayMode/<Area>/, seven areas, plus a
# Shared/ folder per platform for helpers that carry no tests of their own.
# A test's area is the folder its file sits in, full stop. There is no table
# to extend, because there is nothing a new class could fail to match: a file
# is in exactly one folder, always, and a folder cannot drift the way a regex
# can.
#
# What replaced: a $Areas hashtable of class-NAME regexes, and an orphan gate
# that refused the suite while any class matched none of them. It worked, and
# it cost one commit per new test class -- 44 of its 47 commits in 90 days were
# "add one word so the new class matches", the last being 8a5c3ab for
# UiBindingNamesTests. The invariant was worth keeping; the tax was not.
#
# The gate is still mechanised and still unbypassable, it just checks a
# different thing: run_tests_parallel.ps1 refuses to run while any .cs file
# sits DIRECTLY in Tests/EditMode or Tests/PlayMode rather than in one of the
# eight folders, while any other folder exists beside them, and while anything
# under Shared/ carries a [Test] or [UnityTest]. See Get-StructuralViolations.
#
# ---------------------------------------------------------------------------
# WHERE A NEW TEST GOES. One paragraph per area. These carry the reasoning the
# old per-pattern comments carried, because the reasoning is the part that was
# actually worth keeping; the placements it argued for are unchanged.
#
# Combat/ -- a fight and everything inside one. Sessions, turns, damage,
#   status, shields, skills, spells, wards, cooldowns, marks, fear, armour
#   penetration and the enemy AI that drives them. The balance bot lives here
#   too: Domain/Bot's policies decide over FightSession the same way the fight
#   screen's menu does, so BotPolicy/BotFightAction/BotGearWeights/BotShopPolicy
#   are combat even where the decision is about gold or gear. BalanceSheetTests
#   is here for the same reason and not in content: it needs resolved enemy
#   content and DifficultyCurve, which is why it is a PlayMode test, but
#   combat math is its subject. So is WisdomManaRegenTests -- a real-fight
#   test over CombatantState.ManaRegen and the per-turn regen tick, not a
#   stat-derivation subject on its own. Integration tests named
#   "...ReachesCombat" belong here whatever they carry into the fight: what
#   they catch is a fight-side wiring break. The tie-break that settled most
#   of the combat/content overlaps at the move: a test that builds a real
#   FightSession is Combat whatever content it feeds in -- RelicPotency,
#   RelicMechanics, FourthEpicRelics and SpeedAndBountyRelic are here for
#   that reason while RelicPool, RelicLoadout and RelicModifier, which touch
#   no session, are in Content. The exception is a resolver test with an
#   incidental session or two among forty (EnemyEntryResolver,
#   SkillEntryResolver): those stay with their subject.
#
# Hub/ -- what persists between runs and the screens that show it. Talents
#   (the tree, its gates, its effects, its layout), the constellation, the
#   character sheet, the permanent store, the glossary, and the hub screen's
#   own tree. Talent effects are here rather than in Combat even though most
#   of them only ever fire in a fight, because Domain/Talents is where they
#   are authored and where an edit that breaks them lands; the fight-side
#   counterpart (FightTalentTests, WardIsTalentGatedTests) is in Combat.
#
# Content/ -- the authored data and the resolvers that turn it into records.
#   Every *EntryResolverTests, the stat/scaling/rounding/requirement curves,
#   item sets, rarities, achievements, and the half of the relics that is
#   data: Domain/Relics is a content folder, so the offer pool, the loadout
#   rules and a relic's numbers are content even though every one of them is
#   felt in a fight. Weapon DAMAGE is not -- that is combat math over Domain/
#   Combat, and only the weapon ENTRIES are here. ModifierTable is
#   here and not in Combat for the matching reason -- it covers the RiftTier
#   which-modifiers ROLL, which lives in Domain/Rewards beside RarityTable and
#   is a reward-economy subject, not a combat effect. ArtPathConventionTests
#   is here rather than in Art because what it guards is a pair of conventions
#   authored in content JSON and distinguished only by field name.
#
# Run/ -- one descent, and what survives it. The map and its legs, encounters,
#   rooms, depth and difficulty, the enemy BANDS a depth draws from, the
#   wallet, embers, the ledger, settlement, the reckoning, saves and resumes,
#   the reward track, playtime. Character LEVEL is here rather than in a
#   progression area of its own: level is what survives a run, so it belongs
#   with Reward, Ember and Settlement rather than beside them.
#   CarriedHealthTests is here because what it covers is health carried
#   BETWEEN ROOMS across a change of maximum, which is run state and not the
#   combat health most of this game's health is. Prince's Favor is a
#   run-economy value exactly like the wallet -- it feeds the loot roll
#   through ItemOfferRoll.FavorOf -- so ItemOfferFavorTests is here even
#   though the offer tables themselves are Content. The in-run shop is a run
#   rule too: it spends RUN gold, unlike the permanent store in Hub.
#   GlobalStateLintTests is here because most of what it guards -- save roots,
#   the run, navigation -- is run state; it belongs to no single subject and
#   this is the least wrong home for it.
#
# Ui/ -- presentation, and every screen tree. This is where the UiKit
#   screen-tree tests go, ALL of them, including FightScreenTests,
#   ShopScreenTests, MapScreenTests and DefeatScreenTests whose subjects live
#   in another area. That is deliberate and it is the one placement rule here
#   driven by something other than subject: $PathAreas below maps every
#   Domain/UiKit/ edit to 'ui', so a screen-tree test in any other folder
#   would not re-run when the tree it solves was edited. The exception is the
#   hub's own screens (hub, constellation, glossary, character sheet), which
#   have their own $PathAreas row mapping to hub+ui, so they stay in Hub with
#   the rest of their subject. Beyond screens: buttons and their motion,
#   audio and music, inventory and equipment panes, the dossier, options and
#   GameSettings (the model those rows bind to), tooltips, containers,
#   typography (per-role font and material resolution is presentation, not
#   art), and UiBindingNames.
#
# Art/ -- how a figure is drawn and how it moves at rest. Stances and the
#   stance manifest, sprite facing, hand-assembled sheets, shadows, the
#   battle background, post-processing legibility, and the breath curve --
#   how big a figure is at rest between blows is art timing, not combat.
#   Capture tests that exist to photograph art (EnemyStanceCaptureTests) are
#   here; capture tests that photograph a SCREEN are in Ui.
#
# Rng/ -- the random streams themselves. Two files. Seeding a run is Run's
#   business; the generator's own behaviour is this.
#
# Shared/ -- per-platform helpers with no tests of their own: TestSkills,
#   UiTreeTestHelpers, TestGlobals, PlayModeSparkFixture, StageCaptureRig.
#   These are used from several areas, so no area owns them. The gate asserts
#   nothing here carries a [Test] or [UnityTest] -- a suite parked in Shared
#   would be a suite in no area, which is exactly the hole the old orphan gate
#   existed to close.
#
# ONE FILE, ONE AREA. A file's classes share its folder. Nine files declare
# more than one suite today and every one of them is single-subject; if that
# ever stops being true, split the file rather than picking a folder for the
# majority.
$AreaFolders = @("Combat", "Hub", "Content", "Run", "Ui", "Art", "Rng")
$SharedFolder = "Shared"
# Lower-case, in the order -List prints them and the order CLAUDE.md names
# them. What a caller types on the command line.
$AreaNames = @("combat", "hub", "content", "run", "ui", "art", "rng")

# ---------------------------------------------------------------------------
# Discover every test class: which platform it runs on, which area owns it,
# and which file declares it. All three are read off the filesystem rather
# than maintained by hand -- EditMode and PlayMode are separate assemblies in
# separate folders, and an area is a folder inside those.
#
# One file can hold several classes (WardTests.cs also holds EmpoweredTests
# and GiftHasteTests), which is why this scans file CONTENTS and not
# filenames.
#
# The class-declaration regex is deliberately wide: attributes ([TestFixture]
# and the like), and "sealed"/"static"/"partial" modifiers, are all allowed
# between "public" and "class" alongside the bare form the pattern matched
# before this. A "public sealed class FooTests" used to be invisible here --
# and therefore invisible to every area run and to the gate below, which would
# have reported a clean run while quietly never running FooTests at all.
# "abstract" stays excluded: NUnit cannot instantiate an abstract fixture.
$TestClassPattern = '(?m)^\s*(?:\[[^\]]*\]\s*)*public\s+(?:sealed\s+|static\s+|partial\s+)*class\s+(\w+)'
$TestAttrPattern = '\[\s*(Test|UnityTest)\s*[\(\]]'

# Cached because -List calls into this four times and a cold pass reads 272
# files. $script: scope in a DOT-SOURCED file is the calling script's scope,
# which is exactly the lifetime wanted: one process, one discovery.
$script:TestIndexCache = $null

# name -> PSCustomObject { Platform; Area; File; RelPath }
function Get-TestIndex {
    param([switch]$Fresh)
    if ($script:TestIndexCache -and -not $Fresh) { return $script:TestIndexCache }

    $found = @{}
    foreach ($platform in @("EditMode", "PlayMode")) {
        foreach ($folder in ($AreaFolders + $SharedFolder)) {
            $dir = Join-Path (Join-Path $AreasTestsRoot $platform) $folder
            if (-not (Test-Path $dir)) { continue }
            $area = if ($folder -eq $SharedFolder) { $SharedFolder.ToLower() } else { $folder.ToLower() }
            foreach ($file in Get-ChildItem $dir -Filter *.cs -File) {
                $content = Get-Content $file.FullName -Raw
                foreach ($match in [regex]::Matches($content, $TestClassPattern)) {
                    $name = $match.Groups[1].Value
                    # A base class is not a suite; it has no tests of its own.
                    if ($name -like "*TestBase") { continue }
                    $found[$name] = [PSCustomObject]@{
                        Platform = $platform
                        Area     = $area
                        File     = $file.Name
                        RelPath  = "Assets/_Project/Scripts/Tests/$platform/$folder/$($file.Name)"
                    }
                }
            }
        }
    }

    $script:TestIndexCache = $found
    return $found
}

# class name -> "EditMode"/"PlayMode". The shape callers had before areas
# became folders, kept so nothing downstream had to change.
function Get-TestClasses {
    param([hashtable]$Index = (Get-TestIndex))
    $map = @{}
    foreach ($k in $Index.Keys) { $map[$k] = $Index[$k].Platform }
    return $map
}

# class name -> area (lower-case folder name).
function Get-TestAreas {
    param([hashtable]$Index = (Get-TestIndex))
    $map = @{}
    foreach ($k in $Index.Keys) { $map[$k] = $Index[$k].Area }
    return $map
}

# ---------------------------------------------------------------------------
# THE STRUCTURAL GATE. What run_tests_parallel.ps1 refuses to run over.
#
# Three things, all of them things a FOLDER can be wrong about, since a class
# name no longer can be:
#
#   1. A .cs file sitting directly in Tests/EditMode or Tests/PlayMode. It
#      would be in no area, so no area-based tools/test.ps1 slice and no
#      -Changed run would ever reach it -- the same invisibility the old
#      orphan gate existed to prevent, arriving by the only route still open.
#   2. A folder beside the eight. A ninth folder is either a typo or a new
#      area, and a new area is a decision to make here in the guide above,
#      not something to discover from a run that quietly skipped it.
#   3. A [Test] or [UnityTest] under Shared/. Shared is for helpers; a suite
#      parked there is a suite in no area.
#
# No bypass flag, and none is planned. The fix is a git mv.
function Get-StructuralViolations {
    $violations = @()
    $allowed = $AreaFolders + $SharedFolder
    $folderList = ($AreaFolders -join ", ") + " (or $SharedFolder for helpers with no tests)"

    foreach ($platform in @("EditMode", "PlayMode")) {
        $dir = Join-Path $AreasTestsRoot $platform
        if (-not (Test-Path $dir)) { continue }

        foreach ($file in Get-ChildItem $dir -Filter *.cs -File) {
            $violations += "Assets/_Project/Scripts/Tests/$platform/$($file.Name) is not in an area folder. Move it into one of: $folderList"
        }

        foreach ($sub in Get-ChildItem $dir -Directory) {
            if ($allowed -notcontains $sub.Name) {
                $violations += "Assets/_Project/Scripts/Tests/$platform/$($sub.Name)/ is not an area folder. The areas are: $folderList"
            }
        }

        $sharedDir = Join-Path $dir $SharedFolder
        if (Test-Path $sharedDir) {
            foreach ($file in Get-ChildItem $sharedDir -Filter *.cs -File) {
                $content = Get-Content $file.FullName -Raw
                if ($content -match $TestAttrPattern) {
                    $violations += "Assets/_Project/Scripts/Tests/$platform/$SharedFolder/$($file.Name) carries a [Test]/[UnityTest]. $SharedFolder is for helpers only -- move it into its area folder."
                }
            }
        }
    }

    return $violations
}

# ---------------------------------------------------------------------------
# Files that carry a real [Test]/[UnityTest] but whose class(es)
# Get-TestIndex missed entirely -- an "internal class", or any declaration
# shape the widened regex above still does not cover. Kept because a class
# invisible to discovery is invisible to everything built on discovery,
# including the structural gate's Shared check, and a suite that never runs
# reports as a clean run.
#
# Scoped to exactly the folders Get-TestIndex reads, which is what makes the
# two checks a partition rather than a pile: every .cs under Tests/ is either
# in one of the eight folders, where THIS looks, or outside them, where
# Get-StructuralViolations does. Scanning recursively instead would report a
# misplaced file as a discovery failure -- the wrong diagnosis and the wrong
# fix, since discovery is working fine and the file is simply in no area.
function Get-DiscoveryBlindSpots {
    param([hashtable]$Index = (Get-TestIndex))

    $discoveredNames = New-Object System.Collections.Generic.HashSet[string]
    foreach ($k in $Index.Keys) { [void]$discoveredNames.Add($k) }

    $blindSpots = @()
    foreach ($platform in @("EditMode", "PlayMode")) {
      foreach ($folder in ($AreaFolders + $SharedFolder)) {
        $dir = Join-Path (Join-Path $AreasTestsRoot $platform) $folder
        if (-not (Test-Path $dir)) { continue }
        foreach ($file in Get-ChildItem $dir -Filter *.cs -File) {
            $content = Get-Content $file.FullName -Raw
            if ($content -notmatch $TestAttrPattern) { continue }

            $namesInFile = [regex]::Matches($content, $TestClassPattern) |
                ForEach-Object { $_.Groups[1].Value } |
                Where-Object { $_ -notlike "*TestBase" }

            $anyDiscovered = $false
            foreach ($n in $namesInFile) {
                if ($discoveredNames.Contains($n)) { $anyDiscovered = $true; break }
            }

            if (-not $anyDiscovered) {
                $rel = $file.FullName.Substring($AreasProjectRoot.Length + 1) -replace '\\', '/'
                $blindSpots += $rel
            }
        }
      }
    }

    return $blindSpots | Sort-Object -Unique
}

# ---------------------------------------------------------------------------
# Which HOST runs a class: the dotnet project under tools/domain-tests, or
# Unity.
#
# Domain is engine-free by asmdef (noEngineReferences: true, no references),
# so the EditMode suite over it compiles and runs under plain `dotnet test` --
# no editor, no Library, no project sync. That is the whole saving: booting
# Unity costs more than every EditMode test put together.
#
# Two things decide a class's host, and neither is a hand-maintained list:
#   - PlayMode is always Unity. It needs a scene and a running player loop.
#   - An EditMode class is dotnet-hosted UNLESS its file is excluded from
#     tools/domain-tests/PrincesPalace.Domain.Tests/PrincesPalace.Domain.Tests.csproj,
#     which is read here rather than duplicated. The csproj is the single
#     source of truth for what the fast host compiles; a list here would be a
#     second one, and the two would part ways the first time someone added an
#     exclusion without knowing this file existed.
#
# A class in an excluded file falls back to Unity, which is a correctness-
# preserving default: the worst case for getting this wrong is a slow run,
# never a skipped test.
$DomainTestsCsproj = Join-Path $PSScriptRoot "domain-tests\PrincesPalace.Domain.Tests\PrincesPalace.Domain.Tests.csproj"

# Matched on the BASENAME, with the area folder skipped over rather than
# spelled out: an exclusion path is
# "...\Tests\EditMode\<Area>\<File>.cs" now, and a file moving between areas
# must not silently stop being recognised here.
function Get-UnityOnlyTestFiles {
    if (-not (Test-Path $DomainTestsCsproj)) { return @() }
    $content = Get-Content $DomainTestsCsproj -Raw
    $names = @()
    foreach ($m in [regex]::Matches($content, '<Compile\s+Remove="[^"]*EditMode[\\/](?:[A-Za-z0-9_]+[\\/])*([A-Za-z0-9_]+\.cs)"')) {
        $names += $m.Groups[1].Value
    }
    return $names | Sort-Object -Unique
}

# class name -> "dotnet" or "Unity", for every class Get-TestIndex found.
function Get-TestHosts {
    param([hashtable]$Index = (Get-TestIndex))

    $unityOnly = New-Object System.Collections.Generic.HashSet[string]
    foreach ($f in Get-UnityOnlyTestFiles) { [void]$unityOnly.Add($f) }

    $hosts = @{}
    foreach ($name in $Index.Keys) {
        $entry = $Index[$name]
        if ($entry.Platform -ne "EditMode") { $hosts[$name] = "Unity"; continue }
        if ($unityOnly.Contains($entry.File)) { $hosts[$name] = "Unity"; continue }
        $hosts[$name] = "dotnet"
    }
    return $hosts
}

# ---------------------------------------------------------------------------
# -Changed support: map an uncommitted source-file change to the area(s) it
# belongs to, so "tools/test.ps1 -Changed" can run just the slice affected by
# what is actually sitting in the working tree.
#
# This table is about PRODUCTION paths and is unrelated to the folder-per-area
# move above -- production code does not live in area folders, so something
# still has to say which area an edit to Domain/Combat belongs to. It is
# where the remaining judgment in this file lives.
#
# Three tiers, checked in this order per file:
#   1. $ChangedIgnore    -- cannot affect test behaviour (docs, .meta, ...).
#   2. $ChangedFullSuite  -- affects the test INFRASTRUCTURE itself (the
#      tools/ scripts, asmdefs, scenes, package manifests). A change here
#      must never be validated through its own filtering, so it forces the
#      full suite rather than picking a slice.
#   3. A Tests/**/*.cs file maps to the class(es) IT DECLARES, read from its
#      own content -- not through $PathAreas, since a test file's own name
#      already IS the answer.
#   4. $PathAreas, first match wins (see below).
# Anything left over is UNMAPPED, and the caller fails loudly rather than
# guessing -- see Resolve-ChangedPaths. Silently running a subset because a
# source file had no mapping is exactly the "suite that quietly tests less
# than it claims" failure this whole file exists to prevent.
# tools/githooks/ is listed here rather than falling through to the tools/ rule
# below, and it is the one exception to "anything under tools/ forces the full
# suite". Those scripts are git plumbing -- a pre-commit gate and a PreToolUse
# staging guard. They are not on any path Unity compiles, loads or executes, so
# no arrangement of them can change a test result. The tools/ rule exists for
# scripts that drive the suite itself, which these do not.
$ChangedIgnore = '^(docs/|\.claude/|\.gitignore$|\.gitattributes$|tools/githooks/|.*\.md$|.*\.meta$|tools/timings\.json$)'
$ChangedFullSuite = '^(tools/|Packages/|ProjectSettings/|Assets/_Project/Scripts/[^/]+\.asmdef$|Assets/_Project/Scenes/)'

# An ORDERED array, not a hashtable -- @{} enumerates in arbitrary order in
# PS 5.1, and even [ordered] would bury the first-match-wins contract this
# depends on. Keep more specific prefixes ABOVE broader ones in the same
# folder (see the Core/ block: Fight/GameplayManager/etc above the catch-all
# "Core/ -> ui" fallback at the bottom).
#
# The flat "Core/ -> ui" fallback is a deliberate judgment call, not an
# oversight: Core/ holds ~75 mostly visual/controller files, and enumerating
# every one would itself rot the way the original five areas did. The
# alternative -- no fallback, every new Core file UNMAPPED until someone adds
# a row -- is more precise but pushes people toward "just run -Full", which
# defeats the point of this flag.
$PathAreas = @(
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Combat/';    Areas = @('combat') }
    # The balance bot's brains -- policies decide over FightSession the same
    # way the fight screen's menu does, so it belongs beside Domain/Combat/.
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Bot/';       Areas = @('combat') }
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Content/';   Areas = @('content') }
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Dungeon/';   Areas = @('run') }
    @{ Pattern = '^Assets/_Project/Scripts/Domain/DebugMenu/'; Areas = @('ui', 'content') }
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Economy/';   Areas = @('run') }
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Equipment/'; Areas = @('content', 'ui') }
    # Inventory moved out of Data/ (Core) into Domain so its rules could be
    # EditMode-tested at all -- Data/ compiles into Core and the EditMode suite
    # is Domain-only, so InventoryOps was unreachable by any test where it lived.
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Inventory/'; Areas = @('ui', 'run') }
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Relics/';    Areas = @('content') }
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Rewards/';   Areas = @('run', 'content') }
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Rng/';       Areas = @('rng') }
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Stage/';     Areas = @('combat', 'art') }
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Stats/';     Areas = @('content') }
    # The UI construction layer lives in Domain (engine-free) so a screen can be
    # built, solved and audited from EditMode in under a second -- see
    # docs/REBUILD.md M4. Talents moves here for TalentLayout's sake.
    # Hub geometry and the hub's own screen tree live under UiKit, so the
    # generic row below would send a hub edit to the 'ui' suite only and never
    # run the hub tests that exist to catch it. First match wins, so this sits
    # above it deliberately.
    @{ Pattern = '^Assets/_Project/Scripts/Domain/UiKit/(Hub|Constellation|Screens/Hub)'; Areas = @('hub', 'ui') }
    @{ Pattern = '^Assets/_Project/Scripts/Domain/UiKit/';     Areas = @('ui') }
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Talents/';   Areas = @('hub') }
    # Music/audio config. 'ui' rather than a new area of its own: every audio
    # test already sits in Tests/*/Ui, and one more area for two folders would
    # be the kind of over-precision the placement guide above warns about.
    @{ Pattern = '^Assets/_Project/Scripts/Domain/Audio/';     Areas = @('ui') }
    @{ Pattern = '^Assets/_Project/Scripts/Core/Content/';     Areas = @('content') }
    @{ Pattern = '^Assets/_Project/Scripts/Core/Fight';        Areas = @('combat') }
    @{ Pattern = '^Assets/_Project/Scripts/Core/(GameplayManager|RunState|SaveSystem|SaveSlot)'; Areas = @('run') }
    @{ Pattern = '^Assets/_Project/Scripts/Core/(Stance|StaticSwing|Stage|Sprite|Procedural)'; Areas = @('art', 'combat') }
    @{ Pattern = '^Assets/_Project/Scripts/Core/(Music|Sound|Audio)'; Areas = @('ui') }
    @{ Pattern = '^Assets/_Project/Scripts/Core/(Hub|Talent|Store|CharacterSheet|CharacterSelect|CharacterTab)'; Areas = @('hub') }
    @{ Pattern = '^Assets/_Project/Scripts/Core/(DescentMapView|Map)'; Areas = @('run', 'ui') }
    # Core/Bot -- RunOrchestrator (the whole rulebook of a run: arrival, fight
    # build, settlement, offers) plus BotRunDriver/ProfilePresets. Above the
    # Core/ catch-all deliberately: that fallback would claim it as 'ui'
    # alone, and 'ui' runs none of the fight settlement or run-state suites
    # this code is actually the seam for.
    @{ Pattern = '^Assets/_Project/Scripts/Core/Bot/'; Areas = @('combat', 'run') }
    @{ Pattern = '^Assets/_Project/Scripts/Core/'; Areas = @('ui') }
    @{ Pattern = '^Assets/_Project/Scripts/Data/'; Areas = @('run') }
    @{ Pattern = '^Assets/_Project/Scripts/UI/';   Areas = @('ui') }
    # Bakes the shapes a flat uGUI Image cannot draw (the glow, the disc, the
    # armour stand) into committed PNGs. 'art' because it produces art, 'ui'
    # because every consumer is a screen tree.
    @{ Pattern = '^Assets/_Project/Scripts/Editor/ProceduralSpriteBaker'; Areas = @('art', 'ui') }
    # The batch entry point. Its own row because Editor/ had no catch-all and
    # an edit to BalanceBotRunner.cs landed in -Changed's UNMAPPED list --
    # which exits 2 and runs nothing, on a file whose whole job is the bot.
    @{ Pattern = '^Assets/_Project/Scripts/Editor/Bot/'; Areas = @('combat', 'run') }
    @{ Pattern = '^Assets/_Project/Scripts/Editor/SceneBuilder'; Areas = @('ui', 'hub') }
    @{ Pattern = '^Assets/_Project/Scripts/Editor/ContentBuilder'; Areas = @('content') }
    # Dev-only menu item that opens Fight.unity and forces FightBootstrap's
    # placeholder-fight enemy pick -- exercises the same combat bootstrap
    # path as everything else under Core/, nothing UI- or content-specific.
    @{ Pattern = '^Assets/_Project/Scripts/Editor/QuickFightMenu'; Areas = @('combat') }
    # Import-time texture coercion (Enemy/Item/Intent/Status sprite folders).
    # None of these had a $PathAreas row before StatusIconImportPostprocessor
    # was added alongside this file, which meant -Changed would have refused
    # to run at all while any of the three sat uncommitted. One broad pattern
    # rather than one row per postprocessor -- overlap across combat/ui/art/
    # content is fine, and it is precise enough that a real UNMAPPED editor
    # file still refuses loudly.
    @{ Pattern = '^Assets/_Project/Scripts/Editor/.*ImportPostprocessor'; Areas = @('combat', 'ui', 'art', 'content') }
    # THE THREE FILES WHOSE ROWS REACH THE STAGE. A mob, its skills and the
    # spell tiers all decide what gets DRAWN as well as what gets computed: an
    # enemy row names a spritePath, a skill row names the stance its caster
    # strikes in and the vfx folder its cast plays. So an edit to one of them
    # has to re-run the art sweeps (EnemyArtCompletenessTests resolves every
    # pose every mob can reach; the stance-manifest tests measure them) and the
    # fight suites, not just the resolver tests.
    #
    # Above the general ContentData row deliberately -- first match wins.
    @{ Pattern = '^Assets/_Project/ContentData/(enemies|skills|spells)\.json$'; Areas = @('content', 'combat', 'art') }
    # Every other content table -- items, weapons, itemsets, talents, relics,
    # achievements, modifiers, characters. These change what a fight COMPUTES
    # (stats, effects, offers) without changing what it draws, so combat comes
    # along and art does not. 'content' alone was the old mapping and it meant a
    # relic's effect could be rewritten with the fight suites never run.
    @{ Pattern = '^Assets/_Project/ContentData/'; Areas = @('content', 'combat') }
    # Runtime-loaded config that is NOT baked by ContentBuilder -- the two
    # audio tables (audio_levels.json, music_layers.json) and the generated
    # content tree. Both were UNMAPPED, which made "-Changed" fail loudly the
    # moment a content build touched Resources or anyone edited a music table.
    @{ Pattern = '^Assets/_Project/Resources/Audio/';   Areas = @('ui') }
    @{ Pattern = '^Assets/_Project/Resources/Content/'; Areas = @('content') }
    # The enemy intent badges. 'combat' for the lookup tables that name them and
    # 'ui' for the screen side.
    @{ Pattern = '^Assets/_Project/Resources/Intent/';   Areas = @('combat', 'ui') }
    # The party status badges (Chilled/Rooted so far). Same reasoning as
    # Intent/ immediately above, one folder over.
    @{ Pattern = '^Assets/_Project/Resources/Status/';   Areas = @('combat', 'ui') }
    # Runtime-loaded ART: the stance folders a monster's spritePath names, the
    # f0..fN spell sequences, and the manifest that measures both. Unmapped
    # until a whole boss's worth of frames landed at once and -Changed refused;
    # 'art' for the import settings and ground lines, 'combat' because the fight
    # stage is the only thing that loads any of it.
    @{ Pattern = '^Assets/_Project/Resources/(Enemies|Spells)/'; Areas = @('art', 'combat') }
    @{ Pattern = '^Assets/_Project/Resources/StanceManifest\.json'; Areas = @('art', 'combat') }
    # Runtime-loaded shaders/materials. Today that is UIHitFlash, the
    # stage's hit reaction, so 'combat'+'art' rather than a bespoke
    # 'shader' area of its own for one file.
    @{ Pattern = '^Assets/_Project/Resources/(Shaders|Materials)/'; Areas = @('art', 'combat') }
    @{ Pattern = '^Assets/_Project/Art/';         Areas = @('art') }
    # TMP font assets. 'ui' rather than 'art': the thing that breaks when one
    # of these changes is text metrics -- a re-baked atlas shifts glyph
    # advances, so a label that fitted its box stops fitting -- and it is
    # UiAudit's overflow check in the ui suite that catches it, not anything
    # in art. Was UNMAPPED, which failed -Changed loudly the moment the
    # ChakraPetch SDF asset was touched.
    @{ Pattern = '^Assets/_Project/Fonts/';       Areas = @('ui') }
)

# Every changed file (tracked modifications against HEAD, plus untracked
# files git would add), as forward-slash relative paths -- Unity/Windows
# paths use backslash, but every pattern above and in $ChangedIgnore /
# $ChangedFullSuite is written slash-forward, so this is the one place that
# normalizes it.
function Get-ChangedFiles {
    param([string]$RepoRoot = $AreasProjectRoot)

    $tracked = & git -C $RepoRoot diff --name-only HEAD
    $untracked = & git -C $RepoRoot ls-files --others --exclude-standard
    $all = @($tracked) + @($untracked) | Where-Object { $_ } | Sort-Object -Unique
    return $all | ForEach-Object { $_ -replace '\\', '/' }
}

# Resolves a list of changed paths into what to run. Returns a PSCustomObject:
#   Areas     - [string[]] area names to union
#   Classes   - [string[]] specific classes to run directly (from a changed
#               Tests/**/*.cs file's own declared class(es))
#   Ignored   - [string[]] files matched by $ChangedIgnore
#   Unmapped  - [string[]] files that hit none of the tiers -- CALLER MUST
#               treat a non-empty list here as a hard failure, not a warning
#   FullSuite - [string[]] files that forced the full-suite tier
#   Mapping   - [string[]] printable "path -> target" lines, in input order,
#               for full transparency about what a run is about to include
function Resolve-ChangedPaths {
    param(
        [string[]]$Paths,
        [hashtable]$Classes = (Get-TestClasses)
    )

    $areas = @()
    $classesOut = @()
    $ignored = @()
    $unmapped = @()
    $fullSuite = @()
    $mapping = @()

    # The area segment is optional in this pattern on purpose: a test file
    # that has NOT been moved into one is still a test file, and mapping it to
    # its own classes is a better answer than calling it UNMAPPED. The
    # structural gate is what refuses it, loudly, in the one place a refusal
    # belongs.
    $testFilePattern = '^Assets/_Project/Scripts/Tests/(EditMode|PlayMode)/([^/]+/)?[^/]+\.cs$'

    foreach ($path in $Paths) {
        if ($path -match $ChangedIgnore) {
            $ignored += $path
            $mapping += "$path -> (ignored)"
            continue
        }

        if ($path -match $ChangedFullSuite) {
            $fullSuite += $path
            $mapping += "$path -> (forces full suite)"
            continue
        }

        if ($path -match $testFilePattern) {
            $fullPath = Join-Path $AreasProjectRoot ($path -replace '/', '\')
            if (-not (Test-Path $fullPath)) {
                # Deleted test file: nothing to run for it, and its class(es)
                # -- if still present in $Classes at all -- belong to some
                # OTHER surviving file, so do not touch $classesOut here.
                $mapping += "$path -> (deleted, nothing to run)"
                continue
            }

            $content = Get-Content $fullPath -Raw
            $namesInFile = [regex]::Matches($content, $TestClassPattern) |
                ForEach-Object { $_.Groups[1].Value } |
                Where-Object { $_ -notlike "*TestBase" -and $Classes.ContainsKey($_) }

            if ($namesInFile) {
                $classesOut += $namesInFile
                $mapping += "$path -> $($namesInFile -join ', ')"
            } else {
                # A test file with no class discovery recognizes is either a
                # discovery blind spot or a file outside an area folder --
                # Get-DiscoveryBlindSpots and Get-StructuralViolations (and
                # the gate in run_tests_parallel.ps1) are what catch those,
                # not this function.
                $mapping += "$path -> (no discovered class in this file; see -List's structure section)"
            }
            continue
        }

        $hit = $null
        foreach ($entry in $PathAreas) {
            if ($path -match $entry.Pattern) { $hit = $entry; break }
        }

        if ($hit) {
            $areas += $hit.Areas
            $mapping += "$path -> $($hit.Areas -join '+')"
        } else {
            $unmapped += $path
            $mapping += "$path -> (UNMAPPED)"
        }
    }

    return [PSCustomObject]@{
        Areas     = @($areas | Sort-Object -Unique)
        Classes   = @($classesOut | Sort-Object -Unique)
        Ignored   = @($ignored)
        Unmapped  = @($unmapped)
        FullSuite = @($fullSuite)
        Mapping   = @($mapping)
    }
}
