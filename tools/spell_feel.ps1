param(
    [string]$Spell = "",
    # enemies.json ids to cast at; one capture and one report each.
    [string[]]$Versus = @("treant", "rat"),
    # Where the report folders go. Default: docs/captures/spell_feel.
    [string]$OutDir = "",
    # Reuse the frames already under tools/screenshots/spell_feel/<spell>_vs_<enemy>/
    # instead of capturing again.
    [switch]$ReportOnly,
    # Two report folders to put side by side (before, after); no capture.
    [string[]]$Compare = @(),
    [double]$ResidueLimit = 0.5,
    [double]$AccentRatio = 1.5,
    [int]$DesyncFrames = 2
)

# One command for docs/SPELL_FEEL.md step 4: capture a spell at each target,
# then measure it.
#
#   tools/spell_feel.ps1 -Spell blackglass_spear
#   tools/spell_feel.ps1 -Spell blackglass_spear -Versus treant,rat -OutDir docs\captures\x
#   tools/spell_feel.ps1 -Spell blackglass_spear -Versus rat -ReportOnly
#   tools/spell_feel.ps1 -Compare docs\captures\a\spell_vs_rat,docs\captures\b\spell_vs_rat
#
# Capturing is tools/screenshot.ps1 -Runtime -Spell <id> with PP_RUNTIME_VERSUS
# set per target; that script syncs the TestRunner copy and clears its output
# each run, so the frames are copied out to
# tools/screenshots/spell_feel/<spell>_vs_<enemy>/ (git-ignored) before the
# next target starts. The measuring is tools/spell_feel.py; its header says
# what is measured and which three principles WARN.

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent

if ($Compare.Count -gt 0) {
    if ($Compare.Count -ne 2) { Write-Host "-Compare takes exactly two report folders."; exit 1 }
    $compareArgs = @((Join-Path $PSScriptRoot "spell_feel.py"), "compare", $Compare[0], $Compare[1])
    if ($OutDir) { $compareArgs += @("--out", $OutDir) }
    & python @compareArgs
    exit $LASTEXITCODE
}

if (-not $Spell) { Write-Host "Usage: tools/spell_feel.ps1 -Spell <skills.json id> [-Versus treant,rat]"; exit 1 }
if (-not $OutDir) { $OutDir = Join-Path $Root "docs\captures\spell_feel" }

$status = 0
foreach ($enemy in $Versus) {
    $name = "${Spell}_vs_${enemy}"
    $frames = Join-Path $Root "tools\screenshots\spell_feel\$name"

    if (-not $ReportOnly) {
        $env:PP_RUNTIME_VERSUS = $enemy
        try {
            # The one test that records the frame series; the class's other
            # tests are pilot-only overlap checks that Ignore for any other spell.
            & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "screenshot.ps1") `
                -Runtime -Spell $Spell -RuntimeFilter "SpellRuntimeCaptureTests.TheWaterPilotRecordsAsAFrameSeries"
            if ($LASTEXITCODE -ne 0) { Write-Host "capture failed for $enemy"; $status = 1; continue }
        } finally {
            Remove-Item Env:\PP_RUNTIME_VERSUS -ErrorAction SilentlyContinue
        }

        if (Test-Path $frames) { Remove-Item $frames -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $frames | Out-Null
        Copy-Item (Join-Path $Root "tools\screenshots\runtime\spell_${Spell}_*") $frames
    }

    & python (Join-Path $PSScriptRoot "spell_feel.py") report --frames $frames --spell $Spell --versus $enemy `
        --out (Join-Path $OutDir $name) --residue-limit $ResidueLimit --accent-ratio $AccentRatio `
        --desync-frames $DesyncFrames
    if ($LASTEXITCODE -ne 0) { $status = 1 }
}
exit $status
