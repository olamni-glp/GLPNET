# Phase 0 Research — D2NET.Init

**Feature**: `002-d2net-init` — see [spec.md](spec.md) and [plan.md](plan.md)

All Technical Context items in `plan.md` are concrete (no `NEEDS CLARIFICATION` markers remain after `/speckit-clarify`, which already resolved 5 questions). This document records the rationale behind the technology and architectural choices so they can be revisited when sibling D2NET tools (analyzer, porter, verifier) are added.

---

## R1 — Storage engine for the workspace metadata database

**Decision**: **Embedded single-user SQLite** via `Microsoft.Data.Sqlite`. (Adopted as Q6 in `spec.md` Clarifications, after the original PGLite + bridge + ODBC stack proved fundamentally fragile in implementation — see "Implementation history" below.)

**Rationale**:
- Single-user, file-backed metadata DB is exactly the scenario SQLite was designed for.
- `Microsoft.Data.Sqlite` is a Microsoft-maintained ADO.NET driver shipped from the `Microsoft.Data.Sqlite` NuGet package; no native ODBC driver, no Node.js, no bridge process required.
- Cross-platform: SQLite ships as a managed-code-loadable native library inside the NuGet package; the same `dotnet build` works on Windows, Linux, and macOS without runtime prerequisites beyond .NET 8.
- Dramatically simpler operational story than the bridge architecture: every D2NET command opens the SQLite file in-process and closes it on exit; no port management, no process supervision, no protocol translation.
- All schema semantics carry over with trivial syntactic differences (`BIGSERIAL` → `INTEGER PRIMARY KEY AUTOINCREMENT`; `TIMESTAMPTZ DEFAULT now()` → `TEXT DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))`).

**Implementation history (kept for posterity)**:
- The first implementation attempt (recorded in earlier revisions of `plan.md` and `research.md`) used a vendored Node.js bridge running `@electric-sql/pglite` + `pg-gateway` and a per-invocation child-process lifecycle. The .NET code reached the database via `System.Data.Odbc` + psqlODBC.
- That stack failed end-to-end in two distinct ways: (1) psqlODBC's native code triggered `STATUS_STACK_BUFFER_OVERRUN` (`__fastfail`) the moment it tried to read pg-gateway's startup response — likely a malformed handshake bytes; (2) Npgsql connected fine but failed on every extended-protocol Parse/Bind/Execute with `"Received backend message ReadyForQuery while expecting BindCompleteMessage"`, indicating pg-gateway's Sync handling between PGLite and the wire client is broken in the available 0.3.0-beta.4 and 0.2.4 versions.
- The pivot to SQLite preserves every functional requirement the user originally stated (single-user, file-backed, relational, externally queryable) while removing every dependency that proved unreliable.

**Alternatives considered (and now rejected)**:
- **PGLite + Node bridge + ODBC** — the original plan; rejected after both psqlODBC and Npgsql failed end-to-end as described above.
- **PGLite + custom Postgres-wire bridge written in C#** — would let us bypass pg-gateway's bugs but is a multi-week project; out of scope.
- **In-process PGLite via `Jering.Javascript.NodeJS`** — even heavier than the child-process bridge.
- **Embedded Postgres binary (e.g. `EmbeddedPostgres`)** — spawns a native `postgres` server binary per invocation; heavier disk footprint and slower startup than SQLite.

---

## R2 — Bridge TCP port (no longer applicable after Q6)

**Decision**: Removed. With the SQLite pivot the workspace database is opened in-process by every D2NET command — no listener, no port. `--bridge-port` is preserved as a deprecated no-op flag for backward compatibility (FR-023).

---

## R3 — How D2NET.Init obtains the four required inputs

**Decision**:
- CLI flags `--source <name>`, `--target-extension <ext>`, `--target <name>` collect the three names. Any flag absent triggers an interactive prompt for that value.
- `--exclude <path>` (repeatable) and `--accept-suggested-exclusions` together cover the exclusion list. If neither is supplied, the interactive flow runs the full prompt cycle from FR-008 (display proposed list → optionally remove → redisplay → approve).
- `--non-interactive` short-circuits prompting: any missing required input becomes an error, and `--accept-suggested-exclusions` is implied unless `--exclude` is present.

