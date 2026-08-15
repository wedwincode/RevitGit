[CmdletBinding()]
param(
    [string]$Revit2021InstallDir
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($Revit2021InstallDir)) {
    $Revit2021InstallDir = $env:Revit2021InstallDir
}

if ([string]::IsNullOrWhiteSpace($Revit2021InstallDir)) {
    $localPropsPath = Join-Path $repositoryRoot "Directory.Build.props.user"
    if (Test-Path -LiteralPath $localPropsPath -PathType Leaf) {
        [xml]$localProps = Get-Content -LiteralPath $localPropsPath -Raw
        $Revit2021InstallDir = [string]$localProps.Project.PropertyGroup.Revit2021InstallDir
    }
}

if ([string]::IsNullOrWhiteSpace($Revit2021InstallDir)) {
    $Revit2021InstallDir = "C:\Program Files\Autodesk\Revit 2021"
}

$revitExecutable = Join-Path $Revit2021InstallDir "Revit.exe"
if (-not (Test-Path -LiteralPath $revitExecutable -PathType Leaf)) {
    throw "Revit 2021 executable was not found: $revitExecutable"
}

Start-Process -FilePath $revitExecutable
