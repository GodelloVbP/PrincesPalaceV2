# tools/unity_path_test.ps1 -- pins tools/unity_path.ps1 (the Library provenance
# guard, Set-ProjectProductName, Stop-UnityTree, accept_library.ps1, gif.ps1
# -Compare binding) and the runner claims of tools/unity_lock.ps1 it takes.
# Pure PowerShell, no Unity. Every case builds a fixture tree under a temp
# directory; nothing here points at a real project or Library.
# Run: powershell -File tools/unity_path_test.ps1   (exits 1 on any failure)
# Pure ASCII, no BOM.

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "unity_path.ps1")

$failures = @()
function Assert-That {
    param([string]$Name, [bool]$Ok)
    if ($Ok) { Write-Host "  ok    $Name" } else { Write-Host "  FAIL  $Name"; $script:failures += $Name }
}

$root = Join-Path ([System.IO.Path]::GetTempPath()) ("pp_unity_path_test_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$env:PP_RUNNER_WAIT_SECONDS = "1"

# $Leaf decides the class; $Stamp is $null for none. Library gets a marker file
# so a deletion is visible.
function New-Fixture {
    param([string]$Leaf, [string]$Pinned = "6000.5.7f1", [string]$Stamp)
    $p = Join-Path $root $Leaf
    New-Item -ItemType Directory -Force -Path (Join-Path $p "ProjectSettings"), (Join-Path $p "Library") | Out-Null
    Set-Content -LiteralPath (Join-Path $p "ProjectSettings\ProjectVersion.txt") -Value "m_EditorVersion: $Pinned" -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $p "Library\marker.txt") -Value "x" -Encoding ASCII
    if ($Stamp) { [void](Set-LibraryStamp -ProjectPath $p -EditorVersion $Stamp -Tool "fixture") }
    return $p
}
function Has-Library { param([string]$P) return (Test-Path -LiteralPath (Join-Path $P "Library\marker.txt")) }
function Try-Assert {
    param([string]$P)
    try { return @{ Result = (Assert-LibraryProvenance -ProjectPath $P -Tool "unity_path_test.ps1"); Error = $null } }
    catch { return @{ Result = $null; Error = $_.Exception.Message } }
}

