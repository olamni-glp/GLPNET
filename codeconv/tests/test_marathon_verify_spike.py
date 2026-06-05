"""US4 / FR-011 — verification spike: cached-prefix resume + budget
observability, recorded durably (SC-008).

Runs against the fallback store (pure Python, no bridge): the spike is a
self-contained reference harness, so SC-008 is verifiable with no PGLite and
no model-in-the-loop. The production orchestration composes the real Workflow
tool at the skill layer (workflow-composition.md).
"""

from __future__ import annotations

from .conftest import make_marathon


def test_verify_spike_cached_prefix_budget_and_trace(marathon_fallback_store) -> None:
    store = marathon_fallback_store
    m, _b = make_marathon(store, slug="spikefeat", budget=100_000)

    from codeconv.marathon.verify_spike import run_spike

    res = run_spike(store, m.id, ceiling=100_000)

    # Cached-prefix resume: the unchanged prefix is cached and execution
    # resumes at the first changed step (US4-AS1).
    assert res["cached_prefix_ok"] is True
    assert res["first_reexecuted_step"] == 2  # plan -> plan_v2 changed at index 2

    # Budget observable throughout the run (US4-AS2).
    assert res["budget_observed_ok"] is True

    # SC-008: a verification_traces row subject=workflow-spike is recorded.
    assert res["recorded_trace_id"] is not None

    from codeconv.marathon.trace import list_traces

    traces = list_traces(store, m.id, subject="workflow-spike")
    assert len(traces) == 1
    t = traces[0]
    assert t["subject"] == "workflow-spike"
    assert t["decision"] == "accept"
    assert t["experiment_input"]["first_reexecuted_step"] == 2
    # The cascade: a later unchanged step still re-runs after the change.
    assert t["experiment_input"]["reexecuted"] == [2, 3]
    assert t["experiment_input"]["cached_prefix"] == [0, 1]


def test_verify_spike_trace_is_append_only(marathon_fallback_store) -> None:
    store = marathon_fallback_store
    m, _b = make_marathon(store, slug="spikefeat2", budget=100_000)

    from codeconv.marathon.trace import list_traces
    from codeconv.marathon.verify_spike import run_spike

    run_spike(store, m.id, ceiling=100_000)
    run_spike(store, m.id, ceiling=100_000)

    traces = list_traces(store, m.id, subject="workflow-spike")
    assert len(traces) == 2
    # earlier iteration never overwritten; ordered (subject, refine_seq) (US7-AS2)
    assert [t["refine_seq"] for t in traces] == [1, 2]
