"""Append-only verification-trace substrate (FR-016/017, US7).

Records experiment_input / metric_score / decision per (subject,
refine_seq); earlier iterations are never overwritten. Substrate ONLY — no
optimizer/loop (the GEPA/DSPy optimizer is out of scope, Gabi 2026-06-05).
Implemented in US7 (T044).
"""

from __future__ import annotations
