"""Checkpoint write/read + objective resume-locate (FR-002/003).

Objective position location follows the CLAUDE.md Restart-Resume order:
roadmap (`buildkit-roadmap next`) → buildkit pipeline state → spec/plan/
tasks → the max(sequence_no) checkpoint. NEVER reads a conversation
summary. Implemented in US1 (T018/T019/T023).
"""

from __future__ import annotations
