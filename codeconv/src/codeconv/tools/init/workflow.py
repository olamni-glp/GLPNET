"""``codeconv init`` workflow — Feature 016 / US1 + US4.

Source of truth: ``specs/016-codeconv-init-scaffold-langpair/contracts/codeconv_init_cli.md``
(spec FR-006..FR-012, FR-019, FR-021, D3/D6).

``init`` owns **workspace configuration, exclusions, and conversion-phase
tracking** only. It does NOT scan the source tree itself — the per-file
inventory is delegated to the existing ``codeconv discover`` (single
inventory source of truth, decision D3). Every persistent table written
here lives under the ``codeconv`` schema (FR-020); no ``public.*`` object
is created or consulted (D1/R6).

Bridge lifecycle: this tool is just another consumer of the shared
``codeconv.bridge_client.acquire_or_discover`` (feature 012 FR-006 /
FR-019). All DB mutations for one ``init`` run happen in a single
transaction (contract § run behaviour 5).
"""

from __future__ import annotations

from pathlib import Path
from typing import Any, Optional

from sqlalchemy import text

from codeconv.bridge_client import acquire_or_discover
from codeconv.db.engine import build_engine


# Exit codes — contract § Exit codes.
_EXIT_OK = 0
_EXIT_GENERIC = 1
_EXIT_INVALID_PATH = 2
_EXIT_UNREGISTERED_PAIR = 5

# Conversion-phase ordering seeded by ``init`` (data-model §1.3/§1.4;
# D2NET parity: discover then scaffold are the language-specific stages).
_PHASE_SEQUENCE: tuple[tuple[str, int], ...] = (
    ("discover", 1),
    ("scaffold", 2),
)

# Reserved names a source/target path may not be (FR-012). These collide
# with the unified-bridge / codeconv on-disk surface.
_RESERVED_NAMES: frozenset[str] = frozenset(
    {".pgdb", ".pgdb.bridge.lock", ".codeconv", ".git"}
)


def register(dbos_app: Any) -> None:
    """No-op stub for the feature-012 tool contract.

    ``init``'s writes are short-lived single transactions; nothing needs
    DBOS-managed durability. Kept on the public surface so the contract
    (``codeconv_tool_contract.md``) is satisfied.
    """
    return None


# ---------------------------------------------------------------------------
# Path validation (FR-012)
# ---------------------------------------------------------------------------


def _validate_subpath(repo_root: Path, raw: str, *, must_exist: bool) -> Path:
    """Validate a repo-relative source/target path (FR-012).

    Rules: non-empty; resolves INSIDE the repo (no ``..`` escape); leaf
    is not a reserved name; (source) the directory exists. Raises
    :class:`_InvalidPath` (caller → exit 2, no state) on any violation.
    """
    s = (raw or "").strip().replace("\\", "/").strip("/")
    if not s:
        raise _InvalidPath("path is empty")
    resolved = (repo_root / s).resolve()
    try:
        rel = resolved.relative_to(repo_root)
    except ValueError:
        raise _InvalidPath(
            f"path {raw!r} resolves outside the repo ({resolved})"
        )
    parts = rel.parts
    if not parts:
        raise _InvalidPath(f"path {raw!r} resolves to the repo root")
    if any(p in _RESERVED_NAMES for p in parts):
        raise _InvalidPath(
            f"path {raw!r} uses a reserved name "
            f"(reserved: {sorted(_RESERVED_NAMES)})"
        )
    if must_exist and not resolved.is_dir():
        raise _InvalidPath(
            f"source path {raw!r} does not exist or is not a directory "
            f"({resolved})"
        )
    return resolved


class _InvalidPath(Exception):
    """A source/target path failed FR-012 validation (→ exit 2)."""


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _read_settings(engine: Any) -> dict[str, str]:
    with engine.connect() as conn:
        rows = conn.execute(
            text("SELECT key, value FROM codeconv.workspace_settings")
        ).all()
    return {k: v for k, v in rows}


