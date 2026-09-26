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
# windowed one for the RESTORE side -- Start-FocusGuard below still covers the
# whole calling script's process tree for its whole run, one mechanism instead
# of a watchdog per launch. But a -nographics launch has a second, stronger
# option a windowed one does not: it needs no real display, so it can run on a
# Windows desktop object OTHER than the interactive one, where there is
# nothing for its window to steal focus FROM in the first place -- restoring
# focus fast is no longer good enough if the owner can still see the flicker,
# and putting the window somewhere the owner is not looking removes the
# flicker rather than shortening it. See Get-HeadlessDesktop below for the
# mechanism and its fallback.
function Start-UnityQuiet {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )

    if ($ArgumentList -contains "-nographics" -or (Test-GraphicsOnHiddenDesktop -ArgumentList $ArgumentList)) {
        $desktopName = Get-HeadlessDesktop
        if ($desktopName) {
            $proc = Start-ProcessOnDesktop -FilePath $FilePath -ArgumentList $ArgumentList -Desktop $desktopName
            if ($proc) { return $proc }
            Write-Host "Start-UnityQuiet: launch on '$desktopName' did not come up -- falling back to the minimized launch on the interactive desktop for this call."
        }
    }

    # -NoNewWindow (what every call used before 2026-09-08) only suppresses a
    # NEW CONSOLE window, which does nothing for Unity.exe (a GUI-subsystem
    # app, headless or not) -- its own window still opens and can still
    # activate, -nographics included. -WindowStyle Minimized is the flag that
    # actually applies to a GUI app's initial window. This is the fallback
    # path (no real display, or the headless desktop above could not be
    # created/used) -- Start-FocusGuard is what keeps IT from stealing focus.
    return (Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -PassThru -WindowStyle Minimized)
}

# --- Graphics launches on the hidden desktop (AUDIT #204) -------------------
#
# A BATCHMODE launch WITHOUT -nographics (screenshot.ps1, graphics_tests.ps1,
# preview.ps1's picture route) is a capture: it renders to a render texture
# and reads the pixels back, and nobody needs to see its window. Whether such
# a launch goes to the hidden desktop is decided here, once, for every
# capture tool, by PP_GRAPHICS_DESKTOP:
#
#   unset / anything else -> WinSta0\PPHeadless, like -nographics launches.
#   "visible" -> the opt-out: the old minimized launch on the owner's
#                desktop, with Start-FocusGuard handing focus back (measured
#                2026-09-26: Unity held the foreground 359ms before the
#                guard won). For a machine where the hidden desktop gets no
#                graphics device -- the captures then Assert.Ignore "No
#                graphics device" and the tools exit 1 with no pictures.
#
# THE DEFAULT IS HIDDEN BECAUSE IT WAS MEASURED, 2026-09-26, on this
# machine (RTX 4060 laptop, D3D12): screenshot.ps1 -Runtime and preview.ps1
# -Spell (the graphics_tests.ps1 route) both ran on PPHeadless under
# focus_check.ps1 -Mode Command with ZERO foreground changes, an independent
# EnumWindows probe found no Unity window on the interactive desktop at all
# (the visible control run had its UnityContainerWndClass window there), the
# Unity log reported "Direct3D 12 [level 12.2]" on the real GPU, and the
# frames matched the visible run's to under 1/255 mean difference on a
# static screen. If CanvasCapture.IsSupported is false there, the captures
# Ignore and the tools report no pictures and exit 1; a device that exists
# but renders black was not observed and is not guarded against beyond
# looking at the frames.
#
# An ENVIRONMENT VARIABLE rather than a parameter on each script because the
# capture tools call each other as child `powershell -File` processes
# (preview.ps1 -> graphics_tests.ps1), which inherit the environment and
# nothing else; one variable reaches every launch without threading a switch
# through three scripts.
#
# NEVER a non-batchmode launch: preview.ps1 -Launch opens the interactive
# Editor for the author to play a fight in, and on a desktop nobody can see
# that Editor would be useless. That window is the one launch that is
# supposed to be seen.
function Test-GraphicsOnHiddenDesktop {
    param([string[]]$ArgumentList)

    if (-not ($ArgumentList -contains "-batchmode")) { return $false }
    if ($ArgumentList -contains "-nographics") { return $false }

    $choice = "$env:PP_GRAPHICS_DESKTOP".Trim().ToLowerInvariant()
    return ($choice -ne "visible")
}

