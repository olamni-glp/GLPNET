"""US6 — preauthorized commit + push per logical block (SC-010, FR-014/015).

Uses a real local git sandbox (no bridge, no network): a work repo + a bare
remote made divergent so the push is a genuine non-fast-forward. Asserts the
commit stages ONLY the block's files and that a blocked push escalates
(``push_blocked``) instead of forcing.
"""

from __future__ import annotations

import subprocess
from pathlib import Path

import pytest


def _git(repo: Path, *args: str) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["git", "-C", str(repo), *args], capture_output=True, text=True
    )


def _init_repo(path: Path) -> str:
    path.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        ["git", "init", "-b", "main", str(path)], capture_output=True, text=True
    )
    _git(path, "config", "user.email", "t@t")
    _git(path, "config", "user.name", "tester")
    _git(path, "config", "commit.gpgsign", "false")
    (path / "file0.txt").write_text("0\n", encoding="utf-8")
    _git(path, "add", "file0.txt")
    _git(path, "commit", "-m", "init")
    return _git(path, "rev-parse", "--abbrev-ref", "HEAD").stdout.strip()


def _make_marathon(store, slug: str, *, preauth: bool):
    from codeconv.marathon.cadence import block_id, block_kind_for_stage, canonical_stage
    from codeconv.marathon.models import Marathon, StageBlock
    from codeconv.marathon.store import marathon_id_for

    mid = marathon_id_for(slug)
    store.upsert_marathon(
        Marathon(
            id=mid,
            feature_slug=slug,
            feature_branch="main",
            preauth_commit_push=preauth,
        )
    )
    bid = block_id(mid, "plan", 1)
    store.upsert_block(
        StageBlock(
            id=bid,
            marathon_id=mid,
            stage=canonical_stage("plan"),
            block_kind=block_kind_for_stage("plan"),
            ordinal=1,
            status="running",
        )
    )
    return mid, bid


def test_commit_stages_only_block_files_and_push_escalates(tmp_path: Path) -> None:
    work = tmp_path / "work"
    branch = _init_repo(work)

    remote = tmp_path / "remote.git"
    # Default the bare remote to the SAME branch so a clone checks it out (else
    # the second clone lands on a different default branch and never diverges).
    subprocess.run(
        ["git", "init", "--bare", "-b", branch, str(remote)],
        capture_output=True,
        text=True,
    )
    _git(work, "remote", "add", "origin", str(remote))
    _git(work, "push", "-u", "origin", branch)

    # Make the remote ahead via a second clone → our later push is non-ff.
    other = tmp_path / "other"
    subprocess.run(["git", "clone", str(remote), str(other)], capture_output=True, text=True)
    _git(other, "config", "user.email", "o@o")
    _git(other, "config", "user.name", "other")
    (other / "upstream.txt").write_text("up\n", encoding="utf-8")
    _git(other, "add", "upstream.txt")
    _git(other, "commit", "-m", "upstream")
    push_up = _git(other, "push", "origin", branch)
    assert push_up.returncode == 0, f"setup push failed: {push_up.stderr}"

    from codeconv.marathon.store import MarathonStore

    store = MarathonStore(work, fallback_only=True)
    mid, bid = _make_marathon(store, "gitfeat", preauth=True)

    # Two files; only blockA belongs to this block.
    (work / "blockA.txt").write_text("A\n", encoding="utf-8")
    (work / "blockB.txt").write_text("B\n", encoding="utf-8")

    from codeconv.marathon.gitblock import commit_block, push_block

    res = commit_block(store, bid, files=["blockA.txt"], message="block A", repo_root=work)
    assert res["commit_sha"]

    # Only blockA was committed; blockB (and the marathon state dir) stay
    # untracked — no `git add -A` sweep (SC-010).
    status = _git(work, "status", "--porcelain").stdout
    assert "blockB.txt" in status
    assert "blockA.txt" not in status
    assert ".codeconv" in status

    # Push is a non-fast-forward → escalate, never force.
    pres = push_block(store, bid, repo_root=work, remote="origin", branch=branch)
    assert pres["pushed"] is False
    assert pres["escalation"] == "push_blocked"

    from codeconv.marathon.escalation import open_escalations

    assert any(e["kind"] == "push_blocked" for e in open_escalations(store, mid))

    # The remote's upstream commit is intact — we did NOT force-overwrite it.
    rlog = subprocess.run(
        ["git", "-C", str(remote), "log", "--oneline"], capture_output=True, text=True
    ).stdout
    assert "upstream" in rlog


def test_commit_without_preauth_raises(tmp_path: Path) -> None:
    work = tmp_path / "work"
    _init_repo(work)

    from codeconv.marathon.gitblock import CommitPreauthMissing, commit_block
    from codeconv.marathon.store import MarathonStore

    store = MarathonStore(work, fallback_only=True)
    _mid, bid = _make_marathon(store, "noauth", preauth=False)
    (work / "x.txt").write_text("x\n", encoding="utf-8")

    with pytest.raises(CommitPreauthMissing):
        commit_block(store, bid, files=["x.txt"], message="x", repo_root=work)
