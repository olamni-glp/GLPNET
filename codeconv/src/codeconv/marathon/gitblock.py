"""Scoped commit + push boundary, folded onto the checkpoint row (T035/T036, D9).

Ports 024's ``gitblock`` discipline onto the data-driven model
(``contracts/checkpoint-commit.md``): at block completion, under the run's
standing ``preauth_commit_push`` grant, stage **exactly** the checkpoint's
``committed_paths`` (``git add -- <paths>``; never ``git add -A``/``.``),
commit **without** bypassing hooks (never ``--no-verify``), record
``commit_sha`` on the **checkpoint** row, and push **without** forcing (never
``--force``/``--force-with-lease``). A non-fast-forward / rejected push writes
a ``push_blocked`` escalation and sets ``checkpoint.push_escalation`` — it
never retries with force (FR-017, SC-007).

Crash-window re-drive (FR-018): the durable order is checkpoint-then-commit,
so a crash in between leaves a complete checkpoint with ``commit_sha IS
NULL`` — the resume position names "re-drive scoped commit for <stage>" and
:func:`redrive_commit` re-drives exactly that one boundary. If the crash fell
*after* the git commit but *before* the sha record, the re-drive detects the
already-clean paths and records the existing HEAD instead of failing.
"""

from __future__ import annotations

import subprocess
from typing import Any, Optional

from codeconv.marathon.env import resolve_env
from codeconv.marathon.models import CheckpointRow, Escalation, MarathonEnv, MarathonRun
from codeconv.marathon.store import Repository

__all__ = [
    "CommitPreauthMissing",
    "commit_block",
    "commit_push_granted",
    "push_block",
    "redrive_commit",
]


class CommitPreauthMissing(RuntimeError):
    """Commit/push attempted without the standing grant (or after revocation)."""


def _git(repo_dir: Any, *args: str) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["git", "-C", str(repo_dir), *args],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )


def commit_push_granted(run: Optional[MarathonRun]) -> bool:
    return (
        run is not None
        and bool(run.preauth_commit_push)
        and run.preauth_revoked_at is None
    )


def commit_block(
    repo: Repository,
    run: MarathonRun,
    cp: CheckpointRow,
    *,
    message: str,
    repo_dir: Any,
) -> CheckpointRow:
    """Stage exactly ``cp.committed_paths`` and commit (hooks run); record the
    sha on the checkpoint row. Idempotent for the crash-after-commit window."""
    if not commit_push_granted(run):
        raise CommitPreauthMissing(
            f"commit/push not preauthorized for run {run.id!r} "
            f"(grant preauth_commit_push on the run)"
        )
    paths = list(cp.committed_paths or [])
    if not paths:
        raise ValueError("commit_block requires at least one path (no sweeping)")

    add = _git(repo_dir, "add", "--", *paths)  # ONLY the block's paths
    if add.returncode != 0:
        raise RuntimeError(f"git add failed: {add.stderr.strip()}")
    commit = _git(repo_dir, "commit", "-m", message)  # hooks run, no --no-verify
    if commit.returncode != 0:
        # Re-drive after a crash that fell past the commit: the paths are
        # already clean ⇒ the commit landed; record HEAD instead of failing.
        status = _git(repo_dir, "status", "--porcelain", "--", *paths)
        if status.returncode != 0 or status.stdout.strip():
            raise RuntimeError(
                f"git commit failed: {(commit.stderr or commit.stdout).strip()}"
            )
    sha = _git(repo_dir, "rev-parse", "HEAD").stdout.strip()
    if not sha:
        raise RuntimeError("git rev-parse HEAD returned no sha")
    return repo.update_checkpoint(cp.id, commit_sha=sha)


def push_block(
    repo: Repository,
    run_id: str,
    cp: CheckpointRow,
    *,
    repo_dir: Any,
    remote: str = "origin",
    branch: Optional[str] = None,
) -> CheckpointRow:
    """Push the current branch (never ``--force``). Non-FF / rejected →
    ``push_blocked`` escalation + ``push_escalation`` on the checkpoint."""
    if branch is None:
        branch = _git(repo_dir, "rev-parse", "--abbrev-ref", "HEAD").stdout.strip()

    push = _git(repo_dir, "push", remote, branch)  # NEVER --force
    if push.returncode == 0:
        return repo.update_checkpoint(cp.id, pushed=True)

    esc = repo.insert_escalation(
        Escalation(
            run_id=run_id,
            kind="push_blocked",
            stage_id=cp.stage_id,
            detail={
                "remote": remote,
                "branch": branch,
                "sequence_no": cp.sequence_no,
                "stderr": (push.stderr or push.stdout).strip()[:2000],
            },
        )
    )
    return repo.update_checkpoint(
        cp.id, pushed=False, push_escalation="push_blocked"
    )


def redrive_commit(
    run_id: str,
    stage_name: str,
    *,
    message: Optional[str] = None,
    env: Optional[MarathonEnv] = None,
    push: bool = True,
) -> CheckpointRow:
    """Re-drive the one pending scoped commit for a complete stage whose last
    checkpoint has ``commit_sha IS NULL`` (FR-018) — before any new work."""
    env = env if env is not None else resolve_env(run_id)
    repo = Repository(env)
    run = repo.get_run(run_id)
    if run is None:
        raise ValueError(f"unknown run '{run_id}'")
    stage = repo.get_stage(run_id, stage_name)
    if stage is None:
        raise ValueError(f"unknown stage '{stage_name}' in run '{run_id}'")
    cps = [c for c in repo.list_checkpoints(run_id) if c.stage_id == stage.id]
    if not cps:
        raise ValueError(f"stage '{stage_name}' has no checkpoint to re-drive")
    last = max(cps, key=lambda c: c.sequence_no)
    if last.commit_sha is not None:
        return last  # nothing pending — idempotent
    if not last.committed_paths:
        raise ValueError(
            f"stage '{stage_name}' last checkpoint named no paths; "
            f"nothing to re-drive"
        )
    msg = message or f"marathon {run_id}: re-drive scoped commit for {stage_name}"
    cp = commit_block(repo, run, last, message=msg, repo_dir=env.repo_dir)
    if push:
        cp = push_block(repo, run_id, cp, repo_dir=env.repo_dir)
    return cp
