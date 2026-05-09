# Data Model: 012-codeconv-runner

This document specifies every persistent data shape introduced by this feature: PGLite tables (across schemas), on-disk bridge sidecar JSON, on-disk migration record JSON, and tombstone YAML frontmatter. It is the contract; SQL DDL and runtime models follow this exactly.

Schemas in `.pgdb/` after this feature lands:

| Schema | Owner | Source of truth | Touched by this feature? |
|---|---|---|---|
| `public` | (PGLite default) | — | NO — FR-015 forbids feature-introduced tables here |
| `dbos` | DBOS runtime | DBOS migrations (vendored) | indirectly (DBOS startup creates them) |
| `codeconv` | codeconv runner | Alembic migrations under `codeconv/db/migrations/` | YES (this feature defines them) |
| _D2NET schemas_ | D2NET tools | unchanged | NO — FR-015 explicitly forbids rewrite |

---

## 1. `codeconv` schema — PGLite tables

### 1.1 `codeconv.dart_files`

One row per discovered `.dart` file inside `glp_runtime_net/` (after exclusion of generated artefacts per FR-018).

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `path` | `text` | NO | PRIMARY KEY. POSIX-style relative path (forward slashes), relative to `glp_runtime_net/`. E.g. `runtime/heap_fcp.dart`. |
| `name` | `text` | NO | File basename (e.g. `heap_fcp.dart`). |
| `purpose` | `text` | NO | Verbatim leading doc-comment block, or empty string. Mechanical extraction (FR-020). |
| `key_idea` | `text` | NO | Same value as `purpose` when a single block, empty string otherwise (Clarification Q9). |
| `mtime` | `timestamptz` | NO | File modification time at last discover run. Read via `apply_to_engine`-patched loaders to avoid psycopg crash. |
| `sha256` | `text` | NO | Lowercase hex SHA-256 of file content at last discover run. Used for idempotence short-circuit (R15). |
| `discovered_at` | `timestamptz` | NO | Wall-clock at insert; `NOW()` default. Updated on row update. |

**Indexes**:
- `PRIMARY KEY (path)` (implicit unique).
- No additional indexes — 128-row scale; full scan is cheap.

### 1.2 `codeconv.dart_imports`

One row per `import` directive in a Dart file pointing to another Dart file inside `glp_runtime_net/`. Out-of-subtree imports (`package:`, `dart:`, paths resolving outside the subtree) are NOT recorded (R12).

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `from_path` | `text` | NO | Relative path of the importer. References `dart_files.path` (logical FK; not enforced by PGLite). |
| `to_path` | `text` | NO | Relative path of the imported file. Same shape and reference. |

**Constraints**:
- `UNIQUE (from_path, to_path)` per FR-019. Duplicate `import` directives in a single file are warned in the discover log and deduplicated to one row.

### 1.3 `codeconv.dart_callers`

The inverse view of `dart_imports`, denormalised into its own table for query convenience. Computed inside the subtree only (FR-023). Imports BY files outside the subtree pointing INTO the subtree are warned in the log but NOT recorded here.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `from_path` | `text` | NO | Caller (the file containing the `import`). Inside `glp_runtime_net/` only. |
| `to_path` | `text` | NO | Callee (the file being imported). Inside `glp_runtime_net/` only. |

**Constraints**:
- `UNIQUE (from_path, to_path)` per FR-019 (parity with `dart_imports`).

**Note**: `dart_callers` is logically derivable from `dart_imports` (it is the same data). It is materialised as a separate table for query symmetry — most consumer queries either ask "what does X depend on?" (read `dart_imports WHERE from_path = X`) or "who depends on X?" (read `dart_callers WHERE to_path = X`). The duplication is intentional and managed by discover (both tables are written in the same DBOS step per file).

### 1.4 `codeconv.dart_files_orphaned`

One row per Dart file that was previously inventoried but is no longer present at the recorded path (per FR-025). Same column shape as `dart_files` plus `orphaned_at`.

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `path` | `text` | NO | PRIMARY KEY. Same format as `dart_files.path`. |
| `name` | `text` | NO | |
| `purpose` | `text` | NO | Snapshot at last-known state. |
| `key_idea` | `text` | NO | |
| `mtime` | `timestamptz` | NO | mtime at the LAST discover run that saw the file present. |
| `sha256` | `text` | NO | Content hash at that run. |
| `discovered_at` | `timestamptz` | NO | First discover-time of the file (oldest known). |
| `orphaned_at` | `timestamptz` | NO | Wall-clock at which the row was moved here. |

