# Feature Specification: codeconv-depgraph — topologically sorted Dart dependency graph and conversion-readiness oracle

**Feature Branch**: `015-codeconv-depgraph`
**Created**: 2026-05-11
**Status**: Draft
**Input**: User description (verbatim, lightly normalised):

> Create a new `/codeconv-depgraph` skill and Python tool to create a topologically sorted dependency graph for all `.dart` tombstones to identify which `.dart` files are not dependent on any other files for being converted first — so that this depgraph (in JSON and as a table in PGLite form) can be used to identify suitable candidates for conversion either because they have no dependency on another file that first needs to be converted, or all of its dependencies are already converted.

## Context

Feature 012 (codeconv-runner) delivered the `codeconv` schema, the `/codeconv-discover` tool, and a tombstone-backed inventory under `.codeconv/tombstones/`. Feature 014 fixed `package:` self-import resolution so the import graph is complete: **128 files / 443 in-subtree edges / 6 isolated nodes** at the current baseline. The data needed to drive a conversion-readiness oracle is therefore already on disk — what is missing is (a) a tool that consumes that graph and emits a topologically sorted ordering plus a per-file readiness flag, and (b) a place to persist the ordering and a place to record which files have already been converted so successive runs can advance the frontier.

The reference conventions are already established:

- **Skill-as-thin-wrapper-around-CLI** (`/codeconv-discover`, `/codeconv-runner`) — the Python CLI is the source of truth; the skill forwards arguments verbatim.
- **Tool registration under `codeconv/src/codeconv/tools/<name>/`** — auto-discovered by the `codeconv` console script (feature 012, FR-006).
- **Unified PGLite at `.pgdb/`** — all reads and writes go through the bridge daemon via the protocol in `specs/012-codeconv-runner/contracts/bridge_lifecycle.md`.
- **Tombstones at `.codeconv/tombstones/<rel>.dart.md`** — checked in; YAML frontmatter carries `dependencies` and `callers` (feature 012, FR-018/FR-019/FR-023).

The conversion target is **Dart → C# / .NET** (feature 012 clarification, 2026-05-09). This spec covers the *ordering* of that conversion only; the act of converting a file is a separate downstream tool.

## Clarifications

### Session 2026-05-11

- Q: Where is conversion status stored, and which actor writes it? → A: **Option B** — new table `codeconv.dart_conversions` with **two-phase** tracking because conversions are long-running operations that begin first and complete only later: `path PK`, `started_at timestamptz NOT NULL`, `completed_at timestamptz NULL`, `sha256_of_dart_at_start text NOT NULL`, `target_path text NULL`. Tombstone YAML mirrors with `conversion_started_at` and `conversion_completed_at` keys. `/codeconv-depgraph` reads `dart_conversions` to compute eligibility; a file's dependency counts as "converted" **only when `completed_at IS NOT NULL`** (in-progress dependencies do NOT make downstream files ready). Two subcommands write the table: `codeconv depgraph mark-started <path> [--sha256 <hex>]` and `codeconv depgraph mark-completed <path> [--target <path>]`. This couples ordering and conversion-frontier bookkeeping into one tool and lets a developer see which conversions are in-flight vs. done.

- Q: How should the tool handle cycles in the in-subtree import graph? → A: **Option A** — Tarjan SCC condensation. Every Strongly Connected Component is treated as one "group" node; the condensation (a DAG by construction) is topologically sorted. Files inside a multi-file SCC share a single `cycle_group_id int NOT NULL` value, are ordered lexicographically by `path`, and receive the same `topo_level` (the level of their group in the condensation). Singleton SCCs (the common case) get a unique `cycle_group_id` value too — there is no NULL `cycle_group_id`; multi-file SCCs are identified by `count(*) over (partition by cycle_group_id) > 1`. An SCC is `ready` (every member gets `status='ready'`) only when **every dependency of any member that is outside the SCC** has `dart_conversions.completed_at IS NOT NULL`. Inside an SCC, members convert as a single batch — a converter MUST call `mark-started` on every member, do them together, then `mark-completed` on each.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Identify the first wave of conversion candidates (leaves) (Priority: P1)

A developer wants to start converting Dart files to C# / .NET. They invoke `/codeconv-depgraph` on a fresh checkout. The tool reads the `codeconv.dart_imports` table, computes a topological ordering of all 128 (or current count) in-subtree files, identifies every file whose set of in-subtree dependencies is empty, and emits two artifacts: (a) a JSON file listing every file with its `topo_level`, `depends_on`, and `ready` flag, and (b) a new PGLite table (`codeconv.dart_depgraph`) the developer can query directly. The developer reads "ready" rows from either artifact and picks any one to convert first.

