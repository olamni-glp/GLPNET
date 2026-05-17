"""``codeconv scaffold`` workflow — Feature 016 / US2.

Source of truth: ``specs/016-codeconv-init-scaffold-langpair/contracts/codeconv_scaffold_cli.md``
behaviour 1–9 (spec FR-013..FR-018, D4/D6).

Produces the target source tree (the *skeleton* — conversion is a future
stage; the planned target files carry the source bytes verbatim, D2NET
parity). Reads the in-scope set from the ``codeconv`` inventory minus
``codeconv.excluded_directories`` (NOT a ``public.dart_files``), stages
the planned tree under ``<target>.codeconv-scaffold-tmp/``, atomically
moves it into place (no half-written tree on failure — FR-017), records
each produced target rel-path into the **existing feature-015 tombstone
``target_path``** via ``codeconv.tools.depgraph.tombstone_writer`` (a
missing tombstone is a WARNING, not a failure — FR-015), and advances
``codeconv.phase_status['scaffold']``. No new ``scaffold_tracker`` table,
no ``public.*`` object, no new tombstone key (FR-020 / D4).
"""

from __future__ import annotations

import shutil
from pathlib import Path
from typing import Any, Optional

from sqlalchemy import text

from codeconv.bridge_client import acquire_or_discover
from codeconv.db.engine import build_engine

from .planner import plan_target_tree


# Exit codes — contract § Exit codes.
_EXIT_OK = 0
_EXIT_GENERIC = 1
_EXIT_PREREQ = 2
_EXIT_PAIR = 5

# Staging dir suffix (contract step 6 / data-model §5). Sibling of the
# target root; atomically moved into place; never committed.
_STAGING_SUFFIX = ".codeconv-scaffold-tmp"
_OLD_SUFFIX = ".codeconv-scaffold-old"

# Empty sentinel file written at the target-tree root so a subsequent
# scaffold can tell a target IT produced (managed → idempotent refresh,
# no --force-delete-target needed) from a non-empty target holding
# foreign/operator content (unmanaged → refuse without the destructive
# flag). D2Net.Scaffold parity: ``StagingMutator.SentinelFileName``
# (``.d2net-scaffold-tracker``). This is the ONLY reading that makes the
# scaffold contract self-consistent — its "non-empty ⇒ exit 2" (step 6)
# and "idempotent no-op / identical target tree" (Exit codes / SC-002)
# clauses otherwise contradict (a re-scaffold's target is always
# non-empty). Recorded as a spec ambiguity for the contract owner.
_SENTINEL = ".codeconv-scaffold-manifest"


def register(dbos_app: Any) -> None:
    """Feature-018 T015: delegate to centralised durable activation.

    Was a feature-012 no-op stub. Now delegates to
    :func:`codeconv.durable.activate` (idempotent). Wires the
    builder-side durable layer ONLY; ``run_scaffold`` is unchanged
    (D2 hard gate) — the staged-write + atomic-move on-disk crash-safety
    is untouched.
    """
    from codeconv.durable import activate

    activate(dbos_app)


def _dir_nonempty(p: Path) -> bool:
    return p.is_dir() and any(p.iterdir())


def _is_managed_target(target_root: Path) -> bool:
    """True iff ``target_root`` was produced by a prior scaffold of this
    workspace (the sentinel marker is present at its root).

    A managed target is refreshed idempotently WITHOUT requiring
    ``--force-delete-target`` (FR-017 / SC-002 — "identical target tree",
    "idempotent no-op"). A non-empty target WITHOUT the sentinel holds
    foreign/operator content and is refused without the destructive flag
    (contract step 6). D2Net.Scaffold ``TargetTreePlanner.TargetIsManaged``
    parity.
    """
    return (target_root / _SENTINEL).is_file()


def _atomic_move(staging: Path, live: Path, *, replace: bool) -> None:
    """Atomically move ``staging`` over ``live`` (D2Net.StagingMutator
    parity — 3-step compensating rename when a live target exists)."""
    old = live.parent / (live.name + _OLD_SUFFIX)
    if old.exists():
        shutil.rmtree(old, ignore_errors=True)

    if live.exists():
        if not replace:
            # Caller must have gated this; defensive.
            raise RuntimeError(
                f"refusing to overwrite non-empty target {live}"
            )
        # Step 1: live -> old
        live.replace(old)
        try:
            # Step 2: staging -> live
            staging.replace(live)
        except OSError:
            # Compensate: restore the live target.
            if not live.exists() and old.exists():
                old.replace(live)
            raise
        # Step 3: drop the old subtree.
        shutil.rmtree(old, ignore_errors=True)
    else:
        live.parent.mkdir(parents=True, exist_ok=True)
        staging.replace(live)