**Revival**: Per FR-025 + Clarification Q15, when a file reappears at the same path, discover MUST move the row back from `dart_files_orphaned` to `dart_files`, refresh `mtime` + `sha256` from the new file, recompute import + caller edges normally, and move the tombstone back from `.codeconv/tombstones/.orphaned/<rel>.dart.md` to `.codeconv/tombstones/<rel>.dart.md`.

### 1.5 `codeconv.discover_runs`

(NEW — not in spec but needed for SC-009 durability and the `--from-tombstones` "skipped because tombstone-only" telemetry.)

One row per `/codeconv-discover` invocation. Used by DBOS workflow to checkpoint per-file progress (so `kill`-then-resume picks up where it left off, FR-017 + SC-009).

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `id` | `uuid` | NO | PRIMARY KEY. |
| `started_at` | `timestamptz` | NO | |
| `completed_at` | `timestamptz` | YES | NULL while in flight; set on workflow completion. |
| `mode` | `text` | NO | `'normal'` or `'from_tombstones'`. |
| `files_total` | `integer` | YES | NULL until walker finishes; then total file count. |
| `files_processed` | `integer` | NO | Default 0; bumped per-file step. |
| `files_skipped_idempotent` | `integer` | NO | Default 0; bumped on idempotence short-circuit. |
| `warnings` | `jsonb` | NO | Default `'[]'`; appended to per warning event. |

DBOS provides its own workflow tables in the `dbos` schema; `discover_runs` is the codeconv-side cross-reference (gives the runner a stable id to log per warning, and lets `--from-tombstones` runs distinguish themselves in audits). It does NOT duplicate DBOS's durability — DBOS's tables are the source of truth for "did this step complete?"; `discover_runs` is the human-facing counters layer.

---

## 2. `.pgdb/bridge.json` — sidecar discovery file

Atomically written by the bridge after `listen()` resolves, BEFORE the `BRIDGE_READY` token is emitted on stdout (R4). Read by clients that lose the lock race (FR-006 step 3).

```json
{
  "host": "127.0.0.1",
  "port": 54812,
  "pid": 12345,
  "started_at": "2026-05-09T14:32:11.123Z",
  "data_dir": "D:\\BSTDEV\\research\\GLP\\GLPNET\\.pgdb",
  "role": "primary",
  "managed_by": "auto-spawn"
}
```

**Field semantics**:
- `host`: always `127.0.0.1` (loopback only — FR-002 single-bridge-per-repo + no remote access).
- `port`: integer, ephemeral (R3).
- `pid`: integer; the bridge process PID.
- `started_at`: ISO-8601 UTC, millisecond precision.
- `data_dir`: absolute path to `.pgdb/` (so clients can sanity-check they are about to talk to the bridge for the right repo).
- `role`: `"primary"` (reserved for future replica modes; always `"primary"` in this feature).
- `managed_by`: `"auto-spawn"` when the bridge was started by the FR-006 protocol; `"manual"` when started by the operator's escape hatch launcher.

**Lifecycle**:
- Written: after `listen()` resolves and before `BRIDGE_READY` stdout token. Atomic via `tmp + rename`.
- Read: by any client that fails to acquire the bridge lock.
- Deleted: by the bridge on graceful exit (`process.on('SIGTERM' | 'SIGINT' | 'beforeExit')`). On crash, the file lingers — but per FR's edge case, clients treat absence-of-lock as authoritative. If the lock is held, sidecar is trustworthy; if the lock is unheld but sidecar exists, clients ignore the sidecar and re-acquire the lock.

---

## 3. `.pgdb/.migration-record.json` — migration audit

Written by `D2Net.PgdbMigrate` after a successful move (R8 step 4 in normal flow). NOT used as an idempotence flag — re-running migration with absent source is still a no-op regardless (FR-009).

```json
{
  "from": "D:\\BSTDEV\\research\\GLP\\GLPNET\\.D2NET\\pgdb",
  "to": "D:\\BSTDEV\\research\\GLP\\GLPNET\\.pgdb",
  "backup_at": "D:\\BSTDEV\\research\\GLP\\GLPNET\\.D2NET\\pgdb.bak.20260509T143211Z",
  "at": "2026-05-09T14:32:11.456Z",
  "tool_version": "0.1.0"
}
```

---

## 4. Tombstone YAML frontmatter — `.codeconv/tombstones/<rel>.dart.md`

