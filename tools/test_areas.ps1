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
# Named areas: regex patterns matched against CLASS names, not hand-listed
# classes, so a new WhateverFightTests joins "combat" on its own tomorrow.
#
# The invariant this whole file exists to hold: every class Get-TestClasses
# discovers matches at least one pattern below. "tools/test.ps1 -List"
# reports violations under ORPHANS; "tools/run_tests_parallel.ps1" refuses to
# run at all while there are any (see Get-AreaOrphans and its caller there).
# Extend a pattern rather than leaving a class unmatched -- an orphaned class
# is invisible to every area-based AND -Changed run, which is worse than an
# imprecise area. Overlap between areas is fine and already happens (a class
# can and often does belong to two subjects at once).
$Areas = @{
    # 'Balance' for BalanceSheetTests (Phase 5D) -- the combat-math validation
    # suite over §P's canonical profiles. It also needs real resolved enemy
    # content and DifficultyCurve, which is why it lives in PlayMode, but
    # combat is its subject, same reasoning as everything else in this area.
    # 'Modifier' for the item-modifier effect bag (Phase A2, ModifierEffect/
    # ModifierEffectSet) -- it lives in Domain/Combat and mirrors
    # TalentEffectType/TalentEffectSet exactly, so it belongs beside them
    # rather than in 'content' (where ModifierEntryResolverTests already
    # matches on its own via 'Resolver' -- the overlap is fine).
    # 'Wisdom' for WisdomManaRegenTests (the WIS-derived Mana Regen pass) --
    # a real-fight PlayMode test over CombatantState.ManaRegen and the
    # per-turn regen tick, the same subject FightSession.Riders' own regen
    # rider lives in, not a content/stat-derivation subject on its own.
    # 'Mark|Fear|Falling|Penetration|Convergence' for the balance-bot pass's
    # own mechanic tests (MarksTests, FearTests, FallingOffStacksTests,
    # ArmorPenetrationTests, ConvergenceGateTests) -- all five are Domain/
    # Combat facilities (Marks.cs, Fear.cs, FallingOffStacks.cs,
    # CombatantState.ArmorPenetration, ConvergenceGate.cs), the same
    # PathAreas folder every other combat mechanic already lives in.
    # 'Squad' for SquadOfThreeTests/SaveDataSquadOfThreeTests -- the
    # three-member-party placeholders and their fight/paging/bot coverage,
    # same PathAreas folder as Party/Bot above.
    combat  = "Fight|Wool|Spell|Combat|Enemy|Party|Squad|Stage|Boss|Turn|Skill|Damage|Defeat|Teardown|BreakShield|Status|Signature|Ward|Gift|Empower|Cooldown|Relic|Balance|Modifier|Bot|Wisdom|Mark|Fear|Falling|Penetration|Convergence"
    hub     = "Hub|Talent|Principality|CharacterSheet|SheetStat|Store|Constellation|Glossary"
    # 'ModifierTable' explicitly, alongside the bare 'Modifier' already
    # matching combat above -- ModifierTableTests (Phase A3) covers the
    # RiftTier/which-modifiers ROLL, which lives in Domain/Rewards beside
    # RarityTable and is a content/reward-economy subject, not a combat
    # effect. The overlap with combat's 'Modifier' is fine per this file's
    # own header; this is what makes "content,run" actually cover it too.
    # 'RiftTier' alongside 'Rarity' -- RiftTierColorsTests (item-modifier plan
    # Phase E) is the Unity-facing colour table for the ROLL, the exact same
    # relationship RarityColorsTests already has to Rarity, so it belongs in
    # the same area for the same reason.
    content = "Content|ItemSet|Item|Resolver|ArtPath|AbilityScore|StatBlock|StatPoint|Invest|Character|Enemy|Scaling|Gear|Budget|Requirement|Rounding|AbilityDerivation|Weapon|Relic|Rarity|RiftTier|Achievement|RoundTrip|ModifierTable"
    # GlobalState: the lint that keeps a test from leaving a static flipped for
    # the rest of the process. It belongs to no single subject -- the statics it
    # guards are save roots, the run, navigation and two tuning knobs -- and
    # this file's own header says an imprecise area beats an orphan, since an
    # orphan is invisible to every area run. 'run' because most of what it
    # guards is run/save state.
    # 'Level' joins MetaProgression here rather than starting a 'progression'
    # area of its own: character level is what survives a run, so it belongs
    # with Reward, Ember and Settlement rather than beside them. It matches
    # exactly one class today (LevelCurveTests) and no other class in either
    # assembly contains the word.
    # 'CarriedHealth' rather than a bare 'Health': what it covers is health
    # CARRIED BETWEEN ROOMS across a change of maximum, which is run state.
    # A bare pattern would reach into combat, where most of the health in this
    # game lives and where none of it is this.
    # 'Favor' for FortunateFavorTests and ItemOfferFavorTests -- Prince's
    # Favor is a run-economy value exactly like Wallet/Reward/Ember (it feeds
    # the loot roll via ItemOfferRoll.FavorOf/CurrentSquadFavor, both in
    # Core, not a combat rule), even though Fortunate's own bonus is now read
    # live off equipment rather than written anywhere.
    # 'Shop' for the in-run shop (ShopPricingTests, ShopStockTests,
    # ShopMutationTests) -- the shop is a run-economy rule, the same subject
    # Wallet/Reward/Ember/Favor already cover here, and it spends RUN gold
    # rather than the permanent currency the hub's 'Store' pattern names.
    run     = "Dungeon|Map|FullRun|RunState|Currency|MetaProgression|Level|Save|ActiveSquad|Run|Resume|Snapshot|Seed|Descent|Depth|Difficulty|EnemyBand|Wallet|Reward|Reckoning|Ember|Ledger|Settlement|Encounter|Room|GlobalState|CarriedHealth|Playtime|Favor|Shop"
    # 'OfferRow' rather than a bare 'Offer': the offer ROW is a layout and
    # belongs here, but ItemOfferTests and ItemOfferRollTests are reward rules
    # that already sit in 'content' and 'run', and a bare pattern would drag
    # them in for the sake of a shared word.
    # 'Typography' for TypographyRoleTests -- the per-role font/material
    # asset resolution (TmpBootstrap.Typography.cs, SceneBuilder.FontFor/
    # MaterialFor) is UI presentation, same subject as everything else here.
    # 'Settings' for GameSettingsTests (balance-bot item 7, 2026-09-03) --
    # GameSettings is the model OptionsPaneTests/OptionsController's rows
    # bind to (audio/resolution/window mode/fps), the same UI-presentation
    # subject 'Options' already covers here.
    ui      = "Button|Audio|Splash|PauseMenu|DebugMenu|MainMenu|SystemMenu|Cursor|Dialogue|Bark|Inventory|Equipment|Dossier|Options|Settings|Music|Screen|UiKit|Flicker|Ambience|Overlay|OfferRow|Tooltip|Typography|Container|UiBindingNames"
    # 'BreathCurve' rather than a bare 'Breath': the curve is art timing --
    # how big a figure is at rest between blows -- and belongs here, while a
    # bare 'Breath' would be a word common enough to drag in anything.
    art     = "Stance|StaticSwing|BreathCurve|Shadow|WhiteQuad|SpriteFacing|BattleBackground|ItemArt|TalentArt|ArtPath|HandAssembled|Flash|Legibility|PostProcessing"
    rng     = "Rng|SeededRandom|Seed"
}