def _glob_to_excl_path(glob: str) -> str:
    """Map a pair tool-exclusion glob to an ``excluded_directories.path``.

    Directory-form globs (``.dart_tool/``, ``build/``) become the bare
    directory name; filename-suffix globs (``*.g.dart``) are recorded
    verbatim so the record is faithful to the pair's recommendation
    (discover prunes by name regardless; the row is the audit/scope
    record per data-model §1.2 / FR-007).
    """
    g = glob.strip()
    if g.endswith("/"):
        return g[:-1]
    return g


def _apply_exclusion_scope(engine: Any) -> None:
    """Prune ``codeconv.dart_files`` (and its edges) to the in-scope set.

    D3: discover is the single scanner; ``init`` owns exclusions. After
    delegating the inventory to discover, ``init`` removes any inventory
    row that falls under a recorded ``excluded_directories`` directory so
    the in-scope inventory stays consistent with the exclusion set
    (FR-011). Filename-suffix exclusions (``*.g.dart``) are already
    pruned by discover's walker and need no row-level pruning here.
    """
    with engine.begin() as conn:
        excl = [
            r[0]
            for r in conn.execute(
                text(
                    "SELECT path FROM codeconv.excluded_directories "
                    "WHERE path NOT LIKE '*%'"
                )
            ).all()
        ]
        for d in excl:
            d = d.strip("/")
            if not d:
                continue
            params = {"eq": d, "pfx": d + "/%"}
            conn.execute(
                text(
                    "DELETE FROM codeconv.dart_imports "
                    "WHERE from_path = :eq OR to_path = :eq "
                    "   OR from_path LIKE :pfx OR to_path LIKE :pfx"
                ),
                params,
            )
            conn.execute(
                text(
                    "DELETE FROM codeconv.dart_callers "
                    "WHERE from_path = :eq OR to_path = :eq "
                    "   OR from_path LIKE :pfx OR to_path LIKE :pfx"
                ),
                params,
            )
            conn.execute(
                text(
                    "DELETE FROM codeconv.dart_files "
                    "WHERE path = :eq OR path LIKE :pfx"
                ),
                params,
            )


def _delegate_discover(
    repo_root: Path,
    source_abs: Path,
    data_dir: Optional[Path],
    quiet: bool,
) -> dict:
    """In-process discover delegation (D3 / FR-009 / contract step 6).

    No subprocess, no second bridge acquisition — ``init`` already holds
    the bridge; ``run_discover`` reuses ``acquire_or_discover`` which
    discovers the running sidecar.
    """
    from codeconv.tools.discover.workflow import run_discover

    return run_discover(
        repo_root,
        mode="normal",
        root=source_abs,
        quiet=quiet,
        data_dir=data_dir,
    )


# ---------------------------------------------------------------------------
# init run (FR-006..FR-010, FR-012)
# ---------------------------------------------------------------------------


