"""codeconv-planagents workflow orchestrator (deterministic state engine).

Implements ``specs/017-conversion-plan-agents/contracts/
planagents_cli.md`` + ``planagents_schema.md``. Each subcommand handler
(``run_status``, ``run_next``, ``run_plan_started``,
``run_plan_completed``, ``run_aggregate_escalations``,
``run_stamp_tombstones``, ``run_rebuild_plans_from_tombstones``)
acquires-or-discovers the unified PGLite bridge, runs the relevant
transaction(s), and returns a summary dict.

This module owns ALL deterministic state: plan-readiness (via the pure
``readiness`` module), batch selection, ``dart_plans`` lifecycle writes,
tombstone stamping, escalation aggregation. It spawns NO LLM agents
(R1 — the skill does, via the Claude Code Agent tool).

Reads (read-only, FR-003/FR-020):
- ``codeconv.dart_depgraph`` (canonical topo/SCC — feature 015; MUST
  NOT recompute);
- ``codeconv.dart_files`` (node set + ``sha256`` for drift);
- ``codeconv.dart_files_orphaned`` (exclusion — FR-020);
- ``codeconv.dart_imports`` (edges, to derive SCC-external deps —
  consuming feature-015's edge data, NOT recomputing the graph).

Writes (FR-020 — confined to):
- ``codeconv.dart_plans`` (new — INSERT ON CONFLICT / UPDATE);
- ``codeconv.planagents_runs`` (new, optional — provenance);
- the four plan-state tombstone YAML keys (via ``tombstone_writer``);
- the aggregated escalations report.
NEVER writes ``dart_files`` / ``dart_imports`` / ``dart_callers`` /
``dart_files_orphaned`` / ``discover_runs`` / ``dart_depgraph`` /
``dart_conversions`` (FR-020).
"""

from __future__ import annotations

import os
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Optional

from sqlalchemy import text

from codeconv.bridge_client import acquire_or_discover
from codeconv.db.engine import build_engine

from . import artefact as _artefact
from .readiness import (
    DepNode,
    PlanRow,
    SelectedUnit,
    classify_all,
    remaining_ready_count,
    select_next,
)
from .tombstone_writer import (
    plan_completed_keys,
    plan_started_keys,
    stamp_keys,
    write_tombstone_with_plan_keys,
)


_DEPGRAPH_EMPTY_ERROR = "No depgraph. Run /codeconv-depgraph first."


def register(dbos_app: Any) -> None:
    """No-op stub for the feature-012 tool contract.

    planagents' writes are short-lived single transactions; nothing
    needs DBOS-managed durability today (mirrors feature-015's stub).
    """
    return None


# ---------------------------------------------------------------------------
# Shared bridge / read helpers
# ---------------------------------------------------------------------------


def _engine(repo_root: Path, data_dir: Optional[Path]):
    endpoint = acquire_or_discover(
        Path(repo_root).resolve(), ready_timeout=60.0, data_dir=data_dir
    )
    return build_engine(endpoint)


def _read_snapshot(
    engine,
) -> Optional[
    tuple[
        dict[str, DepNode],
        dict[str, frozenset[str]],
        dict[str, PlanRow],
    ]
]:
    """Read the depgraph + cross-SCC deps + plans snapshot.

    Returns ``None`` iff ``codeconv.dart_depgraph`` is empty/absent
    (the caller maps this to exit 2 — FR-018). Orphaned files are
    excluded from the node set (FR-020).
    """
    with engine.connect() as conn:
        depg_rows = conn.execute(
            text(
                "SELECT path, topo_level, cycle_group_id "
                "FROM codeconv.dart_depgraph"
            )
        ).all()
        if not depg_rows:
            return None
        orphaned = {
            r[0]
            for r in conn.execute(
                text("SELECT path FROM codeconv.dart_files_orphaned")
            ).all()
        }
        edges = conn.execute(
            text("SELECT from_path, to_path FROM codeconv.dart_imports")
        ).all()
        plan_rows = conn.execute(
            text(
                "SELECT path, plan_completed_at "
                "FROM codeconv.dart_plans"
            )
        ).all()

    nodes: dict[str, DepNode] = {}
    cgid: dict[str, int] = {}
    for path, topo_level, cycle_group_id in depg_rows:
        if path in orphaned:
            continue
        nodes[path] = DepNode(
            topo_level=int(topo_level), cycle_group_id=int(cycle_group_id)
        )
        cgid[path] = int(cycle_group_id)

    # SCC-external in-subtree dependency set per node. Intra-SCC edges
    # excluded (mirrors feature-015 FR-006). Only edges whose BOTH
    # endpoints are non-orphaned depgraph nodes count.
    cross: dict[str, set[str]] = {p: set() for p in nodes}
    for u, v in edges:
        if u not in nodes or v not in nodes:
            continue
        if cgid[u] != cgid[v]:
            cross[u].add(v)
    cross_frozen = {p: frozenset(s) for p, s in cross.items()}

    plans: dict[str, PlanRow] = {}
    for path, completed_at in plan_rows:
        plans[path] = PlanRow(completed=completed_at is not None)

    return nodes, cross_frozen, plans


