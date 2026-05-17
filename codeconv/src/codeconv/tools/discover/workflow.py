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

from sqlalchemy import bindparam, text
from sqlalchemy.engine import Engine

from codeconv.bridge_client import acquire_or_discover
from codeconv.db.engine import build_engine

from .parse import _IMPORT_RE
from .pubspec import read_package_name
from .tombstone import (
    merge_preserving_feature015,
    move_from_orphaned,
    move_to_orphaned,
    read_tombstone,
    tombstone_path,
    write_tombstone,
)
from .walker import walk_dart_files


_LOG = logging.getLogger("codeconv.discover")


def _active_source_pair(engine: Optional[Any] = None) -> Any:
    """Resolve the language pair whose source hooks discover must use.

    Feature 016 / T011 + research R2/R3 (FR-004 carve-out): discover
    sources its Dart-specifics from the selected language pair via
    ``codeconv.langpairs`` instead of importing ``parse``/``pubspec``
    directly, so a new pair slots in with zero discover edits (SC-003).

    Per FR-004 / R3: discover tolerates an un-initialised workspace and
    falls back to the default registered pair (``dart→csharp``) so
    pre-016 ``/codeconv-discover`` usage is byte-identical; once a
    workspace IS initialised it is bound to the workspace-recorded pair.
    The resolved ``dart_csharp`` pair delegates to the exact same
    ``parse``/``pubspec`` implementations, so behaviour for the default
    Dart path is byte-identical (the feature-012/014/015 discover suites
    are the regression oracle — FR-023/SC-005).
    """
    from codeconv import langpairs

    if engine is not None:
        try:
            return langpairs.resolve_workspace_pair(
                engine, require_workspace=False
            )
        except Exception:
            # Defensive: an unreadable/missing workspace_settings (e.g.
            # pre-0003 cluster) must not break discover — fall back to
            # the registered default exactly as the no-workspace path.
            pass
    return langpairs.get("dart", "csharp")


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
    """Dispatch on ``mode`` → run discover workflow → return summary dict.

    Per ``specs/012-codeconv-runner/contracts/codeconv_discover_cli.md``
    (Amendment v2), mode dispatch happens BEFORE bridge acquisition:

    - ``verify_tombstones``: read-only source-truth audit; the bridge is
      NOT required and is never acquired.
    - ``from_tombstones``: parse + structural-validate + referential-
      completeness PREFLIGHT runs before any bridge / ``discover_runs`` /
      DB touch, so a bad input leaves the inventory untouched (exit 65).
    - ``normal``: unchanged feature-012/-014 behaviour.

    Every returned summary carries an ``exit_code`` (0 on success);
    ``data_dir`` overrides the default ``<repo_root>/.pgdb`` cluster
    location (for repos on PGLite-hostile filesystems like exFAT).
    """
    repo_root = Path(repo_root).resolve()
    subtree = (root or (repo_root / "glp_runtime_net")).resolve()
    tombstones_root = repo_root / ".codeconv" / "tombstones"
    # `--verify-tombstones` is strictly read-only (contract: NO DB writes,
    # NO tombstone rewrites). Creating the tombstone dir in a clean
    # checkout would be a filesystem write — only normal / from_tombstones
    # (which may write/rebuild tombstones) create it.
    if mode != "verify_tombstones":
        tombstones_root.mkdir(parents=True, exist_ok=True)

    def _finalise(summary: dict) -> dict:
        summary["mode"] = mode
        try:
            summary["root"] = str(
                subtree.relative_to(repo_root)
            ).replace("\\", "/")
        except ValueError:
            summary["root"] = str(subtree)
        summary.setdefault("exit_code", 0)
        return summary

    # ---- verify-tombstones: NO bridge, read-only, reads .dart -----------
    if mode == "verify_tombstones":
        # No bridge ⇒ no workspace read; FR-004 carve-out default pair.
        return _finalise(
            _run_verify_tombstones(
                repo_root, subtree, tombstones_root, _active_source_pair()
            )
        )

    # ---- from-tombstones: PREFLIGHT before any bridge / DB --------------
    preflight: Optional[dict] = None
    if mode == "from_tombstones":
        preflight = _preflight_from_tombstones(tombstones_root)
        if preflight.get("abort"):
            return _finalise(
                {
                    "files_walked": 0,
                    "files_processed": 0,
                    "files_skipped_idempotent": 0,
                    "imports": 0,
                    "callers": 0,
                    "orphaned": 0,
                    "revived": 0,
                    "warnings": preflight["warnings"],
                    "duration_seconds": preflight["duration_seconds"],
                    "error": preflight.get("error"),
                    "exit_code": 65,
                }
            )
        if dry_run:
            # --dry-run: do NOT touch DB / bridge; report would-be counts
            # straight from the validated preflight.
            return _finalise(
                {
                    "files_walked": len(preflight["files"]),
                    "files_processed": len(preflight["files"]),
                    "files_skipped_idempotent": 0,
                    "imports": len(preflight["imports"]),
                    "callers": len(preflight["callers"]),
                    "orphaned": len(preflight["orphaned"]),
                    "revived": 0,
                    "warnings": preflight["warnings"],
                    "duration_seconds": preflight["duration_seconds"],
                    "exit_code": 0,
                }
            )

    started_at = _utc_now()
    run_id = str(uuid.uuid4())

    endpoint = acquire_or_discover(
        repo_root,
        ready_timeout=30.0,
        bridge_script=bridge_script,
        data_dir=data_dir,
    )
    engine = build_engine(endpoint)

    # Feature 016 / T011: resolve the active language pair once per run.
    # Bound to the workspace-recorded pair when initialised; falls back to
    # the registered default (dart→csharp) otherwise (FR-004 carve-out /
    # R3). Discover sources its per-file source-parse hooks from this pair
    # so a new pair slots in with zero discover edits (SC-003); the
    # default Dart path is byte-identical (FR-023/SC-005).
    source_pair = _active_source_pair(engine)

    package_name = None
    pubspec_warning = None
    if mode == "normal":
        # Feature 014 / FR-004: read pubspec.yaml once per run; cache the
        # (name, warning) pair and thread it through the parser. Only
        # normal mode parses .dart, so this is normal-mode-only. The
        # warning's path field is repo-relative (feature-014 contract);
        # this is the dart_csharp pubspec implementation called with the
        # same args as pre-016 — byte-identical (regression oracle).
        package_name, pubspec_warning = read_package_name(
            subtree, repo_root=repo_root
        )

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
            assert preflight is not None  # validated above
            summary = _run_from_tombstones(
                engine, run_id, preflight, dry_run
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
                source_pair,
                package_name=package_name,
                pubspec_warning=pubspec_warning,
            )
    finally:
        with engine.begin() as conn:
            conn.execute(
                text(
                    "UPDATE codeconv.discover_runs SET completed_at = :t WHERE id = :id"
                ),
                {"t": _utc_now(), "id": run_id},
            )

    return _finalise(summary)


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
    source_pair: Any,
    *,
    package_name: Optional[str] = None,
    pubspec_warning: Optional[dict] = None,
) -> dict:
    t0 = time.monotonic()
    warnings_list: list[dict] = []

    # Feature 014 / FR-005: emit the pubspec_missing warning exactly once
    # per run, regardless of file count.
    if pubspec_warning is not None:
        warnings_list.append(pubspec_warning)

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
            source_pair,
            package_name=package_name,
        )
        if result == "processed":
            files_processed += 1
        elif result == "skipped":
            files_skipped += 1

    orphaned = 0
    revived = 0
    if not dry_run:
        # Reconciliation phase.
        with engine.begin() as conn:
            # Referential completeness (Amendment v3 / option A′): an
            # in-subtree import directive can resolve by path shape (R12)
            # to a file that does not (yet) exist on disk and was therefore
            # never inventoried into dart_files. Such an edge is dangling.
            # We WARN about it but do NOT delete it from dart_imports:
            # dart_imports is a faithful, persistent record of the source's
            # import directives. A destructive delete would lose the edge
            # permanently across idempotent discover runs (an unchanged
            # importer is sha-skipped, so a later-created target would never
            # re-add the edge). The dangling endpoint is instead resolved
            # non-destructively at read time by `depgraph compute` (which
            # filters edges to inventoried nodes before algorithm.compute,
            # self-healing once the target is inventoried).
            valid_paths = {
                r[0]
                for r in conn.execute(
                    text("SELECT path FROM codeconv.dart_files")
                ).all()
            }
            for from_path, to_path in conn.execute(
                text(
                    "SELECT from_path, to_path FROM codeconv.dart_imports"
                )
            ).all():
                if to_path not in valid_paths:
                    warnings_list.append(
                        {
                            "kind": "missing_target",
                            "path": to_path,
                            "referrer": from_path,
                        }
                    )
                elif from_path not in valid_paths:
                    warnings_list.append(
                        {
                            "kind": "missing_target",
                            "path": from_path,
                            "referrer": to_path,
                        }
                    )
            # Recompute callers as the faithful inverse of dart_imports.
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

        # Outside-subtree caller scan (FR-023 + Feature 014 / FR-006).
        warnings_list.extend(
            _scan_outside_callers(
                repo_root, subtree, package_name=package_name
            )
        )

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
    source_pair: Any,
    *,
    package_name: Optional[str] = None,
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

    # Feature 016 / T011: per-file source-parse via the resolved language
    # pair's hooks (dart_csharp delegates to the same parse impl —
    # byte-identical; FR-023/SC-005).
    purpose = source_pair.extract_leading_doc(abs_path)
    key_idea = purpose

    with warnings.catch_warnings(record=True) as caught:
        warnings.simplefilter("always")
        imports_list = source_pair.extract_imports(
            abs_path, subtree, package_name
        )
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
    # Carry forward the six feature-015 keys if a prior tombstone (e.g. from
    # `stamp-tombstones`) had them — discover must not erase depgraph /
    # conversion state on re-write (round-trip preservation, Amendment v2).
    fields = merge_preserving_feature015(
        fields, tombstone_path(tombstones_root, rel_path)
    )
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
        fields = merge_preserving_feature015(
            fields, tombstone_path(tombstones_root, path)
        )
        write_tombstone(tombstones_root, path, fields)


