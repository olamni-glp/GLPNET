"""Human-review recording + batch promotion gate (US3 / FR-006).

Implements ``specs/019-codeconv-codegen/contracts/metric_contract.md`` §
"Batch promotion gate": a batch is ``promoted`` iff **100% of its files
build** AND the **median human_review score ≥ 4/5**. Else: re-generate
(retry) or escalate.

The pure ``promotion_gate`` (no I/O) is unit-tested (T033). The
``run_record_review`` / ``run_promote_batch`` entrypoints touch
``codeconv.dart_codegen`` (the deterministic state) and a checked-in
reviews log (the free-text notes that feed the OFFLINE optimizer's
dataset — never used to silently rewrite production code).
"""

from __future__ import annotations

import statistics
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Optional, Sequence

from sqlalchemy import text

from codeconv.bridge_client import acquire_or_discover
from codeconv.db.engine import build_engine


REVIEWS_LOG_REL = ".codeconv/conversion-code/_reviews.md"

PROMOTION_MIN_MEDIAN = 4.0


@dataclass(frozen=True)
class GateResult:
    """Outcome of the promotion gate for one batch."""

    passed: bool
    all_built: bool
    median_human: Optional[float]
    blockers: tuple[str, ...]

    def __bool__(self) -> bool:
        return self.passed


def promotion_gate(
    rows: Sequence[tuple[str, str, Optional[int]]],
) -> GateResult:
    """Pure promotion gate over ``[(path, build_status, human_score)]``.

    Passes iff EVERY file has ``build_status == 'pass'`` AND the median of
    the recorded (non-null) human scores is ≥ 4. An empty batch, an
    unbuilt file, or no recorded reviews ⇒ fail with named blockers.
    """
    if not rows:
        return GateResult(False, False, None, ("empty batch",))

    blockers: list[str] = []
    not_built = [p for p, bs, _ in rows if bs != "pass"]
    all_built = not not_built
    for p in not_built:
        blockers.append(f"{p}: build_status != pass")

    scores = [int(s) for _p, _bs, s in rows if s is not None]
    median_human: Optional[float]
    if not scores:
        median_human = None
        blockers.append("no human reviews recorded for the batch")
    else:
        median_human = float(statistics.median(scores))
        if median_human < PROMOTION_MIN_MEDIAN:
            blockers.append(
                f"median human review {median_human} < {PROMOTION_MIN_MEDIAN}"
            )

    passed = all_built and median_human is not None and (
        median_human >= PROMOTION_MIN_MEDIAN
    )
    return GateResult(passed, all_built, median_human, tuple(blockers))


# ---------------------------------------------------------------------------
# Entrypoints (DB + reviews-log)
# ---------------------------------------------------------------------------


def _engine(repo_root: Path, data_dir: Optional[Path]):
    endpoint = acquire_or_discover(
        Path(repo_root).resolve(), ready_timeout=60.0, data_dir=data_dir
    )
    return build_engine(endpoint)


def run_record_review(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    batch_id: str,
    file_path: str,
    score: int,
    note: Optional[str] = None,
) -> dict:
    """Record one sampled human review on ``file_path`` in ``batch_id``.

    Sets ``human_review_score`` (1–5, CHECK-enforced) + ``batch_id`` on
    the ``dart_codegen`` row (which must already exist + be built). The
    free-text ``note`` is appended to the checked-in reviews log (feeds
    the offline optimizer; never rewrites production code).
    """
    repo_root = Path(repo_root).resolve()
    if not 1 <= int(score) <= 5:
        return {
            "ok": False,
            "exit_code": 2,
            "error": "--score must be in 1..5",
            "path": file_path,
        }
    engine = _engine(repo_root, data_dir)
    with engine.connect() as conn:
        row = conn.execute(
            text(
                "SELECT build_status FROM codeconv.dart_codegen WHERE path = :p"
            ),
            {"p": file_path},
        ).first()
    if row is None:
        return {
            "ok": False,
            "exit_code": 2,
            "error": f"no codegen row for {file_path!r}; build it first",
            "path": file_path,
        }
    with engine.begin() as conn:
        conn.execute(
            text(
                "UPDATE codeconv.dart_codegen SET "
                "human_review_score = :s, batch_id = :b "
                "WHERE path = :p"
            ),
            {"s": int(score), "b": batch_id, "p": file_path},
        )

    if note:
        log = repo_root / REVIEWS_LOG_REL
        log.parent.mkdir(parents=True, exist_ok=True)
        with log.open("a", encoding="utf-8") as fh:
            fh.write(
                f"- [{batch_id}] {file_path} (score {int(score)}): {note}\n"
            )

    return {
        "ok": True,
        "exit_code": 0,
        "path": file_path,
        "batch_id": batch_id,
        "score": int(score),
        "action": "review_recorded",
    }


def run_promote_batch(
    *,
    repo_root: Path,
    data_dir: Optional[Path] = None,
    batch_id: str,
) -> dict:
    """Apply the promotion gate; set ``promoted`` on pass, else blockers.

    The gate (FR-006): 100% of the batch's files build AND median human
    review ≥ 4/5. On pass, set ``promoted=true`` for every batch file. On
    fail, report the blockers (the skill retries/escalates) and write
    nothing.
    """
    repo_root = Path(repo_root).resolve()
    engine = _engine(repo_root, data_dir)
    with engine.connect() as conn:
        rows = conn.execute(
            text(
                "SELECT path, build_status, human_review_score "
                "FROM codeconv.dart_codegen WHERE batch_id = :b"
            ),
            {"b": batch_id},
        ).all()
    rows_t = [(r[0], r[1], r[2]) for r in rows]
    gate = promotion_gate(rows_t)

    if not gate.passed:
        return {
            "ok": True,
            "exit_code": 0,
            "batch_id": batch_id,
            "promoted": False,
            "all_built": gate.all_built,
            "median_human": gate.median_human,
            "blockers": list(gate.blockers),
        }

    run_id = str(uuid.uuid4())
    with engine.begin() as conn:
        conn.execute(
            text(
                "UPDATE codeconv.dart_codegen SET promoted = true, "
                "codegen_run_id = :rid WHERE batch_id = :b"
            ),
            {"rid": run_id, "b": batch_id},
        )
    return {
        "ok": True,
        "exit_code": 0,
        "batch_id": batch_id,
        "promoted": True,
        "all_built": True,
        "median_human": gate.median_human,
        "files_promoted": len(rows_t),
        "run_id": run_id,
    }


__all__ = [
    "PROMOTION_MIN_MEDIAN",
    "REVIEWS_LOG_REL",
    "GateResult",
    "promotion_gate",
    "run_promote_batch",
    "run_record_review",
]
