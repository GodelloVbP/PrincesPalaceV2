param(
    [ValidateSet("Headless", "Runtime", "Both", "Command")]
    [string]$Mode = "Both",

    # A quick [D] or [U] class -- see tools/test.ps1 -List -- driven headless
    # through Start-UnityQuiet's -nographics path. Default matches the class
    # the focus-guard task itself was asked to prove clean against.
    [string]$HeadlessClass = "NavContextStackTests",

    # A PlayMode capture fixture driven through tools/screenshot.ps1 -Runtime,
    # which needs a real graphics device and so goes through Start-UnityQuiet's
    # WINDOWED path -- the one the guard actually has work to do on.
    [string]$RuntimeFilter = "DossierTooltipCaptureTests",

    # Mode Command: an arbitrary child powershell.exe argument line, watched
    # the same way as the two built-in modes. Lets this script be pointed at
    # ANY tools/ entry point (run_tests_parallel.ps1, an area slice, ...)
    # without teaching it a new named mode every time one is needed.
    [string]$CommandLine,
    [string]$CommandLabel = "command",

    [switch]$SkipSync
)

# tools/focus_check.ps1 -- proves, or disproves, that launching Unity (or
# anything else tools/ starts) from tools/ leaves the caller's foreground
# window alone.
#
# INDEPENDENT of Start-UnityQuiet's own guard in tools/unity_path.ps1 on
# purpose: this watches from OUTSIDE the launched process tree, with its own
# P/Invoke calls, so a bug in the guard being tested cannot also grade its own
# homework. It does not open Notepad or force anything to the foreground first
# -- it records whatever IS foreground when it starts, which is "whatever you
# were doing", and then watches whether that changes for reasons other than
# your own input while each launch runs.
#
# EVERY foreground change is logged, not just the net result: timestamp, the
# new foreground window's pid, process name, PARENT pid and parent process
# name (Get-CimInstance Win32_Process, cached and refreshed periodically so a
# fast-lived process can still be identified after it exits), and window
# title. The 2026-09-18 owner report ("still does it") is exactly a case
# where the OLD version of this script -- pass/fail on whether foreground was
# restored by the end -- could have missed the culprit: it only proves
# something came back, never says what took it in the first place. This
# version's whole log is the evidence, whether the run passes or not.
#
# It also tracks the DESCENDANT PROCESS TREE of the child it launches (built
# the same way the process-tree focus guard in tools/unity_path.ps1 does, but
# reimplemented here rather than shared -- this script exists to catch a bug
# in that guard, so it must not depend on the guard's own tree-walking code
# being correct) and marks every logged window IN-TREE or OUT-OF-TREE. A
# steal from something OUTSIDE the launched tree (another app, a Windows
# notification) is not this script's or the guard's problem to fix; a steal
# from INSIDE it is exactly what tools/unity_path.ps1's Start-FocusGuard
# exists to stop.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/focus_check.ps1
#   ... -Mode Headless                     (just tools/test.ps1 <HeadlessClass>)
#   ... -Mode Runtime                      (just tools/screenshot.ps1 -Runtime -RuntimeFilter <RuntimeFilter>)
#   ... -HeadlessClass Wool -RuntimeFilter RuntimeScreenshotTests
#   ... -Mode Command -CommandLine '-NoProfile -ExecutionPolicy Bypass -File "..\run_tests_parallel.ps1"' -CommandLabel "full suite"
#
# TWO RESULTS, REPORTED SEPARATELY. "FOCUS CHECK" is whether the foreground
# window was left alone; "COMMAND" is whether the watched command itself
# succeeded (its exit code). They are independent -- a run that fails at once
# never gets far enough to steal focus, so a clean focus log over a failed
# command proves nothing about the guard -- and this used to print only the
# first: "FOCUS CHECK: ok", exit 0, over a child that had exited non-zero
# without launching anything. Exit code: the first failed command's own exit
# code; else 1 when focus was not restored; else 0.
#
# Pure ASCII, no BOM -- CLAUDE.md's PowerShell gotcha.

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent

