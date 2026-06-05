"""Preauthorized commit + push per logical block (FR-014/015, D10, SC-010).

Under the standing commit/push grant the harness, at block completion, stages
**exactly** the block's files (never ``git add -A``), commits **without**
bypassing git hooks (never ``--no-verify``), and pushes **without** forcing
(never ``--force``/``--force-with-lease``). A non-fast-forward / rejected push
writes a ``push_blocked`` escalation and stops — it never forces (SC-010).

Outcomes are recorded to ``marathon.git_blocks`` (+ JSON mirror
``git/<block>.json``).
"""

from __future__ import annotations

import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Optional


class CommitPreauthMissing(RuntimeError):
    """Raised when commit/push is attempted without the standing grant #1
    (or after it was revoked) — FR-014/D10."""


def _git(repo_root: Any, *args: str) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["git", "-C", str(repo_root), *args],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )


def _commit_push_granted(marathon: Any) -> bool:
    return (
        marathon is not None
        and bool(getattr(marathon, "preauth_commit_push", False))
        and getattr(marathon, "preauth_revoked_at", None) is None
    )


def commit_block(
    store: Any,
    block_id: str,
    *,
    files: list[str],
    message: str,
    repo_root: Any,
    marathon: Any = None,
) -> dict[str, Any]:
    """Stage exactly ``files`` (no sweeping) and commit (hooks run). Requires
    the standing commit/push grant. Records a ``git_blocks`` row."""
    from .store import marathon_id_of_block

    mid = marathon_id_of_block(block_id)
    m = marathon if marathon is not None else store.read_marathon(mid)
    if not _commit_push_granted(m):
        raise CommitPreauthMissing(
            f"commit/push not preauthorized for marathon {mid!r} "
            f"(grant at `marathon start --preauth-commit-push`)"
        )
    if not files:
        raise ValueError("commit_block requires at least one file (no sweeping)")

    add = _git(repo_root, "add", "--", *files)  # ONLY the block's files
    if add.returncode != 0:
        raise RuntimeError(f"git add failed: {add.stderr.strip()}")
    commit = _git(repo_root, "commit", "-m", message)  # hooks run (no --no-verify)
    if commit.returncode != 0:
        raise RuntimeError(
            f"git commit failed: {(commit.stderr or commit.stdout).strip()}"
        )
    sha = _git(repo_root, "rev-parse", "HEAD").stdout.strip()
    _record_git_block(
        store, block_id, commit_sha=sha, staged_files=list(files), pushed=False
    )
    return {"commit_sha": sha, "staged_files": list(files)}


def push_block(
    store: Any,
    block_id: str,
    *,
    repo_root: Any,
    remote: str = "origin",
    branch: Optional[str] = None,
) -> dict[str, Any]:
    """Push the current branch (never ``--force``). A non-fast-forward /
    rejected push → ``push_blocked`` escalation + stop (FR-015/SC-010)."""
    from .escalation import write_escalation
    from .store import marathon_id_of_block

    if branch is None:
        branch = _git(repo_root, "rev-parse", "--abbrev-ref", "HEAD").stdout.strip()

    push = _git(repo_root, "push", remote, branch)  # NEVER --force
    if push.returncode == 0:
        _update_git_block_push(store, block_id, pushed=True, escalation=None)
        return {"pushed": True, "escalation": None}

    detail = {
        "remote": remote,
        "branch": branch,
        "stderr": push.stderr.strip()[:2000],
    }
    eid = write_escalation(
        store,
        marathon_id_of_block(block_id),
        kind="push_blocked",
        detail=detail,
        block_id=block_id,
    )
    _update_git_block_push(store, block_id, pushed=False, escalation="push_blocked")
    return {"pushed": False, "escalation": "push_blocked", "escalation_id": eid}


# --- git_blocks record (primary + JSON mirror) -----------------------------


def _record_git_block(
    store: Any,
    block_id: str,
    *,
    commit_sha: Optional[str],
    staged_files: list[str],
    pushed: bool,
    escalation: Optional[str] = None,
) -> None:
    from .store import _atomic_write_json, _safe_filename, marathon_id_of_block

    created = None
    engine = store._primary()
    if engine is not None:
        from sqlalchemy import text

        with engine.begin() as conn:
            row = conn.execute(
                text(
                    "INSERT INTO marathon.git_blocks "
                    "(block_id, commit_sha, staged_files, pushed, escalation) "
                    "VALUES (:bid, :sha, CAST(:files AS jsonb), :pushed, :esc) "
                    "RETURNING created_at"
                ),
                {
                    "bid": block_id,
                    "sha": commit_sha,
                    "files": json.dumps(staged_files),
                    "pushed": pushed,
                    "esc": escalation,
                },
            ).one()
            created = row.created_at
    if created is None:
        created = datetime.now(timezone.utc)
    mid = marathon_id_of_block(block_id)
    _atomic_write_json(
        store._marathon_dir(mid) / "git" / f"{_safe_filename(block_id)}.json",
        {
            "block_id": block_id,
            "commit_sha": commit_sha,
            "staged_files": staged_files,
            "pushed": pushed,
            "escalation": escalation,
            "created_at": created.isoformat(),
        },
    )


def _update_git_block_push(
    store: Any, block_id: str, *, pushed: bool, escalation: Optional[str]
) -> None:
    from .store import _atomic_write_json, _safe_filename, marathon_id_of_block

    engine = store._primary()
    if engine is not None:
        from sqlalchemy import text

        with engine.begin() as conn:
            conn.execute(
                text(
                    "UPDATE marathon.git_blocks SET pushed = :pushed, "
                    "escalation = :esc WHERE block_id = :bid"
                ),
                {"pushed": pushed, "esc": escalation, "bid": block_id},
            )
    mid = marathon_id_of_block(block_id)
    path = store._marathon_dir(mid) / "git" / f"{_safe_filename(block_id)}.json"
    if path.is_file():
        try:
            d = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return
        d["pushed"] = pushed
        d["escalation"] = escalation
        _atomic_write_json(path, d)


def read_git_block(store: Any, block_id: str) -> Optional[dict[str, Any]]:
    from .store import _safe_filename, marathon_id_of_block

    engine = store._primary()
    if engine is not None:
        from sqlalchemy import text

        with engine.connect() as conn:
            row = conn.execute(
                text(
                    "SELECT commit_sha, staged_files, pushed, escalation, created_at "
                    "FROM marathon.git_blocks WHERE block_id = :bid ORDER BY id DESC "
                    "LIMIT 1"
                ),
                {"bid": block_id},
            ).one_or_none()
        if row is not None:
            return {
                "block_id": block_id,
                "commit_sha": row.commit_sha,
                "staged_files": row.staged_files,
                "pushed": row.pushed,
                "escalation": row.escalation,
                "created_at": row.created_at.isoformat() if row.created_at else None,
            }
    mid = marathon_id_of_block(block_id)
    path = store._marathon_dir(mid) / "git" / f"{_safe_filename(block_id)}.json"
    if path.is_file():
        try:
            return json.loads(path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return None
    return None


__all__ = [
    "CommitPreauthMissing",
    "commit_block",
    "push_block",
    "read_git_block",
]
