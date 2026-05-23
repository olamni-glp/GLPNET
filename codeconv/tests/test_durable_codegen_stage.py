"""T039 — durable codegen step: registered, needs_agent_work, replay-safe.

Mixes a pure registration check with a @needs_bridge step-behavior check.
Decision B (2026-05-23): the ``codegen`` DBOS step IS registered
(replay-safe, available for explicit chaining) but is a SEPARATELY-DRIVEN
phase — NOT in the builder's default per-unit sequence (driven via
``/codeconv-codegen``). Its body (``run_codegen_step``) returns the
``needs_agent_work`` sentinel when the ``.cs`` is absent (never raises)
and is replay-safe (re-running yields the same outcome — no LM/web call).
"""

from __future__ import annotations

from pathlib import Path

from .conftest import needs_bridge
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


def test_codegen_step_registered_pure() -> None:
    """The ``codegen`` DBOS step is registered (replay-safe, available for
    explicit chaining), AND — decision B — it is NOT in the builder's
    default per-unit sequence (codegen is driven via /codeconv-codegen)."""
    import importlib

    import codeconv.durable as durable
    import codeconv.durable.steps as steps_mod

    durable.reset_registry_for_tests()
    # reload (not plain import) so registration re-runs even when the
    # module is already cached by an earlier test (order-independent).
    importlib.reload(steps_mod)
    steps = set(durable.registered_steps())
    assert {"plan", "codegen"} <= steps, steps

    from codeconv.durable.workflows import PER_UNIT_STAGES

    assert "codegen" not in PER_UNIT_STAGES, PER_UNIT_STAGES


@needs_bridge
def test_step_codegen_absent_cs_is_needs_agent_work(discover_repo: Path) -> None:
    """The step body returns the typed sentinel (never raises) when the
    produced .cs is absent — the skill spawns the agent on this."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)

    from codeconv.tools.codegen.workflow import run_codegen_step

    res = run_codegen_step(str(discover_repo), rel_path="lib/a.dart")
    assert res["outcome"] == "needs_agent_work"
    assert res["needs_agent_work"] is True


@needs_bridge
def test_step_codegen_replay_safe(discover_repo: Path) -> None:
    """Re-running the step on the same (absent) artifact yields the same
    outcome and writes no duplicate row (deterministic, replay-safe)."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)

    from codeconv.tools.codegen.workflow import run_codegen_step

    r1 = run_codegen_step(str(discover_repo), rel_path="lib/a.dart")
    r2 = run_codegen_step(str(discover_repo), rel_path="lib/a.dart")
    assert r1["outcome"] == r2["outcome"] == "needs_agent_work"

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(acquire_or_discover(discover_repo, ready_timeout=30.0))
    with eng.connect() as conn:
        n = conn.execute(
            text(
                "SELECT COUNT(*) FROM codeconv.dart_codegen "
                "WHERE path = 'lib/a.dart'"
            )
        ).scalar()
    assert n == 1  # phase-1 started row, idempotent (no duplicate)
