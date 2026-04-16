# PZServerLauncher Release Hardening Plan v7

## Summary

Harden the current launcher into a predictable `0.3.0` release on both `Avalonia desktop` and the built-in `Blazor web` host without expanding scope into email delivery.

This version locks the following decisions:
- `0.3.0` is a `SemVer minor` release because it adds visible operator capabilities
- persistent logging uses `rolling log files`
- launch must `fail loud` when the launcher cannot build its own direct Java command
- fixes apply to `both desktop and web surfaces`
- `Network & Admin` drafts stay `disabled` so write-only secrets are never stored in draft persistence
- owner bootstrap must expose a `real create/save action` instead of status-only UI with no obvious submit path

## Key Changes

### 1. Persistent Logging Under The Launcher Root

- Add one shared rolling file logging implementation in the shared code path so both app surfaces use the same behavior.
- Write launcher logs under the launcher root:
  - `logs/app.log`
  - `logs/host.log`
  - `logs/profiles/<profileId>.log`
- Keep the existing in-memory recent log buffer for UI responsiveness and SignalR updates.
- Tee all runtime and install output that currently only goes into `RuntimeStateStore` into the corresponding profile log file.
- Tee host/framework logs into `host.log`.
- Tee desktop lifecycle and client-side failure logs into `app.log`.
- Use bounded rolling retention:
  - active file size limit: `10 MiB`
  - archive count: `5`
- Use UTC timestamps in file output so desktop, host, and runtime events can be correlated safely.

### 2. Launch Flow Must Be Deterministic

- Remove silent vendor batch fallback as a normal launch path.
- Replace it with a launcher-owned launch assessment that either:
  - returns a valid direct Java launch plan, or
  - returns a blocking diagnostic explaining why the launcher cannot safely start the server
- If direct Java extraction fails, start must stop before process launch and surface a clear operator message in both desktop and web.
- Keep improving extraction support for common vendor batch layouts, but unresolved launchers must block instead of silently handing control back to the vendor batch file.
- Update install posture, overview summaries, and launch labels so runtime state becomes:
  - `Direct Java ready`, or
  - `Launch blocked`
- Remove wording that presents `vendor batch fallback` as an acceptable steady-state mode.

### 3. Structured Editing Must Tolerate Real Files Better

- Keep the current structured page scope:
  - `General`
  - `Sandbox`
  - `Mods & Maps`
  - `Network & Admin`
- Reduce unnecessary fallback by making the `.ini` and `SandboxVars.lua` document services more tolerant and more round-trip safe.
- Preserve:
  - comments
  - blank lines
  - unknown keys and tables
  - untouched formatting where possible
  - unrelated file content outside the owned structured fields
- Only force `Advanced Files` fallback when the file is genuinely unsafe to patch, such as:
  - broken structure
  - missing required root node
  - duplicate or ambiguous target keys
  - unsupported syntax that prevents safe ownership-aware editing
- Keep `Advanced Files` permanently available as the explicit escape hatch for unsupported or intentionally raw scenarios.

### 4. Draft Behavior Must Be Consistent Across Surfaces

- Use the existing SQLite-backed draft store as the only draft persistence mechanism.
- Make `General`, `Sandbox`, and `Mods & Maps` support real draft load, save, and discard behavior on both desktop and web.
- Fix web pages that currently claim to save drafts but only revalidate data without persisting it.
- Add missing host-side draft UX where the page metadata already marks drafts as supported.
- Keep `Network & Admin` no-draft on both surfaces because the page contains write-only secrets:
  - join password
  - RCON password
  - launcher admin bootstrap password
- Ensure all banners, buttons, and status text match the real behavior so the UI never claims draft persistence where none exists.

### 5. Overview And Posture Summaries Must Degrade Gracefully

- Stop using all-or-nothing summary fallback when one page or one request fails.
- Build profile posture from partial results when possible:
  - `Community`
  - `Server rules`
  - `Network`
  - `World`
  - `Sandbox tuning`
  - `Welcome`
- Replace generic `temporarily unavailable` text with concrete state such as:
  - host unavailable
  - structured fallback active
  - launch blocked
  - partial data loaded