# --- Headless desktop: give -nographics Unity nowhere to steal focus FROM --
#
# A Windows window station (WinSta0 for the interactive session) can hold
# more than one desktop object. Every window belongs to exactly one desktop,
# and only the window station's currently-ACTIVE desktop is ever what the
# user sees or can be alt-tabbed into -- a window created on a different
# desktop in the same station cannot become foreground on the real screen at
# all, whether or not it tries. That is the actual fix for "batchmode Unity
# still flickers the foreground for a second even with -nographics and even
# with a 200ms restore guard": stop restoring focus fast, and instead never
# hand batchmode Unity a desktop the owner is looking at. -nographics is what
# makes this available -- no real display is needed, so which desktop the
# process's (invisible, D3D-less) window lives on has no visible-output cost.
# (Written 2026-09-18 assuming graphics captures needed the interactive
# desktop's device; measured false on 2026-09-26 -- see
# Test-GraphicsOnHiddenDesktop above. Batchmode graphics launches now come
# here too.)
#
# One desktop object, named so a second concurrent tools/ session (see
# docs/WORKFLOW.md's parallel-session rules) finds the SAME one rather than
# creating a competing object: CreateDesktop returns a handle to an existing
# desktop of the given name instead of erroring, so two sessions sharing this
# machine share this desktop safely -- each holds its own handle, and one
# session's Close-HeadlessDesktop does not touch the other's launches, which
# hold their own reference via their own child processes.
#
# Created LAZILY and ONCE PER SCRIPT RUN, not once per launch -- several
# tools/ scripts launch Unity more than once in a run (run_tests_parallel.ps1's
# two platforms, bot.ps1's shards), and there is no reason to repeat a
# CreateDesktop call, or repeat logging its failure, for each one.
# $script:HeadlessDesktopAttempted, not just checking the handle for null,
# is what makes a FAILED attempt sticky too -- a desktop that could not be
# created a moment ago will not succeed on the next launch in the same run
# either, so retrying per-launch would only repeat the same failure and spam
# the same warning N times.
$script:HeadlessDesktopHandle = $null
$script:HeadlessDesktopAttempted = $false
$script:HeadlessDesktopName = "PPHeadless"

if (-not ("PP.HeadlessDesktop.NativeMethods" -as [type])) {
    Add-Type -Namespace PP.HeadlessDesktop -Name NativeMethods -MemberDefinition @"
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr CreateDesktop(string lpszDesktop, IntPtr lpszDevice, IntPtr pDevmode, uint dwFlags, uint dwDesiredAccess, IntPtr lpsa);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool CloseDesktop(IntPtr hDesktop);
"@
}

