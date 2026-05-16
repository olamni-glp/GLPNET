# Feature Specification: codeconv init + scaffold behind a pluggable language-pair registry

**Feature Branch**: `016-codeconv-init-scaffold-langpair`
**Created**: 2026-05-16
**Status**: Draft
**Input**: User description: "reimplement D2NET as codeconv python tools and skill wrappers — mostly renaming but also make source and target language changeable; allow an option for different source/target languages but for now implement the D2NET source→target pair (.dart → C#); ensure full unit, integration, and regression tests in place"

## Context & Decisions (resolved before drafting)

This feature ports the two load-bearing D2NET .NET tools (Init, Scaffold) into the existing `codeconv` Python tool package, and replaces their implicit Dart→C# coupling with an explicit, pluggable source→target **language-pair registry**. The following scope decisions were made by the project owner and are treated as fixed requirements, not open questions:

- **D1 — `D2Net.PgdbMigrate`: dropped.** It is a completed one-shot legacy `.D2NET/pgdb/` → `.pgdb/` migration, language-agnostic, a no-op after first success. It is NOT ported; its source is archived in place and excluded from scope.
- **D2 — `D2Net.BridgeClient`: retired, not ported.** Ported tools reuse the existing Python `codeconv.bridge_client.acquire_or_discover` (already used by discover/depgraph). The .NET bridge client is removed with the rest of `tools/d2net/`.
- **D3 — Init delegates inventory to `discover`.** The ported init tool does NOT re-implement source-tree scanning. It owns workspace configuration, exclusions, and conversion-phase tracking; the per-file inventory is produced by the existing `codeconv discover` (single source of truth).
- **D4 — Scaffold is tombstone-integrated, not merged into one tool.** Scaffold stays a distinct stage but reads the `codeconv` inventory/tombstones (not a D2NET `public.dart_files`) and records each produced target artefact path into the existing feature-015 tombstone `target_path` (the depgraph mark-* surface). A separate scaffold-tracking table is NOT created.
- **D5 — D2NET `public.*` tables move into the `codeconv` schema.** A new Alembic migration introduces `codeconv.workspace_settings`, `codeconv.excluded_directories`, `codeconv.phase_sequence`, `codeconv.phase_status`. `public.dart_files` and `public.scaffold_tracker` are not recreated (folded into `codeconv.dart_files` + tombstone `target_path`).
- **D6 — Pluggable language-pair registry; same `codeconv` package; de-branded.** Each `(source, target)` pair is a plugin exposing per-stage hooks; a master plugin package aggregates all per-stage plugins for one pair. The chosen pair is selected once, persisted in workspace settings, and **fixed for every stage**. Tools/skills are renamed `codeconv init` / `codeconv scaffold` and `/codeconv-init` / `/codeconv-scaffold` (consistent with `/codeconv-discover` / `/codeconv-depgraph`). The first and only registered pair in this feature is **Dart→C#**.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Initialize a conversion workspace for a language pair (Priority: P1)

An engineer preparing to convert the `glp_runtime_net/` Dart subtree to C# runs a single command to establish the conversion workspace: it records the chosen source→target language pair, validates the source/target locations, proposes and records directory exclusions, prepares the conversion-phase tracking state, and triggers the file inventory (delegated to discover). After this, the workspace "knows" it is a Dart→C# conversion and every later stage is bound to that pair.

**Why this priority**: Nothing else in the conversion pipeline can run without an initialized, language-pair-bound workspace. This is the foundation and a minimal end-to-end deliverable on its own (it produces a fully configured, inventoried workspace).

**Independent Test**: On a clean checkout, run the init command selecting the Dart→C# pair against `glp_runtime_net/`. Verify: workspace settings persist the pair and source/target; exclusions are proposed and stored; phase tracking is initialized; the file inventory exists (discover ran or was already current); a second init is idempotent; selecting an unregistered pair is rejected with an actionable error.

**Acceptance Scenarios**:

1. **Given** a clean repo with no workspace state, **When** the engineer initializes the workspace for the Dart→C# pair against `glp_runtime_net/`, **Then** the workspace settings record source=`glp_runtime_net`, the Dart→C# pair, and the target location; recommended exclusions are recorded; conversion-phase tracking is initialized; and the file inventory is populated by discover.
2. **Given** an already-initialized workspace, **When** the engineer re-runs init with the same inputs, **Then** the operation is idempotent (no duplicate state, no destructive change) and reports "already initialized".
3. **Given** an engineer requests an unregistered language pair (e.g. Dart→Rust), **When** init runs, **Then** it refuses with an actionable error naming the registered pairs, and writes no workspace state.
4. **Given** an engineer requests a destructive re-initialization (rebuild), **When** init runs, **Then** it requires explicit confirmation before discarding existing workspace state.

---

### User Story 2 - Scaffold the target tree for the chosen pair (Priority: P1)

With an initialized Dart→C# workspace, the engineer runs a single command that produces the target source tree: it mirrors the non-excluded source tree into the target location, applies the target language's file extension and per-file working-directory convention as defined by the chosen pair's plugin, advances the conversion-phase state, and records each produced target path back onto the corresponding tombstone so depgraph/conversion tracking and scaffolding share one per-file record.

**Why this priority**: Scaffolding is the second mandatory stage and the point where the target language plugin's output conventions take effect. Together with US1 it delivers the working "init → scaffold" slice that replaces the day-to-day D2NET workflow.

**Independent Test**: After US1 on the Dart→C# pair, run scaffold. Verify: the target tree mirrors the non-excluded source tree with C# extensions and the pair's working-directory convention; the conversion phase advances to "scaffold"; each scaffolded source file's tombstone carries the produced `target_path`; a re-run is idempotent; a destructive target overwrite requires explicit confirmation.

**Acceptance Scenarios**:

1. **Given** an initialized Dart→C# workspace, **When** scaffold runs, **Then** every non-excluded source file is mirrored into the target tree with the pair-defined target extension and working-directory convention.
2. **Given** scaffold completes, **When** the engineer inspects any scaffolded source file's tombstone, **Then** it records the produced target artefact path in the `target_path` field used by the conversion-tracking surface.
3. **Given** a scaffolded workspace with unchanged inventory, **When** scaffold runs again, **Then** the operation is idempotent (no spurious target churn, no duplicate phase rows).
4. **Given** a non-empty target location, **When** scaffold is asked to overwrite it, **Then** it requires explicit destructive confirmation before deleting/replacing target contents.

---

### User Story 3 - Switch/extend the source→target language pair (Priority: P2)

A maintainer wants the conversion toolchain to support a future language pair (e.g. a different source or target). They add one new language-pair plugin (its per-stage hooks plus a master registration) and register it; no stage tool (init, discover, depgraph, scaffold) source is modified. The new pair becomes selectable at init time; once a workspace selects it, every stage uses that pair's plugin and refuses to operate under a different pair.

**Why this priority**: The pluggability is the explicit reason for this feature beyond a rename. It must be demonstrable that a new pair slots in without touching stage tools, but the only pair shipped/validated here is Dart→C#.

**Independent Test**: Register a second trivial test-only pair via the registry; verify it appears as a selectable pair, that selecting it binds all stages to it, and that no stage-tool source file was edited to add it. Verify the shipped Dart→C# pair remains the only production pair and is end-to-end green.

**Acceptance Scenarios**:

1. **Given** a new language-pair plugin is registered, **When** an engineer lists available pairs, **Then** the new pair appears alongside Dart→C# with no edit to any stage tool.
2. **Given** a workspace initialized for one pair, **When** any stage tool runs and detects a different/unset pair than the workspace records, **Then** it refuses with an actionable error rather than producing mixed-pair output.

---

### User Story 4 - Manage exclusions on an existing workspace (Priority: P2)

An engineer refines which directories are in scope for conversion by adding or removing exclusions on an already-initialized workspace, and the inventory and downstream state stay consistent with the new exclusion set.