- Keep both desktop and web summary surfaces aligned so the same backend condition produces the same operator meaning.

### 6. Owner Bootstrap Needs A Real Submission Flow

- Fix the owner bootstrap UX so the intended bootstrap surface presents:
  - editable owner fields
  - a clear create/save action
  - success/error feedback
- Remove dead-end status-only owner-bootstrap panels that imply setup can happen there when no submit path exists.
- Keep one clear source of truth for where owner bootstrap is allowed to happen:
  - if bootstrap remains desktop-only, web and host pages must say so explicitly and route the operator back to the desktop flow
  - if bootstrap is expanded beyond desktop later, the page must still expose an actual submit action instead of passive status text
- Add explicit CTA coverage from `Dashboard`, `Host`, and `Users` so pre-bootstrap operators can always find the working owner-creation path.
- Make the bootstrap surface save the entered owner information through the existing bootstrap endpoint and refresh launcher-visible owner state immediately after success.

### 7. Release And Installer Alignment

- Bump `PZServerLauncherVersion` to `0.3.0` before building release artifacts.
- Rebuild the MSI so installer metadata matches the tested code.
- Keep version increments SemVer-based after this milestone:
  - `0.3.1+` for backward-compatible fixes
  - `0.4.0` for the next backward-compatible operator-facing feature set
  - `1.0.0` only when the product is intentionally declared stable at that compatibility level

## Public Interfaces And Internal Contracts

- Add a shared launch assessment contract in Core planning so callers can distinguish `launchable` from `blocked` without silent fallback.
- Keep `SettingsPageDto.SupportsDrafts` as the contract source of truth and enforce it consistently in both desktop and web.
- Replace normal degraded-path use of `ProjectZomboidProfilePostureSummaryBuilder.Unavailable(...)` with partial-summary-aware composition.
- Keep existing raw file endpoints and live log APIs, but extend behavior so persistent on-disk logs and blocked launch diagnostics are surfaced through the current app flows.

## Test Plan

### Logging

- Add tests for rolling file creation and rotation.
- Add tests for app, host, and per-profile runtime/install log persistence.
- Add tests proving the in-memory recent-log buffer still behaves the same while logs are also written to disk.

### Launching

- Update planner tests so unsupported launchers no longer produce `VendorBatchFallback`.
- Add tests for blocked-launch diagnostics when direct Java extraction fails.
- Update install posture tests so unsupported launchers report `launch blocked` instead of `fallback active`.

### Structured Editing And Drafts

- Add `.ini` round-trip tests with comments, blank lines, and unknown keys preserved.
- Add `SandboxVars.lua` round-trip tests with preserved unknown tables and nested content.
- Add negative tests for malformed or ambiguous structured targets that must still force raw fallback.
- Add draft workflow tests for:
  - `General`
  - `Sandbox`
  - `Mods & Maps`
- Add explicit no-draft tests proving `Network & Admin` never persists write-only secrets.

### Summary And UI Behavior

- Add tests proving one page failure does not blank every overview summary segment.
- Verify desktop and web both surface:
  - launch blocked state
  - structured fallback state
  - real draft availability
  - persistent log file locations
  - a clear working owner-bootstrap action or an explicit redirect to the allowed bootstrap surface

### Release Verification

- Run `dotnet test PZServerLauncher.sln`.
- Run `scripts/test-installer.ps1`.
- Rebuild the installer after the version bump and validate the produced MSI matches `0.3.0`.

### Owner Bootstrap

- Add flow tests proving:
  - pre-bootstrap users can reach the intended owner-bootstrap action from the launcher UI
  - the bootstrap form has a real submit/create button
  - successful bootstrap immediately refreshes owner state in host/dashboard/users summaries
  - surfaces that cannot bootstrap directly do not pretend they can and instead show the correct next step

## Assumptions

- Email stays out of scope.
- Desktop and web are both in scope for this hardening pass.
- `Advanced Files` remains a permanent fallback and is not being removed.
- `Network & Admin` drafts remain disabled by design because of secret-bearing fields.
- Persistent logs live inside the launcher root, not `%LocalAppData%`.
- Follow-up fixes after this milestone start at `0.3.1`.
