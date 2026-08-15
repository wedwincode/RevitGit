[CmdletBinding()]
param(
    [string]$Destination
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Destination)) {
    if ([string]::IsNullOrWhiteSpace($env:APPDATA)) {
        throw "APPDATA is not defined. Supply -Destination explicitly."
    }

    $Destination = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2021"
}

$destinationRoot = [System.IO.Path]::GetFullPath($Destination)
$manifestPath = Join-Path $destinationRoot "RevitGit.addin"
$pluginDirectory = Join-Path $destinationRoot "RevitGit"

if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
    Remove-Item -LiteralPath $manifestPath -Force
}

if (Test-Path -LiteralPath $pluginDirectory -PathType Container) {
    $resolvedPluginDirectory = [System.IO.Path]::GetFullPath($pluginDirectory)
    $expectedParent = [System.IO.Path]::GetFullPath((Split-Path -Parent $resolvedPluginDirectory))
    if ($expectedParent -ne $destinationRoot -or (Split-Path -Leaf $resolvedPluginDirectory) -ne "RevitGit") {
        throw "Refusing to remove an unexpected directory: $resolvedPluginDirectory"
    }

    Remove-Item -LiteralPath $resolvedPluginDirectory -Recurse -Force
}

Write-Output "RevitGit removed from: $destinationRoot"
