"""Durable ``equiv`` step (T024) + tool-side ``register()`` delegate.

The durable stage that wraps the standalone US1 ``compare`` (T019). Per
``contracts/dbos_equiv_stage.md`` (the 019 R3 carry-forward, HARD GATE):

  * The step body is a **deterministic function of recorded inputs** —
    apply the pure ``relation.py`` to recorded golden+candidate traces, then
    two-phase write ``dart_equivalence``. Same inputs ⇒ same verdict.
  * **No REPL spawn, no wall-clock, no LM, no network inside the step.** The
    nondeterministic REPL spawn lives in the agent/CLI ``capture`` layer.
  * **Never raises on verdict outcomes.** Missing recorded traces ⇒ typed
    ``needs_agent_work`` sentinel; a ``divergent`` verdict is a normal result.
  * **Idempotent / resumable.** ``upsert_compared`` ON CONFLICT preserves
    earlier ``builds=true`` / ``trace_captured=true`` (the SQL OR-merges),
    so a replayed step writes the same row.

Layered for testability:

  * :func:`compute_step_result` — PURE; no bridge, no DB. Returns a typed
    :class:`StepResult` (``bad_data`` / ``needs_agent_work`` / ``compared``).
    Fully unit-testable from constructed fixtures.
  * :func:`step_equiv` — the durable step body: ``compute_step_result`` +
    (on ``compared``) ``store.upsert_compared``. The only bridge-touching path.

The DBOS ``@step`` decoration is applied centrally at activation time (T025 —
``codeconv.durable.steps``); ``register()`` here just delegates to ``activate``
so every tool's ``register(dbos_app)`` is idempotent (codegen/planagents pattern).
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any, Optional

from codeconv.tools.equiv import corpus as _corpus
from codeconv.tools.equiv import manifest as _manifest
from codeconv.tools.equiv import recorded as _recorded
from codeconv.tools.equiv import verdict as _verdict


_SUBSYS_PARTS = (".codeconv", "equiv-manifest", "subsystems.yml")


# Result kinds — exit-code/sentinel parity with the standalone CLI (equiv_cli.md).
KIND_COMPARED = "compared"
KIND_NEEDS_AGENT_WORK = "needs_agent_work"
KIND_BAD_DATA = "bad_data"


@dataclass(frozen=True)
class StepResult:
    """Typed step outcome — also the JSON shape returned to callers.

    ``kind == 'compared'`` carries the full row fields ``upsert_compared`` needs;
    ``needs_agent_work`` / ``bad_data`` carry a ``reason`` and no row fields
    (the step writes nothing in those branches)."""

    kind: str
    tombstone_key: str
    source_path: str
    subsystem: Optional[str] = None
    tier: Optional[str] = None
    compare_mode: Optional[str] = None
    split: Optional[str] = None
    verdict: Optional[str] = None
    golden_hash: Optional[str] = None
    candidate_hash: Optional[str] = None
    divergence: Optional[dict] = None
    bytecode_diff_empty: Optional[bool] = None
    builds: bool = False
    trace_captured: bool = False
    reason: Optional[str] = None

    def to_dict(self) -> dict:
        out: dict = {"kind": self.kind,
                     "tombstone_key": self.tombstone_key,
                     "source_path": self.source_path}
        for k in ("subsystem", "tier", "compare_mode", "split", "verdict",
                  "divergence", "bytecode_diff_empty", "reason"):
            v = getattr(self, k)
            if v is not None:
                out[k] = v
        return out


def compute_step_result(
    repo_root: Path | str, tombstone_key: str, source_path: str,
) -> StepResult:
    """PURE deterministic ingest: recorded artifacts → row fields. No DB.

    The unit-testable heart of the durable step. The bridge-touching caller
    (:func:`step_equiv`) only adds the ``upsert_compared`` write."""
    repo = Path(repo_root)

    sources = {s.path: s for s in _corpus.load(repo)}
    source = sources.get(source_path)
    if source is None:
        return StepResult(
            kind=KIND_BAD_DATA, tombstone_key=tombstone_key, source_path=source_path,
            reason=f"source {source_path!r} not in corpus",
        )

    subsystem = _manifest.load(repo.joinpath(*_SUBSYS_PARTS)).subsystem_for(tombstone_key)
    if subsystem is None:
        return StepResult(
            kind=KIND_BAD_DATA, tombstone_key=tombstone_key, source_path=source_path,
            reason=f"tombstone_key {tombstone_key!r} classifies to no subsystem",
        )

    entry = _recorded.load_entry(repo, tombstone_key, source_path)
    if not entry.have_traces:
        return StepResult(
            kind=KIND_NEEDS_AGENT_WORK, tombstone_key=tombstone_key, source_path=source_path,
            subsystem=subsystem, tier=source.tier, compare_mode=source.compare_mode,
            split=source.split,
            reason="no recorded golden+candidate trace (run `equiv capture`)",
        )

    result = _verdict.compare_recorded(
        entry.golden_trace,  # type: ignore[arg-type]
        entry.candidate_trace,  # type: ignore[arg-type]
        compare_mode=source.compare_mode, tier=source.tier,
    )
    bytecode_diff_empty: Optional[bool] = None
    if entry.have_bytecode:
        bytecode_diff_empty = _verdict.compare_bytecode(
            entry.golden_bc, entry.candidate_bc,  # type: ignore[arg-type]
        ).empty

    divergence = (
        _verdict.divergence_to_dict(result.verdict.divergence)
        if result.verdict.divergence is not None else None
    )
    return StepResult(
        kind=KIND_COMPARED,
        tombstone_key=tombstone_key, source_path=source_path,
        subsystem=subsystem, tier=source.tier,
        compare_mode=source.compare_mode, split=source.split,
        verdict="equivalent" if result.equivalent else "divergent",
        golden_hash=result.golden_trace_hash,
        candidate_hash=result.candidate_trace_hash,
        divergence=divergence,
        bytecode_diff_empty=bytecode_diff_empty,
        # A candidate trace exists ⇒ the C# built & ran (drives the high band
        # for fidelity, R4/SC-004); trace_captured=True is the data-model gate
        # input for moving out of the flat 0.25 low band.
        builds=True, trace_captured=True,
    )


def step_equiv(
    *,
    repo_root: Path | str,
    data_dir: Optional[Path],
    tombstone_key: str,
    source_path: str,
) -> dict:
    """The durable step body: deterministic verdict ingest + DB write.

    Replay-safe (R12): given the same recorded artifacts, ``compute_step_result``
    yields the same ``StepResult`` and ``upsert_compared`` is idempotent under
    its unique-index ``ON CONFLICT`` clause. Never raises on verdict outcomes
    (``divergent`` is a row state, ``needs_agent_work`` is a typed sentinel);
    a DB outage propagates so DBOS can retry the step.
    """
    result = compute_step_result(repo_root, tombstone_key, source_path)
    if result.kind != KIND_COMPARED:
        return result.to_dict()

    # Lazy import keeps the bridge/SQLAlchemy import off the pure path.
    from codeconv.tools.equiv import store as _store

    _store.upsert_compared(
        Path(repo_root), data_dir,
        tombstone_key=result.tombstone_key,
        source_path=result.source_path,
        subsystem=result.subsystem or "",
        tier=result.tier or "",
        compare_mode=result.compare_mode or "",
        split=result.split or "",
        verdict=result.verdict or "",
        golden_hash=result.golden_hash,
        candidate_hash=result.candidate_hash,
        divergence=result.divergence,
        bytecode_diff_empty=result.bytecode_diff_empty,
        builds=result.builds,
        trace_captured=result.trace_captured,
    )
    return result.to_dict()


def register(dbos_app: Any) -> None:
    """Delegate to the centralised durable activation (idempotent).

    Mirrors ``codegen.workflow.register`` / ``planagents.workflow.register``.
    The actual ``@DBOS.step`` decoration for the equiv step is wired in
    ``codeconv.durable.steps`` at T025; this delegate ensures every tool's
    register() converges on a single activation."""
    from codeconv.durable import activate

    activate(dbos_app)


__all__ = [
    "KIND_BAD_DATA",
    "KIND_COMPARED",
    "KIND_NEEDS_AGENT_WORK",
    "StepResult",
    "compute_step_result",
    "register",
    "step_equiv",
]
