---
name: codeconv-runner
description: Forward arguments to the `codeconv` Python console script. Use when the user types `/codeconv-runner` or asks for a list of registered codeconv tools, codeconv doctor diagnostics, or codeconv migrations.
---

# /codeconv-runner

Thin wrapper over the `codeconv` console script. Forwards all arguments verbatim.

## What this skill does

1. Resolve the codeconv venv (`codeconv/.venv/Scripts/python.exe` on Windows, `codeconv/.venv/bin/python` on POSIX). If absent, instruct Gabi to run `python -m venv codeconv/.venv && codeconv/.venv/Scripts/python.exe -m pip install -e codeconv[dev]` first.
2. Invoke `codeconv <args verbatim>` from the repo root.
3. Show stdout/stderr from the run.

## Built-in commands the runner exposes

| Command | Effect |
|---|---|
| `codeconv list` | Print registered tools (one per line). |
| `codeconv doctor` | Diagnose bridge daemon, sidecar, schemas, psycopg loaders. Exit 0 if green. |
| `codeconv migrate` | Run Alembic upgrade head (creates `codeconv` schema), then DBOS launch (creates `dbos` schema). Idempotent. |
| `codeconv <tool> ...` | Invoke a registered tool by name (e.g. `codeconv discover`). Tools live under `codeconv/src/codeconv/tools/<name>/` and are auto-discovered. |
| `codeconv --version` | Print package version. |

## Global flags

- `--repo-root <path>` — locate tools and `.codeconv/` (default cwd).
- `--data-dir <path>` — override the PGLite cluster location (default `<repo-root>/.pgdb`). Use when the repo lives on a filesystem PGLite can't use (notably **exFAT** — atomic-rename / advisory-lock / mmap operations fail mid-DBOS-migration). Point this at a directory on an NTFS volume, e.g. `--data-dir $env:LOCALAPPDATA/codeconv-pgdb`. The bridge sidecar, OS lock (`<data-dir>.bridge.lock/`), consumer registrations (`<data-dir>.consumers/`), and force-shutdown marker all follow the override.
- `--bridge-port <int>` — override sidecar discovery (debugging only).
- `--quiet` — suppress non-error output.
- `--json` — emit machine-readable summaries on subcommands that support it.

## Pre-execution checks

- Bridge daemon is auto-spawned by `acquire_or_discover` per `specs/012-codeconv-runner/contracts/bridge_lifecycle.md`. The first `codeconv` invocation in a fresh repo pays a ~7 s PGLite cold-init penalty; subsequent invocations connect to the already-running bridge daemon.
- All `codeconv`-launched processes register themselves under `<repo>/.pgdb.consumers/<pid>.lock`. The bridge daemon polls these and shuts down ~30 s after the last consumer exits (orphan-shutdown). To force an immediate non-destructive shutdown: `New-Item <repo>/.pgdb.shutdown` (Windows) or `touch <repo>/.pgdb.shutdown` (POSIX) — bridge polls the marker every second and exits gracefully.

## Examples

- `/codeconv-runner list` → forwards to `codeconv list`
- `/codeconv-runner doctor --json` → forwards to `codeconv doctor --json`
- `/codeconv-runner migrate` → runs Alembic + DBOS migrations
- `/codeconv-runner discover --root glp_runtime_net` → invokes the discover tool (after Phase 6 lands it)

## What this skill does NOT do

- Does NOT spawn the bridge daemon directly — that's `acquire_or_discover`'s job inside `codeconv`.
- Does NOT modify anything on disk beyond what `codeconv` itself writes.
- Does NOT replace `/D2NET-init` or `/D2NET-scaffold` — those use the same bridge daemon but their own CLI surface.

## Contract

`specs/012-codeconv-runner/contracts/codeconv_runner_cli.md` is the source of truth. This skill MUST stay in sync with that contract; if you change behavior here, update the contract first.
