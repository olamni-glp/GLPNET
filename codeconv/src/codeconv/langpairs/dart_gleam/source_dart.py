"""Dart source-side hooks for the ``dart_gleam`` language pair.

Source of truth:
``specs/032-codeconv-gleam-langpair/contracts/dart_gleam_hooks.md`` (source
side — identical-in-result to the Dart source side, FR-002) over the 016
base contract (``langpair_plugin_contract.md`` behavioural requirement 1).

Dart is the shared, authoritative source for BOTH the C# and the Gleam
targets, so these hooks MUST return results equal to ``dart_csharp``'s on
the same inputs. This module is therefore a thin, **independent** delegate
to the same single-source-of-truth implementations in
``codeconv.tools.discover.{walker,parse,pubspec}`` that
``dart_csharp.source_dart`` delegates to (research.md R-001). It
deliberately does NOT import ``dart_csharp.source_dart`` (that would couple
the two production pairs) and does NOT copy the parser logic (DISCIPLINE
§1.3 forbids duplicating a parser). The byte-faithfulness is structural:
both pairs call the same proven functions.

Pure / side-effect-free (filesystem read at most: no DB, bridge, network)
so the unit tests need no ``@needs_bridge`` (FR-009).
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional

# Byte-faithful delegation to the proven feature-012/014 implementations
# (the same single source of truth dart_csharp.source_dart delegates to).
from codeconv.tools.discover.parse import (
    extract_imports as _extract_imports,
)
from codeconv.tools.discover.parse import (
    extract_leading_doc as _extract_leading_doc,
)
from codeconv.tools.discover.pubspec import (
    read_package_name as _read_package_name,
)
from codeconv.tools.discover.walker import (
    _EXCLUDED_SEGMENTS,
    _GENERATED_SUFFIXES,
)


# Dart source extension(s). A single ``.dart`` extension today (identical
# to the Dart source side of dart_csharp — Dart is the shared source).
SOURCE_EXTENSIONS: tuple[str, ...] = (".dart",)

# Recommended tool-exclusion globs for the Dart side, derived from the
# discover walker's own exclusion semantics so init's recorded exclusions
# and discover's pruning agree exactly (FR-002 — identical to dart_csharp).
TOOL_EXCLUSION_GLOBS: tuple[str, ...] = tuple(
    [f"{seg}/" for seg in sorted(_EXCLUDED_SEGMENTS)]
    + [f"*{suf}" for suf in _GENERATED_SUFFIXES]
)


def source_extensions() -> tuple[str, ...]:
    """Return the Dart source extension(s)."""
    return SOURCE_EXTENSIONS


def tool_exclusion_globs() -> tuple[str, ...]:
    """Return the recommended Dart tool-exclusion globs."""
    return TOOL_EXCLUSION_GLOBS


def read_package_name(
    subtree_root: Path,
) -> tuple[Optional[str], Optional[dict]]:
    """Byte-faithful delegate to ``discover.pubspec.read_package_name``."""
    return _read_package_name(subtree_root, repo_root=None)


def extract_imports(
    file_path: Path,
    subtree_root: Path,
    package_name: Optional[str],
) -> list[str]:
    """Byte-faithful delegate to ``discover.parse.extract_imports``."""
    return _extract_imports(file_path, subtree_root, package_name)


def extract_leading_doc(file_path: Path) -> str:
    """Byte-faithful delegate to ``discover.parse.extract_leading_doc``."""
    return _extract_leading_doc(file_path)


__all__ = [
    "SOURCE_EXTENSIONS",
    "TOOL_EXCLUSION_GLOBS",
    "extract_imports",
    "extract_leading_doc",
    "read_package_name",
    "source_extensions",
    "tool_exclusion_globs",
]
