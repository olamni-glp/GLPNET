"""codeconv-convspec tool — feature 018 (US2), deterministic half.

Per-file deep-analysis + research → a structured, machine-consumable
per-file conversion spec WITH embedded rationale/provenance (FR-011),
recording reusable decisions into the persistent conversion-idiom KB
(FR-012) so research is never redundantly repeated (FR-024). **Spec
only — never emits C#** (FR-023).

This Python tool owns ALL deterministic state (readiness, idiom-KB
lookup/record, artifact validation+ingest, escalation aggregation).
The agent/LLM deep-analysis + web research is carried by the
``/codeconv-convspec`` skill (analysis sub-agent + a SEPARATE research
sub-agent) — spawning Claude sub-agents is a harness capability the
Python CLI structurally lacks (justified deviation, plan Complexity
Tracking; mirrors feature-017). No LLM/web here, ever (DBOS
replay-safety, R3).

Auto-discovered by the feature-012 runner (exports ``app`` +
``register_workflows``). Bare ``codeconv convspec`` = ``status``.
"""

from __future__ import annotations

import json as _json
from pathlib import Path
from typing import Optional

import typer

from .workflow import register, run_convspec_ingest

app = typer.Typer(
    add_completion=False,
    help=(
        "Per-file Dart→C#/.NET conversion-spec generation + idiom KB "
        "(deep-analysis + official-docs research; spec-only — no C#)."
    ),
    invoke_without_command=True,
    no_args_is_help=False,
)


def _ctx_repo_root(ctx: typer.Context) -> Path:
    return Path(ctx.obj["repo_root"]) if ctx.obj else Path.cwd()


def _ctx_data_dir(ctx: typer.Context) -> Optional[Path]:
    return ctx.obj.get("data_dir") if ctx.obj else None


def _engine(repo_root: Path, data_dir: Optional[Path]):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(
        acquire_or_discover(repo_root, ready_timeout=60.0, data_dir=data_dir)
    )


@app.callback()
def _default(ctx: typer.Context) -> None:
    if ctx.invoked_subcommand is None:
        ctx.invoke(status)


@app.command("status")
def status(ctx: typer.Context) -> None:
    """convspec-readiness counts over the read-only depgraph +
    ``dart_convspecs`` two-phase state (no agents, no writes)."""
    repo_root = _ctx_repo_root(ctx)
    engine = _engine(repo_root, _ctx_data_dir(ctx))
    from sqlalchemy import text

    with engine.connect() as conn:
        total = conn.execute(
            text("SELECT count(*) FROM codeconv.dart_depgraph")
        ).scalar() or 0
        specced = conn.execute(
            text(
                "SELECT count(*) FROM codeconv.dart_convspecs "
                "WHERE convspec_completed_at IS NOT NULL"
            )
        ).scalar() or 0
        esc = conn.execute(
            text(
                "SELECT count(*) FROM codeconv.dart_convspecs "
                "WHERE open_escalation_count > 0"
            )
        ).scalar() or 0
    typer.echo(
        _json.dumps(
            {"total": total, "specced": specced, "escalated": esc}
        )
    )
    raise typer.Exit(0)


@app.command("next")
def next_(
    ctx: typer.Context,
    limit: int = typer.Option(7, "--limit"),
) -> None:
    """Emit the next convspec-ready batch (the skill loop consumes
    this). Read-only; deterministic (pure readiness over the 015
    depgraph + dart_convspecs)."""
    repo_root = _ctx_repo_root(ctx)
    engine = _engine(repo_root, _ctx_data_dir(ctx))
    from sqlalchemy import text

    from .readiness import DepNode, SpecRow, select_next

    with engine.connect() as conn:
        nodes = {
            r[0]: DepNode(topo_level=r[1], cycle_group_id=r[2])
            for r in conn.execute(
                text(
                    "SELECT path, topo_level, cycle_group_id "
                    "FROM codeconv.dart_depgraph"
                )
            ).all()
        }
        specs = {
            r[0]: SpecRow(completed=r[1] is not None)
            for r in conn.execute(
                text(
                    "SELECT path, convspec_completed_at "
                    "FROM codeconv.dart_convspecs"
                )
            ).all()
        }
    units = select_next(
        nodes=nodes, cross_scc_deps={}, specs=specs, limit=limit
    )
    typer.echo(
        _json.dumps(
            {"units": [{"members": list(u.members)} for u in units]}
        )
    )
    raise typer.Exit(0)


