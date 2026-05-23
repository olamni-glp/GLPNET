"""T055 [US1] — E2 remedy: pipeline stage order + scaffold records
target path (FR-007/FR-008), entrypoints unchanged (D2).

`discover` is the inventory entry stage; `scaffold` produces the target
tree and records each produced target path into per-file
conversion-tracking state — all inside the durable builder workflow,
calling the existing 015/016 entrypoints verbatim (no behaviour change).
"""

from __future__ import annotations

from pathlib import Path

from .conftest import needs_bridge
from ._builder_e2e_helpers import (
    builder_run,
    last_json,
    migrate_discover_depgraph,
    mk_nfile_subtree,
)


def test_stage_order_constants_pure() -> None:
    """discover/depgraph are the graph-entry stages; scaffold precedes
    plan in the per-unit order (FR-007/FR-008). Amendment 2 / FR-044:
    the per-unit work splits across the agent gate into PRE
    (scaffold→convspec) and POST (convspec→plan); convspec is the gate
    boundary present in both."""
    from codeconv.durable.workflows import (
        GRAPH_ENTRY_STAGES,
        PER_UNIT_STAGES,
        POST_STAGES,
        PRE_STAGES,
    )

    assert GRAPH_ENTRY_STAGES[0] == "discover", GRAPH_ENTRY_STAGES
    assert "depgraph_compute" in GRAPH_ENTRY_STAGES
    assert list(PER_UNIT_STAGES).index("scaffold") < list(
        PER_UNIT_STAGES
    ).index("plan"), PER_UNIT_STAGES
    # Feature 019 (decision B): codegen is a separately-driven durable
    # phase, NOT in the builder's per-unit/POST sequence — so the builder
    # keeps its "completed-at-plan" semantics. (The codegen DBOS step is
    # still registered; see test_codegen_capability.)
    assert "codegen" not in PER_UNIT_STAGES, PER_UNIT_STAGES
    # PRE ends, and POST begins, at the convspec agent-gate boundary.
    assert PRE_STAGES == ("scaffold", "convspec"), PRE_STAGES
    assert POST_STAGES == ("convspec", "plan"), POST_STAGES
    assert PRE_STAGES[-1] == POST_STAGES[0] == "convspec"
    # PRE then POST, deduped, == the logical full per-unit order.
    merged = list(PRE_STAGES) + [s for s in POST_STAGES if s not in PRE_STAGES]
    assert merged == list(PER_UNIT_STAGES), (merged, PER_UNIT_STAGES)


@needs_bridge
def test_scaffold_records_target_path_in_tombstones(
    discover_repo: Path,
) -> None:
    """After a builder run the scaffold stage has produced the target
    tree and recorded ``target_path`` into each file's tombstone
    (feature-016 behaviour, driven verbatim through the durable step)."""
    sub = mk_nfile_subtree(discover_repo, n=20)
    migrate_discover_depgraph(discover_repo, sub)

    r = last_json(builder_run(discover_repo, timeout=600.0))
    assert r["outcome"] == "completed", r

    from codeconv.tools.discover.tombstone import read_tombstone

    tombs = sorted((discover_repo / ".codeconv" / "tombstones").rglob("*.md"))
    assert tombs, "no tombstones produced by the pipeline"
    with_target = 0
    for t in tombs:
        fm = read_tombstone(t)
        if fm.get("target_path"):
            with_target += 1
    assert with_target >= 1, (
        "scaffold stage did not record any target_path into tombstones "
        "(FR-008) — the durable scaffold step is not wired correctly"
    )
