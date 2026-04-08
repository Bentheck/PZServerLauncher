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

## First Milestones

1. Execute SteamCMD update scripts from the app.
2. Persist multiple server profiles.
3. Parse and save `*.ini` and `*_SandboxVars.lua`.
4. Launch the server with live log streaming.
5. Add backups and restore before destructive actions.