# STARTUPINFO/PROCESS_INFORMATION/CreateProcess -- kept in a separate
# -TypeDefinition block (not -MemberDefinition, which cannot declare a
# struct type) the same way Start-FocusGuard's own job script block declares
# PROCESSENTRY32 below. System.Diagnostics.Process/Start-Process has no way
# to set a child's desktop -- that field only exists on the raw Win32
# STARTUPINFO CreateProcess takes, so this is the one launch site in tools/
# that has to drop to CreateProcess directly instead.
#
# THE WHOLE CALL LIVES INSIDE THIS COMPILED TYPE, not just the DllImport
# declarations -- measured (2026-09-18), not assumed. Building the STARTUPINFO
# with `New-Object`, mutating it from PowerShell, and passing it across a
# `[ref]` to CreateProcess reliably came back Win32 error 123
# (ERROR_INVALID_NAME) even for the simplest possible call ("cmd.exe" /c "exit
# 0", no desktop set at all). Moving the identical struct construction and the
# same CreateProcess call inside a compiled C# method and only crossing the
# PowerShell/.NET boundary with a plain string in and a small result object
# out made the exact same call succeed every time. This is a known rough edge
# in PowerShell's interop with mutable-struct-by-reference P/Invoke signatures
# (the struct's string fields need marshaling PowerShell's own dynamic
# invocation does not reproduce correctly), not a bug in the call shape
# itself -- so DesktopLauncher.Launch below is the actual seam, and
# Start-ProcessOnDesktop is a thin PowerShell wrapper around it that never
# touches STARTUPINFO directly.
#
# SECOND, SEPARATE PITFALL, ALSO MEASURED: wrapping the launched pid with
# Process.GetProcessById(pid) -- what the resulting object was ORIGINALLY
# built with here -- does return a real Process object, and .Id/.HasExited/
# .Refresh()/.WaitForExit() all work on it. But .ExitCode does not: .NET
# Framework's Process.ExitCode throws "Process was not started by this
# object, so requested information cannot be determined" for any Process
# obtained via GetProcessById rather than Process.Start(), because it gates
# on a private `associated` flag that only Start() sets. bot.ps1 reads
# .ExitCode on exactly this kind of object (its shard error report), so this
# is not a corner this project can leave broken. The fix is
# AdoptProcess below: it calls the same two private methods
# (SetProcessHandle/SetProcessId) that Process.Start() itself calls right
# after ITS OWN internal CreateProcess call, on a blank `new Process()`,
# using a Microsoft.Win32.SafeHandles.SafeProcessHandle built from our raw
# handle. The result is indistinguishable from one Start() would have
# produced -- Associated included -- so .ExitCode works, and SafeHandle's own
# finalizer (ownsHandle: true) closes the raw handle when the Process object
# is garbage collected, so this file has no handles of its own left to track
# or close.
if (-not ("PP.HeadlessDesktop.DesktopLauncher" -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PP.HeadlessDesktop {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct STARTUPINFO {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    internal static class ProcessNativeMethods {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool CreateProcess(
            string lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        internal static extern bool CloseHandle(IntPtr hObject);
    }

    // Plain data PowerShell reads back with ordinary property access -- no
    // struct, no [ref], nothing PowerShell's marshaling has to reproduce.
    public class DesktopLaunchResult {
        public bool Success;
        public Process Process;
        public int Win32Error;
    }

    public static class DesktopLauncher {
        public static DesktopLaunchResult Launch(string commandLine, string desktop)
        {
            var result = new DesktopLaunchResult();

            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
            si.lpDesktop = desktop;
            si.dwFlags = 0x00000001;   // STARTF_USESHOWWINDOW
            si.wShowWindow = 0;        // SW_HIDE -- never displayed regardless, belt and suspenders

            const uint CREATE_NO_WINDOW = 0x08000000;
            var cmdLineBuffer = new StringBuilder(commandLine, 32768);

            PROCESS_INFORMATION pi;
            bool ok = ProcessNativeMethods.CreateProcess(
                null, cmdLineBuffer, IntPtr.Zero, IntPtr.Zero, false,
                CREATE_NO_WINDOW, IntPtr.Zero, null, ref si, out pi);

            if (!ok) {
                result.Success = false;
                result.Win32Error = Marshal.GetLastWin32Error();
                return result;
            }

            ProcessNativeMethods.CloseHandle(pi.hThread);
            result.Success = true;
            result.Process = AdoptProcess(pi.dwProcessId, pi.hProcess);
            return result;
        }

        // See this Add-Type block's header comment for why this exists:
        // Process.GetProcessById(pid) leaves .ExitCode permanently broken.
        // This reproduces exactly what Process.Start() does internally after
        // its own CreateProcess call, so the result is Associated the same
        // way, with no functional gap versus a normally-started process.
        private static Process AdoptProcess(int pid, IntPtr rawHandle)
        {
            var handle = new SafeProcessHandle(rawHandle, true);
            var process = new Process();
            var setHandle = typeof(Process).GetMethod("SetProcessHandle", BindingFlags.NonPublic | BindingFlags.Instance);
            var setId = typeof(Process).GetMethod("SetProcessId", BindingFlags.NonPublic | BindingFlags.Instance);
            setHandle.Invoke(process, new object[] { handle });
            setId.Invoke(process, new object[] { pid });
            return process;
        }
    }
}
"@
}

# Returns "WinSta0\PPHeadless" once the desktop object exists (creating it on
# the first call), or $null if it could not be created -- callers treat $null
# as "fall back to the minimized interactive-desktop launch", never as a
# reason to stop the launch entirely. GENERIC_ALL, not a hand-picked list of
# the individual DESKTOP_* rights: desktop objects define a GENERIC_MAPPING
# the same way most securable objects do, and GENERIC_ALL is what every other
# example of creating a desktop for a child process to run on uses.
function Get-HeadlessDesktop {
    if ($script:HeadlessDesktopAttempted) {
        if ($script:HeadlessDesktopHandle) { return "WinSta0\$($script:HeadlessDesktopName)" }
        return $null
    }
    $script:HeadlessDesktopAttempted = $true

    $GENERIC_ALL = 0x10000000
    $handle = [PP.HeadlessDesktop.NativeMethods]::CreateDesktop($script:HeadlessDesktopName, [IntPtr]::Zero, [IntPtr]::Zero, 0, $GENERIC_ALL, [IntPtr]::Zero)
    if ($handle -eq [IntPtr]::Zero) {
        $err = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
        Write-Host "Start-UnityQuiet: CreateDesktop('$($script:HeadlessDesktopName)') failed (Win32 error $err) -- headless launches this run will use the minimized interactive-desktop fallback instead."
        return $null
    }

    $script:HeadlessDesktopHandle = $handle
    return "WinSta0\$($script:HeadlessDesktopName)"
}

function Close-HeadlessDesktop {
    if ($script:HeadlessDesktopHandle) {
        [void][PP.HeadlessDesktop.NativeMethods]::CloseDesktop($script:HeadlessDesktopHandle)
        $script:HeadlessDesktopHandle = $null
    }
}

# Start-ProcessOnDesktop -- thin PowerShell wrapper around
# DesktopLauncher.Launch. Every current caller of Start-UnityQuiet uses the
# returned object as a System.Diagnostics.Process: .Id, .HasExited,
# .Refresh(), .WaitForExit()/.WaitForExit(ms), .ExitCode, and piping it
# through the Wait-Process cmdlet (which specifically requires a real Process
# instance, not a duck-typed lookalike -- ruling out a custom wrapper object
# as a fix for anything below). DesktopLauncher.Launch's AdoptProcess already
# produces exactly that, fully functional, so there is nothing left to adapt
# here beyond building the command line and surfacing a failure.
function Start-ProcessOnDesktop {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList,
        [Parameter(Mandatory = $true)][string]$Desktop
    )

    # Start-Process -ArgumentList (string[]) joins with a single space to
    # build ProcessStartInfo.Arguments before handing off to CreateProcess
    # itself -- every ArgumentList built in tools/ already double-quotes its
    # own path-bearing elements for exactly that reason, so reproducing the
    # same join here reconstructs the identical command line CreateProcess
    # would otherwise have been given via Start-Process.
    $cmdLine = "`"$FilePath`" " + ($ArgumentList -join " ")

    $result = [PP.HeadlessDesktop.DesktopLauncher]::Launch($cmdLine, $Desktop)
    if (-not $result.Success) {
        Write-Host "Start-UnityQuiet: CreateProcess on '$Desktop' failed (Win32 error $($result.Win32Error))."
        return $null
    }

    return $result.Process
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
    if ($script:FocusGuardJob) {
        Stop-Job -Job $script:FocusGuardJob -ErrorAction SilentlyContinue
        Remove-Job -Job $script:FocusGuardJob -Force -ErrorAction SilentlyContinue
        $script:FocusGuardJob = $null
    }
    # Always attempted, even when the job above was never started -- a script
    # that called Get-HeadlessDesktop (via Start-UnityQuiet) without ever
    # calling Start-FocusGuard would otherwise leak the handle. Every current
    # caller pairs the two, but this does not rely on that staying true.
    # No-op when nothing was ever created (Close-HeadlessDesktop checks the
    # handle itself).
    Close-HeadlessDesktop
}
