"""codeconv-depgraph workflow orchestrator.

Per ``specs/015-codeconv-depgraph/contracts/depgraph_cli.md``: each
subcommand-handler function (``run_compute``, ``run_mark_started``,
``run_mark_completed``, ``run_stamp_tombstones``,
``run_rebuild_conversions_from_tombstones``) acquires-or-discovers the
unified PGLite bridge, runs the relevant transaction(s), and returns a
summary dict.

Bridge lifecycle: handled by
:func:`codeconv.bridge_client.acquire_or_discover`; the depgraph tool is
just another consumer (feature 012 FR-006). Atomic-per-run write
protocol (FR-008) is implemented by ``run_compute`` inside a single
SQLAlchemy transaction.
"""

from __future__ import annotations

import time
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Optional

from sqlalchemy import text
from sqlalchemy.engine import Engine

from codeconv.bridge_client import acquire_or_discover
from codeconv.db.engine import build_engine

from .algorithm import DepgraphResult, compute
from .json_writer import write_depgraph_json
from .tombstone_writer import (
    update_conversion_keys,
    update_depgraph_keys,
    write_tombstone_with_extras,
)


def register(dbos_app: Any) -> None:
    """No-op stub for the feature-012 tool contract.

    Depgraph's writes are short-lived single transactions; nothing needs
    DBOS-managed durability today. Reserved for a future enhancement.
    """
    return None


# ---------------------------------------------------------------------------
# compute
# ---------------------------------------------------------------------------


