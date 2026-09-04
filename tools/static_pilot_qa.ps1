# Photographs one beat of the Bog Witch's plain melee swing at NORMAL speed,
# copies the frames back to the main project, and assembles the strip and the
# real-time GIF that make the timing judgeable.
#
# A thin wrapper around tools/graphics_tests.ps1, naming
# StaticPilotStageCaptureTests and passing it a LABEL -- which is the whole
# reason this exists as a script rather than a command to retype. The
# static-combat pilot (docs/STATIC_COMBAT_ART_DEEP_DIVE.md) is a before/after
# comparison, so the same fixture has to be run twice against two folders and
# the second run must be byte-for-byte the same procedure as the first.
#
# THE REVIEW SPLIT: Claude reads strip.png and timing.json -- frame indices,
# the impact millisecond, whether anything moved. Only a human can judge
# whether playback.gif READS as a blow. One run produces both halves.
#
# Pure ASCII, no BOM -- CLAUDE.md's PowerShell gotcha applies here too.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/static_pilot_qa.ps1 -Label before
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/static_pilot_qa.ps1 -Label after
param([string]$Label = "unlabelled")

$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "graphics_tests.ps1") `
    -Filter PrincesPalace.PlayModeTests.StaticPilotStageCaptureTests `
    -Label $Label
$code = $LASTEXITCODE

# The test runs against the isolated -TestRunner copy (graphics_tests.ps1's own
# header says why), so Application.dataPath resolves THERE and so does the
# capture. Copied back the same way any PlayMode capture tool syncs its
# frames from the runner copy to main.
$project = Split-Path $PSScriptRoot -Parent
$runnerDir = (Split-Path $project -Parent) + "\" + (Split-Path $project -Leaf) +
             "-TestRunner\tools\screenshots\runtime\static_pilot\" + $Label
$mainRoot = Join-Path $project "tools\screenshots\runtime\static_pilot"
$mainDir = Join-Path $mainRoot $Label

if (-not (Test-Path $runnerDir)) {
    Write-Host "No frames at $runnerDir -- see the graphics_tests.ps1 output above for what went wrong."
    exit $code
}

# Stale-frame guard: this label's previous run must never be mistaken for this
# one. Only THIS label is cleared -- the other half of the comparison is the
# whole point and must survive.
if (Test-Path $mainDir) { Remove-Item $mainDir -Recurse -Force }
New-Item -ItemType Directory -Force $mainRoot | Out-Null
Copy-Item $runnerDir $mainDir -Recurse -Force
Write-Host "Frames copied to: $mainDir"

if ($code -ne 0) {
    Write-Host "StaticPilotStageCaptureTests failed -- skipping assembly over a possibly-incomplete capture."
    exit $code
}

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    Write-Host "No 'python' on PATH -- frames were captured and copied, but strip/GIF assembly needs it."
    exit 1
}

Write-Host ""
Write-Host "Assembling strip, GIF and (when both labels exist) the comparison..."
& python (Join-Path $PSScriptRoot "capture_strip.py") --root $mainRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Artifacts under: $mainRoot"
Write-Host "  $Label/strip.png            -- every frame, stamped with index and ms"
Write-Host "  $Label/playback.gif         -- watch this. Only a human can judge the timing."
Write-Host "  $Label/timing.json          -- impact frame, settle frame, travel peaks"
Write-Host "  before_vs_after.png         -- written once both labels have been captured"

exit 0
