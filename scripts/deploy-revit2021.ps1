[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$Destination,

    [string]$BuildOutput
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($BuildOutput)) {
    $BuildOutput = Join-Path $repositoryRoot "src\RevitGit.Revit2021\bin\$Configuration"
}

$sourceAssembly = Join-Path $BuildOutput "RevitGit.Revit2021.dll"
if (-not (Test-Path -LiteralPath $sourceAssembly -PathType Leaf)) {
    throw "RevitGit.Revit2021.dll was not found. Build the $Configuration configuration first: $sourceAssembly"
}

if ([string]::IsNullOrWhiteSpace($Destination)) {
    if ([string]::IsNullOrWhiteSpace($env:APPDATA)) {
        throw "APPDATA is not defined. Supply -Destination explicitly."
    }

    $Destination = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2021"
}

$templatePath = Join-Path $repositoryRoot "deployment\RevitGit.addin.template"
if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf)) {
    throw "Add-in manifest template was not found: $templatePath"
}

$destinationRoot = [System.IO.Path]::GetFullPath($Destination)
$pluginDirectory = Join-Path $destinationRoot "RevitGit"
$deployedAssembly = Join-Path $pluginDirectory "RevitGit.Revit2021.dll"
$manifestPath = Join-Path $destinationRoot "RevitGit.addin"

New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
Copy-Item -LiteralPath $sourceAssembly -Destination $deployedAssembly -Force

$escapedAssemblyPath = [System.Security.SecurityElement]::Escape($deployedAssembly)
$manifest = (Get-Content -LiteralPath $templatePath -Raw).Replace("__REVITGIT_ASSEMBLY_PATH__", $escapedAssemblyPath)
Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding UTF8

if ((Test-Path -LiteralPath (Join-Path $pluginDirectory "RevitAPI.dll")) -or
    (Test-Path -LiteralPath (Join-Path $pluginDirectory "RevitAPIUI.dll"))) {
    throw "Deployment contains Autodesk Revit API assemblies. Remove them; the Revit host supplies these assemblies."
}

Write-Output "RevitGit deployed to: $destinationRoot"
Write-Output "Manifest: $manifestPath"
Write-Output "Assembly: $deployedAssembly"
