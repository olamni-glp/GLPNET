"""Pubspec.yaml loader for `/codeconv-discover` — Feature 014 / FR-004 / FR-005.

One function: :func:`read_package_name`. Called exactly once per
``/codeconv-discover`` invocation by :func:`codeconv.tools.discover.workflow.run_discover`.

The result is cached in-memory for the lifetime of that single invocation
and threaded through the parser so every ``package:<self>/<rest>`` import
target can be rewritten to ``lib/<rest>`` before in-subtree resolution.
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional

import yaml


def read_package_name(
    subtree_root: Path,
    *,
    repo_root: Optional[Path] = None,
) -> tuple[Optional[str], Optional[dict]]:
    """Read the subtree's ``pubspec.yaml``; return ``(name, warning_or_None)``.

    Behaviour:

    - File absent           → ``(None, {"kind": "pubspec_missing", "path": ..., "reason": "absent"})``
    - ``yaml.YAMLError`` on load
      OR top-level is not a mapping
                            → ``(None, {"kind": "pubspec_missing", "path": ..., "reason": "unparseable"})``
    - Loads but no ``name:``
      OR ``name`` is non-string / empty
                            → ``(None, {"kind": "pubspec_missing", "path": ..., "reason": "no_name_field"})``
    - Happy path            → ``(name, None)``

    ``path`` in the warning is POSIX-relative against ``repo_root`` if
    ``repo_root`` is supplied AND the pubspec is under it; otherwise the
    absolute path string of the expected location.
    """
    expected = (subtree_root / "pubspec.yaml").resolve()

    def _path_for_warning() -> str:
        if repo_root is not None:
            try:
                return expected.relative_to(repo_root.resolve()).as_posix()
            except ValueError:
                pass
        return str(expected)

    if not expected.exists():
        return None, {
            "kind": "pubspec_missing",
            "path": _path_for_warning(),
            "reason": "absent",
        }

    try:
        text = expected.read_text(encoding="utf-8", errors="replace")
        data = yaml.safe_load(text)
    except (OSError, yaml.YAMLError):
        return None, {
            "kind": "pubspec_missing",
            "path": _path_for_warning(),
            "reason": "unparseable",
        }

    if not isinstance(data, dict):
        return None, {
            "kind": "pubspec_missing",
            "path": _path_for_warning(),
            "reason": "unparseable",
        }

    name = data.get("name")
    if not isinstance(name, str) or not name.strip():
        return None, {
            "kind": "pubspec_missing",
            "path": _path_for_warning(),
            "reason": "no_name_field",
        }

    return name.strip(), None


__all__ = ["read_package_name"]
