# tools/temp_paths.ps1 -- one naming rule for the scratch files tools/ drops
# in %TEMP%, keyed so two checkouts of this project cannot collide.
#
# Dot-sourced by test.ps1 and graphics_tests.ps1. Never invoked directly.
# Pure ASCII, no BOM: CLAUDE.md's PowerShell gotcha applies here same as
# everywhere else in tools/ -- an em-dash inside a string breaks PS 5.1's
# parser and points the error at an unrelated line, so plain "--" throughout.
#
# WHY THIS EXISTS. tools/test.ps1 wrote its dotnet transcript to a fixed
# %TEMP%\domain-tests-run.log. That is fine for one checkout and wrong the
# moment there are two: this repo is worked in git worktrees under
# .claude/worktrees/, and %TEMP% is per USER, not per tree. Two runs at once
# then fight over one file, and the loser either fails to open it at all or
# -- much worse -- reads the WINNER's transcript and reports another tree's
# failure as its own. Both halves were observed on 2026-09-07: one agent read
# another's failure as its own, and the run that provoked this fix died on
# "the process cannot access the file ... because it is being used by another
# process" while a second worktree held the handle.
#
# THE KEY IS THE PROJECT ROOT, not the branch and not the invocation. Two runs
# in the SAME tree still collide, and should: they are already fighting over
# tools/domain-tests/obj and the -TestRunner copies, so one shared log is the
# least of their problems and a per-invocation name would only hide that.
# What has to be separated is what genuinely IS separate on disk.
#
# LEAF PLUS HASH, not hash alone, so a human looking at %TEMP% can tell whose
# log is whose without running anything. The hash is what makes it unique (two
# worktrees can share a leaf name under different parents); the leaf is what
# makes it readable. MD5 is a filename discriminator here and nothing more --
# it is not standing in for a security property.
#
# $PSScriptRoot inside a DOT-SOURCED file resolves to THIS file's own folder
# in PowerShell 5.1, not the caller's -- so this computes its own paths and
# callers pass nothing. Same note, and the same reason, as test_areas.ps1.

$ProjectTempRoot = Split-Path $PSScriptRoot -Parent

$ProjectTempNormalized = $ProjectTempRoot.TrimEnd('\', '/').ToLowerInvariant()
$ProjectTempMd5 = [System.Security.Cryptography.MD5]::Create()
try {
    $ProjectTempHash = ($ProjectTempMd5.ComputeHash(
        [System.Text.Encoding]::UTF8.GetBytes($ProjectTempNormalized)) |
        ForEach-Object { $_.ToString("x2") }) -join ""
} finally {
    $ProjectTempMd5.Dispose()
}

# Anything unsafe in a Windows file name becomes an underscore -- this
# project's own folder is "Prince's Palace-v2", and an apostrophe in a path
# that later gets interpolated into a quoted string is a trap worth not
# setting.
$ProjectTempLeaf = (Split-Path $ProjectTempRoot -Leaf) -replace '[^A-Za-z0-9._-]', '_'

$ProjectTempTag = "$ProjectTempLeaf-" + $ProjectTempHash.Substring(0, 8)

# Takes the name the file WOULD have had and returns the keyed path:
#
#     Get-ProjectTempPath "domain-tests-run.log"
#       -> %TEMP%\domain-tests-run.Prince_s_Palace-v2-1a2b3c4d.log
#
# The tag goes before the extension rather than after it, so the file still
# opens in whatever a .log or an .xml is associated with.
function Get-ProjectTempPath {
    param([Parameter(Mandatory = $true)][string]$Name)

    $stem = [System.IO.Path]::GetFileNameWithoutExtension($Name)
    $ext = [System.IO.Path]::GetExtension($Name)

    return (Join-Path $env:TEMP "$stem.$ProjectTempTag$ext")
}
