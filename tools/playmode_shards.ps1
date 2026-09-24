# tools/playmode_shards.ps1 -- split the PlayMode suite across N runner copies.
#
# Dot-sourced by run_tests_parallel.ps1 (after test_areas.ps1). Never invoked
# directly. Pure ASCII, no BOM (CLAUDE.md's PowerShell gotcha).
#
# WHY. PlayMode was 338s in one Unity process while EditMode finished in 23s
# beside it, so the gate's wall clock WAS the PlayMode process. It is split by
# FIXTURE (test class), never by test: a fixture loads its scene once and
# shares it across its tests (docs/TESTING.md "Shared scenes in PlayMode"), so
# a fixture cut in two would pay its scene twice and, worse, run half its
# tests against a scene state they were never written for.
#
# CONTIGUOUS RANGES OF THE SUITE'S OWN ORDER, NOT A GREEDY SCATTER. Each shard
# runs one unbroken stretch of the fixture sequence the unsharded run uses
# (NUnit's: full name, ordinal, case-insensitive), with the cut points chosen
# to make the slowest shard as fast as possible. Within a shard every fixture
# therefore has exactly the predecessors it has in a single-process run, or
# none (the first fixture of a shard starts as cold as the suite's first).
#
# Longest-processing-time-first was built first and failed on its second run:
# it deals classes out by weight, so a shard's ORDER is a new permutation
# every time the timings move, and the suite has order-dependent leaks that
# the single-process order happens to heal. Proven, not guessed: 13
# FightBeatPhaseStanceTests/FightContactCueTests cases ("the beat never
# finished playing") fail deterministically in ONE process given shard 2's
# LPT order, and pass in the canonical one, where FightAfterTheEliteTests
# (which resets Time.timeScale in its SetUp) sits between the leak and them.
# A contiguous cut can never produce an order the unsharded gate has not
# already run green, so it cannot expose such a leak; fixing the leaks is
# test work, not this file's. The price is balance: cuts fall between
# fixtures, so the slowest shard is over the ideal by at most one fixture
# (the largest is ~17s) and usually by a second or two.
#
# WEIGHTS: the last run's per-fixture wall times ($ShardTimingsFile, written
# by Save-ShardTimings from every shard's test-profile-PlayMode.csv). A class
# with no recorded time (new, renamed) weighs the mean of the known ones.
# With no timings at all, by test count read off the class's file.

$ShardTimingsFile = Join-Path $PSScriptRoot ".test-timings.csv"

# Every PlayMode class that is a fixture. Shared/ is excluded: the structural
# gate refuses a testable file there, so what discovery finds in Shared/ are
# helpers (PlayModeTestProfiler, ScriptedBaseInput, ...), never suites.
function Get-PlayModeFixtureClasses {
    param([hashtable]$Index)
    return @($Index.Keys | Where-Object {
        $Index[$_].Platform -eq "PlayMode" -and $Index[$_].Area -ne $SharedFolder.ToLower()
    } | Sort-Object)
}

function Read-ShardTimings {
    $t = @{}
    if (-not (Test-Path $ShardTimingsFile)) { return $t }
    try {
        foreach ($row in (Import-Csv $ShardTimingsFile)) {
            if ($row.kind -ne "fixture" -or -not $row.fixture) { continue }
            $v = 0.0
            if ([double]::TryParse($row.wall_s, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$v)) {
                $t[$row.fixture] = $v
            }
        }
    } catch {
        Write-Host "  (timings file $ShardTimingsFile unreadable: $($_.Exception.Message) -- balancing by test count)"
        return @{}
    }
    return $t
}

