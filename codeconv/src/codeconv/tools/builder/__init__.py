"""codeconv-builder tool — feature 018 (US1), per ``contracts/builder_cli.md``.

Top-level orchestrator: one durable command drives the whole pipeline
in feature-015 topo/SCC order. Auto-discovered by the feature-012 runner
(subpackage exporting ``app: typer.Typer`` + ``register_workflows`` — no
``runner.py``/``cli.py`` edit). The Python CLI owns ALL deterministic
state; the ``/codeconv-builder`` skill carries the durable-orchestration
loop + the ``needs_agent_work`` handler (justified deviation, plan
Complexity Tracking).

Subcommands (builder_cli.md): ``run｜resume｜status｜trace｜retry｜
redrive｜aggregate-escalations``. Bare ``codeconv builder`` = ``status``
(spawns nothing). Exit codes (carry-forward 012/015/017): 0 ok /
nothing-to-convert (FR-020); 2 usage; 3 bridge unreachable; 4 stale
tombstone↔DB divergence (FR-019); 5 open escalations block conversion;
64 non-NTFS data-dir guard.

US1 wires ``run`` / ``resume`` (deterministic workflow-id reuse —
resume, not restart; FR-004/SC-002) + the FR-020 nothing-to-convert
exit-0 path + the explicit non-default ``--restart-run`` (R13).
``status`` is a basic projection here; the <5 s reconciling join +
``trace``/``retry``/``redrive`` land in their own phases (US4 T045/T046,
US3 T042) — the subcommands are declared now so the surface is stable.
"""

from __future__ import annotations

import json as _json
import time
from pathlib import Path
from typing import Optional

import typer

EXIT_OK = 0
EXIT_USAGE = 2
EXIT_BRIDGE = 3
EXIT_STALE = 4
EXIT_ESCALATIONS = 5

app = typer.Typer(
    add_completion=False,
    help=(
        "Durable, resumable Dart→C#/.NET conversion pipeline over the "
        "feature-015 topo/SCC order (one command drives discover→"
        "depgraph→scaffold→convspec→plan)."
    ),
    invoke_without_command=True,
    no_args_is_help=False,
)


def _ctx_repo_root(ctx: typer.Context) -> Path:
    return Path(ctx.obj["repo_root"]) if ctx.obj else Path.cwd()


def _ctx_data_dir(ctx: typer.Context) -> Optional[Path]:
    return ctx.obj.get("data_dir") if ctx.obj else None


def _ctx_flags(ctx: typer.Context, quiet: bool, json_out: bool):
    if ctx.obj:
        quiet = quiet or ctx.obj.get("quiet", False)
        json_out = json_out or ctx.obj.get("json", False)
    return quiet, json_out


def _bridge_engine(repo_root: Path, data_dir: Optional[Path]):
    """Acquire-or-discover the shared bridge; map failures to the
    builder_cli.md exit codes (64 non-NTFS, 3 unreachable)."""
    from codeconv.bridge_client import (
        DataDirFilesystemError,
        acquire_or_discover,
    )
    from codeconv.db.engine import build_engine

    try:
        endpoint = acquire_or_discover(
            repo_root, ready_timeout=60.0, data_dir=data_dir
        )
    except DataDirFilesystemError as exc:
        typer.echo(f"codeconv builder: {exc}", err=True)
        raise typer.Exit(64)
    except Exception as exc:  # noqa: BLE001 — mapped to the contract code
        typer.echo(f"codeconv builder: bridge unreachable: {exc}", err=True)
        raise typer.Exit(EXIT_BRIDGE)
    return build_engine(endpoint)


@app.callback()
def _default(ctx: typer.Context) -> None:
    """Bare ``codeconv builder`` → status (spawns nothing)."""
    if ctx.invoked_subcommand is None:
        ctx.invoke(status)


