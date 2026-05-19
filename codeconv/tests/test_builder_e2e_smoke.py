"""End-to-end durable smoke (T054-gate probe).

Minimal proof that the DBOS bootstrap + outer workflow actually executes
on the single-writer PGLite bridge — the R12 reality check the T054
gate exists to surface. 3-file chain; migrate→discover→depgraph→
`builder run`. Asserts the run launches the durable workflow with NO
DBOS/PGLite error and `builder status` works. (Scaled to ≥20 files in
test_dbos_throughput_smoke.py once this passes.)

It also pins, **end-to-end through the real bridge/run path**, the
feature-018 ``needs_agent_work`` surfacing: with NO convspec artifacts
staged the convspec step short-circuits, so the spec-true outcome is
``needs_agent_work`` with a non-empty list (US2 Acceptance 1 — a run
that persists zero specs is NOT ``completed``). This is the exact
behaviour the ``outer_builder_workflow`` propagation defect (found by
the 2026-05-19 genuine live pass) used to hide by reporting
``completed``.
"""

from __future__ import annotations

import json
from pathlib import Path

from .conftest import needs_bridge, run_codeconv
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


@needs_bridge
def test_builder_run_end_to_end_3file(discover_repo: Path) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert (
        run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    )

    # The R12 surface: launch DBOS in-process + drive the outer workflow.
    proc = run_codeconv(
        discover_repo, "builder", "run", "--json", timeout=240.0
    )
    assert proc.returncode == 0, (
        f"builder run failed (rc={proc.returncode})\n"
        f"STDOUT:\n{proc.stdout}\nSTDERR:\n{proc.stderr}"
    )
    payload = json.loads(proc.stdout.strip().splitlines()[-1])
    # No artifacts staged ⇒ every unit short-circuits at convspec ⇒ the
    # outer workflow MUST observe needs_agent_work and surface it (the
    # defect regression: pre-fix this wrongly reported "completed").
    assert payload["outcome"] == "needs_agent_work", payload
    assert payload["needs_agent_work"], payload
    assert payload["units"] >= 1, payload

    # status must reconcile + return promptly.
    s = run_codeconv(discover_repo, "builder", "status", "--json", timeout=60.0)
    assert s.returncode == 0, s.stderr
    sp = json.loads(s.stdout.strip().splitlines()[-1])
    assert sp["counts"]["total"] >= 1, sp