**Rationale**:
- Matches the spec's "params or prompts" model (FR-005) without inventing state machines.
- `--non-interactive` is explicitly documented so CI / scripted use is reproducible without TTY tricks.
- Repeatable `--exclude` is more ergonomic on a CLI than a single comma-separated string and matches how `dotnet test --filter` etc. work.

**Alternatives considered**:
- **Single config file (e.g. `.d2net.init.toml`)**: more declarative but adds a parser and a config schema. Rejected as YAGNI; the four inputs comfortably fit on a flag line.
- **Always interactive**: matches some `npm init`-style tools but breaks CI use. Rejected.

---

## R4 — Archive / backup / old heuristic (FR-007)

**Decision**: Closed list of case-insensitive substring markers, matched against the **leaf name** of each directory under the source tree:

```text
archive  archives
backup   backups   bak
old      legacy
obsolete deprecated
attic
```

A directory matches if its leaf name (lower-cased) contains any of the markers as a contiguous substring. The match list is hard-coded in `ExclusionDetector.cs`; tuning it is out of scope for the MVP.

**Rationale**:
- Substring (not prefix/suffix) catches `archive_2024`, `2023_backups`, `old-stuff`, `legacy_lib`, etc.
- Matching the leaf only avoids over-matching paths that happen to *contain* a project named `legacy/` deep in their tree but really mean something else.
- Closed list keeps SC-010 testable.

**Alternatives considered**:
- **Word-boundary regex** (`\b(archive|backup|...)\b`): cleaner conceptually but trips on names like `archived_old.dart_tool` and similar; substring is more forgiving and meets the user's "or similar variants and abbreviations" intent.
- **`.gitignore`-style glob list in a config file**: out of scope; no user request for it.

---

## R5 — Schema for the workspace database

**Decision**: As fixed in clarifications Q4 / FR-012 / FR-013 / FR-014 / FR-015 / FR-016. Captured authoritatively in `contracts/db-schema.sql`. Summary:

```sql
CREATE TABLE setting               (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE excluded_directories  (path TEXT PRIMARY KEY, kind TEXT NOT NULL);
CREATE TABLE dart_files            (id BIGSERIAL PRIMARY KEY,
                                    filename TEXT NOT NULL,
                                    full_path TEXT NOT NULL UNIQUE);
CREATE TABLE phase_sequence        (phase TEXT PRIMARY KEY, sequence INTEGER NOT NULL);
CREATE TABLE phase_status          (phase TEXT PRIMARY KEY,
                                    status TEXT NOT NULL,
                                    last_updated TIMESTAMPTZ NOT NULL DEFAULT now());
```

The `excluded_directories.kind` column is a small extension over the literal spec text: it records *why* the directory is excluded (`tool` for well-known tool subdirs the developer opted into, `pattern` for archive/backup/old-style matches, `manual` for `--exclude` flags). It defaults to `manual` when source attribution is ambiguous. This adds zero burden on consumers (they can ignore it) but lets future D2NET commands and `--Exclusions --json` show *why* each exclusion exists.

**Rationale**:
- Flat key/value `setting` (Q4) keeps the JSON mirror straightforward.
- `dart_files.full_path` is `UNIQUE` to enforce the "one row per Dart file" invariant (`(filename, full_path)` is too weak — two files in different folders can share the same filename, but each has a distinct full_path).
- `last_updated TIMESTAMPTZ DEFAULT now()` lets downstream commands `INSERT` a phase row without specifying the timestamp, and `--current-phase` prints in ISO-8601 by default.

**Alternatives considered**:
- **Composite PK `(phase)`** vs surrogate-key `id` for phase tables: phase is naturally unique; surrogate key adds nothing.
- **`dart_files.id` as `INTEGER` GENERATED ALWAYS AS IDENTITY**: equivalent to `BIGSERIAL` here; `BIGSERIAL` is shorter and matches what most Postgres ecosystems write.

---

## R6 — Where connection details live, and what fields they include

**Decision**: Persisted **twice** — in `D2NET-Settings.json` (under the `connection` key) and as rows in the `setting` table — and the two MUST agree.

