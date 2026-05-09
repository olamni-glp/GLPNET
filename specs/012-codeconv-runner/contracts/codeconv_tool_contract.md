# Contract: codeconv tool registration interface

Source: spec FR-016; research R10; reference: BREENDEV `/opskit-init` skill.

This contract specifies the interface a registered codeconv tool exposes to the runner. Adding a new tool is a two-deliverable change: (a) a new Python subpackage; (b) a new Claude Code skill. No edits to runner code.

## Required: Python subpackage shape

```
codeconv/src/codeconv/tools/<tool-name>/
├── __init__.py            # Exports `app` and (optionally) `register_workflows`
├── workflow.py            # DBOS workflow + per-step functions
└── (any other tool internals)
```

### `__init__.py` MUST export

```python
import typer

app: typer.Typer = typer.Typer(
    help="One-line summary of the tool, shown by `codeconv list`.",
    add_completion=False,
)

@app.command("run")  # or any command name(s)
def run(
    # tool-specific flags
    ...
) -> None:
    """Run the tool. Implementation may delegate to a DBOS workflow."""
    ...
```

The `app` attribute is required. It MUST be a `typer.Typer` instance.

### `__init__.py` MAY export

```python
def register_workflows(dbos_app) -> None:
    """Register DBOS workflows at runner startup. Optional but required if the tool uses durable workflows."""
    from .workflow import my_workflow
    dbos_app.register_workflow(my_workflow)
```

If the tool uses durable workflows (FR-017), this function MUST be present. The runner calls it after `dbos.launch()`.

## Required: companion Claude Code skill

```
.claude/skills/codeconv-<tool-name>/
└── SKILL.md
```

The skill MUST be a thin wrapper following the `/D2NET-init` and `/opskit-init` patterns:

- YAML frontmatter declares `name`, `description`, `argument-hint`, `compatibility`.
- The skill body documents the slash → CLI mapping (`/codeconv-<name> [args]` → `codeconv <name> [args]`).
- The skill MUST NOT interpret arguments — it forwards them verbatim to the underlying CLI.
- Skills MAY add a destructive-operation gate or confirmation step BEFORE invoking the CLI, mirroring `/D2NET-init`'s Step 6 protocol, IF the tool can perform destructive operations.

## Tool naming rules

- The tool's directory name is `<tool-name>` (kebab-case allowed; Python imports use Python's auto-translation).
- The slash command is `/codeconv-<tool-name>`.
- The CLI subcommand is `codeconv <tool-name>` (Typer auto-routes).
- The DBOS schema for tool-specific tables (if any) MUST be `codeconv` — the existing schema; tools MAY add their own tables to the codeconv schema via Alembic migrations under `codeconv/db/migrations/`. Tools MUST NOT create new schemas without explicit spec amendment.

## Discovery semantics

At runner startup, `pkgutil.iter_modules(codeconv.tools.__path__)` yields every direct subpackage of `codeconv.tools`. The runner imports each, looks for `app` (required) and `register_workflows` (optional), and adds the tool to the main Typer app.

Tools that fail to import (syntax error, missing `app` attribute) are reported via stderr with a clear diagnostic; the runner continues with the remaining tools. The failed tool is NOT registered — its slash command will return exit 64 (unknown tool) until fixed.

## Telemetry hooks (optional)

Tools MAY emit per-step telemetry to `codeconv.discover_runs.warnings` (or a tool-specific equivalent) using DBOS's `get_workflow_id()` for cross-reference. This is documented for parity but not required by spec.

## Anti-patterns (forbidden)

- Tools MUST NOT use `entry_points` in `pyproject.toml` for registration (R10: file-system scan is the contract; entry-points would require pyproject edits per tool).
- Tools MUST NOT modify the runner's `cli.py` or `runner.py`.
- Tools MUST NOT write to schemas other than `codeconv` (FR-015).
- Tools MUST NOT bypass the bridge — direct PGLite WASM access is forbidden; all DB work goes through the unified bridge endpoint.

## Worked example: `discover` tool layout

```
codeconv/src/codeconv/tools/discover/
├── __init__.py            # exports app, register_workflows
├── workflow.py            # @workflow def discover_workflow(run_id, ...)
├── walker.py              # filesystem walk + filter
├── parse.py               # leading-doc-comment + import extraction
└── tombstone.py           # frontmatter read/write
```

`__init__.py`:

```python
import typer
from .workflow import discover_workflow, register as register_dbos

app = typer.Typer(help="Walk glp_runtime_net/ and inventory .dart files into codeconv schema + tombstones.")

@app.command("run")
def run(
    from_tombstones: bool = typer.Option(False, "--from-tombstones", help="Reconstruct inventory from tombstones (no .dart parse)."),
    quiet: bool = False,
) -> None:
    from codeconv.runner import get_dbos
    dbos = get_dbos()
    dbos.start_workflow(discover_workflow, mode="from_tombstones" if from_tombstones else "normal", quiet=quiet)

def register_workflows(dbos_app) -> None:
    register_dbos(dbos_app)
```

This is the template every new tool follows.
