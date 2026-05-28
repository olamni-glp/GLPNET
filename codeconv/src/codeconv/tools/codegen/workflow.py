"""codeconv-codegen workflow orchestrator (deterministic state engine).

Implements ``specs/019-codeconv-codegen/contracts/codegen_cli.md`` +
``dbos_codegen_stage.md`` + ``codegen_schema.md``. Each subcommand
handler acquires-or-discovers the unified PGLite bridge, runs the
relevant transaction(s), and returns a summary dict.

This module owns ALL deterministic codegen state: codegen-readiness (via
the pure ``readiness`` module), batch selection, the two-phase
``dart_codegen`` lifecycle, build-gating (via ``buildgate``), escalation
counting (via ``escalations``), tombstone stamping. It spawns NO LLM
agents and makes NO network/LM call (R3 replay-safety) — the
``/codeconv-codegen`` skill carries the codegen sub-agent loop and
re-drives ``ingest`` once the agent has written the ``.cs``.

Reads (read-only): ``codeconv.dart_depgraph`` (canonical topo/SCC —
feature 015; MUST NOT recompute), ``codeconv.dart_files`` (sha for
drift), ``codeconv.dart_files_orphaned`` (exclusion), and
``codeconv.dart_imports`` (edges → SCC-external deps). Writes ONLY
``codeconv.dart_codegen`` + the five codegen tombstone keys + the
aggregated escalations report.
"""

from __future__ import annotations

import os
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable, Optional

from sqlalchemy import text

from codeconv.bridge_client import acquire_or_discover
from codeconv.db.engine import build_engine

from . import artefact as _artefact
from . import buildprops as _buildprops
from . import escalations as _esc
from .buildgate import BUILD_FAIL, BUILD_PASS, NOT_BUILT, BuildResult, run_build, run_test
from .readiness import (
    CodegenRow,
    DepNode,
    SelectedUnit,
    classify_all,
    remaining_ready_count,
    select_next,
)
from .tombstone_writer import (
    codegen_completed_keys,
    codegen_started_keys,
    write_tombstone_with_codegen_keys,
)


_DEPGRAPH_EMPTY_ERROR = "No depgraph. Run /codeconv-depgraph first."


def is_test_path(rel: str) -> bool:
    """True iff ``rel`` is a test-tree file (excluded from the codegen
    frontier in Increment 1; included in Increment 2 — T037/FR-012).

    A path is test-tree if its first segment is ``test`` or it has a
    ``/test/`` segment (the Dart convention).
    """
    r = rel.replace("\\", "/").strip("/")
    return r == "test" or r.startswith("test/") or "/test/" in r


