[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$InstallerVersion = ""
)

$ErrorActionPreference = "Stop"

function Invoke-ExternalStep {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action,
        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE"
    }
}

function Get-RepoInstallerVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    $propsPath = Join-Path $RepoRoot "Directory.Build.props"
    if (-not (Test-Path $propsPath)) {
        throw "Unable to locate Directory.Build.props at $propsPath"
    }

    [xml]$props = Get-Content -Path $propsPath
    $versionNode = $props.Project.PropertyGroup.PZServerLauncherVersion | Select-Object -First 1
    $version = [string]$versionNode
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Directory.Build.props does not define PZServerLauncherVersion."
    }

    return $version.Trim()
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishRoot = Join-Path $artifactsRoot "publish"
$appPublishDir = Join-Path $publishRoot "app"
$hostPublishDir = Join-Path $publishRoot "host"

if ([string]::IsNullOrWhiteSpace($InstallerVersion)) {
    $InstallerVersion = Get-RepoInstallerVersion -RepoRoot $repoRoot
}

Write-Host "Using installer version $InstallerVersion"

$localDotnet = Join-Path $env:USERPROFILE ".dotnet"
if (-not [string]::IsNullOrWhiteSpace($env:DOTNET_ROOT) -and (Test-Path (Join-Path $env:DOTNET_ROOT "dotnet.exe"))) {
    $dotnetExe = Join-Path $env:DOTNET_ROOT "dotnet.exe"
}
elseif (Test-Path (Join-Path $localDotnet "dotnet.exe")) {
    $dotnetExe = Join-Path $localDotnet "dotnet.exe"
}
else {
    $dotnetExe = "dotnet"
}

if (Test-Path $localDotnet) {
    $env:DOTNET_ROOT = $localDotnet
    $env:PATH = "$localDotnet;$localDotnet\tools;$env:PATH"
}

Write-Host "Publishing desktop app to $appPublishDir"
Invoke-ExternalStep -FailureMessage "Desktop app publish failed." -Action {
    & $dotnetExe publish (Join-Path $repoRoot "src\PZServerLauncher.App\PZServerLauncher.App.csproj") `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --self-contained false `
        /p:PublishSingleFile=false `
        /p:NuGetAudit=false `
        -o $appPublishDir
}

Write-Host "Publishing host to $hostPublishDir"
Invoke-ExternalStep -FailureMessage "Host publish failed." -Action {
    & $dotnetExe publish (Join-Path $repoRoot "src\PZServerLauncher.Host\PZServerLauncher.Host.csproj") `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --self-contained false `
        /p:PublishSingleFile=false `
        /p:NuGetAudit=false `
        -o $hostPublishDir
}

Write-Host "Building WiX installer"
Invoke-ExternalStep -FailureMessage "WiX installer build failed." -Action {
    & $dotnetExe build (Join-Path $repoRoot "installer\PZServerLauncher.Setup.wixproj") `
        -c $Configuration `
        -p:AppPublishDir="$appPublishDir" `
        -p:HostPublishDir="$hostPublishDir" `
        -p:NuGetAudit=false `
        -p:InstallerVersion="$InstallerVersion"
}

$msi = Get-ChildItem -Path (Join-Path $repoRoot "installer\bin") -Filter *.msi -Recurse |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $msi) {
    throw "Installer build completed but no MSI was produced."
}

Write-Host "Built MSI: $($msi.FullName)"
