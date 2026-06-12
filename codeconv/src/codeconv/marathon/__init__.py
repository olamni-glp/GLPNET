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


# --- guarded execution (exit-code mapping per contracts/cli.md) --------------
# Library imports stay INSIDE the command bodies so `codeconv marathon --help`
# never pays for sqlalchemy/bridge imports (bridge acquisition itself is always
# deferred to the library call).


def _execute(ctx: typer.Context, run_id: str, op, *, json_out: bool) -> None:
    """Resolve the per-run env, run ``op(env)``, emit its payload dict.

    Exit-code mapping: usage/filesystem guard → 64; escalation (blocked, not
    an error) → 2 with the payload still emitted; unexpected → 70.
    """
    from codeconv.bridge_client import DataDirFilesystemError
    from codeconv.marathon.env import StoreRootInsideRepoError, resolve_env
    from codeconv.marathon.intake import PrereqAgainstCompletedStage
    from codeconv.marathon.store import ConcurrentWriter

    try:
        env = resolve_env(
            run_id, data_dir=_data_dir(ctx), repo_dir=_repo_root(ctx)
        )
        payload = op(env)
    except (DataDirFilesystemError, StoreRootInsideRepoError, ValueError) as exc:
        typer.echo(f"error: {exc}", err=True)
        raise typer.Exit(EXIT_USAGE)
    except PrereqAgainstCompletedStage as exc:
        _emit(
            ctx,
            {
                "escalation": "prereq_against_completed_stage",
                "escalation_id": exc.escalation_id,
                "detail": str(exc),
            },
            json_out=json_out,
        )
        raise typer.Exit(EXIT_ESCALATION)
    except ConcurrentWriter as exc:
        # Blocked-not-broken (FR-015): refusal of a second live writer.
        _emit(
            ctx,
            {"escalation": "concurrent_writer", "detail": str(exc)},
            json_out=json_out,
        )
        raise typer.Exit(EXIT_ESCALATION)
    except typer.Exit:
        raise
    except Exception as exc:  # surfaced, never swallowed (II)
        typer.echo(f"internal error: {exc}", err=True)
        raise typer.Exit(EXIT_INTERNAL)
    _emit(ctx, payload, json_out=json_out)


def _split_csv(raw: Optional[str]) -> Optional[list[str]]:
    if raw is None:
        return None
    return [part.strip() for part in raw.split(",") if part.strip()]


def _position_payload(pos) -> dict:
    from dataclasses import asdict

    return asdict(pos)


# --- US1 subcommands (T018): register / append-stage / stage-start /
# --- checkpoint / resume / position / finalize --------------------------------