Fields written (one row per key in `setting`, one nested object key in JSON):

| key | example value |
|-----|---------------|
| `source_dir` | `glp_runtime` |
| `target_extension` | `_net` |
| `target_dir` | `glp_runtime_net` |
| `db_engine` | `sqlite` |
| `db_file` | `D:\BSTDEV\research\GLP\GLPNET\.D2NET\pgdb\workspace.sqlite` |
| `db_connection_string` | `Data Source=D:\BSTDEV\research\GLP\GLPNET\.D2NET\pgdb\workspace.sqlite` |

**Rationale**:
- Storing both forms means external clients can paste the `connection_string` directly into a SQLite tool, while D2NET commands can read individual fields if they ever need to override one (e.g. open `Mode=ReadOnly` for inspection).
- No password — SQLite uses file-system permissions, which match the workspace's per-developer model.

**Alternatives considered**:
- **Settings file only, DB has no copy**: violates FR-010 ("MUST also be persisted as rows in the `setting` table").

---

## R7 — Error handling and clean-up on abort (FR-022)

**Decision**: Build the workspace in a temp staging folder, move-into-place atomically on success.

Concretely:
1. `InitRunner` creates `<repo-root>/.D2NET.tmp.<guid>/` and writes everything (PGLite data files, `D2NET-Settings.json`) under it.
2. Spawns the bridge against the temp folder, runs DDL, populates tables.
3. Stops the bridge.
4. If everything succeeded, `Directory.Move(tmp, ".D2NET")`.
5. On any failure between (1) and (4), `Directory.Delete(tmp, recursive: true)` is performed in a `finally` block. The repo root remains in its pre-init state.
6. For the `--FORCE --DELETE-EXISTING` path, the existing `.D2NET` is renamed to `.D2NET.deleting.<guid>` first (cheap), then a fresh init is performed in `.D2NET.tmp.<guid>`, then on success the temp is renamed to `.D2NET` and the `.deleting` folder is deleted recursively. On failure mid-way, the `.deleting` folder is renamed back to `.D2NET` so the user does not lose their previous workspace.

**Rationale**:
- File-system rename is atomic on every supported OS as long as source and destination are on the same volume (which is guaranteed since both are under the repo root).
- Delete-recursive of a temp folder is safe on failure paths because nothing else points at that GUID-named folder.
- Avoids any need for SQL rollback — the database files themselves are part of the temp folder.

**Alternatives considered**:
- **Write directly to `.D2NET/` and clean up in `finally`**: simpler but leaves a partial workspace if the process is killed (Ctrl-C, taskkill). The temp+rename pattern survives kill mid-write.
- **Database-level transactions on schema creation**: PGLite supports them but the schema-init path is so short that the additional rollback complexity is not worth it; the temp+rename approach covers the only meaningful failure mode (process crash mid-init).

---

## R8 — Discovery of well-known tool subdirectories (FR-006)

**Decision**: A static list checked against the source tree at scan time:

```text
.git          .dart_tool    build         .idea         .vscode
node_modules  bin           obj           .gradle       .next
.pytest_cache .venv         venv          .nuget        .terraform
```

The first five (matching the `D2Net.Scaffold` pruned-dir set in `001-d2net-scaffold`) are mandatory; the remainder are added because they commonly appear in mixed-language repos and the spec authorises extending the list (FR-006 says "at minimum"). Each detected directory is offered to the user as a single yes/no toggle (default = yes, exclude). In `--non-interactive` mode the default is taken without prompting.

**Rationale**:
- Aligning the first five with the existing `D2Net.Scaffold` pruning makes the two tools' world-views consistent — a directory pruned by Scaffold's mirror operation is also excluded from Init's inventory.
- The expanded list saves the user from manually `--exclude`-ing the obvious cases.

**Alternatives considered**:
- **Read `.gitignore`**: would also catch project-specific patterns but introduces a parser and unclear semantics for negative matches. Rejected as out of scope for the MVP.
- **Same hard-coded set as `D2Net.Scaffold`**: too narrow; the spec explicitly allows extending.

---

## R9 — Output format for inspection options