if (-not ("PP.FocusCheck.NativeMethods" -as [type])) {
    Add-Type -Namespace PP.FocusCheck -Name NativeMethods -MemberDefinition @"
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
"@
}

# --- process-tree tracking, refreshed periodically --------------------------
#
# One bulk Win32_Process query (Get-CimInstance with no -Filter) rather than a
# per-event lookup: the whole point of this script is to catch changes that
# can happen inside a 200ms poll window, and a WMI round-trip per event is
# slower than that. Cached and rebuilt on a timer instead.
$script:ProcCache = @{}          # pid -> @{ Name; ParentId }
$script:LastProcRefresh = [DateTime]::MinValue

function Update-ProcCache {
    if (([DateTime]::Now - $script:LastProcRefresh).TotalMilliseconds -lt 750) { return }
    $script:LastProcRefresh = [DateTime]::Now
    $rows = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue
    $next = @{}
    foreach ($r in $rows) {
        $next[[int]$r.ProcessId] = @{ Name = $r.Name; ParentId = [int]$r.ParentProcessId }
    }
    $script:ProcCache = $next
}

function Get-ProcInfo {
    param([int]$ProcId)
    if ($script:ProcCache.ContainsKey($ProcId)) { return $script:ProcCache[$ProcId] }
    # Fallthrough for a pid that appeared since the last cache refresh: a
    # single targeted query rather than waiting for the next timer tick.
    $row = Get-CimInstance Win32_Process -Filter "ProcessId = $ProcId" -ErrorAction SilentlyContinue
    if ($row) {
        $info = @{ Name = $row.Name; ParentId = [int]$row.ParentProcessId }
        $script:ProcCache[$ProcId] = $info
        return $info
    }
    return @{ Name = "?"; ParentId = -1 }
}

# All pids descended from $RootPid, walking Update-ProcCache's snapshot. The
# root is included. Rebuilt from the cache on every call (cheap: a hashtable
# walk, no I/O) so it always reflects the latest refresh.
function Get-DescendantPids {
    param([int]$RootPid)

    $byParent = @{}
    foreach ($pid_ in $script:ProcCache.Keys) {
        $parentId = $script:ProcCache[$pid_].ParentId
        if (-not $byParent.ContainsKey($parentId)) { $byParent[$parentId] = New-Object System.Collections.Generic.List[int] }
        $byParent[$parentId].Add($pid_)
    }

    $result = New-Object System.Collections.Generic.HashSet[int]
    $queue = New-Object System.Collections.Generic.Queue[int]
    $queue.Enqueue($RootPid)
    while ($queue.Count -gt 0) {
        $cur = $queue.Dequeue()
        # $wasAdded captures HashSet.Add's bool return -- left inline in an
        # "if (-not ...)" it leaks onto the function's OUTPUT pipeline instead
        # of just driving the branch, and the caller ends up with an array of
        # stray booleans plus the HashSet rather than the HashSet alone.
        $wasAdded = $result.Add($cur)
        if (-not $wasAdded) { continue }
        if ($byParent.ContainsKey($cur)) {
            foreach ($child in $byParent[$cur]) { $queue.Enqueue($child) }
        }
    }
    # ",$result" (not "$result") -- PowerShell auto-enumerates a returned
    # collection onto the pipeline, so a HashSet with exactly one element
    # would otherwise come back to the caller as a bare Int32 instead of a
    # one-element HashSet, and $treePids.Contains(...) then throws "Int32
    # does not contain a method named Contains". Wrapping in a one-element
    # array is the standard idiom for handing back a collection intact.
    return ,$result
}


