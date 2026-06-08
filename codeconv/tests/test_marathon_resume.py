"""US1 — restart-safe resume with no context loss (SC-001/002, I3/I4/I8).

Pure fallback store: write checkpoints, drop the store object (== process
restart), construct a fresh store over the same dir, and assert the resume
position is recovered from durable state — never a summary.
"""

from __future__ import annotations

from .conftest import make_marathon


def test_resume_returns_max_seq_after_restart(marathon_fallback_store) -> None:
    """SC-001 / I3 / I8: after N checkpoints + a restart, resume returns the
    Nth checkpoint, derived from durable state (not a conversation summary)."""
    store = marathon_fallback_store
    m, b = make_marathon(store, slug="resumefeat")
    units = ["t1", "t2", "t3", "t4", "t5"]
    for i in range(1, 4):  # complete t1..t3 over 3 checkpoints
        store.write_checkpoint(
            b.id,
            stage="plan",
            wip_unit=units[i],
            completed_units=units[:i],
            remaining_units=units[i:],
            workflow_run_id="wf_run_x",
            budget_spent=i * 10,
        )

    # Drop + restart: a brand-new store object over the same repo dir reads
    # only the JSON durable state.
    from codeconv.marathon.checkpoint import resume
    from codeconv.marathon.store import MarathonStore

    restarted = MarathonStore(store.repo_root, fallback_only=True)
    rep = resume(restarted, m.id)

    assert rep.found is True
    assert rep.sequence_no == 3
    assert rep.completed_units == ["t1", "t2", "t3"]
    assert rep.remaining_units == ["t4", "t5"]
    assert rep.wip_unit == "t4"
    assert rep.workflow_run_id == "wf_run_x"
    assert rep.store_origin == "fallback"
    assert rep.diverged is False


def test_resume_skips_completed_units(marathon_fallback_store) -> None:
    """SC-002 / I4: completed units are never re-executed on resume."""
    store = marathon_fallback_store
    m, b = make_marathon(store, slug="skipfeat")
    all_units = ["a", "b", "c", "d"]
    store.write_checkpoint(
        b.id,
        stage="plan",
        wip_unit="c",
        completed_units=["a", "b"],
        remaining_units=["c", "d"],
        workflow_run_id=None,
        budget_spent=5,
    )

    from codeconv.marathon.checkpoint import resume, units_to_execute

    rep = resume(store, m.id)
    to_run = units_to_execute(all_units, rep.completed_units)
    assert to_run == ["c", "d"]
    assert "a" not in to_run and "b" not in to_run


def test_resume_cold_start_is_not_found(marathon_fallback_store) -> None:
    """No checkpoints yet → resume reports a cold start (found=False)."""
    store = marathon_fallback_store
    m, _b = make_marathon(store, slug="coldfeat")

    from codeconv.marathon.checkpoint import resume

    assert resume(store, m.id).found is False


def test_resume_flags_commit_push_pending_on_crash(marathon_fallback_store) -> None:
    """FR-014/SC-010 crash window: a completed block (final checkpoint, no
    remaining) whose commit/push never landed is flagged ``commit_push_pending``
    so the skill re-drives commit+push on resume instead of skipping it."""
    store = marathon_fallback_store
    m, b = make_marathon(store, slug="cppend")
    store.write_checkpoint(
        b.id,
        stage="plan",
        wip_unit=None,
        completed_units=["u1", "u2"],
        remaining_units=[],
        workflow_run_id="wf_x",
        budget_spent=2,
    )

    from codeconv.marathon.checkpoint import resume

    rep = resume(store, m.id)
    assert rep.block_complete is True
    assert rep.commit_push_pending is True  # no git_blocks record → crash pre-commit


def test_resume_no_pending_after_successful_push(marathon_fallback_store) -> None:
    """A completed block whose commit was pushed is NOT flagged pending."""
    store = marathon_fallback_store
    m, b = make_marathon(store, slug="cpdone")
    store.write_checkpoint(
        b.id,
        stage="plan",
        wip_unit=None,
        completed_units=["u1"],
        remaining_units=[],
        workflow_run_id="wf_y",
        budget_spent=1,
    )
    # Record a committed + pushed git block for this block (JSON mirror).
    from codeconv.marathon.gitblock import _record_git_block, _update_git_block_push

    _record_git_block(
        store, b.id, commit_sha="abc123", staged_files=["f.py"], pushed=False
    )
    _update_git_block_push(store, b.id, pushed=True, escalation=None)

    from codeconv.marathon.checkpoint import resume

    rep = resume(store, m.id)
    assert rep.block_complete is True
    assert rep.commit_push_pending is False