def _counts(
    nodes: dict[str, DepNode],
    cross: dict[str, frozenset[str]],
    plans: dict[str, PlanRow],
) -> dict[str, int]:
    states = classify_all(
        nodes=nodes, cross_scc_deps=cross, plans=plans
    )
    out = {
        "plan_pending": 0,
        "plan_ready": 0,
        "plan_in_progress": 0,
        "planned": 0,
    }
    for s in states.values():
        out[s] += 1
    return out


# ---------------------------------------------------------------------------
# status (default)
# ---------------------------------------------------------------------------


def run_status(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    quiet: bool = False,
) -> dict:
    """Readiness view; spawns nothing; writes nothing (FR-018)."""
    engine = _engine(repo_root, data_dir)
    snap = _read_snapshot(engine)
    if snap is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": _DEPGRAPH_EMPTY_ERROR,
            "files_total": 0,
        }
    nodes, cross, plans = snap
    counts = _counts(nodes, cross, plans)

    with engine.connect() as conn:
        open_total = conn.execute(
            text(
                "SELECT COALESCE(SUM(open_escalation_count),0) "
                "FROM codeconv.dart_plans"
            )
        ).scalar()
        stale = _stale_paths(conn, plans)

    return {
        "ok": True,
        "exit_code": 0,
        "files_total": len(nodes),
        "plan_pending": counts["plan_pending"],
        "plan_ready": counts["plan_ready"],
        "plan_in_progress": counts["plan_in_progress"],
        "planned": counts["planned"],
        "open_escalations_total": int(open_total or 0),
        "stale": sorted(stale),
        "stale_count": len(stale),
    }


def _stale_paths(conn, plans: dict[str, PlanRow]) -> set[str]:
    """Paths whose current dart_files.sha256 != sha256_of_dart_at_plan_start.

    Source-drift detection (FR-015): a plan is *stale* when the file was
    edited since ``plan-started``. Reported, never silently treated as
    current.
    """
    if not plans:
        return set()
    rows = conn.execute(
        text(
            "SELECT p.path "
            "FROM codeconv.dart_plans p "
            "JOIN codeconv.dart_files f ON f.path = p.path "
            "WHERE f.sha256 <> p.sha256_of_dart_at_plan_start"
        )
    ).all()
    return {r[0] for r in rows}


# ---------------------------------------------------------------------------
# next
# ---------------------------------------------------------------------------