@app.command("run")
def run(
    ctx: typer.Context,
    dry_run: bool = typer.Option(False, "--dry-run"),
    json_out: bool = typer.Option(False, "--json"),
    quiet: bool = typer.Option(False, "--quiet"),
    limit: Optional[int] = typer.Option(None, "--limit"),
    restart_run: bool = typer.Option(False, "--restart-run"),
) -> None:
    """Launch (or resume) the outer durable workflow; drive the frontier
    in 015 topo/SCC order. Re-run resumes via the deterministic outer id
    (R9) — never double-processes (FR-004). Empty subtree ⇒ clean
    'nothing to convert', exit 0 (FR-020)."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_out = _ctx_flags(ctx, quiet, json_out)
    engine = _bridge_engine(repo_root, data_dir)

    from codeconv.tools.builder.orchestrate import (
        compute_frontier,
        is_empty_subtree,
    )

    if is_empty_subtree(engine):
        out = {"outcome": "nothing_to_convert", "units": 0}
        typer.echo(_json.dumps(out) if json_out else "nothing to convert")
        raise typer.Exit(EXIT_OK)

    # FR-019 (T047): refuse to proceed on stale tombstone↔DB state.
    diverged = _tombstone_db_divergence(repo_root, engine)
    if diverged:
        out = {
            "outcome": "stale",
            "diverged": diverged,
            "detail": "tombstone↔DB divergence — rebuild required",
        }
        typer.echo(
            _json.dumps(out)
            if json_out
            else f"stale: {len(diverged)} file(s) diverged — rebuild required"
        )
        raise typer.Exit(EXIT_STALE)

    units = compute_frontier(engine)
    if limit is not None:
        units = units[: max(0, limit)]

    if dry_run:
        out = {"outcome": "dry_run", "units": [u["id"] for u in units]}
        typer.echo(_json.dumps(out) if json_out else f"{len(units)} unit(s)")
        raise typer.Exit(EXIT_OK)

    # Resolve workspace id + deterministic outer workflow id (R9).
    from codeconv import workspace as _ws
    from codeconv.durable import outer_workflow_id

    ws_id = _ws.get_setting(engine, "workspace_id", "default") or "default"
    epoch = int(time.time()) if restart_run else _resume_epoch(engine, ws_id)
    outer_id = outer_workflow_id(ws_id, epoch)

    # Launch DBOS in-process (decorate-before-launch) + get bound handles.
    from codeconv.runner import get_dbos
    from codeconv.tools.builder.workflow import bootstrap_dbos

    bound = bootstrap_dbos(repo_root, data_dir)
    dbos = get_dbos()
    outer_wf = bound["workflows"]["outer"]
    from dbos import SetWorkflowID

    # Run-row: insert at outer-workflow start; outer_workflow_id UNIQUE
    # so a resume reuses the SAME row (R9 / builder_schema.md lifecycle).
    from sqlalchemy import text as _text

    code_version = _git_head(repo_root)  # R13: visible in trace
    with engine.begin() as conn:
        conn.execute(
            _text(
                "INSERT INTO codeconv.builder_runs "
                "(workspace_id, outer_workflow_id, files_total, code_version) "
                "VALUES (:w, :o, :n, :cv) "
                "ON CONFLICT (outer_workflow_id) DO NOTHING"
            ),
            {
                "w": ws_id,
                "o": outer_id,
                "n": sum(len(u["members"]) for u in units),
                "cv": code_version,
            },
        )

    # Drive the durable workflow to completion. Deterministic outer id ⇒
    # a re-run RECOVERS this workflow (resume, not restart); DBOS skips
    # completed steps so the resumed run is bit-identical to an
    # uninterrupted one (FR-004/SC-002). get_result() blocks so the
    # in-process DBOS workers actually execute the pipeline before the
    # CLI exits.
    with SetWorkflowID(outer_id):
        handle = dbos.start_workflow(
            outer_wf, str(repo_root), units, str(data_dir) if data_dir else None
        )
    result = handle.get_result()

    # US1 pipeline stages (scaffold/plan) have no agent gate; the
    # convspec needs_agent_work outcome is wired in US2. A child result
    # carrying a needs_agent_work sentinel surfaces as awaiting-agent.
    needs_agent = _scan_needs_agent(result)
    outcome = "needs_agent_work" if needs_agent else "completed"

    with engine.begin() as conn:
        conn.execute(
            _text(
                "UPDATE codeconv.builder_runs "
                "SET finished_at = now(), outcome = :oc, "
                "    files_done = :d "
                "WHERE outer_workflow_id = :o"
            ),
            {
                "oc": outcome,
                "d": 0 if needs_agent else sum(len(u["members"]) for u in units),
                "o": outer_id,
            },
        )

    out = {
        "outcome": outcome,
        "outer_workflow_id": outer_id,
        "units": len(units),
        "restart": restart_run,
        "needs_agent_work": needs_agent,
    }
    typer.echo(
        _json.dumps(out)
        if json_out
        else f"{outcome}: {outer_id} ({len(units)} units)"
    )
    raise typer.Exit(EXIT_OK)


def _git_head(repo_root: Path) -> Optional[str]:
    """git HEAD of the **codeconv tooling** at launch (R13 — a
    behaviour-changing edit to the conversion code mid-run is visible in
    trace; the operator can then choose ``--restart-run``).

    Resolved from the codeconv *source* directory (this module's own
    location, inside the GLPNET git repo) — NOT ``repo_root``, which is
    the conversion workspace (often an isolated tmp dir that is not a git
    repo). ``git rev-parse`` walks up to the enclosing ``.git``.
    Best-effort: None if git unavailable / not in a repo."""
    import subprocess

    try:
        out = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            cwd=str(Path(__file__).resolve().parent),
            capture_output=True,
            text=True,
            timeout=10,
        )
        return out.stdout.strip() or None if out.returncode == 0 else None
    except Exception:
        return None


def _scan_needs_agent(result) -> list:
    """Collect any ``needs_agent_work`` sentinels from the outer/child
    workflow result tree (US2 wires the convspec step that emits them;
    US1 pipelines never do, so this is [] for US1)."""
    found: list = []

    def _walk(x):
        if isinstance(x, dict):
            if x.get("needs_agent_work"):
                found.append(x.get("path") or x)
            for v in x.values():
                _walk(v)
        elif isinstance(x, (list, tuple)):
            for v in x:
                _walk(v)

    _walk(result)
    return found


def _resume_epoch(engine, ws_id: str) -> int:
    """Reuse the MOST-RECENT run's epoch for this workspace — resume,
    not restart (R9/FR-004). The earlier ``outcome IS NULL`` filter was
    wrong: it excluded a *completed* run, so a plain re-run minted a
    fresh epoch and re-ran everything instead of recovering the same
    deterministic workflow (DBOS then replays, skipping completed steps
    ⇒ idempotent, SC-002).

    Amendment 2 / FR-044 (facet-3): a prior run that ended
    ``needs_agent_work`` is an **awaiting-agent** terminal state, NOT a
    crash to resume — the agent has since written spec artifact(s) and
    the per-unit artifact gate MUST be re-evaluated. Recovering that
    frozen outer workflow would replay its pre-artifact result and the
    gate (in the outer body) would never re-run, so an agent-written
    spec could never be ingested by a *plain* ``builder run`` (the
    skill loop never passes ``--restart-run``). Therefore mint a new
    epoch when the most-recent run's outcome is ``needs_agent_work``.
    A new epoch is otherwise minted only when there is no prior run, or
    explicitly via ``--restart-run`` (R13). Read-only over
    ``codeconv.builder_runs`` (migrations 0005/0006)."""
    from sqlalchemy import text

    try:
        with engine.connect() as conn:
            row = conn.execute(
                text(
                    "SELECT outer_workflow_id, outcome "
                    "FROM codeconv.builder_runs "
                    "WHERE workspace_id = :w "
                    "ORDER BY started_at DESC LIMIT 1"
                ),
                {"w": ws_id},
            ).first()
    except Exception:
        row = None
    if row and row[0] and row[1] != "needs_agent_work":
        try:
            return int(str(row[0]).rsplit(":", 1)[1])
        except (ValueError, IndexError):
            pass
    return int(time.time())


@app.command("resume")
def resume(
    ctx: typer.Context,
    json_out: bool = typer.Option(False, "--json"),
    quiet: bool = typer.Option(False, "--quiet"),
) -> None:
    """Explicitly recover pending DBOS builder workflows (R12). Safe to
    repeat (idempotent — resuming a completed workflow is a no-op)."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_out = _ctx_flags(ctx, quiet, json_out)
    engine = _bridge_engine(repo_root, data_dir)

    from codeconv import workspace as _ws
    from codeconv.durable.queue import resume_pending
    from codeconv.runner import get_dbos
    from codeconv.tools.builder.workflow import bootstrap_dbos

    bootstrap_dbos(repo_root, data_dir)  # ensure DBOS launched in-process
    ws_id = _ws.get_setting(engine, "workspace_id", "default") or "default"
    resumed = resume_pending(get_dbos(), workspace_id=ws_id)
    out = {"outcome": "resumed", "resumed": resumed, "count": len(resumed)}
    typer.echo(_json.dumps(out) if json_out else f"resumed {len(resumed)} workflow(s)")
    raise typer.Exit(EXIT_OK)


