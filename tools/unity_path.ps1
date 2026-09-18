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

# --- the focus guard ---------------------------------------------------
#
# Owner complaint, verbatim: "during test suites it alt-tabs my laptop out of
# whatever I was doing ... and I would like for that to never happen ever
# again." Every tools/ script used to call Start-Process on Unity.exe
# directly, so the fix would otherwise have needed N separate edits, kept in
# sync by hand, forever. Get-UnityExe was already the one thing all seven
# launch sites dot-source, so Start-UnityQuiet lives beside it: route a launch
# through here and the fix applies with nothing left to remember.
#
# Two layers:
#
#   (a) Headless whenever pixels are not needed. Checked the Unity 6000.x
#       Editor command-line reference for anything better than this: there is
#       no flag that keeps a batchmode window from being createable besides
#       -nographics itself, and no flag documented anywhere that stops a
#       batchmode-WITHOUT--nographics window from being activated once a
#       graphics device exists (-silent-crashes only suppresses the crash
#       dialog). So -nographics is the whole of layer (a): a launch whose
#       -ArgumentList already contains it cannot create a window at all, and
#       passes straight through with the exact Start-Process shape every
#       caller used before this existed.
#
#   (b) A foreground guard for the launches that DO need a real window (a
#       runtime/graphics capture with a device to read pixels back from, or
#       the interactive Editor behind -Launch): record whatever window is
#       foreground right now, start Unity minimized, then run a watchdog for
#       as long as that process lives. Whenever Unity's own window becomes
#       foreground, the watchdog minimizes it again and hands focus back to
#       whatever the recording caught -- via SetForegroundWindow first, then
#       (Windows' foreground lock refusing that from a background process) a
#       synthetic ALT tap, then AttachThreadInput, in that order, stopping at
#       the first that works. The watchdog only ever touches $proc.Id's own
#       window and stops the moment that process exits.
function Start-UnityQuiet {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )

    # HEADLESS: no window, nothing to guard. Identical to what every call
    # site did before this function existed.
    if ($ArgumentList -contains "-nographics") {
        return (Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -PassThru -NoNewWindow)
    }

    if (-not ("PP.FocusGuard.NativeMethods" -as [type])) {
        Add-Type -Namespace PP.FocusGuard -Name NativeMethods -MemberDefinition @"
            [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
"@
    }

    # Recorded BEFORE Unity boots -- this is whatever the owner was doing a
    # moment ago, and the whole point is to hand it straight back.
    $prevForeground = [PP.FocusGuard.NativeMethods]::GetForegroundWindow()

    # Started minimized rather than with the -NoNewWindow every headless
    # call uses: -NoNewWindow only suppresses a NEW CONSOLE window, which
    # does nothing for Unity.exe (a GUI-subsystem app) -- its own window
    # still opens and can still activate. -WindowStyle Minimized is the flag
    # that actually applies to a GUI app's initial window; the watchdog below
    # is the backstop for the window Unity opens (or re-shows) afterwards.
    $proc = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -PassThru -WindowStyle Minimized

    # The watchdog runs in its OWN process (Start-Job), so it re-declares the
    # P/Invoke signatures it needs rather than sharing this process's AppDomain
    # -- there is nothing to share across a process boundary. Window handles
    # (HWNDs) and process ids ARE valid across that boundary, which is what
    # makes passing them in as plain numbers work.
    Start-Job -ScriptBlock {
        param($TargetPid, $PrevForegroundLong)

        Add-Type -Namespace PP.FocusGuardJob -Name NativeMethods -MemberDefinition @"
            [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
            [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
            [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
            [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
            [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
            [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
            [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
"@

        $prevForeground = [IntPtr]$PrevForegroundLong
        $SW_MINIMIZE = 6
        $VK_MENU = 0x12
        $KEYEVENTF_KEYUP = 0x2

        # $UnityFg is whatever window Unity currently holds foreground WITH --
        # attaching to ITS thread while it still legitimately owns the
        # foreground lock is what makes SetForegroundWindow succeed
        # deterministically, rather than racing whatever window Windows'
        # own z-order heuristic would have picked once Unity's window was
        # already minimized. Attach-restore-detach MUST happen before the
        # minimize call, not after -- reversed, the first attempt here
        # landed focus on a third window for several seconds before the
        # right one came back, because by the time it ran, Unity no longer
        # held the lock it was meant to borrow.
        function Restore-Foreground([IntPtr]$hwnd, [IntPtr]$unityFg) {
            [uint32]$unityThreadProcId = 0
            $unityThread = [PP.FocusGuardJob.NativeMethods]::GetWindowThreadProcessId($unityFg, [ref]$unityThreadProcId)
            $myThread = [PP.FocusGuardJob.NativeMethods]::GetCurrentThreadId()

            [PP.FocusGuardJob.NativeMethods]::AttachThreadInput($myThread, $unityThread, $true) | Out-Null
            $ok = [PP.FocusGuardJob.NativeMethods]::SetForegroundWindow($hwnd)
            [PP.FocusGuardJob.NativeMethods]::AttachThreadInput($myThread, $unityThread, $false) | Out-Null
            if ($ok) { return }

            # AttachThreadInput refused too (rare -- seen only when Unity's
            # own thread has already lost the lock by the time this runs).
            # Fall back to the plain call, then the ALT-tap workaround that
            # relaxes the foreground lock for whichever thread sent it.
            if ([PP.FocusGuardJob.NativeMethods]::SetForegroundWindow($hwnd)) { return }
            [PP.FocusGuardJob.NativeMethods]::keybd_event($VK_MENU, 0, 0, [UIntPtr]::Zero)
            [PP.FocusGuardJob.NativeMethods]::keybd_event($VK_MENU, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
            [PP.FocusGuardJob.NativeMethods]::SetForegroundWindow($hwnd) | Out-Null
        }

        # For the process's LIFETIME, and NOTHING else: the loop's only exit
        # is $TargetPid no longer existing, and every action inside it is
        # gated on the foreground window belonging to that exact pid -- a
        # window that is not Unity's is never touched.
        while ($true) {
            $target = Get-Process -Id $TargetPid -ErrorAction SilentlyContinue
            if (-not $target) { break }

            $fg = [PP.FocusGuardJob.NativeMethods]::GetForegroundWindow()
            if ($fg -ne [IntPtr]::Zero) {
                [uint32]$fgPid = 0
                [PP.FocusGuardJob.NativeMethods]::GetWindowThreadProcessId($fg, [ref]$fgPid) | Out-Null
                if ($fgPid -eq $TargetPid) {
                    # ORDER MATTERS: restore focus to the recorded window
                    # WHILE Unity's window is still the one holding the
                    # foreground lock, THEN minimize Unity. See the header
                    # comment on Restore-Foreground above.
                    Restore-Foreground $prevForeground $fg
                    [PP.FocusGuardJob.NativeMethods]::ShowWindow($fg, $SW_MINIMIZE) | Out-Null
                }
            }

            Start-Sleep -Milliseconds 250
        }
    } -ArgumentList $proc.Id, [int64]$prevForeground | Out-Null

    return $proc
}
