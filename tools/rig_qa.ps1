# Renders a rig's own clips to real PNGs under tools/screenshots/rigs/,
# copies them back to the main project, then assembles what they're
# actually FOR: a GIF per stance, a contact strip, an onion skin, and the
# same churn/redraw metrics the frame-sheet pipeline already trusts -- so
# the motion can be LOOKED AT instead of guessed from hand-typed keyframe
# degrees and a hope that a screen recording agrees.
#
# A thin wrapper around tools/graphics_tests.ps1, naming
# RigCaptureTests.CaptureEveryRatClip specifically. WHY a PlayMode test
# and not an Editor -executeMethod batch script: SpriteSkin deformation is
# driven by an actual per-frame tick (DeformationManagerUpdater.LateUpdate),
# and a synchronous Edit-mode script never gives Unity's update loop a
# chance to run it between posing the bones and rendering -- see
# RigCaptureTests.cs's own header for the "disassembled rat" that produced.
#
# THE REVIEW SPLIT this loop is built around: Claude reads the contact
# strips, the onion skins and the churn/redraw numbers -- all of it is
# pixels-and-text, no motion perception required. Only a human can judge
# whether a GIF's TIMING reads as breathing or as a drunk sway; that
# judgment is not automatable and this script does not try. One run
# produces both halves.
#
# Pure ASCII, no BOM -- CLAUDE.md's PowerShell gotcha applies here too.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/rig_qa.ps1

$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "graphics_tests.ps1") -Filter PrincesPalace.PlayModeTests.RigCaptureTests
$code = $LASTEXITCODE

# The test runs against the isolated -TestRunner copy (see
# graphics_tests.ps1's own header for why), so Application.dataPath -- and
# therefore RigCaptureTests' own output -- resolves THERE, not under the
# main project's tools/ directory. Copy it back, same pattern
# tools/screenshot.ps1 uses for its -Runtime capture (that one is a flat
# Copy-Item; this tree is nested <root>/<id>/<stance>/, so -Recurse).
$project = Split-Path $PSScriptRoot -Parent
$runnerRigs = (Split-Path $project -Parent) + "\" + (Split-Path $project -Leaf) + "-TestRunner\tools\screenshots\rigs"
$mainRigs = Join-Path $project "tools\screenshots\rigs"

if (-not (Test-Path $runnerRigs)) {
    Write-Host "No output directory -- see the graphics_tests.ps1 output above for what went wrong."
    exit $code
}

# Stale-output guard: a leftover GIF from a previous rig, or a previous run
# of THIS rig before a content edit, must never be mistaken for this run's.
if (Test-Path $mainRigs) { Remove-Item $mainRigs -Recurse -Force }
Copy-Item $runnerRigs $mainRigs -Recurse -Force
Write-Host "Frames copied to: $mainRigs"

if ($code -ne 0) {
    Write-Host "RigCaptureTests failed -- skipping GIF assembly and QA over a possibly-incomplete capture."
    exit $code
}

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    Write-Host "No 'python' on PATH -- frames were captured and copied, but GIF/QA assembly needs it."
    exit 1
}

Write-Host ""
Write-Host "Assembling GIFs, strips and onion skins..."
& python (Join-Path $PSScriptRoot "rig_clip_qa.py") --root $mainRigs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Churn/redraw metrics + contact sheets..."
foreach ($root in (Get-ChildItem $mainRigs -Directory)) {
    & python (Join-Path $PSScriptRoot "actor_stance_qa.py") `
        --report $root.FullName `
        --out-dir (Join-Path $mainRigs "actor_qa") `
        --no-guides
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host ""
Write-Host "Artifacts under: $mainRigs"
Write-Host "  <id>/<stance>/<stance>.gif        -- watch this. Only a human can judge the timing."
Write-Host "  <id>/<stance>/<stance>_frames.png -- contact strip"
Write-Host "  <id>/<stance>/<stance>_onion.png  -- silhouette drift/spread at a glance"
Write-Host "  actor_qa/<id>.png                 -- churn/redraw metrics, same gate the frame-sheet roster uses"

exit 0