def register(dbos_app: Any) -> None:
    """Delegate to the centralised durable activation (idempotent).

    Mirrors planagents.workflow.register — wires the builder-side durable
    layer; the codegen ``run_*`` entrypoints are unchanged.
    """
    from codeconv.durable import activate

    activate(dbos_app)


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
    *,
    include_tests: bool = False,
) -> Optional[
    tuple[dict[str, DepNode], dict[str, frozenset[str]], dict[str, CodegenRow]]
]:
    """Read the depgraph + cross-SCC deps + codegen-row snapshot.

    Returns ``None`` iff ``codeconv.dart_depgraph`` is empty/absent (exit
    2). Orphaned files are excluded from the node set. A codegen row is
    ``completed`` iff ``codegen_completed_at IS NOT NULL`` (build-passing
    accept — the only thing that unblocks downstream). ``include_tests``
    False (Increment 1) drops test-tree files from the frontier (T037);
    nothing depends on a test file, so dropping them is dependency-safe.
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
        cg_rows = conn.execute(
            text("SELECT path, codegen_completed_at FROM codeconv.dart_codegen")
        ).all()

    nodes: dict[str, DepNode] = {}
    cgid: dict[str, int] = {}
    for path, topo_level, cycle_group_id in depg_rows:
        if path in orphaned:
            continue
        if not include_tests and is_test_path(path):
            continue
        nodes[path] = DepNode(
            topo_level=int(topo_level), cycle_group_id=int(cycle_group_id)
        )
        cgid[path] = int(cycle_group_id)

    cross: dict[str, set[str]] = {p: set() for p in nodes}
    for u, v in edges:
        if u not in nodes or v not in nodes:
            continue
        if cgid[u] != cgid[v]:
            cross[u].add(v)
    cross_frozen = {p: frozenset(s) for p, s in cross.items()}

    rows: dict[str, CodegenRow] = {}
    for path, completed_at in cg_rows:
        rows[path] = CodegenRow(completed=completed_at is not None)

    return nodes, cross_frozen, rows


def _stale_paths(conn) -> set[str]:
    """Paths whose current dart_files.sha256 != sha256_of_dart_at_codegen_start.

    Source-drift detection (FR-008): a codegen is *stale* when the file
    was edited since ``codegen_started_at``. Reported, never silently
    treated as current.
    """
    rows = conn.execute(
        text(
            "SELECT c.path "
            "FROM codeconv.dart_codegen c "
            "JOIN codeconv.dart_files f ON f.path = c.path "
            "WHERE c.sha256_of_dart_at_codegen_start IS NOT NULL "
            "AND f.sha256 <> c.sha256_of_dart_at_codegen_start"
        )
    ).all()
    return {r[0] for r in rows}


# ---------------------------------------------------------------------------
# status (default)
# ---------------------------------------------------------------------------


def run_status(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    include_tests: bool = False,
    quiet: bool = False,
) -> dict:
    """Readiness + lifecycle counts; spawns nothing; writes nothing.

    States (data-model § State transitions): ``not_started``,
    ``in_progress`` (row, not built, no escalation), ``built`` (built,
    not promoted), ``converted`` (built + promoted), ``escalated``
    (open_escalation_count > 0), ``stale`` (sha drift). ``stale`` and
    ``escalated`` are reported as overlays on the lifecycle counts.
    """
    repo_root = Path(repo_root).resolve()
    engine = _engine(repo_root, data_dir)
    snap = _read_snapshot(engine, include_tests=include_tests)
    if snap is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": _DEPGRAPH_EMPTY_ERROR,
            "files_total": 0,
        }
    nodes, cross, rows = snap

    # Frontier readiness (pending/ready/in_progress/done).
    states = classify_all(nodes=nodes, cross_scc_deps=cross, rows=rows)
    ready = sum(1 for s in states.values() if s == "codegen_ready")

    with engine.connect() as conn:
        detail = conn.execute(
            text(
                "SELECT path, codegen_completed_at, promoted, "
                "open_escalation_count, build_status "
                "FROM codeconv.dart_codegen"
            )
        ).all()
        open_total = conn.execute(
            text(
                "SELECT COALESCE(SUM(open_escalation_count),0) "
                "FROM codeconv.dart_codegen"
            )
        ).scalar()
        promoted_total = conn.execute(
            text(
                "SELECT COUNT(*) FROM codeconv.dart_codegen WHERE promoted"
            )
        ).scalar()
        stale = _stale_paths(conn)

    by_path = {r[0]: r for r in detail}
    not_started = built = converted = escalated = in_progress = 0
    for p in nodes:
        row = by_path.get(p)
        if row is None:
            not_started += 1
            continue
        _p, completed_at, promoted, oec, _bs = row
        if (oec or 0) > 0:
            escalated += 1
        elif completed_at is not None:
            converted += 1 if promoted else 0
            built += 0 if promoted else 1
        else:
            in_progress += 1

    # Optimized-prompt presence (status warns when baseline is in use).
    from .prompt import optimized_prompt_path

    has_optimized = optimized_prompt_path(repo_root).is_file()

    return {
        "ok": True,
        "exit_code": 0,
        "files_total": len(nodes),
        "not_started": not_started,
        "codegen_ready": ready,
        "in_progress": in_progress,
        "built": built,
        "converted": converted,
        "escalated": escalated,
        "promoted_total": int(promoted_total or 0),
        "open_escalations_total": int(open_total or 0),
        "stale": sorted(stale),
        "stale_count": len(stale),
        "optimized_prompt": has_optimized,
        "prompt_warning": (
            None if has_optimized else "no optimized prompt; using baseline"
        ),
    }


# ---------------------------------------------------------------------------
# next
# ---------------------------------------------------------------------------


def run_next(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    limit: int = 7,
    include_tests: bool = False,
    json_out_path: Optional[Path] = None,
    dry_run: bool = False,
    quiet: bool = False,
) -> dict:
    """Emit the next codegen-ready batch (read-only — writes nothing)."""
    repo_root = Path(repo_root).resolve()
    engine = _engine(repo_root, data_dir)
    snap = _read_snapshot(engine, include_tests=include_tests)
    if snap is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": _DEPGRAPH_EMPTY_ERROR,
            "schema_version": 1,
            "batch": [],
        }
    nodes, cross, rows = snap

    units = select_next(
        nodes=nodes, cross_scc_deps=cross, rows=rows, limit=limit
    )
    remaining = remaining_ready_count(
        nodes=nodes, cross_scc_deps=cross, rows=rows, selected=units
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
        payload["message"] = "nothing to generate"

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


def _batch_json(
    units: list[SelectedUnit], nodes: dict[str, DepNode]
) -> list[dict[str, Any]]:
    """Flatten units to the ``next`` JSON ``batch`` (deterministic order).

    Each row carries the artefact paths the skill assembles for the
    sub-agent: the produced ``.cs`` path, the spec/plan artefacts, the
    tombstone, and the true SCC siblings (every other file sharing the
    cycle group — SCC = one coordinated batch, FR-002).
    """
    members_by_cg: dict[int, list[str]] = {}
    for p, n in nodes.items():
        members_by_cg.setdefault(n.cycle_group_id, []).append(p)
    for cg in members_by_cg:
        members_by_cg[cg].sort()

    rows: list[dict[str, Any]] = []
    for u in units:
        for m in sorted(u.members):
            cg = nodes[m].cycle_group_id
            scc_siblings = [s for s in members_by_cg[cg] if s != m]
            rows.append(
                {
                    "path": m,
                    "cycle_group_id": u.cycle_group_id,
                    "topo_level": nodes[m].topo_level,
                    "scc_siblings": scc_siblings,
                    "target_cs": _artefact.produced_cs_rel(m),
                    "spec": f".codeconv/conversion-specs/{m}.md",
                    "plan": f".codeconv/conversion-plans/{m}.md",
                    "tombstone": f".codeconv/tombstones/{m}.md",
                }
            )
    rows.sort(key=lambda r: (r["topo_level"], r["path"]))
    return rows


# ---------------------------------------------------------------------------
# ingest  (== the durable run_codegen_step core)
# ---------------------------------------------------------------------------


def run_codegen_ingest(
    repo_root: Path,
    *,
    rel_path: str,
    data_dir: Optional[Path] = None,
    respec: bool = False,
    increment: int = 1,
    batch_id: Optional[str] = None,
    target_root: Optional[str] = None,
    build_fn: Optional[Callable[[Path], BuildResult]] = None,
    no_tombstone_update: bool = False,
) -> dict:
    """Deterministically ingest the produced ``.cs`` for ``rel_path``.

    Lifecycle (dbos_codegen_stage.md § Step body):

    1. Phase-1: ``INSERT … dart_codegen (path, codegen_started_at, sha)
       ON CONFLICT DO NOTHING``; ``respec`` re-opens a completed row on
       sha drift (UPDATE in place — never DELETE, R9).
    2. An open escalation artifact ⇒ record ``open_escalation_count`` and
       return ``escalated`` (conversion-blocked; no .cs guess).
    3. Locate the produced ``.cs``. **Absent ⇒ return
       ``{"outcome":"needs_agent_work", ...}``** (typed sentinel, NEVER
       raise).
    4. Present ⇒ validate it is real C# (artefact.py). Invalid ⇒
       ``needs_agent_work`` (the agent must fix the artifact).
    5. Valid ⇒ run the build gate. ``pass`` ⇒ phase-2 terminal write
       (``codegen_completed_at`` + ``target_cs_path``) ⇒ ``built``.
       ``fail`` ⇒ record ``build_status='fail'`` (a feedback fact, NOT an
       exception) and return ``needs_agent_work`` with the parsed errors
       for a bounded repair. ``not_built`` (env failure) ⇒
       ``needs_agent_work`` with the reason.

    Never raises on the normal paths. ``build_fn`` is injectable so
    in-process tests need no live ``dotnet``.
    """
    repo_root = Path(repo_root).resolve()
    tombstones_root = repo_root / ".codeconv" / "tombstones"
    engine = _engine(repo_root, data_dir)
    run_id = str(uuid.uuid4())

    # Validate the path is a real (non-orphaned) inventory entry.
    with engine.connect() as conn:
        f = conn.execute(
            text("SELECT sha256 FROM codeconv.dart_files WHERE path = :p"),
            {"p": rel_path},
        ).first()
        orphan = conn.execute(
            text("SELECT 1 FROM codeconv.dart_files_orphaned WHERE path = :p"),
            {"p": rel_path},
        ).first()
        existing = conn.execute(
            text(
                "SELECT codegen_started_at, codegen_completed_at, "
                "sha256_of_dart_at_codegen_start "
                "FROM codeconv.dart_codegen WHERE path = :p"
            ),
            {"p": rel_path},
        ).first()
    if f is None:
        return {
            "ok": False,
            "exit_code": 2,
            "outcome": "error",
            "error": f"path {rel_path!r} not in codeconv.dart_files",
            "path": rel_path,
        }
    if orphan is not None:
        return {
            "ok": False,
            "exit_code": 2,
            "outcome": "error",
            "error": f"path {rel_path!r} is orphaned; not a conversion target",
            "path": rel_path,
        }
    current_sha = f[0]

    # --- Phase-1: started marker (idempotent) + respec re-open. -------
    started_at = _utc_now()
    started_iso = _iso_or_none(started_at)
    drifted = (
        existing is not None
        and existing[2] is not None
        and existing[2] != current_sha
    )
    completed_before = existing is not None and existing[1] is not None
    with engine.begin() as conn:
        if existing is None:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_codegen "
                    "(path, codegen_started_at, "
                    " sha256_of_dart_at_codegen_start, build_status, "
                    " open_escalation_count, codegen_run_id) "
                    "VALUES (:p, :sa, :sha, 'not_built', 0, :rid) "
                    "ON CONFLICT (path) DO NOTHING"
                ),
                {"p": rel_path, "sa": started_at, "sha": current_sha,
                 "rid": run_id},
            )
        elif respec and (drifted or completed_before):
            # Re-open in place (FR-008/FR-019): reset terminal state,
            # re-snapshot sha. Never deletes the row (R9).
            conn.execute(
                text(
                    "UPDATE codeconv.dart_codegen SET "
                    "  codegen_started_at = :sa, "
                    "  codegen_completed_at = NULL, "
                    "  sha256_of_dart_at_codegen_start = :sha, "
                    "  target_cs_path = NULL, "
                    "  build_status = 'not_built', "
                    "  open_escalation_count = 0, "
                    "  promoted = false, "
                    "  codegen_run_id = :rid "
                    "WHERE path = :p"
                ),
                {"sa": started_at, "sha": current_sha, "rid": run_id,
                 "p": rel_path},
            )

    # Batch membership (promotion grouping). Tagging happens HERE, at
    # ingest, so EVERY file in a batch carries the batch_id — the
    # promotion gate's "100% of the batch's files build" must see unbuilt
    # / unreviewed members too, not only the sampled-and-reviewed ones.
    if batch_id is not None:
        with engine.begin() as conn:
            conn.execute(
                text(
                    "UPDATE codeconv.dart_codegen SET batch_id = :b "
                    "WHERE path = :p"
                ),
                {"b": batch_id, "p": rel_path},
            )

    if not no_tombstone_update:
        write_tombstone_with_codegen_keys(
            tombstones_root,
            rel_path,
            codegen_started_keys(codegen_started_at=started_iso),
        )

    # --- Escalation gate (escalate-don't-guess, FR-007). -------------
    esc_artifact = _esc.escalation_artifact_path(repo_root, rel_path)
    open_esc = _esc.count_open(esc_artifact)
    if open_esc > 0:
        with engine.begin() as conn:
            conn.execute(
                text(
                    "UPDATE codeconv.dart_codegen SET "
                    "open_escalation_count = :n, codegen_run_id = :rid "
                    "WHERE path = :p"
                ),
                {"n": int(open_esc), "rid": run_id, "p": rel_path},
            )
        return {
            "ok": True,
            "exit_code": 0,
            "outcome": "escalated",
            "path": rel_path,
            "open_escalation_count": int(open_esc),
            "conversion_blocked": True,
        }

    # --- Locate the produced .cs. ------------------------------------
    root = target_root or _artefact.DEFAULT_TARGET_ROOT
    cs_rel = _artefact.produced_cs_rel(rel_path, root)
    cs_path = repo_root / cs_rel
    if not cs_path.is_file():
        return {
            "ok": True,
            "exit_code": 0,
            "outcome": "needs_agent_work",
            "needs_agent_work": True,
            "path": rel_path,
            "target_cs": cs_rel,
            "reason": "produced .cs absent — codegen sub-agent must emit it",
        }

    # --- Validate it is real C# (inverse of convspec rule). ----------
    cs_text = cs_path.read_text(encoding="utf-8", errors="replace")
    val_errors = _artefact.validate_generated(cs_text)
    if val_errors:
        return {
            "ok": True,
            "exit_code": 0,
            "outcome": "needs_agent_work",
            "needs_agent_work": True,
            "path": rel_path,
            "target_cs": cs_rel,
            "reason": "produced file is not valid C#",
            "validation_errors": val_errors,
        }

    # --- Compile-list maintenance (Converted.props). -----------------
    # The .csproj sets ``EnableDefaultCompileItems=false`` so files only
    # compile when explicitly listed; the include MUST be in place before
    # the build runs, else the gate trivially passes on an excluded file.
    # On fail we revert the include so it cannot poison subsequent files.
    props_path = _buildprops.converted_props_path(repo_root, root)
    include_rel = _buildprops.include_relpath(cs_rel, root)
    _buildprops.update(props_path, add=include_rel)

    # --- Build gate (the hard gate). ---------------------------------
    project = _resolve_build_project(repo_root, cs_path, root)
    # Increment 2 runs `dotnet test` (build + execute → test_pass_rate);
    # Increment 1 runs `dotnet build` only. ``build_fn`` overrides both.
    builder = build_fn or (run_test if increment >= 2 else run_build)
    result = builder(project)
    if result.status != BUILD_PASS:
        _buildprops.update(props_path, remove=include_rel)

    with engine.begin() as conn:
        conn.execute(
            text(
                "UPDATE codeconv.dart_codegen SET "
                "build_status = :bs, target_cs_path = :tp, "
                "test_pass_rate = :tr, codegen_run_id = :rid "
                "WHERE path = :p"
            ),
            {
                "bs": result.status,
                "tp": cs_rel,
                "tr": result.test_pass_rate,
                "rid": run_id,
                "p": rel_path,
            },
        )

    if result.status == BUILD_PASS:
        completed_iso = _iso_or_none(_utc_now())
        with engine.begin() as conn:
            conn.execute(
                text(
                    "UPDATE codeconv.dart_codegen SET "
                    "codegen_completed_at = NOW(), "
                    "open_escalation_count = 0 "
                    "WHERE path = :p AND codegen_completed_at IS NULL"
                ),
                {"p": rel_path},
            )
        if not no_tombstone_update:
            write_tombstone_with_codegen_keys(
                tombstones_root,
                rel_path,
                codegen_completed_keys(
                    completed_at=completed_iso,
                    target_cs_path=cs_rel,
                    build_status=BUILD_PASS,
                    open_escalation_count=0,
                ),
            )
        return {
            "ok": True,
            "exit_code": 0,
            "outcome": "built",
            "path": rel_path,
            "target_cs": cs_rel,
            "build_status": BUILD_PASS,
            "test_pass_rate": result.test_pass_rate,
        }

    # Build fail / not_built — a recorded feedback fact, returned as
    # needs_agent_work for one bounded repair (NOT an exception). After
    # the bounded repair the agent escalates (writes the escalation
    # artifact) and re-ingest returns ``escalated``.
    return {
        "ok": True,
        "exit_code": 0,
        "outcome": "needs_agent_work",
        "needs_agent_work": True,
        "path": rel_path,
        "target_cs": cs_rel,
        "build_status": result.status,
        "build_errors": list(result.errors),
        "reason": result.reason
        or ("build failed — bounded repair or escalate"),
    }


def run_codegen_step(
    repo_root: str,
    data_dir: Optional[str] = None,
    rel_path: str = "",
    respec: bool = False,
) -> dict:
    """Durable-step entry point (T038). Delegates VERBATIM to
    :func:`run_codegen_ingest` (D2 — the durable layer adds nothing).

    Signature matches contract dbos_codegen_stage.md
    (``run_codegen_step(repo_root, data_dir, rel_path, respec=False)``).
    Never raises on the normal paths; returns the same typed outcome dict
    (``built｜needs_agent_work｜escalated``) the skill + durable layer
    branch on.
    """
    return run_codegen_ingest(
        Path(repo_root),
        rel_path=rel_path,
        data_dir=Path(data_dir) if data_dir else None,
        respec=respec,
    )


def _resolve_build_project(repo_root: Path, cs_path: Path, target_root: str) -> Path:
    """Resolve the .csproj/dir the build gate should compile.

    Nearest enclosing ``*.csproj`` walking up from the produced ``.cs``,
    bounded at the target root; fallback to the target root directory
    (``dotnet build`` accepts a directory containing one project).
    """
    root_abs = (repo_root / target_root.replace("\\", "/").strip("/")).resolve()
    cur = cs_path.parent.resolve()
    while True:
        projs = sorted(cur.glob("*.csproj"))
        if projs:
            return projs[0]
        if cur == root_abs or root_abs not in cur.parents:
            break
        cur = cur.parent
    # Fall back to a project at the target root, else the root dir.
    root_projs = sorted(root_abs.glob("*.csproj"))
    return root_projs[0] if root_projs else root_abs


# ---------------------------------------------------------------------------
# retry
# ---------------------------------------------------------------------------


def run_retry(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    path: str,
    no_tombstone_update: bool = False,
) -> dict:
    """Re-open a file for re-generation (stale or failed) — FR-008.

    Clears build/escalation/terminal state in place (never DELETE, R9):
    ``build_status='not_built'``, ``codegen_completed_at=NULL``,
    ``open_escalation_count=0``, ``promoted=false``, and re-snapshots the
    current source sha. Row absent ⇒ exit 2 (nothing to retry).
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
        row = conn.execute(
            text("SELECT 1 FROM codeconv.dart_codegen WHERE path = :p"),
            {"p": path},
        ).first()
    if f is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": f"path {path!r} not in codeconv.dart_files",
            "path": path,
        }
    if row is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": f"no codegen row to retry: {path}",
            "path": path,
        }

    started_at = _utc_now()
    with engine.begin() as conn:
        conn.execute(
            text(
                "UPDATE codeconv.dart_codegen SET "
                "  codegen_started_at = :sa, "
                "  codegen_completed_at = NULL, "
                "  sha256_of_dart_at_codegen_start = :sha, "
                "  target_cs_path = NULL, "
                "  build_status = 'not_built', "
                "  test_pass_rate = NULL, "
                "  open_escalation_count = 0, "
                "  promoted = false, "
                "  batch_id = NULL, "
                "  codegen_run_id = :rid "
                "WHERE path = :p"
            ),
            {"sa": started_at, "sha": f[0], "rid": run_id, "p": path},
        )

    if not no_tombstone_update:
        write_tombstone_with_codegen_keys(
            tombstones_root,
            path,
            codegen_started_keys(codegen_started_at=_iso_or_none(started_at)),
        )

    return {
        "ok": True,
        "exit_code": 0,
        "path": path,
        "action": "reopened",
        "run_id": run_id,
    }


