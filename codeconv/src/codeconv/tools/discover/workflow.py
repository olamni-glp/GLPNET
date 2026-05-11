"""codeconv discover workflow — Phase 6 / US4 / T072.

Maps to ``specs/012-codeconv-runner/contracts/codeconv_discover_cli.md``
§ Steps (normal mode) and § Steps (--from-tombstones mode).

High-level entry point: :func:`run_discover`. The CLI surface in
``__init__.py`` calls this function; tests call it via subprocess.

Effective workflow durability is provided by:

1. Per-file ``(mtime, sha256)`` idempotence short-circuit (R15) — files
   already inventoried are skipped on re-run, so killing mid-flight and
   re-invoking the same command resumes correctly (SC-009 / FR-017).
2. UPSERT semantics on ``codeconv.dart_files`` so partial writes can be
   safely retried.

DBOS-managed workflow wrapping (``@DBOS.workflow`` / ``@DBOS.step``) is
deferred to Phase 7 polish; the spec FR-017 behavioural contract — kill
and resume yields no re-parse of completed files — is satisfied by the
short-circuit mechanism above. The deferral is captured in the polish
task list.
"""

from __future__ import annotations

import hashlib
import logging
import time
import uuid
import warnings
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Optional

from sqlalchemy import text
from sqlalchemy.engine import Engine

from codeconv.bridge_client import acquire_or_discover
from codeconv.db.engine import build_engine

from .parse import _IMPORT_RE, extract_imports, extract_leading_doc
from .tombstone import (
    move_from_orphaned,
    move_to_orphaned,
    read_tombstone,
    write_tombstone,
)
from .walker import walk_dart_files


_LOG = logging.getLogger("codeconv.discover")


def register(dbos_app: Any) -> None:
    """Register discover with DBOS at runner startup.

    Currently a no-op — see module docstring on FR-017 deferral. Kept on
    the public surface so the contract (``codeconv_tool_contract.md``)
    is satisfied and a future polish phase can wire up the real DBOS
    workflow without an API change.
    """
    return None


def run_discover(
    repo_root: Path,
    *,
    mode: str = "normal",
    root: Optional[Path] = None,
    dry_run: bool = False,
    no_orphan_revival: bool = False,
    quiet: bool = True,
    bridge_script: Optional[Path] = None,
    data_dir: Optional[Path] = None,
) -> dict:
    """Acquire bridge → run discover workflow → return summary dict.

    ``data_dir`` overrides the default ``<repo_root>/.pgdb`` cluster
    location (for repos on PGLite-hostile filesystems like exFAT).
    """
    repo_root = Path(repo_root).resolve()
    subtree = (root or (repo_root / "glp_runtime_net")).resolve()
    tombstones_root = repo_root / ".codeconv" / "tombstones"
    tombstones_root.mkdir(parents=True, exist_ok=True)

    started_at = _utc_now()
    run_id = str(uuid.uuid4())

    endpoint = acquire_or_discover(
        repo_root,
        ready_timeout=30.0,
        bridge_script=bridge_script,
        data_dir=data_dir,
    )
    engine = build_engine(endpoint)

    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.discover_runs "
                "  (id, started_at, mode, files_processed, files_skipped_idempotent, warnings) "
                "VALUES (:id, :started_at, :mode, 0, 0, '[]'::jsonb)"
            ),
            {"id": run_id, "started_at": started_at, "mode": mode},
        )

    try:
        if mode == "from_tombstones":
            summary = _run_from_tombstones(
                engine, run_id, tombstones_root, dry_run
            )
        else:
            summary = _run_normal(
                engine,
                run_id,
                repo_root,
                subtree,
                tombstones_root,
                dry_run,
                no_orphan_revival,
                quiet,
            )
    finally:
        with engine.begin() as conn:
            conn.execute(
                text(
                    "UPDATE codeconv.discover_runs SET completed_at = :t WHERE id = :id"
                ),
                {"t": _utc_now(), "id": run_id},
            )

    summary["mode"] = mode
    try:
        summary["root"] = str(subtree.relative_to(repo_root)).replace("\\", "/")
    except ValueError:
        summary["root"] = str(subtree)
    return summary


# ---------------------------------------------------------------------------
# Normal mode
# ---------------------------------------------------------------------------