# ---------------------------------------------------------------------------
# Discover which platform each test class lives on, read off the filesystem
# rather than maintained by hand: EditMode and PlayMode are separate
# assemblies in separate folders, so the folder a class sits in IS its
# platform. One file can hold several classes (AudioTests.cs also holds
# SplashTests), which is why this scans file CONTENTS and not filenames.
#
# The class-declaration regex is deliberately wide: attributes ([TestFixture]
# and the like), and "sealed"/"static"/"partial" modifiers, are all allowed
# between "public" and "class" alongside the bare form the pattern matched
# before this. A "public sealed class FooTests" used to be invisible here --
# and therefore invisible to $Areas, to Get-AreaOrphans, and to the orphan
# gate below, which would have reported a clean run while quietly never
# running FooTests at all. "abstract" stays excluded: NUnit cannot
# instantiate an abstract fixture, so GameplayTestBase correctly still does
# not count as a suite.
function Get-TestClasses {
    $found = @{}
    foreach ($platform in @("EditMode", "PlayMode")) {
        $dir = Join-Path $AreasTestsRoot $platform
        if (-not (Test-Path $dir)) { continue }
        foreach ($file in Get-ChildItem $dir -Filter *.cs -File) {
            $content = Get-Content $file.FullName -Raw
            foreach ($match in [regex]::Matches($content, '(?m)^\s*(?:\[[^\]]*\]\s*)*public\s+(?:sealed\s+|static\s+|partial\s+)*class\s+(\w+)')) {
                $name = $match.Groups[1].Value
                # A base class is not a suite; it has no tests of its own.
                if ($name -like "*TestBase") { continue }
                $found[$name] = $platform
            }
        }
    }
    return $found
}