# Test-count fallback: the test attributes in the class's file, split evenly
# across the classes that file declares. Rough, and only used with no timings.
function Get-ClassTestCounts {
    param([hashtable]$Index, [string[]]$Classes)
    $byFile = @{}
    foreach ($c in $Classes) {
        $rel = $Index[$c].RelPath
        if (-not $byFile.ContainsKey($rel)) { $byFile[$rel] = @() }
        $byFile[$rel] += $c
    }
    $counts = @{}
    foreach ($rel in $byFile.Keys) {
        $path = Join-Path $AreasProjectRoot $rel
        $n = 1
        if (Test-Path $path) {
            $n = [Math]::Max(1, [regex]::Matches((Get-Content $path -Raw), $TestAttrPattern).Count)
        }
        $share = [Math]::Max(1.0, $n / $byFile[$rel].Count)
        foreach ($c in $byFile[$rel]) { $counts[$c] = $share }
    }
    return $counts
}

# Returns @{ Source = "timings"|"test count"; Shards = @( @{ Index; Classes; Estimate } ) }
function Get-PlayModeShardPlan {
    param([hashtable]$Index, [string[]]$Classes, [int]$Shards)

    $timings = Read-ShardTimings
    $known = @($Classes | Where-Object { $timings.ContainsKey($_) })
    $weights = @{}
    $source = ""
    if ($known.Count -gt 0) {
        $mean = ($known | ForEach-Object { $timings[$_] } | Measure-Object -Average).Average
        foreach ($c in $Classes) { $weights[$c] = if ($timings.ContainsKey($c)) { $timings[$c] } else { $mean } }
        $source = "timings ($($known.Count)/$($Classes.Count) classes timed, $ShardTimingsFile)"
    } else {
        $weights = Get-ClassTestCounts -Index $Index -Classes $Classes
        $source = "test count (no timings file yet)"
    }

    $ordered = @(Get-ClassRunOrder -Index $Index -Classes $Classes)
    $w = @($ordered | ForEach-Object { [double]$weights[$_] })

    # The smallest per-shard capacity a left-to-right greedy fill can meet in
    # $Shards ranges, by bisection between the heaviest single fixture (no
    # cut can do better) and the whole suite (one range holds everything).
    $lo = ($w | Measure-Object -Maximum).Maximum
    $hi = ($w | Measure-Object -Sum).Sum
    for ($iter = 0; $iter -lt 60 -and ($hi - $lo) -gt 0.01; $iter++) {
        $cap = ($lo + $hi) / 2
        $ranges = 1; $acc = 0.0
        foreach ($x in $w) {
            if ($acc + $x -gt $cap) { $ranges++; $acc = $x } else { $acc += $x }
        }
        if ($ranges -le $Shards) { $hi = $cap } else { $lo = $cap }
    }

    $bins = @()
    for ($i = 1; $i -le $Shards; $i++) {
        $bins += [pscustomobject]@{ Index = $i; Classes = (New-Object System.Collections.Generic.List[string]); Estimate = 0.0 }
    }
    $b = 0
    for ($k = 0; $k -lt $ordered.Count; $k++) {
        # Cut when this fixture would overflow the range -- or when the
        # fixtures left are only just enough to give every later shard one.
        $left = $ordered.Count - $k
        $emptyAfter = $Shards - 1 - $b
        if ($b -lt $Shards - 1 -and $bins[$b].Classes.Count -gt 0 -and
            (($bins[$b].Estimate + $w[$k] -gt $hi) -or ($left -le $emptyAfter))) {
            $b++
        }
        $bins[$b].Classes.Add($ordered[$k])
        $bins[$b].Estimate += $w[$k]
    }
    return [pscustomobject]@{ Source = $source; Shards = $bins }
}