def _scan_outside_callers(
    repo_root: Path,
    subtree: Path,
    *,
    package_name: Optional[str] = None,
) -> list[dict]:
    """Scan the repo for ``.dart`` files OUTSIDE the subtree that import
    INTO it. Returns a list of warning dicts (FR-023). Edges are NEVER
    recorded in ``dart_callers`` for these matches.

    Feature 014 / FR-006: when ``package_name`` is supplied, an outside
    file importing ``package:<package_name>/...`` triggers the same
    ``outside_caller`` warning as the relative-path form.
    """
    out: list[dict] = []
    if not repo_root.is_dir():
        return out
    subtree_real = subtree.resolve()
    lib_real = (subtree_real / "lib").resolve()
    self_prefix = (
        f"package:{package_name}/" if package_name is not None else None
    )
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

                # Feature 014 / FR-006: self-package rewrite parity with
                # extract_imports. Must come BEFORE the external-skip.
                if (
                    self_prefix is not None
                    and target.startswith(self_prefix)
                    and len(target) > len(self_prefix)
                ):
                    rest = target[len(self_prefix):]
                    try:
                        resolved = (subtree_real / "lib" / rest).resolve()
                    except (OSError, RuntimeError):
                        continue
                    try:
                        resolved.relative_to(lib_real)
                    except ValueError:
                        continue
                    try:
                        inside_rel = resolved.relative_to(
                            subtree_real
                        ).as_posix()
                    except ValueError:
                        continue
                    try:
                        outside_rel = abs_path.resolve().relative_to(
                            repo_root
                        ).as_posix()
                    except ValueError:
                        outside_rel = str(abs_path)
                    out.append(
                        {
                            "kind": "outside_caller",
                            "outside_file": outside_rel,
                            "inside_file": inside_rel,
                        }
                    )
                    continue

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