try {
    Write-Host "classification"
    foreach ($leaf in @("PP-TestRunner", "PP-TestRunner2", "PP-Capture", "PP-Bot1", "PP-Bot12", "PP-u66", "PP-u66-TestRunner", "PP-u66-TestRunner3")) {
        Assert-That "$leaf is disposable" ((Get-UnityProjectClass -ProjectPath "C:\Games\$leaf") -eq "Disposable")
    }
    foreach ($leaf in @("PP", "PP\", "agent-fake", "TestRunnerNotes", "PP-u660", "PP-BotNotes")) {
        Assert-That "$leaf is a project tree" ((Get-UnityProjectClass -ProjectPath "C:\Games\$leaf") -eq "Project")
    }

    Write-Host "disposable copy"
    $p = New-Fixture -Leaf "A-TestRunner" -Stamp "6000.5.7f1"
    $r = Try-Assert $p
    Assert-That "matching stamp launches and keeps the Library" ($r.Result -eq "match" -and (Has-Library $p))

    $p = New-Fixture -Leaf "B-TestRunner" -Stamp "6000.5.4f1"
    $r = Try-Assert $p
    Assert-That "mismatching stamp deletes the Library and stamps the new one" ($r.Result -eq "deleted" -and -not (Has-Library $p) -and (Get-LibraryStamp -ProjectPath $p) -eq "6000.5.7f1")

    $p = New-Fixture -Leaf "C-Capture"
    $r = Try-Assert $p
    Assert-That "no stamp (unknown provenance) deletes the Library and stamps" ($r.Result -eq "deleted" -and -not (Has-Library $p) -and (Get-LibraryStamp -ProjectPath $p) -eq "6000.5.7f1")

    $p = New-Fixture -Leaf "D-u66-TestRunner2"
    Remove-Item -LiteralPath (Join-Path $p "Library") -Recurse -Force
    $r = Try-Assert $p
    Assert-That "no Library at all: Library created and stamped before launch" ($r.Result -eq "stamped" -and (Get-LibraryStamp -ProjectPath $p) -eq "6000.5.7f1")

    Write-Host "disposable copy, claims"
    $p = New-Fixture -Leaf "E-TestRunner"
    $claims = Enter-RunnerClaim -RunnerPaths @($p) -Tool "unity_path_test.ps1"
    $r = Try-Assert $p
    Exit-RunnerClaim -Claims $claims
    Assert-That "a claim this process already holds passes through and the Library is deleted" ($r.Result -eq "deleted" -and -not (Has-Library $p))

    $p = New-Fixture -Leaf "F-TestRunner"
    $r = Try-Assert $p
    Assert-That "an unclaimed copy is claimed, wiped and released" ($r.Result -eq "deleted" -and -not (Has-Library $p))
    $again = Enter-RunnerClaim -RunnerPaths @($p) -Tool "unity_path_test.ps1" -WaitSeconds 0
    Assert-That "the claim is released after the wipe" ($null -ne $again)
    Exit-RunnerClaim -Claims $again

    # A sibling process holds the claim: the wipe must not happen.
    $p = New-Fixture -Leaf "G-TestRunner"
    $ready = Join-Path $root "holder.ready"
    $holderScript = Join-Path $root "holder.ps1"
    Set-Content -LiteralPath $holderScript -Encoding ASCII -Value @(
        ('. "' + (Join-Path $PSScriptRoot "unity_lock.ps1") + '"'),
        ('$c = Enter-RunnerClaim -RunnerPaths @("' + $p + '") -Tool "holder" -WaitSeconds 0'),
        ('Set-Content -LiteralPath "' + $ready + '" -Value "go"'),
        'Start-Sleep -Seconds 60'
    )
    $holder = Start-Process -FilePath "powershell.exe" -ArgumentList @("-NoProfile", "-File", "`"$holderScript`"") -PassThru -WindowStyle Hidden
    try {
        $deadline = (Get-Date).AddSeconds(30)
        while (-not (Test-Path -LiteralPath $ready) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
        $r = Try-Assert $p
        Assert-That "a copy held by another process refuses the launch and keeps the Library" ($r.Error -match "another run holds" -and (Has-Library $p) -and $null -eq (Get-LibraryStamp -ProjectPath $p))
    } finally {
        if (-not $holder.HasExited) { $holder.Kill() }
        $holder.WaitForExit()
    }

    Write-Host "project tree"
    $p = New-Fixture -Leaf "Proj1" -Stamp "6000.5.7f1"
    $r = Try-Assert $p
    Assert-That "matching stamp launches" ($r.Result -eq "match" -and (Has-Library $p))

    $p = New-Fixture -Leaf "Proj2" -Stamp "6000.5.4f1"
    $r = Try-Assert $p
    Assert-That "mismatching stamp refuses, naming both versions, keeps the Library and the old stamp" ($r.Error -match "6000\.5\.4f1" -and $r.Error -match "6000\.5\.7f1" -and (Has-Library $p) -and (Get-LibraryStamp -ProjectPath $p) -eq "6000.5.4f1")

    $p = New-Fixture -Leaf "Proj3"
    $r = Try-Assert $p
    Assert-That "no stamp proceeds with the Library kept and marks it unknown, not the pinned version" ($r.Result -eq "unknown" -and $null -eq $r.Error -and (Has-Library $p) -and (Get-LibraryStamp -ProjectPath $p) -eq "unknown")
    $r = Try-Assert $p
    Assert-That "an unknown stamp keeps proceeding and stays unknown" ($r.Result -eq "unknown" -and $null -eq $r.Error -and (Has-Library $p) -and (Get-LibraryStamp -ProjectPath $p) -eq "unknown")

    Write-Host "stamp"
    $p = New-Fixture -Leaf "Proj4"
    Set-Content -LiteralPath (Get-LibraryStampPath -ProjectPath $p) -Value "{ not json" -Encoding ASCII
    Assert-That "an unreadable stamp reads as no stamp" ($null -eq (Get-LibraryStamp -ProjectPath $p))
    $noLib = Join-Path $root "NoLib"
    New-Item -ItemType Directory -Force -Path $noLib | Out-Null
    Set-LibraryStamp -ProjectPath $noLib -EditorVersion "1" -Tool "t"
    Assert-That "Set-LibraryStamp creates the Library a first import will fill" ((Get-LibraryStamp -ProjectPath $noLib) -eq "1")
    Assert-That "-projectPath argument is read unquoted" ((Get-UnityProjectArg -ArgumentList @("-batchmode", "-projectPath", "`"C:\a b\c`"", "-quit")) -eq "C:\a b\c")
    Assert-That "no -projectPath argument gives null" ($null -eq (Get-UnityProjectArg -ArgumentList @("-batchmode")))

    Write-Host "Set-ProjectProductName"
    $asset = Join-Path $root "Proj[x]\ProjectSettings.asset"
    [void][System.IO.Directory]::CreateDirectory((Split-Path $asset -Parent))
    [System.IO.File]::WriteAllText($asset, "a: 1`nproductName: Old`nb: 2`n", (New-Object System.Text.UTF8Encoding($false)))
    Set-ProjectProductName -SettingsPath $asset -Product "New"
    $bytes = [System.IO.File]::ReadAllBytes($asset)
    Assert-That "productName is rewritten in a path with brackets" ([System.IO.File]::ReadAllText($asset) -match "productName: New")
    Assert-That "the rewritten settings file has no BOM" (-not ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB))

    Write-Host "Stop-UnityTree"
    $parent = Start-Process -FilePath "cmd.exe" -ArgumentList @("/c", "ping -n 60 127.0.0.1 > nul") -PassThru -WindowStyle Hidden
    Start-Sleep -Milliseconds 800
    $kids = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$($parent.Id)")
    Stop-UnityTree -Proc $parent
    Start-Sleep -Milliseconds 500
    Assert-That "the launcher is gone" ($parent.HasExited)
    Assert-That "its child went with it" (@($kids | Where-Object { Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue }).Count -eq 0)

    # The launcher exits between the HasExited check and taskkill: taskkill then
    # writes "not found" to stderr, which is terminating under Stop.
    $ghost = [pscustomobject]@{ Id = 2147000000; HasExited = $false }
    $threw = $false
    try { Stop-UnityTree -Proc $ghost } catch { $threw = $true }
    Assert-That "a launcher that vanished before taskkill does not throw under Stop" (-not $threw)

    Write-Host "gif.ps1 -Compare binding"
    $out = (& powershell.exe -NoProfile -File (Join-Path $PSScriptRoot "gif.ps1") -Compare "nope_a" "nope_b" 2>&1 | Out-String)
    Assert-That "-Compare a b reaches the comparison" ($out -notmatch "takes two run directories")
    $out = (& powershell.exe -NoProfile -File (Join-Path $PSScriptRoot "gif.ps1") -Compare "nope_a,nope_b" 2>&1 | Out-String)
    Assert-That "-Compare a,b still reaches the comparison" ($out -notmatch "takes two run directories")
    $out = (& powershell.exe -NoProfile -File (Join-Path $PSScriptRoot "gif.ps1") -Compare "nope_a" 2>&1 | Out-String)
    Assert-That "-Compare with one run still refuses" ($out -match "takes two run directories")

    Write-Host "-AcceptLibrary"
    # A refusal writes to stderr, which Stop would turn into a terminating error here.
    $ErrorActionPreference = "Continue"
    $p = New-Fixture -Leaf "Proj7" -Pinned "6000.6.2f1" -Stamp "6000.5.7f1"
    $null = & powershell.exe -NoProfile -File (Join-Path $PSScriptRoot "accept_library.ps1") -AcceptLibrary $p 2>&1
    Assert-That "accept_library.ps1 rewrites the stamp to the pinned version" ($LASTEXITCODE -eq 0 -and (Get-LibraryStamp -ProjectPath $p) -eq "6000.6.2f1")
    $r = Try-Assert $p
    Assert-That "the accepted tree then launches" ($r.Result -eq "match")
    $d = New-Fixture -Leaf "Z-TestRunner" -Stamp "6000.5.4f1"
    $null = & powershell.exe -NoProfile -File (Join-Path $PSScriptRoot "accept_library.ps1") -AcceptLibrary $d 2>&1
    Assert-That "a disposable copy is refused and its stamp untouched" ($LASTEXITCODE -ne 0 -and (Get-LibraryStamp -ProjectPath $d) -eq "6000.5.4f1")
} finally {
    Remove-Item Env:\PP_RUNNER_WAIT_SECONDS -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) { Write-Host "`n$($failures.Count) FAILED"; exit 1 }
Write-Host "`nall passed"