@marathon_app.command("register")
def cmd_register(
    ctx: typer.Context,
    run: str = typer.Option(..., "--run", help="Run id (canonical)."),
    stages: Optional[str] = typer.Option(
        None, "--stages", help="Comma-separated ordered stage names."
    ),
    title: Optional[str] = typer.Option(None, "--title"),
    budget: Optional[int] = typer.Option(None, "--budget", help="Budget ceiling."),
    budget_unit: Optional[str] = typer.Option(None, "--budget-unit"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Register (or re-attach) a run with its own ordered stage list (FR-001)."""

    def op(env):
        from codeconv.marathon.stages import register_run

        result = register_run(
            run,
            stages=_split_csv(stages),
            title=title,
            budget_ceiling=budget,
            budget_unit=budget_unit,
            env=env,
        )
        return {
            "run": result.id,
            "title": result.title,
            "status": result.status,
            "stages": _split_csv(stages) or [],
        }

    _execute(ctx, run, op, json_out=json_out)


@marathon_app.command("append-stage")
def cmd_append_stage(
    ctx: typer.Context,
    name: str = typer.Argument(..., help="New stage name."),
    run: str = typer.Option(..., "--run"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Append a dynamically-discovered stage; the total grows (FR-002)."""

    def op(env):
        from codeconv.marathon.stages import append_stage

        stage = append_stage(run, name, env=env)
        return {
            "run": run,
            "stage": stage.name,
            "stage_index": stage.stage_index,
            "order_key": stage.order_key,
            "origin": stage.origin,
        }

    _execute(ctx, run, op, json_out=json_out)


@marathon_app.command("stage-start")
def cmd_stage_start(
    ctx: typer.Context,
    name: str = typer.Argument(..., help="Stage name."),
    run: str = typer.Option(..., "--run"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Flip a stage pending→running (started ≠ complete, FR-004)."""

    def op(env):
        from codeconv.marathon.checkpoint import start_stage

        stage = start_stage(run, name, env=env)
        return {
            "run": run,
            "stage": stage.name,
            "status": stage.status,
            "started_at": str(stage.started_at),
        }

    _execute(ctx, run, op, json_out=json_out)


@marathon_app.command("checkpoint")
def cmd_checkpoint(
    ctx: typer.Context,
    name: str = typer.Argument(..., help="Stage name."),
    run: str = typer.Option(..., "--run"),
    completed: Optional[str] = typer.Option(
        None, "--completed", help="JSON array of completed units."
    ),
    remaining: Optional[str] = typer.Option(
        None, "--remaining", help="JSON array of remaining units ([] = complete)."
    ),
    wip: Optional[str] = typer.Option(None, "--wip", help="Unit in flight."),
    budget: int = typer.Option(0, "--budget", help="Budget delta for this block."),
    paths: Optional[str] = typer.Option(
        None, "--paths", help="Comma-separated paths this block touched."
    ),
    issues: Optional[str] = typer.Option(
        None, "--issues", help="Comma-separated issue summaries to open."
    ),
    message: Optional[str] = typer.Option(None, "-m", "--message"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Append a durable checkpoint; empty remaining completes the stage."""

    def op(env):
        import json as _json

        from codeconv.marathon.checkpoint import checkpoint as _checkpoint

        cp = _checkpoint(
            run,
            name,
            completed_units=_json.loads(completed) if completed else None,
            remaining_units=_json.loads(remaining) if remaining else None,
            wip_unit=wip,
            budget_delta=budget,
            committed_paths=_split_csv(paths),
            issues=_split_csv(issues),
            message=message,
            env=env,
        )
        return {
            "run": run,
            "stage": name,
            "sequence_no": cp.sequence_no,
            "store_origin": cp.store_origin,
            "remaining_units": cp.remaining_units,
        }

    _execute(ctx, run, op, json_out=json_out)


def _cmd_resume_impl(ctx: typer.Context, run: str, json_out: bool) -> None:
    def op(env):
        from codeconv.marathon.position import resume_position

        pos = resume_position(run, env=env)
        if pos.diverged:
            _emit(ctx, _position_payload(pos), json_out=json_out)
            raise typer.Exit(EXIT_ESCALATION)  # store fork — never pick (rule 7)
        return _position_payload(pos)

    _execute(ctx, run, op, json_out=json_out)


@marathon_app.command("resume")
def cmd_resume(
    ctx: typer.Context,
    run: str = typer.Option(..., "--run"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """The objective resume position, from durable rows alone (SC-008)."""
    _cmd_resume_impl(ctx, run, json_out)


@marathon_app.command("position")
def cmd_position(
    ctx: typer.Context,
    run: str = typer.Option(..., "--run"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Alias of `resume` surfacing the four-field position (contracts/cli.md)."""
    _cmd_resume_impl(ctx, run, json_out)


@marathon_app.command("capture")
def cmd_capture(
    ctx: typer.Context,
    run: str = typer.Option(..., "--run"),
    kind: str = typer.Option(
        ...,
        "--kind",
        help="latent-requirement | issue | bug | missing-prerequisite",
    ),
    title: str = typer.Option(..., "--title"),
    description: Optional[str] = typer.Option(None, "--description"),
    blocks: Optional[str] = typer.Option(
        None, "--blocks", help="Stage this missing-prerequisite blocks."
    ),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Capture emergent work; expands a 5-stage mini-pipeline (FR-005/006)."""

    def op(env):
        from codeconv.marathon.intake import capture_item

        item = capture_item(
            run,
            kind=kind,
            title=title,
            description=description,
            blocks_stage=blocks,
            env=env,
        )
        return {
            "run": run,
            "item": item.id,
            "kind": item.kind,
            "title": item.title,
            "blocks_stage_id": item.blocks_stage_id,
            "artifacts_dir": item.artifacts_dir,
        }

    _execute(ctx, run, op, json_out=json_out)


@marathon_app.command("finalize")
def cmd_finalize(
    ctx: typer.Context,
    run: str = typer.Option(..., "--run"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Finalize the run — only when every current stage is complete."""

    def op(env):
        from codeconv.marathon.stages import finalize

        result = finalize(run, env=env)
        return {"run": result.id, "status": result.status}

    _execute(ctx, run, op, json_out=json_out)


# --- US3 subcommands (T032): keeper start|stop|recover + doctor ---------------

keeper_app = typer.Typer(
    add_completion=False,
    no_args_is_help=True,
    help="Per-run store keeper lifecycle (start | stop | recover).",
)
marathon_app.add_typer(keeper_app, name="keeper")


def _endpoint_payload(ep) -> dict:
    return {
        "host": ep.host,
        "port": ep.port,
        "pid": ep.pid,
        "data_dir": ep.data_dir,
    }


@keeper_app.command("start")
def cmd_keeper_start(
    ctx: typer.Context,
    run: str = typer.Option(..., "--run"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Spawn/discover the per-run bridge; publish its endpoint (FR-012)."""

    def op(env):
        from codeconv.marathon.keeper import start_keeper

        return _endpoint_payload(start_keeper(run, env=env))

    _execute(ctx, run, op, json_out=json_out)


@keeper_app.command("stop")
def cmd_keeper_stop(
    ctx: typer.Context,
    run: str = typer.Option(..., "--run"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Graceful flush-and-exit; the next start needs no recovery (FR-013)."""

    def op(env):
        from codeconv.marathon.keeper import stop_keeper

        stop_keeper(run, env=env)
        return {"run": run, "keeper": "stopped"}

    _execute(ctx, run, op, json_out=json_out)


@keeper_app.command("recover")
def cmd_keeper_recover(
    ctx: typer.Context,
    run: str = typer.Option(..., "--run"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Auto-clear stale residue; a live bridge is consumed, never killed."""

    def op(env):
        from codeconv.marathon.keeper import recover_keeper

        return _endpoint_payload(recover_keeper(run, env=env))

    _execute(ctx, run, op, json_out=json_out)


@marathon_app.command("doctor")
def cmd_doctor(
    ctx: typer.Context,
    run: str = typer.Option(..., "--run"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Read-only health: endpoint, active store, last-seq, escalations, budget."""

    def op(env):
        from codeconv.marathon.keeper import doctor

        return doctor(run, env=env)

    _execute(ctx, run, op, json_out=json_out)


__all__ = [
    "EXIT_ESCALATION",
    "EXIT_INTERNAL",
    "EXIT_OK",
    "EXIT_USAGE",
    "_data_dir",
    "_emit",
    "_repo_root",
    "keeper_app",
    "marathon_app",
]
