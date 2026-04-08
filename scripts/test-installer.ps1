[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$BaseVersion = "0.1.0",
    [string]$UpgradeVersion = "0.1.1",
    [switch]$SkipIfNotAdmin
)

$ErrorActionPreference = "Stop"

function Invoke-MsiExec {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    $process = Start-Process -FilePath "msiexec.exe" -ArgumentList $Arguments -PassThru -Wait -NoNewWindow
    if ($process.ExitCode -notin 0, 3010) {
        throw "msiexec failed with exit code $($process.ExitCode). See log: $LogPath"
    }
}

function Invoke-PowerShellScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    powershell -ExecutionPolicy Bypass -File $ScriptPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE"
    }
}

function Test-ProcessPathRunning {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath
    )

    $normalizedPath = [IO.Path]::GetFullPath($ExecutablePath)
    $processes = Get-CimInstance Win32_Process -Filter "Name = 'PZServerLauncher.Host.exe'" -ErrorAction SilentlyContinue
    foreach ($process in $processes) {
        if ($process.ExecutablePath -and [string]::Equals([IO.Path]::GetFullPath($process.ExecutablePath), $normalizedPath, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Get-LatestMsiPath {
    param([string]$RepoRoot)

    $msi = Get-ChildItem -Path (Join-Path $RepoRoot "installer\bin") -Filter *.msi -Recurse |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $msi) {
        throw "No MSI was found under installer\\bin."
    }

    return $msi.FullName
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$artifactsRoot = Join-Path $repoRoot "artifacts"
$logsRoot = Join-Path $artifactsRoot "installer-smoke"
$installerCopiesRoot = Join-Path $logsRoot "msi"
$tempRoot = if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { $env:TEMP } else { $env:RUNNER_TEMP }
$installRoot = Join-Path $tempRoot "PZServerLauncher-Smoke"

$installDirectory = Join-Path $installRoot "install"
$dataRoot = Join-Path $env:LOCALAPPDATA "PZServerLauncher"
$sentinelFile = Join-Path $dataRoot "data\upgrade-sentinel.txt"

if (Test-Path $installRoot) {
    Remove-Item -LiteralPath $installRoot -Recurse -Force
}

if (Test-Path $sentinelFile) {
    Remove-Item -LiteralPath $sentinelFile -Force
}

New-Item -ItemType Directory -Force -Path $logsRoot | Out-Null
New-Item -ItemType Directory -Force -Path $installerCopiesRoot | Out-Null
New-Item -ItemType Directory -Force -Path $installRoot | Out-Null

$buildInstallerScript = Join-Path $repoRoot "scripts\build-installer.ps1"
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdministrator = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

Write-Host "Building base installer $BaseVersion"
Invoke-PowerShellScript -ScriptPath $buildInstallerScript -FailureMessage "Base installer build failed." -Arguments @(
    "-Configuration", $Configuration,
    "-RuntimeIdentifier", $RuntimeIdentifier,
    "-InstallerVersion", $BaseVersion)

$baseMsi = Get-LatestMsiPath -RepoRoot $repoRoot
$baseMsiCopy = Join-Path $installerCopiesRoot "PZServerLauncher-$BaseVersion.msi"
Copy-Item -Path $baseMsi -Destination $baseMsiCopy -Force

if (-not $isAdministrator) {
    $message = "Installer smoke tests require an elevated PowerShell session because the MSI installs per-machine."
    if ($SkipIfNotAdmin) {
        Write-Warning "$message Skipping install/upgrade/uninstall verification."
        exit 0
    }

    throw $message
}

$installLog = Join-Path $logsRoot "install-$BaseVersion.log"
Write-Host "Installing base MSI to $installDirectory"
Invoke-MsiExec -Arguments @(
    "/i", "`"$baseMsiCopy`"",
    "INSTALLFOLDER=`"$installDirectory`"",
    "/qn",
    "/norestart",
    "/l*v", "`"$installLog`"") -LogPath $installLog

$desktopExe = Join-Path $installDirectory "App\PZServerLauncher.App.exe"
$hostExe = Join-Path $installDirectory "Host\PZServerLauncher.Host.exe"
if (!(Test-Path $desktopExe)) {
    throw "Desktop executable was not installed to $desktopExe"
}

if (!(Test-Path $hostExe)) {
    throw "Host executable was not installed to $hostExe"
}

New-Item -ItemType Directory -Force -Path (Split-Path $sentinelFile -Parent) | Out-Null
Set-Content -Path $sentinelFile -Value "Created before upgrade $(Get-Date -Format o)"

$hostProcess = Start-Process -FilePath $hostExe -WorkingDirectory (Split-Path $hostExe -Parent) -PassThru
try {
    Start-Sleep -Seconds 3
    if (-not (Test-ProcessPathRunning -ExecutablePath $hostExe)) {
        throw "The installed host did not stay running before the upgrade verification began."
    }

    Write-Host "Building upgrade installer $UpgradeVersion"
    Invoke-PowerShellScript -ScriptPath $buildInstallerScript -FailureMessage "Upgrade installer build failed." -Arguments @(
        "-Configuration", $Configuration,
        "-RuntimeIdentifier", $RuntimeIdentifier,
        "-InstallerVersion", $UpgradeVersion)

    $upgradeMsi = Get-LatestMsiPath -RepoRoot $repoRoot
    $upgradeMsiCopy = Join-Path $installerCopiesRoot "PZServerLauncher-$UpgradeVersion.msi"
    Copy-Item -Path $upgradeMsi -Destination $upgradeMsiCopy -Force

    $upgradeLog = Join-Path $logsRoot "upgrade-$UpgradeVersion.log"
    Write-Host "Upgrading installation with MSI $UpgradeVersion"
    Invoke-MsiExec -Arguments @(
        "/i", "`"$upgradeMsiCopy`"",
        "INSTALLFOLDER=`"$installDirectory`"",
        "/qn",
        "/norestart",
        "/l*v", "`"$upgradeLog`"") -LogPath $upgradeLog

    if (!(Test-Path $desktopExe) -or !(Test-Path $hostExe)) {
        throw "Installed binaries were not present after upgrade."
    }

    if (!(Test-Path $sentinelFile)) {
        throw "Expected app data sentinel to survive the installer upgrade."
    }

    Start-Sleep -Seconds 3
    if (-not (Test-ProcessPathRunning -ExecutablePath $hostExe)) {
        throw "Expected the installer upgrade to restart the host because it was running before the upgrade."
    }

    $uninstallLog = Join-Path $logsRoot "uninstall-$UpgradeVersion.log"
    Write-Host "Uninstalling upgraded MSI"
    Invoke-MsiExec -Arguments @(
        "/x", "`"$upgradeMsiCopy`"",
        "/qn",
        "/norestart",
        "/l*v", "`"$uninstallLog`"") -LogPath $uninstallLog
}
finally {
    if ($hostProcess -and !$hostProcess.HasExited) {
        Stop-Process -Id $hostProcess.Id -Force -ErrorAction SilentlyContinue
    }
}

if ((Test-Path $desktopExe) -or (Test-Path $hostExe)) {
    throw "Installed binaries still exist after uninstall."
}

Write-Host "Installer smoke test completed successfully."
Write-Host "Logs: $logsRoot"
