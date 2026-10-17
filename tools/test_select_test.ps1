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

Write-Host "changed lines ignore comments"
$oldSrc = "int a = 1;`n// was Time.timeScale = 0;`nint b = 2;`n"
$newSrc = "int a = 1;`n// now Time.timeScale = 1;`nint b = 3;`n"
$hits = @(Get-ChangedLines -Old $oldSrc -New $newSrc | Where-Object { $_ -match $GlobalStatePattern })
Assert-That "a comment line naming Time.timeScale is not a global-state write" ($hits.Count -eq 0)
$realSrc = "int a = 1;`nTime.timeScale = 0;`n"
$hits = @(Get-ChangedLines -Old "int a = 1;`n" -New $realSrc | Where-Object { $_ -match $GlobalStatePattern })
Assert-That "a real Time.timeScale write still is" ($hits.Count -eq 1)

Write-Host "repo readers"
$readers = @(Get-RepoReaderClasses -ClassesByFile $byFile)
Assert-That "RewardArtTests (File.Exists and an upward Directory walk) is a repo reader" ($readers -contains "RewardArtTests")

Write-Host "changed files with a non-ASCII path"
$tmpRepo = Join-Path ([IO.Path]::GetTempPath()) ("pp_changed_" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path (Join-Path $tmpRepo "Assets") | Out-Null
try {
    $accent = [string][char]0xE9
    & git -C $tmpRepo init -q 2>$null
    & git -C $tmpRepo -c user.name=t -c user.email=t@t commit -q --allow-empty -m base 2>$null
    [IO.File]::WriteAllText((Join-Path $tmpRepo ("Assets\caf" + $accent + ".cs")), "x")
    $names = @(Get-ChangedFiles -RepoRoot $tmpRepo)
    Assert-That "an untracked Assets file with an accent comes back unquoted" ($names.Count -eq 1 -and $names[0].StartsWith("Assets/caf"))
} finally {
    Remove-Item -LiteralPath $tmpRepo -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "a .meta is ignored only beside code or docs"
$p = "Assets/_Project"
$classMap = Get-TestClasses
foreach ($m in @("$p/Scripts/Core/Foo.cs.meta", "$p/Scripts/Core.meta", "docs/PLAN.md.meta", "$p/Art.meta")) {
    $r = Resolve-ChangedPaths -Paths @($m) -Classes $classMap
    Assert-That "$m is ignored" (@($r.Ignored).Count -eq 1)
}
$r = Resolve-ChangedPaths -Paths @("$p/Art/Processed/x.png.meta") -Classes $classMap
Assert-That "a sprite meta routes to the art area" (@($r.Ignored).Count -eq 0 -and $r.Areas -contains "art")
$r = Resolve-ChangedPaths -Paths @("$p/Fonts/F.ttf.meta") -Classes $classMap
Assert-That "a font meta routes to the ui area" (@($r.Ignored).Count -eq 0 -and $r.Areas -contains "ui")
$r = Resolve-ChangedPaths -Paths @("$p/Scripts/Core/PrincesPalace.Core.asmdef.meta") -Classes $classMap
Assert-That "an asmdef meta forces the full suite" (@($r.FullSuite).Count -eq 1)

if ($failures.Count -gt 0) { Write-Host "`n$($failures.Count) FAILED"; exit 1 }
Write-Host "`nall ok"