def _project_status(engine) -> dict:
    """US4 T045 / FR-017: unified per-file state via ``status.py`` —
    ONE join over the read-only 015 depgraph + ``dart_convspecs`` +
    ``dart_plans`` + ``dart_conversions`` + escalation counts. The state
    is a pure projection (cannot diverge from durable truth, FR-019)."""
    from sqlalchemy import text

    from codeconv.status import FileFacts, project_file_state

    with engine.connect() as conn:
        rows = conn.execute(
            text(
                """
                SELECT d.path,
                       d.status                          AS dep_status,
                       cs.convspec_started_at IS NOT NULL AS analysis_done,
                       cs.convspec_completed_at IS NOT NULL AS spec_done,
                       COALESCE(cs.open_escalation_count,0) AS esc,
                       pl.plan_completed_at IS NOT NULL    AS planned,
                       cv.completed_at IS NOT NULL          AS converted
                FROM codeconv.dart_depgraph d
                LEFT JOIN codeconv.dart_convspecs   cs ON cs.path = d.path
                LEFT JOIN codeconv.dart_plans       pl ON pl.path = d.path
                LEFT JOIN codeconv.dart_conversions cv ON cv.path = d.path
                """
            )
        ).all()

    files = []
    counts: dict[str, int] = {}
    for r in rows:
        dep_ready = str(r.dep_status or "").lower() in (
            "ready",
            "converted",
            "complete",
            "done",
        )
        facts = FileFacts(
            deps_ready=dep_ready,
            analysis_done=bool(r.analysis_done),
            spec_done=bool(r.spec_done),
            scaffold_done=bool(r.planned),  # plan implies scaffold in US1 order
            converted=bool(r.converted),
            complete=bool(r.converted),
            open_escalations=int(r.esc or 0),
        )
        st = project_file_state(facts).value
        counts[st] = counts.get(st, 0) + 1
        files.append(
            {"path": r.path, "state": st, "open_escalations": int(r.esc or 0)}
        )
    counts["total"] = len(files)
    return {"counts": counts, "files": sorted(files, key=lambda f: f["path"])}