@app.command("ingest")
def ingest(
    ctx: typer.Context,
    path: str = typer.Argument(...),
    respec: bool = typer.Option(False, "--respec"),
) -> None:
    """Deterministically ingest the agent-produced artifact for
    ``path`` (two-phase ``dart_convspecs`` write; ``--respec`` re-opens
    on sha drift, FR-019). Returns the same result the DBOS step would
    (specced / needs_agent_work / escalated)."""
    repo_root = _ctx_repo_root(ctx)
    out = run_convspec_ingest(
        repo_root,
        rel_path=path,
        data_dir=_ctx_data_dir(ctx),
        respec=respec,
    )
    typer.echo(_json.dumps(out))
    raise typer.Exit(0)


@app.command("record-idiom")
def record_idiom_cmd(
    ctx: typer.Context,
    construct: str = typer.Option(..., "--construct"),
    source_form: str = typer.Option(..., "--source-form"),
    target_form: str = typer.Option(..., "--target-form"),
    rationale: str = typer.Option(..., "--rationale"),
    first_seen: str = typer.Option(..., "--first-seen"),
) -> None:
    """Record a resolved conversion idiom (FR-012). Conflicting
    ``target_form`` for an existing active idiom ⇒ flagged
    ``conflicted`` + escalate (FR-014), never silent overwrite."""
    repo_root = _ctx_repo_root(ctx)
    engine = _engine(repo_root, _ctx_data_dir(ctx))
    from . import idioms

    key = idioms.normalise_construct(construct)
    outcome = idioms.record_idiom(
        engine,
        construct_key=key,
        source_form=source_form,
        target_form=target_form,
        rationale=rationale,
        first_seen_path=first_seen,
    )
    typer.echo(_json.dumps({"construct_key": key, "outcome": outcome}))
    raise typer.Exit(0)


@app.command("aggregate-escalations")
def aggregate_escalations(
    ctx: typer.Context,
    report_out: Optional[str] = typer.Option(None, "--report-out"),
) -> None:
    """Walk ``dart_convspecs`` + conflicted/escalated idioms; write the
    single ``.codeconv/conversion-idioms/_escalations-report.md``
    (FR-013/014)."""
    repo_root = _ctx_repo_root(ctx)
    engine = _engine(repo_root, _ctx_data_dir(ctx))
    from sqlalchemy import text

    with engine.connect() as conn:
        blocked = [
            (r[0], r[1])
            for r in conn.execute(
                text(
                    "SELECT path, open_escalation_count "
                    "FROM codeconv.dart_convspecs "
                    "WHERE open_escalation_count > 0 ORDER BY path"
                )
            ).all()
        ]
        idiom_conf = [
            (r[0], r[1])
            for r in conn.execute(
                text(
                    "SELECT construct_key, status "
                    "FROM codeconv.conversion_idioms "
                    "WHERE status IN ('conflicted','escalated') "
                    "ORDER BY construct_key"
                )
            ).all()
        ]
    out_rel = report_out or ".codeconv/conversion-idioms/_escalations-report.md"
    dest = repo_root / out_rel
    dest.parent.mkdir(parents=True, exist_ok=True)
    lines = ["# Conversion Escalations Report", ""]
    lines.append(f"## Files with open escalations ({len(blocked)})")
    lines += [f"- `{p}` — {n} open" for p, n in blocked] or ["- (none)"]
    lines.append("")
    lines.append(f"## Conflicted / escalated idioms ({len(idiom_conf)})")
    lines += [f"- `{k}` — {s}" for k, s in idiom_conf] or ["- (none)"]
    dest.write_text("\n".join(lines) + "\n", encoding="utf-8")
    typer.echo(
        _json.dumps(
            {
                "report": out_rel,
                "files_blocked": len(blocked),
                "idiom_conflicts": len(idiom_conf),
            }
        )
    )
    raise typer.Exit(0)


def register_workflows(dbos_app) -> None:
    """Feature-012 auto-discovery hook → shared durable activation."""
    register(dbos_app)


__all__ = ["app", "register_workflows"]