def run_init(
    *,
    repo_root: Path,
    source: str = "glp_runtime_net",
    target: Optional[str] = None,
    source_lang: str = "dart",
    target_lang: str = "csharp",
    exclude: Optional[list[str]] = None,
    accept_suggested_exclusions: bool = False,
    non_interactive: bool = False,
    rebuild: bool = False,
    confirm_rebuild: bool = False,
    data_dir: Optional[Path] = None,
    quiet: bool = False,
) -> dict:
    """Configure the workspace + delegate the inventory to discover.

    Implements ``contracts/codeconv_init_cli.md`` § run behaviour 1–7.
    """
    repo_root = Path(repo_root).resolve()
    exclude = list(exclude or [])

    # 1. Resolve the pair against the registry — unregistered → exit 5,
    #    NO writes (FR-005). Done BEFORE the bridge so an unregistered
    #    pair never touches the DB.
    from codeconv import langpairs

    try:
        pair = langpairs.get(source_lang, target_lang)
    except langpairs.UnknownLangPair as exc:
        return {
            "ok": False,
            "exit_code": _EXIT_UNREGISTERED_PAIR,
            "error": str(exc),
            "registered_pairs": [
                f"{s}->{t}" for s, t in langpairs.list_pairs()
            ],
        }

    # 2. Validate source/target — invalid → exit 2, NO partial state
    #    (FR-012). Done BEFORE the bridge so invalid input never touches
    #    the DB.
    try:
        source_abs = _validate_subpath(repo_root, source, must_exist=True)
        if target is None or not str(target).strip():
            raise _InvalidPath(
                "--target is required for a usable workspace"
            )
        _validate_subpath(repo_root, target, must_exist=False)
    except _InvalidPath as exc:
        return {
            "ok": False,
            "exit_code": _EXIT_INVALID_PATH,
            "error": f"invalid path: {exc}",
        }

    if non_interactive and not (accept_suggested_exclusions or exclude):
        return {
            "ok": False,
            "exit_code": _EXIT_INVALID_PATH,
            "error": (
                "--non-interactive requires --accept-suggested-exclusions "
                "or at least one --exclude"
            ),
        }

    source_rel = source_abs.relative_to(repo_root).as_posix()
    target_rel = (
        (repo_root / target.strip().replace("\\", "/").strip("/"))
        .resolve()
        .relative_to(repo_root)
        .as_posix()
    )

    # 3. Acquire-or-discover the shared bridge (FR-019).
    endpoint = acquire_or_discover(
        repo_root, ready_timeout=60.0, data_dir=data_dir
    )
    engine = build_engine(endpoint)

    existing = _read_settings(engine)
    already_init = bool(
        existing.get("source_lang") and existing.get("target_lang")
    )
    desired = {
        "source_lang": source_lang,
        "target_lang": target_lang,
        "source_path": source_rel,
        "target_path": target_rel,
    }

    # 4a. Destructive re-init gate (FR-010): --rebuild requires the
    #     explicit confirmation token (skill-driven). Refuse otherwise;
    #     NO state discarded.
    if rebuild and not confirm_rebuild:
        return {
            "ok": False,
            "exit_code": _EXIT_INVALID_PATH,
            "error": (
                "--rebuild is destructive (discards existing workspace "
                "state). Re-run with --confirm-rebuild to proceed "
                "(the /codeconv-init skill drives this confirmation)."
            ),
            "already_initialized": already_init,
        }

    # 4b. Idempotent: already initialised with unchanged inputs and not a
    #     rebuild → no mutation, exit 0 (FR-010 / SC-002).
    if already_init and not rebuild:
        unchanged = all(
            existing.get(k) == v for k, v in desired.items()
        )
        if unchanged:
            return {
                "ok": True,
                "exit_code": _EXIT_OK,
                "already_initialized": True,
                "source_lang": source_lang,
                "target_lang": target_lang,
                "source_path": source_rel,
                "target_path": target_rel,
                "message": "already initialized (idempotent no-op)",
            }

    # 5. One transaction: UPSERT workspace_settings; compute + UPSERT
    #    excluded_directories (tool recs + manual); seed phase tables.
    tool_globs = list(pair.tool_exclusion_globs())
    tool_excls = sorted({_glob_to_excl_path(g) for g in tool_globs})
    manual_excls = sorted(
        {
            e.strip().replace("\\", "/").strip("/")
            for e in exclude
            if e and e.strip()
        }
    )

    with engine.begin() as conn:
        if rebuild:
            # Destructive re-init: clear the workspace tables (NOT the
            # inventory — discover re-establishes it below).
            conn.execute(text("DELETE FROM codeconv.workspace_settings"))
            conn.execute(text("DELETE FROM codeconv.excluded_directories"))
            conn.execute(text("DELETE FROM codeconv.phase_status"))
            conn.execute(text("DELETE FROM codeconv.phase_sequence"))

        for k, v in desired.items():
            conn.execute(
                text(
                    "INSERT INTO codeconv.workspace_settings (key, value) "
                    "VALUES (:k, :v) "
                    "ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value"
                ),
                {"k": k, "v": v},
            )

        for path in tool_excls:
            conn.execute(
                text(
                    "INSERT INTO codeconv.excluded_directories (path, kind) "
                    "VALUES (:p, 'tool') "
                    "ON CONFLICT (path) DO UPDATE SET kind = 'tool'"
                ),
                {"p": path},
            )
        for path in manual_excls:
            conn.execute(
                text(
                    "INSERT INTO codeconv.excluded_directories (path, kind) "
                    "VALUES (:p, 'manual') "
                    "ON CONFLICT (path) DO NOTHING"
                ),
                {"p": path},
            )

        for phase, seq in _PHASE_SEQUENCE:
            conn.execute(
                text(
                    "INSERT INTO codeconv.phase_sequence (phase, sequence) "
                    "VALUES (:p, :s) "
                    "ON CONFLICT (phase) DO UPDATE SET sequence = EXCLUDED.sequence"
                ),
                {"p": phase, "s": seq},
            )
            conn.execute(
                text(
                    "INSERT INTO codeconv.phase_status (phase, status) "
                    "VALUES (:p, 'PENDING') "
                    "ON CONFLICT (phase) DO NOTHING"
                ),
                {"p": phase},
            )

    # 6. Delegate the inventory to discover (D3 / FR-009). Then prune to
    #    the exclusion scope (FR-011) so the in-scope inventory matches.
    disc = _delegate_discover(repo_root, source_abs, data_dir, quiet)
    _apply_exclusion_scope(engine)

    with engine.connect() as conn:
        files_in_scope = int(
            conn.execute(
                text("SELECT COUNT(*) FROM codeconv.dart_files")
            ).scalar()
            or 0
        )

    # 7. Summary.
    return {
        "ok": True,
        "exit_code": _EXIT_OK,
        "already_initialized": False,
        "source_lang": source_lang,
        "target_lang": target_lang,
        "source_path": source_rel,
        "target_path": target_rel,
        "tool_exclusions": tool_excls,
        "manual_exclusions": manual_excls,
        "phases_seeded": [p for p, _ in _PHASE_SEQUENCE],
        "inventory_files": files_in_scope,
        "discover": {
            "files_walked": disc.get("files_walked"),
            "files_processed": disc.get("files_processed"),
            "exit_code": disc.get("exit_code", 0),
        },
        "rebuilt": bool(rebuild),
    }


