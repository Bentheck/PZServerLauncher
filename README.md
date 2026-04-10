# PZServerLauncher

Starter scaffold for a Project Zomboid server installer, configurator, and launcher.

## Stack

- C#
- .NET 10
- Avalonia for the desktop UI
- xUnit for tests

## Solution Layout

- `src/PZServerLauncher.App`
  Avalonia desktop shell and view models.
- `src/PZServerLauncher.Core`
  Domain models, profile definitions, and service contracts.
- `src/PZServerLauncher.Infrastructure`
  Concrete planning and process-oriented services.
- `tests/PZServerLauncher.Tests`
  Tests for planners and future config/parser logic.
- `installer`
  WiX-based Windows packaging assets and MSI authoring.
- `scripts/build-installer.ps1`
  Publishes the desktop app and host, then builds the MSI installer.

## First Milestones

1. Execute SteamCMD update scripts from the app.
2. Persist multiple server profiles.
3. Parse and save `*.ini` and `*_SandboxVars.lua`.
4. Launch the server with live log streaming.
5. Add backups and restore before destructive actions.

## Packaging

Build the Windows installer with:

```powershell
./scripts/build-installer.ps1
```

Optional parameters:

```powershell
./scripts/build-installer.ps1 -Configuration Release -RuntimeIdentifier win-x64 -InstallerVersion 0.2.0
```

The script stages published outputs under `artifacts/publish` and then builds the WiX MSI from those staged folders. If `-InstallerVersion` is omitted, it uses the repo version from `Directory.Build.props`.

Run the local installer smoke test with:

```powershell
./scripts/test-installer.ps1
```

That script builds two MSI versions, installs the first, upgrades to the second, checks that app data survives the upgrade, and then uninstalls the product. By default it uses the repo version from `Directory.Build.props` as the base MSI version and increments the patch version for the upgrade pass. The same flow runs in GitHub Actions on `windows-latest` through `.github/workflows/installer-smoke.yml`.

Local smoke testing requires an elevated PowerShell session because the MSI is authored as a per-machine installer.
