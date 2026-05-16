"""Tombstone read-modify-write helpers for codeconv-planagents.

Implements ``specs/017-conversion-plan-agents/contracts/
conversion_plan_artefact_format.md`` § "Tombstone YAML delta" + FR-013 +
data-model §2. Four append-only plan-state keys are written AFTER
feature-015's six (``_FIELD_ORDER`` was extended in
``codeconv.tools.discover.tombstone``):

- ``plan_started_at``       (``plan-started``)
- ``plan_completed_at``     (``plan-completed``; YAML-null while in
                              progress)
- ``plan_path``             (``plan-completed``; YAML-null until recorded)
- ``open_escalation_count`` (``plan-completed``; integer >= 0)

All writes go through feature-012's canonical YAML emitter
(``write_tombstone``) so the byte-for-byte idempotence property carries
forward. Artefact *content* is NOT mirrored into YAML — only plan
*state* (FR-010/FR-013). Both the feature-015 and feature-017 appended
keys are carried forward on every re-write so neither is silently
dropped (data-model §2 round-trip).
"""

from __future__ import annotations

from pathlib import Path
from typing import Any, Optional

from codeconv.tools.discover.tombstone import (
    _FEATURE_015_KEYS,
    _FEATURE_017_KEYS,
    read_tombstone,
    tombstone_path,
    write_tombstone,
)


# Original feature-012 eight keys (preserved verbatim on every write).
_BASE_KEYS = (
    "path",
    "name",
    "purpose",
    "key_idea",
    "dependencies",
    "callers",
    "mtime",
    "sha256",
)


def plan_started_keys(*, plan_started_at: str) -> dict[str, Any]:
    """Keys subset written by ``plan-started`` (only ``plan_started_at``).

    ``plan-started`` records dispatch; ``plan_completed_at`` /
    ``plan_path`` / ``open_escalation_count`` are written later by
    ``plan-completed``. Only the explicitly-passed key is included so a
    later ``plan-completed`` write does not depend on stamp ordering.
    """
    return {"plan_started_at": plan_started_at}


def plan_completed_keys(
    *,
    completed_at: str,
    plan_path: Optional[str],
    open_escalation_count: int,
) -> dict[str, Any]:
    """Keys subset written by ``plan-completed``.

    ``plan_path`` may be ``None`` (emitted as YAML ``null`` — row exists
    but artefact path not recorded). ``open_escalation_count`` is always
    an int >= 0.
    """
    return {
        "plan_completed_at": completed_at,
        "plan_path": plan_path,
        "open_escalation_count": int(open_escalation_count),
    }


def stamp_keys(
    *,
    plan_started_at: Optional[str],
    plan_completed_at: Optional[str],
    plan_path: Optional[str],
    open_escalation_count: Optional[int],
    row_exists: bool,
) -> dict[str, Any]:
    """Build the four-key subset written by ``stamp-tombstones``.

    Null-vs-missing convention (data-model §2, identical to feature 015):

    - ``row_exists is False`` ⇒ NO ``dart_plans`` row ⇒ all four keys
      are OMITTED (return ``{}``) so the tombstone is byte-identical to
      a never-planned one.
    - ``row_exists is True`` ⇒ the keys are PRESENT;
      ``plan_completed_at`` / ``plan_path`` are present-with-``null``
      when the column is NULL (in-progress plan), distinguishing
      "no plan record" from "plan record exists but incomplete".
    """
    if not row_exists:
        return {}
    return {
        "plan_started_at": plan_started_at,
        "plan_completed_at": plan_completed_at,
        "plan_path": plan_path,
        "open_escalation_count": (
            int(open_escalation_count)
            if open_escalation_count is not None
            else 0
        ),
    }


def write_tombstone_with_plan_keys(
    tombstones_root: Path,
    rel_path: str,
    extras: dict[str, Any],
) -> Optional[Path]:
    """Merge ``extras`` into an existing tombstone and write back.

    Preserves the original eight keys verbatim, then carries forward any
    previously-stamped feature-015 AND feature-017 keys, then overlays
    ``extras`` (most-recent values win). If the tombstone does not exist
    on disk, returns ``None`` (no write — same contract as feature-015's
    ``write_tombstone_with_extras``). Emission order + idempotence are
    guaranteed by ``write_tombstone`` (``_FIELD_ORDER`` is append-only).
    """
    target = tombstone_path(tombstones_root, rel_path)
    if not target.is_file():
        return None

    fm = read_tombstone(target)

    merged: dict[str, Any] = {}
    for key in _BASE_KEYS:
        if key in fm:
            merged[key] = fm[key]
    # Carry forward previously-stamped appended keys (both features), so
    # a plan-state write never erases depgraph/conversion state and vice
    # versa (data-model §2 round-trip; mirrors feature-015's writer).
    for key in (*_FEATURE_015_KEYS, *_FEATURE_017_KEYS):
        if key in fm:
            merged[key] = fm[key]
    for key, value in extras.items():
        merged[key] = value

    return write_tombstone(tombstones_root, rel_path, merged)


def read_plan_keys(tombstones_root: Path, rel_path: str) -> dict[str, Any]:
    """Read the four plan-state keys from a tombstone (for rebuild).

    Returns a dict containing only the feature-017 keys that are present
    in the frontmatter (a YAML-``null`` value is preserved as ``None``;
    an absent key is simply not in the returned dict — the null-vs-
    missing distinction the rebuilder needs).
    """
    target = tombstone_path(tombstones_root, rel_path)
    if not target.is_file():
        return {}
    fm = read_tombstone(target)
    return {k: fm[k] for k in _FEATURE_017_KEYS if k in fm}


__all__ = [
    "plan_completed_keys",
    "plan_started_keys",
    "read_plan_keys",
    "stamp_keys",
    "write_tombstone_with_plan_keys",
]