# The classes in the order a single unsharded run executes them: NUnit sorts
# by full name (namespace.class), ordinal, case-insensitive -- checked against
# a real test-results-PlayMode.xml, where DossierEquipTests (namespace
# PrincesPalace.Tests.PlayMode) runs after every PrincesPalace.PlayModeTests
# fixture and a culture sort would have put it among the Dossier* ones. The
# namespace is read off each class's file.
function Get-ClassRunOrder {
    param([hashtable]$Index, [string[]]$Classes)
    $nsByFile = @{}
    $keyed = foreach ($c in $Classes) {
        $rel = $Index[$c].RelPath
        if (-not $nsByFile.ContainsKey($rel)) {
            $path = Join-Path $AreasProjectRoot $rel
            $ns = ""
            if (Test-Path $path) {
                $m = [regex]::Match((Get-Content $path -Raw), '(?m)^\s*namespace\s+([\w.]+)')
                if ($m.Success) { $ns = $m.Groups[1].Value }
            }
            $nsByFile[$rel] = $ns
        }
        $full = if ($nsByFile[$rel]) { "$($nsByFile[$rel]).$c" } else { $c }
        [pscustomobject]@{ Class = $c; Full = $full }
    }
    $arr = @($keyed)
    $keys = [string[]]@($arr | ForEach-Object { $_.Full })
    $items = [string[]]@($arr | ForEach-Object { $_.Class })
    [Array]::Sort($keys, $items, [StringComparer]::OrdinalIgnoreCase)
    return $items
}

# The union of the shards must be exactly the class set, with no class in two
# shards. Returns the problems, named; empty means the partition is sound.
function Test-ShardPartition {
    param([string[]]$Classes, $Plan, [hashtable]$Index)
    $problems = @()
    $seen = @{}
    foreach ($s in $Plan.Shards) {
        foreach ($c in $s.Classes) {
            if ($seen.ContainsKey($c)) { $problems += "$c is in shard $($seen[$c]) AND shard $($s.Index)" }
            else { $seen[$c] = $s.Index }
        }
    }
    foreach ($c in $Classes) {
        if (-not $seen.ContainsKey($c)) { $problems += "$c is in no shard" }
    }
    foreach ($c in $seen.Keys) {
        if ($Classes -notcontains $c) { $problems += "$c is in shard $($seen[$c]) but is not a discovered PlayMode class" }
    }
    # The property the whole design leans on (see this file's header): the
    # shards, end to end, ARE the single-process run order.
    if ($problems.Count -eq 0 -and $Index) {
        $order = @(Get-ClassRunOrder -Index $Index -Classes $Classes)
        $joined = @($Plan.Shards | ForEach-Object { $_.Classes })
        for ($k = 0; $k -lt $order.Count; $k++) {
            if ($joined[$k] -ne $order[$k]) {
                $problems += "shards are not contiguous ranges of the run order: position $k is $($joined[$k]), the unsharded run has $($order[$k]) there"
                break
            }
        }
    }
    return $problems
}

# Unity's -testFilter, anchored on the class segment exactly the way
# tools/test.ps1 builds it: ".*\.(A|B|C)\..*" against namespace.class.method.
function Get-ShardFilter {
    param([string[]]$Classes)
    return ".*\.(" + (($Classes | ForEach-Object { [regex]::Escape($_) }) -join "|") + ")\..*"
}