def run_next(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    limit: int = 7,
    replan: Optional[str] = None,
    json_out_path: Optional[Path] = None,
    dry_run: bool = False,
    quiet: bool = False,
) -> dict:
    """Emit the next plan-ready batch (read-only — writes nothing).

    ``--replan <selection>`` forces re-selection of the named/stale
    files even if ``planned`` (FR-015): such paths are treated as
    plan-pending for THIS selection (the actual ``dart_plans`` UPDATE
    happens on the subsequent ``plan-started``).
    """
    repo_root = Path(repo_root).resolve()
    engine = _engine(repo_root, data_dir)
    snap = _read_snapshot(engine)
    if snap is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": _DEPGRAPH_EMPTY_ERROR,
            "schema_version": 1,
            "batch": [],
        }
    nodes, cross, plans = snap

    replan_set = _resolve_replan_selection(engine, replan, plans) if replan else set()
    if replan_set:
        # Drop the rows for replan-selected files from the in-memory
        # snapshot so readiness re-treats them as plan-pending ⇒
        # plan-ready (their external deps are already planned since they
        # were previously planned). FR-015: never silently skip a stale
        # plan; --replan opt-in re-selects it.
        plans = {p: r for p, r in plans.items() if p not in replan_set}

    units = select_next(
        nodes=nodes, cross_scc_deps=cross, plans=plans, limit=limit
    )
    remaining = remaining_ready_count(
        nodes=nodes,
        cross_scc_deps=cross,
        plans=plans,
        selected=units,
    )

    batch = _batch_json(units, nodes)
    payload = {
        "schema_version": 1,
        "ok": True,
        "exit_code": 0,
        "limit": limit,
        "batch": batch,
        "remaining_ready": remaining,
    }
    if not batch:
        payload["message"] = "nothing to plan"

    if json_out_path is not None and not dry_run:
        import json as _json

        op = Path(json_out_path)
        op.parent.mkdir(parents=True, exist_ok=True)
        tmp = op.with_suffix(op.suffix + ".tmp")
        tmp.write_text(
            _json.dumps(payload, indent=2, sort_keys=False) + "\n",
            encoding="utf-8",
        )
        os.replace(tmp, op)

    return payload


def _resolve_replan_selection(
    engine, replan: str, plans: dict[str, PlanRow]
) -> set[str]:
    """Resolve a ``--replan`` selection token to a set of paths.

    Supported (deterministic, no guessing — FR-015):
    - ``stale`` : every planned file whose source SHA drifted.
    - a comma-separated explicit path list.
    """
    token = replan.strip()
    if token == "stale":
        with engine.connect() as conn:
            return _stale_paths(conn, plans)
    return {p.strip() for p in token.split(",") if p.strip()}


def _batch_json(
    units: list[SelectedUnit], nodes: dict[str, DepNode]
) -> list[dict[str, Any]]:
    """Flatten units to the ``next`` JSON ``batch`` (FR-021 ordering).

    Ordered by ``(topo_level ASC, path ASC)`` with SCC members
    contiguous + lexicographic; each row's keys alphabetical.
    """
    rows: list[dict[str, Any]] = []
    for u in units:
        siblings_all = sorted(u.members)
        for m in sorted(u.members):
            scc_siblings = (
                [s for s in siblings_all if s != m]
                if u.is_scc_batch
                else []
            )
            rows.append(
                {
                    "artefact": _artefact.artefact_rel_path(m),
                    "cycle_group_id": u.cycle_group_id,
                    "path": m,
                    "scc_siblings": scc_siblings,
                    "tombstone": f".codeconv/tombstones/{m}.md",
                    "topo_level": nodes[m].topo_level,
                }
            )
    rows.sort(key=lambda r: (r["topo_level"], r["path"]))
    return rows


# ---------------------------------------------------------------------------
# plan-started
# ---------------------------------------------------------------------------


