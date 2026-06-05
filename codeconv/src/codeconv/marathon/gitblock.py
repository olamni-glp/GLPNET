"""Preauthorized commit + push per logical block (FR-014/015, D10, SC-010).

Stages exactly the block's files (never ``git add -A``), never force-pushes,
never bypasses git hooks; a non-fast-forward push writes a ``push_blocked``
escalation and stops. Implemented in US6 (T040/T041).
"""

from __future__ import annotations
