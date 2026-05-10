"""codeconv top-level Typer CLI.

Per ``specs/012-codeconv-runner/contracts/codeconv_runner_cli.md``:
- Global flags: --repo-root, --bridge-port, --quiet, --json, --help, --version
- Built-in commands: list, doctor, migrate
- Tool subcommands: discovered via :func:`codeconv.runner.tool_registry`
- Exit codes: 0/1/2/64/65/70 (see contract)

The DBOS instance is built lazily — ``codeconv list`` does NOT need
DBOS. ``codeconv migrate``, ``codeconv doctor``, and tool subcommands
do trigger bridge acquisition + engine + DBOS bootstrap.
"""

from __future__ import annotations

import json as _json
import os
import sys
import warnings
from contextlib import contextmanager
from pathlib import Path
from typing import Any, Iterator, Optional

import typer

from codeconv import runner as _runner


_EXIT_OK = 0
_EXIT_GENERIC = 1
_EXIT_BRIDGE_UNREACHABLE = 2
_EXIT_USAGE = 64
_EXIT_DATA = 65
_EXIT_INTERNAL = 70


app = typer.Typer(
    add_completion=False,
    help="codeconv — DBOS-backed code-conversion runner over PGLite at .pgdb/.",
    no_args_is_help=True,
)


@app.callback(invoke_without_command=False)
def _global(
    ctx: typer.Context,
    repo_root: Optional[Path] = typer.Option(
        None,
        "--repo-root",
        help="Locate .pgdb/, tools, and .codeconv/. Defaults to cwd.",
    ),
    bridge_port: Optional[int] = typer.Option(
        None,
        "--bridge-port",
        help="Override sidecar discovery (debugging only).",
    ),
    quiet: bool = typer.Option(False, "--quiet", help="Suppress non-error output."),
    json_out: bool = typer.Option(
        False,
        "--json",
        help="Emit machine-readable summaries on subcommands that support it.",
    ),
    version: bool = typer.Option(
        False,
        "--version",
        is_eager=True,
        help="Print package version and exit.",
        callback=lambda v: _print_version(v),
    ),
) -> None:
    """Stash global state on the Click ``ctx.obj`` so subcommands can read it."""
    ctx.ensure_object(dict)
    ctx.obj["repo_root"] = (repo_root or Path.cwd()).resolve()
    ctx.obj["bridge_port"] = bridge_port
    ctx.obj["quiet"] = quiet
    ctx.obj["json"] = json_out


def _print_version(value: bool) -> None:
    if not value:
        return
    try:
        from importlib.metadata import version

        print(version("codeconv"))
    except Exception:
        print("0.0.0+unknown")
    raise typer.Exit(_EXIT_OK)


# ---------------------------------------------------------------------------
# Built-in commands
# ---------------------------------------------------------------------------


@app.command("list")
def cmd_list(ctx: typer.Context) -> None:
    """Print registered tools and their summaries."""
    tools = _runner.tool_registry()
    if ctx.obj.get("json"):
        payload = [
            {
                "name": t.name,
                "description": t.description,
                "slash_command": f"/codeconv-{t.name}",
            }
            for t in tools
        ]
        typer.echo(_json.dumps(payload, indent=2))
        return
    if not tools:
        if not ctx.obj.get("quiet"):
            typer.echo("(no tools registered)")
        return
    width = max(len(t.name) for t in tools)
    for t in tools:
        line = f"{t.name.ljust(width)}  {t.description}".rstrip()
        typer.echo(line)


@app.command("doctor")
def cmd_doctor(ctx: typer.Context) -> None:
    """Diagnose bridge, sidecar, schemas, and DBOS state."""
    repo_root: Path = ctx.obj["repo_root"]
    quiet: bool = ctx.obj.get("quiet", False)
    json_mode: bool = ctx.obj.get("json", False)

    report: dict[str, Any] = {
        "repo_root": str(repo_root),
        "checks": [],
        "ok": True,
    }

    def add(name: str, ok: bool, detail: str = "") -> None:
        report["checks"].append({"name": name, "ok": ok, "detail": detail})
        if not ok:
            report["ok"] = False

    # 1. .pgdb/ present.
    data_dir = repo_root / ".pgdb"
    add("data_dir_exists", data_dir.is_dir(), str(data_dir))

    # 2. Bridge reachable + sidecar shape valid.
    try:
        from codeconv.bridge_client import acquire_or_discover

        endpoint = acquire_or_discover(repo_root)
        add("bridge_reachable", True, f"{endpoint.host}:{endpoint.port}")
    except Exception as exc:
        add("bridge_reachable", False, repr(exc))
        if json_mode:
            typer.echo(_json.dumps(report, indent=2))
        else:
            _print_doctor_text(report, quiet)
        raise typer.Exit(_EXIT_BRIDGE_UNREACHABLE)

    # 3. Schemas present (codeconv, dbos).
    try:
        from codeconv.db.engine import build_engine
        from sqlalchemy import text

        engine = build_engine(endpoint)
        with engine.connect() as conn:
            rows = conn.execute(
                text(
                    "SELECT schema_name FROM information_schema.schemata "
                    "WHERE schema_name IN ('codeconv', 'dbos', 'public')"
                )
            ).all()
            schemas = {r[0] for r in rows}
        add("schema_codeconv", "codeconv" in schemas, "")
        add("schema_dbos", "dbos" in schemas, "")
    except Exception as exc:
        add("schemas_query", False, repr(exc))

    # 4. psycopg loaders patched (apply_to_engine is idempotent — verify by
    #    checking the SQLAlchemy event registry).
    try:
        from sqlalchemy import event

        from codeconv._vendor.pglite_compat_loaders import _on_psycopg_connect

        listener_present = event.contains(engine, "connect", _on_psycopg_connect)
        add("psycopg_loaders_patched", listener_present, "")
    except Exception as exc:
        add("psycopg_loaders_patched", False, repr(exc))

    if json_mode:
        typer.echo(_json.dumps(report, indent=2))
    else:
        _print_doctor_text(report, quiet)
    raise typer.Exit(_EXIT_OK if report["ok"] else _EXIT_GENERIC)


