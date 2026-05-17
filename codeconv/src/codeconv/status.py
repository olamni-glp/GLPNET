"""Unified per-file conversion state — pure projection (feature 018).

FR-017 / FR-022, contract ``status_trace_contract.md``, data-model §5.

**Projection only.** The unified state is *never a separately-stored
field*: it is derived from the durable facts (feature-015 depgraph
readiness + the two-phase ``dart_convspecs`` / ``dart_plans`` /
``dart_conversions`` columns + the open-escalation count). Because it is
a pure function of durable truth it cannot diverge from it (FR-017 /
FR-019). This module owns ONLY the vocabulary + the §5 state machine;
the DB join that gathers the facts lives in ``builder status`` (T045) so
this module stays pure and unit-testable with no bridge (parallels the
feature-017 readiness predicate).

State machine (data-model §5):

    not_started → blocked_on_deps → analysed → specced
                → scaffolded → converted → complete

``escalated`` is reachable from any non-terminal state (open escalations
> 0) and resolves back to the state the durable facts imply once the
escalation count returns to 0. ``complete`` is the only terminal state.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum


class FileState(str, Enum):
    """The single per-file state vocabulary (FR-022 — one definition)."""

    NOT_STARTED = "not_started"
    BLOCKED_ON_DEPS = "blocked_on_deps"
    ANALYSED = "analysed"
    SPECCED = "specced"
    SCAFFOLDED = "scaffolded"
    CONVERTED = "converted"
    ESCALATED = "escalated"
    COMPLETE = "complete"


# Non-terminal states, in §5 progression order. ``escalated`` is NOT in
# this list (it is an overlay reachable from any of these); ``complete``
# is terminal and excluded.
PROGRESSION: tuple[FileState, ...] = (
    FileState.NOT_STARTED,
    FileState.BLOCKED_ON_DEPS,
    FileState.ANALYSED,
    FileState.SPECCED,
    FileState.SCAFFOLDED,
    FileState.CONVERTED,
)

TERMINAL: FileState = FileState.COMPLETE


class EscalationKind(str, Enum):
    """Escalation vocabulary (contract ``convspec_artifact_format.md`` §
    Escalation schema; FR-013 / FR-014)."""

    UNDECIDABLE = "undecidable"
    IDIOM_VS_RESEARCH_CONFLICT = "idiom_vs_research_conflict"
    IDIOM_VS_IDIOM_CONFLICT = "idiom_vs_idiom_conflict"


@dataclass(frozen=True)
class FileFacts:
    """The minimal durable facts the projection consumes.

    All fields are plain booleans/ints gathered by the caller from the
    durable record (depgraph readiness + the two-phase ``*_completed_at``
    columns + escalation count). Keeping them primitive is what makes the
    projection pure and replay-independent.

    - ``deps_ready``: feature-015 depgraph says every dependency (or the
      whole SCC group) is converted — read-only, MUST NOT recompute (015).
    - ``analysis_done``: convspec deep-analysis stage reached
      (``dart_convspecs.convspec_started_at`` is set).
    - ``spec_done``: per-file conversion spec finalised
      (``dart_convspecs.convspec_completed_at`` is set).
    - ``scaffold_done``: target code-unit scaffolded
      (feature-016 scaffold recorded the target path).
    - ``converted``: code generation completed (later stage /
      ``dart_conversions.conversion_completed_at``).
    - ``complete``: the file's whole pipeline (incl. SCC group) is done.
    - ``open_escalations``: count of unresolved escalations for the file.
    """

    deps_ready: bool
    analysis_done: bool
    spec_done: bool
    scaffold_done: bool
    converted: bool
    complete: bool
    open_escalations: int = 0


def project_file_state(facts: FileFacts) -> FileState:
    """Derive the unified :class:`FileState` from durable facts (pure).

    Order of evaluation follows data-model §5: ``complete`` is terminal
    and wins; otherwise an open escalation overlays ``escalated``;
    otherwise the highest progression milestone reached, with
    ``blocked_on_deps`` taking precedence over ``not_started`` when the
    file has not started AND its dependencies are not ready.
    """
    if facts.complete:
        return FileState.COMPLETE

    if facts.open_escalations > 0:
        # Reachable from any non-terminal state; resolves back once the
        # count returns to 0 (this function is re-evaluated each query).
        return FileState.ESCALATED

    if facts.converted:
        return FileState.CONVERTED
    if facts.scaffold_done:
        return FileState.SCAFFOLDED
    if facts.spec_done:
        return FileState.SPECCED
    if facts.analysis_done:
        return FileState.ANALYSED

    # Nothing started yet: distinguish "waiting on deps" from a clean
    # not-started so the operator sees *why* a file is idle (FR-017).
    if not facts.deps_ready:
        return FileState.BLOCKED_ON_DEPS
    return FileState.NOT_STARTED


__all__ = [
    "EscalationKind",
    "FileFacts",
    "FileState",
    "PROGRESSION",
    "TERMINAL",
    "project_file_state",
]
