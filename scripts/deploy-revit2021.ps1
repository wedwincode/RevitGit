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

$runtimeAssemblyNames = @(
    "RevitGit.Revit2021.dll",
    "RevitGit.Application.dll",
    "RevitGit.Domain.dll",
    "RevitGit.Infrastructure.FileSystem.dll",
    "RevitGit.Infrastructure.Git.dll",
    "RevitGit.UI.dll",
    "LibGit2Sharp.dll"
)

$libGitConfigName = "LibGit2Sharp.dll.config"
$nativeRelativePath = "lib\win32\x64\git2-3f4182d.dll"

foreach ($runtimeAssemblyName in $runtimeAssemblyNames) {
    $sourceAssembly = Join-Path $BuildOutput $runtimeAssemblyName
    if (-not (Test-Path -LiteralPath $sourceAssembly -PathType Leaf)) {
        throw "$runtimeAssemblyName was not found. Build the $Configuration configuration first: $sourceAssembly"
    }
}

$libGitConfig = Join-Path $BuildOutput $libGitConfigName
if (-not (Test-Path -LiteralPath $libGitConfig -PathType Leaf)) {
    throw "$libGitConfigName was not found in the Revit runtime output: $libGitConfig"
}

$nativeSource = Join-Path $BuildOutput $nativeRelativePath
if (-not (Test-Path -LiteralPath $nativeSource -PathType Leaf)) {
    throw "The Windows x64 native libgit2 binary was not found: $nativeSource"
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
foreach ($runtimeAssemblyName in $runtimeAssemblyNames) {
    Copy-Item `
        -LiteralPath (Join-Path $BuildOutput $runtimeAssemblyName) `
        -Destination (Join-Path $pluginDirectory $runtimeAssemblyName) `
        -Force
}
Copy-Item -LiteralPath $libGitConfig -Destination (Join-Path $pluginDirectory $libGitConfigName) -Force
$nativeDestinationDirectory = Join-Path $pluginDirectory "lib\win32\x64"
New-Item -ItemType Directory -Path $nativeDestinationDirectory -Force | Out-Null
Copy-Item -LiteralPath $nativeSource -Destination (Join-Path $nativeDestinationDirectory "git2-3f4182d.dll") -Force

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
Write-Output "Managed runtime: $($runtimeAssemblyNames -join ', ')"
Write-Output "Native runtime: $nativeRelativePath"
