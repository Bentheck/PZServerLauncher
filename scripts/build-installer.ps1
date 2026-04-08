[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$InstallerVersion = "0.1.0"
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

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishRoot = Join-Path $artifactsRoot "publish"
$appPublishDir = Join-Path $publishRoot "app"
$hostPublishDir = Join-Path $publishRoot "host"

$localDotnet = Join-Path $env:USERPROFILE ".dotnet"
if (Test-Path $localDotnet) {
    $env:DOTNET_ROOT = $localDotnet
    $env:PATH = "$localDotnet;$localDotnet\tools;$env:PATH"
}

Write-Host "Publishing desktop app to $appPublishDir"
Invoke-ExternalStep -FailureMessage "Desktop app publish failed." -Action {
    dotnet publish (Join-Path $repoRoot "src\PZServerLauncher.App\PZServerLauncher.App.csproj") `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --self-contained false `
        /p:PublishSingleFile=false `
        -o $appPublishDir
}

Write-Host "Publishing host to $hostPublishDir"
Invoke-ExternalStep -FailureMessage "Host publish failed." -Action {
    dotnet publish (Join-Path $repoRoot "src\PZServerLauncher.Host\PZServerLauncher.Host.csproj") `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --self-contained false `
        /p:PublishSingleFile=false `
        -o $hostPublishDir
}

Write-Host "Building WiX installer"
Invoke-ExternalStep -FailureMessage "WiX installer build failed." -Action {
    dotnet build (Join-Path $repoRoot "installer\PZServerLauncher.Setup.wixproj") `
        -c $Configuration `
        -p:AppPublishDir="$appPublishDir" `
        -p:HostPublishDir="$hostPublishDir" `
        -p:InstallerVersion="$InstallerVersion"
}

$msi = Get-ChildItem -Path (Join-Path $repoRoot "installer\bin") -Filter *.msi -Recurse |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $msi) {
    throw "Installer build completed but no MSI was produced."
}

Write-Host "Built MSI: $($msi.FullName)"