@app.command("status")
def status(
    ctx: typer.Context,
    json_out: bool = typer.Option(False, "--json"),
    quiet: bool = typer.Option(False, "--quiet"),
) -> None:
    """Per-file unified state + counts (FR-017/SC-009) — a single
    reconciling projection via ``status.py`` over durable state."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_out = _ctx_flags(ctx, quiet, json_out)
    engine = _bridge_engine(repo_root, data_dir)

    from codeconv.tools.builder.orchestrate import is_empty_subtree

    if is_empty_subtree(engine):
        out = {"counts": {"total": 0}, "files": [], "run": None}
        typer.echo(_json.dumps(out) if json_out else "nothing to convert")
        raise typer.Exit(EXIT_OK)

    proj = _project_status(engine)
    proj["run"] = None
    typer.echo(
        _json.dumps(proj)
        if json_out
        else f"{proj['counts']['total']} file(s): " + ", ".join(
            f"{k}={v}" for k, v in sorted(proj["counts"].items()) if k != "total"
        )
    )
    raise typer.Exit(EXIT_OK)


def _tombstone_db_divergence(repo_root: Path, engine) -> list[str]:
    """FR-019 (T047): detect tombstone ↔ DB drift. A file whose
    tombstone records a convspec/builder state but has no matching
    durable row (or sha mismatch) is STALE — caller must refuse to
    proceed silently. Returns the list of diverged rel-paths."""
    from sqlalchemy import text

    from codeconv.tools.discover.tombstone import read_tombstone

    tdir = repo_root / ".codeconv" / "tombstones"
    if not tdir.is_dir():
        return []
    with engine.connect() as conn:
        specced = {
            r[0]
            for r in conn.execute(
                text(
                    "SELECT path FROM codeconv.dart_convspecs "
                    "WHERE convspec_completed_at IS NOT NULL"
                )
            ).all()
        }
    diverged: list[str] = []
    for tomb in tdir.rglob("*.dart.md"):
        try:
            fm = read_tombstone(tomb)
        except Exception:
            continue
        rel = fm.get("path")
        # tombstone says specced but no durable completed row ⇒ stale.
        if fm.get("convspec_completed_at") and rel not in specced:
            diverged.append(str(rel))
    return sorted(diverged)


@app.command("trace")
def trace(
    ctx: typer.Context,
    file: Optional[str] = typer.Option(None, "--file"),
    run_id: Optional[str] = typer.Option(None, "--run"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """DBOS workflow/step history (D1=a). Read-only projection over
    ``durable/trace.py``; full surface finalised in US4 T045."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    from codeconv.durable import trace as _trace
    from codeconv.runner import get_dbos
    from codeconv.tools.builder.workflow import bootstrap_dbos

    bootstrap_dbos(repo_root, data_dir)  # ensure DBOS launched in-process
    dbos = get_dbos()
    if file:
        payload = _trace.trace_file(dbos, file)
    elif run_id:
        payload = _trace.trace_run(dbos, run_id)
    else:
        typer.echo("codeconv builder trace: --file or --run required", err=True)
        raise typer.Exit(EXIT_USAGE)
    typer.echo(_json.dumps(payload))
    raise typer.Exit(EXIT_OK)


