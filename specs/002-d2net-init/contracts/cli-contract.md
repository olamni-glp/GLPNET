# CLI Contract — `d2net-init`

This is the public command-line contract for the `d2net-init` tool. It is the single human/script-facing interface for the `D2NET.Init` feature; downstream tools and CI scripts depend on its arguments, exit codes, and stdout/stderr behaviour.

## Invocation forms

```text
# Fresh init (interactive — any missing input is prompted for)
d2net-init [--source <name>] [--target-extension <ext>] [--target <name>]
           [--exclude <path> ...] [--accept-suggested-exclusions]
           [--non-interactive] [--bridge-port <port>]

# Destructive re-init
d2net-init --FORCE --DELETE-EXISTING
           [--source <name>] [--target-extension <ext>] [--target <name>]
           [--exclude <path> ...] [--accept-suggested-exclusions]
           [--non-interactive] [--bridge-port <port>]

# Inspection
d2net-init --list           [--json] [--bridge-port <port>]
d2net-init --Exclusions     [--json] [--bridge-port <port>]
d2net-init --current-phase  [--json] [--bridge-port <port>]

# Help / version
d2net-init --help
d2net-init -h
d2net-init --version
```

During development the canonical run form is:

```text
dotnet run --project tools/d2net/src/D2Net.Init -- <flags as above>
```

After `dotnet publish -c Release`, invocation becomes `d2net-init.exe <flags>` plus the published `pgbridge/` folder sitting next to the executable.

## Mode selection

The CLI runs in exactly one of three modes per invocation; the modes are mutually exclusive.

| Mode | Triggered by | Description |
|------|-------------|-------------|
| **fresh-init** | Default (no inspection flag, `.D2NET/` absent) | Creates the workspace from scratch. Refuses if `.D2NET/` already exists unless `--FORCE --DELETE-EXISTING` is set. |
| **force-delete-init** | `--FORCE --DELETE-EXISTING` (both required) | Deletes any existing `.D2NET/` and runs fresh-init. |
| **inspect** | `--list`, `--Exclusions`, or `--current-phase` (exactly one) | Read-only. Refuses if `.D2NET/` does not exist. Requires no other input flags except `--json` and `--bridge-port`. |

Mixing flags from different modes (e.g. `--list --source foo`, or `--FORCE` without `--DELETE-EXISTING`) is an argument error (exit 1).

## Flags

| Flag | Description | Mode |
|------|-------------|------|
| `--source <name>` | Source directory name relative to repo root. If absent in interactive mode, prompted for. | fresh-init, force-delete-init |
| `--target-extension <ext>` | Target directory suffix (e.g. `_net`). May be empty. | fresh-init, force-delete-init |
| `--target <name>` | Target directory name. | fresh-init, force-delete-init |
| `--exclude <path>` | A relative path under `<source>` to exclude. Repeatable. | fresh-init, force-delete-init |
| `--accept-suggested-exclusions` | Skip the exclusion prompt cycle; keep all auto-detected items. | fresh-init, force-delete-init |
| `--non-interactive` | Treat any missing required input as an error rather than prompting. | fresh-init, force-delete-init |
| `--FORCE` | Required (with `--DELETE-EXISTING`) to overwrite an existing workspace. **Case-sensitive uppercase per the spec.** Lowercase `--force` MUST also be accepted as a synonym. | force-delete-init |
| `--DELETE-EXISTING` | Required (with `--FORCE`). **Case-sensitive uppercase per the spec.** Lowercase `--delete-existing` MUST also be accepted. | force-delete-init |
| `--list` | Inspection: list all rows of `dart_files`. | inspect |
| `--Exclusions` | Inspection: list all rows of `excluded_directories`. (Spec spelling preserved; lowercase `--exclusions` MUST be accepted as synonym.) | inspect |
| `--current-phase` | Inspection: print the lowest-sequence non-COMPLETED `phase_status` row, or `no active phase` if none. | inspect |
| `--json` | In inspection modes, emit compact JSON to stdout and route diagnostics to stderr. | inspect |
| `--bridge-port <port>` | **Deprecated** after the SQLite pivot (FR-023). Accepted and silently ignored for backward compatibility. | all |
| `--help`, `-h` | Print usage and exit 0. | all |
| `--version` | Print version and exit 0. | all |

## Exit codes