# Live ancestor walk from $ProcId up to $RootPid, rather than a membership
# test against the periodically-refreshed Get-DescendantPids snapshot. A
# process that spawned in the last ~1s (exactly the case for a just-launched
# Unity.exe) is not yet in that snapshot, and tagging it OUT-OF-TREE on that
# account would misreport a real culprit as unrelated background noise --
# which is precisely the kind of miss the 2026-09-18 "still does it" report
# says the previous, less detailed version of this script could produce.
# Get-ProcInfo already falls back to a live single-pid query on a cache miss,
# so this walk is correct even for a process younger than the last refresh.
function Test-IsDescendant {
    param([int]$ProcId, [int]$RootPid, [int]$MaxHops = 32)

    $cur = $ProcId
    for ($i = 0; $i -lt $MaxHops; $i++) {
        if ($cur -eq $RootPid) { return $true }
        if ($cur -le 0) { return $false }
        $info = Get-ProcInfo -ProcId $cur
        if ($info.ParentId -eq $cur) { return $false }   # defend against a bogus self-parent
        $cur = $info.ParentId
    }
    return $false
}

function Get-WindowDetail {
    param([IntPtr]$Hwnd)

    if ($Hwnd -eq [IntPtr]::Zero) {
        return [pscustomobject]@{ Hwnd = $Hwnd; ProcId = 0; ProcName = "(none)"; ParentId = -1; ParentName = "(none)"; Title = "" }
    }

    $sb = New-Object System.Text.StringBuilder 256
    [void][PP.FocusCheck.NativeMethods]::GetWindowText($Hwnd, $sb, 256)
    [uint32]$procId = 0
    [void][PP.FocusCheck.NativeMethods]::GetWindowThreadProcessId($Hwnd, [ref]$procId)

    Update-ProcCache
    $info = Get-ProcInfo -ProcId ([int]$procId)
    $parentInfo = if ($info.ParentId -ge 0) { Get-ProcInfo -ProcId $info.ParentId } else { @{ Name = "?" } }

    return [pscustomobject]@{
        Hwnd       = $Hwnd
        ProcId     = [int]$procId
        ProcName   = $info.Name
        ParentId   = $info.ParentId
        ParentName = $parentInfo.Name
        Title      = $sb.ToString()
    }
}

function Format-WindowDetail {
    param($Detail, [Nullable[bool]]$InTree = $null)

    $treeTag = ""
    if ($InTree -ne $null) { $treeTag = if ($InTree) { " [IN-TREE]" } else { " [OUT-OF-TREE]" } }
    return ("hwnd={0} pid={1} ({2}) parent-pid={3} ({4}) title=`"{5}`"{6}" -f `
        $Detail.Hwnd, $Detail.ProcId, $Detail.ProcName, $Detail.ParentId, $Detail.ParentName, $Detail.Title, $treeTag)
}

# One-shot inventory of every VISIBLE top-level window currently owned by a
# pid in $TreePids, whether or not it was ever foreground. A window that
# flashes into existence and back out between two 150ms polls without ever
# grabbing focus would otherwise leave no trace at all -- this is the backstop
# for that case, called periodically rather than only at the end so a
# short-lived window is not missed entirely.
function Get-TreeWindows {
    param([System.Collections.Generic.HashSet[int]]$TreePids)

    $found = New-Object System.Collections.Generic.List[object]
    $callback = {
        param([IntPtr]$hWnd, [IntPtr]$lParam)
        if (-not [PP.FocusCheck.NativeMethods]::IsWindowVisible($hWnd)) { return $true }
        [uint32]$procId = 0
        [void][PP.FocusCheck.NativeMethods]::GetWindowThreadProcessId($hWnd, [ref]$procId)
        if ($TreePids.Contains([int]$procId)) {
            $sb = New-Object System.Text.StringBuilder 256
            [void][PP.FocusCheck.NativeMethods]::GetWindowText($hWnd, $sb, 256)
            $cls = New-Object System.Text.StringBuilder 256
            [void][PP.FocusCheck.NativeMethods]::GetClassName($hWnd, $cls, 256)
            $script:EnumHits.Add([pscustomobject]@{ Hwnd = $hWnd; ProcId = [int]$procId; Title = $sb.ToString(); Class = $cls.ToString() })
        }
        return $true
    } -as [PP.FocusCheck.NativeMethods+EnumWindowsProc]

    $script:EnumHits = $found
    [void][PP.FocusCheck.NativeMethods]::EnumWindows($callback, [IntPtr]::Zero)
    # Same one-element-collection hazard as Get-DescendantPids -- see its
    # comment on "return ,$result".
    return ,$found
}

