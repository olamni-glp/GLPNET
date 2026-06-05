"""Workflow-tool composition layer (FR-009/010/012).

One stage-block == one Workflow run. The harness records the run id as
run-linkage, snapshots ``budget.spent()/remaining()`` at checkpoints, and
enforces the budget ceiling — it does NOT re-implement fan-out, per-agent
journaling, or cached-prefix resume (those are the Workflow tool's; the
production orchestration composes it via the buildkit-stage skill).
Implemented in US4 (T013), US3 (T029/T030/T032), US5 (T037), US6 (T042).
"""

from __future__ import annotations
