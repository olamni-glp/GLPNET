"""T063 — facet-3 acceptance gate: the agent gate is traversable by a
PLAIN ``builder run`` (Amendment 2 / FR-044).

This is the exact cycle the 2026-05-19 genuine-live-pass probe proved
BROKEN before remediation: a unit short-circuits at convspec
(``needs_agent_work``); an agent writes the spec artifact; re-driving
**without** ``--restart-run`` MUST ingest it and progress the file to a
terminal state. Pre-remediation the per-unit child returned on the
sentinel ⇒ terminally SUCCESS ⇒ its checkpointed convspec step never
re-ran, and the outer reused the awaiting-agent epoch ⇒ the staged spec
was never ingested (file stuck at ``analysed`` forever).

Real bridge, end-to-end — the authoritative regression for the whole
defect class.
"""

from __future__ import annotations

import json
from pathlib import Path

from .conftest import needs_bridge, run_codeconv, stage_convspec_artifacts
from ._builder_e2e_helpers import (
    builder_run,
    last_json,
    migrate_discover_depgraph,
    mk_nfile_subtree,
)


def _status(repo_root: Path) -> dict:
    p = run_codeconv(
        repo_root, "builder", "status", "--json", timeout=60.0
    )
    assert p.returncode == 0, p.stderr
    return json.loads(p.stdout.strip().splitlines()[-1])


@needs_bridge
def test_plain_redrive_ingests_agent_spec(discover_repo: Path) -> None:
    sub = mk_nfile_subtree(discover_repo, n=3)
    # stage_specs=False ⇒ NO artifacts yet (the awaiting-agent state).
    migrate_discover_depgraph(discover_repo, sub, stage_specs=False)

    # 1) First drive: every unit short-circuits at convspec ⇒ the run
    #    MUST surface needs_agent_work (facet-1) and NOT report completed.
    r1 = last_json(builder_run(discover_repo, timeout=300.0))
    assert r1["outcome"] == "needs_agent_work", r1
    assert r1["needs_agent_work"], r1
    s1 = _status(discover_repo)
    assert s1["counts"].get("analysed", 0) >= 1, s1
    assert s1["counts"].get("complete", 0) == 0, s1

    # 2) The agent writes valid spec artifacts.
    staged = stage_convspec_artifacts(discover_repo, sub)
    assert staged, "fixture should have staged ≥1 artifact"

    # 3) PLAIN re-drive — NO --restart-run (the skill loop never passes
    #    it). The fixed gate must mint a fresh epoch (prior run was
    #    awaiting-agent), recover the deterministic PRE wfs, launch the
    #    content-addressed POST wfs, ingest the specs, run plan.
    r2 = last_json(builder_run(discover_repo, timeout=600.0))
    assert r2["outcome"] == "completed", r2
    assert not r2["needs_agent_work"], r2
    s2 = _status(discover_repo)
    # Every file progressed PAST analysed — none stuck awaiting.
    assert s2["counts"].get("analysed", 0) == 0, s2
    assert s2["counts"]["total"] >= 3, s2
    from codeconv.status import FileState

    terminalish = {
        FileState.SPECCED.value,
        FileState.SCAFFOLDED.value,
        FileState.CONVERTED.value,
        FileState.COMPLETE.value,
    }
    assert all(
        f["state"] in terminalish for f in s2["files"]
    ), s2["files"]

    # 4) A further plain re-drive is an idempotent no-op (SC-002/FR-004):
    #    completed run ⇒ epoch reused ⇒ DBOS replays, nothing re-done.
    r3 = last_json(builder_run(discover_repo, timeout=300.0))
    assert r3["outcome"] == "completed", r3
    assert _status(discover_repo)["counts"] == s2["counts"], "not idempotent"
