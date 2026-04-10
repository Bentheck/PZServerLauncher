# Project Zomboid Manager Master Implementation Plan v6

## Summary

Turn the current infrastructure-heavy shell into a real Project Zomboid server installer, configurator, and launcher on both `Avalonia desktop` and optional `Blazor web`, while preserving the existing host/runtime foundation.

This plan is both:
- the `product/source-of-truth spec`
- the `parallel implementation plan`

The product target is:
- `desktop + web parity`
- `desktop-first within each milestone, web mirrored before the milestone is complete`
- `Build 41` and `Build 42` as separate structured editor families
- `SQLite` as the only app persistence store
- Project Zomboid config files on disk as the source of truth for actual server settings
- raw file editing kept permanently as an advanced fallback

The implementation target is:
- `4 parallel streams`
- `milestone-gated integration`
- strict file ownership to avoid merge collisions

## Product Design

### Workspace And Navigation

Replace the current single large desktop screen with a workspace shell on both desktop and web.

Global navigation:
- `Dashboard`
- `Profiles`
- `Host`
- `Remote Access`
- `Users`

Per-profile navigation:
- `Overview`
- `Install & Update`
- `General`
- `Sandbox`
- `Mods & Maps`
- `Network & Admin`
- `Backups`
- `Logs`
- `Advanced Files`
- `Classic` during migration only

Desktop uses Avalonia page/view composition with MVVM.
Web uses Blazor pages/components with matching route names and page responsibilities.

### Product Pages

`Overview`
- runtime state
- branch/build
- install state
- latest logs
- backup summary
- quick actions

`Install & Update`
- branch selection
- install directory
- detected server state
- preflight results
- install/update actions
- streamed job progress

`General`
- server identity
- server name
- memory
- startup behavior
- basic host/server knobs
- primary ports

`Sandbox`
- near-full structured gameplay/world settings
- separate Build 41 and Build 42 page families

`Mods & Maps`
- add workshop items by URL or ID
- scan local workshop content
- discover mod IDs and map folders
- ordering for workshop/mods/maps
- named presets
- duplicate/missing item checks
- local dependency hints only
- map validation

`Network & Admin`
- bind IP
- admin/RCON settings
- access/security-related server options modeled from server config

`Backups`
- list backups
- create manual backup
- restore flow
- retention summary

`Logs`
- live runtime stream
- recent logs
- host/runtime messages

`Advanced Files`
- raw `.ini`
- raw `SandboxVars.lua`
- raw `spawnregions.lua`
- raw `spawnpoints.lua`
- used whenever structured editing is unsupported or intentionally out of scope

### Build 41 / Build 42 Model

Use separate structured catalogs:
- `Build41ServerCatalog`
- `Build41SandboxCatalog`
- `Build42ServerCatalog`
- `Build42SandboxCatalog`

Do not try to build one shared field list with field hiding.

Every structured field has:
- stable field ID
- catalog ID
- catalog version
- page/section ownership
- target file and key path
- control type
- value type
- validation rules
- default value
- help text
- restart-required flag

Stable ID format:
- `b41.server.<section>.<field>`
- `b41.sandbox.<section>.<field>`
- `b42.server.<section>.<field>`
- `b42.sandbox.<section>.<field>`

### Structured Config Editing

Implement real round-trip-safe document services for:
- `Server INI`
- `SandboxVars.lua`

Each service includes:
- tokenizer/parser
- AST/document model
- writer/formatter
- patch layer that updates only structured fields owned by the active catalog

Preservation rules:
- preserve comments
- preserve blank lines
- preserve section order
- preserve key order
- preserve unknown keys
- preserve unsupported but parseable nodes

Structured editing is disabled when:
- parse fails
- unsupported syntax makes safe patching impossible
- the file version is newer than supported parser/catalog logic

In those cases:
- show a read-only explanation in the structured page
- route the user to `Advanced Files`

