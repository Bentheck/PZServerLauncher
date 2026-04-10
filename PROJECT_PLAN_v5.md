# Project Zomboid Manager UX Plan v5

## Summary

Rebuild the product into a real Project Zomboid server manager on both `Avalonia desktop` and optional `Blazor web`, using the existing host/runtime foundation and the existing `SQLite` persistence model. The desktop and web keep different authentication mechanisms, but they share one capability-based authorization source of truth. Structured settings become real round-trip editors backed by AST/document models, not whole-file rewrite.

This version closes the remaining reviewer gaps with four explicit decisions:
- `SQLite` remains the only app persistence store; config files on disk remain the source of truth for actual server settings
- one shared capability registry drives both endpoint authorization and UI visibility
- the `Classic` screen is migrated in-place with explicit dirty-state behavior and no dual-write overlap
- structured editor drafts are branch-scoped, catalog-versioned, and stored in SQLite

## Key Changes

### 1. Persistence Model

- Keep existing app persistence in `SQLite` for:
  - profiles
  - host settings
  - users
  - jobs/audit
  - new structured-editor drafts
- Do not introduce a second file-based app persistence layer.
- Actual Project Zomboid settings remain sourced from on-disk server config files:
  - `.ini`
  - `SandboxVars.lua`
  - other raw config files
- Add a draft persistence table, keyed by:
  - `ProfileId`
  - `Branch`
  - `CatalogId`
  - `CatalogVersion`
  - `PageId`
- Draft rows store:
  - serialized structured values for that page
  - dirty flag
  - last-synced file hash snapshot
  - updated timestamp
- `CommonConfigDto` remains as a legacy compatibility surface only during the migration period for the `Classic` page. New pages use structured settings DTOs.

### 2. Shared Auth / Capability Source Of Truth

- Keep authentication mechanisms as they are:
  - desktop: loopback bearer token mapped to `LocalSystem`
  - web: Identity cookie login with roles
- Replace policy-by-convention drift with one shared capability model:
  - introduce `Capability` enum
  - introduce `ICapabilityResolver`
  - introduce `CapabilityMatrix`
- Role/caller to capability mapping is centralized in one service:
  - `LocalSystem`: all capabilities
  - `Owner`: all web capabilities
  - `Admin`: config/mods/remote-access/install/runtime but not ownership transfer rules
  - `Operator`: runtime/install/backup/logs
  - `Viewer`: read-only pages
- Endpoint authorization uses capability requirements derived from that resolver.
- UI visibility also comes from the same resolved capability set returned by the host, not from hardcoded local role checks.
- Add a workspace bootstrap payload that includes:
  - current caller identity summary
  - resolved capabilities
  - enabled pages/actions for the current surface
- Desktop and web may still render differently, but page/action access must be capability-driven from the same host response.

### 3. Structured Settings With Round-Trip Safety

- Implement real document services for:
  - `Server INI`
  - `SandboxVars.lua`
- Each document service includes:
  - parser/tokenizer
  - AST/document model
  - formatter/writer
  - patch application layer that updates only owned structured fields
- Preservation rules are strict:
  - preserve comments
  - preserve blank lines
  - preserve section/key order
  - preserve unknown keys
  - preserve unsupported but parseable nodes
- Structured editing is disabled for a file when:
  - parse fails
  - unsupported syntax makes safe patching impossible
  - file version is newer than supported catalog/parser rules
- In those cases, the page shows a read-only explanation and routes the user to `Advanced Files`.
- `SpawnRegions.lua` and `SpawnPoints.lua` remain `Advanced Files` only in this phase.

### 4. Versioned Build 41 / Build 42 Catalogs

- Use separate catalogs for Build 41 and Build 42:
  - server catalog
  - sandbox catalog
- Every field has a stable ID and belongs to exactly one catalog/version.
- Catalogs are versioned artifacts with:
  - `CatalogId`
  - `CatalogVersion`
  - page/section/field layout
  - optional field alias map for migrated fields
- Draft behavior:
  - drafts are branch-scoped
  - a Build 41 draft is never applied to a Build 42 editor
  - a Build 42 draft is never applied to a Build 41 editor
- Branch switch rules:
  - switching branch immediately invalidates the active in-memory drafts for the old branch
  - old-branch drafts remain stored in SQLite, archived under their original branch/catalog version
  - the target branch loads from file state first, then target-branch draft if one exists and matches the active catalog version or a supported alias migration
  - no automatic semantic translation between Build 41 and Build 42 settings
- Catalog version change rules:
  - if alias migration exists, migrate draft forward
  - if no safe migration exists, discard the draft for that page and regenerate editor state from current files
  - actual config files are never rewritten just because a catalog version changed

### 5. Workspace Shell Migration And Cutover

