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
- **FR-004**: The chosen language pair MUST be selected once (at init), persisted in workspace settings, and treated as fixed for every subsequent stage; a stage that requires a workspace (e.g. `scaffold`) MUST refuse to run if the effective pair is unset or differs from the workspace-recorded pair. Carve-out for backward compatibility: `discover` MAY run without an initialised workspace by falling back to the default registered pair (Dart→C#) so pre-016 `/codeconv-discover` usage is unaffected; once a workspace IS initialised, `discover` too is bound to its recorded pair. Any explicit per-invocation pair override that disagrees with an initialised workspace MUST be refused (no mixed-pair output).
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

---

## Amendment 1 (2026-05-17): `codeconv mirror` stage — pluggable source-tree scaffold

**Status**: Approved by project owner 2026-05-17 (this conversation). This amendment is additive and authoritative; it does **not** renumber or change the behaviour of FR-001…FR-026 except the two explicitly-amended requirements (FR-009, FR-012) quoted below.

### Motivation & decision

`codeconv scaffold` (US2 / FR-013…FR-018) mirrors the **inventory subtree** (`glp_runtime_net/`) into the C# **target tree** (`out/csharp`). Nothing in the codeconv toolchain *produces* `glp_runtime_net/` itself — that was the job of the now-removed D2NET `d2net-scaffold` (spec `001-d2net-scaffold`). `glp_runtime_net/` is gitignored regenerable output (`.gitignore:27` "d2net-scaffold output (regenerable; do not commit)"), so it is absent in every fresh checkout/worktree, which blocks the whole pipeline.

- **D7 — Add a distinct `mirror` stage that reproduces `001-d2net-scaffold` exactly, generically, via the language-pair registry.** `codeconv mirror` walks the **source-language tree** (`glp_runtime/` for Dart→C#) and produces the inventory subtree (`glp_runtime_net/`) with the source-file preservation, companion-artifact stubs, and root tracker JSON defined by spec `001-d2net-scaffold`, with every language-specific value supplied by the chosen pair's plugin. The names `scaffold` / `/codeconv-scaffold` remain bound to US2 (the `glp_runtime_net → out/csharp` stage); the new stage is `codeconv mirror` / `/codeconv-mirror`. This is the single, permanent, language-extensible mechanism — the obsolete D2NET `d2net-scaffold` is **not** revived.

### Stage ordering (amended pipeline)

`init` → **`mirror`** → `discover` → `depgraph` → `scaffold`.

`init` configures the workspace (pair + source/target paths + phase tracking) and is the **sole** authority for pair selection (D6/FR-004). `mirror` reads the bound pair from `codeconv.workspace_settings` (it is therefore **not** fs-only: it does one read-only bridge/DB lookup to obtain the pair). Because `mirror` produces the configured source subtree that `init`'s inventory delegation (FR-009) and `discover` consume, `init` must tolerate that subtree not yet existing — see the FR-009/FR-012 amendments.

### Amended requirements

**FR-009 (amended).** Original: *"The `init` tool MUST obtain the per-file inventory by delegating to the existing discover capability rather than re-implementing source-tree scanning (single inventory source of truth)."* Amended: the `init` tool MUST obtain the per-file inventory by delegating to discover **when the configured source subtree exists**. When the configured source subtree does **not** yet exist (it is produced by a later `mirror` run), `init` MUST persist the workspace configuration and phase tracking, **defer** the inventory, and emit an actionable warning (e.g. *"configured source `<path>` absent — run `codeconv mirror` then `codeconv discover`"*) — it MUST NOT hard-fail. Inventory remains a single source of truth (still produced only by discover, just deferred).

**FR-012 (amended).** Original: *"The `init` tool MUST validate source/target locations (existence, in-repo, not a reserved name) and reject invalid input with actionable errors and no partial state."* Amended: `init` MUST still reject a source/target that is out-of-repo, a reserved name, or otherwise malformed, with an actionable error and no partial state. The single carve-out: a configured source path that is well-formed and in-repo but **does not yet exist** is NOT rejected — it is downgraded to the FR-009 deferred-inventory warning (the path is legal; it will be produced by `mirror`).

### User Story 6 — Mirror the source-language tree into the inventory subtree (Priority: P1)

With a workspace initialised for a pair (US1), the engineer runs one command that walks the pair's source-language tree and produces the inventory subtree: directory layout mirrored, non-source files byte-identical, every source file preserved with the pair's preserved-suffix, the pair's companion-artifact stubs generated per source file, and the pair's root tracker JSON written. After this, `discover` has a subtree to inventory and the rest of the pipeline proceeds.

**Why this priority**: Without it the pipeline cannot start on any fresh checkout/worktree (the inventory subtree does not exist). It is the missing first stage that makes "regenerate per worktree" possible.

**Independent Test**: On a worktree with `glp_runtime/` present and `glp_runtime_net/` absent, run `init` (Dart→C#) then `mirror`; verify the spec-`001` acceptance set holds (mirrored dirs, byte-identical non-source files, verbatim `.dart` copies — Option 1, see FR-032 — nine companion stubs per Dart file with the spec-`001` extensions, root `d2net-tracker.json` with one record per Dart file); a re-run refuses without `--refresh`; `--refresh` preserves companions and the tracker; a colliding companion name aborts pre-flight with no target writes.

**Acceptance Scenarios**:

1. **Given** an initialised Dart→C# workspace, `glp_runtime/` present, configured source `glp_runtime_net/` absent, **When** `mirror` runs, **Then** `glp_runtime_net/` is created per spec-`001` FR-002…FR-010 with the FR-032 Option-1 deviation (mirrored non-pruned dirs; non-source files byte-identical; each `*.dart` preserved **verbatim under its original `.dart` name**; nine companion stubs `.cs .ana .tst .con .dep .cgn .iss .sta .ver` each with a `// TODO:` line; root `d2net-tracker.json` with one record per Dart file, nine companions each status `todo`).
2. **Given** `mirror` has run, **When** it is re-run without `--refresh`, **Then** it refuses (target exists) and changes nothing; **When** re-run with `--refresh`, **Then** `.dart.src`/non-source files are rewritten from current source, newly-found Dart files get fresh companion stubs, every pre-existing companion file and the tracker are left byte-identical (spec-`001` FR-011).
3. **Given** a source folder where a generated companion name would collide with a pre-existing non-source file, **When** `mirror` runs, **Then** it reports all collisions, exits non-zero, and writes nothing to the target (spec-`001` FR-012).
4. **Given** no initialised workspace (or a workspace whose pair is unset/unregistered), **When** `mirror` runs, **Then** it refuses with an actionable error ("run `codeconv init` first" / lists registered pairs) and writes nothing — pair selection is solely through `init` (D6/FR-004).
5. **Given** the configured target is the same as or nested inside the source, **When** `mirror` runs, **Then** it refuses (spec-`001` FR-014).

### New Functional Requirements (mirror)

The `mirror` tool MUST reproduce spec-`001-d2net-scaffold` FR-002…FR-014 **verbatim in behaviour**, with every language-specific value obtained from the workspace-bound pair's plugin (no behaviour hard-coded in the stage tool):

- **FR-027**: Provide a `mirror` tool (`codeconv mirror`, skill `/codeconv-mirror`) in the existing `codeconv` package, conforming to the feature-012 tool contract and inheriting top-level conventions, with command tree `codeconv mirror [run]`.
- **FR-028**: `mirror` MUST resolve the language pair **solely** from `codeconv.workspace_settings` (set by `init`); if no workspace/pair is set it MUST refuse with "run `codeconv init` first", and if the recorded pair is unregistered it MUST refuse listing registered pairs (D6/FR-004/FR-005). It performs exactly one read-only bridge/DB lookup for this; it makes no `codeconv`-schema writes and does not touch phase tracking (phase tracking is workspace state owned by init/discover/scaffold).
- **FR-029**: `mirror`'s **input** (source-language tree root) is the workspace setting **`mirror_source_root`** (recorded by `init` via `--mirror-source`, default `glp_runtime`); `mirror`'s **output root** is the workspace `source_path` (the inventory subtree, e.g. `glp_runtime_net` — unchanged spec-016 meaning, still what `discover`/`scaffold` consume). `mirror` MUST refuse if the output root is the same as or nested inside the input root (spec-`001` FR-014). `init` MUST record `mirror_source_root` into `codeconv.workspace_settings` and validate it is in-repo / not a reserved name (a not-yet-existing but well-formed in-repo `mirror_source_root` is legal — it normally *does* exist; FR-012's deferral applies to the configured `source_path`, the mirror *output*, not its input).
- **FR-030**: `mirror` MUST traverse the source tree recursively in deterministic order, pruning directories whose name is in the **effective** mirror-prune set; pruned directories MUST NOT appear in the output and their contents MUST NOT be processed (spec-`001` FR-002). The effective set = the pair's standard set (Dart→C#: `.dart_tool`, `build`, `archive`, `backup`, `.git`, `.idea`, `.vscode` — `build`/`archive`/`backup` are pruned as standard per owner decision 2026-05-17, since the curated `glp_runtime_net` excluded `bin/archive` etc. and mirroring them carries dangling archive imports the feature-015 `depgraph` referential check rejects) **minus** any workspace force-includes and **plus** any workspace gitignore-style exclusions (FR-042/FR-043).
- **FR-031**: Every non-source file MUST be copied to the mirrored relative path byte-for-byte identical (spec-`001` FR-003). A "source file" is one whose name ends with one of the pair's `source_extensions()`.
- **FR-032**: Every source file MUST be preserved at the mirrored relative path with the pair's `preserved_source_suffix()` appended, byte-identical to the source. **Option 1 deviation from spec-`001` FR-004 (the single deliberate one):** spec-`001` renames `foo.dart`→`foo.dart.src`, but unlike standalone D2NET the codeconv pipeline runs `discover` *after* `mirror`, and `discover` detects Dart source by the `.dart` extension — a `.dart.src` file would be invisible to it (empty inventory → dead pipeline). So `dart_csharp.preserved_source_suffix()` is `""`: the source is mirrored verbatim under its original `.dart` name. The 9 companion stubs and the root tracker (FR-033/FR-034) are still produced — the full substantive spec-`001` behaviour. (A pair whose downstream consumer does not read the source extension MAY return a non-empty suffix; the hook stays pair-defined.)
- **FR-033**: For every source file, `mirror` MUST create one companion stub per extension in the pair's `companion_extensions()` (Dart→C#: the nine `.cs .ana .tst .con .dep .cgn .iss .sta .ver`), named by replacing the trailing source extension, each containing the pair's single-line companion stub comment (Dart→C#: a `// TODO:` C-style line naming the file and category) (spec-`001` FR-005/FR-006).
- **FR-034**: `mirror` MUST write a single root tracker file named by the pair's `tracker_filename()` (Dart→C#: `d2net-tracker.json`) at the output root containing an array with exactly one record per source file; each record identifies the source file by its output-root-relative preserved (`.src`) path and lists every companion (filename + status) with status drawn from the closed enumeration `{todo,in-progress,done,blocked}` initialised to `todo` (spec-`001` FR-007…FR-010).
- **FR-035**: When the output root already exists, `mirror` MUST refuse by default (report, leave it untouched) and MUST support `--refresh` with spec-`001` FR-011 semantics: rewrite preserved-source and non-source files from current source; create companion stubs only for newly-discovered source files; leave every pre-existing companion file and the tracker file byte-identical; report newly-discovered source files in the summary.
- **FR-036**: Before writing anything, `mirror` MUST run a pre-flight pass detecting every case where a companion file would collide with a pre-existing non-source file of the same name in the same folder; on any collision it MUST report the full list, exit non-zero, and leave the output tree entirely unwritten (spec-`001` FR-012).
- **FR-037**: `mirror` MUST stage its writes (sibling staging dir, atomic move into place) so a failure leaves the live output tree untouched, and MUST emit a human-readable stdout summary (counts: dirs created, non-source copied, source preserved, companions generated, tracker records) and honour `--quiet`/`--json` (spec-`001` FR-013 + feature-016 staged-write/idempotence conventions).
- **FR-038**: Adding mirror support for a new language pair MUST require editing only that pair's plugin package + the registry auto-import line — **zero** edits to any stage tool (extends SC-003 to the mirror stage).

### Langpair plugin contract additions (mirror hooks)

`contracts/langpair_plugin_contract.md` is extended with mirror-side hooks on `LangPair` (additive; existing hooks unchanged). All MUST be pure/side-effect-free (filesystem read at most), unit-testable without `@needs_bridge`:

- `mirror_prune_segments() -> tuple[str, ...]` — the pair's **standard** directory names pruned during the mirror walk. Dart→C#: `(".dart_tool","build","archive","backup",".git",".idea",".vscode")` (spec-`001` FR-002 base set extended with `archive`/`backup` per owner decision 2026-05-17; `build` already in the base; intentionally independent of discover's walker `_EXCLUDED_SEGMENTS`). The *effective* prune set is this standard set adjusted by the workspace force-includes / gitignore-style exclusions of FR-042/FR-043.
- `preserved_source_suffix() -> str` — appended to a source filename for its preserved copy. Dart→C#: `".src"`.
- `companion_extensions() -> tuple[str, ...]` — companion-artifact extensions generated per source file. Dart→C#: `(".cs",".ana",".tst",".con",".dep",".cgn",".iss",".sta",".ver")`.
- `companion_stub_comment(companion_ext: str, source_basename: str) -> str` — the single-line stub body for a companion. Dart→C#: `// TODO: <ext-category> — port from <source_basename>`.
- `tracker_filename() -> str` — the root tracker filename. Dart→C#: `"d2net-tracker.json"` (kept literal for spec-`001` behavioural fidelity; pair-defined so other pairs differ).

### Testing (mirror)

- **FR-039**: Unit tests for the Dart→C# mirror hooks (exact-value asserts + negative controls), pure/no-bridge.
- **FR-040**: Integration tests for `mirror`: fresh-run spec-`001` acceptance set on a synthetic source tree; idempotent refuse-existing; `--refresh` preserves companions+tracker and rewrites src/non-source; collision pre-flight abort (no writes); pair-unset/unregistered refusal; target-nested-in-source refusal.
- **FR-041**: A full-chain regression `init → mirror → discover → depgraph → scaffold` on a synthetic subtree asserting cross-stage consistency, plus confirmation the pre-existing discover/depgraph/scaffold suites stay green (no regression to features 012/014/015 or US1–US5).

### Success criteria (mirror)

- **SC-009**: From a worktree with only `glp_runtime/`, `init` (Dart→C#) then `mirror` produces a `glp_runtime_net/` that satisfies every spec-`001` SC-001…SC-006 measurable outcome.
- **SC-010**: Re-running `mirror` without `--refresh` is a zero-change refusal; `--refresh` leaves every companion file and the tracker byte-identical and brings `.src`/non-source files into agreement with the current source (spec-`001` SC-008/SC-009).
- **SC-011**: Adding the mirror behaviour for a second (test-only) pair requires zero stage-tool edits (mirror extension of SC-003).
- **SC-012**: After this amendment the documented regenerate-per-worktree pipeline (`init → mirror → discover → depgraph → scaffold`) runs end-to-end with no D2NET binary/skill referenced (extends SC-006).

### Workspace scope overrides (owner decision 2026-05-17)

`init` is the sole authority for the mirror's effective scope (consistent with D6 — pair/scope set once at init). Two override surfaces, persisted in `codeconv.workspace_settings`, consumed by `mirror` (not by `discover`/`scaffold`, so feature-012/014/015 behaviour is unchanged — FR-023):

- **FR-042 (force-include override).** `init` MUST accept a repeatable `--include-pruned <dir>` that records workspace setting `mirror_force_include` (the set of standard-pruned directory **names** to NOT prune). `mirror`'s effective prune set = the pair's `mirror_prune_segments()` **minus** `mirror_force_include`. A name not in the standard set is a no-op (recorded; harmless). This lets an operator force-include a normally-excluded dir (e.g. `build`).
- **FR-043 (gitignore-style mirror exclusions).** `init` MUST accept a repeatable `--mirror-exclude <pattern>` that records workspace setting `mirror_exclude_patterns` (newline-joined, order-preserved). `mirror` MUST skip any directory **or** file whose output-root-relative POSIX path matches any pattern, using **gitignore semantics**: `#`/blank ignored; trailing `/` = directory-only; leading `/` = anchored to the subtree root; `**` = any number of path segments; `*`/`?` = within-segment glob (no `/`); a pattern with no `/` matches at any depth by basename; later patterns override earlier (last-match wins, gitignore order). A directory match prunes its whole subtree. The matcher is a **small internal implementation** — no new third-party dependency (dependency authority; `pathspec` is not a codeconv dep). These patterns are mirror-scope only and are **distinct** from `excluded_directories` (the existing literal-dir exclusions consumed by `discover`/`init`), so `discover`/`scaffold`/feature-015 behaviour is untouched (FR-023).
- **FR-044**: Unit tests for the internal gitignore matcher (anchored / unanchored / `**` / trailing-slash dir-only / `*`/`?` / last-match-wins, with negative controls) and integration tests for `init --include-pruned` (a standard-pruned dir reappears in the mirror) and `init --mirror-exclude` (an extra dir/pattern is pruned from the mirror), pure-unit where possible.

- **SC-013**: `init --include-pruned build` makes `build/` appear in the mirrored subtree; without it `build/` is pruned (round-trip verifiable).
- **SC-014**: `init --mirror-exclude '<pat>'` removes exactly the matching dirs/files from the mirrored subtree, with gitignore semantics, and changes nothing in `discover`/`scaffold`/depgraph behaviour vs. the no-pattern run except the excluded paths.

### Issue resolution log

- **Issue #1 (RESOLVED 2026-05-17)** — `depgraph compute` raised `ValueError: edge endpoint not in nodes` on the faithful full mirror. Root cause: the 016 branch base (`177a33f8`) predated feature-015's **option-A'** self-healing filter, so 016's `tools/depgraph/workflow.py` passed raw `dart_imports` edges to the SCC algorithm. A 016-local re-add of the filter unblocked it; on reconciliation (`main`←015 already has option-A') the 016 re-add was **dropped in favour of `main`'s feature-015 implementation** (`edges` restricted to inventoried-node endpoints before `compute`; count surfaced as the feature-015 key `dangling_edges_dropped`). Verified e2e on the 016-local fix: `depgraph` green, 35 dangling edges dropped, 178 files, 1 cycle. Net: no new semantics — `main`'s feature-015 option-A' is authoritative; FR-023 preserved (the curated `glp_runtime_net` simply never had dangling edges to exercise it).