# ---------------------------------------------------------------------------
# add-exclude / remove-exclude (FR-011)
# ---------------------------------------------------------------------------


def _require_workspace(engine: Any) -> Optional[dict]:
    s = _read_settings(engine)
    if not (s.get("source_lang") and s.get("target_lang")):
        return {
            "ok": False,
            "exit_code": _EXIT_INVALID_PATH,
            "error": "no initialised workspace; run `codeconv init` first",
        }
    return None


def _resync_after_exclusion_change(
    repo_root: Path,
    engine: Any,
    data_dir: Optional[Path],
    quiet: bool,
) -> dict:
    """Re-delegate discover then re-apply the exclusion scope so
    ``codeconv.dart_files`` matches the new exclusion set (FR-011)."""
    s = _read_settings(engine)
    source_rel = s.get("source_path") or "glp_runtime_net"
    source_abs = (repo_root / source_rel).resolve()
    disc = _delegate_discover(repo_root, source_abs, data_dir, quiet)
    _apply_exclusion_scope(engine)
    with engine.connect() as conn:
        n = int(
            conn.execute(
                text("SELECT COUNT(*) FROM codeconv.dart_files")
            ).scalar()
            or 0
        )
    return {"discover_exit": disc.get("exit_code", 0), "inventory_files": n}


