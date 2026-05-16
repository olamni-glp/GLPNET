"""Canonical JSON writer for ``.codeconv/depgraph.json``.

Per ``specs/015-codeconv-depgraph/contracts/depgraph_cli.md`` § "JSON
output shape":

- Top-level key order: ``schema_version``, ``metadata``, ``ready``, ``files``
  (Python 3.7+ preserves dict insertion order; ``json.dumps`` respects it
  when ``sort_keys=False``).
- Inside ``metadata`` and each ``files[]`` row, keys are sorted alphabetically.
- The ``ready`` array is sorted lex.
- The ``files`` array is sorted by ``(topo_level ASC, cycle_group_id ASC, path ASC)``.
- ``depends_on`` / ``depended_on_by`` arrays are sorted lex.

Atomic write: temp file + ``os.replace`` to avoid partial files on crash.
"""

from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any


SCHEMA_VERSION = 1


def write_depgraph_json(
    out_path: Path,
    *,
    metadata: dict[str, Any],
    file_rows: list[dict[str, Any]],
) -> Path:
    """Write the depgraph JSON artefact to ``out_path`` (atomic)."""
    out_path = Path(out_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)

    payload = build_payload(metadata=metadata, file_rows=file_rows)
    body = json.dumps(payload, indent=2, sort_keys=False, default=str) + "\n"

    tmp_path = out_path.with_suffix(out_path.suffix + ".tmp")
    tmp_path.write_text(body, encoding="utf-8")
    os.replace(tmp_path, out_path)
    return out_path


def build_payload(
    *,
    metadata: dict[str, Any],
    file_rows: list[dict[str, Any]],
) -> dict[str, Any]:
    """Build the canonical depgraph payload dict.

    Top-level key order is preserved by inserting in this exact sequence:
    ``schema_version``, ``metadata``, ``ready``, ``files``. Inner dicts are
    sorted alphabetically.
    """
    ready_paths = sorted(
        row["path"] for row in file_rows if row.get("status") == "ready"
    )

    sorted_rows = sorted(
        file_rows,
        key=lambda r: (r["topo_level"], r["cycle_group_id"], r["path"]),
    )

    canonical_files: list[dict[str, Any]] = []
    for row in sorted_rows:
        canonical_files.append(
            _sorted_dict(
                {
                    "conversion_completed_at": row.get("conversion_completed_at"),
                    "conversion_started_at": row.get("conversion_started_at"),
                    "cycle_group_id": row["cycle_group_id"],
                    "depended_on_by": sorted(row.get("depended_on_by", [])),
                    "depends_on": sorted(row.get("depends_on", [])),
                    "path": row["path"],
                    "ready": row["ready"],
                    "status": row["status"],
                    "target_path": row.get("target_path"),
                    "topo_level": row["topo_level"],
                }
            )
        )

    canonical_metadata = _sorted_dict(metadata)

    payload: dict[str, Any] = {}
    payload["schema_version"] = SCHEMA_VERSION
    payload["metadata"] = canonical_metadata
    payload["ready"] = ready_paths
    payload["files"] = canonical_files
    return payload


def _sorted_dict(d: dict[str, Any]) -> dict[str, Any]:
    return {k: d[k] for k in sorted(d)}


__all__ = ["SCHEMA_VERSION", "build_payload", "write_depgraph_json"]