**Why this priority**: The D2NET workflow relies on incremental exclusion management (D2Net.Init's add/remove-exclude runners). It is needed for real use but the init→scaffold core (US1+US2) is the MVP.

**Independent Test**: On an initialized workspace, add an exclusion covering known source files, confirm those files leave the in-scope inventory; remove it, confirm they return; verify exclusions persist in workspace state.

**Acceptance Scenarios**:

1. **Given** an initialized workspace, **When** the engineer adds a directory exclusion, **Then** files under it are removed from the in-scope inventory and the exclusion persists.
2. **Given** an exclusion exists, **When** the engineer removes it, **Then** previously excluded files return to the in-scope inventory and the change persists.

---

### User Story 5 - Full Dart→C# pipeline regression (Priority: P2)

A CI/maintainer runs the complete conversion-readiness pipeline for the Dart→C# pair — init → discover → depgraph → scaffold — on the real `glp_runtime_net/` checkout and confirms the stages interoperate (shared inventory, shared tombstone records, consistent phase tracking) with no regression to the existing feature-012/-014/-015 behavior.

**Why this priority**: The brief explicitly requires full unit, integration, and regression coverage. This story guards the seams between the ported tools and the existing codeconv tools.

**Independent Test**: Run the four stages in order on `glp_runtime_net/`; assert each stage's success, that scaffold's `target_path` values are visible to the conversion-tracking surface, that phase tracking reflects the completed stages, and that the pre-existing discover/depgraph test suites remain green.

**Acceptance Scenarios**:

1. **Given** a clean checkout, **When** init → discover → depgraph → scaffold run in order for Dart→C#, **Then** all four succeed and share one consistent per-file record (inventory + tombstone + target path + phase state).
2. **Given** the pipeline has run, **When** the existing codeconv discover/depgraph regression suites run, **Then** they remain green (no behavioral regression to features 012/014/015).

### Edge Cases

- Init invoked with a source path outside the repo, a reserved name, or a non-existent directory → rejected with an actionable error, no state written.
- Init invoked when legacy D2NET `public.*` workspace state exists → the new `codeconv`-schema workspace state is authoritative; legacy `public.*` data is not consulted and not auto-migrated (workspace is re-established by running init).
- Scaffold invoked before init / before any inventory exists → refuses with an actionable error indicating the missing prerequisite stage.
- Scaffold invoked when the workspace's recorded language pair differs from the requested/registered pair → refuses (no mixed-pair output).
- A source file's tombstone is missing when scaffold tries to record its `target_path` → recorded as a warning; scaffold still produces the target file (tombstone refresh is a separate, idempotent step).
- An unregistered language pair is requested at init → refused; the registry's known pairs are listed.
- Destructive operations (workspace rebuild, target overwrite) without explicit confirmation → refused.
- Concurrent stage invocations against the same workspace → serialized via the existing bridge/locking mechanism; no corruption of workspace or inventory state.

## Requirements *(mandatory)*

### Functional Requirements

**Language-pair registry & plugins**

- **FR-001**: The system MUST provide a language-pair registry mapping a `(source, target)` identity to a language-pair plugin, with at least the Dart→C# pair registered.
- **FR-002**: A language-pair plugin MUST expose per-stage hooks sufficient for every stage that has language-specific behavior, at minimum: source file selection criteria and source tool-exclusion recommendations (init/discover), source dependency/import extraction (discover), and target file extension plus target working-directory/naming convention (scaffold).
- **FR-003**: The system MUST allow a new language pair to be added by registering one new plugin (its per-stage hooks plus a single master registration) WITHOUT modifying any stage tool's source.
- **FR-004**: The chosen language pair MUST be selected once (at init), persisted in workspace settings, and treated as fixed for every subsequent stage; a stage MUST refuse to run if the effective pair is unset or differs from the workspace-recorded pair.
- **FR-005**: Requesting an unregistered language pair MUST fail with an actionable error that names the registered pairs and writes no workspace state.

**Init tool (`codeconv init`, skill `/codeconv-init`)**

- **FR-006**: The system MUST provide an `init` tool that records workspace configuration (source location, selected language pair, target location) into `codeconv`-schema workspace settings.
- **FR-007**: The `init` tool MUST propose and persist recommended directory exclusions derived from the selected source language's tool conventions, and MUST support a non-interactive mode that accepts the suggested exclusions.
- **FR-008**: The `init` tool MUST initialize conversion-phase tracking state in the `codeconv` schema.
- **FR-009**: The `init` tool MUST obtain the per-file inventory by delegating to the existing discover capability rather than re-implementing source-tree scanning (single inventory source of truth).
- **FR-010**: The `init` tool MUST be idempotent for unchanged inputs and MUST require explicit confirmation before any destructive re-initialization.
- **FR-011**: The `init` tool MUST support adding and removing directory exclusions on an existing workspace, keeping the in-scope inventory consistent with the exclusion set.
- **FR-012**: The `init` tool MUST validate source/target locations (existence, in-repo, not a reserved name) and reject invalid input with actionable errors and no partial state.

**Scaffold tool (`codeconv scaffold`, skill `/codeconv-scaffold`)**

- **FR-013**: The system MUST provide a `scaffold` tool that mirrors the non-excluded source tree into the target location, applying the selected pair's target extension and working-directory/naming convention.
- **FR-014**: The `scaffold` tool MUST read the in-scope file set from the `codeconv` inventory/tombstones (NOT from a D2NET `public.dart_files` table).
- **FR-015**: The `scaffold` tool MUST record each produced target artefact path into the corresponding tombstone's `target_path` field (the feature-015 conversion-tracking surface); a missing tombstone for a scaffolded file MUST be a warning, not a failure.
- **FR-016**: The `scaffold` tool MUST advance conversion-phase tracking to reflect scaffold completion.
- **FR-017**: The `scaffold` tool MUST be idempotent for an unchanged inventory and MUST require explicit confirmation before any destructive overwrite of a non-empty target location, using staged writes so a failure does not leave a half-written target tree.
- **FR-018**: The `scaffold` tool MUST refuse to run if init/inventory prerequisites are absent or if the workspace's recorded pair does not match the registered/selected pair.

**Packaging, schema, skills**

- **FR-019**: The `init` and `scaffold` tools MUST live in the existing `codeconv` Python tool package and conform to the existing codeconv tool contract (auto-discovered tool exposing the standard tool surface; honoring the existing top-level workspace/data-dir/quiet/json conventions and the shared bridge client).
- **FR-020**: All persistent tables introduced by this feature MUST reside in the `codeconv` database schema via a new schema migration; no feature table may be created in `public`. The migration introduces workspace-settings, excluded-directories, phase-sequence, and phase-status tables; it MUST NOT recreate `public.dart_files` or a separate scaffold-tracking table.
- **FR-021**: Each ported tool MUST ship a thin slash-skill wrapper (`/codeconv-init`, `/codeconv-scaffold`) consistent with the existing `/codeconv-discover` / `/codeconv-depgraph` wrappers, preserving D2NET's destructive-operation confirmation behavior.
- **FR-022**: The legacy `D2Net.PgdbMigrate` and `D2Net.BridgeClient` MUST NOT be ported; the `tools/d2net/` .NET sources and the `D2NET-init` / `D2NET-scaffold` / `D2NET-pgdb-migrate` skills MUST be removed or archived so there is one (codeconv) toolchain, with documentation updated to point at the new commands.
- **FR-023**: The feature MUST NOT regress the existing feature-012/-014/-015 behavior: the discover and depgraph tools, the `codeconv` schema's existing tables, the tombstone format's existing keys, and their test suites remain unchanged in behavior.

**Testing**

- **FR-024**: The feature MUST ship unit tests for the registry and the Dart→C# plugin's per-stage hooks (selection, exclusions, target naming) with positive and negative controls.
- **FR-025**: The feature MUST ship integration tests for `init` and `scaffold` against the shared bridge (idempotence, destructive-confirmation, exclusion add/remove, pair-mismatch refusal, tombstone `target_path` recording).
- **FR-026**: The feature MUST ship a regression test exercising the full Dart→C# pipeline (init → discover → depgraph → scaffold) on a synthetic subtree and asserting cross-stage consistency, plus confirmation that the pre-existing discover/depgraph suites stay green.

### Key Entities *(include if feature involves data)*

- **Language-pair plugin**: Identified by a `(source, target)` pair (e.g. Dart→C#). Aggregates per-stage hooks: source selection criteria, source tool-exclusion recommendations, source dependency extraction, target extension, target working-directory/naming convention. A master registration bundles the per-stage pieces for one pair.
- **Language-pair registry**: The lookup from `(source, target)` to plugin; enumerates available pairs; the selection authority used by init and enforced by every stage.
- **Workspace settings**: Persistent `codeconv`-schema record of the selected pair, source location, target location, and tool options for one workspace. The single authority for "which pair is this workspace bound to".
- **Exclusion set**: Persistent `codeconv`-schema set of excluded directories (with origin: tool-suggested vs manual) that defines the in-scope file set.
- **Conversion-phase tracking**: Persistent `codeconv`-schema state recording phase ordering and per-phase status (e.g. scaffold IN_PROGRESS/complete).
- **Per-file conversion record**: The existing `codeconv` inventory row + tombstone for a source file, extended in use (not in shape) so scaffold writes its produced `target_path` into the existing feature-015 tombstone field shared with depgraph conversion tracking.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An engineer can take a clean checkout to a fully scaffolded Dart→C# target tree using only the two new commands (init, scaffold) plus discover/depgraph — with zero manual database or filesystem steps.
- **SC-002**: Re-running init or scaffold on an unchanged workspace produces zero state change (idempotent), verifiable by a no-diff check of workspace state, inventory, tombstones, and target tree.
- **SC-003**: Adding a new language pair requires editing only the new plugin/registration files — zero edits to any stage tool source — demonstrated by a registered second test pair.
- **SC-004**: 100% of feature requirements have automated coverage: unit (registry + Dart→C# hooks), integration (init/scaffold behaviors), and a full-pipeline regression, all green.
- **SC-005**: The pre-existing discover/depgraph test suites remain green after this feature (no regression to features 012/014/015).
- **SC-006**: After the feature lands there is exactly one conversion toolchain: no `D2NET-*` skill and no `tools/d2net/` .NET binary is required for or referenced by the documented Dart→C# workflow.
- **SC-007**: Every scaffolded source file's produced target path is retrievable from its tombstone's conversion-tracking field, verified across the full `glp_runtime_net/` subtree.
- **SC-008**: A stage invoked against a workspace bound to a different language pair refuses with a non-zero, actionable result in 100% of mismatch cases (no mixed-pair output is ever produced).

## Assumptions

- This feature builds on the unmerged feature-015 branch (`015-codeconv-depgraph`); the tombstone `target_path` / conversion keys and the depgraph mark-* surface from feature 015 are available as the conversion-tracking substrate for D4/FR-015.
- The unified PGLite bridge and `.pgdb/` cluster (feature 012), the `--data-dir` override (feature 013, required on this exFAT checkout), and the shared `codeconv.bridge_client` are reused; no new bridge or .NET bridge client is introduced.
- D2NET workspace state is transient build scaffolding; legacy `public.*` D2NET data is not migrated — a workspace is re-established by running the new `init`. No data-migration tool is in scope (D1).
- The only production language pair delivered and validated in this feature is Dart→C#; the registry is proven extensible via a test-only second pair, but no other production pair is implemented.
- "Source/target language changeable" means: changeable by selecting a different *registered* pair at init time; it does not mean automatic transpilation of arbitrary languages.
- The default discover subtree (`glp_runtime_net/`) and the existing exclusion conventions (`.dart_tool/`, `build/`, etc.) carry over as the Dart side of the Dart→C# plugin.
- Existing codeconv top-level conventions (workspace root, data-dir override, quiet/json, exit-code semantics, schema isolation in `codeconv`) are inherited unchanged.
