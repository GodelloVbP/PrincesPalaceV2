# tools/runner_sync_test.ps1 -- pins tools/runner_sync.ps1. Pure PowerShell, no
# Unity, no real project. Run: powershell -File tools/runner_sync_test.ps1
# (exits 1 on any failure). Pure ASCII, no BOM.

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "runner_sync.ps1")

$failures = @()
function Assert-That {
    param([string]$Name, [bool]$Ok)
    if ($Ok) { Write-Host "  ok    $Name" } else { Write-Host "  FAIL  $Name"; $script:failures += $Name }
}

Write-Host "Invoke-PerRunner"
# A -Changed slice with no PlayMode class leaves only the EditMode copy, so the
# secondary list handed to the fan-out is empty.
$threw = $false
$res = $null
try { $res = Invoke-PerRunner -Runners @() -Script { param($r, $s) 1 } } catch { $threw = $true }
Assert-That "an empty runner list is a no-op, not a binding error" (-not $threw -and @($res).Count -eq 0)

$res = Invoke-PerRunner -Runners @(@{ Path = "a" }, @{ Path = "b" }) -Script { param($r, $s) "$($r.Path)$($s.Tag)" } -Shared @{ Tag = "!" }
Assert-That "each runner's result comes back in order" (@($res).Count -eq 2 -and $res[0] -eq "a!" -and $res[1] -eq "b!")

if ($failures.Count -gt 0) { Write-Host "`n$($failures.Count) FAILED"; exit 1 }
Write-Host "`nall passed"
