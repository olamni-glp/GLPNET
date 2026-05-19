"""Widen ``builder_runs.outcome`` CHECK to allow ``needs_agent_work``.

Feature-018 remediation (2026-05-19, found by the genuine live pass).

The 0005 ``builder_runs_outcome_check`` allowed only
``{completed, resumed, nothing_to_convert, escalated}``. That set was
written against the ``outer_builder_workflow`` propagation defect: the
buggy code could never produce ``needs_agent_work`` (child sentinels
were discarded), so the schema constraint never needed it. With the
defect fixed, ``builder run`` correctly writes
``builder_runs.outcome = 'needs_agent_work'`` for the awaiting-agent
path (US2 Acceptance 1 / ``dbos_workflow_model.md`` § needs_agent_work)
— which the old constraint rejected with a CheckViolation.

Forward-only (0005 is released — tag ``v2026.05.17-2``); single linear
head preserved per ``contracts/migration_linearization.md`` (SC-004).
"""
from __future__ import annotations

from typing import Sequence, Union

from alembic import op


revision: str = "0006"
down_revision: Union[str, None] = "0005"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


_OUTCOMES_NEW = (
    "'completed', 'resumed', 'nothing_to_convert', 'escalated', "
    "'needs_agent_work'"
)
_OUTCOMES_OLD = "'completed', 'resumed', 'nothing_to_convert', 'escalated'"


def _set_outcome_check(values: str) -> None:
    op.execute(
        "ALTER TABLE codeconv.builder_runs "
        "DROP CONSTRAINT IF EXISTS builder_runs_outcome_check;"
    )
    op.execute(
        "ALTER TABLE codeconv.builder_runs "
        "ADD CONSTRAINT builder_runs_outcome_check CHECK ("
        f"outcome IS NULL OR outcome IN ({values}));"
    )


def upgrade() -> None:
    _set_outcome_check(_OUTCOMES_NEW)


def downgrade() -> None:
    _set_outcome_check(_OUTCOMES_OLD)
