# Renders a rig's own clips to real PNGs under tools/screenshots/rigs/,
# so its motion can actually be LOOKED AT instead of guessed from
# hand-typed keyframe degrees and a hope that a screen recording agrees.
#
# A thin wrapper around tools/graphics_tests.ps1, naming
# RigCaptureTests.CaptureEveryRatClip specifically. WHY a PlayMode test
# and not an Editor -executeMethod batch script: SpriteSkin deformation is
# driven by an actual per-frame tick (DeformationManagerUpdater.LateUpdate),
# and a synchronous Edit-mode script never gives Unity's update loop a
# chance to run it between posing the bones and rendering -- see
# RigCaptureTests.cs's own header for the "disassembled rat" that produced.
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
# therefore this tool's output -- resolves THERE, not under the main
# project's own tools/ directory.
$project = Split-Path $PSScriptRoot -Parent
$runnerTools = (Split-Path $project -Parent) + "\" + (Split-Path $project -Leaf) + "-TestRunner\tools\screenshots\rigs"
if (Test-Path $runnerTools) {
    Write-Host "Frames written under: $runnerTools"
} else {
    Write-Host "No output directory -- see the graphics_tests.ps1 output above for what went wrong."
}

exit $code