**Decision**: As fixed in Q5 / FR-019a. Plain-text default, stable JSON behind `--json`.

| Option | Plain text (default) | `--json` document |
|--------|---------------------|-------------------|
| `--list` | one line per Dart file: `<filename>\t<full_path>`, sorted by `full_path` ascending | `{"dart_files":[{"id":..,"filename":..,"full_path":..},...]}` |
| `--Exclusions` | one line per excluded path, sorted ascending | `{"excluded_directories":[".git","build/legacy",...]}` |
| `--current-phase` | one line: `<phase>\t<status>\tlast_updated=<ISO-8601>` (or `no active phase`) | `{"phase":..,"status":..,"last_updated":..,"sequence":..}` or `{"phase":null}` |

JSON output uses `System.Text.Json` with `WriteIndented = false` (compact, one-line) so it pipes cleanly into `jq`. Diagnostics in `--json` mode go to stderr; stdout is JSON-only.

**Rationale**:
- TSV (`\t`) for plain text is a deliberate choice over space-aligned columns: it pipes into `awk`/`cut`, parses unambiguously, and avoids quoting concerns when filenames contain spaces.
- Compact JSON keeps `--json` mode terse for shell pipelines while remaining trivial to pretty-print on demand.

**Alternatives considered**:
- **Pretty-printed JSON by default in `--json` mode**: rejected — pipelines should not have to deal with multi-line JSON; users who want it can always pipe through `jq .`.

---

## R10 — Test approach

**Decision**: Integration-test-heavy with thin unit tests, mirroring `D2Net.Scaffold.Tests`. Each integration test:
1. Builds a small fixture repo in `Path.GetTempPath()/d2net-init-tests/<guid>/repo/<source>` (synthetic `.dart` files plus archive/backup directories).
2. Invokes `Program.Run(...)` against that repo with controlled stdin/stdout/stderr captured into `StringWriter`s and the CWD passed in explicitly.
3. Verifies the resulting `.D2NET` workspace by:
   - Asserting on file-system shape (`.D2NET/D2NET-Settings.json` exists, `pgdb/workspace.sqlite` exists).
   - Opening the same SQLite file (`Microsoft.Data.Sqlite`, `Mode=ReadOnly`) via the `DbVerifier` test helper and asserting on table contents.
4. Tears down the temp folder in `IDisposable.Dispose`.

**Rationale**:
- Tests open the workspace database with the same client the production code uses; there is no bridge to mock or substitute.
- The `Program.Run` overload that takes (`args[]`, `stdin`, `stdout`, `stderr`, `cwd`) makes the entire CLI testable without subprocesses, so the test suite is fast (≈3 seconds for 70 tests on a workstation) and parallelisable.

**Alternatives considered**:
- **Shell out to the published `d2net-init.exe`**: more end-to-end but slower and harder to assert on stdout/stderr precisely. Reserved for the manual `quickstart.md` walkthrough.

---

## Summary of resolved unknowns

| ID | Topic | Status |
|----|-------|--------|
| R1 | Storage engine | Resolved → embedded SQLite via Microsoft.Data.Sqlite (Q6 pivot from PGLite) |
| R2 | Bridge TCP port | Removed (no longer applicable after Q6); flag preserved as deprecated no-op |
| R3 | Input collection (CLI flags vs prompts) | Resolved → flags + prompt fallback + `--non-interactive` |
| R4 | Archive/backup/old heuristic | Resolved → closed substring marker list against leaf name |
| R5 | DB schema | Resolved → captured in `contracts/db-schema.sql` (SQLite syntax) |
| R6 | Connection field set | Resolved → 6 keys (3 dirs + 3 connection), persisted in both JSON and `setting` |
| R7 | Atomicity / abort cleanup | Resolved → temp staging folder + atomic rename + `ClearAllPools` before move |
| R8 | Well-known tool subdirs | Resolved → 15-entry static list, first five mandatory |
| R9 | Inspection output format | Resolved → TSV plain text + compact JSON behind `--json` |
| R10 | Test approach | Resolved → integration tests opening the same SQLite file via `DbVerifier` |

No `NEEDS CLARIFICATION` items remain.