def run_scaffold(
    *,
    repo_root: Path,
    source_lang: Optional[str] = None,
    target_lang: Optional[str] = None,
    force_delete_target: bool = False,
    no_tombstone_update: bool = False,
    data_dir: Optional[Path] = None,
    quiet: bool = False,
) -> dict:
    """Mirror the in-scope source tree into the target (contract 1–9)."""
    repo_root = Path(repo_root).resolve()

    # 1. Acquire-or-discover the shared bridge (FR-019).
    endpoint = acquire_or_discover(
        repo_root, ready_timeout=60.0, data_dir=data_dir
    )
    engine = build_engine(endpoint)

    # 2. Resolve + enforce the workspace pair (FR-004/FR-018/SC-008).
    #    REQUIRES an initialised workspace. Unset → exit 2; an explicit
    #    override disagreeing with the workspace pair → exit 5. NO output
    #    produced in either case.
    from codeconv import langpairs

    override = None
    if source_lang is not None and target_lang is not None:
        override = (source_lang, target_lang)
    try:
        pair = langpairs.resolve_workspace_pair(
            engine, require_workspace=True, override=override
        )
    except langpairs.PairMismatch as exc:
        return {
            "ok": False,
            "exit_code": _EXIT_PAIR,
            "error": str(exc),
        }
    except langpairs.UnknownLangPair as exc:
        # Unset workspace pair OR a workspace pair that is not registered.
        s = _read_settings(engine)
        unset = not (s.get("source_lang") and s.get("target_lang"))
        return {
            "ok": False,
            "exit_code": _EXIT_PREREQ if unset else _EXIT_PAIR,
            "error": (
                "no initialised workspace; run `codeconv init` first"
                if unset
                else str(exc)
            ),
        }

    settings = _read_settings(engine)
    target_rel_root = settings.get("target_path")
    if not target_rel_root:
        return {
            "ok": False,
            "exit_code": _EXIT_PREREQ,
            "error": "workspace has no target_path; run `codeconv init` first",
        }

    # 3. Prerequisite: a non-empty inventory must exist (FR-018).
    with engine.connect() as conn:
        inv_count = int(
            conn.execute(
                text("SELECT COUNT(*) FROM codeconv.dart_files")
            ).scalar()
            or 0
        )
    if inv_count == 0:
        return {
            "ok": False,
            "exit_code": _EXIT_PREREQ,
            "error": (
                "inventory is empty (codeconv.dart_files) — "
                "run `codeconv init` first"
            ),
        }

    # 4–5. Plan the target tree from the inventory minus exclusions, via
    #      the pair's target hooks.
    plan = plan_target_tree(engine, pair)
    if not plan:
        return {
            "ok": False,
            "exit_code": _EXIT_PREREQ,
            "error": (
                "no in-scope source files to scaffold "
                "(all excluded or inventory empty)"
            ),
        }

    source_rel_root = settings.get("source_path") or "glp_runtime_net"
    source_root = (repo_root / source_rel_root).resolve()
    target_root = (repo_root / target_rel_root).resolve()
    staging = target_root.parent / (target_root.name + _STAGING_SUFFIX)

    # 6. Destructive-overwrite gate (contract step 6, reconciled with
    #    FR-017/SC-002): a non-empty live target holding foreign/operator
    #    content (NO scaffold sentinel) without --force-delete-target is
    #    refused; the live tree is left untouched (no staging side effects
    #    yet). A target a prior scaffold of this workspace produced
    #    (sentinel present) is "managed" and refreshed idempotently
    #    without the destructive flag — otherwise every re-scaffold (whose
    #    target is always non-empty) would be refused, contradicting the
    #    contract's "idempotent no-op / identical target tree".
    target_managed = _is_managed_target(target_root)
    if (
        _dir_nonempty(target_root)
        and not target_managed
        and not force_delete_target
    ):
        return {
            "ok": False,
            "exit_code": _EXIT_PREREQ,
            "error": (
                f"target {target_rel_root!r} is non-empty and was not "
                f"produced by codeconv scaffold (no scaffold manifest). "
                f"Re-run with --force-delete-target to overwrite it (the "
                f"/codeconv-scaffold skill drives this confirmation)."
            ),
        }

    # 6 (cont). Stage the planned tree under <target>.codeconv-scaffold-tmp/.
    if staging.exists():
        shutil.rmtree(staging, ignore_errors=True)
    staging.mkdir(parents=True, exist_ok=True)

    files_written = 0
    workdirs_created = 0
    try:
        for pf in plan:
            src_abs = source_root / pf.source_rel
            dst_abs = staging / pf.target_rel
            dst_abs.parent.mkdir(parents=True, exist_ok=True)
            # Skeleton: copy the source bytes to the target-ext path
            # (conversion is a future stage — contract § Does NOT).
            if src_abs.is_file():
                shutil.copyfile(src_abs, dst_abs)
            else:
                # Inventory row without a live source file: still create
                # an empty placeholder so the target tree mirrors the
                # recorded inventory (discover owns source truth).
                dst_abs.write_text("", encoding="utf-8")
            files_written += 1
            if pf.workdir_rel:
                (staging / pf.workdir_rel).mkdir(
                    parents=True, exist_ok=True
                )
                workdirs_created += 1

        # Sentinel at the staged tree root so the produced target is
        # recognised as scaffold-managed on the next run (idempotent
        # refresh without --force-delete-target).
        (staging / _SENTINEL).write_text("", encoding="utf-8")

        # 6 (cont). Atomic move staging → target (no half-written tree on
        # failure). The live target is replaced when it is non-empty
        # (managed-refresh OR --force-delete-target — both already gated
        # above; an unmanaged non-empty target without force returned
        # earlier).
        _atomic_move(
            staging, target_root, replace=_dir_nonempty(target_root)
        )
    finally:
        # A failure before/at the move leaves the live target untouched;
        # always discard the staging dir.
        if staging.exists():
            shutil.rmtree(staging, ignore_errors=True)

    # 7. Record the produced target rel-path into the feature-015
    #    tombstone ``target_path`` (canonical writer; missing → warn).
    tombstones_root = repo_root / ".codeconv" / "tombstones"
    tombstones_missing = 0
    tombstones_written = 0
    if not no_tombstone_update:
        from codeconv.tools.depgraph.tombstone_writer import (
            write_tombstone_with_extras,
        )

        for pf in plan:
            written = write_tombstone_with_extras(
                tombstones_root,
                pf.source_rel,
                {"target_path": pf.target_rel},
            )
            if written is None:
                tombstones_missing += 1
            else:
                tombstones_written += 1

    # 8. Advance phase_status['scaffold'] and ensure phase_sequence has
    #    'scaffold'. One transaction for the DB mutations.
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.phase_sequence (phase, sequence) "
                "VALUES ('scaffold', 2) "
                "ON CONFLICT (phase) DO NOTHING"
            )
        )
        conn.execute(
            text(
                "INSERT INTO codeconv.phase_status (phase, status, last_updated) "
                "VALUES ('scaffold', 'COMPLETE', NOW()) "
                "ON CONFLICT (phase) DO UPDATE SET "
                "  status = 'COMPLETE', last_updated = NOW()"
            )
        )

    # 9. Summary.
    return {
        "ok": True,
        "exit_code": _EXIT_OK,
        "source_path": source_rel_root,
        "target_path": target_rel_root,
        "pair": list(pair.key()),
        "files_written": files_written,
        "workdirs_created": workdirs_created,
        "tombstones_written": tombstones_written,
        "tombstones_missing": tombstones_missing,
        "phase": "scaffold",
        "phase_status": "COMPLETE",
        "forced": bool(force_delete_target),
    }


def _read_settings(engine: Any) -> dict[str, str]:
    with engine.connect() as conn:
        rows = conn.execute(
            text("SELECT key, value FROM codeconv.workspace_settings")
        ).all()
    return {k: v for k, v in rows}


__all__ = ["register", "run_scaffold"]