`SpawnRegions.lua` and `SpawnPoints.lua` remain advanced/raw only in this phase.

### Persistence Model

Keep existing app persistence in `SQLite` for:
- profiles
- host settings
- users
- jobs/audit
- structured editor drafts

Do not introduce a second app-level persistence system.

Project Zomboid config files remain the source of truth for actual server settings.

Add a draft persistence store in SQLite keyed by:
- `ProfileId`
- `Branch`
- `CatalogId`
- `CatalogVersion`
- `PageId`

Draft data stores:
- serialized structured values for that page
- dirty flag
- last-synced file hash snapshot
- updated timestamp

`CommonConfigDto` remains only as a temporary legacy compatibility surface for the `Classic` migration path. New pages use structured settings DTOs.

### Branch Switch And Draft Rules

Drafts are branch-scoped.
A Build 41 draft is never applied to a Build 42 editor.
A Build 42 draft is never applied to a Build 41 editor.

Branch switch behavior:
- switching branch invalidates active in-memory drafts for the old branch
- old drafts remain stored in SQLite under their original branch/catalog version
- the target branch loads from file state first
- then applies a target-branch draft only if it matches the current catalog version or a declared alias migration

Catalog version change behavior:
- if alias migration exists, migrate draft forward
- if no safe migration exists, discard the draft for that page and regenerate from files
- catalog version changes never rewrite config files by themselves

There is no automatic semantic translation between Build 41 and Build 42 settings.

### Auth And Authorization

Keep auth mechanisms as they are:
- desktop: loopback bearer token mapped to `LocalSystem`
- web: Identity cookie login with roles

Add one shared capability-based source of truth:
- `Capability` enum
- `CapabilityMatrix`
- `ICapabilityResolver`

Role/caller mapping:
- `LocalSystem`: all capabilities
- `Owner`: full web capability set
- `Admin`: config/mods/remote-access/install/runtime but not ownership-transfer-only actions
- `Operator`: runtime/install/backup/logs
- `Viewer`: read-only pages

Endpoint authorization must derive from capabilities.
UI page/action visibility must also derive from the same resolved capabilities returned by the host.

Add a workspace bootstrap payload that includes:
- caller identity summary
- resolved capabilities
- enabled pages/actions for the current surface

Desktop and web may render differently, but page/action access must come from the same capability model.

### Shell Migration And Cutover

Migration is staged, not all-at-once.

1. Add the new shell and route/frame system.
2. Host the current monolithic experience inside the new shell as `Classic`.
3. Replace feature areas one by one with new pages.
4. Remove the overlapping editable controls from `Classic` in the same milestone that a new page replaces them.
5. Remove `Classic` only after all primary pages are live on desktop and mirrored on web.

Dirty-state rules:
- every page, including `Classic`, owns its own dirty state
- leaving a dirty page prompts `Save / Discard / Cancel`
- no silent discard on navigation

Cutover rules:
- `Classic` must never remain a second editable surface for an area already replaced
- during transition, existing host operations remain the backend for install/update/runtime/backup; only the shell and editing surfaces change

## Public APIs And Types

Add shared structured-settings and workspace contracts:
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

Add host endpoints:
- `GET /api/workspace/bootstrap`
- `GET /api/profiles/{id}/settings/catalog`
- `GET /api/profiles/{id}/settings/{page}`
- `POST /api/profiles/{id}/settings/{page}/validate`
- `PUT /api/profiles/{id}/settings/{page}`
- `GET /api/profiles/{id}/settings/draft/{page}`
- `PUT /api/profiles/{id}/settings/draft/{page}`
- `DELETE /api/profiles/{id}/settings/draft/{page}`

Keep existing raw config endpoints unchanged for `Advanced Files`.
Keep existing runtime/install/backup endpoints unless response-shape additions are required by the workspace.

Add internal services:
- `IIniDocumentService`
- `ISandboxVarsDocumentService`
- `ISettingsCatalogResolver`
- `IStructuredSettingsService`
- `ICapabilityResolver`