def run_compute(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    json_out: Optional[Path] = None,
    dry_run: bool = False,
    quiet: bool = False,
) -> dict:
    """Read graph → compute → write DB + JSON. Atomic-per-run."""
    repo_root = Path(repo_root).resolve()
    json_out_path = (
        Path(json_out).resolve()
        if json_out is not None
        else (repo_root / ".codeconv" / "depgraph.json").resolve()
    )

    endpoint = acquire_or_discover(repo_root, ready_timeout=30.0, data_dir=data_dir)
    engine = build_engine(endpoint)

    t0 = time.monotonic()
    run_id = str(uuid.uuid4())
    started_at = _utc_now()

    # 1. Read inventory + conversions.
    with engine.connect() as conn:
        nodes = [r[0] for r in conn.execute(
            text("SELECT path FROM codeconv.dart_files")
        ).all()]
        if not nodes:
            return {
                "ok": False,
                "exit_code": 2,
                "error": "No inventoried files. Run /codeconv-discover first.",
                "files_total": 0,
            }
        edges = [
            (r[0], r[1])
            for r in conn.execute(
                text("SELECT from_path, to_path FROM codeconv.dart_imports")
            ).all()
        ]
        conv_rows = conn.execute(
            text(
                "SELECT path, started_at, completed_at FROM codeconv.dart_conversions"
            )
        ).all()
        last_discover_run_id = conn.execute(
            text(
                "SELECT id FROM codeconv.discover_runs "
                "WHERE completed_at IS NOT NULL "
                "ORDER BY completed_at DESC LIMIT 1"
            )
        ).scalar()

    conv_by_path: dict[str, tuple[Optional[datetime], Optional[datetime]]] = {}
    for path, started, completed in conv_rows:
        conv_by_path[path] = (started, completed)

    # 2. Run the algorithm.
    result = compute(nodes, edges)

    # 3. Derive status per FR-006.
    statuses = _derive_statuses(result, conv_by_path)

    # 4. Build per-file row data.
    file_rows = []
    for path in sorted(nodes):
        status = statuses[path]
        started, completed = conv_by_path.get(path, (None, None))
        target_path = None
        # target_path queried lazily below if any caller needs it; only the
        # JSON writer uses it, so look it up once for all converted files.
        file_rows.append(
            {
                "path": path,
                "topo_level": result.topo_level[path],
                "cycle_group_id": result.cycle_group_id[path],
                "ready": status == "ready",
                "status": status,
                "dependency_count": len(result.dependencies[path]),
                "caller_count": len(result.callers[path]),
                "depends_on": result.dependencies[path],
                "depended_on_by": result.callers[path],
                "conversion_started_at": _iso_or_none(started),
                "conversion_completed_at": _iso_or_none(completed),
                "target_path": None,
            }
        )

    # Fetch target_path for any file that has one (single query).
    with engine.connect() as conn:
        target_rows = conn.execute(
            text(
                "SELECT path, target_path FROM codeconv.dart_conversions "
                "WHERE target_path IS NOT NULL"
            )
        ).all()
    target_by_path = {r[0]: r[1] for r in target_rows}
    for row in file_rows:
        if row["path"] in target_by_path:
            row["target_path"] = target_by_path[row["path"]]

    # 5. Metrics.
    ready_count = sum(1 for r in file_rows if r["status"] == "ready")
    in_progress_count = sum(1 for r in file_rows if r["status"] == "in_progress")
    converted_count = sum(1 for r in file_rows if r["status"] == "converted")
    cycle_count = result.cycle_count

    # 6. Atomic-per-run write (FR-008): one transaction.
    if not dry_run:
        with engine.begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.depgraph_runs "
                    "  (id, started_at, mode) VALUES (:id, :sa, 'compute')"
                ),
                {"id": run_id, "sa": started_at},
            )
            conn.execute(text("DELETE FROM codeconv.dart_depgraph"))
            for row in file_rows:
                conn.execute(
                    text(
                        "INSERT INTO codeconv.dart_depgraph "
                        "  (path, topo_level, cycle_group_id, ready, status, "
                        "   dependency_count, caller_count, depgraph_run_id, "
                        "   discover_run_id) "
                        "VALUES (:path, :topo_level, :cycle_group_id, :ready, "
                        "        :status, :dependency_count, :caller_count, "
                        "        :run_id, :discover_run_id) "
                        "ON CONFLICT (path) DO UPDATE SET "
                        "  topo_level = EXCLUDED.topo_level, "
                        "  cycle_group_id = EXCLUDED.cycle_group_id, "
                        "  ready = EXCLUDED.ready, "
                        "  status = EXCLUDED.status, "
                        "  dependency_count = EXCLUDED.dependency_count, "
                        "  caller_count = EXCLUDED.caller_count, "
                        "  computed_at = NOW(), "
                        "  depgraph_run_id = EXCLUDED.depgraph_run_id, "
                        "  discover_run_id = EXCLUDED.discover_run_id"
                    ),
                    {
                        "path": row["path"],
                        "topo_level": row["topo_level"],
                        "cycle_group_id": row["cycle_group_id"],
                        "ready": row["ready"],
                        "status": row["status"],
                        "dependency_count": row["dependency_count"],
                        "caller_count": row["caller_count"],
                        "run_id": run_id,
                        "discover_run_id": str(last_discover_run_id)
                        if last_discover_run_id is not None
                        else None,
                    },
                )
            conn.execute(
                text(
                    "UPDATE codeconv.depgraph_runs SET "
                    "  completed_at = NOW(), files_total = :ft, "
                    "  ready_count = :rc, in_progress_count = :ipc, "
                    "  converted_count = :cc, cycle_count = :cyc "
                    "WHERE id = :id"
                ),
                {
                    "ft": len(file_rows),
                    "rc": ready_count,
                    "ipc": in_progress_count,
                    "cc": converted_count,
                    "cyc": cycle_count,
                    "id": run_id,
                },
            )

    # 7. Write JSON artefact.
    if not dry_run:
        write_depgraph_json(
            json_out_path,
            metadata={
                "generated_at": _iso_now(),
                "inventory_files_total": len(file_rows),
                "inventory_edges_total": len(edges),
                "ready_count": ready_count,
                "in_progress_count": in_progress_count,
                "converted_count": converted_count,
                "cycle_count": cycle_count,
                "last_discover_run_id": str(last_discover_run_id)
                if last_discover_run_id is not None
                else None,
            },
            file_rows=file_rows,
        )

    duration = time.monotonic() - t0
    return {
        "ok": True,
        "exit_code": 0,
        "files_total": len(file_rows),
        "edges_total": len(edges),
        "ready_count": ready_count,
        "in_progress_count": in_progress_count,
        "converted_count": converted_count,
        "cycle_count": cycle_count,
        "json_out": str(json_out_path),
        "dry_run": dry_run,
        "run_id": run_id,
        "duration_seconds": round(duration, 3),
    }