Written by `/codeconv-discover` (FR-021). Each tombstone is a Markdown document with YAML frontmatter (delimited by `---` lines) and a body containing the verbatim doc-comment block.

### 4.1 Frontmatter schema

```yaml
---
path: runtime/heap_fcp.dart
name: heap_fcp.dart
purpose: |
  FCP-style flat concurrent Prolog heap with bidirectional variable pairs.
  See docs/heap/heap-pointer-architecture-spec.md for invariants.
key_idea: |
  FCP-style flat concurrent Prolog heap with bidirectional variable pairs.
  See docs/heap/heap-pointer-architecture-spec.md for invariants.
dependencies:
  - runtime/cell.dart
  - runtime/tag.dart
callers:
  - runtime/runner.dart
  - runtime/unify.dart
mtime: '2026-04-30T11:14:22.000Z'
sha256: 7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b
---

FCP-style flat concurrent Prolog heap with bidirectional variable pairs.
See docs/heap/heap-pointer-architecture-spec.md for invariants.
```

### 4.2 Field semantics

| Field | Type | Notes |
|---|---|---|
| `path` | string | POSIX path relative to `glp_runtime_net/`. R7. |
| `name` | string | File basename. |
| `purpose` | string | Verbatim doc-comment block, or `''`. YAML block scalar (`|`) preferred for multi-line. |
| `key_idea` | string | Same as `purpose` per Clarification Q9. |
| `dependencies` | list[string] | Sorted lexically. POSIX paths relative to `glp_runtime_net/`. Empty list `[]` if none. |
| `callers` | list[string] | Sorted lexically. POSIX paths. Inside-subtree only (FR-023). Empty list if none. |
| `mtime` | string | ISO-8601 UTC, millisecond precision (matches PG `timestamptz` output shape). |
| `sha256` | string | Lowercase hex. |

### 4.3 Body

The body of the tombstone (everything after the closing `---` frontmatter delimiter) is the verbatim doc-comment block, OR empty when there is no leading doc-comment. The body is informational — `--from-tombstones` reads only the frontmatter for inventory reconstruction. Hand-edits to the body are NOT preserved across re-runs (per Edge Cases).

### 4.4 `.codeconv/tombstones/.orphaned/<rel>.dart.md`

Identical schema. Written when a file is orphaned (FR-025); restored to `.codeconv/tombstones/<rel>.dart.md` on revival (with `mtime`/`sha256` refreshed from the new file).

---

## 5. State transitions

### 5.1 `dart_files` row lifecycle

```
                    discover (file present)
        ┌──────────────────────────────────────┐
        │                                      ▼
   (no row)                              dart_files row
        ▲                                      │
        │ discover --from-tombstones          │ discover (file gone)
        │ on missing tombstone                │
        │                                      ▼
        │                            dart_files_orphaned row
        │                                      │
        │ discover (file reappears)           │
        │  (FR-025 revival)                    │
        └──────────────────────────────────────┘
```

### 5.2 `discover_runs` row lifecycle

```
   created (mode, started_at, files_processed=0)
                    │
                    │ DBOS workflow steps
                    │ (each file: bump files_processed)
                    ▼
   completed (completed_at set, files_total set)
```

A row in state "in flight" (no `completed_at`) means a discover invocation was killed; DBOS is the source of truth for resume — re-invoking discover finds the in-flight row, resumes the corresponding DBOS workflow.

### 5.3 Bridge lifecycle

```
                  client startup
                        │
                        ▼
        try acquire .pgdb/.bridge.lock (proper-lockfile)
                ┌──────┴──────┐
            won │             │ lost
                ▼             ▼
       spawn bridge      read .pgdb/bridge.json
       wait READY        connect TCP
       proceed           proceed
                ▲
                │
       bridge listen()
       write bridge.json (atomic)
       emit BRIDGE_READY on stdout
       detach pipes
       (lifetime: until killed; lock held throughout)
                │
                ▼
       on exit: kernel releases lock automatically;
       bridge attempts to delete bridge.json (best effort)
```

---

## 6. What this model does NOT include

- **Semantic enrichment fields** (`engineer_purpose`, `algorithm_summary`, etc.) — out of scope per FR-020 + FR-028. Any future enrichment tool may add columns to `dart_files` (or a sibling table) under that tool's own migration; not delivered here.
- **Dart → C# translation artefacts** — out of scope per FR-028.
- **D2NET schema definitions** — unchanged per FR-015. Documented separately if needed by `D2Net.PgdbMigrate`.
- **DBOS internal tables** — DBOS owns its `dbos` schema; not modeled here.