_REQUIRED_012_FIELDS: tuple[str, ...] = ("path", "sha256")

# Feature-015 optional keys + their accepted Python types after YAML load.
# (``None`` is always accepted — that is the legitimate YAML-null state.)
_OPTIONAL_015_TYPES: dict[str, tuple[type, ...]] = {
    "topo_level": (int,),
    "cycle_group_id": (int,),
    "status": (str,),
    "conversion_started_at": (str,),
    "conversion_completed_at": (str,),
    "target_path": (str,),
}
_STATUS_VALUES = {"pending", "ready", "in_progress", "converted"}


def _preflight_from_tombstones(tombstones_root: Path) -> dict:
    """Parse + structural-validate + referential-completeness PREFLIGHT
    for ``--from-tombstones`` (contract § Steps (--from-tombstones mode)
    steps 1–3). Runs BEFORE any bridge / ``discover_runs`` / DB touch.

    Returns either ``{"abort": True, "warnings": [...], "error": <str>,
    "duration_seconds": ...}`` (caller maps to exit 65, zero mutation) or
    a validated payload: ``files`` (list of dart_files row dicts),
    ``imports`` / ``callers`` (referentially-complete edge tuples),
    ``orphaned`` (dart_files_orphaned row dicts), plus ``warnings``.
    """
    t0 = time.monotonic()
    warnings_list: list[dict] = []
    bad_files: list[str] = []
    raw: list[tuple[str, dict]] = []  # (rel_path, frontmatter)

    if not tombstones_root.is_dir():
        return {
            "abort": False,
            "warnings": warnings_list,
            "duration_seconds": round(time.monotonic() - t0, 2),
            "files": [],
            "imports": [],
            "callers": [],
            "orphaned": [],
        }

    # ---- Step 1+2a: walk T (excl .orphaned/), parse, required-field gate
    for tomb_path in sorted(tombstones_root.rglob("*.dart.md")):
        rel_to_root = tomb_path.relative_to(tombstones_root)
        if ".orphaned" in rel_to_root.parts:
            continue
        try:
            fm = read_tombstone(tomb_path)
        except Exception as exc:  # unparseable → hard-fail
            bad_files.append(f"{tomb_path} ({exc})")
            continue
        missing = []
        for key in _REQUIRED_012_FIELDS:
            val = fm.get(key)
            if not isinstance(val, str) or not val.strip():
                missing.append(key)
        if missing:
            bad_files.append(
                f"{tomb_path} (missing/invalid required field(s): "
                f"{', '.join(missing)})"
            )
            continue
        raw.append((str(fm["path"]).replace("\\", "/"), fm))

    if bad_files:
        return {
            "abort": True,
            "warnings": warnings_list,
            "error": (
                "codeconv discover --from-tombstones: ABORT — "
                f"{len(bad_files)} malformed / required-field-invalid "
                "tombstone(s); inventory left untouched:\n  "
                + "\n  ".join(bad_files)
            ),
            "duration_seconds": round(time.monotonic() - t0, 2),
        }

    # ---- Step 2b: optional feature-015 key type validation (warn-only) --
    for rel_path, fm in raw:
        for key, types in _OPTIONAL_015_TYPES.items():
            if key not in fm:
                continue
            val = fm[key]
            if val is None:
                continue  # legitimate YAML-null
            ok = isinstance(val, types)
            if ok and key == "status" and val not in _STATUS_VALUES:
                ok = False
            if not ok:
                warnings_list.append(
                    {
                        "kind": "type_invalid_015_field",
                        "path": rel_path,
                        "field": key,
                    }
                )

    valid_paths = {rel_path for rel_path, _ in raw}

    # ---- Step 3: referential completeness (drop+warn dangling edges) ----
    files: list[dict] = []
    imports: list[tuple[str, str]] = []
    callers: list[tuple[str, str]] = []
    for rel_path, fm in raw:
        files.append(
            {
                "path": rel_path,
                "name": fm.get("name") or Path(rel_path).name,
                "purpose": fm.get("purpose") or "",
                "key_idea": fm.get("key_idea") or "",
                "mtime": fm.get("mtime") or _format_mtime(_utc_now()),
                "sha256": fm["sha256"],
            }
        )
        deps = fm.get("dependencies")
        if deps is not None and not isinstance(deps, list):
            warnings_list.append(
                {"kind": "malformed_field", "path": rel_path, "field": "dependencies"}
            )
            deps = []
        for dep in deps or []:
            dep = str(dep).replace("\\", "/")
            if dep not in valid_paths:
                warnings_list.append(
                    {
                        "kind": "missing_tombstone",
                        "path": dep,
                        "referrer": rel_path,
                    }
                )
                continue
            imports.append((rel_path, dep))
        clrs = fm.get("callers")
        if clrs is not None and not isinstance(clrs, list):
            warnings_list.append(
                {"kind": "malformed_field", "path": rel_path, "field": "callers"}
            )
            clrs = []
        for caller in clrs or []:
            caller = str(caller).replace("\\", "/")
            if caller not in valid_paths:
                warnings_list.append(
                    {
                        "kind": "missing_tombstone",
                        "path": caller,
                        "referrer": rel_path,
                    }
                )
                continue
            callers.append((caller, rel_path))

    # ---- Collect orphaned tombstones for the step-6 UPSERT --------------
    orphaned: list[dict] = []
    orphan_root = tombstones_root / ".orphaned"
    if orphan_root.is_dir():
        for tomb_path in sorted(orphan_root.rglob("*.dart.md")):
            try:
                fm = read_tombstone(tomb_path)
            except Exception as exc:
                warnings_list.append(
                    {
                        "kind": "malformed_orphan_tombstone",
                        "path": str(tomb_path),
                        "error": str(exc),
                    }
                )
                continue
            opath = fm.get("path")
            if not isinstance(opath, str) or not opath.strip():
                continue
            orphaned.append(
                {
                    "path": opath.replace("\\", "/"),
                    "name": fm.get("name") or Path(opath).name,
                    "purpose": fm.get("purpose") or "",
                    "key_idea": fm.get("key_idea") or "",
                    "mtime": fm.get("mtime") or _format_mtime(_utc_now()),
                    "sha256": fm.get("sha256") or "",
                }
            )

    return {
        "abort": False,
        "warnings": warnings_list,
        "duration_seconds": round(time.monotonic() - t0, 2),
        "files": files,
        "imports": imports,
        "callers": callers,
        "orphaned": orphaned,
    }


