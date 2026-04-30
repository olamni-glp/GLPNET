# CLI Contract — `d2net-init` (PGLite upgrade)

**Feature**: `005-d2net-pglite-bridge` — see [spec.md](../spec.md), [plan.md](../plan.md)

This contract describes the `d2net-init` CLI surface after the storage swap. Every flag and behavior from the shipped 002 contract carries forward unchanged unless explicitly noted as **MODIFIED** or **NEW**. The shipped 002 cli-contract.md is the baseline; this document is the diff.

## Invocation forms

```text
# Init (fresh or --FORCE --DELETE-EXISTING)
d2net-init [--source <name>] [--target-extension <ext>] [--target <name>]
           [--exclude <path> ...] [--accept-suggested-exclusions]
           [--non-interactive] [--bridge-port <port>]
           [--FORCE --DELETE-EXISTING]

# Inspection (mutually exclusive)
d2net-init --list           [--json] [--bridge-port <port>]
d2net-init --Exclusions     [--json] [--bridge-port <port>]
d2net-init --current-phase  [--json] [--bridge-port <port>]

# Misc
d2net-init --help
d2net-init --version
```

## Flag-by-flag delta from shipped 002

| Flag                              | Shipped 002 behaviour              | Upgrade behaviour                                                                                                                                                                                              |
|-----------------------------------|------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `--source`                        | unchanged                          | unchanged                                                                                                                                                                                                      |
| `--target-extension`              | unchanged                          | unchanged                                                                                                                                                                                                      |
| `--target`                        | unchanged                          | unchanged                                                                                                                                                                                                      |
| `--exclude`                       | unchanged                          | unchanged                                                                                                                                                                                                      |
| `--accept-suggested-exclusions`   | unchanged                          | unchanged                                                                                                                                                                                                      |
| `--non-interactive`               | unchanged                          | unchanged                                                                                                                                                                                                      |
| `--FORCE` / `--DELETE-EXISTING`   | unchanged                          | unchanged. **Detection extended (FR-014)** to also catch SQLite-era workspaces (presence of `.D2NET/pgdb/workspace.sqlite`, or `connection.engine != "pglite"` in settings).                                   |
| `--list` / `--Exclusions` / `--current-phase` | each does a single SQL read-only query; `--json` switches output | unchanged user-visible behavior. Internally now spawns its own bridge subprocess against the persisted `connection.port` (or `--bridge-port` override).                                                       |
| `--json`                          | unchanged                          | unchanged                                                                                                                                                                                                      |
| `--bridge-port <int>`             | **NO-OP** (FR-023 of 002 marked it deprecated under SQLite) | **MEANINGFUL** (FR-012 repeals the no-op). Init: writes the chosen port to settings. Inspection: overrides the persisted port for the live invocation only — does **not** modify settings. Default: `54400`. |
| `--help`                          | unchanged                          | help text mentions the new bridge port semantics and the Node.js requirement                                                                                                                                  |
| `--version`                       | bumps to `0.2.0`                   | bumps to `0.2.0` (signals the storage-engine swap)                                                                                                                                                             |

## Exit codes

Inherits the shipped 002 numbering plus two new codes for bridge-related failures.

| Code | Symbol                              | Meaning                                                                                                          |
|------|-------------------------------------|------------------------------------------------------------------------------------------------------------------|
|  0   | `Success`                           | Operation completed                                                                                              |
|  1   | `ArgumentError`                     | Bad CLI flags / missing required value                                                                           |
|  2   | `WrongCwd`                          | CWD does not look like a D2NET repository root (FR-002 of 002)                                                   |
|  3   | `WorkspaceAlreadyExists`            | `.D2NET` exists and `--FORCE --DELETE-EXISTING` not supplied (FR-003 of 002 + FR-014 of 005)                     |
|  4   | `SourceDirMissing`                  | Supplied source directory does not exist                                                                         |
|  5   | `BridgePortInUse`                   | Requested bridge port is already bound; init aborted                                                             |
|  6   | `WorkspaceMissingForInspection`     | Inspection invocation found no `.D2NET` workspace                                                                |
|  7   | `BridgeStartFailed`                 | Bridge subprocess timed out waiting for `BRIDGE_READY`, or printed `BRIDGE_ERROR` other than `pglite_init_failed` |
|  8   | `DbOpenFailed`                      | Bridge `BRIDGE_ERROR pglite_init_failed`, or `NpgsqlException` opening the connection (corrupt-data hint emitted in this case)             |
|  9   | `InteractivePromptCancelled`        | Ctrl-C / EOF during interactive prompt                                                                           |
| 10   | `NodeMissing`                       | `node` not on PATH, or below the minimum supported version (R8: 20+ LTS)                                         |
| 11   | `BridgeBundleMissing`               | Vendored `pgbridge/bridge-direct.mjs` or its `node_modules` is missing/corrupt                                   |

## Stdout/stderr conventions (preserved from 002)

- All human-readable diagnostics on stderr.
- Init's success summary on stdout (FR-021 of 002).
- Inspection options' data on stdout (plain text or JSON per `--json`).
- `--json` mode emits no banners/progress on stdout.

## NEW: bridge-related diagnostics

When the bridge subprocess fails to start or returns a `BRIDGE_ERROR`:

```text
# stdout: empty (always)
# stderr (example for pglite_init_failed):
PGLite bridge failed to open the workspace database: BRIDGE_ERROR pglite_init_failed <node-side message>
The workspace database appears to be unreadable. To rebuild from the source tree, re-run with:
  d2net-init --FORCE --DELETE-EXISTING [other flags...]
exit code 7
```

When the bridge port is already bound:

```text
# stderr:
PGLite bridge port 54400 is already in use. Either stop the conflicting process, or supply --bridge-port <n>.
exit code 17
```

When `node` is missing or too old:

```text
# stderr:
The PGLite bridge requires Node.js >= 20 on PATH.
Install Node.js LTS from https://nodejs.org/ and retry.
exit code 16
```

When the vendored bundle is missing:

```text
# stderr:
The PGLite bridge bundle is missing or corrupt. Reinstall d2net-init.
expected: <abs path to pgbridge/bridge-direct.mjs>
exit code 18
```

## NEW: bridge-port lifecycle (FR-012, Q3)

Init: persists the chosen port to `connection.port` in `D2NET-Settings.json` and to `db_port` in the `setting` table.

Inspection: defaults to the persisted `connection.port`. `--bridge-port <n>` on an inspection invocation overrides only the live run; settings are never rewritten by inspection.

Concrete examples:

```text
# Fresh init on default port (writes connection.port = 54400)
d2net-init --source glp_runtime --target-extension _net --target glp_runtime_net \
           --accept-suggested-exclusions --non-interactive

# Fresh init on a custom port (writes connection.port = 55000)
d2net-init --source glp_runtime --target-extension _net --target glp_runtime_net \
           --accept-suggested-exclusions --non-interactive --bridge-port 55000

# Inspection: uses the persisted port (54400 in case 1, 55000 in case 2)
d2net-init --list

# Inspection: live override; settings UNCHANGED
d2net-init --list --bridge-port 56000

# Re-init writes a new port
d2net-init --FORCE --DELETE-EXISTING --bridge-port 60000 [...]
# settings now have connection.port = 60000
```