def _run_normal(
    engine: Engine,
    run_id: str,
    repo_root: Path,
    subtree: Path,
    tombstones_root: Path,
    dry_run: bool,
    no_orphan_revival: bool,
    quiet: bool,
) -> dict:
    t0 = time.monotonic()
    warnings_list: list[dict] = []

    files = list(walk_dart_files(subtree))
    files_total = len(files)

    with engine.begin() as conn:
        conn.execute(
            text(
                "UPDATE codeconv.discover_runs SET files_total = :n WHERE id = :id"
            ),
            {"n": files_total, "id": run_id},
        )

    files_processed = 0
    files_skipped = 0
    for abs_path, rel_path in files:
        result = _process_one_file(
            engine,
            run_id,
            abs_path,
            rel_path,
            subtree,
            tombstones_root,
            dry_run,
            warnings_list,
        )
        if result == "processed":
            files_processed += 1
        elif result == "skipped":
            files_skipped += 1

    orphaned = 0
    revived = 0
    if not dry_run:
        # Reconciliation phase — recompute callers from imports table.
        with engine.begin() as conn:
            conn.execute(text("DELETE FROM codeconv.dart_callers"))
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_callers (from_path, to_path) "
                    "SELECT from_path, to_path FROM codeconv.dart_imports "
                    "ON CONFLICT (from_path, to_path) DO NOTHING"
                )
            )

        # Orphan files no longer present.
        present = {rel for _, rel in files}
        with engine.begin() as conn:
            rows = conn.execute(
                text("SELECT path FROM codeconv.dart_files")
            ).all()
            in_db = {r[0] for r in rows}
            absent = in_db - present
            for rel in sorted(absent):
                conn.execute(
                    text(
                        "INSERT INTO codeconv.dart_files_orphaned "
                        "  (path, name, purpose, key_idea, mtime, sha256, "
                        "   discovered_at, orphaned_at) "
                        "SELECT path, name, purpose, key_idea, mtime, sha256, "
                        "       discovered_at, NOW() "
                        "FROM codeconv.dart_files WHERE path = :p "
                        "ON CONFLICT (path) DO UPDATE SET "
                        "  purpose = EXCLUDED.purpose, "
                        "  key_idea = EXCLUDED.key_idea, "
                        "  mtime = EXCLUDED.mtime, "
                        "  sha256 = EXCLUDED.sha256, "
                        "  orphaned_at = NOW()"
                    ),
                    {"p": rel},
                )
                conn.execute(
                    text("DELETE FROM codeconv.dart_files WHERE path = :p"),
                    {"p": rel},
                )
                conn.execute(
                    text(
                        "DELETE FROM codeconv.dart_imports "
                        "WHERE from_path = :p OR to_path = :p"
                    ),
                    {"p": rel},
                )
                conn.execute(
                    text(
                        "DELETE FROM codeconv.dart_callers "
                        "WHERE from_path = :p OR to_path = :p"
                    ),
                    {"p": rel},
                )
                move_to_orphaned(tombstones_root, rel)
                orphaned += 1

        # Revive previously-orphaned files now present.
        if not no_orphan_revival:
            with engine.begin() as conn:
                rows = conn.execute(
                    text("SELECT path FROM codeconv.dart_files_orphaned")
                ).all()
                orphaned_set = {r[0] for r in rows}
            for rel in sorted(orphaned_set & present):
                with engine.begin() as conn:
                    conn.execute(
                        text(
                            "DELETE FROM codeconv.dart_files_orphaned WHERE path = :p"
                        ),
                        {"p": rel},
                    )
                move_from_orphaned(tombstones_root, rel)
                revived += 1

        # Outside-subtree caller scan (FR-023).
        warnings_list.extend(_scan_outside_callers(repo_root, subtree))

        # Backfill tombstone callers list now that the inverted graph is settled.
        _backfill_tombstone_callers(engine, tombstones_root)

    with engine.begin() as conn:
        imports_count = (
            conn.execute(
                text("SELECT COUNT(*) FROM codeconv.dart_imports")
            ).scalar()
            or 0
        )
        callers_count = (
            conn.execute(
                text("SELECT COUNT(*) FROM codeconv.dart_callers")
            ).scalar()
            or 0
        )

    duration = time.monotonic() - t0
    return {
        "files_walked": files_total,
        "files_processed": files_processed,
        "files_skipped_idempotent": files_skipped,
        "imports": int(imports_count),
        "callers": int(callers_count),
        "orphaned": orphaned,
        "revived": revived,
        "warnings": warnings_list,
        "duration_seconds": round(duration, 2),
    }


