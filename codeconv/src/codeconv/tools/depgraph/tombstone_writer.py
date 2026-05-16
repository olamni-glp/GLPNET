"""Tombstone read-modify-write helpers for codeconv-depgraph.

Per ``specs/015-codeconv-depgraph/contracts/tombstone_format_delta.md``:

- ``mark-started`` writes only ``conversion_started_at``.
- ``mark-completed`` writes ``conversion_completed_at`` (+ ``target_path``
  when ``--target`` was supplied).
- ``stamp-tombstones`` writes all six new keys.

All paths go through feature 012's canonical YAML writer in
``codeconv.tools.discover.tombstone`` so the byte-for-byte idempotence
property carries forward.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any, Optional

from codeconv.tools.discover.tombstone import (
    read_tombstone,
    tombstone_path,
    write_tombstone,
)


def update_conversion_keys(
    *,
    started_at: Optional[str] = None,
    completed_at: Optional[str] = None,
    target_path: Optional[str] = None,
) -> dict[str, Any]:
    """Build the keys subset written by ``mark-started`` / ``mark-completed``.

    Only keys explicitly passed (non-``None``) are included, so an omitted
    ``--target`` leaves any prior ``target_path`` untouched (write-once per
    FR-006a). ``stamp-tombstones`` passes ``target_path`` through the
    six-key path in :func:`update_depgraph_keys` + the extras dict instead.
    """
    out: dict[str, Any] = {}
    if started_at is not None:
        out["conversion_started_at"] = started_at
    if completed_at is not None:
        out["conversion_completed_at"] = completed_at
    if target_path is not None:
        out["target_path"] = target_path
    return out


def update_depgraph_keys(
    *,
    topo_level: int,
    cycle_group_id: int,
    status: str,
) -> dict[str, Any]:
    """Build the keys subset written by ``stamp-tombstones``."""
    return {
        "topo_level": topo_level,
        "cycle_group_id": cycle_group_id,
        "status": status,
    }


def write_tombstone_with_extras(
    tombstones_root: Path,
    rel_path: str,
    extras: dict[str, Any],
) -> Optional[Path]:
    """Merge ``extras`` into an existing tombstone's frontmatter and write back.

    If the tombstone does not exist on disk, returns ``None`` (no write).
    Existing first-eight keys are preserved verbatim; only the appended
    feature-015 keys are touched by this helper.

    Note: ``extras`` values of ``None`` are preserved and emitted as YAML
    ``null`` — distinguishing "field exists but unset" (e.g. in-progress
    ``conversion_completed_at``) from "field absent" (no key in input).
    """
    target = tombstone_path(tombstones_root, rel_path)
    if not target.is_file():
        return None

    fm = read_tombstone(target)

    merged: dict[str, Any] = {}
    # Preserve the original eight keys verbatim.
    for key in ("path", "name", "purpose", "key_idea", "dependencies",
                "callers", "mtime", "sha256"):
        if key in fm:
            merged[key] = fm[key]
    # Carry forward any previously stamped feature-015 keys, then overlay
    # the new extras (so the most recent values win).
    for key in ("topo_level", "cycle_group_id", "status",
                "conversion_started_at", "conversion_completed_at",
                "target_path"):
        if key in fm:
            merged[key] = fm[key]
    for key, value in extras.items():
        merged[key] = value

    return write_tombstone(tombstones_root, rel_path, merged)


__all__ = [
    "update_conversion_keys",
    "update_depgraph_keys",
    "write_tombstone_with_extras",
]
