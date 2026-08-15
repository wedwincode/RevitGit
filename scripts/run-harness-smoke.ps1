param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$harness = Join-Path $repositoryRoot "tools\RevitGit.Harness\bin\$Configuration\RevitGit.Harness.exe"

if (-not (Test-Path -LiteralPath $harness)) {
    throw "Harness executable was not found. Build the $Configuration configuration first: $harness"
}

$scenarios = @("linear-history", "branching", "restore", "compare", "reopen")
foreach ($scenario in $scenarios) {
    & $harness scenario $scenario
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Write-Output "HARNESS SMOKE: PASS"