- Add the new shell first with:
  - global nav: `Dashboard`, `Profiles`, `Host`, `Remote Access`, `Users`
  - per-profile nav: `Overview`, `Install & Update`, `General`, `Sandbox`, `Mods & Maps`, `Network & Admin`, `Backups`, `Logs`, `Advanced Files`, `Classic`
- `Classic` exists only during migration.
- Dirty-state rule is explicit:
  - every page, including `Classic`, owns its own dirty state
  - navigating away from a dirty page prompts `Save / Discard / Cancel`
  - no silent discard on route change
- Cutover rule is explicit:
  - when a new page fully replaces a feature area, the corresponding editable controls are removed from `Classic` in the same milestone
  - `Classic` must never remain a second editable surface for the same settings after replacement
- `Classic` retirement condition:
  - remove `Classic` only after `Overview`, `Install & Update`, `General`, `Sandbox`, `Mods & Maps`, `Network & Admin`, `Backups`, and `Logs` are live on desktop and mirrored on web
- During transition, existing lifecycle/install/backup flows continue to use current host operations underneath; only the shell and editor surfaces change.

## Public APIs / Types

- Add structured settings types:
  - `Capability`
  - `ResolvedCapabilitiesDto`
  - `WorkspaceBootstrapDto`
  - `SettingsCatalogDto`
  - `SettingsPageDto`
  - `SettingsSectionDto`
  - `SettingsFieldDto`
  - `SettingsValueSetDto`
  - `SettingsDraftDto`
  - `SettingsValidationResultDto`
  - `SettingsSaveResultDto`
- Add/replace host endpoints:
  - `GET /api/workspace/bootstrap`
  - `GET /api/profiles/{id}/settings/catalog`
  - `GET /api/profiles/{id}/settings/{page}`
  - `POST /api/profiles/{id}/settings/{page}/validate`
  - `PUT /api/profiles/{id}/settings/{page}`
  - `GET /api/profiles/{id}/settings/draft/{page}`
  - `PUT /api/profiles/{id}/settings/draft/{page}`
  - `DELETE /api/profiles/{id}/settings/draft/{page}`
- Keep raw config endpoints unchanged for `Advanced Files`.
- Keep existing runtime/install/backup endpoints unless the new shell needs response-shape additions.

## Delivery Order

1. Shared capability foundation and workspace bootstrap
   - add capability resolver
   - convert endpoint policy wiring to capability-backed enforcement
   - return resolved capabilities to both desktop and web

2. Shell migration scaffold
   - add new shell and routing
   - embed current screen as temporary `Classic`
   - add dirty-state guard behavior

3. Structured settings foundation
   - add AST/parser/writer services
   - add versioned catalog resolver
   - add SQLite draft storage

4. First product pages
   - `Overview`
   - `Install & Update`
   - `General`
   - desktop first, then web mirror
   - remove overlapping edit controls from `Classic`

5. Remaining structured product pages
   - `Sandbox`
   - `Mods & Maps`
   - `Network & Admin`
   - `Backups`
   - `Logs`
   - desktop first, then web mirror
   - continue shrinking `Classic`

6. Final cutover
   - move unsupported/raw-only content to `Advanced Files`
   - retire `Classic`
   - remove legacy `CommonConfigDto` editing path from the desktop UI surface

## Test Plan

- Persistence tests
  - drafts save/load from SQLite with branch/catalog/page keys
  - branch switch does not reuse incompatible drafts
  - catalog migration applies aliases only when declared
- Auth/capability tests
  - one resolver drives endpoint authorization
  - same resolved capability set drives UI bootstrap payload
  - desktop `LocalSystem` receives full capability set
  - web roles receive expected capability subsets
- Round-trip document tests
  - `.ini` preserves comments/order/unknown keys on structured save
  - `SandboxVars.lua` preserves supported/unsupported content when safe
  - unsupported syntax disables structured editor and falls back to raw
- Shell migration tests
  - dirty page prompts on navigation
  - `Classic` and new pages never both edit the same settings area at once
  - cutover removes replaced controls from `Classic`
- Product acceptance tests
  - create/import profile, land on `Overview`
  - install/update from structured page
  - edit `General` and `Sandbox` with safe round-trip persistence
  - manage mods/maps with presets and validation
  - repeat each completed feature area on web after desktop parity pass
- Regression tests
  - tray/minimize behavior
  - host shutdown controls
  - remote-access setup/self-test/firewall flow
  - installer upgrade preserves state and restarts host if previously running

## Assumptions And Defaults

- Desktop stays `Avalonia + .axaml + MVVM`.
- Web stays `Blazor`.
- Windows-only and same-machine hosting remain the supported scope.
- SQLite remains the sole app persistence store.
- Actual Zomboid config files remain the source of truth for server settings.
- Build 41 and Build 42 remain separate structured editor families.
- Raw editing remains as a permanent advanced fallback.
- No automatic cross-branch translation of structured settings drafts.