def _run_from_tombstones(
    engine: Engine,
    run_id: str,
    preflight: dict,
    dry_run: bool,
) -> dict:
    """Apply a validated ``--from-tombstones`` preflight payload to the
    inventory in ONE transaction (contract § Steps step 5 — Option B).

    dart_imports / dart_callers are TRUNCATEd then bulk-reinserted (no
    inbound FK). dart_files is UPSERTed per surviving path then only the
    paths absent from T are DELETEd — so feature-015's dart_conversions /
    dart_depgraph (FK → dart_files ON DELETE CASCADE) are PRESERVED for
    every surviving file. A path genuinely absent from T is a vanished
    file; its DELETE cascades its conversion/depgraph row away.
    """
    t0 = time.monotonic()
    warnings_list: list[dict] = list(preflight["warnings"])
    files: list[dict] = preflight["files"]
    imports: list[tuple[str, str]] = preflight["imports"]
    callers: list[tuple[str, str]] = preflight["callers"]
    orphaned: list[dict] = preflight["orphaned"]

    if dry_run:
        return {
            "files_walked": len(files),
            "files_processed": len(files),
            "files_skipped_idempotent": 0,
            "imports": len(imports),
            "callers": len(callers),
            "orphaned": len(orphaned),
            "revived": 0,
            "warnings": warnings_list,
            "duration_seconds": round(time.monotonic() - t0, 2),
        }

    keep_paths = [f["path"] for f in files]

    with engine.begin() as conn:  # ONE transaction (atomic-per-run)
        # dart_imports / dart_callers: no inbound FK → safe to TRUNCATE.
        conn.execute(
            text(
                "TRUNCATE codeconv.dart_imports, codeconv.dart_callers"
            )
        )
        # dart_files: UPSERT each surviving path (NOT a blanket TRUNCATE,
        # so dart_conversions / dart_depgraph survive via the FK).
        for f in files:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_files "
                    "  (path, name, purpose, key_idea, mtime, sha256, discovered_at) "
                    "VALUES (:path, :name, :purpose, :key_idea, "
                    "        :mtime, :sha256, NOW()) "
                    "ON CONFLICT (path) DO UPDATE SET "
                    "  name = EXCLUDED.name, "
                    "  purpose = EXCLUDED.purpose, "
                    "  key_idea = EXCLUDED.key_idea, "
                    "  mtime = EXCLUDED.mtime, "
                    "  sha256 = EXCLUDED.sha256, "
                    "  discovered_at = NOW()"
                ),
                f,
            )
        # DELETE only vanished paths → cascades their conversion/depgraph.
        if keep_paths:
            stmt = text(
                "DELETE FROM codeconv.dart_files WHERE path NOT IN :keep"
            ).bindparams(bindparam("keep", expanding=True))
            conn.execute(stmt, {"keep": keep_paths})
        else:
            conn.execute(text("DELETE FROM codeconv.dart_files"))
        # Reinsert the referentially-complete edge sets.
        for f_path, t_path in imports:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_imports (from_path, to_path) "
                    "VALUES (:f, :t) "
                    "ON CONFLICT (from_path, to_path) DO NOTHING"
                ),
                {"f": f_path, "t": t_path},
            )
        for f_path, t_path in callers:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_callers (from_path, to_path) "
                    "VALUES (:f, :t) "
                    "ON CONFLICT (from_path, to_path) DO NOTHING"
                ),
                {"f": f_path, "t": t_path},
            )
        # Step 6: UPSERT .orphaned/ tombstones into dart_files_orphaned.
        for o in orphaned:
            conn.execute(
                text(
                    "INSERT INTO codeconv.dart_files_orphaned "
                    "  (path, name, purpose, key_idea, mtime, sha256, "
                    "   discovered_at, orphaned_at) "
                    "VALUES (:path, :name, :purpose, :key_idea, "
                    "        :mtime, :sha256, NOW(), NOW()) "
                    "ON CONFLICT (path) DO UPDATE SET "
                    "  name = EXCLUDED.name, "
                    "  purpose = EXCLUDED.purpose, "
                    "  key_idea = EXCLUDED.key_idea, "
                    "  mtime = EXCLUDED.mtime, "
                    "  sha256 = EXCLUDED.sha256, "
                    "  orphaned_at = NOW()"
                ),
                o,
            )

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
        "files_walked": len(files),
        "files_processed": len(files),
        "files_skipped_idempotent": 0,
        "imports": int(imports_count),
        "callers": int(callers_count),
        "orphaned": len(orphaned),
        "revived": 0,
        "warnings": warnings_list,
        "duration_seconds": round(time.monotonic() - t0, 2),
    }


