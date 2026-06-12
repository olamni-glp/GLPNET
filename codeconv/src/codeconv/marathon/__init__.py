"""``codeconv marathon`` — durable, restart-safe, **workload-agnostic** stage
harness (feature 030, refining feature 024).

A statically-registered Typer sub-app (mirroring the bridge-free ``tutorials``
command in ``codeconv/cli.py``) — deliberately **NOT** wired through
``runner.tool_registry()`` so the harness never appears as a Dart→C# conversion
tool in ``codeconv list``.

The refined harness composes the existing ``codeconv`` substrate as libraries —
``bridge_client`` (PGLite bridge), ``db.engine`` (SQLAlchemy) — pointed at a
**per-run isolated store outside the repo** owned by a background keeper, with a
data-driven (registrable + growable) stage list, emergent-work intake + mini-
pipeline, and 024's ported strengths (approval gate, per-block/subagent re-run,
budget ceiling, verification-trace substrate, dual-store reconciliation).

Subcommands (``contracts/cli.md``): ``register``, ``append-stage``,
``stage-start``, ``checkpoint``, ``capture``, ``resume``, ``position``,
``status``, ``gate``, ``rerun``, ``trace``, ``reconcile``, ``finalize``,
``keeper start|stop|recover``, ``doctor``. They are wired incrementally as the
pipeline lands — US1 (T018), US2 (T025), US3 (T032), US4 (T039), US5 (T050) —
each a thin 1:1 wrapper over exactly one public library function (FR-025 parity).
``--run <id>`` is canonical; ``--feature <slug>`` is a deprecated 024 alias.
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional

import typer

# Exit codes (contracts/cli.md): 0 success · 2 escalation/awaiting-Gabi (blocked,
# not an error: fork divergence, budget halt, push_blocked, prereq-against-done) ·
# 64 usage/filesystem guard · 70 internal failure.
EXIT_OK = 0
EXIT_ESCALATION = 2
EXIT_USAGE = 64
EXIT_INTERNAL = 70


marathon_app = typer.Typer(
    add_completion=False,
    no_args_is_help=True,
    help=(
        "Durable, restart-safe, workload-agnostic stage harness driving one "
        "long buildkit feature across many sessions (feature 030). See "
        "/marathon-stage-harness."
    ),
)


# --- context accessors (the parent callback stashes these on ctx.obj) -------
# Reused by the per-phase subcommand wiring; kept here so the glue is defined
# once for every command.


def _repo_root(ctx: typer.Context) -> Path:
    obj = getattr(ctx, "obj", None)
    if isinstance(obj, dict) and obj.get("repo_root"):
        return Path(obj["repo_root"])
    return Path.cwd()


def _data_dir(ctx: typer.Context) -> Optional[Path]:
    obj = getattr(ctx, "obj", None)
    if isinstance(obj, dict) and obj.get("data_dir"):
        return Path(obj["data_dir"])
    return None


def _ctx_flag(ctx: typer.Context, key: str) -> bool:
    obj = getattr(ctx, "obj", None)
    return bool(obj.get(key)) if isinstance(obj, dict) else False


def _emit(ctx: typer.Context, payload: dict, *, json_out: bool) -> None:
    """Print a result either as JSON (``--json``, global or local) or as a
    compact ``key: value`` human line."""
    import json as _json

    if json_out or _ctx_flag(ctx, "json"):
        typer.echo(_json.dumps(payload, indent=2, default=str))
    else:
        for k, v in payload.items():
            typer.echo(f"{k}: {v}")


# Subcommands are registered by the per-user-story phases (see module docstring).
# Until then the app loads with no commands so ``codeconv marathon --help`` and
# the static registration in ``codeconv/cli.py`` keep working through the rewrite.


__all__ = [
    "EXIT_ESCALATION",
    "EXIT_INTERNAL",
    "EXIT_OK",
    "EXIT_USAGE",
    "_data_dir",
    "_emit",
    "_repo_root",
    "marathon_app",
]
