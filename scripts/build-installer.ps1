[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$InstallerVersion = "",
    [string]$DotnetRoot = ""
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

function Reset-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,
        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
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
$installerPublishDir = Join-Path $publishRoot "installer-app"
$dotnetCliHome = Join-Path $repoRoot ".dotnet-home"

if ([string]::IsNullOrWhiteSpace($InstallerVersion)) {
    $InstallerVersion = Get-RepoInstallerVersion -RepoRoot $repoRoot
}

Write-Host "Using installer version $InstallerVersion"

$dotnetCandidates = @()
if (-not [string]::IsNullOrWhiteSpace($DotnetRoot)) {
    $dotnetCandidates += $DotnetRoot
}

$dotnetCandidates += @(
    (Join-Path $repoRoot ".dotnet-build"),
    (Join-Path $repoRoot ".dotnet-local")
)

if (-not [string]::IsNullOrWhiteSpace($env:DOTNET_ROOT)) {
    $dotnetCandidates += $env:DOTNET_ROOT
}

$dotnetCandidates += @(
    (Join-Path $env:USERPROFILE ".dotnet"),
    "dotnet"
)

$selectedDotnetRoot = $null
$dotnetExe = $null
foreach ($candidate in $dotnetCandidates) {
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        continue
    }

    if ($candidate -eq "dotnet") {
        $dotnetExe = "dotnet"
        break
    }

    $candidateExe = Join-Path $candidate "dotnet.exe"
    if (Test-Path $candidateExe) {
        $selectedDotnetRoot = $candidate
        $dotnetExe = $candidateExe
        break
    }
}

if ($null -eq $dotnetExe) {
    throw "Unable to locate a dotnet SDK installation."
}

if ($null -ne $selectedDotnetRoot) {
    $env:DOTNET_ROOT = $selectedDotnetRoot
    $env:PATH = "$selectedDotnetRoot;$selectedDotnetRoot\tools;$env:PATH"
}

$env:DOTNET_CLI_HOME = $dotnetCliHome
New-Item -ItemType Directory -Force -Path $dotnetCliHome | Out-Null

Reset-Directory -Path $appPublishDir
Reset-Directory -Path $installerPublishDir

Write-Host "Publishing desktop app to $appPublishDir"
Invoke-ExternalStep -FailureMessage "Desktop app publish failed." -Action {
    & $dotnetExe publish (Join-Path $repoRoot "src\PZServerLauncher.App\PZServerLauncher.App.csproj") `
        -c $Configuration `
        -r $RuntimeIdentifier `
        -m:1 `
        --self-contained false `
        /p:PublishSingleFile=false `
        /p:UsedAvaloniaProducts= `
        /p:RestoreDisableParallel=true `
        /p:NuGetAudit=false `
        -o $appPublishDir
}

Write-Host "Staging desktop app payload in $installerPublishDir"
Copy-DirectoryContents -Source $appPublishDir -Destination $installerPublishDir

Write-Host "Building WiX installer"
Invoke-ExternalStep -FailureMessage "WiX installer build failed." -Action {
    & $dotnetExe build (Join-Path $repoRoot "installer\PZServerLauncher.Setup.wixproj") `
        -c $Configuration `
        -m:1 `
        -p:AppPublishDir="$installerPublishDir" `
        -p:SuppressValidation=true `
        -p:RestoreDisableParallel=true `
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