# One test-run document out of N shard documents: counts summed, the start
# the earliest, the end the latest, every shard's top-level test-suite kept
# whole beneath it. Written to $OutPath so a reader of
# test-results-PlayMode.xml sees the whole suite, not one shard of it.
function Merge-ShardResults {
    param([string[]]$Paths, [string]$OutPath)

    $docs = @($Paths | ForEach-Object { [xml](Get-Content $_ -Raw) })
    $out = New-Object System.Xml.XmlDocument
    [void]$out.AppendChild($out.CreateXmlDeclaration("1.0", "utf-8", $null))
    $root = $out.CreateElement("test-run")
    [void]$out.AppendChild($root)

    $sum = @{}
    foreach ($a in @("testcasecount", "total", "passed", "failed", "inconclusive", "skipped", "asserts")) {
        $sum[$a] = 0
        foreach ($d in $docs) { $sum[$a] += [int]$d.'test-run'.GetAttribute($a) }
    }
    $first = $docs[0].'test-run'
    $starts = @($docs | ForEach-Object { $_.'test-run'.GetAttribute("start-time") } | Sort-Object)
    $ends = @($docs | ForEach-Object { $_.'test-run'.GetAttribute("end-time") } | Sort-Object)
    $durations = @($docs | ForEach-Object { [double]::Parse($_.'test-run'.GetAttribute("duration"), [Globalization.CultureInfo]::InvariantCulture) })
    $result = if ($sum["failed"] -gt 0) { "Failed" } else { $first.GetAttribute("result") }

    $root.SetAttribute("id", $first.GetAttribute("id"))
    $root.SetAttribute("testcasecount", $sum["testcasecount"])
    $root.SetAttribute("result", $result)
    foreach ($a in @("total", "passed", "failed", "inconclusive", "skipped", "asserts")) { $root.SetAttribute($a, $sum[$a]) }
    $root.SetAttribute("engine-version", $first.GetAttribute("engine-version"))
    $root.SetAttribute("clr-version", $first.GetAttribute("clr-version"))
    $root.SetAttribute("start-time", $starts[0])
    $root.SetAttribute("end-time", $ends[-1])
    # The LONGEST shard: the suite's wall time, which is what a single run's
    # duration meant. The shards' sum is CPU-ish time and is printed apart.
    $root.SetAttribute("duration", (($durations | Measure-Object -Maximum).Maximum).ToString([Globalization.CultureInfo]::InvariantCulture))
    $root.SetAttribute("shards", $docs.Count)

    for ($i = 0; $i -lt $docs.Count; $i++) {
        [void]$root.AppendChild($out.CreateComment(" shard $($i + 1): $($Paths[$i]) "))
        foreach ($suite in $docs[$i].'test-run'.SelectNodes("test-suite")) {
            [void]$root.AppendChild($out.ImportNode($suite, $true))
        }
    }
    $out.Save($OutPath)
    return [pscustomobject]$sum
}

# Post-run check on what the shards ACTUALLY ran, not what they were asked
# to: every expected class shows up as a fixture in exactly one shard, and no
# test case ran twice. A filter that silently selected nothing (or one class
# name that also matched a namespace segment) would otherwise pass green.
function Test-ShardCoverage {
    param([string[]]$Paths, [string[]]$Classes)
    $problems = @()
    $fixtureShard = @{}
    $caseSeen = @{}
    for ($i = 0; $i -lt $Paths.Count; $i++) {
        $doc = [xml](Get-Content $Paths[$i] -Raw)
        foreach ($s in $doc.SelectNodes("//test-suite[@type='TestFixture' or @type='ParameterizedFixture']")) {
            $cls = ($s.GetAttribute("classname") -split '\.')[-1]
            if (-not $cls) { continue }
            if ($fixtureShard.ContainsKey($cls) -and $fixtureShard[$cls] -ne ($i + 1)) {
                $problems += "fixture $cls ran in shard $($fixtureShard[$cls]) AND shard $($i + 1)"
            }
            $fixtureShard[$cls] = $i + 1
        }
        foreach ($tc in $doc.SelectNodes("//test-case")) {
            $fn = $tc.GetAttribute("fullname")
            if ($caseSeen.ContainsKey($fn)) { $problems += "test case $fn ran in shard $($caseSeen[$fn]) AND shard $($i + 1)" }
            else { $caseSeen[$fn] = $i + 1 }
        }
    }
    foreach ($c in $Classes) {
        if (-not $fixtureShard.ContainsKey($c)) { $problems += "class $c was assigned a shard but no shard's results contain it" }
    }
    return $problems
}

# Merges every shard's test-profile-PlayMode.csv into $ShardTimingsFile for the
# next run's balance. The profiler's own columns, plus which shard wrote the row.
function Save-ShardTimings {
    param([string[]]$CsvPaths)
    $rows = @()
    for ($i = 0; $i -lt $CsvPaths.Count; $i++) {
        if (-not (Test-Path $CsvPaths[$i])) { continue }
        foreach ($r in (Import-Csv $CsvPaths[$i])) {
            $r | Add-Member -NotePropertyName shard -NotePropertyValue ($i + 1) -Force
            $rows += $r
        }
    }
    if ($rows.Count -eq 0) { return 0 }
    $rows | Export-Csv -Path $ShardTimingsFile -NoTypeInformation -Encoding UTF8
    return @($rows | Where-Object { $_.kind -eq "fixture" }).Count
}
