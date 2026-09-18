# tools/unity_path.ps1 -- resolves the Unity editor THIS project is pinned to,
# AND is the one seam every script in tools/ launches Unity.exe through.
#
# Dot-sourced by every script in tools/ that launches Unity --
# run_tests_parallel.ps1, test.ps1, screenshot.ps1, preview.ps1,
# build_content.ps1, graphics_tests.ps1 and bot.ps1. Never invoked directly.
#
# Pure ASCII, no BOM: CLAUDE.md's PowerShell gotcha applies here same as
# everywhere else in tools/ -- an em-dash inside a string breaks PS 5.1's parser
# and points the error at an unrelated line, so plain "--" throughout.
#
# WHY THIS EXISTS: all four scripts hardcoded
# "...\Hub\Editor\6000.5.4f1\Editor\Unity.exe". The moment the project was
# opened in a newer editor, ProjectVersion.txt moved and the scripts did not --
# so the harness would have driven the OLD editor against a project marked for
# the new one. Two editors taking turns on one project thrash Library and can
# silently downgrade assets. Reading the version the project actually declares
# makes that impossible rather than merely corrected once.
#
# $PSScriptRoot inside a DOT-SOURCED file resolves to THIS file's own folder in
# PowerShell 5.1, not the caller's -- so this computes its own paths.

$UnityPathProjectRoot = Split-Path $PSScriptRoot -Parent

function Get-UnityExe {
    param([string]$ProjectRoot = $UnityPathProjectRoot)

    $versionFile = Join-Path $ProjectRoot "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path $versionFile)) {
        throw "No ProjectVersion.txt at $versionFile - cannot tell which Unity editor this project wants."
    }

    $line = Get-Content $versionFile | Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
    if (-not $line) {
        throw "ProjectVersion.txt at $versionFile has no m_EditorVersion line."
    }

    $version = ($line -replace '^m_EditorVersion:\s*', '').Trim()
    $exe = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"

    if (-not (Test-Path $exe)) {
        # Names the version rather than just failing to find a file, because
        # "install 6000.5.7f1 via Unity Hub" is the actual fix and a missing-path
        # error does not say that.
        throw "This project is pinned to Unity $version but it is not installed at $exe. Install $version via Unity Hub, or open the project in an installed version to re-pin it."
    }

    return $exe
}

# --- Start-UnityQuiet: launch shape only, no watchdog of its own -------
#
# Owner complaint, verbatim: "during test suites it alt-tabs my laptop out of
# whatever I was doing ... and I would like for that to never happen ever
# again." Every tools/ script used to call Start-Process on Unity.exe
# directly, so the fix would otherwise have needed N separate edits, kept in
# sync by hand, forever. Get-UnityExe was already the one thing all seven
# launch sites dot-source, so Start-UnityQuiet lives beside it: route a launch
# through here and the fix applies with nothing left to remember.
#
# 2026-09-18 REVISION -- the original version of this function special-cased
# "-nographics" as needing no guard at all ("a launch whose -ArgumentList
# already contains it cannot create a window at all"), on the strength of the
# Unity 6000.x command-line reference documenting no flag that does better.
# The owner's report after that fix shipped -- "still does it" -- sent this
# back to be MEASURED instead of reasoned about, with tools/focus_check.ps1
# extended to log every foreground change during a real run_tests_parallel.ps1
# / test.ps1 pass. The measurement: batchmode Unity.exe WITH -nographics still
# creates a top-level window and wins the foreground for over a second while
# it boots, exactly as often as a windowed launch does. The reference was
# right that no LAUNCH FLAG suppresses this; it does not follow that nothing
# can, because the fix does not have to live in how Unity is started.
#
# So this function no longer tries to tell a headless launch apart from a
# windowed one, and it no longer runs its own watchdog. It always starts
# Unity minimized (belt) and leaves winning back any foreground it grabs
# anyway to Start-FocusGuard below (suspenders) -- ONE mechanism covering the
# whole calling script's process tree for its whole run, rather than one
# watchdog per launch that only ever knew about its own $proc.Id. A script
# that calls this function without having called Start-FocusGuard first is
# back to the pre-2026-09-18 unguarded behavior for as long as that window
# stays foreground -- every current caller in tools/ calls it, so don't add a
# new Unity launch site that skips it.
function Start-UnityQuiet {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )

    # -NoNewWindow (what every call used before 2026-09-08) only suppresses a
    # NEW CONSOLE window, which does nothing for Unity.exe (a GUI-subsystem
    # app, headless or not) -- its own window still opens and can still
    # activate, -nographics included. -WindowStyle Minimized is the flag that
    # actually applies to a GUI app's initial window.
    return (Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -PassThru -WindowStyle Minimized)
}

