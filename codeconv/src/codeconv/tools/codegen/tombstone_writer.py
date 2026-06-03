"""Tombstone read-modify-write helpers for codeconv-codegen.

Implements ``specs/019-codeconv-codegen/contracts/codegen_schema.md`` §
"Tombstone keys (append-only)". Five codegen-state keys are written
AFTER feature-018's six (``_FIELD_ORDER`` was extended in
``codeconv.tools.discover.tombstone``):

- ``codegen_started_at``              (``ingest`` phase-1)
- ``codegen_completed_at``            (``ingest`` phase-2; null until built)
- ``target_cs_path``                  (the produced ``out/csharp/<rel>.cs``)
- ``build_status``                    (``pass``｜``fail``｜``not_built``)
- ``codegen_open_escalation_count``   (integer >= 0)

All writes go through feature-012's canonical YAML emitter
(``write_tombstone``) so byte-for-byte idempotence carries forward.
Artefact *content* (the .cs) is NEVER mirrored into YAML — only codegen
*state*. EVERY previously-stamped appended key (features 015/017/018) is
carried forward on each write so no prior state is silently dropped
(data-model round-trip).
"""

from __future__ import annotations

from pathlib import Path
from typing import Any, Optional

from codeconv.tools.discover.tombstone import (
    _FEATURE_019_KEYS,
    _PRESERVED_APPENDED_KEYS,
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

# Appended keys NOT authored by codegen — carried forward verbatim. This
# is every preserved appended key except codegen's own five (which this
# writer overlays from ``extras``).
_NON_CODEGEN_APPENDED = tuple(
    k for k in _PRESERVED_APPENDED_KEYS if k not in _FEATURE_019_KEYS
)


def codegen_started_keys(*, codegen_started_at: str) -> dict[str, Any]:
    """Keys written at ``ingest`` phase-1 (only ``codegen_started_at``).

    Phase-1 records that codegen began; the build/target/escalation keys
    are written by phase-2 (``codegen_completed_keys``). Only the
    explicitly-passed key is included so a later phase-2 write does not
    depend on stamp ordering (mirrors planagents ``plan_started_keys``).
    """
    return {"codegen_started_at": codegen_started_at}


def codegen_completed_keys(
    *,
    completed_at: str,
    target_cs_path: Optional[str],
    build_status: str,
    open_escalation_count: int,
) -> dict[str, Any]:
    """Keys written at ``ingest`` phase-2 (terminal, only on build pass).

    ``target_cs_path`` may be ``None`` (emitted as YAML ``null``).
    ``build_status`` is one of ``pass｜fail｜not_built``.
    ``open_escalation_count`` is always an int >= 0.
    """
    return {
        "codegen_completed_at": completed_at,
        "target_cs_path": target_cs_path,
        "build_status": build_status,
        "codegen_open_escalation_count": int(open_escalation_count),
    }


def codegen_no_emit_keys(*, reason: Optional[str] = None) -> dict[str, Any]:
    """Keys written by ``mark-no-emit`` (Stage 4).

    Writes ``codegen_no_emit: true`` and — when supplied — the free-text
    ``codegen_no_emit_reason``. When ``reason`` is None the reason key is
    OMITTED (not emitted as null), so a tombstone marked without a reason
    stays byte-minimal. These keys are append-only (after the feature-020
    equiv keys) and carried forward verbatim by every non-no-emit writer.
    """
    out: dict[str, Any] = {"codegen_no_emit": True}
    if reason is not None:
        out["codegen_no_emit_reason"] = reason
    return out


def stamp_keys(
    *,
    codegen_started_at: Optional[str],
    codegen_completed_at: Optional[str],
    target_cs_path: Optional[str],
    build_status: Optional[str],
    open_escalation_count: Optional[int],
    row_exists: bool,
) -> dict[str, Any]:
    """Build the five-key subset written by ``stamp-tombstones``.

    Null-vs-missing convention (identical to features 015/017):

    - ``row_exists is False`` ⇒ NO ``dart_codegen`` row ⇒ all five keys
      are OMITTED (return ``{}``) so the tombstone is byte-identical to a
      never-codegen'd one.
    - ``row_exists is True`` ⇒ the keys are PRESENT;
      ``codegen_completed_at`` / ``target_cs_path`` / ``build_status``
      are present-with-``null`` when the column is NULL (in-progress),
      distinguishing "no codegen record" from "record exists but
      incomplete".
    """
    if not row_exists:
        return {}
    return {
        "codegen_started_at": codegen_started_at,
        "codegen_completed_at": codegen_completed_at,
        "target_cs_path": target_cs_path,
        "build_status": build_status,
        "codegen_open_escalation_count": (
            int(open_escalation_count)
            if open_escalation_count is not None
            else 0
        ),
    }


def write_tombstone_with_codegen_keys(
    tombstones_root: Path,
    rel_path: str,
    extras: dict[str, Any],
) -> Optional[Path]:
    """Merge ``extras`` (codegen keys) into an existing tombstone.

    Preserves the original eight keys verbatim, carries forward every
    non-codegen appended key (features 015/017/018) so no prior state is
    erased, then overlays ``extras`` (most-recent codegen values win). If
    the tombstone does not exist on disk, returns ``None`` (no write —
    same contract as the planagents/feature-015 writers). Emission order
    + idempotence are guaranteed by ``write_tombstone`` (``_FIELD_ORDER``
    is append-only).
    """
    target = tombstone_path(tombstones_root, rel_path)
    if not target.is_file():
        return None

    fm = read_tombstone(target)

    merged: dict[str, Any] = {}
    for key in _BASE_KEYS:
        if key in fm:
            merged[key] = fm[key]
    for key in _NON_CODEGEN_APPENDED:
        if key in fm:
            merged[key] = fm[key]
    for key, value in extras.items():
        merged[key] = value

    return write_tombstone(tombstones_root, rel_path, merged)


def read_codegen_keys(tombstones_root: Path, rel_path: str) -> dict[str, Any]:
    """Read the five codegen-state keys from a tombstone (for rebuild).

    Returns a dict containing only the feature-019 keys present in the
    frontmatter (a YAML-``null`` value is preserved as ``None``; an
    absent key is simply not in the returned dict — the null-vs-missing
    distinction a rebuilder needs).
    """
    target = tombstone_path(tombstones_root, rel_path)
    if not target.is_file():
        return {}
    fm = read_tombstone(target)
    return {k: fm[k] for k in _FEATURE_019_KEYS if k in fm}


__all__ = [
    "codegen_completed_keys",
    "codegen_no_emit_keys",
    "codegen_started_keys",
    "read_codegen_keys",
    "stamp_keys",
    "write_tombstone_with_codegen_keys",
]