def run_plan_started(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    path: str,
    sha256: Optional[str] = None,
    replan: bool = False,
    no_tombstone_update: bool = False,
) -> dict:
    """Record that a planning sub-agent was dispatched for ``path``.

    ``INSERT … ON CONFLICT (path) DO NOTHING`` (idempotent — FR-014);
    a pre-existing not-completed row warns "already started", a
    completed row warns "already completed" (no-op exit 0) UNLESS
    ``replan`` is set, in which case the row is UPDATEd in place (new
    ``plan_started_at`` + SHA, ``plan_completed_at`` reset NULL) — the
    row is never deleted (data-model §1.1 / R9).
    """
    repo_root = Path(repo_root).resolve()
    tombstones_root = repo_root / ".codeconv" / "tombstones"
    engine = _engine(repo_root, data_dir)
    run_id = str(uuid.uuid4())

    with engine.connect() as conn:
        f = conn.execute(
            text("SELECT sha256 FROM codeconv.dart_files WHERE path = :p"),
            {"p": path},
        ).first()
        orphan = conn.execute(
            text(
                "SELECT 1 FROM codeconv.dart_files_orphaned WHERE path = :p"
            ),
            {"p": path},
        ).first()
    if f is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": f"path {path!r} not in codeconv.dart_files",
            "path": path,
        }
    if orphan is not None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": f"path {path!r} is orphaned; not a conversion target",
            "path": path,
        }
    sha_to_record = sha256 if sha256 is not None else f[0]

    with engine.connect() as conn:
        existing = conn.execute(
            text(
                "SELECT plan_started_at, plan_completed_at "
                "FROM codeconv.dart_plans WHERE path = :p"
            ),
            {"p": path},
        ).first()

    started_at = _utc_now()
    started_iso = _iso_or_none(started_at)

    if existing is not None and not replan:
        _, completed = existing
        return {
            "ok": True,
            "exit_code": 0,
            "warning": (
                f"already completed: {path}"
                if completed is not None
                else f"already started: {path}"
            ),
            "path": path,
            "action": "noop",
        }

    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.planagents_runs "
                "  (id, started_at, mode) "
                "VALUES (:id, :sa, 'plan-started')"
            ),
            {"id": run_id, "sa": started_at},
        )
        if replan and existing is not None:
            conn.execute(
                text(
                    "UPDATE codeconv.dart_plans SET "
                    "  plan_started_at = :sa, "
                    "  sha256_of_dart_at_plan_start = :sha, "
                    "  plan_completed_at = NULL, "
                    "  plan_path = NULL, "
                    "  plan_run_id = :rid "
                    "WHERE path = :p"
                ),
                {"sa": started_at, "sha": sha_to_record,
                 "rid": run_id, "p": path},
            )
            action = "replanned"
        else:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_plans "
                    "  (path, plan_started_at, plan_completed_at, "
                    "   sha256_of_dart_at_plan_start, plan_path, "
                    "   open_escalation_count, plan_run_id) "
                    "VALUES (:p, :sa, NULL, :sha, NULL, 0, :rid) "
                    "ON CONFLICT (path) DO NOTHING"
                ),
                {"p": path, "sa": started_at,
                 "sha": sha_to_record, "rid": run_id},
            )
            action = "started"
        conn.execute(
            text(
                "UPDATE codeconv.planagents_runs SET completed_at = NOW() "
                "WHERE id = :id"
            ),
            {"id": run_id},
        )

    if not no_tombstone_update:
        write_tombstone_with_plan_keys(
            tombstones_root,
            path,
            plan_started_keys(plan_started_at=started_iso),
        )

    return {
        "ok": True,
        "exit_code": 0,
        "path": path,
        "action": action,
        "plan_started_at": started_iso,
        "sha256_of_dart_at_plan_start": sha_to_record,
        "run_id": run_id,
    }


# ---------------------------------------------------------------------------
# plan-completed
# ---------------------------------------------------------------------------


