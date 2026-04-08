# PZServerLauncher Full Project Plan v2

## Summary

- Build `PZServerLauncher` as a Windows-first Project Zomboid server management suite with two clients: a primary `Avalonia desktop app` and an optional `Blazor web admin`.
- Make desktop/local hosting the complete default experience. A user can install, configure, launch, back up, and restore servers without ever enabling the web feature.
- Use a separate per-user background host as the single source of truth for installs, runtime operations, config writes, jobs, backups, auth, and audit logging.
- Support `Build 41 Stable` and `Build 42 Unstable` in v1, always with isolated install and cache roots per profile.

## Key Changes And Decisions

### Runtime And Transport

- Add `PZServerLauncher.Host` as a separate single-instance background process running under the current Windows user, not as a Windows service in v1.
- The desktop app never mutates files or processes directly. It always talks to the host.
- The host always exposes a loopback-only control API on `127.0.0.1` using the first free port in `48231-48239`, recorded in a per-user host-state file under `%LocalAppData%\PZServerLauncher\state`.
- The desktop authenticates to the loopback API with a DPAPI-protected local bearer token generated at first bootstrap. No anonymous local mutating endpoints exist.
- The web/admin surface is optional and disabled by default. When disabled, the host opens no non-loopback listener and serves no browser UI.
- When enabled, the same host additionally binds HTTPS on a user-selected IPv4 address and port, default `8443`. The same application services are used for both loopback desktop and optional remote web access.
- Use `REST` for commands and queries and a single `SignalR` hub for log lines, job progress, and runtime status. Do not maintain separate desktop-only and web-only mutation paths.

### Host Lifetime And Packaging