def _derive_statuses(
    result: DepgraphResult,
    conv_by_path: dict[str, tuple[Optional[datetime], Optional[datetime]]],
) -> dict[str, str]:
    """FR-006: classify every file into pending / ready / in_progress / converted.

    Eligibility for ``ready`` ignores intra-SCC edges; only edges that cross
    out of the file's SCC count, and they count only when fully converted.
    """
    cycle_group_id = result.cycle_group_id
    statuses: dict[str, str] = {}

    # First pass: anything with a conversions row goes straight to
    # in_progress / converted regardless of dependency state.
    for path in result.dependencies:  # iterates every node
        row = conv_by_path.get(path)
        if row is None:
            statuses[path] = "pending"  # provisional; refined below
        else:
            started, completed = row
            statuses[path] = "converted" if completed is not None else "in_progress"

    # Second pass: any pending file whose SCC-external dependencies are
    # all converted is promoted to ready. SCC members move together — if
    # any member is in_progress or converted, the SCC is not "ready as a
    # batch"; the per-row status above takes precedence.
    # Bucket files by SCC for the SCC-external-dep test.
    members_by_scc: dict[int, list[str]] = {}
    for p, sid in cycle_group_id.items():
        members_by_scc.setdefault(sid, []).append(p)

    for sid, members in members_by_scc.items():
        # Collect SCC-external dependencies for the whole SCC.
        external_deps: set[str] = set()
        for m in members:
            for dep in result.dependencies[m]:
                if cycle_group_id[dep] != sid:
                    external_deps.add(dep)
        all_external_done = all(
            statuses.get(d) == "converted" for d in external_deps
        )
        for m in members:
            if statuses[m] == "pending" and all_external_done:
                statuses[m] = "ready"

    return statuses


# ---------------------------------------------------------------------------
# mark-started
# ---------------------------------------------------------------------------


def run_mark_started(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    path: str,
    sha256: Optional[str] = None,
    no_tombstone_update: bool = False,
) -> dict:
    repo_root = Path(repo_root).resolve()
    tombstones_root = repo_root / ".codeconv" / "tombstones"

    endpoint = acquire_or_discover(repo_root, ready_timeout=30.0, data_dir=data_dir)
    engine = build_engine(endpoint)
    run_id = str(uuid.uuid4())

    with engine.connect() as conn:
        file_row = conn.execute(
            text("SELECT sha256 FROM codeconv.dart_files WHERE path = :p"),
            {"p": path},
        ).first()
    if file_row is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": f"path {path!r} not in codeconv.dart_files",
            "path": path,
        }
    db_sha256 = file_row[0]
    sha256_to_record = sha256 if sha256 is not None else db_sha256

    with engine.connect() as conn:
        conv_row = conn.execute(
            text(
                "SELECT started_at, completed_at "
                "FROM codeconv.dart_conversions WHERE path = :p"
            ),
            {"p": path},
        ).first()

    started_at_iso: Optional[str] = None
    if conv_row is not None:
        started, completed = conv_row
        if completed is not None:
            return {
                "ok": True,
                "exit_code": 0,
                "warning": f"already completed: {path}",
                "path": path,
                "action": "noop",
            }
        return {
            "ok": True,
            "exit_code": 0,
            "warning": f"already started: {path}",
            "path": path,
            "action": "noop",
        }

    started_at = _utc_now()
    started_at_iso = _iso_or_none(started_at)
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.depgraph_runs "
                "  (id, started_at, mode) VALUES (:id, :sa, 'mark-started')"
            ),
            {"id": run_id, "sa": started_at},
        )
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_conversions "
                "  (path, started_at, completed_at, sha256_of_dart_at_start, "
                "   target_path, marked_started_run_id) "
                "VALUES (:p, :sa, NULL, :sha, NULL, :rid)"
            ),
            {
                "p": path,
                "sa": started_at,
                "sha": sha256_to_record,
                "rid": run_id,
            },
        )
        conn.execute(
            text(
                "UPDATE codeconv.depgraph_runs SET completed_at = NOW() WHERE id = :id"
            ),
            {"id": run_id},
        )

    if not no_tombstone_update:
        _update_tombstone(
            tombstones_root,
            path,
            update_conversion_keys(started_at=started_at_iso),
        )

    return {
        "ok": True,
        "exit_code": 0,
        "path": path,
        "action": "started",
        "started_at": started_at_iso,
        "sha256_of_dart_at_start": sha256_to_record,
        "run_id": run_id,
    }


# ---------------------------------------------------------------------------
# mark-completed
# ---------------------------------------------------------------------------