def run_plan_completed(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    path: str,
    plan_path: Optional[str] = None,
    escalations: int = 0,
    no_tombstone_update: bool = False,
) -> dict:
    """Record that the conversion plan for ``path`` is complete.

    Row absent ⇒ error exit 2 ("must call plan-started first" — no
    auto-create). Already completed ⇒ warn exit 0 (idempotent).
    ``escalations`` (>= 0) is written into
    ``dart_plans.open_escalation_count``; ``> 0`` ⇒ conversion-blocked
    per FR-017 (the plan still counts as ``planned`` for the planning
    frontier).
    """
    repo_root = Path(repo_root).resolve()
    tombstones_root = repo_root / ".codeconv" / "tombstones"
    engine = _engine(repo_root, data_dir)
    run_id = str(uuid.uuid4())

    if escalations < 0:
        return {
            "ok": False,
            "exit_code": 2,
            "error": "--escalations must be >= 0",
            "path": path,
        }

    with engine.connect() as conn:
        f = conn.execute(
            text("SELECT 1 FROM codeconv.dart_files WHERE path = :p"),
            {"p": path},
        ).first()
    if f is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": f"path {path!r} not in codeconv.dart_files",
            "path": path,
        }

    with engine.connect() as conn:
        row = conn.execute(
            text(
                "SELECT plan_started_at, plan_completed_at, plan_path "
                "FROM codeconv.dart_plans WHERE path = :p"
            ),
            {"p": path},
        ).first()
    if row is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": f"must call plan-started first: {path}",
            "path": path,
        }
    _, completed_at, existing_plan_path = row
    if completed_at is not None:
        return {
            "ok": True,
            "exit_code": 0,
            "warning": f"already completed: {path}",
            "path": path,
            "action": "noop",
            "plan_path": existing_plan_path,
        }

    completed_value = _utc_now()
    completed_iso = _iso_or_none(completed_value)
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.planagents_runs "
                "  (id, started_at, mode) "
                "VALUES (:id, :sa, 'plan-completed')"
            ),
            {"id": run_id, "sa": completed_value},
        )
        conn.execute(
            text(
                "UPDATE codeconv.dart_plans SET "
                "  plan_completed_at = :ca, plan_path = :pp, "
                "  open_escalation_count = :n, plan_run_id = :rid "
                "WHERE path = :p AND plan_completed_at IS NULL"
            ),
            {
                "ca": completed_value,
                "pp": plan_path,
                "n": int(escalations),
                "rid": run_id,
                "p": path,
            },
        )
        conn.execute(
            text(
                "UPDATE codeconv.planagents_runs SET completed_at = NOW() "
                "WHERE id = :id"
            ),
            {"id": run_id},
        )

    if not no_tombstone_update:
        write_tombstone_with_plan_keys(
            tombstones_root,
            path,
            plan_completed_keys(
                completed_at=completed_iso,
                plan_path=plan_path,
                open_escalation_count=int(escalations),
            ),
        )

    return {
        "ok": True,
        "exit_code": 0,
        "path": path,
        "action": "completed",
        "plan_completed_at": completed_iso,
        "plan_path": plan_path,
        "open_escalation_count": int(escalations),
        "conversion_blocked": int(escalations) > 0,
        "run_id": run_id,
    }


# ---------------------------------------------------------------------------
# aggregate-escalations
# ---------------------------------------------------------------------------


def run_aggregate_escalations(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    report_out: Optional[Path] = None,
    dry_run: bool = False,
    quiet: bool = False,
) -> dict:
    """Walk all artefacts; write one engineer-facing escalations report.

    FR-016: each entry states file(s), observed situation, why it is not
    pre-specified/incremental, and the decision required, ordered by
    ``(path ASC, E-number ASC)`` with a back-link to the source
    artefact. ``--dry-run`` computes but writes nothing (SC-008).
    """
    repo_root = Path(repo_root).resolve()
    root = _artefact.artefact_root(repo_root)
    report_path = _artefact.escalations_report_path(
        repo_root, override=report_out
    )

    collected: list[tuple[str, dict[str, str]]] = []
    if root.is_dir():
        for art in sorted(root.rglob("*.dart.md")):
            if art.name == _artefact.ESCALATIONS_REPORT_NAME:
                continue
            rel = art.relative_to(root).as_posix()
            for e in _artefact.iter_open_escalations(art):
                collected.append((rel, e))

    collected.sort(key=lambda t: (t[0], t[1]["e_number"]))

    lines: list[str] = [
        "# Aggregated Conversion-Plan Escalations",
        "",
        "Open escalations across all conversion-plan artefacts. Each",
        "MUST be resolved by the engineer before the corresponding file",
        "is converted (FR-016/FR-017). Planning may proceed; conversion",
        "is blocked until these are resolved.",
        "",
        f"Total open escalations: {len(collected)}",
        "",
    ]
    for rel, e in collected:
        anchor = f"e{e['e_number']}"
        lines += [
            f"## {rel} — E{e['e_number']}: {e['title']}",
            "",
            f"- **File(s)**: {e['files']}",
            f"- **Observed**: {e['observed']}",
            f"- **Why not pre-specified+incremental**: {e['why']}",
            f"- **Decision required**: {e['decision']}",
            f"- **Source**: [{rel}#{anchor}]({rel}#{anchor})",
            "",
        ]

    if not dry_run:
        report_path.parent.mkdir(parents=True, exist_ok=True)
        tmp = report_path.with_suffix(report_path.suffix + ".tmp")
        tmp.write_text("\n".join(lines) + "\n", encoding="utf-8")
        os.replace(tmp, report_path)

    return {
        "ok": True,
        "exit_code": 0,
        "open_escalations_total": len(collected),
        "report_path": str(report_path),
        "dry_run": dry_run,
    }