- The desktop app launches the host if it is not already running, then attaches to it.
- Closing the desktop window minimizes the desktop app to tray. Choosing `Exit Desktop` stops only the desktop UI and leaves the host untouched.
- Stopping the host is a separate explicit action from desktop settings. If any managed server is running, the host shutdown flow offers only `Cancel` or `Stop all servers, then stop host`.
- `Start host with Windows` is a host-level setting, off by default, implemented with an `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry.
- Each server profile has a separate `StartWithHost` flag, off by default. If enabled, that profile auto-starts when the host starts.
- The Windows installer uses `WiX v5`. During upgrade it stops the host, upgrades binaries, preserves data, and restarts the host only if it was running before the upgrade.

### Server Launch, Config, Mods, And Backups

- Do not edit vendor-installed Project Zomboid files in place.
- On each launch, the host generates a temporary wrapper command in `%LocalAppData%\PZServerLauncher\runtime\profiles\<profileId>\launch\` and invokes the bundled dedicated-server Java runtime directly.
- The wrapper is built from the current installed `StartServer64.bat` template: the host extracts the official non-memory JVM flags and launch target, replaces only memory flags, and appends launcher-controlled game args.
- Memory control is implemented only through launcher-generated JVM args (`-Xms` and `-Xmx`), never by patching vendor batch files. If extraction from the installed batch template fails after an update, the profile falls back to official batch launch and custom memory controls are disabled until compatibility is restored.
- Split configuration into two API shapes:
  - `GET/PUT /api/profiles/{id}/config/common` for structured, typed settings used by both desktop and web.
  - `GET/PUT /api/profiles/{id}/config/files/{kind}` for raw text files with `sha256`, parse diagnostics, and optimistic concurrency. `kind` is one of `Ini`, `SandboxVars`, `SpawnRegions`, `SpawnPoints`.
- The web UI uses only `common` config endpoints. Raw file editing is desktop-only in v1.
- Workshop/mod management stores ordered `WorkshopItemIds`, `EnabledModIds`, and `MapFolders` in the profile.
- Input accepts Steam Workshop URLs or numeric IDs, normalizes them to unique workshop IDs, then scans locally downloaded workshop content and `mod.info` files to discover valid mod IDs and map folders.
- v1 does not call Steam Web APIs, resolve dependencies automatically, or implement conflict-solving heuristics. Validation is limited to duplicates, missing local mods, missing map folders, and empty presets.
- Restore always requires the server to be stopped. The restore flow offers `Stop, restore, restart` as one operation.
- Backup contents are fixed in v1: profile metadata snapshot, `Server\<serverName>*`, `Saves\Multiplayer\<serverName>\**`, `db\**`, and the profile export manifest.
- Backups are stored as ZIP archives with `manifest.json` and per-entry `SHA-256` checksums.
- Pre-update backup is always on. Scheduled backups are optional and off by default. Retention defaults are: keep last `10` scheduled backups, last `5` pre-update backups, and never auto-delete manual backups.

### Remote Access, Auth, And Security

- Remote/web onboarding is explicit and desktop-driven. Enabling it launches a wizard that collects:
  - Bind IPv4 address
  - External HTTPS port, default `8443`
  - PFX certificate path
  - PFX password
  - Optional public hostname
  - Whether to create a Windows Firewall inbound rule
- The wizard validates that the bind address is available, the certificate loads, and the hostname matches a certificate SAN if a hostname is provided.
- Router/NAT configuration is never automated in v1. The app provides a checklist and a local self-test only.
- Remote users are stored with `ASP.NET Core Identity` in SQLite. Roles are `Owner`, `Admin`, `Operator`, and `Viewer`.
- `Owner` and `Admin` require password plus TOTP for all web logins. `Operator` and `Viewer` use password only in v1.
- The first remote enablement converts local-only setup into an account-backed setup by creating the initial `Owner` account and enrolling TOTP.
- Apply rate limiting and lockout to web auth: `5` failed login attempts per account or per IP in `15` minutes causes a `15` minute lockout.
- There is no cloud relay, ACME automation, SSH host management, or external identity provider in v1.

## Public APIs, Types, And Storage

- Add projects/layers: `Host`, `Contracts`, and optional hosted `Web` assets while keeping `Core`, `Infrastructure`, and `App`.
- Standardize these core types: `ServerProfile`, `BackupPolicy`, `RemoteAccessSettings`, `WorkshopPreset`, `ServerRuntimeStatus`, `OperationJob`, `AuditEntry`, `HostHealth`, `UserRole`, and `OwnerBootstrapState`.
- Standardize these services: `IProfileRepository`, `ISteamCmdService`, `IServerInstallService`, `IServerProcessSupervisor`, `IServerConfigService`, `IModPresetService`, `IBackupService`, `IRemoteAccessService`, `IAuditService`, and `IAuthBootstrapService`.
- Use these host endpoints:
  - `GET/POST/PUT/DELETE /api/profiles`
  - `POST /api/profiles/{id}/install`
  - `POST /api/profiles/{id}/update`
  - `POST /api/profiles/{id}/start`
  - `POST /api/profiles/{id}/stop`
  - `POST /api/profiles/{id}/restart`
  - `GET /api/profiles/{id}/status`
  - `GET/PUT /api/profiles/{id}/config/common`
  - `GET/PUT /api/profiles/{id}/config/files/{kind}`
  - `POST /api/profiles/{id}/backup`
  - `POST /api/profiles/{id}/restore`
  - `GET/POST/PUT/DELETE /api/users`
  - `GET/PUT /api/settings/host`
  - `GET/PUT /api/settings/remote-access`
  - `POST /api/onboarding/bootstrap`
- Expose one SignalR hub at `/hubs/runtime` for job progress, profile state changes, and live log streaming.
- Use `SQLite + EF Core migrations` for persistent data.
- On host startup, acquire a single migration lock, create `app.db.bak`, apply pending migrations transactionally, and restore from backup if migration fails.

## Delivery Sequence

- Phase 1: Host foundation, contracts, loopback transport, DPAPI local auth token, SQLite, logging, audit trail, single-instance host management, and desktop-to-host connection bootstrap.
- Phase 2: SteamCMD bootstrap, install/update/import flows, profile persistence, Build 41 vs Build 42 isolation, and validation for ports, paths, and branch compatibility.
- Phase 3: Runtime process supervision, wrapper-based launch, memory control, live logs, crash detection, auto-restart, tray behavior, and host lifetime settings.
- Phase 4: Structured config editor, raw file editor, config concurrency checks, workshop/mod scanning, backup/restore, scheduled jobs, and retention.
- Phase 5: Optional web admin, HTTPS onboarding, identity, roles, TOTP, rate limiting, remote status/control pages, and remote settings UX.
- Phase 6: WiX installer, upgrade preservation, migration recovery, first-run polish, import wizard polish, and end-to-end hardening.

## Test Plan

- Unit tests for loopback host bootstrap, local token validation, host-state port discovery, launch-template extraction, memory arg generation, backup retention, workshop scanning, and role authorization rules.
- Parser round-trip tests for `.ini` and `SandboxVars.lua` plus diagnostics tests for malformed raw files.
- Integration tests with fake SteamCMD and fake server launchers for install success, install failure, update failure, wrapper launch success, wrapper launch compatibility failure, graceful shutdown, crash detection, and restore requiring a stopped server.
- Host API tests proving:
  - Loopback API works when web admin is disabled
  - No non-loopback listener exists when web admin is disabled
  - Remote HTTPS listener binds only after successful onboarding
  - `Owner/Admin` require TOTP and `Viewer` cannot mutate
  - Raw file endpoints reject stale `sha256`
- Migration tests proving the DB backup is created, a failed migration restores the prior DB, and installer upgrades preserve profiles, users, jobs, and remote-access settings.
- End-to-end tests for:
  - Local-only first run with web skipped
  - Import of an existing local server
  - Desktop exit leaving host running
  - Optional later enablement of remote admin
  - Config change, backup, restart, restore, and optional restart after restore
  - Upgrade with host restart and profile persistence

## Assumptions And Defaults

- v1 officially supports `Windows 10/11 x64` only.
- v1 manages servers running on the same Windows machine only.
- Supported branches are `Build 41 Stable` and `Build 42 Unstable`.
- SteamCMD is downloaded on demand and stored under the app’s local tools directory.
- The optional web feature is built in but off by default.
- The desktop app is the advanced control surface. The web UI is narrower and safer in v1.
- Remote access is self-hosted only. Manual port forwarding is required if the user wants internet reachability.
- No in-app self-update is included in v1.
