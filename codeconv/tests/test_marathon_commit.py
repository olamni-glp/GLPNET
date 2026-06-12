"""US4 scoped-commit boundary — staged paths only, non-FF, re-drive (T033).

Per ``contracts/checkpoint-commit.md``: a checkpoint stages ONLY its named
paths (no ``git add -A``, no force, no ``--no-verify``); a non-fast-forward
push writes ``push_blocked`` and does not force; a complete checkpoint with
``commit_sha IS NULL`` is re-driven on resume before new work (FR-017/018,
SC-007).
"""

from __future__ import annotations

import subprocess

from .conftest import needs_bridge

RUN_ID = "us4-commit"


def _git(repo, *args):
    return subprocess.run(
        ["git", "-C", str(repo), *args],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )


def _make_work_repo_with_origin(tmp_path):
    """A scratch work repo with a bare origin, ready to commit and push."""
    origin = tmp_path / "origin.git"
    work = tmp_path / "work"
    subprocess.run(["git", "init", "--bare", str(origin)], capture_output=True)
    subprocess.run(["git", "init", str(work)], capture_output=True)
    for k, v in (
        ("user.email", "test@glpnet"),
        ("user.name", "glpnet-test"),
        ("commit.gpgsign", "false"),
    ):
        _git(work, "config", k, v)
    (work / "README.md").write_text("seed\n", encoding="utf-8")
    _git(work, "add", "--", "README.md")
    _git(work, "commit", "-m", "seed")
    _git(work, "remote", "add", "origin", str(origin))
    branch = _git(work, "rev-parse", "--abbrev-ref", "HEAD").stdout.strip()
    _git(work, "push", "origin", branch)
    return work, origin, branch


@needs_bridge
def test_scoped_commit_nonff_push_and_redrive(marathon_store, tmp_path):
    from codeconv.marathon.checkpoint import checkpoint, start_stage
    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.models import CheckpointRow
    from codeconv.marathon.position import resume_position
    from codeconv.marathon.stages import register_run
    from codeconv.marathon.store import Repository

    work, origin, branch = _make_work_repo_with_origin(tmp_path)
    env = resolve_env(RUN_ID, data_dir=marathon_store, repo_dir=work)
    repo = Repository(env)

    register_run(RUN_ID, stages=["a", "b", "c"], env=env)
    repo.update_run(RUN_ID, preauth_commit_push=True)  # standing grant #1
    run = repo.get_run(RUN_ID)

    # --- scoped commit: ONLY the named paths are staged (SC-007).
    (work / "a.txt").write_text("block a\n", encoding="utf-8")
    (work / "unrelated.txt").write_text("not mine\n", encoding="utf-8")
    start_stage(RUN_ID, "a", env=env)
    cp = checkpoint(
        RUN_ID, "a", committed_paths=["a.txt"], message="a: scoped", env=env
    )
    assert cp.commit_sha, "scoped commit must record the sha on the checkpoint"
    assert cp.pushed is True
    shown = _git(work, "show", "--name-only", "--format=", cp.commit_sha).stdout
    assert "a.txt" in shown and "unrelated.txt" not in shown
    status = _git(work, "status", "--porcelain").stdout
    assert "unrelated.txt" in status  # never swept (no `git add -A`)

    # --- non-FF push: escalate push_blocked; NEVER force (FR-017).
    clone2 = tmp_path / "clone2"
    subprocess.run(
        ["git", "clone", str(origin), str(clone2)], capture_output=True
    )
    for k, v in (("user.email", "x@y"), ("user.name", "other"),
                 ("commit.gpgsign", "false")):
        _git(clone2, "config", k, v)
    (clone2 / "theirs.txt").write_text("remote moved\n", encoding="utf-8")
    _git(clone2, "add", "--", "theirs.txt")
    _git(clone2, "commit", "-m", "remote advance")
    _git(clone2, "push", "origin", branch)
    their_sha = _git(clone2, "rev-parse", "HEAD").stdout.strip()

    (work / "b.txt").write_text("block b\n", encoding="utf-8")
    start_stage(RUN_ID, "b", env=env)
    cp_b = checkpoint(
        RUN_ID, "b", committed_paths=["b.txt"], message="b: scoped", env=env
    )
    assert cp_b.commit_sha  # commit landed locally
    assert cp_b.pushed is False
    assert cp_b.push_escalation == "push_blocked"
    kinds = [e.kind for e in repo.list_escalations(RUN_ID, open_only=True)]
    assert "push_blocked" in kinds
    # The remote was NOT forced over: its advance is still the remote tip.
    remote_tip = _git(work, "ls-remote", "origin", branch).stdout.split()[0]
    assert remote_tip == their_sha

    # --- re-drive guard (FR-018): a complete checkpoint with named paths but
    # commit_sha NULL is named FIRST on resume, before any new work.
    stage_c = repo.get_stage(RUN_ID, "c")
    repo.update_stage(stage_c.id, status="complete")
    repo.insert_checkpoint(
        CheckpointRow(
            run_id=RUN_ID, stage_id=stage_c.id, sequence_no=0,
            committed_paths=["c.txt"],
        )
    )
    pos = resume_position(RUN_ID, env=env)
    assert pos.next_action == "re-drive scoped commit for c"

    # redrive_commit drives exactly that one boundary (push will stay blocked
    # behind the same non-FF remote — that's the push path, already asserted).
    (work / "c.txt").write_text("block c\n", encoding="utf-8")
    from codeconv.marathon.gitblock import redrive_commit

    cp_c = redrive_commit(RUN_ID, "c", env=env, push=False)
    assert cp_c.commit_sha
    assert resume_position(RUN_ID, env=env).next_action != (
        "re-drive scoped commit for c"
    )