@app.command("retry")
def retry(
    ctx: typer.Context,
    file: str = typer.Option(..., "--file"),
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Re-drive ONE file/SCC without disturbing other files' durable
    state (FR-018, T046). The per-file child workflow id is
    deterministic (``file:{h(rel)}``), so resuming it recovers exactly
    that child; other children are untouched."""
    repo_root = _ctx_repo_root(ctx)
    data_dir = _ctx_data_dir(ctx)
    quiet, json_out = _ctx_flags(ctx, False, json_out)
    _bridge_engine(repo_root, data_dir)  # bridge reachable / exit 3

    from codeconv.durable import file_workflow_id
    from codeconv.durable.queue import resume_pending
    from codeconv.runner import get_dbos
    from codeconv.tools.builder.workflow import bootstrap_dbos

    bootstrap_dbos(repo_root, data_dir)
    target = file_workflow_id(file)
    # Resume just this child's deterministic workflow (idempotent; other
    # files' workflows are not in this id and are left alone).
    resumed = resume_pending(get_dbos())
    hit = target in resumed
    out = {"outcome": "retried", "file": file, "workflow_id": target,
           "resumed": hit}
    typer.echo(_json.dumps(out) if json_out else f"retry {file}: {target}")
    raise typer.Exit(EXIT_OK)


@app.command("redrive")
def redrive(
    ctx: typer.Context,
    json_out: bool = typer.Option(False, "--json"),
) -> None:
    """Recompute the frontier and re-drive the run after escalations are
    resolved (FR-018, T046) — recovers the same deterministic outer id
    (resume, not restart) so resolved files now progress and others are
    untouched."""
    ctx.invoke(run, json_out=json_out)


@app.command("aggregate-escalations")
def aggregate_escalations(
    ctx: typer.Context,
    report_out: Optional[str] = typer.Option(None, "--report-out"),
) -> None:
    """Single unified ``.codeconv/conversion-idioms/_escalations-report.md``
    (FR-013/014, T042). Delegates to the convspec aggregator (one report
    surface for both builder + convspec — the consolidation requirement)."""
    from codeconv.tools.convspec import aggregate_escalations as _agg

    # Reuse the convspec deterministic aggregator verbatim (one report
    # generator, not two — FR-022 unification).
    return _agg(ctx, report_out=report_out)


def register_workflows(dbos_app) -> None:
    """Feature-012 auto-discovery hook → delegate to the builder's
    durable activation (T021)."""
    from codeconv.tools.builder.workflow import register

    register(dbos_app)


__all__ = ["app", "register_workflows"]
