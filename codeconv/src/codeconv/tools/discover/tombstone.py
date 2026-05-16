"""Tombstone read/write for `/codeconv-discover` — Phase 6 / US4 / T071.

Per ``specs/012-codeconv-runner/contracts/tombstone_format.md``:

- Layout: ``<tombstones_root>/<rel-path>.dart.md`` mirroring the source
  tree exactly (directories created on demand).
- Frontmatter key order is fixed:
  ``path, name, purpose, key_idea, dependencies, callers, mtime, sha256``.
- POSIX path separators always (R7); lists sorted lexically.
- Body contains the verbatim ``purpose`` text (or empty) so engineers
  can read tombstones as Markdown; ``--from-tombstones`` consumes only
  the frontmatter.

Orphaned tombstones live under ``<tombstones_root>/.orphaned/<rel>.dart.md``
(FR-025). :func:`move_to_orphaned` and :func:`move_from_orphaned`
handle the two halves of the orphan / revival lifecycle.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any, Mapping

import yaml


# Field order pinned by ``contracts/tombstone_format.md`` § Diff stability.
# Feature 015 (codeconv-depgraph) appends six keys at the end — the
# position of every existing key is unchanged so feature-012/-014
# idempotence tests continue to produce identical first-eight-key bytes.
_FIELD_ORDER: tuple[str, ...] = (
    "path",
    "name",
    "purpose",
    "key_idea",
    "dependencies",
    "callers",
    "mtime",
    "sha256",
    # --- feature 015 (codeconv-depgraph) appended fields ---
    "topo_level",
    "cycle_group_id",
    "status",
    "conversion_started_at",
    "conversion_completed_at",
    "target_path",
)

# The feature-015 appended keys, as a set, for round-trip preservation.
# `discover` (a feature-012 caller) MUST carry these forward unchanged when
# it re-writes a tombstone it did not author, or a re-discover after
# `stamp-tombstones` would silently erase depgraph + conversion state.
_FEATURE_015_KEYS: tuple[str, ...] = (
    "topo_level",
    "cycle_group_id",
    "status",
    "conversion_started_at",
    "conversion_completed_at",
    "target_path",
)

# YAML emitter settings pinned for diff stability.
_YAML_DUMP_KWARGS: dict[str, Any] = dict(
    default_flow_style=False,
    sort_keys=False,
    allow_unicode=True,
    width=10000,
)


def tombstone_path(tombstones_root: Path, rel_path: str) -> Path:
    """Return the on-disk tombstone path for a given subtree-relative
    ``.dart`` path.

    ``rel_path`` is POSIX-style (e.g. ``runtime/cell.dart``); the
    on-disk path uses the OS native separator.
    """
    return tombstones_root / Path(rel_path.replace("/", "/") + ".md")


def orphan_path(tombstones_root: Path, rel_path: str) -> Path:
    """Return the on-disk path for an orphaned tombstone."""
    return tombstones_root / ".orphaned" / Path(rel_path.replace("/", "/") + ".md")


def write_tombstone(
    tombstones_root: Path,
    rel_path: str,
    fields: Mapping[str, Any],
) -> Path:
    """Write a tombstone file for ``rel_path`` under ``tombstones_root``.

    Field order, list sorting, and POSIX path encoding are all enforced
    by this function so callers don't have to re-implement them.
    Returns the absolute path of the written tombstone.
    """
    target = tombstone_path(tombstones_root, rel_path)
    target.parent.mkdir(parents=True, exist_ok=True)

    canonical = _canonicalise(fields)
    yaml_text = _emit_yaml(canonical)
    body = canonical.get("purpose") or ""
    # Tombstone body: verbatim purpose with a single trailing newline.
    if body and not body.endswith("\n"):
        body = body + "\n"
    text = f"---\n{yaml_text}---\n\n{body}"

    target.write_text(text, encoding="utf-8")
    return target


def read_tombstone(path: Path) -> dict[str, Any]:
    """Parse a tombstone file and return its frontmatter as a dict."""
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---"):
        raise ValueError(f"tombstone missing frontmatter delimiter: {path}")
    parts = text.split("---", 2)
    if len(parts) < 3:
        raise ValueError(f"tombstone has only one '---' delimiter: {path}")
    yaml_block = parts[1]
    data = yaml.safe_load(yaml_block) or {}
    if not isinstance(data, dict):
        raise ValueError(f"tombstone frontmatter is not a mapping: {path}")
    return data


def move_to_orphaned(
    tombstones_root: Path, rel_path: str
) -> Path | None:
    """Move a live tombstone to the ``.orphaned/`` tree.

    Returns the destination path on success; ``None`` if the source
    tombstone wasn't present.
    """
    src = tombstone_path(tombstones_root, rel_path)
    if not src.is_file():
        return None
    dst = orphan_path(tombstones_root, rel_path)
    dst.parent.mkdir(parents=True, exist_ok=True)
    if dst.exists():
        dst.unlink()
    src.replace(dst)
    return dst


def move_from_orphaned(
    tombstones_root: Path, rel_path: str
) -> Path | None:
    """Move an orphaned tombstone back to the live tree.

    Returns the destination path on success; ``None`` if the orphan
    wasn't present.
    """
    src = orphan_path(tombstones_root, rel_path)
    if not src.is_file():
        return None
    dst = tombstone_path(tombstones_root, rel_path)
    dst.parent.mkdir(parents=True, exist_ok=True)
    if dst.exists():
        dst.unlink()
    src.replace(dst)
    return dst


# ---------------------------------------------------------------------------
# Internals
# ---------------------------------------------------------------------------


def _canonicalise(fields: Mapping[str, Any]) -> dict[str, Any]:
    """Coerce field dict into the contract's canonical shape.

    - Required keys present (with sensible defaults).
    - Lists sorted lexically.
    - Path-like fields use forward slashes.
    - Feature 015 appended keys (``topo_level``, ``cycle_group_id``,
      ``status``, ``conversion_started_at``, ``conversion_completed_at``)
      are passed through only when present in the input; ``None`` is
      preserved (emitted as YAML ``null``) per
      ``specs/015-codeconv-depgraph/contracts/tombstone_format_delta.md``.
    """
    out: dict[str, Any] = {}
    out["path"] = _to_posix(fields.get("path", ""))
    out["name"] = str(fields.get("name", ""))
    out["purpose"] = str(fields.get("purpose", ""))
    out["key_idea"] = str(fields.get("key_idea", ""))
    out["dependencies"] = sorted(_to_posix(p) for p in fields.get("dependencies") or [])
    out["callers"] = sorted(_to_posix(p) for p in fields.get("callers") or [])
    out["mtime"] = str(fields.get("mtime", ""))
    out["sha256"] = str(fields.get("sha256", ""))
    # Feature 015 — pass-through with null preservation (incl. target_path,
    # the 6th key added per codex spec-review P1-e).
    for key in _FEATURE_015_KEYS:
        if key in fields:
            out[key] = fields[key]
    return out


def merge_preserving_feature015(
    new_fields: Mapping[str, Any],
    existing_tombstone: Path | None,
) -> dict[str, Any]:
    """Return ``new_fields`` with the six feature-015 keys carried forward
    from ``existing_tombstone`` when the caller (discover) did not supply
    them.

    Fixes the discover key-stripping bug-risk: ``_process_one_file`` /
    ``_backfill_tombstone_callers`` rebuild an 8-key dict from freshly
    parsed ``.dart`` + DB edges; without this merge a ``/codeconv-discover``
    run after ``stamp-tombstones`` would drop every feature-015 key
    (depgraph + conversion state) from the tombstone, defeating the
    round-trip. A key the caller explicitly provides wins; otherwise the
    on-disk value is preserved verbatim (including YAML-``null``).
    """
    merged = dict(new_fields)
    if existing_tombstone is None or not existing_tombstone.is_file():
        return merged
    try:
        prior = read_tombstone(existing_tombstone)
    except (OSError, ValueError):
        return merged
    for key in _FEATURE_015_KEYS:
        if key not in merged and key in prior:
            merged[key] = prior[key]
    return merged


def _to_posix(value: Any) -> str:
    s = str(value)
    return s.replace("\\", "/")


def _emit_yaml(fields: Mapping[str, Any]) -> str:
    """Emit YAML in the contract's pinned key order."""
    out_chunks: list[str] = []
    for key in _FIELD_ORDER:
        if key not in fields:
            continue
        chunk = yaml.safe_dump({key: fields[key]}, **_YAML_DUMP_KWARGS)
        out_chunks.append(chunk)
    return "".join(out_chunks)


__all__ = [
    "merge_preserving_feature015",
    "move_from_orphaned",
    "move_to_orphaned",
    "orphan_path",
    "read_tombstone",
    "tombstone_path",
    "write_tombstone",
]
