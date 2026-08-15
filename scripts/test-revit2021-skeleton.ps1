param(
    [string]$MsBuildPath,
    [string]$Revit2021InstallDir
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src\RevitGit.Revit2021\RevitGit.Revit2021.csproj"
$templatePath = Join-Path $repositoryRoot "deployment\RevitGit.addin.template"
$deployScript = Join-Path $PSScriptRoot "deploy-revit2021.ps1"
$removeScript = Join-Path $PSScriptRoot "remove-revit2021.ps1"
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("RevitGit-Epic8-" + [Guid]::NewGuid().ToString("N"))
$buildOutput = Join-Path $testRoot "build"
$destination = Join-Path $testRoot "addins"

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

try {
    [xml]$project = Get-Content -LiteralPath $projectPath -Raw
    $namespace = New-Object System.Xml.XmlNamespaceManager($project.NameTable)
    $namespace.AddNamespace("msb", "http://schemas.microsoft.com/developer/msbuild/2003")

    $localImport = $project.SelectSingleNode("/msb:Project/msb:Import[contains(@Project, 'Directory.Build.props.user')]", $namespace)
    $fallback = $project.SelectSingleNode("//msb:Revit2021InstallDir[@Condition]", $namespace)
    $validationErrors = @($project.SelectNodes("//msb:Target[@Name='ValidateRevit2021Install']/msb:Error", $namespace))
    Assert-True ($null -ne $localImport) "Developer-local Directory.Build.props.user import is missing."
    Assert-True ($fallback.InnerText -eq "C:\Program Files\Autodesk\Revit 2021") "Standard Revit 2021 fallback is incorrect."
    Assert-True ($fallback.Condition -match "Revit2021InstallDir.*==.*''") "Fallback must apply only when no override is set."
    Assert-True ($validationErrors.Count -eq 5) "Revit installation validation must check both APIs, Revit.exe, and the API assembly versions."
    Assert-True (($validationErrors | ForEach-Object { $_.Text }) -join " " -match "Resolved Revit2021InstallDir") "Missing-install error does not report the resolved path."
    Assert-True (($validationErrors | ForEach-Object { $_.Text }) -join " " -match "expected 21.0.0.0") "Revit 2021 API version sanity-check is missing."

    $revitApi = $project.SelectSingleNode("//msb:Reference[@Include='RevitAPI']", $namespace)
    $revitApiUi = $project.SelectSingleNode("//msb:Reference[@Include='RevitAPIUI']", $namespace)
    Assert-True ($null -ne $revitApi) "RevitAPI reference is missing."
    Assert-True ($null -ne $revitApiUi) "RevitAPIUI reference is missing."
    Assert-True ($revitApi.HintPath -eq '$(Revit2021InstallDir)\RevitAPI.dll') "RevitAPI HintPath does not use Revit2021InstallDir."
    Assert-True ($revitApiUi.HintPath -eq '$(Revit2021InstallDir)\RevitAPIUI.dll') "RevitAPIUI HintPath does not use Revit2021InstallDir."
    Assert-True ($revitApi.Private -eq "false") "RevitAPI Copy Local must be false."
    Assert-True ($revitApiUi.Private -eq "false") "RevitAPIUI Copy Local must be false."
    $platformTargets = @($project.SelectNodes("//msb:PlatformTarget", $namespace) | ForEach-Object { $_.InnerText })
    Assert-True ($platformTargets.Count -eq 2 -and ($platformTargets | Where-Object { $_ -ne "x64" }).Count -eq 0) "The Revit-hosted project must target x64 in Debug and Release."

    $headlessProjects = @(
        "src\RevitGit.Domain\RevitGit.Domain.csproj",
        "src\RevitGit.Application\RevitGit.Application.csproj",
        "tools\RevitGit.Harness\RevitGit.Harness.csproj"
    )
    foreach ($headlessProject in $headlessProjects) {
        $headlessText = Get-Content -LiteralPath (Join-Path $repositoryRoot $headlessProject) -Raw
        Assert-True ($headlessText -notmatch "RevitAPI") "$headlessProject must remain independent of Revit API."
    }

    [xml]$template = Get-Content -LiteralPath $templatePath -Raw
    $addIn = $template.RevitAddIns.AddIn
    Assert-True ($addIn.Type -eq "Application") "Manifest must register an external application."
    Assert-True ([Guid]::Parse($addIn.AddInId) -ne [Guid]::Empty) "Manifest AddInId is invalid."
    Assert-True ($addIn.FullClassName -eq "RevitGit.Revit2021.RevitGitApplication") "Manifest FullClassName is incorrect."
    Assert-True ($addIn.Assembly -eq "__REVITGIT_ASSEMBLY_PATH__") "Manifest assembly placeholder is incorrect."

    New-Item -ItemType Directory -Path $buildOutput -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $buildOutput "RevitGit.Revit2021.dll") -Value "test assembly"
    Set-Content -LiteralPath (Join-Path $buildOutput "RevitGit.Domain.dll") -Value "test domain assembly"
    & $deployScript -Configuration Debug -Destination $destination -BuildOutput $buildOutput

    $manifestPath = Join-Path $destination "RevitGit.addin"
    $deployedAssembly = Join-Path $destination "RevitGit\RevitGit.Revit2021.dll"
    $deployedDomainAssembly = Join-Path $destination "RevitGit\RevitGit.Domain.dll"
    Assert-True (Test-Path -LiteralPath $manifestPath) "Deploy did not create the manifest."
    Assert-True (Test-Path -LiteralPath $deployedAssembly) "Deploy did not copy the add-in assembly."
    Assert-True (Test-Path -LiteralPath $deployedDomainAssembly) "Deploy did not copy the required Domain assembly."
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $destination "RevitGit\RevitAPI.dll"))) "Deploy copied RevitAPI.dll."
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $destination "RevitGit\RevitAPIUI.dll"))) "Deploy copied RevitAPIUI.dll."

    [xml]$deployedManifest = Get-Content -LiteralPath $manifestPath -Raw
    Assert-True ($deployedManifest.RevitAddIns.AddIn.Assembly -eq $deployedAssembly) "Manifest does not reference the deployed assembly."

    if (-not [string]::IsNullOrWhiteSpace($MsBuildPath)) {
        Assert-True (Test-Path -LiteralPath $MsBuildPath -PathType Leaf) "MSBuild was not found: $MsBuildPath"

        $isolatedProject = Join-Path $testRoot "RevitGit.Revit2021.csproj"
        Copy-Item -LiteralPath $projectPath -Destination $isolatedProject
        $fallbackOutput = & $MsBuildPath $isolatedProject /nologo /getProperty:Revit2021InstallDir 2>&1 | Out-String
        Assert-True ($LASTEXITCODE -eq 0) "Could not evaluate the standard fallback."
        Assert-True ($fallbackOutput.Trim() -eq "C:\Program Files\Autodesk\Revit 2021") "Standard fallback did not resolve as expected: $fallbackOutput"

        $explicitMissingPath = "Z:\RevitGit-Missing-Revit-2021"
        $explicitOutput = & $MsBuildPath $projectPath /nologo /getProperty:Revit2021InstallDir "/p:Revit2021InstallDir=$explicitMissingPath" 2>&1 | Out-String
        Assert-True ($LASTEXITCODE -eq 0) "Could not evaluate an explicit Revit2021InstallDir."
        Assert-True ($explicitOutput.Trim() -eq $explicitMissingPath) "Explicit Revit2021InstallDir did not take priority: $explicitOutput"

        $missingOutput = & $MsBuildPath $projectPath /nologo /t:ValidateRevit2021Install "/p:Revit2021InstallDir=$explicitMissingPath" /v:minimal 2>&1 | Out-String
        Assert-True ($LASTEXITCODE -ne 0) "Missing Revit API assemblies should fail validation."
        Assert-True ($missingOutput -match [regex]::Escape("Resolved Revit2021InstallDir: $explicitMissingPath")) "Missing-install error does not contain the resolved path: $missingOutput"

        if (-not [string]::IsNullOrWhiteSpace($Revit2021InstallDir)) {
            $validOutput = & $MsBuildPath $projectPath /nologo /t:ValidateRevit2021Install "/p:Revit2021InstallDir=$Revit2021InstallDir" /v:minimal 2>&1 | Out-String
            Assert-True ($LASTEXITCODE -eq 0) "The supplied Revit 2021 installation did not validate: $validOutput"
        }
    }

    Set-Content -LiteralPath (Join-Path $destination "unrelated.addin") -Value "keep"
    & $removeScript -Destination $destination
    Assert-True (-not (Test-Path -LiteralPath $manifestPath)) "Undeploy did not remove RevitGit.addin."
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $destination "RevitGit"))) "Undeploy did not remove the RevitGit directory."
    Assert-True (Test-Path -LiteralPath (Join-Path $destination "unrelated.addin")) "Undeploy removed an unrelated file."

    Write-Output "REVIT 2021 SKELETON ACCEPTANCE: PASS"
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
