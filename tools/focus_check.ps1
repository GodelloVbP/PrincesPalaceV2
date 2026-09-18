param(
    [ValidateSet("Headless", "Runtime", "Both")]
    [string]$Mode = "Both",

    # A quick [D] or [U] class -- see tools/test.ps1 -List -- driven headless
    # through Start-UnityQuiet's -nographics path. Default matches the class
    # the focus-guard task itself was asked to prove clean against.
    [string]$HeadlessClass = "NavContextStackTests",

    # A PlayMode capture fixture driven through tools/screenshot.ps1 -Runtime,
    # which needs a real graphics device and so goes through Start-UnityQuiet's
    # WINDOWED path -- the one the guard actually has work to do on.
    [string]$RuntimeFilter = "DossierTooltipCaptureTests",

    [switch]$SkipSync
)

# tools/focus_check.ps1 -- proves, or disproves, that launching Unity from
# tools/ leaves the caller's foreground window alone.
#
# INDEPENDENT of Start-UnityQuiet's own guard in tools/unity_path.ps1 on
# purpose: this watches from OUTSIDE the launched process tree, with its own
# P/Invoke calls, so a bug in the guard being tested cannot also grade its own
# homework. It does not open Notepad or force anything to the foreground first
# -- it records whatever IS foreground when it starts, which is "whatever you
# were doing", and then watches whether that changes for reasons other than
# your own input while each launch runs.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/focus_check.ps1
#   ... -Mode Headless                     (just tools/test.ps1 <HeadlessClass>)
#   ... -Mode Runtime                      (just tools/screenshot.ps1 -Runtime -RuntimeFilter <RuntimeFilter>)
#   ... -HeadlessClass Wool -RuntimeFilter RuntimeScreenshotTests
#
# Pure ASCII, no BOM -- CLAUDE.md's PowerShell gotcha.

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent

if (-not ("PP.FocusCheck.NativeMethods" -as [type])) {
    Add-Type -Namespace PP.FocusCheck -Name NativeMethods -MemberDefinition @"
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
"@
}

function Get-WindowLabel {
    param([IntPtr]$Hwnd)

    if ($Hwnd -eq [IntPtr]::Zero) { return "(no foreground window)" }

    $sb = New-Object System.Text.StringBuilder 256
    [void][PP.FocusCheck.NativeMethods]::GetWindowText($Hwnd, $sb, 256)
    [uint32]$procId = 0
    [void][PP.FocusCheck.NativeMethods]::GetWindowThreadProcessId($Hwnd, [ref]$procId)
    $procName = "?"
    try { $procName = (Get-Process -Id $procId -ErrorAction Stop).ProcessName } catch {}

    return "hwnd=$Hwnd pid=$procId ($procName) title=`"$($sb.ToString())`""
}

# Runs one tools/ command line as a CHILD powershell.exe (so this script's own
# console is never what gets watched) and polls the foreground window every
# 200ms for exactly as long as that child process is alive -- covering the
# whole launch, not just the moment Unity itself is up.
function Watch-Launch {
    param([string]$Label, [string]$ArgumentLine)

    $before = [PP.FocusCheck.NativeMethods]::GetForegroundWindow()
    Write-Host ""
    Write-Host "=== $Label ==="
    Write-Host ("  {0:yyyy-MM-dd HH:mm:ss.fff}  foreground before: {1}" -f (Get-Date), (Get-WindowLabel $before))

    $proc = Start-Process -FilePath "powershell.exe" -ArgumentList $ArgumentLine -PassThru -NoNewWindow

    $changeLog = @()
    $last = $before
    while (-not $proc.HasExited) {
        $now = [PP.FocusCheck.NativeMethods]::GetForegroundWindow()
        if ($now -ne $last) {
            $entry = "{0:yyyy-MM-dd HH:mm:ss.fff}  {1}  ->  {2}" -f (Get-Date), (Get-WindowLabel $last), (Get-WindowLabel $now)
            Write-Host "  CHANGE: $entry"
            $changeLog += $entry
            $last = $now
        }
        Start-Sleep -Milliseconds 200
    }
    $proc.WaitForExit()

    $after = [PP.FocusCheck.NativeMethods]::GetForegroundWindow()
    Write-Host ("  {0:yyyy-MM-dd HH:mm:ss.fff}  foreground after:  {1}" -f (Get-Date), (Get-WindowLabel $after))

    if ($changeLog.Count -eq 0) {
        Write-Host "  RESULT: no foreground change at all during $Label."
        return $true
    }

    Write-Host "  RESULT: $($changeLog.Count) foreground change(s) logged above during $Label."
    if ($after -eq $before) {
        Write-Host "  foreground WAS restored to the window it started on -- a transient minimize/restore, not a steal."
        return $true
    }

    Write-Host "  FOREGROUND WAS NOT RESTORED by the end of $Label. This is the failure this script exists to catch."
    return $false
}

$ok = $true

if ($Mode -eq "Headless" -or $Mode -eq "Both") {
    $line = "-NoProfile -ExecutionPolicy Bypass -File `"$ProjectRoot\tools\test.ps1`" $HeadlessClass"
    if ($SkipSync) { $line += " -SkipSync" }
    $ok = (Watch-Launch -Label "headless: tools/test.ps1 $HeadlessClass" -ArgumentLine $line) -and $ok
}

if ($Mode -eq "Runtime" -or $Mode -eq "Both") {
    $line = "-NoProfile -ExecutionPolicy Bypass -File `"$ProjectRoot\tools\screenshot.ps1`" -Runtime -RuntimeFilter $RuntimeFilter"
    if ($SkipSync) { $line += " -SkipSync" }
    $ok = (Watch-Launch -Label "windowed: tools/screenshot.ps1 -Runtime -RuntimeFilter $RuntimeFilter" -ArgumentLine $line) -and $ok
}

Write-Host ""
if ($ok) {
    Write-Host "FOCUS CHECK: ok -- foreground was never left on something other than what it started on."
    exit 0
}

Write-Host "FOCUS CHECK: FAILED -- see FOREGROUND WAS NOT RESTORED above. Do not report the guard as fixed."
exit 1
