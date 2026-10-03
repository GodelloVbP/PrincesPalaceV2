# tools/accept_library.ps1 -- rewrites a project tree's Library stamp to the
# editor version its ProjectVersion.txt names. Run once per tree, after the Hub
# has opened that project cleanly in that editor; until then the launch guard
# in tools/unity_path.ps1 refuses a project tree whose stamp names another one.
#
#   powershell -File tools\accept_library.ps1 -AcceptLibrary "C:\path\to\project"
#
# Pure ASCII, no BOM.
param(
    [Parameter(Mandatory = $true)][string]$AcceptLibrary
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "unity_path.ps1")

$version = Confirm-LibraryAccepted -ProjectPath $AcceptLibrary
Write-Host "Library stamp for $AcceptLibrary set to Unity $version."