# ---------------------------------------------------------------------------
# aggregate-escalations  (T021)
# ---------------------------------------------------------------------------


def run_aggregate_escalations(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    report_out: Optional[Path] = None,
    dry_run: bool = False,
    quiet: bool = False,
) -> dict:
    """Walk ``.codeconv/conversion-code/`` and write one escalations report.

    FR-009: each entry states kind, file(s), detail, and what is needed,
    ordered by ``(path ASC, E-number ASC)`` with a back-link to the
    source artifact. The total MUST equal ``Σ dart_codegen
    .open_escalation_count > 0`` (counts reconciled here). ``--dry-run``
    computes but writes nothing.
    """
    repo_root = Path(repo_root).resolve()
    root = _esc.artefact_root(repo_root)
    report_path = _esc.escalations_report_path(repo_root, override=report_out)

    collected: list[tuple[str, dict[str, str]]] = []
    if root.is_dir():
        for art in sorted(root.rglob("*.dart.md")):
            if art.name == _esc.ESCALATIONS_REPORT_NAME:
                continue
            rel = art.relative_to(root).as_posix()
            for e in _esc.iter_open(art):
                collected.append((rel, e))
    collected.sort(key=lambda t: (t[0], t[1]["e_number"]))

    lines: list[str] = [
        "# Aggregated Codegen Escalations",
        "",
        "Open codegen escalations across all generated files. Each MUST",
        "be resolved before the corresponding file is converted",
        "(FR-007/FR-009). The file is conversion-blocked until resolved.",
        "",
        f"Total open escalations: {len(collected)}",
        "",
    ]
    for rel, e in collected:
        anchor = f"e{e['e_number']}"
        lines += [
            f"## {rel} — E{e['e_number']}: {e['title']}",
            "",
            f"- **Kind**: {e['kind']}",
            f"- **File(s)**: {e['files']}",
            f"- **Detail**: {e['detail']}",
            f"- **Needs**: {e['needs']}",
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
# Helpers
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


__all__ = [
    "register",
    "run_aggregate_escalations",
    "run_codegen_ingest",
    "run_codegen_step",
    "run_next",
    "run_retry",
    "run_status",
]