# ---------------------------------------------------------------------------
# stamp-tombstones
# ---------------------------------------------------------------------------


def run_stamp_tombstones(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    dry_run: bool = False,
    quiet: bool = False,
) -> dict:
    """Embed the four plan-state keys into every tombstone (FR-013).

    Idempotent + byte-identical re-stamp (data-model §2). Null-vs-
    missing convention applied per ``stamp_keys``. ``--dry-run`` writes
    nothing.
    """
    repo_root = Path(repo_root).resolve()
    tombstones_root = repo_root / ".codeconv" / "tombstones"
    engine = _engine(repo_root, data_dir)
    run_id = str(uuid.uuid4())
    started_at = _utc_now()

    snap = _read_snapshot(engine)
    if snap is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": _DEPGRAPH_EMPTY_ERROR,
            "files_stamped": 0,
        }
    nodes, _cross, _plans = snap

    with engine.connect() as conn:
        plan_rows = conn.execute(
            text(
                "SELECT path, plan_started_at, plan_completed_at, "
                "plan_path, open_escalation_count "
                "FROM codeconv.dart_plans"
            )
        ).all()
    plan_by_path = {
        r[0]: (r[1], r[2], r[3], r[4]) for r in plan_rows
    }

    files_stamped = 0
    for path in sorted(nodes):
        if path in plan_by_path:
            ps, pc, pp, oec = plan_by_path[path]
            extras = stamp_keys(
                plan_started_at=_iso_or_none(ps),
                plan_completed_at=_iso_or_none(pc),
                plan_path=pp,
                open_escalation_count=oec,
                row_exists=True,
            )
        else:
            # No dart_plans row ⇒ the four keys are OMITTED so the
            # tombstone is byte-identical to a never-planned one.
            extras = stamp_keys(
                plan_started_at=None,
                plan_completed_at=None,
                plan_path=None,
                open_escalation_count=None,
                row_exists=False,
            )
        if not dry_run:
            write_tombstone_with_plan_keys(tombstones_root, path, extras)
        files_stamped += 1

    if not dry_run:
        with engine.begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.planagents_runs "
                    "  (id, started_at, completed_at, mode, files_total) "
                    "VALUES (:id, :sa, NOW(), 'stamp-tombstones', :ft)"
                ),
                {"id": run_id, "sa": started_at, "ft": files_stamped},
            )

    return {
        "ok": True,
        "exit_code": 0,
        "files_stamped": files_stamped,
        "dry_run": dry_run,
        "run_id": run_id,
    }


# ---------------------------------------------------------------------------
# rebuild-plans-from-tombstones
# ---------------------------------------------------------------------------


