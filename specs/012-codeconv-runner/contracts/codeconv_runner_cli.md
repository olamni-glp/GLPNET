# Contract: `codeconv` Python CLI

Source: spec FR-013, FR-014, FR-015, FR-016, FR-017; clarifications Q11; research R5, R6, R10.

The `codeconv` console script (entry point declared in `codeconv/pyproject.toml`) is the source of truth for `/codeconv-runner` and all `/codeconv-<name>` skills. The slash skills are thin wrappers that forward arguments verbatim (FR-013).

## Invocation

```
codeconv [GLOBAL FLAGS] <command> [COMMAND FLAGS]
```

## Global flags

| Flag | Type | Default | Semantics |
|---|---|---|---|
| `--repo-root <path>` | path | cwd | Locates `.pgdb/` (and tools, and `.codeconv/`). |
| `--bridge-port <int>` | int | autodiscover | Override sidecar discovery (debugging only). |
| `--quiet` | bool | false | Suppress non-error output. |
| `--json` | bool | false | Emit machine-readable summaries on subcommand outputs that support it. |
| `--help` | bool | false | Typer-generated help; surfaces all subcommands. |
| `--version` | bool | false | Print package version, exit 0. |

## Built-in commands

### `codeconv list`

Print registered tools and their summaries.

- Exit 0 always; output one line per tool: `<name>  <one-line-help>`.
- With `--json`: emit `[{name, description, slash_command}, ...]`.

### `codeconv doctor`

Diagnose bridge, sidecar, schemas, and DBOS state.

- Checks: bridge reachable; `.pgdb/bridge.json` shape valid; `dbos`, `codeconv` schemas exist; psycopg loaders patched.
- Exit 0 if all green; non-zero with a structured report listing each failure.

### `codeconv migrate`

Run DBOS + codeconv schema migrations against the unified bridge.

- Idempotent. Used by the runner before any tool invocation, but exposed to operators for explicit control.
- Applies `_apply_pglite_compat_patch()` (R6) before DBOS migrations.

### `codeconv <tool-name> [tool-flags]`

Invoke a registered tool by name. The runner discovers tools via R10 (file-system scan of `codeconv/src/codeconv/tools/<name>/`). Each tool exposes its own Typer sub-app; flags are tool-specific and documented per-tool.

If `<tool-name>` is unknown: exit 64 (`EX_USAGE`) with a list of registered tools.

## Tool registration contract

A registered tool is a Python subpackage `codeconv.tools.<name>` exporting at least:

```python
import typer

app: typer.Typer = typer.Typer(help="One-line description.")

# Optional: register DBOS workflows at runner startup.
def register_workflows(dbos_app) -> None:
    ...
```

The runner's main `codeconv` Typer app does:

```python
import pkgutil, importlib
for module_info in pkgutil.iter_modules(codeconv.tools.__path__):
    tool = importlib.import_module(f"codeconv.tools.{module_info.name}")
    main_app.add_typer(tool.app, name=module_info.name)
    if hasattr(tool, "register_workflows"):
        tool.register_workflows(dbos_app)
```

**No edits to `codeconv/runner.py` or `codeconv/cli.py` are required to add a new tool** (FR-016). The companion `.claude/skills/codeconv-<name>/SKILL.md` IS a per-tool deliverable (it must exist for the slash command to work) but it is not "the runner's own code" — adding a skill is intrinsic to delivering a new tool.

## DBOS workflow contract

Each tool that performs durable work MUST wrap its main loop in a DBOS workflow (FR-017). The codeconv-runner exposes `from codeconv.runner import workflow` (a re-export of `dbos.workflow`) for tool authors. Per-tool workflows MAY checkpoint per logical unit of work (e.g., per file in `discover`).

Resume: re-invoking the same command with the same arguments rejoins the existing workflow if one is mid-flight (DBOS's standard resume semantics). Tools MUST be deterministic enough that re-execution of an already-completed step is a logical no-op.

## Engine + connection sequence

On startup, the runner does (in this exact order):

```python
from codeconv._vendor.pglite_engine_kwargs import pglite_engine_kwargs
from codeconv._vendor.pglite_compat_loaders import apply_to_engine
from codeconv.bridge_client import acquire_or_discover

endpoint = acquire_or_discover(repo_root)
url = f"postgresql+psycopg://postgres:postgres@{endpoint.host}:{endpoint.port}/postgres"
engine = create_engine(url, **pglite_engine_kwargs(application_name='codeconv'))
apply_to_engine(engine)

# Apply DBOS startup patch BEFORE dbos.launch()
from codeconv._vendor.pglite_engine_kwargs import _apply_pglite_compat_patch  # exposed by upstream
_apply_pglite_compat_patch()
dbos = DBOS(config=DBOSConfig(database_url=url, db_engine_kwargs=pglite_engine_kwargs('codeconv'), schema='dbos'))
dbos.launch()
apply_to_engine(dbos.app_db.engine)  # safe-loaders on DBOS-side engine too
```

This is the contract — implementations MUST follow this sequence (FR-014).

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | Generic error (caught exception with diagnostic message) |
| 2 | Bridge unreachable / startup failed |
| 64 | Usage error (unknown tool, bad flag) |
| 65 | Data error (e.g., corrupt tombstone in `--from-tombstones` mode) |
| 70 | Internal error (unhandled exception; bug to file) |

## What MUST NOT happen

- The runner MUST NOT call DBOS `migration_one` without `_apply_pglite_compat_patch()` applied first (per FR-014).
- The runner MUST NOT create tables in `public` schema (FR-015).
- The runner MUST NOT issue `COPY ... FROM STDIN` against PGLite (FR-026).
- The runner MUST NOT enable client-side prepared-statement caching (FR-027 + applicability.md).
