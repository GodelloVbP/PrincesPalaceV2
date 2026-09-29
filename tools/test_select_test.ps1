# tools/test_select_test.ps1 -- pins the commit gate's selection rules.
#
# Pure PowerShell, no Unity, no git history: every case reads the working tree
# through the same graph the gate uses. Run: powershell -File tools/test_select_test.ps1
# Exits 1 on any failed expectation. Pure ASCII, no BOM.

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "test_areas.ps1")
. (Join-Path $PSScriptRoot "playmode_shards.ps1")
. (Join-Path $PSScriptRoot "test_select.ps1")

$scripts = "Assets/_Project/Scripts"
$g = Get-RefGraph
$index = Get-TestIndex
$byFile = Get-ClassesByFile -Index $index
$failures = @()

function Assert-That {
    param([string]$Name, [bool]$Ok)
    if ($Ok) { Write-Host "  ok    $Name" } else { Write-Host "  FAIL  $Name"; $script:failures += $Name }
}

# The classes the gate would pick for a change to $Path: the seeds it declares
# plus the engine objects they reach, one hop out, exactly as Resolve-GateSelection.
function Get-SelectedClasses {
    param([string]$Path)
    $fi = $g.IndexOf[$Path]
    $seeds = @($g.Declares[$fi])
    $seeds = @($seeds + @(Get-FacadeSeeds -Graph $g -Seeds $seeds -Path $Path) | Sort-Object -Unique)
    $closure = $g.Close([string[]]$seeds, [int[]]@($fi), $SelectDepth)
    $out = @()
    foreach ($f in @($closure.Depth.Keys)) {
        $rel = $g.Rel[$f]
        if ($byFile.ContainsKey($rel)) { $out += $byFile[$rel] }
    }
    return @($out | Sort-Object -Unique)
}

Write-Host "scope keys"
Assert-That "a file in Domain/Combat and one in Domain/Combat/Session share a scope" `
    ((Get-ScopeKey -Rel "$scripts/Domain/Combat/CritRules.cs") -eq (Get-ScopeKey -Rel "$scripts/Domain/Combat/Session/DamagePipeline.cs"))
Assert-That "Domain/Stats shares the Domain/Combat engine scope" `
    ((Get-ScopeKey -Rel "$scripts/Domain/Combat/CritRules.cs") -eq (Get-ScopeKey -Rel "$scripts/Domain/Stats/StatBlock.cs"))
Assert-That "Domain/Dungeon is still its own scope" `
    ((Get-ScopeKey -Rel "$scripts/Domain/Combat/CritRules.cs") -ne (Get-ScopeKey -Rel "$scripts/Domain/Dungeon/DifficultyCurve.cs"))

Write-Host "damage path reaches the fixtures that pin seeded fight numbers"
$journey = @("JourneyFightRoundTests", "JourneyFightRoundMouseTests")
foreach ($p in @("Domain/Combat/Session/DamagePipeline.cs", "Domain/Combat/CritRules.cs", "Domain/Stats/StatBlock.cs")) {
    $sel = Get-SelectedClasses -Path "$scripts/$p"
    foreach ($c in $journey) { Assert-That "$p selects $c" ($sel -contains $c) }
}

Write-Host "the rule does not collapse into everything"
$deep = @(Get-FacadeSeeds -Graph $g -Seeds @($g.Declares[$g.IndexOf["$scripts/Domain/Dungeon/DifficultyCurve.cs"]]) -Path "$scripts/Domain/Dungeon/DifficultyCurve.cs")
Assert-That "a Domain/Dungeon file promotes no Combat engine object" (-not ($deep -contains "FightSession"))
$sel = Get-SelectedClasses -Path "$scripts/Domain/Dungeon/DifficultyCurve.cs"
Assert-That "a Domain/Dungeon change selects fewer than 100 classes (got $($sel.Count))" ($sel.Count -lt 100)

if ($failures.Count -gt 0) { Write-Host "`n$($failures.Count) FAILED"; exit 1 }
Write-Host "`nall ok"