## Parallel Execution Model

### Streams

Use `4 parallel streams`.

#### Stream 1. Desktop Workspace
Owns:
- `src/PZServerLauncher.App/**`

Builds:
- desktop shell
- route/page frame
- temporary `Classic` page host
- dirty-state prompts
- desktop pages for all target areas
- tray/minimize and host controls remain intact

Must not edit:
- `src/PZServerLauncher.Contracts/**`
- `src/PZServerLauncher.Host/**`
- migrations

#### Stream 2. Structured Settings Core
Owns:
- `src/PZServerLauncher.Core/**`
- `src/PZServerLauncher.Infrastructure/**`
- parser/catalog tests in `tests/PZServerLauncher.Tests/**`

Builds:
- `.ini` parser/AST/writer
- `SandboxVars.lua` parser/AST/writer
- patch/merge layer
- Build 41/42 catalogs
- field IDs, catalog IDs, catalog versions, alias maps
- unsupported syntax/version fallback rules

Must not edit:
- `src/PZServerLauncher.App/**`
- `src/PZServerLauncher.Host/Program.cs`
- DB schema/migrations
- contracts after interface freeze

#### Stream 3. Host APIs, Capabilities, Drafts
Owns:
- `src/PZServerLauncher.Contracts/**`
- `src/PZServerLauncher.Host/Program.cs`
- `src/PZServerLauncher.Host/Services/**`
- `src/PZServerLauncher.Host/Data/**`
- EF migrations
- host API/integration tests

Builds:
- capability resolver and bootstrap payload
- endpoint authorization model
- SQLite-backed draft persistence
- structured settings endpoints
- orchestrators that use Stream 2 services
- branch-scoped draft/version migration handling
- final removal of legacy `CommonConfigDto` editing path

Must not edit:
- desktop views/viewmodels
- web page markup except minimal route wire-up if unavoidable

#### Stream 4. Web Workspace
Owns:
- `src/PZServerLauncher.Host/Components/**`
- web-only styling/layout

Builds:
- web shell
- matching route structure
- capability-driven page/action visibility
- web versions of all target pages
- temporary `Classic` page only if needed for migration parity

Must not edit:
- `Program.cs`
- services/data
- contracts
- parser/catalog code

### Shared Ownership Rules

- `Contracts` only Stream 3
- `Program.cs` only Stream 3
- `Host/Data` and migrations only Stream 3
- `Host/Components` only Stream 4
- `App` only Stream 1
- structured parser/catalog logic only Stream 2

If a stream needs a shared contract change after interface freeze, it requests it through Stream 3 at the next gate rather than editing it directly.

### Branching

Create:
- `codex/ux-v5-integration`

Create stream branches off it:
- `codex/ux-desktop-workspace`
- `codex/ux-structured-settings-core`
- `codex/ux-host-capabilities-api`
- `codex/ux-web-workspace`

Policy:
- no direct merges between stream branches
- all merges go into integration
- no merge to `main` until final gate passes

### Integration Style

Use `milestone gates`, not continuous free-form merging.

Each stream may commit freely inside its own branch.
Integration happens only at milestone boundaries after tests pass.

## Milestones

### Milestone 0. Interface Freeze
Short, mostly serial.

Stream 3 defines:
- capability contracts
- workspace bootstrap DTOs
- structured settings DTOs
- draft DTOs

Stream 2 defines:
- catalog interfaces
- parser/writer interfaces
- branch/catalog version contracts

Gate:
- contracts compile
- host compiles with placeholders
- page IDs and route names are frozen
- no unresolved ownership conflicts

### Milestone 1. Shell + Foundations
Parallel.

- Stream 1: desktop shell, route frame, `Classic`, dirty-state framework
- Stream 2: `.ini` and `SandboxVars.lua` AST foundations, catalog framework, versioning primitives
- Stream 3: capability resolver, workspace bootstrap endpoint, SQLite draft schema/migration, settings endpoint skeletons
- Stream 4: web shell, route frame, capability-driven nav scaffold

