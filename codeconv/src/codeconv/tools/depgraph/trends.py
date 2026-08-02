"""Cross-run trend reporting for ``depgraph trends`` (feature 062, US1).

Pure, stdlib-only. Given >=2 recorded depgraph runs (each a dict of the
run's aggregate metrics), produce a **deterministic, secret-redacted**
per-metric delta report that is **byte-identical on unchanged inputs**
(spec FR-002, contract ``specs/062-.../contracts/depgraph-cli.md`` §
``trends``).

Determinism rules (mirror :mod:`json_writer`):

1. Runs are sorted by ``(started_at, id)`` before diffing — input order
   never affects the output.
2. No wall-clock value enters the report body (a generation timestamp, per
   R-2, belongs in a filename only, never here).
3. The report is emitted with ``json.dumps(..., sort_keys=True)`` so key
   order is fixed.

"Secret-redacted": every string value that flows into the report is passed
through :func:`_redact`, which masks embedded secret-like tokens. Run
aggregate metrics are integers, so this is a no-op in practice today, but
the pass is applied (and tested) so the report stays safe if run metadata
ever grows string fields.
"""

from __future__ import annotations

import re
from typing import Any, Sequence

SCHEMA_VERSION = 1

# Metric columns compared across runs (codeconv.depgraph_runs aggregate
# columns). Order here does not affect output (keys are sorted on emit); it
# only documents the compared set.
METRIC_FIELDS: tuple[str, ...] = (
    "files_total",
    "ready_count",
    "in_progress_count",
    "converted_count",
    "cycle_count",
)

# Secret-like token: a long run of base64/hex-ish characters, or an obvious
# key=secret assignment. Deliberately conservative — run metrics never match.
_SECRET_TOKEN = re.compile(
    r"(?i)(?:(?:api|secret|token|password|pwd|key)\s*[:=]\s*\S+)"
    r"|(?:[A-Za-z0-9+/_-]{32,}={0,2})"
)


class TrendError(ValueError):
    """Raised when a trend report cannot be produced (e.g. <2 runs)."""


def _redact(value: Any) -> Any:
    """Mask secret-like tokens in a string; pass non-strings through."""
    if isinstance(value, str):
        return _SECRET_TOKEN.sub("[REDACTED]", value)
    return value


def _run_key(run: dict[str, Any]) -> tuple[str, str]:
    """Deterministic sort key: (started_at, id) as strings (never None)."""
    return (str(run.get("started_at") or ""), str(run.get("id") or ""))


def compute_trends(runs: Sequence[dict[str, Any]]) -> dict[str, Any]:
    """Compute a deterministic per-metric delta report over >=2 runs.

    Args:
        runs: recorded runs, each a dict with ``id``, ``started_at`` and the
            :data:`METRIC_FIELDS` (missing metrics are treated as ``None``
            and reported as ``null`` — never fabricated as ``0``).

    Returns:
        The canonical report dict (see module docstring for shape).

    Raises:
        TrendError: fewer than two runs were supplied.
    """
    if len(runs) < 2:
        raise TrendError("at least two runs required")

    ordered = sorted(runs, key=_run_key)

    report_runs: list[dict[str, Any]] = []
    for run in ordered:
        entry: dict[str, Any] = {
            "id": _redact(str(run.get("id"))),
            "started_at": _redact(str(run.get("started_at"))),
        }
        for field in METRIC_FIELDS:
            entry[field] = run.get(field)
        report_runs.append(entry)

    metric_deltas: dict[str, Any] = {}
    for field in METRIC_FIELDS:
        series = [run.get(field) for run in ordered]
        first, last = series[0], series[-1]
        # Step deltas only where both endpoints are numeric; otherwise null
        # (never invent a delta across a missing value).
        step_deltas: list[Any] = []
        for prev, cur in zip(series, series[1:]):
            if isinstance(prev, (int, float)) and isinstance(cur, (int, float)):
                step_deltas.append(cur - prev)
            else:
                step_deltas.append(None)
        total = (
            last - first
            if isinstance(first, (int, float)) and isinstance(last, (int, float))
            else None
        )
        metric_deltas[field] = {
            "first": first,
            "last": last,
            "delta": total,
            "series": series,
            "step_deltas": step_deltas,
        }

    return {
        "schema_version": SCHEMA_VERSION,
        "run_count": len(ordered),
        "runs": report_runs,
        "metric_deltas": metric_deltas,
    }


__all__ = ["SCHEMA_VERSION", "METRIC_FIELDS", "TrendError", "compute_trends"]