# Runs one tools/ command line as a CHILD powershell.exe (so this script's own
# console is never what gets watched) and polls the foreground window every
# 150ms for exactly as long as that child process is alive -- covering the
# whole launch, not just the moment Unity itself is up.
function Watch-Launch {
    param([string]$Label, [string]$ArgumentLine)

    $before = [PP.FocusCheck.NativeMethods]::GetForegroundWindow()
    $beforeDetail = Get-WindowDetail -Hwnd $before
    Write-Host ""
    Write-Host "=== $Label ==="
    Write-Host ("  {0:yyyy-MM-dd HH:mm:ss.fff}  foreground before: {1}" -f (Get-Date), (Format-WindowDetail $beforeDetail))

    Write-Host "  child: powershell.exe $ArgumentLine"
    $proc = Start-Process -FilePath "powershell.exe" -ArgumentList $ArgumentLine -PassThru -NoNewWindow
    # Touching .Handle now is what keeps .ExitCode readable after the child
    # exits: without an open handle PS 5.1's Start-Process -PassThru object
    # reports an EMPTY exit code once the process is gone.
    [void]$proc.Handle

    Update-ProcCache
    $treePids = Get-DescendantPids -RootPid $proc.Id

    $changeLog = @()
    $windowInventory = @{}   # hwnd(int64) -> detail, everything ever seen in-tree
    $last = $before
    $lastTreeRefresh = [DateTime]::MinValue
    $lastEnumScan = [DateTime]::MinValue

    while (-not $proc.HasExited) {
        # Descendant set drifts as children spawn/exit (robocopy, dotnet,
        # Unity's own child processes) -- refresh roughly once a second, not
        # every poll, so this stays cheap enough to run at 150ms resolution.
        if (([DateTime]::Now - $lastTreeRefresh).TotalMilliseconds -ge 1000) {
            Update-ProcCache
            $treePids = Get-DescendantPids -RootPid $proc.Id
            $lastTreeRefresh = [DateTime]::Now
        }

        $now = [PP.FocusCheck.NativeMethods]::GetForegroundWindow()
        if ($now -ne $last) {
            $detail = Get-WindowDetail -Hwnd $now
            $inTree = Test-IsDescendant -ProcId $detail.ProcId -RootPid $proc.Id
            $entry = "{0:yyyy-MM-dd HH:mm:ss.fff}  ->  {1}" -f (Get-Date), (Format-WindowDetail $detail $inTree)
            Write-Host "  CHANGE: $entry"
            $changeLog += $entry
            if ($inTree) { $windowInventory[[int64]$detail.Hwnd] = $detail }
            $last = $now
        }

        # Backstop for a window that opens and closes between two foreground
        # polls without ever becoming foreground at all (still worth knowing
        # it existed, even though it cannot have stolen focus).
        if (([DateTime]::Now - $lastEnumScan).TotalMilliseconds -ge 750) {
            foreach ($w in (Get-TreeWindows -TreePids $treePids)) {
                if (-not $windowInventory.ContainsKey([int64]$w.Hwnd)) {
                    $d = Get-WindowDetail -Hwnd $w.Hwnd
                    $windowInventory[[int64]$w.Hwnd] = $d
                    Write-Host ("  WINDOW SEEN (in-tree, not necessarily foreground): {0}  class={1}" -f (Format-WindowDetail $d $true), $w.Class)
                }
            }
            $lastEnumScan = [DateTime]::Now
        }

        Start-Sleep -Milliseconds 150
    }
    $proc.WaitForExit()
    $exitCode = $proc.ExitCode
    if ($null -eq $exitCode) { $exitCode = -1 }
    if ($exitCode -eq 0) {
        Write-Host "  COMMAND: $Label exited 0."
    } else {
        Write-Host "  COMMAND FAILED: $Label exited $exitCode. A command that failed early may never have launched what the focus result is about."
    }

    $after = [PP.FocusCheck.NativeMethods]::GetForegroundWindow()
    $afterDetail = Get-WindowDetail -Hwnd $after
    Write-Host ("  {0:yyyy-MM-dd HH:mm:ss.fff}  foreground after:  {1}" -f (Get-Date), (Format-WindowDetail $afterDetail))

    if ($windowInventory.Count -gt 0) {
        Write-Host "  IN-TREE windows observed during $Label ($($windowInventory.Count)):"
        foreach ($d in $windowInventory.Values) { Write-Host ("    {0}" -f (Format-WindowDetail $d $true)) }
    }

    $focusOk = $true
    if ($changeLog.Count -eq 0) {
        Write-Host "  RESULT: no foreground change at all during $Label."
    } else {
        Write-Host "  RESULT: $($changeLog.Count) foreground change(s) logged above during $Label."
        if ($after -eq $before) {
            Write-Host "  foreground WAS restored to the window it started on -- a transient minimize/restore, not a steal."
        } else {
            Write-Host "  FOREGROUND WAS NOT RESTORED by the end of $Label. This is the failure this script exists to catch."
            $focusOk = $false
        }
    }
    return [pscustomobject]@{ Label = $Label; FocusOk = $focusOk; ExitCode = [int]$exitCode }
}