Gate:
- desktop and web shells render with matching IA
- bootstrap returns capabilities and enabled pages
- draft table exists and migration passes
- parser/catalog unit tests pass
- old desktop flow still reachable through `Classic`

### Milestone 2. First Product Pages
Parallel.

- Stream 1: desktop `Overview`, `Install & Update`, `General`
- Stream 2: structured field ownership maps for `General`
- Stream 3: host read/validate/save flow for `General`, draft save/load, file-hash sync rules
- Stream 4: web `Overview`, `Install & Update`, `General`

Gate:
- `General` edits round-trip safely
- no overlapping editable `General` controls remain in `Classic`
- desktop and web use the same page IDs and DTOs
- create/import profile, install/update, and `General` edit/save work end-to-end

### Milestone 3. Sandbox
Parallel.

- Stream 1: desktop `Sandbox`
- Stream 2: Build 41/42 sandbox catalogs, field maps, alias migration rules
- Stream 3: sandbox validation/save/draft orchestration, unsupported-file fallback contract
- Stream 4: web `Sandbox`

Gate:
- sandbox edits are branch-specific
- branch switch does not reuse incompatible drafts
- unsupported syntax disables structured editing and routes to `Advanced Files`
- no overlapping editable sandbox controls remain in `Classic`

### Milestone 4. Mods & Maps
Parallel.

- Stream 1: desktop `Mods & Maps`
- Stream 2: mod/map validation models and supporting catalog integration
- Stream 3: presets, ordered collections, scan/result endpoints, dependency-hint contracts
- Stream 4: web `Mods & Maps`

Gate:
- presets save/load
- ordering and validation work
- map mismatch and missing-item checks work
- no overlapping editable mods/maps controls remain in `Classic`

### Milestone 5. Remaining Pages And Cutover
Parallel.

- Stream 1: desktop `Network & Admin`, `Backups`, `Logs`, `Advanced Files`, final shell polish
- Stream 2: final structured ownership maps and fallback coverage
- Stream 3: endpoint cleanup, remove legacy `CommonConfigDto` editing path, finalize capability enforcement
- Stream 4: web versions of remaining pages

Gate:
- all target pages exist on desktop and web
- `Classic` removed
- legacy editable path removed
- full regression suite passes

## Test Plan

### Stream-Level
Stream 1:
- dirty-state navigation prompts
- page routing and `Classic` coexistence
- no duplicate editable controls after each cutover

Stream 2:
- `.ini` round-trip preservation
- `SandboxVars.lua` round-trip preservation
- unsupported syntax fallback
- catalog ID/version/alias migration tests

Stream 3:
- capability resolution and endpoint authorization
- SQLite draft persistence
- branch-scoped draft behavior
- catalog-version migration behavior

Stream 4:
- web page visibility follows resolved capabilities
- route parity with desktop
- page load/save flows against the same host APIs

### Gate-Level End To End
- import/create profile to `Overview`
- install/update flow
- edit/save `General`
- edit/save `Sandbox`
- manage mods/maps presets
- branch switch with draft isolation
- fallback to `Advanced Files` on unsupported structured parse

### Regression
- tray/minimize behavior
- host shutdown controls
- remote-access setup/self-test/firewall flow
- installer upgrade preserves state and restarts host if previously running

## Assumptions

- Desktop stays `Avalonia + .axaml + MVVM`
- Web stays `Blazor`
- Windows-only and same-machine hosting remain the scope
- SQLite remains the sole app persistence store
- actual Zomboid config files remain the source of truth for server settings
- Build 41 and Build 42 remain separate structured editor families
- raw editing remains a permanent advanced fallback
- no automatic cross-branch translation of structured settings drafts
- when behavior questions arise during implementation, this master plan is the source of truth