# ---------------------------------------------------------------------------
# --verify-tombstones mode (read-only source-truth audit; NO bridge)
# ---------------------------------------------------------------------------


def _run_verify_tombstones(
    repo_root: Path,
    subtree: Path,
    tombstones_root: Path,
    source_pair: Any,
) -> dict:
    """Read-only source-truth audit (contract § Steps (--verify-tombstones
    mode)). Reads ``.dart`` sources; NO DB writes, NO tombstone rewrites;
    bridge NOT acquired. Returns a discover-shaped summary with the four
    verify counts + an ``exit_code`` (0 / 1 / 65).
    """
    t0 = time.monotonic()
    warnings_list: list[dict] = []

    def _summary(
        *,
        exit_code: int,
        error: Optional[str] = None,
        verified_clean: int = 0,
        stale: int = 0,
        missing_source: int = 0,
        missing_tombstone: int = 0,
        walked: int = 0,
    ) -> dict:
        out: dict = {
            "files_walked": walked,
            "files_processed": verified_clean,
            "files_skipped_idempotent": 0,
            "imports": 0,
            "callers": 0,
            "orphaned": 0,
            "revived": 0,
            "verified_clean": verified_clean,
            "stale": stale,
            "missing_source": missing_source,
            "missing_tombstone": missing_tombstone,
            "warnings": warnings_list,
            "duration_seconds": round(time.monotonic() - t0, 2),
            "exit_code": exit_code,
        }
        if error is not None:
            out["error"] = error
        return out

    # ---- Step 1: walk T (excl .orphaned/) ------------------------------
    tomb_set: list[tuple[Path, dict]] = []
    bad_files: list[str] = []
    if tombstones_root.is_dir():
        for tomb_path in sorted(tombstones_root.rglob("*.dart.md")):
            rel_to_root = tomb_path.relative_to(tombstones_root)
            if ".orphaned" in rel_to_root.parts:
                continue
            try:
                fm = read_tombstone(tomb_path)
            except Exception as exc:
                bad_files.append(f"{tomb_path} ({exc})")
                continue
            # Verify-mode is a source-truth audit: a format-invalid
            # tombstone (missing/!type-valid REQUIRED 012 field — path
            # AND sha256) aborts exit 65, same gate as the
            # --from-tombstones preflight. sha256 is required for the
            # recompute-and-compare; without it the file can't be
            # audited and MUST NOT pass as a mere stale warning.
            missing = [
                k
                for k in _REQUIRED_012_FIELDS
                if not isinstance(fm.get(k), str) or not fm[k].strip()
            ]
            if missing:
                bad_files.append(
                    f"{tomb_path} (missing/invalid required field(s): "
                    f"{', '.join(missing)})"
                )
                continue
            tomb_set.append((tomb_path, fm))

    # ---- Step 2: unparseable / format-invalid → abort exit 65 ----------
    if bad_files:
        return _summary(
            exit_code=65,
            error=(
                "codeconv discover --verify-tombstones: ABORT — "
                f"{len(bad_files)} malformed / unparseable tombstone(s):\n  "
                + "\n  ".join(bad_files)
            ),
        )

    # ---- Step 3: build whole-subtree import graph ONCE -----------------
    package_name, _ = read_package_name(subtree, repo_root=repo_root)
    present: dict[str, Path] = {}
    for abs_path, rel in walk_dart_files(subtree):
        present[rel.replace("\\", "/")] = abs_path

    # Step 5: zero .dart sources under the subtree → exit 1.
    if not present:
        return _summary(
            exit_code=1,
            error=(
                "codeconv discover --verify-tombstones: no .dart sources "
                f"under {subtree} — source-truth verification needs sources "
                "(exit 1; not 2)."
            ),
            walked=len(tomb_set),
        )

    derived_deps: dict[str, list[str]] = {}
    derived_sha: dict[str, str] = {}
    for rel, abs_path in present.items():
        derived_deps[rel] = sorted(
            source_pair.extract_imports(abs_path, subtree, package_name)
        )
        try:
            derived_sha[rel] = hashlib.sha256(
                abs_path.read_bytes()
            ).hexdigest()
        except OSError:
            derived_sha[rel] = ""
    derived_callers: dict[str, list[str]] = {}
    for frm, tos in derived_deps.items():
        for to in tos:
            derived_callers.setdefault(to, []).append(frm)
    for k in derived_callers:
        derived_callers[k] = sorted(set(derived_callers[k]))

    tomb_paths = {
        str(fm["path"]).replace("\\", "/") for _, fm in tomb_set
    }

    verified_clean = stale = missing_source = 0
    for _tomb_path, fm in tomb_set:
        rel_path = str(fm["path"]).replace("\\", "/")
        if rel_path not in present:
            warnings_list.append(
                {"kind": "missing_source", "path": rel_path}
            )
            missing_source += 1
            continue
        diff_fields: list[str] = []
        if str(fm.get("sha256") or "") != derived_sha.get(rel_path, ""):
            diff_fields.append("sha256")
        rec_deps = sorted(
            str(d).replace("\\", "/") for d in (fm.get("dependencies") or [])
        )
        if rec_deps != derived_deps.get(rel_path, []):
            diff_fields.append("dependencies")
        rec_callers = sorted(
            str(c).replace("\\", "/") for c in (fm.get("callers") or [])
        )
        if rec_callers != derived_callers.get(rel_path, []):
            diff_fields.append("callers")
        if diff_fields:
            warnings_list.append(
                {
                    "kind": "stale_tombstone",
                    "path": rel_path,
                    "fields": diff_fields,
                }
            )
            stale += 1
        else:
            verified_clean += 1

    # ---- Step 4: .dart with no tombstone in T → missing_tombstone ------
    missing_tombstone = 0
    for rel in sorted(present):
        if rel not in tomb_paths:
            warnings_list.append(
                {"kind": "missing_tombstone", "path": rel}
            )
            missing_tombstone += 1

    return _summary(
        exit_code=0,
        verified_clean=verified_clean,
        stale=stale,
        missing_source=missing_source,
        missing_tombstone=missing_tombstone,
        walked=len(tomb_set),
    )


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
