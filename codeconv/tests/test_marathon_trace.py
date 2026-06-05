"""US7 — durable verification-trace substrate (US7-AS1/2/3).

Append-only; ordered by (subject, refine_seq); reconstructable by an external
reader with no harness internals. Fallback store — no bridge needed.
"""

from __future__ import annotations

from .conftest import make_marathon


def test_traces_persist_across_restart(marathon_fallback_store) -> None:
    """US7-AS1: records survive a restart (a fresh store over the same dir)."""
    store = marathon_fallback_store
    m, _b = make_marathon(store, slug="tracefeat")

    from codeconv.marathon.trace import list_traces, write_trace

    write_trace(
        store,
        m.id,
        subject="codegen",
        experiment_input={"variant": "A"},
        metric_score=0.7,
        decision="accept",
    )

    from codeconv.marathon.store import MarathonStore

    restarted = MarathonStore(store.repo_root, fallback_only=True)
    traces = list_traces(restarted, m.id, subject="codegen")
    assert len(traces) == 1
    assert traces[0]["decision"] == "accept"
    assert traces[0]["experiment_input"] == {"variant": "A"}


def test_iterations_preserve_order_no_overwrite(marathon_fallback_store) -> None:
    """US7-AS2: multiple iterations preserve (subject, refine_seq) order; an
    earlier iteration is never overwritten."""
    store = marathon_fallback_store
    m, _b = make_marathon(store, slug="traceiter")

    from codeconv.marathon.trace import list_traces, write_trace

    for i, variant in enumerate(["A", "B", "C"], start=1):
        t = write_trace(
            store,
            m.id,
            subject="codegen",
            experiment_input={"variant": variant},
            metric_score=float(i),
            decision="accept" if variant != "B" else "reject",
        )
        assert t.refine_seq == i  # monotonic, allocated automatically

    traces = list_traces(store, m.id, subject="codegen")
    assert [t["refine_seq"] for t in traces] == [1, 2, 3]
    assert [t["experiment_input"]["variant"] for t in traces] == ["A", "B", "C"]
    # the rejected middle iteration is retained, not overwritten
    assert traces[1]["decision"] == "reject"


def test_external_reader_reconstructs_history(marathon_fallback_store) -> None:
    """US7-AS3: an external reader reconstructs the ordered history from the
    plain records, needing no harness internals."""
    store = marathon_fallback_store
    m, _b = make_marathon(store, slug="traceext")

    from codeconv.marathon.trace import list_traces, write_trace

    write_trace(store, m.id, subject="planner", experiment_input={"k": 1}, decision="reject")
    write_trace(store, m.id, subject="planner", experiment_input={"k": 2}, decision="accept")
    write_trace(store, m.id, subject="codegen", experiment_input={"k": 9}, decision="accept")

    all_traces = list_traces(store, m.id)
    # ordered by (subject, refine_seq) — plain dicts, no harness objects
    keyed = [(t["subject"], t["refine_seq"], t["decision"]) for t in all_traces]
    assert keyed == [
        ("codegen", 1, "accept"),
        ("planner", 1, "reject"),
        ("planner", 2, "accept"),
    ]