def _process_one_file(
    engine: Engine,
    run_id: str,
    abs_path: Path,
    rel_path: str,
    subtree: Path,
    tombstones_root: Path,
    dry_run: bool,
    warnings_list: list[dict],
) -> str:
    try:
        st = abs_path.stat()
    except OSError:
        return "skipped"
    mtime = datetime.fromtimestamp(st.st_mtime, tz=timezone.utc)

    try:
        sha256 = hashlib.sha256(abs_path.read_bytes()).hexdigest()
    except OSError:
        return "skipped"

    # Idempotence short-circuit (R15) — primary resume mechanism.
    with engine.begin() as conn:
        existing = conn.execute(
            text("SELECT sha256 FROM codeconv.dart_files WHERE path = :p"),
            {"p": rel_path},
        ).first()
    if existing is not None and existing[0] == sha256:
        return "skipped"

    if dry_run:
        return "processed"

    purpose = extract_leading_doc(abs_path)
    key_idea = purpose

    with warnings.catch_warnings(record=True) as caught:
        warnings.simplefilter("always")
        imports_list = extract_imports(abs_path, subtree)
    for w in caught:
        msg = str(w.message)
        if "duplicate import" in msg:
            warnings_list.append(
                {
                    "kind": "duplicate_import",
                    "file": rel_path,
                    "import": msg,
                }
            )

    name = abs_path.name
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_files "
                "  (path, name, purpose, key_idea, mtime, sha256, discovered_at) "
                "VALUES (:path, :name, :purpose, :key_idea, :mtime, :sha256, NOW()) "
                "ON CONFLICT (path) DO UPDATE SET "
                "  name = EXCLUDED.name, "
                "  purpose = EXCLUDED.purpose, "
                "  key_idea = EXCLUDED.key_idea, "
                "  mtime = EXCLUDED.mtime, "
                "  sha256 = EXCLUDED.sha256, "
                "  discovered_at = NOW()"
            ),
            {
                "path": rel_path,
                "name": name,
                "purpose": purpose,
                "key_idea": key_idea,
                "mtime": mtime,
                "sha256": sha256,
            },
        )
        conn.execute(
            text("DELETE FROM codeconv.dart_imports WHERE from_path = :p"),
            {"p": rel_path},
        )
        conn.execute(
            text("DELETE FROM codeconv.dart_callers WHERE to_path = :p"),
            {"p": rel_path},
        )
        for to_path in imports_list:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_imports (from_path, to_path) "
                    "VALUES (:f, :t) "
                    "ON CONFLICT (from_path, to_path) DO NOTHING"
                ),
                {"f": rel_path, "t": to_path},
            )
        conn.execute(
            text(
                "UPDATE codeconv.discover_runs "
                "SET files_processed = files_processed + 1 WHERE id = :id"
            ),
            {"id": run_id},
        )

    fields = {
        "path": rel_path,
        "name": name,
        "purpose": purpose,
        "key_idea": key_idea,
        "dependencies": imports_list,
        "callers": [],  # filled by _backfill_tombstone_callers
        "mtime": _format_mtime(mtime),
        "sha256": sha256,
    }
    write_tombstone(tombstones_root, rel_path, fields)

    return "processed"


def _backfill_tombstone_callers(
    engine: Engine, tombstones_root: Path
) -> None:
    """Fill the ``callers`` field of every live tombstone from the
    finalised ``codeconv.dart_callers`` table.

    Live = under ``tombstones_root`` excluding the ``.orphaned/``
    subtree. Tombstones are rewritten in place; field ordering and
    sorting are enforced by ``write_tombstone``.
    """
    if not tombstones_root.is_dir():
        return
    with engine.begin() as conn:
        rows = conn.execute(
            text(
                "SELECT df.path, df.name, df.purpose, df.key_idea, df.mtime, df.sha256 "
                "FROM codeconv.dart_files df ORDER BY df.path"
            )
        ).all()
        all_imports = conn.execute(
            text(
                "SELECT from_path, to_path FROM codeconv.dart_imports"
            )
        ).all()
        all_callers = conn.execute(
            text(
                "SELECT to_path, from_path FROM codeconv.dart_callers"
            )
        ).all()

    deps_by: dict[str, list[str]] = {}
    for f, t in all_imports:
        deps_by.setdefault(f, []).append(t)
    callers_by: dict[str, list[str]] = {}
    for to_path, from_path in all_callers:
        callers_by.setdefault(to_path, []).append(from_path)

    for path, name, purpose, key_idea, mtime, sha256 in rows:
        fields = {
            "path": path,
            "name": name or Path(path).name,
            "purpose": purpose or "",
            "key_idea": key_idea or "",
            "dependencies": deps_by.get(path, []),
            "callers": callers_by.get(path, []),
            "mtime": _format_mtime(mtime) if isinstance(mtime, datetime) else str(mtime),
            "sha256": sha256,
        }
        write_tombstone(tombstones_root, path, fields)


