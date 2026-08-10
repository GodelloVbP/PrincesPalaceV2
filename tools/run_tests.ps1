param(
    [switch]$SkipSync,
    [ValidateSet("PlayMode", "EditMode", "All")]
    [string]$Platform = "All"
)

# Runs the full test suite headlessly against an isolated copy of the project
# (Prince's Palace-TestRunner, sibling folder), so it never touches the
# lockfile of a live Editor session. Safe to run while Unity is open and in
# use on your screen at the same time.

$SourceProject = "C:\Games\Prince's Palace"
$TestProject = "C:\Games\Prince's Palace-TestRunner"
. (Join-Path $PSScriptRoot "unity_path.ps1")
$UnityExe = Get-UnityExe

if (-not $SkipSync) {
    Write-Host "Syncing Assets/Packages/ProjectSettings into the isolated test copy..."
    robocopy "$SourceProject\Assets" "$TestProject\Assets" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    robocopy "$SourceProject\Packages" "$TestProject\Packages" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    robocopy "$SourceProject\ProjectSettings" "$TestProject\ProjectSettings" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null

    # Diverge the product name so Application.persistentDataPath (and thus
    # save_slot_*.json) never overlaps with the real project's save data.
    $settingsPath = Join-Path $TestProject "ProjectSettings\ProjectSettings.asset"
    (Get-Content $settingsPath -Raw) -replace "productName: .*", "productName: PrincesPalaceTestRunner" |
        Set-Content $settingsPath -Encoding utf8
}

function Invoke-TestPlatform {
    param([string]$TestPlatform)

    $resultsPath = Join-Path $TestProject "test-results-$TestPlatform.xml"
    $logPath = Join-Path $TestProject "test-run-$TestPlatform.log"
    if (Test-Path $resultsPath) { Remove-Item $resultsPath -Force }

    Write-Host "`nRunning $TestPlatform tests..."
    $proc = Start-Process -FilePath $UnityExe -ArgumentList @(
        "-batchmode", "-nographics", "-silent-crashes",
        "-projectPath", "`"$TestProject`"",
        "-runTests", "-testPlatform", $TestPlatform,
        "-testResults", "`"$resultsPath`"",
        "-logFile", "`"$logPath`"",
        "-buildTarget", "StandaloneWindows64"
    ) -PassThru -Wait -NoNewWindow

    if (-not (Test-Path $resultsPath)) {
        Write-Host "No results file produced for $TestPlatform (Unity exit code $($proc.ExitCode)). Tail of log:"
        Get-Content $logPath -Tail 60 | ForEach-Object { Write-Host $_ }
        return $false
    }

    [xml]$results = Get-Content $resultsPath
    $root = $results.'test-run'
    Write-Host "$TestPlatform -- Total: $($root.total)  Passed: $($root.passed)  Failed: $($root.failed)  Skipped: $($root.skipped)  Duration: $($root.duration)s"

    $failures = $results.SelectNodes("//test-case[@result='Failed']")
    foreach ($f in $failures) {
        Write-Host "`nFAILED: $($f.fullname)"
        if ($f.failure -and $f.failure.message) {
            Write-Host $f.failure.message.InnerText
        }
    }

    return [int]$root.failed -eq 0
}

$platforms = if ($Platform -eq "All") { @("EditMode", "PlayMode") } else { @($Platform) }
$allPassed = $true
foreach ($p in $platforms) {
    $passed = Invoke-TestPlatform -TestPlatform $p
    $allPassed = $allPassed -and $passed
}

if ($allPassed) {
    Write-Host "`nAll tests passed."
    exit 0
} else {
    Write-Host "`nSome tests failed."
    exit 1
}