def run_rebuild_plans_from_tombstones(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    dry_run: bool = False,
    quiet: bool = False,
) -> dict:
    """Rebuild ``codeconv.dart_plans`` from tombstone YAML (DB-wipe).

    For every tombstone with ``plan_started_at`` present + non-null,
    ``INSERT … ON CONFLICT (path) DO UPDATE``.
    ``sha256_of_dart_at_plan_start`` is re-snapshotted from the CURRENT
    ``dart_files.sha256`` (same caveat as feature-015
    ``rebuild-conversions-from-tombstones``). ``--dry-run`` writes
    nothing.
    """
    repo_root = Path(repo_root).resolve()
    tombstones_root = repo_root / ".codeconv" / "tombstones"
    engine = _engine(repo_root, data_dir)
    run_id = str(uuid.uuid4())
    started_at = _utc_now()

    from codeconv.tools.discover.tombstone import read_tombstone

    rows_upserted = 0
    rows_skipped = 0
    if tombstones_root.is_dir():
        for tomb in sorted(tombstones_root.rglob("*.dart.md")):
            rel = tomb.relative_to(tombstones_root)
            if ".orphaned" in rel.parts:
                continue
            try:
                fm = read_tombstone(tomb)
            except Exception:
                rows_skipped += 1
                continue
            ps_yaml = fm.get("plan_started_at")
            if ps_yaml is None or ps_yaml in ("", "null"):
                rows_skipped += 1
                continue
            file_rel = fm.get("path")
            if not file_rel:
                rows_skipped += 1
                continue
            pc_yaml = fm.get("plan_completed_at")
            if pc_yaml in ("", "null"):
                pc_yaml = None
            pp_yaml = fm.get("plan_path")
            if pp_yaml in ("", "null"):
                pp_yaml = None
            oec_yaml = fm.get("open_escalation_count")
            try:
                oec = int(oec_yaml) if oec_yaml is not None else 0
            except (TypeError, ValueError):
                oec = 0

            with engine.connect() as conn:
                f = conn.execute(
                    text(
                        "SELECT sha256 FROM codeconv.dart_files "
                        "WHERE path = :p"
                    ),
                    {"p": file_rel},
                ).first()
            if f is None:
                rows_skipped += 1
                continue
            sha = f[0]

            if not dry_run:
                with engine.begin() as conn:
                    conn.execute(
                        text(
                            "INSERT INTO codeconv.dart_plans "
                            "  (path, plan_started_at, plan_completed_at, "
                            "   sha256_of_dart_at_plan_start, plan_path, "
                            "   open_escalation_count) "
                            "VALUES (:p, :sa, :ca, :sha, :pp, :n) "
                            "ON CONFLICT (path) DO UPDATE SET "
                            "  plan_started_at = EXCLUDED.plan_started_at, "
                            "  plan_completed_at = EXCLUDED.plan_completed_at, "
                            "  sha256_of_dart_at_plan_start = "
                            "      EXCLUDED.sha256_of_dart_at_plan_start, "
                            "  plan_path = EXCLUDED.plan_path, "
                            "  open_escalation_count = "
                            "      EXCLUDED.open_escalation_count"
                        ),
                        {
                            "p": file_rel,
                            "sa": _parse_iso(ps_yaml),
                            "ca": _parse_iso(pc_yaml) if pc_yaml else None,
                            "sha": sha,
                            "pp": pp_yaml,
                            "n": oec,
                        },
                    )
            rows_upserted += 1

    if not dry_run:
        with engine.begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.planagents_runs "
                    "  (id, started_at, completed_at, mode, files_total) "
                    "VALUES (:id, :sa, NOW(), "
                    "        'rebuild-plans-from-tombstones', :ft)"
                ),
                {"id": run_id, "sa": started_at, "ft": rows_upserted},
            )

    return {
        "ok": True,
        "exit_code": 0,
        "rows_upserted": rows_upserted,
        "rows_skipped": rows_skipped,
        "dry_run": dry_run,
        "run_id": run_id,
    }


# ---------------------------------------------------------------------------
# Helpers (mirror feature-015 workflow helpers exactly)
# ---------------------------------------------------------------------------


def _utc_now() -> datetime:
    return datetime.now(tz=timezone.utc)


def _iso_or_none(dt: Any) -> Optional[str]:
    if dt is None:
        return None
    if isinstance(dt, str):
        return dt
    if isinstance(dt, datetime):
        if dt.tzinfo is None:
            dt = dt.replace(tzinfo=timezone.utc)
        return dt.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    return str(dt)


def _parse_iso(s: Any) -> Optional[datetime]:
    if s is None or s == "":
        return None
    if isinstance(s, datetime):
        return s
    txt = str(s)
    if txt.endswith("Z"):
        txt = txt[:-1] + "+00:00"
    return datetime.fromisoformat(txt)


__all__ = [
    "register",
    "run_aggregate_escalations",
    "run_next",
    "run_plan_completed",
    "run_plan_started",
    "run_rebuild_plans_from_tombstones",
    "run_stamp_tombstones",
    "run_status",
]