$launches = @()

if ($Mode -eq "Headless" -or $Mode -eq "Both") {
    $line = "-NoProfile -ExecutionPolicy Bypass -File `"$ProjectRoot\tools\test.ps1`" $HeadlessClass"
    if ($SkipSync) { $line += " -SkipSync" }
    $launches += Watch-Launch -Label "headless: tools/test.ps1 $HeadlessClass" -ArgumentLine $line
}

if ($Mode -eq "Runtime" -or $Mode -eq "Both") {
    $line = "-NoProfile -ExecutionPolicy Bypass -File `"$ProjectRoot\tools\screenshot.ps1`" -Runtime -RuntimeFilter $RuntimeFilter"
    if ($SkipSync) { $line += " -SkipSync" }
    $launches += Watch-Launch -Label "windowed: tools/screenshot.ps1 -Runtime -RuntimeFilter $RuntimeFilter" -ArgumentLine $line
}

if ($Mode -eq "Command") {
    if (-not $CommandLine) {
        Write-Host "-Mode Command requires -CommandLine."
        exit 2
    }
    $launches += Watch-Launch -Label $CommandLabel -ArgumentLine $CommandLine
}

$focusFailed = @($launches | Where-Object { -not $_.FocusOk })
$commandFailed = @($launches | Where-Object { $_.ExitCode -ne 0 })

Write-Host ""
if ($focusFailed.Count -eq 0) {
    Write-Host "FOCUS CHECK: ok -- foreground was never left on something other than what it started on."
} else {
    Write-Host "FOCUS CHECK: FAILED -- see FOREGROUND WAS NOT RESTORED above. Do not report the guard as fixed."
}
if ($commandFailed.Count -eq 0) {
    Write-Host "COMMAND: ok -- every watched command exited 0."
} else {
    foreach ($c in $commandFailed) { Write-Host "COMMAND: FAILED -- $($c.Label) exited $($c.ExitCode)." }
}

if ($commandFailed.Count -gt 0) { exit $commandFailed[0].ExitCode }
if ($focusFailed.Count -gt 0) { exit 1 }
exit 0