def _scan_outside_callers(repo_root: Path, subtree: Path) -> list[dict]:
    """Scan the repo for ``.dart`` files OUTSIDE the subtree that import
    INTO it. Returns a list of warning dicts (FR-023). Edges are NEVER
    recorded in ``dart_callers`` for these matches.
    """
    out: list[dict] = []
    if not repo_root.is_dir():
        return out
    subtree_real = subtree.resolve()
    for sibling in repo_root.iterdir():
        if not sibling.is_dir():
            continue
        if sibling.resolve() == subtree_real:
            continue
        # Skip well-known noise dirs.
        if sibling.name in {".git", ".pgdb", ".codeconv", "node_modules", ".venv"}:
            continue
        for abs_path, _ in walk_dart_files(sibling):
            try:
                content = abs_path.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            for m in _IMPORT_RE.finditer(content):
                target = m.group("target").strip()
                if target.startswith(("package:", "dart:", "dart-ext:")):
                    continue
                try:
                    resolved = (abs_path.parent / target).resolve()
                except (OSError, RuntimeError):
                    continue
                try:
                    inside_rel = resolved.relative_to(subtree_real).as_posix()
                except ValueError:
                    continue
                try:
                    outside_rel = abs_path.resolve().relative_to(repo_root).as_posix()
                except ValueError:
                    outside_rel = str(abs_path)
                out.append(
                    {
                        "kind": "outside_caller",
                        "outside_file": outside_rel,
                        "inside_file": inside_rel,
                    }
                )
    return out


# ---------------------------------------------------------------------------
# --from-tombstones mode
# ---------------------------------------------------------------------------


def _run_from_tombstones(
    engine: Engine,
    run_id: str,
    tombstones_root: Path,
    dry_run: bool,
) -> dict:
    t0 = time.monotonic()
    warnings_list: list[dict] = []

    if not tombstones_root.is_dir():
        return {
            "files_walked": 0,
            "files_processed": 0,
            "files_skipped_idempotent": 0,
            "imports": 0,
            "callers": 0,
            "orphaned": 0,
            "revived": 0,
            "warnings": warnings_list,
            "duration_seconds": round(time.monotonic() - t0, 2),
        }

    if not dry_run:
        with engine.begin() as conn:
            conn.execute(
                text(
                    "TRUNCATE codeconv.dart_files, "
                    "         codeconv.dart_imports, "
                    "         codeconv.dart_callers"
                )
            )

    files_processed = 0
    for tomb_path in sorted(tombstones_root.rglob("*.dart.md")):
        rel_to_root = tomb_path.relative_to(tombstones_root)
        if ".orphaned" in rel_to_root.parts:
            continue
        try:
            fm = read_tombstone(tomb_path)
        except Exception as exc:
            warnings_list.append(
                {
                    "kind": "malformed_tombstone",
                    "path": str(tomb_path),
                    "error": str(exc),
                }
            )
            continue

        if dry_run:
            files_processed += 1
            continue

        rel_path = fm["path"]
        with engine.begin() as conn:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_files "
                    "  (path, name, purpose, key_idea, mtime, sha256, discovered_at) "
                    "VALUES (:path, :name, :purpose, :key_idea, "
                    "        :mtime, :sha256, NOW())"
                ),
                {
                    "path": rel_path,
                    "name": fm.get("name", "") or Path(rel_path).name,
                    "purpose": fm.get("purpose", "") or "",
                    "key_idea": fm.get("key_idea", "") or "",
                    "mtime": fm.get("mtime", "") or _format_mtime(_utc_now()),
                    "sha256": fm.get("sha256", "") or "",
                },
            )
            for to_path in fm.get("dependencies") or []:
                conn.execute(
                    text(
                        "INSERT INTO codeconv.dart_imports (from_path, to_path) "
                        "VALUES (:f, :t) "
                        "ON CONFLICT (from_path, to_path) DO NOTHING"
                    ),
                    {"f": rel_path, "t": to_path},
                )
            for caller in fm.get("callers") or []:
                conn.execute(
                    text(
                        "INSERT INTO codeconv.dart_callers (from_path, to_path) "
                        "VALUES (:f, :t) "
                        "ON CONFLICT (from_path, to_path) DO NOTHING"
                    ),
                    {"f": caller, "t": rel_path},
                )
        files_processed += 1

    with engine.begin() as conn:
        imports_count = (
            conn.execute(
                text("SELECT COUNT(*) FROM codeconv.dart_imports")
            ).scalar()
            or 0
        )
        callers_count = (
            conn.execute(
                text("SELECT COUNT(*) FROM codeconv.dart_callers")
            ).scalar()
            or 0
        )

    return {
        "files_walked": files_processed,
        "files_processed": files_processed,
        "files_skipped_idempotent": 0,
        "imports": int(imports_count),
        "callers": int(callers_count),
        "orphaned": 0,
        "revived": 0,
        "warnings": warnings_list,
        "duration_seconds": round(time.monotonic() - t0, 2),
    }


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _utc_now() -> datetime:
    return datetime.now(tz=timezone.utc)


def _format_mtime(dt: datetime) -> str:
    """ISO-8601 UTC with ms precision (matches PG ``timestamptz`` text)."""
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    ms = dt.microsecond // 1000
    return dt.strftime("%Y-%m-%dT%H:%M:%S.") + f"{ms:03d}Z"


__all__ = ["register", "run_discover"]