def _print_doctor_text(report: dict[str, Any], quiet: bool) -> None:
    if quiet and report["ok"]:
        return
    for check in report["checks"]:
        status = "OK  " if check["ok"] else "FAIL"
        suffix = f" — {check['detail']}" if check["detail"] else ""
        typer.echo(f"  [{status}] {check['name']}{suffix}")
    typer.echo("OVERALL: " + ("OK" if report["ok"] else "FAIL"))


@app.command("migrate")
def cmd_migrate(ctx: typer.Context) -> None:
    """Run Alembic + DBOS migrations against the unified bridge.

    Order is fixed: Alembic upgrade head FIRST (creates the ``codeconv``
    schema + tables), THEN DBOS launch (which runs ``dbos`` schema
    migrations). Re-running is a no-op (Alembic head-already-current,
    DBOS migrations idempotent).
    """
    repo_root: Path = ctx.obj["repo_root"]
    quiet: bool = ctx.obj.get("quiet", False)

    try:
        from codeconv.bridge_client import acquire_or_discover
        from codeconv.db.engine import build_url, setup_dbos
    except Exception as exc:
        typer.echo(f"codeconv migrate: import failed: {exc}", err=True)
        raise typer.Exit(_EXIT_INTERNAL)

    try:
        endpoint = acquire_or_discover(repo_root)
    except Exception as exc:
        typer.echo(f"codeconv migrate: bridge unreachable: {exc}", err=True)
        raise typer.Exit(_EXIT_BRIDGE_UNREACHABLE)

    # 1. Alembic upgrade head.
    if not quiet:
        typer.echo("[migrate] alembic upgrade head ...")
    try:
        _run_alembic_upgrade(endpoint=endpoint)
    except Exception as exc:
        typer.echo(f"codeconv migrate: alembic failed: {exc}", err=True)
        raise typer.Exit(_EXIT_GENERIC)

    # 2. DBOS launch (which runs DBOS migrations).
    if not quiet:
        typer.echo("[migrate] dbos launch ...")
    try:
        dbos = setup_dbos(endpoint)
        _runner.set_dbos(dbos)
    except Exception as exc:
        typer.echo(f"codeconv migrate: dbos launch failed: {exc}", err=True)
        raise typer.Exit(_EXIT_GENERIC)

    if not quiet:
        typer.echo("[migrate] OK")


def _run_alembic_upgrade(endpoint: Any) -> None:
    """Invoke Alembic programmatically against the unified bridge.

    Loads the package-internal ``alembic.ini`` so the CLI works regardless
    of the user's cwd.
    """
    from alembic import command
    from alembic.config import Config

    here = Path(__file__).parent
    ini_path = here / "db" / "alembic.ini"
    cfg = Config(str(ini_path))
    cfg.set_main_option("script_location", str((here / "db" / "migrations").resolve()))
    cfg.set_main_option("sqlalchemy.url", _build_url_from_endpoint(endpoint))
    command.upgrade(cfg, "head")


def _build_url_from_endpoint(endpoint: Any) -> str:
    return (
        f"postgresql+psycopg://postgres:postgres@"
        f"{endpoint.host}:{endpoint.port}/postgres"
    )


# ---------------------------------------------------------------------------
# Dynamic tool registration
# ---------------------------------------------------------------------------


def _register_tools() -> None:
    """Add a Typer sub-app for each registered tool.

    Called at module import time so ``codeconv --help`` already lists
    every discovered tool. Tool ``register_workflows(dbos_app)`` is
    deferred to first use of a tool subcommand (it requires DBOS).
    """
    for tool in _runner.tool_registry():
        try:
            app.add_typer(tool.app, name=tool.name, help=tool.description or None)
        except Exception as exc:
            warnings.warn(f"failed to add tool '{tool.name}' to CLI: {exc!r}", stacklevel=2)


_register_tools()


def main() -> None:
    """Entry point for ``[project.scripts]`` ``codeconv = codeconv.cli:app``.

    Typer's ``app`` is itself callable, but exposing ``main`` keeps the
    setuptools ``console_script`` shape standard.
    """
    app()


if __name__ == "__main__":  # pragma: no cover
    main()
