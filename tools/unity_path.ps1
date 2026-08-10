# tools/unity_path.ps1 -- resolves the Unity editor THIS project is pinned to.
#
# Dot-sourced by run_tests.ps1, run_tests_parallel.ps1, test.ps1 and
# screenshot.ps1. Never invoked directly.
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
