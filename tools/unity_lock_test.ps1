# tools/unity_lock_test.ps1 -- pins Test-UnityHubProcess in tools/unity_lock.ps1.
# Pure PowerShell, no Unity. Run: powershell -File tools/unity_lock_test.ps1
# Pure ASCII, no BOM.

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "unity_lock.ps1")

$failures = @()
function Assert-That {
    param([string]$Name, [bool]$Ok)
    if ($Ok) { Write-Host "  ok    $Name" } else { Write-Host "  FAIL  $Name"; $script:failures += $Name }
}

$hub = 'C:\Program Files\Unity Hub\resources\unity.exe'
$editor = 'C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe'

Assert-That "Hub serve by path is the Hub" (Test-UnityHubProcess -ExecutablePath $hub -CommandLine ('"' + $hub + '" serve'))
Assert-That "Editor under Hub\Editor is not the Hub" (-not (Test-UnityHubProcess -ExecutablePath $editor -CommandLine ('"' + $editor + '"')))
Assert-That "Editor with no project path is not the Hub" (-not (Test-UnityHubProcess -ExecutablePath $editor -CommandLine ''))
Assert-That "unreadable path, serve argument is the Hub" (Test-UnityHubProcess -ExecutablePath '' -CommandLine '"unity.exe" serve')
Assert-That "unreadable path, no serve is not the Hub" (-not (Test-UnityHubProcess -ExecutablePath '' -CommandLine '"Unity.exe" -batchmode'))

if ($failures.Count -gt 0) { Write-Host "$($failures.Count) failed"; exit 1 }
Write-Host "all passed"