**Why this priority**: This is the user's stated headline goal — "identify which `.dart` files are not dependent on any other files for being converted first." Without this story, the feature does not exist.

**Independent Test**: On a checkout with `/codeconv-discover` already run, invoke `/codeconv-depgraph`. The JSON output and the `codeconv.dart_depgraph` table MUST agree on the same `ready=true` set. The `ready=true` count MUST equal the count of files in `codeconv.dart_files` whose `path` appears nowhere in `codeconv.dart_imports.from_path` (i.e. zero in-subtree outgoing edges) — confirmable by a 2-line SQL query.

**Acceptance Scenarios**:

1. **Given** the current baseline (128 files, 443 edges, 6 isolated), **When** `/codeconv-depgraph` runs, **Then** at least the 6 isolated files appear with `ready=true` and `topo_level=0`, and the JSON + table row counts both equal the count of inventoried files in `codeconv.dart_files`.
2. **Given** an empty / never-discovered repo, **When** `/codeconv-depgraph` runs, **Then** it exits with a clear error pointing the user at `/codeconv-discover` (no silent empty-graph emission).
3. **Given** `/codeconv-depgraph` has run once, **When** it runs again with no source-state change, **Then** the JSON output is byte-identical (modulo a timestamp metadata field) and the `codeconv.dart_depgraph` table contents are unchanged (idempotent re-run, SC-002).

---

### User Story 2 — Advance the conversion frontier (Priority: P2)

A developer has converted some files (calling `codeconv depgraph mark-started` to begin, then `mark-completed` to finish). They re-run `/codeconv-depgraph`. The tool consults `codeconv.dart_conversions`, recomputes `status` + `ready`, and now flags files at the next `topo_level` whose every SCC-external dependency has `completed_at IS NOT NULL`. The developer picks the next candidate from the refreshed `ready` array.

**Why this priority**: This is the second half of the user's request and the whole reason for incremental, ordered conversion. P2 (not P1) because US1 alone delivers value: a developer can start converting leaves immediately without ever calling `mark-*`; US2's frontier-advance only kicks in once the first wave is actually converted.

**Independent Test**: After at least one file has been marked through both phases (`mark-started` → `mark-completed`), re-run `/codeconv-depgraph`. Verify that (a) every file whose entire SCC-external dependency set has `completed_at IS NOT NULL` appears with `ready=true`, (b) no file with at least one un-completed SCC-external dependency appears with `ready=true`, (c) completed files have `status='converted'` and are absent from the top-level `ready` array, (d) files mid-conversion (started but not completed) have `status='in_progress'` and do not unblock downstream files.

**Acceptance Scenarios**:

1. **Given** files A, B, C where A has no deps, B depends on A, C depends on B, and **none** are in `dart_conversions`, **When** the tool runs, **Then** only A has `status='ready'`.
2. **Given** the same A→B→C chain and `mark-started A` has been called but not `mark-completed A`, **When** the tool runs, **Then** A has `status='in_progress'`, B has `status='pending'` (NOT `ready`), C has `status='pending'`.
3. **Given** the same chain and BOTH `mark-started A` and `mark-completed A` have been called, **When** the tool runs, **Then** A has `status='converted'`, B has `status='ready'`, C has `status='pending'`.
4. **Given** the same chain and A and B are both fully converted (`completed_at IS NOT NULL`), **When** the tool runs, **Then** A and B both have `status='converted'`, C has `status='ready'`.

---

### User Story 3 — Convert a cycle as a single batch (Priority: P2)