def run_mark_completed(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    path: str,
    target: Optional[str] = None,
    no_tombstone_update: bool = False,
) -> dict:
    repo_root = Path(repo_root).resolve()
    tombstones_root = repo_root / ".codeconv" / "tombstones"

    endpoint = acquire_or_discover(repo_root, ready_timeout=30.0, data_dir=data_dir)
    engine = build_engine(endpoint)
    run_id = str(uuid.uuid4())

    with engine.connect() as conn:
        file_row = conn.execute(
            text("SELECT 1 FROM codeconv.dart_files WHERE path = :p"),
            {"p": path},
        ).first()
    if file_row is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": f"path {path!r} not in codeconv.dart_files",
            "path": path,
        }

    with engine.connect() as conn:
        conv_row = conn.execute(
            text(
                "SELECT started_at, completed_at, target_path "
                "FROM codeconv.dart_conversions WHERE path = :p"
            ),
            {"p": path},
        ).first()
    if conv_row is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": f"never started: {path}; call mark-started first",
            "path": path,
        }
    started, completed_at, existing_target = conv_row
    if completed_at is not None:
        return {
            "ok": True,
            "exit_code": 0,
            "warning": f"already completed: {path}",
            "path": path,
            "action": "noop",
            "target_path": existing_target,
        }

    completed_at_value = _utc_now()
    completed_at_iso = _iso_or_none(completed_at_value)
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.depgraph_runs "
                "  (id, started_at, mode) VALUES (:id, :sa, 'mark-completed')"
            ),
            {"id": run_id, "sa": completed_at_value},
        )
        conn.execute(
            text(
                "UPDATE codeconv.dart_conversions SET "
                "  completed_at = :ca, target_path = :tp, "
                "  marked_completed_run_id = :rid "
                "WHERE path = :p"
            ),
            {
                "ca": completed_at_value,
                "tp": target,
                "rid": run_id,
                "p": path,
            },
        )
        conn.execute(
            text(
                "UPDATE codeconv.depgraph_runs SET completed_at = NOW() WHERE id = :id"
            ),
            {"id": run_id},
        )

    if not no_tombstone_update:
        extras: dict[str, Any] = {"conversion_completed_at": completed_at_iso}
        # Note: target_path is NOT in tombstone _FIELD_ORDER (out of scope per
        # contracts/tombstone_format_delta.md). It lives in the DB only.
        _update_tombstone(tombstones_root, path, extras)

    return {
        "ok": True,
        "exit_code": 0,
        "path": path,
        "action": "completed",
        "completed_at": completed_at_iso,
        "target_path": target,
        "run_id": run_id,
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
    """Embed depgraph + conversion state into every tombstone's YAML frontmatter."""
    repo_root = Path(repo_root).resolve()
    tombstones_root = repo_root / ".codeconv" / "tombstones"

    endpoint = acquire_or_discover(repo_root, ready_timeout=30.0, data_dir=data_dir)
    engine = build_engine(endpoint)
    run_id = str(uuid.uuid4())
    started_at = _utc_now()

    with engine.connect() as conn:
        depg_rows = conn.execute(
            text(
                "SELECT path, topo_level, cycle_group_id, status "
                "FROM codeconv.dart_depgraph"
            )
        ).all()
        if not depg_rows:
            return {
                "ok": False,
                "exit_code": 2,
                "error": "no rows in codeconv.dart_depgraph; run `codeconv depgraph compute` first",
                "files_stamped": 0,
            }
        conv_rows = conn.execute(
            text(
                "SELECT path, started_at, completed_at "
                "FROM codeconv.dart_conversions"
            )
        ).all()

    conv_by_path: dict[str, tuple[Optional[datetime], Optional[datetime]]] = {
        r[0]: (r[1], r[2]) for r in conv_rows
    }

    files_stamped = 0
    for path, topo_level, cycle_group_id, status in depg_rows:
        started, completed = conv_by_path.get(path, (None, None))
        extras: dict[str, Any] = {}
        extras.update(
            update_depgraph_keys(
                topo_level=topo_level,
                cycle_group_id=cycle_group_id,
                status=status,
            )
        )
        if path in conv_by_path:
            extras["conversion_started_at"] = _iso_or_none(started)
            extras["conversion_completed_at"] = _iso_or_none(completed)
        else:
            # No row ⇒ keys absent from frontmatter (contract:
            # tombstone_format_delta.md § Null vs missing convention).
            pass

        if not dry_run:
            _update_tombstone(tombstones_root, path, extras)
        files_stamped += 1

    if not dry_run:
        with engine.begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.depgraph_runs "
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
# rebuild-conversions-from-tombstones
# ---------------------------------------------------------------------------


def run_rebuild_conversions_from_tombstones(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    dry_run: bool = False,
    quiet: bool = False,
) -> dict:
    """Rebuild ``codeconv.dart_conversions`` from tombstone YAML frontmatter."""
    repo_root = Path(repo_root).resolve()
    tombstones_root = repo_root / ".codeconv" / "tombstones"

    endpoint = acquire_or_discover(repo_root, ready_timeout=30.0, data_dir=data_dir)
    engine = build_engine(endpoint)
    run_id = str(uuid.uuid4())
    started_at = _utc_now()

    from codeconv.tools.discover.tombstone import read_tombstone

    rows_upserted = 0
    rows_skipped = 0
    if tombstones_root.is_dir():
        for tomb_path in sorted(tombstones_root.rglob("*.dart.md")):
            rel = tomb_path.relative_to(tombstones_root)
            if ".orphaned" in rel.parts:
                continue
            try:
                fm = read_tombstone(tomb_path)
            except Exception:
                rows_skipped += 1
                continue
            started_at_yaml = fm.get("conversion_started_at")
            if started_at_yaml is None or started_at_yaml in ("", "null"):
                # Key absent OR explicitly null ⇒ no conversion record to restore.
                rows_skipped += 1
                continue
            file_rel = fm.get("path")
            if not file_rel:
                rows_skipped += 1
                continue
            completed_at_yaml = fm.get("conversion_completed_at")
            # Look up current dart_files.sha256 — round-trip caveat per
            # contracts/depgraph_cli.md § "sha256 round-trip caveat".
            with engine.connect() as conn:
                file_row = conn.execute(
                    text("SELECT sha256 FROM codeconv.dart_files WHERE path = :p"),
                    {"p": file_rel},
                ).first()
            if file_row is None:
                rows_skipped += 1
                continue
            sha256 = file_row[0]

            if not dry_run:
                with engine.begin() as conn:
                    conn.execute(
                        text(
                            "INSERT INTO codeconv.dart_conversions "
                            "  (path, started_at, completed_at, "
                            "   sha256_of_dart_at_start, target_path) "
                            "VALUES (:p, :sa, :ca, :sha, NULL) "
                            "ON CONFLICT (path) DO UPDATE SET "
                            "  started_at = EXCLUDED.started_at, "
                            "  completed_at = EXCLUDED.completed_at, "
                            "  sha256_of_dart_at_start = EXCLUDED.sha256_of_dart_at_start"
                        ),
                        {
                            "p": file_rel,
                            "sa": _parse_iso(started_at_yaml),
                            "ca": _parse_iso(completed_at_yaml)
                            if completed_at_yaml
                            else None,
                            "sha": sha256,
                        },
                    )
            rows_upserted += 1

    if not dry_run:
        with engine.begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.depgraph_runs "
                    "  (id, started_at, completed_at, mode, files_total) "
                    "VALUES (:id, :sa, NOW(), 'rebuild-conversions-from-tombstones', :ft)"
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
# Helpers
# ---------------------------------------------------------------------------


def _utc_now() -> datetime:
    return datetime.now(tz=timezone.utc)


def _iso_now() -> str:
    return _iso_or_none(_utc_now()) or ""


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
    # Handle trailing Z (ISO 8601 UTC suffix not accepted by datetime.fromisoformat
    # on Python 3.10; supported on 3.11+ which is the project minimum).
    if txt.endswith("Z"):
        txt = txt[:-1] + "+00:00"
    return datetime.fromisoformat(txt)


def _update_tombstone(
    tombstones_root: Path, rel_path: str, extras: dict
) -> None:
    """Merge extras into an existing tombstone's frontmatter, preserving order."""
    write_tombstone_with_extras(tombstones_root, rel_path, extras)


__all__ = [
    "register",
    "run_compute",
    "run_mark_completed",
    "run_mark_started",
    "run_rebuild_conversions_from_tombstones",
    "run_stamp_tombstones",
]
