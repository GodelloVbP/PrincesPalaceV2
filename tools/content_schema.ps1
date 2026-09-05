param(
    [switch]$Check
)

# Regenerates docs/CONTENT_SCHEMA.md from the Raw*Entry types.
#
# The generator (Assets/_Project/Scripts/Domain/Content/ContentSchema.cs) and
# the test that runs it (Tests/EditMode/ContentSchemaTests.cs) are the same
# code either way - only CONTENT_SCHEMA_WRITE picks WRITE over COMPARE. That
# is what keeps this script from being a second copy of the generation logic
# that could itself drift from the one dotnet test actually runs.
#
# Default: WRITE mode, then a second COMPARE run to prove the file it just
# wrote is stable (a generator with a non-deterministic field order would
# fail its own compare test on the very next run). -Check skips the write and
# only compares, for a pre-commit-style check that refuses to touch the file.

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionDir = Join-Path $PSScriptRoot "domain-tests"
$filter = "FullyQualifiedName~.ContentSchemaTests."

function Invoke-SchemaTest([bool]$write) {
    Push-Location $repoRoot
    try {
        if ($write) {
            $env:CONTENT_SCHEMA_WRITE = "1"
        } else {
            Remove-Item Env:\CONTENT_SCHEMA_WRITE -ErrorAction SilentlyContinue
        }
        # Piped through Out-Host rather than left as the function's own
        # output: PowerShell functions return everything written to the
        # output stream during their body, so an unpiped `& dotnet test`
        # here would make its console text part of $code instead of the
        # integer below, and the caller's -ne 0 check would compare against
        # an array instead of an exit code.
        & dotnet test $solutionDir --nologo --filter $filter | Out-Host
        $code = $LASTEXITCODE
    } finally {
        Remove-Item Env:\CONTENT_SCHEMA_WRITE -ErrorAction SilentlyContinue
        Pop-Location
    }
    return $code
}

if ($Check) {
    Write-Host "Comparing docs/CONTENT_SCHEMA.md against the Raw*Entry types (no write)..."
    $code = Invoke-SchemaTest $false
    if ($code -ne 0) {
        Write-Host ""
        Write-Host "docs/CONTENT_SCHEMA.md is stale. Run tools/content_schema.ps1 (no -Check) to fix it."
        exit $code
    }
    Write-Host "docs/CONTENT_SCHEMA.md matches."
    exit 0
}

Write-Host "Regenerating docs/CONTENT_SCHEMA.md..."
$writeCode = Invoke-SchemaTest $true
if ($writeCode -ne 0) {
    Write-Host ""
    Write-Host "Generation failed - see the dotnet test output above (most likely a missing [ContentDoc])."
    exit $writeCode
}

Write-Host ""
Write-Host "Verifying the write is stable..."
$verifyCode = Invoke-SchemaTest $false
if ($verifyCode -ne 0) {
    Write-Host ""
    Write-Host "The file this script just wrote does not match its own regeneration - the generator is not deterministic. This is a bug in ContentSchema.cs, not in the JSON or the [ContentDoc] attributes."
    exit $verifyCode
}

Write-Host ""
Write-Host "docs/CONTENT_SCHEMA.md is up to date. Review the diff, then commit it."
exit 0