# ---------------------------------------------------------------------------
# Files that carry a real [Test]/[UnityTest] but whose class(es)
# Get-TestClasses missed entirely -- an "internal class", or any declaration
# shape the widened regex above still does not cover. This is what keeps the
# orphan gate honest: an orphan check can only see what discovery found, so a
# class invisible to discovery would never even reach the orphan list, and
# the gate would report a clean run while a whole suite silently never runs.
#
# Kept independent of $Areas on purpose -- this catches a DISCOVERY failure,
# not a categorization failure. Conflating the two into one check would make
# a discovery bug look like nothing more than an uncategorized class.
function Get-DiscoveryBlindSpots {
    param([hashtable]$Classes = (Get-TestClasses))

    $discoveredNames = New-Object System.Collections.Generic.HashSet[string]
    foreach ($k in $Classes.Keys) { [void]$discoveredNames.Add($k) }

    $blindSpots = @()
    foreach ($platform in @("EditMode", "PlayMode")) {
        $dir = Join-Path $AreasTestsRoot $platform
        if (-not (Test-Path $dir)) { continue }
        foreach ($file in Get-ChildItem $dir -Filter *.cs -File) {
            $content = Get-Content $file.FullName -Raw
            if ($content -notmatch '\[\s*(Test|UnityTest)\s*[\(\]]') { continue }

            $namesInFile = [regex]::Matches($content, '(?m)^\s*(?:\[[^\]]*\]\s*)*public\s+(?:sealed\s+|static\s+|partial\s+)*class\s+(\w+)') |
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

    return $blindSpots | Sort-Object -Unique
}

# ---------------------------------------------------------------------------
# Classes matched by NO $Areas pattern. Takes names as a PARAMETER (default:
# a fresh discovery pass) specifically so this is provable in isolation --
# "Get-AreaOrphans -ClassNames @('MadeUpTests')" answers the question without
# touching the filesystem or booting Unity, which is how the gate's
# non-vacuity gets checked cheaply and repeatedly rather than trusted.
function Get-AreaOrphans {
    param([string[]]$ClassNames = (Get-TestClasses).Keys)

    $orphans = @()
    foreach ($name in $ClassNames) {
        $matched = $false
        foreach ($pattern in $Areas.Values) {
            if ($name -match $pattern) { $matched = $true; break }
        }
        if (-not $matched) { $orphans += $name }
    }
    return $orphans | Sort-Object -Unique
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

function Get-UnityOnlyTestFiles {
    if (-not (Test-Path $DomainTestsCsproj)) { return @() }
    $content = Get-Content $DomainTestsCsproj -Raw
    $names = @()
    foreach ($m in [regex]::Matches($content, '<Compile\s+Remove="[^"]*EditMode[\\/]([A-Za-z0-9_]+\.cs)"')) {
        $names += $m.Groups[1].Value
    }
    return $names | Sort-Object -Unique
}

# class name -> "dotnet" or "Unity", for every class Get-TestClasses found.
function Get-TestHosts {
    param([hashtable]$Classes = (Get-TestClasses))

    $unityOnly = New-Object System.Collections.Generic.HashSet[string]
    foreach ($f in Get-UnityOnlyTestFiles) { [void]$unityOnly.Add($f) }

    # Which file declares which EditMode class. Same regex Get-TestClasses
    # uses, deliberately -- a class this cannot place is one Get-TestClasses
    # never saw either, so there is nothing to disagree about.
    $fileOf = @{}
    $dir = Join-Path $AreasTestsRoot "EditMode"
    if (Test-Path $dir) {
        foreach ($file in Get-ChildItem $dir -Filter *.cs -File) {
            $content = Get-Content $file.FullName -Raw
            foreach ($match in [regex]::Matches($content, '(?m)^\s*(?:\[[^\]]*\]\s*)*public\s+(?:sealed\s+|static\s+|partial\s+)*class\s+(\w+)')) {
                $fileOf[$match.Groups[1].Value] = $file.Name
            }
        }
    }

    $hosts = @{}
    foreach ($name in $Classes.Keys) {
        if ($Classes[$name] -ne "EditMode") { $hosts[$name] = "Unity"; continue }
        $declaringFile = $fileOf[$name]
        if ($declaringFile -and $unityOnly.Contains($declaringFile)) { $hosts[$name] = "Unity"; continue }
        if (-not $declaringFile) { $hosts[$name] = "Unity"; continue }
        $hosts[$name] = "dotnet"
    }
    return $hosts
}

# ---------------------------------------------------------------------------
# -Changed support: map an uncommitted source-file change to the area(s) it
# belongs to, so "tools/test.ps1 -Changed" can run just the slice affected by
# what is actually sitting in the working tree.
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
# defeats the point of this flag. The area-orphan gate is where the loud,
# unbypassable invariant belongs; this map is allowed to be a little soft.
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
    # test class already matches the ui pattern (Audio|Music), and one more
    # area for two folders would be the kind of over-precision this file's
    # own header warns rots first.
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
    # content is fine per this file's own header, and it is precise enough
    # that a real UNMAPPED editor file still refuses loudly.
    @{ Pattern = '^Assets/_Project/Scripts/Editor/.*ImportPostprocessor'; Areas = @('combat', 'ui', 'art', 'content') }
    @{ Pattern = '^Assets/_Project/ContentData/'; Areas = @('content') }
    # Runtime-loaded config that is NOT baked by ContentBuilder -- the two
    # audio tables (audio_levels.json, music_layers.json) and the generated
    # content tree. Both were UNMAPPED, which made "-Changed" fail loudly the
    # moment a content build touched Resources or anyone edited a music table.
    @{ Pattern = '^Assets/_Project/Resources/Audio/';   Areas = @('ui') }
    @{ Pattern = '^Assets/_Project/Resources/Content/'; Areas = @('content') }
    # The enemy intent badges. 'combat' for the lookup tables that name them and
    # 'ui' for EnemyIntentIconTests, which loads the Fight scene and checks that
    # every kind resolves to a sprite that actually imported -- the one test that
    # would catch a new icon dropped in with the wrong texture type.
    @{ Pattern = '^Assets/_Project/Resources/Intent/';   Areas = @('combat', 'ui') }
    # The party status badges (Chilled/Rooted so far). Same reasoning as
    # Intent/ immediately above, one folder over: 'combat' for the lookup
    # table that names them (FightHudModel.StatusBadgeIcons) and 'ui' for
    # StatusBadgeIconTests, which checks every iconised kind resolves to a
    # sprite that actually imported.
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

    $testFilePattern = '^Assets/_Project/Scripts/Tests/(EditMode|PlayMode)/[^/]+\.cs$'
    $classDeclPattern = '(?m)^\s*(?:\[[^\]]*\]\s*)*public\s+(?:sealed\s+|static\s+|partial\s+)*class\s+(\w+)'

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
            $namesInFile = [regex]::Matches($content, $classDeclPattern) |
                ForEach-Object { $_.Groups[1].Value } |
                Where-Object { $_ -notlike "*TestBase" -and $Classes.ContainsKey($_) }

            if ($namesInFile) {
                $classesOut += $namesInFile
                $mapping += "$path -> $($namesInFile -join ', ')"
            } else {
                # A test file with no class Get-TestClasses recognizes is a
                # discovery blind spot, not an unmapped path -- Get-
                # DiscoveryBlindSpots (and the gate in run_tests_parallel.ps1)
                # is what is supposed to catch that, not this function.
                $mapping += "$path -> (no discovered class in this file; see -List's blind-spot section)"
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