| Code | Meaning |
|-----:|---------|
| 0 | Success. |
| 1 | Generic argument or invocation error (mutually-exclusive flags, unrecognised flag, etc.). Usage hint on stderr. |
| 2 | Repo-root validation failed: CWD does not look like a repo root (FR-002 wrong-CWD case). |
| 3 | `.D2NET/` already exists and `--FORCE --DELETE-EXISTING` was not supplied (or was supplied incompletely). Per FR-003. |
| 4 | Source directory does not exist as a direct subdirectory of CWD. Per FR-004. |
| 5 | (Reserved.) Was "bridge port in use" before the SQLite pivot; retained as a constant for backward compatibility but no current code path returns this. |
| 6 | Inspection mode invoked but `.D2NET/` (or the SQLite database file under it) does not exist. Per FR-020. |
| 7 | (Reserved.) Was "bridge failed to start" before the SQLite pivot; retained as a constant. |
| 8 | Could not open the workspace SQLite database file (locked, corrupt, permission denied, etc.). Stderr names the file path. |
| 9 | Interactive prompt was cancelled (Ctrl-C, EOF, terminal closed). Workspace, if partially created, is removed before exit (FR-022). |

A non-zero exit code MUST be accompanied by a human-readable explanation on stderr. In `--json` inspection mode the explanation MUST go to stderr; stdout MUST remain empty.

## stdout / stderr

### Fresh-init success (FR-021)

```text
d2net-init: workspace ready at D:\BSTDEV\RESEARCH\glp\glpnet\.D2NET
  Source           : glp_runtime
  Target extension : _net
  Target           : glp_runtime_net
  Settings file    : D:\BSTDEV\RESEARCH\glp\glpnet\.D2NET\D2NET-Settings.json
  Database         : D:\BSTDEV\RESEARCH\glp\glpnet\.D2NET\pgdb (PGLite, port 54329 while bridge runs)
  Excluded dirs    : 7 (3 well-known tool, 2 archive/backup, 2 manual)
  Dart files       : 207
  Created at       : 2026-04-30T12:34:56Z
```

### Inspection — `--list` (plain)

```text
runner.dart	glp_runtime/lib/runtime/runner.dart
heap.dart	glp_runtime/lib/runtime/heap.dart
... (one line per Dart file, sorted by full_path)
```

### Inspection — `--list --json`

```json
{"dart_files":[{"id":1,"filename":"runner.dart","full_path":"glp_runtime/lib/runtime/runner.dart"},{"id":2,"filename":"heap.dart","full_path":"glp_runtime/lib/runtime/heap.dart"}]}
```

### Inspection — `--Exclusions` (plain)

```text
.dart_tool
.git
.idea
.vscode
build
lib/legacy
old_experiments
```

### Inspection — `--Exclusions --json`

```json
{"excluded_directories":[".dart_tool",".git",".idea",".vscode","build","lib/legacy","old_experiments"]}
```

### Inspection — `--current-phase` (plain, active phase exists)

```text
analyze	IN_PROGRESS	last_updated=2026-04-30T13:01:22Z
```

### Inspection — `--current-phase` (plain, no active phase)

```text
no active phase
```

### Inspection — `--current-phase --json`

```json
{"phase":"analyze","status":"IN_PROGRESS","last_updated":"2026-04-30T13:01:22Z","sequence":2}
```

or, if no active phase exists:

```json
{"phase":null}
```

### Interactive exclusion-approval flow (transcript example)

```text
Suggested exclusions (8):
  [tool   ] .git
  [tool   ] .dart_tool
  [tool   ] build
  [tool   ] .idea
  [tool   ] .vscode
  [pattern] archive_2024
  [pattern] lib/legacy
  [pattern] old_experiments
Actions:
  [a]ccept all       — approve the list as-is
  [r]emove <n>       — remove item by row number
  [l]ist             — redisplay current list
  [q]uit             — abort init
> r 6
Suggested exclusions (7):
  [tool   ] .git
  ...
> a
Approved.
```

The prompt loop continues until the user types `a` or `q` (FR-008). On `q` the run aborts with exit code 9.

## Idempotency guarantees

- **fresh-init mode is strictly create-only**: it refuses to touch an existing `.D2NET/`.
- **force-delete-init mode** is destructive but atomic: on failure the previous workspace is restored (R7).
- **inspect mode** is strictly read-only: it modifies zero bytes under `.D2NET/` (SC-009). Running an inspect command in a loop is safe.

## Notes for downstream tools

- The settings file (`<repo-root>/.D2NET/D2NET-Settings.json`) is part of this contract — see `settings-schema.json`.
- The DB schema is part of this contract — see `db-schema.sql`. Downstream tools can rely on the five tables existing with the documented columns.
- The workspace database is **embedded SQLite** at `<repo-root>/.D2NET/pgdb/workspace.sqlite`. Downstream tools open it directly with any standard SQLite client (`Microsoft.Data.Sqlite`, `sqlite3` CLI, DB Browser for SQLite, JetBrains DataGrip with the SQLite driver, etc.). No daemon, no TCP listener, no ODBC driver dependency.
