"""Standardized periodic status report (FR-013, D8, SC-005).

Exactly four fields — done / issues / tokens (spent + remaining) / to-do —
emitted on a ~5-min cadence during active work and persisted to
``marathon.status_reports``. Implemented in US5 (T036).
"""

from __future__ import annotations