def run_add_exclude(
    *,
    repo_root: Path,
    path: str,
    data_dir: Optional[Path] = None,
    quiet: bool = False,
) -> dict:
    """Add a manual exclusion then re-sync the inventory (FR-011)."""
    repo_root = Path(repo_root).resolve()
    endpoint = acquire_or_discover(
        repo_root, ready_timeout=60.0, data_dir=data_dir
    )
    engine = build_engine(endpoint)
    refuse = _require_workspace(engine)
    if refuse is not None:
        return refuse

    rel = (path or "").strip().replace("\\", "/").strip("/")
    if not rel:
        return {
            "ok": False,
            "exit_code": _EXIT_INVALID_PATH,
            "error": "exclusion path is empty",
        }
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.excluded_directories (path, kind) "
                "VALUES (:p, 'manual') "
                "ON CONFLICT (path) DO UPDATE SET kind = 'manual'"
            ),
            {"p": rel},
        )
    resync = _resync_after_exclusion_change(
        repo_root, engine, data_dir, quiet
    )
    return {
        "ok": True,
        "exit_code": _EXIT_OK,
        "action": "add-exclude",
        "path": rel,
        **resync,
    }


def run_remove_exclude(
    *,
    repo_root: Path,
    path: str,
    data_dir: Optional[Path] = None,
    quiet: bool = False,
) -> dict:
    """Remove an exclusion then re-sync the inventory (FR-011)."""
    repo_root = Path(repo_root).resolve()
    endpoint = acquire_or_discover(
        repo_root, ready_timeout=60.0, data_dir=data_dir
    )
    engine = build_engine(endpoint)
    refuse = _require_workspace(engine)
    if refuse is not None:
        return refuse

    rel = (path or "").strip().replace("\\", "/").strip("/")
    with engine.begin() as conn:
        deleted = conn.execute(
            text(
                "DELETE FROM codeconv.excluded_directories WHERE path = :p"
            ),
            {"p": rel},
        ).rowcount
    resync = _resync_after_exclusion_change(
        repo_root, engine, data_dir, quiet
    )
    return {
        "ok": True,
        "exit_code": _EXIT_OK,
        "action": "remove-exclude",
        "path": rel,
        "removed": int(deleted or 0),
        **resync,
    }


# ---------------------------------------------------------------------------
# list / inspect (read-only)
# ---------------------------------------------------------------------------


def run_list(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    quiet: bool = False,
) -> dict:
    """List the in-scope inventoried files (read-only)."""
    repo_root = Path(repo_root).resolve()
    endpoint = acquire_or_discover(
        repo_root, ready_timeout=60.0, data_dir=data_dir
    )
    engine = build_engine(endpoint)
    with engine.connect() as conn:
        files = sorted(
            r[0]
            for r in conn.execute(
                text("SELECT path FROM codeconv.dart_files")
            ).all()
        )
    return {
        "ok": True,
        "exit_code": _EXIT_OK,
        "files": files,
        "count": len(files),
    }


def run_inspect(
    *,
    repo_root: Path,
    exclusions: bool = False,
    current_phase: bool = False,
    data_dir: Optional[Path] = None,
    quiet: bool = False,
) -> dict:
    """Workspace introspection (read-only)."""
    repo_root = Path(repo_root).resolve()
    endpoint = acquire_or_discover(
        repo_root, ready_timeout=60.0, data_dir=data_dir
    )
    engine = build_engine(endpoint)
    out: dict[str, Any] = {"ok": True, "exit_code": _EXIT_OK}
    with engine.connect() as conn:
        out["settings"] = _read_settings(engine)
        if exclusions or not current_phase:
            out["exclusions"] = [
                {"path": r[0], "kind": r[1]}
                for r in conn.execute(
                    text(
                        "SELECT path, kind FROM codeconv.excluded_directories "
                        "ORDER BY path"
                    )
                ).all()
            ]
        if current_phase or not exclusions:
            out["phases"] = [
                {"phase": r[0], "sequence": r[1], "status": r[2]}
                for r in conn.execute(
                    text(
                        "SELECT ps.phase, ps.sequence, st.status "
                        "FROM codeconv.phase_sequence ps "
                        "LEFT JOIN codeconv.phase_status st "
                        "  ON st.phase = ps.phase "
                        "ORDER BY ps.sequence"
                    )
                ).all()
            ]
    return out


__all__ = [
    "register",
    "run_add_exclude",
    "run_init",
    "run_inspect",
    "run_list",
    "run_remove_exclude",
]