# --- Start-FocusGuard / Stop-FocusGuard: the process-TREE focus guard ------
#
# Call Start-FocusGuard once, near the top of a tools/ script, right after
# dot-sourcing this file -- then wrap the rest of the script's body in
# try { ... } finally { Stop-FocusGuard }. PowerShell's try/catch/finally do
# NOT introduce a new variable scope (only a function or script-block
# invocation does), so wrapping an existing script this way changes nothing
# about what its variables can see; it only guarantees Stop-FocusGuard runs
# on every exit path, including "exit N" from deep inside a function the
# script calls -- PowerShell's exit unwinds through enclosing finally blocks
# on its way out, same as a thrown error would.
#
# WHY A TREE GUARD, NOT ANOTHER PER-LAUNCH ONE: the 2026-09-18 measurement
# (see Start-UnityQuiet's header above) found the thief was Unity's OWN
# window despite -nographics, which the old per-launch watchdog could not
# have caught even if it ran unconditionally, because that watchdog only
# existed for the windowed branch. A guard scoped to the whole tree of the
# CALLING SCRIPT, rather than to one child process, covers that launch and
# every other one the script makes (robocopy, dotnet, a second Unity
# instance for the other test platform) with the same mechanism, and it does
# not care which of them turns out to be the culprit -- it does not need to
# know in advance. This SUPERSEDES the old per-launch watchdog rather than
# adding to it: one mechanism, not two.
#
# SAFETY PROPERTY: the guard only ever acts on a window whose owning process
# is a live descendant of the calling script's OWN pid ($PID at the moment
# Start-FocusGuard is called), reverified by a fresh process-table walk
# roughly once a second for as long as the guard runs. It never minimizes or
# touches a window outside that set, and it stops the moment the calling
# script's pid is gone even if Stop-FocusGuard is never reached (belt, for
# the same reason Start-UnityQuiet keeps its own minimized start: two
# independent ways to fail safe are better than one that has to be perfect).
$script:FocusGuardJob = $null

