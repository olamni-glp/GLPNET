"""``codeconv.dart_equivalence`` persistence (two-phase upsert).

The ONLY bridge-touching module in ``tools/equiv`` (everything else is pure).
Uses the canonical ``codeconv.db.engine.connect`` accessor (DISCIPLINE §1.3 —
no per-tool ``_engine`` copy). Imports NO dspy/litellm/openai (SC-008).

**Invoked by the durable ``equiv`` step (T024), which is the sole
``dart_equivalence`` writer.** The standalone ``equiv compare`` / ``capture``
CLI deliberately does NOT call this — it produces the deterministic verdict +
recorded artifacts only; persistence is the durable step's job (R12, the
deterministic ingest of recorded traces). Kept here as the persistence layer
the step will wrap.

Two-phase write discipline (data-model.md), parallel to ``dart_codegen``:

  * ``upsert_captured`` — ``phase='captured'`` + trace hashes + the scorer's
    build/back-test/trace-captured inputs.
  * ``upsert_compared`` — ``phase='compared'`` + ``verdict`` + ``divergence``
    (jsonb), written deterministically from recorded traces.

The fidelity score is NEVER written here (computed-on-read by ``fidelity.py``,
R4/SC-004) — this table holds only the scorer's inputs.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Optional

from sqlalchemy import text

from codeconv.db.engine import connect as _engine_connect


def _engine(repo_root: Path, data_dir: Optional[Path]):
    return _engine_connect(repo_root, data_dir)


# Insert-or-update keyed on the unique (tombstone_key, source_path) index.
# COALESCE on the build/back-test/trace flags so a later phase never clobbers
# an earlier-recorded ``true`` with a default ``false``.
_UPSERT_COMPARED = text(
    """
    INSERT INTO codeconv.dart_equivalence
        (tombstone_key, source_path, subsystem, tier, compare_mode, split,
         phase, verdict, golden_trace_hash, candidate_trace_hash,
         divergence, bytecode_diff_empty, builds, trace_captured,
         dart_source_hash, updated_at)
    VALUES
        (:tombstone_key, :source_path, :subsystem, :tier, :compare_mode, :split,
         'compared', :verdict, :golden_hash, :candidate_hash,
         CAST(:divergence AS jsonb), :bytecode_diff_empty, :builds, :trace_captured,
         :dart_source_hash, now())
    ON CONFLICT (tombstone_key, source_path) DO UPDATE SET
        subsystem            = EXCLUDED.subsystem,
        tier                 = EXCLUDED.tier,
        compare_mode         = EXCLUDED.compare_mode,
        split                = EXCLUDED.split,
        phase                = 'compared',
        verdict              = EXCLUDED.verdict,
        golden_trace_hash    = EXCLUDED.golden_trace_hash,
        candidate_trace_hash = EXCLUDED.candidate_trace_hash,
        divergence           = EXCLUDED.divergence,
        bytecode_diff_empty  = COALESCE(EXCLUDED.bytecode_diff_empty,
                                        codeconv.dart_equivalence.bytecode_diff_empty),
        builds               = codeconv.dart_equivalence.builds OR EXCLUDED.builds,
        trace_captured       = codeconv.dart_equivalence.trace_captured OR EXCLUDED.trace_captured,
        dart_source_hash     = EXCLUDED.dart_source_hash,
        updated_at           = now();
    """
)

_UPSERT_CAPTURED = text(
    """
    INSERT INTO codeconv.dart_equivalence
        (tombstone_key, source_path, subsystem, tier, compare_mode, split,
         phase, golden_trace_hash, candidate_trace_hash,
         builds, trace_captured, dart_source_hash, updated_at)
    VALUES
        (:tombstone_key, :source_path, :subsystem, :tier, :compare_mode, :split,
         'captured', :golden_hash, :candidate_hash,
         :builds, :trace_captured, :dart_source_hash, now())
    ON CONFLICT (tombstone_key, source_path) DO UPDATE SET
        subsystem            = EXCLUDED.subsystem,
        tier                 = EXCLUDED.tier,
        compare_mode         = EXCLUDED.compare_mode,
        split                = EXCLUDED.split,
        phase                = 'captured',
        golden_trace_hash    = EXCLUDED.golden_trace_hash,
        candidate_trace_hash = EXCLUDED.candidate_trace_hash,
        builds               = codeconv.dart_equivalence.builds OR EXCLUDED.builds,
        trace_captured       = codeconv.dart_equivalence.trace_captured OR EXCLUDED.trace_captured,
        dart_source_hash     = EXCLUDED.dart_source_hash,
        updated_at           = now();
    """
)


def upsert_compared(
    repo_root: Path,
    data_dir: Optional[Path],
    *,
    tombstone_key: str,
    source_path: str,
    subsystem: str,
    tier: str,
    compare_mode: str,
    split: str,
    verdict: str,
    golden_hash: Optional[str],
    candidate_hash: Optional[str],
    divergence: Optional[dict],
    bytecode_diff_empty: Optional[bool],
    builds: bool,
    trace_captured: bool,
    dart_source_hash: Optional[str] = None,
) -> None:
    """Write a ``phase='compared'`` verdict row (deterministic)."""
    engine = _engine(repo_root, data_dir)
    with engine.begin() as conn:
        conn.execute(
            _UPSERT_COMPARED,
            {
                "tombstone_key": tombstone_key,
                "source_path": source_path,
                "subsystem": subsystem,
                "tier": tier,
                "compare_mode": compare_mode,
                "split": split,
                "verdict": verdict,
                "golden_hash": golden_hash,
                "candidate_hash": candidate_hash,
                "divergence": json.dumps(divergence) if divergence is not None else None,
                "bytecode_diff_empty": bytecode_diff_empty,
                "builds": builds,
                "trace_captured": trace_captured,
                "dart_source_hash": dart_source_hash,
            },
        )


def upsert_captured(
    repo_root: Path,
    data_dir: Optional[Path],
    *,
    tombstone_key: str,
    source_path: str,
    subsystem: str,
    tier: str,
    compare_mode: str,
    split: str,
    golden_hash: Optional[str],
    candidate_hash: Optional[str],
    builds: bool,
    trace_captured: bool,
    dart_source_hash: Optional[str] = None,
) -> None:
    """Write a ``phase='captured'`` row after a REPL run (capture layer)."""
    engine = _engine(repo_root, data_dir)
    with engine.begin() as conn:
        conn.execute(
            _UPSERT_CAPTURED,
            {
                "tombstone_key": tombstone_key,
                "source_path": source_path,
                "subsystem": subsystem,
                "tier": tier,
                "compare_mode": compare_mode,
                "split": split,
                "golden_hash": golden_hash,
                "candidate_hash": candidate_hash,
                "builds": builds,
                "trace_captured": trace_captured,
                "dart_source_hash": dart_source_hash,
            },
        )


__all__ = ["upsert_captured", "upsert_compared"]
