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


def test_record_live_spike_accepts_real_observation(marathon_fallback_store) -> None:
    """FR-011/SC-008 against the REAL tool: a live observation with a contiguous
    cached prefix + cascade re-run + monotonic spend is accepted and recorded as
    a ``workflow-spike-live`` trace carrying the real runIds."""
    store = marathon_fallback_store
    m, _b = make_marathon(store, slug="livespike", budget=10_000_000)

    from codeconv.marathon.trace import list_traces
    from codeconv.marathon.verify_spike import record_live_spike

    res = record_live_spike(
        store,
        m.id,
        run_a_id="wf_a123",
        run_b_id="wf_b456",
        steps_a=["scaffold", "analyze", "plan", "emit"],
        steps_b=["scaffold", "analyze", "plan_v2", "emit"],
        cached_prefix=[0, 1],
        reexecuted=[2, 3],  # cascade: emit re-runs after the change at 2
        budget_trace=[(2, 4_001_000), (3, 4_002_000)],  # cumulative, monotonic
    )
    assert res["live"] is True
    assert res["cached_prefix_ok"] is True
    assert res["budget_observed_ok"] is True
    assert res["first_reexecuted_step"] == 2
    assert res["recorded_trace_id"] is not None

    traces = list_traces(store, m.id, subject="workflow-spike-live")
    assert len(traces) == 1
    t = traces[0]
    assert t["decision"] == "accept"
    assert t["experiment_input"]["run_a_id"] == "wf_a123"
    assert t["experiment_input"]["run_b_id"] == "wf_b456"
    assert t["experiment_input"]["cached_prefix"] == [0, 1]
    assert t["experiment_input"]["reexecuted"] == [2, 3]


def test_record_live_spike_rejects_non_contiguous_prefix(
    marathon_fallback_store,
) -> None:
    """A broken observation (a cached step AFTER the first re-run — no real
    cascade) is rejected, not silently accepted."""
    store = marathon_fallback_store
    m, _b = make_marathon(store, slug="livespikebad", budget=10_000_000)

    from codeconv.marathon.verify_spike import record_live_spike

    res = record_live_spike(
        store,
        m.id,
        run_a_id="wf_a",
        run_b_id="wf_b",
        steps_a=["a", "b", "c", "d"],
        steps_b=["a", "b2", "c", "d"],
        cached_prefix=[0, 2],  # step 2 cached AFTER change at 1 → impossible cascade
        reexecuted=[1, 3],
        budget_trace=[(1, 1_000), (3, 2_000)],
    )
    assert res["cached_prefix_ok"] is False
    assert res["budget_observed_ok"] is True
    # decision recorded as reject (still durable — the verification FAILED).
    from codeconv.marathon.trace import list_traces

    assert list_traces(store, m.id, subject="workflow-spike-live")[0]["decision"] == (
        "reject"
    )