function Start-FocusGuard {
    if ($script:FocusGuardJob) {
        # Already running (a script called this twice, or a caller forgot to
        # pair it with Stop-FocusGuard on an earlier path) -- refuse to leak a
        # second watchdog rather than silently doubling up.
        return
    }

    if (-not ("PP.FocusGuardEntry.NativeMethods" -as [type])) {
        Add-Type -Namespace PP.FocusGuardEntry -Name NativeMethods -MemberDefinition @"
            [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
"@
    }

    # Recorded BEFORE anything in the script's own tree can boot -- this is
    # whatever the owner was doing a moment ago, and the whole point is to
    # hand it straight back.
    $prevForeground = [PP.FocusGuardEntry.NativeMethods]::GetForegroundWindow()
    $ownPid = $PID

    # The watchdog runs in its OWN process (Start-Job), so it re-declares the
    # P/Invoke signatures it needs rather than sharing this process's
    # AppDomain -- there is nothing to share across a process boundary.
    # Window handles (HWNDs) and process ids ARE valid across that boundary,
    # which is what makes passing them in as plain numbers work.
    $script:FocusGuardJob = Start-Job -ScriptBlock {
        param($RootPid, $PrevForegroundLong)

        # -TypeDefinition, not -MemberDefinition: a PROCESSENTRY32 struct
        # cannot be declared inside a -MemberDefinition class body, and the
        # toolhelp snapshot below needs one. Get-CimInstance Win32_Process
        # (used by the first version of this walk) goes over WMI/COM and
        # measured 100-300ms per call -- fine once a second, too slow to
        # afford on every 200ms poll, which is what let one of the 2026-09-18
        # measurements take 1.3s to recover a stolen foreground instead of
        # one poll interval. CreateToolhelp32Snapshot is a direct kernel call
        # over the whole process table, measured under 10ms, so the walk
        # below can now afford to run EVERY iteration instead of being
        # throttled to once a second with a slower live-walk fallback for the
        # gap in between -- one mechanism instead of two.
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

namespace PP.FocusGuardEntryJob {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct PROCESSENTRY32 {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    public static class NativeMethods {
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);
        [DllImport("kernel32.dll")] public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
        [DllImport("kernel32.dll")] public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr hObject);
    }
}
"@

        $prevForeground = [IntPtr]$PrevForegroundLong
        $SW_MINIMIZE = 6
        $VK_MENU = 0x12
        $KEYEVENTF_KEYUP = 0x2

        # $stolenFg is whatever window currently holds foreground WITH --
        # attaching to ITS thread while it still legitimately owns the
        # foreground lock is what makes SetForegroundWindow succeed
        # deterministically, rather than racing whatever window Windows' own
        # z-order heuristic would have picked once that window was already
        # minimized. Attach-restore-detach MUST happen before the minimize
        # call, not after -- reversed, the first attempt at this (in the old
        # per-launch watchdog this replaces) landed focus on a third window
        # for several seconds before the right one came back, because by the
        # time it ran, the thief no longer held the lock it was meant to
        # borrow.
        function Restore-Foreground([IntPtr]$hwnd, [IntPtr]$stolenFg) {
            [uint32]$stolenThreadProcId = 0
            $stolenThread = [PP.FocusGuardEntryJob.NativeMethods]::GetWindowThreadProcessId($stolenFg, [ref]$stolenThreadProcId)
            $myThread = [PP.FocusGuardEntryJob.NativeMethods]::GetCurrentThreadId()

            [PP.FocusGuardEntryJob.NativeMethods]::AttachThreadInput($myThread, $stolenThread, $true) | Out-Null
            $ok = [PP.FocusGuardEntryJob.NativeMethods]::SetForegroundWindow($hwnd)
            [PP.FocusGuardEntryJob.NativeMethods]::AttachThreadInput($myThread, $stolenThread, $false) | Out-Null
            if ($ok) { return }

            # AttachThreadInput refused too (rare -- seen only when the thief's
            # own thread has already lost the lock by the time this runs).
            # Fall back to the plain call, then the ALT-tap workaround that
            # relaxes the foreground lock for whichever thread sent it.
            if ([PP.FocusGuardEntryJob.NativeMethods]::SetForegroundWindow($hwnd)) { return }
            [PP.FocusGuardEntryJob.NativeMethods]::keybd_event($VK_MENU, 0, 0, [UIntPtr]::Zero)
            [PP.FocusGuardEntryJob.NativeMethods]::keybd_event($VK_MENU, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
            [PP.FocusGuardEntryJob.NativeMethods]::SetForegroundWindow($hwnd) | Out-Null
        }

        # Every descendant pid of $RootPid (the calling script's OWN
        # process), from ONE CreateToolhelp32Snapshot call -- rebuilt fresh on
        # EVERY iteration of the loop below, not throttled to once a second.
        # An earlier version of this walk used Get-CimInstance Win32_Process,
        # measured at 100-300ms per call over WMI/COM: cheap enough once a
        # second, too slow to afford every 200ms poll, and throttling it to
        # once a second was measured (2026-09-18) to leave a process that
        # spawned a window inside that same second uncaught for up to a full
        # second -- longer than one poll interval, which is the bar this
        # guard is held to. A toolhelp snapshot is a direct kernel call over
        # the whole process table, measured under 10ms, so a fresh one every
        # iteration is what actually closes that gap, and it does so with one
        # mechanism rather than a periodic scan plus a separate live-walk
        # fallback for the space in between.
        function Get-DescendantPids([int]$RootPidArg) {
            $TH32CS_SNAPPROCESS = 0x00000002

            $snap = [PP.FocusGuardEntryJob.NativeMethods]::CreateToolhelp32Snapshot($TH32CS_SNAPPROCESS, 0)
            if ($snap -eq [IntPtr]-1 -or $snap -eq [IntPtr]::Zero) {
                # No snapshot to walk -- still say the root itself is "in the
                # tree" (never touch anything else) rather than returning an
                # empty set that would make every window look out-of-tree.
                $fallback = New-Object System.Collections.Generic.HashSet[int]
                [void]$fallback.Add($RootPidArg)
                return ,$fallback
            }

            try {
                $byParent = @{}
                $entry = New-Object PP.FocusGuardEntryJob.PROCESSENTRY32
                # Marshal.SizeOf($entry), an INSTANCE, not Marshal.SizeOf(a
                # type literal) -- PowerShell's method dispatch resolved the
                # type-literal form to the SizeOf(object) overload and then
                # tried to marshal the Type OBJECT itself ("Type
                # 'System.RuntimeType' cannot be marshaled"), rather than the
                # struct type it names. Passing the already-constructed
                # instance sidesteps the overload ambiguity entirely.
                $entry.dwSize = [uint32][System.Runtime.InteropServices.Marshal]::SizeOf($entry)

                $ok = [PP.FocusGuardEntryJob.NativeMethods]::Process32First($snap, [ref]$entry)
                while ($ok) {
                    $childPid = [int]$entry.th32ProcessID
                    $parentPid = [int]$entry.th32ParentProcessID
                    if (-not $byParent.ContainsKey($parentPid)) { $byParent[$parentPid] = New-Object System.Collections.Generic.List[int] }
                    $byParent[$parentPid].Add($childPid)
                    $ok = [PP.FocusGuardEntryJob.NativeMethods]::Process32Next($snap, [ref]$entry)
                }
            } finally {
                [void][PP.FocusGuardEntryJob.NativeMethods]::CloseHandle($snap)
            }

            $result = New-Object System.Collections.Generic.HashSet[int]
            $queue = New-Object System.Collections.Generic.Queue[int]
            $queue.Enqueue($RootPidArg)
            while ($queue.Count -gt 0) {
                $cur = $queue.Dequeue()
                # $added captures HashSet.Add's bool return -- inlined into
                # the "if" it would leak onto this function's own output
                # pipeline (see the "return ,$result" note below for why that
                # is dangerous), and it is also what makes the BFS's
                # already-visited check work at all. An EARLIER version of
                # this function pre-added $RootPidArg to $result before this
                # loop even started (as a fallback value); that pre-add made
                # THIS line's Add-of-the-root return false on the very first
                # iteration ("already present"), which skipped straight past
                # expanding the root's own children and left every real
                # descendant undiscovered -- measured as descCount staying at
                # 1 for the whole life of a guarded process. The root must be
                # discovered BY the walk, not seeded ahead of it.
                $added = $result.Add($cur)
                if (-not $added) { continue }
                if ($byParent.ContainsKey($cur)) {
                    foreach ($child in $byParent[$cur]) { $queue.Enqueue($child) }
                }
            }
            # ",$result" (not "$result") -- PowerShell auto-enumerates a
            # returned collection onto the pipeline, so a HashSet with
            # exactly one element would otherwise come back as a bare Int32
            # instead of a one-element HashSet, and ".Contains(...)" below
            # would throw. Wrapping in a one-element array is the standard
            # idiom for handing back a collection intact.
            return ,$result
        }

        # For the ROOT SCRIPT's lifetime, and nothing else: the loop's only
        # exit is $RootPid no longer existing (the calling script's own
        # process, whether it got here via falling off the end, "exit N", or
        # an unhandled error), and every action inside it is gated on the
        # foreground window's pid being in the live descendant set -- a
        # window outside the tree is never touched, full stop.
        while ($true) {
            $root = Get-Process -Id $RootPid -ErrorAction SilentlyContinue
            if (-not $root) { break }

            $descendants = Get-DescendantPids $RootPid

            $fg = [PP.FocusGuardEntryJob.NativeMethods]::GetForegroundWindow()
            if ($fg -ne [IntPtr]::Zero) {
                [uint32]$fgPid = 0
                [PP.FocusGuardEntryJob.NativeMethods]::GetWindowThreadProcessId($fg, [ref]$fgPid) | Out-Null
                if ($descendants.Contains([int]$fgPid)) {
                    # ORDER MATTERS: restore focus to the recorded window
                    # WHILE the thief's window is still the one holding the
                    # foreground lock, THEN minimize the thief. See the
                    # header comment on Restore-Foreground above.
                    Restore-Foreground $prevForeground $fg
                    [PP.FocusGuardEntryJob.NativeMethods]::ShowWindow($fg, $SW_MINIMIZE) | Out-Null
                }
            }

            Start-Sleep -Milliseconds 200
        }
    # ([int64]$prevForeground), PARENTHESIZED -- this is not decoration.
    # Measured directly (2026-09-18): Windows PowerShell 5.1 mis-parses a bare
    # "-ArgumentList $a, [int64]$b" as command arguments rather than
    # expressions, and the cast token does not get APPLIED -- it gets
    # STRINGIFIED and prepended, so the job received the literal text
    # "[int64]1248206" instead of the number 1248206, and
    # "[IntPtr]$PrevForegroundLong" inside the job threw on its very first
    # line, every time, before the watchdog loop ever ran a single iteration.
    # This is the exact form the ORIGINAL per-launch watchdog used
    # (unparenthesized), which means that watchdog never worked either --
    # nothing ever surfaced the failure because Start-UnityQuiet discarded the
    # job object without checking its state. Wrapping the cast in parens
    # forces it to be evaluated as an expression before Start-Job ever sees
    # the result, which -- verified with tools/focus_check.ps1 driving a
    # WinForms test window through this exact function -- is what actually
    # makes the watchdog loop run.
    } -ArgumentList $ownPid, ([int64]$prevForeground)
}

function Stop-FocusGuard {
    if (-not $script:FocusGuardJob) { return }
    Stop-Job -Job $script:FocusGuardJob -ErrorAction SilentlyContinue
    Remove-Job -Job $script:FocusGuardJob -Force -ErrorAction SilentlyContinue
    $script:FocusGuardJob = $null
}