The Dart import graph contains a 3-file circular import (A → B → C → A). The developer runs `/codeconv-depgraph` and sees all three files share `cycle_group_id=N`, share the same `topo_level`, and all three are `status='ready'` together once their SCC-external dependencies are converted. They `mark-started` all three, convert them as a batch (because no member can be converted alone — the C# / .NET output must reference siblings that may not exist yet), then `mark-completed` all three. The depgraph re-run advances downstream files exactly as it does for non-cyclic dependencies.

**Why this priority**: The 128-file / 443-edge baseline almost certainly contains at least one cycle. Without SCC handling, every downstream file behind a cycle is permanently blocked.

**Independent Test**: Construct a minimal fixture inventory with three files where A imports B, B imports C, C imports A, and a fourth file D imports A. Run `/codeconv-depgraph`. Verify that (a) A, B, C share the same `cycle_group_id` and `topo_level`, (b) D has a higher `topo_level` and a distinct `cycle_group_id`, (c) A/B/C are simultaneously `status='ready'` if no member has an external dep, (d) D is `status='pending'` (or `'ready'` only after all of A/B/C are `'converted'`).

**Acceptance Scenarios**:

1. **Given** a 3-file cycle fixture, **When** the tool runs, **Then** all three cycle members share one `cycle_group_id` value and one `topo_level`, and the JSON metadata reports `cycle_count=1`.
2. **Given** the same 3-file cycle, **When** `mark-started` is called on one member, **Then** that member becomes `status='in_progress'` while the other two remain `status='ready'` (they're separate rows in `dart_conversions` and the SCC eligibility rule only short-circuits dependencies, not membership).
3. **Given** the production inventory contains zero multi-file cycles, **When** the tool runs, **Then** the JSON metadata reports `cycle_count=0` and every file's `cycle_group_id` is unique to that file.

---

### Edge Cases

- **Empty inventory**: `codeconv.dart_files` has zero rows → emit an explicit error pointing the user at `/codeconv-discover`. Do not write an empty JSON file or table.
- **Orphaned files**: `codeconv.dart_files_orphaned` contains rows. Orphaned files MUST NOT appear in the depgraph (they are not conversion targets). The tool MUST NOT touch `dart_files_orphaned` at all.
- **External-package imports** (e.g. `package:flutter/...` resolved outside the subtree): excluded by construction — `codeconv.dart_imports` already filters to in-subtree edges (feature 012 FR-019, feature 014). The depgraph tool inherits this scope and MUST NOT widen it.
- **Stale `codeconv.dart_imports`** (re-run of `/codeconv-discover` would change the graph): out of scope to detect — the user is responsible for running discover first. The JSON output's `last_discover_run_id` (FR-007, MUST — clarified 2026-05-11 from the earlier "MAY" wording) carries the `discover_run_id` of the most-recent successful discover run whose writes are currently visible in `dart_files`/`dart_imports`, so a forensic comparison can detect drift.
- **Self-import** (a file imports itself): forms a 1-element SCC with a self-loop; the file gets a singleton `cycle_group_id` like any other file, and the self-edge is an intra-SCC edge per FR-005. No special handling required.
- **Rebuilding state from tombstones**: `dart_depgraph` is a pure function of `dart_imports`+`dart_conversions` and is therefore always RECOMPUTED from those tables — there is no `--from-tombstones` mode on `compute`. The inverse round-trip for `dart_conversions` is provided by the dedicated `codeconv depgraph rebuild-conversions-from-tombstones` subcommand (decided at /speckit-plan, research note R3). `dart_imports` itself rebuilds via `/codeconv-discover --from-tombstones` (feature 012 FR-022). Together, the two subcommands restore the full state of the depgraph after a DB wipe.
- **Two competing depgraph runs**: serialised through the bridge daemon (single PGLite session, feature 012 US1). No special handling needed at this layer.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST register a new Python tool `codeconv-depgraph` under `codeconv/src/codeconv/tools/depgraph/` so that `codeconv list` shows it and `codeconv depgraph ...` invokes it (per feature-012 FR-006 auto-discovery).
- **FR-002**: System MUST provide a slash-command thin wrapper `/codeconv-depgraph` at `.claude/skills/codeconv-depgraph/SKILL.md` that forwards arguments verbatim to `codeconv depgraph` and otherwise mirrors `/codeconv-discover`'s skill conventions (venv resolution, repo-root cwd, pre-execution checks, stdout/stderr passthrough).
- **FR-003**: System MUST read the dependency graph exclusively from `codeconv.dart_imports` (canonical) and `codeconv.dart_files` (node set) — NEVER from `.dart` source. Tombstones MAY be a secondary source if a `--from-tombstones` mode is supported (final scope per planning).
- **FR-004**: System MUST compute Strongly Connected Components of the in-subtree import graph (Tarjan's algorithm or equivalent), construct the condensation DAG, topologically sort it, and assign every inventoried file a non-negative integer `topo_level` equal to the level of its SCC in the condensation. For every edge `(from_path → to_path)` in `dart_imports`, EITHER `topo_level(from_path) > topo_level(to_path)` (cross-SCC edge) OR `from_path` and `to_path` share a `cycle_group_id` (intra-SCC edge).
- **FR-005**: System MUST assign every inventoried file a `cycle_group_id int NOT NULL` value. Files in the same SCC share the same value; singleton SCCs each get a unique value. The JSON output and `codeconv.dart_depgraph` table MUST both expose `cycle_group_id` so multi-file cycles are queryable via `SELECT cycle_group_id, count(*) FROM codeconv.dart_depgraph GROUP BY cycle_group_id HAVING count(*) > 1`. The JSON metadata block MUST include `cycle_count` = number of distinct `cycle_group_id` values with more than one member (i.e. the count of multi-file cycles; singleton SCCs are not "cycles" for this metric).
- **FR-006**: System MUST classify every inventoried file into exactly one `status` value: `pending` (no row in `codeconv.dart_conversions`), `ready` (pending AND every **SCC-external** in-subtree dependency has `dart_conversions.completed_at IS NOT NULL` — or the file's SCC has no external deps), `in_progress` (row exists with `started_at IS NOT NULL` AND `completed_at IS NULL`), or `converted` (row exists with `completed_at IS NOT NULL`). The boolean `ready` is true iff `status='ready'`. Eligibility ignores intra-SCC edges (a file inside a 3-file cycle does not block itself or its SCC-mates); only edges crossing into the file's SCC from outside count. In-progress external dependencies DO NOT make downstream files ready — only `completed_at IS NOT NULL` deps count.

- **FR-006a**: System MUST expose two write subcommands: `codeconv depgraph mark-started <path> [--sha256 <hex>]` (inserts a `dart_conversions` row with `started_at=NOW()`, `sha256_of_dart_at_start` from arg or from `dart_files.sha256`, `completed_at NULL`, `target_path NULL`) and `codeconv depgraph mark-completed <path> [--target <path>]` (updates the existing row, setting `completed_at=NOW()` and `target_path`). Both subcommands MUST validate `path` exists in `codeconv.dart_files`; both MUST be idempotent on re-run: `mark-started` on an already-started row is a no-op with a warning; `mark-completed` on an already-completed row is a no-op with a warning AND MUST NOT overwrite the existing `target_path` or `completed_at` (both are write-once per row in v1 — clarified 2026-05-11). Both subcommands MUST update the corresponding tombstone YAML frontmatter (`conversion_started_at`, `conversion_completed_at`) so the schema can be rebuilt from tombstones (mirroring feature-012 FR-022 round-trip). Ordering: the DB transaction COMMITs BEFORE the file-system tombstone write; if the FS write fails after a successful COMMIT, the tool emits a warning and exits non-zero, and the next `stamp-tombstones` invocation will reconcile.
- **FR-007**: System MUST emit a JSON artifact at a default path (e.g. `.codeconv/depgraph.json`) containing, at minimum, for every inventoried file: `path`, `topo_level`, `cycle_group_id`, `depends_on` (in-subtree only — sourced live from `dart_imports`, not stored on `dart_depgraph`), `depended_on_by` (in-subtree only — sourced live from `dart_imports`), `ready` (boolean), `status` (one of `pending`/`ready`/`in_progress`/`converted`), `conversion_started_at` (NULL if absent), `conversion_completed_at` (NULL if absent), `target_path` (NULL if absent). The JSON MUST include a top-level metadata block with `generated_at` (ISO8601 UTC with 'Z' suffix), `inventory_files_total`, `inventory_edges_total`, `ready_count`, `in_progress_count`, `converted_count`, `cycle_count` (count of multi-file SCCs only, per FR-005), and `last_discover_run_id` (the `discover_run_id` of the most-recent successful discover run whose writes are currently visible in `dart_files`/`dart_imports`; NULL if discover never ran — clarified 2026-05-11 from earlier ambiguous "most recent known"). The JSON MUST also include a top-level `ready` array (paths of all `status='ready'` files, lexicographically sorted) so a developer can answer "what should I convert first?" by reading the first array of the document.
- **FR-008**: System MUST persist the same per-file information to a new table `codeconv.dart_depgraph` (or successor name) in the unified PGLite via the bridge daemon. The table MUST be (re)written atomically per run — a partial write that crashes mid-flight MUST leave the previous run's data intact OR be detectable so the next run can recover.
- **FR-009**: System MUST be idempotent — a re-run on unchanged inventory state produces byte-identical JSON (modulo timestamp metadata) and zero diff in `codeconv.dart_depgraph` content.
- **FR-010**: System MUST exit non-zero with a clear, actionable error message when `codeconv.dart_files` is empty (instruct user to run `/codeconv-discover`).
- **FR-011**: System MUST NOT modify `codeconv.dart_files`, `codeconv.dart_imports`, `codeconv.dart_callers`, `codeconv.dart_files_orphaned`, or `codeconv.discover_runs`. The tool's writes are confined to: (a) `codeconv.dart_depgraph` (new, the topological-ordering table), (b) `codeconv.dart_conversions` (new, written only via the `mark-started` / `mark-completed` subcommands), (c) `.codeconv/depgraph.json` (the JSON artifact), and (d) the `conversion_started_at` / `conversion_completed_at` YAML keys in tombstones (written only by the `mark-*` subcommands). The default read-only `codeconv depgraph` invocation MUST NOT write to (b) or (d).
- **FR-012**: System MUST support `--json` (JSON summary of the run on stdout), `--quiet` (suppress per-file logging), and `--dry-run` (compute everything but skip writes to DB and disk) — mirroring `/codeconv-discover`'s flag surface.
- **FR-013**: System MUST honor the global `--repo-root` and `--data-dir` flags inherited from the `codeconv` console script (feature 012). The JSON artifact path defaults to `<repo-root>/.codeconv/depgraph.json` and MUST be overridable via `--json-out <path>`.
- **FR-014**: System MUST provide a `codeconv depgraph stamp-tombstones` subcommand that embeds the current depgraph result (`topo_level`, `status`, `cycle_group_id`, plus `conversion_started_at` / `conversion_completed_at` from `dart_conversions`) into every tombstone's YAML frontmatter. The subcommand MUST be idempotent (re-stamping unchanged data produces a zero-diff tombstone). This is the round-trip mechanism that satisfies the rebuild-from-tombstones property for both depgraph and conversion state.
- **FR-015**: System MUST emit deterministic output — within a `topo_level`, files are ordered lexicographically by `path`. Within a `cycle_group_id`, members are ordered lexicographically by `path`. JSON keys are emitted in a stable order.

### Key Entities

- **dart_depgraph_row** (`codeconv.dart_depgraph`): one row per inventoried file. Attributes: `path` (PK, FK to `codeconv.dart_files.path`), `topo_level int NOT NULL`, `cycle_group_id int NOT NULL` (every file is in some SCC; singletons get unique values; multi-file SCCs share — see FR-005; **amended 2026-05-11 from `int NULL` to align with FR-005**), `ready boolean NOT NULL`, `status text NOT NULL` (one of `pending`, `ready`, `in_progress`, `converted` — per FR-006), `dependency_count int NOT NULL` (count of in-subtree dependencies — number of rows in `dart_imports` where this is `from_path`; **renamed 2026-05-11 from `in_degree` for clarity, since the standard graph-theory `in_degree` of a node in `dart_imports` would be the number of callers, not dependencies**), `caller_count int NOT NULL` (count of in-subtree callers — number of rows in `dart_imports` where this is `to_path`; renamed from `out_degree` for the same reason), `computed_at timestamptz NOT NULL`, `discover_run_id uuid NULL` (FK to `codeconv.discover_runs.id`, the inventory state this row was computed against).
- **depgraph_run** (optional, per planning): one row per `/codeconv-depgraph` invocation. Attributes: `id uuid PK`, `started_at`, `completed_at`, `inventory_files_total`, `cycle_count`, `ready_count`, `warnings jsonb`. Mirrors `codeconv.discover_runs` shape for traceability.
- **dart_conversion** (`codeconv.dart_conversions`): one row per file once conversion begins. Attributes: `path text PK` (FK to `codeconv.dart_files.path`), `started_at timestamptz NOT NULL`, `completed_at timestamptz NULL`, `sha256_of_dart_at_start text NOT NULL` (snapshot of the source `.dart` SHA when conversion began, so post-completion source drift is detectable), `target_path text NULL` (relative path to the produced C# / .NET file, written at completion). A row's lifecycle: absent → present-with-completed_at-NULL (`in_progress`) → completed_at-set (`converted`). The table has no DELETE workflow in v1.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On the current baseline (128 files, 443 edges, 6 isolated post-feature-014), `/codeconv-depgraph` completes in ≤ 5 seconds on a warm bridge and ≤ 15 seconds on a cold-bridge first run, and produces a JSON artifact + populated `codeconv.dart_depgraph` table with exactly 128 rows.
- **SC-002**: A re-run of `/codeconv-depgraph` on unchanged inventory state produces byte-identical JSON output (modulo the `generated_at` metadata field) and zero row-content diff in `codeconv.dart_depgraph` (idempotence, mirroring `/codeconv-discover` SC-008).
- **SC-003**: For every edge `(A → B)` in `codeconv.dart_imports`, the JSON output and the table BOTH satisfy EITHER `topo_level(A) > topo_level(B)` (cross-SCC edge) OR `cycle_group_id(A) = cycle_group_id(B)` (intra-SCC edge). Verifiable by a single SQL self-join over `codeconv.dart_imports` × `codeconv.dart_depgraph`.
- **SC-004**: The `ready=true` set in the JSON output is EXACTLY the set of files satisfying the FR-006 rule, verifiable by an independent 2-query SQL check against `codeconv.dart_files`, `codeconv.dart_imports`, and (per Q1) the conversion-status column/table.
- **SC-005**: A developer reading the JSON output can answer "what should I convert first?" in under 30 seconds by visually scanning rows with `topo_level=0, ready=true` — i.e. the file is structured for quick human consumption (e.g. top-level `ready` array, not nested deep inside per-level groupings).
- **SC-006**: When run against a synthetic 3-file cycle fixture (A↔B↔C), all three files share one `cycle_group_id` and one `topo_level` in BOTH the JSON and the table; JSON metadata reports `cycle_count=1`; the cycle is queryable via the `GROUP BY cycle_group_id HAVING count(*) > 1` pattern.
- **SC-007**: Schema isolation is preserved — `codeconv.dart_depgraph` is created in the `codeconv` schema only; no objects are created in `public` or `dbos` (mirroring feature-012 FR-015).
- **SC-008**: `/codeconv-depgraph --dry-run` produces the same stdout/stderr as a real run but writes nothing to the DB or `.codeconv/`; verifiable by `git status` showing no changes and `SELECT count(*) FROM codeconv.dart_depgraph` returning the pre-run value.

## Assumptions

- The unified PGLite bridge at `.pgdb/` is the only DB target (feature 012). No new bridge or sidecar is introduced.
- `/codeconv-discover` has been run at least once before `/codeconv-depgraph` is invoked. The depgraph tool MAY check `codeconv.discover_runs` to detect a never-discovered repo and fail loudly (FR-010), but it does NOT auto-run discover itself.
- The dependency graph used as input is in-subtree only — edges to external packages or files outside `glp_runtime_net/` are excluded by construction in `codeconv.dart_imports` (feature 012 FR-019, feature 014 self-package fix). This tool does not redefine that scope.
- Tombstones are read-only inputs to `compute` / `mark-*`; the `stamp-tombstones` subcommand (FR-014, mandatory v1 — clarified 2026-05-11 from earlier "optional v1" wording, because Q1=B/two-phase tracking makes tombstones the round-trip carrier of conversion state) is the only writer that updates the depgraph-related YAML keys. A separate `rebuild-conversions-from-tombstones` subcommand (decided at /speckit-plan, research note R3) provides the inverse round-trip for `dart_conversions`.
- The Python tool is registered via the same auto-discovery mechanism as `discover` (feature 012, FR-006). No new registration plumbing is needed.
- The slash-command skill follows the conventions in `.claude/skills/codeconv-discover/SKILL.md` verbatim — venv resolution, repo-root cwd, pre-execution checks, stdout/stderr passthrough. No new skill machinery is introduced.
- `codeconv` schema migrations: any new tables/columns required by this feature are added via a new Alembic revision under `codeconv/src/codeconv/db/migrations/versions/` (e.g. `0002_dart_depgraph.py`). Idempotent. No data migration of feature-012 tables is needed.
- The user-facing output is for developers — JSON is human-readable (indented), table is queryable via the bridge with `psql` or any Postgres client.
- Cycle handling, conversion-status tracking, and tombstone stamping are the three design questions that materially affect schema and CLI surface; everything else can be defaulted at plan time.

## Out of Scope

- Actually converting `.dart` files to C# / .NET — covered by a future codeconv-* tool.
- Inferring `purpose` or `key_idea` semantically (still mechanical-only per feature-012 FR-020).
- Cross-process bridge coordination (feature 012 covers this; this tool is just another consumer).
- A graphical visualisation of the depgraph — JSON + SQL is enough for v1.
- Comparing depgraphs across time (diffing depgraph_run rows) — out of scope; future feature.
- Recommending an optimal multi-file batch (e.g. "convert these 3 files together") — out of scope beyond surfacing SCC groups per Q2.
