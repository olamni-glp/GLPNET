"""US5 verification-trace substrate — append-only, ordered (T043, FR-023).

``write_trace`` never overwrites (UNIQUE ``(run, subject, refine_seq)``);
``list_traces`` is ordered by ``(subject, refine_seq)``.
"""

from __future__ import annotations

import pytest

from .conftest import needs_bridge

RUN_ID = "us5-trace"


@needs_bridge
def test_trace_append_only_and_ordered(marathon_store):
    import sqlalchemy.exc

    from codeconv.marathon.env import resolve_env
    from codeconv.marathon.stages import register_run
    from codeconv.marathon.store import Repository
    from codeconv.marathon.trace import list_traces, write_trace

    env = resolve_env(RUN_ID, data_dir=marathon_store)
    repo = Repository(env)
    register_run(RUN_ID, stages=["a"], env=env)

    t1 = write_trace(
        repo, RUN_ID, subject="design", experiment_input={"k": 1},
        metric_score=0.4, decision="reject",
    )
    t2 = write_trace(
        repo, RUN_ID, subject="design", experiment_input={"k": 2},
        metric_score=0.9, decision="accept",
    )
    t_other = write_trace(
        repo, RUN_ID, subject="api", experiment_input={"v": 1},
    )
    assert (t1.refine_seq, t2.refine_seq) == (1, 2)  # auto-monotonic
    assert t_other.refine_seq == 1  # per-subject sequence

    # Never overwrites: an explicit duplicate (run, subject, refine_seq) is
    # refused by the substrate UNIQUE constraint.
    with pytest.raises(sqlalchemy.exc.IntegrityError):
        write_trace(
            repo, RUN_ID, subject="design", experiment_input={"k": 99},
            refine_seq=1,
        )
    # The earlier iteration is intact.
    history = list_traces(repo, RUN_ID, subject="design")
    assert [t.refine_seq for t in history] == [1, 2]
    assert history[0].experiment_input == {"k": 1}
    assert history[0].decision == "reject"

    # Ordered by (subject, refine_seq) across subjects.
    all_traces = list_traces(repo, RUN_ID)
    assert [(t.subject, t.refine_seq) for t in all_traces] == [
        ("api", 1), ("design", 1), ("design", 2),
    ]

    # Dual-store: the mirror carries every trace.
    mirror = sorted(
        p.name for p in (marathon_store / "json" / "traces").glob("*.json")
    )
    assert mirror == ["api-1.json", "design-1.json", "design-2.json"]
