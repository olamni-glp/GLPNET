"""Dart source-side hooks for the ``dart_csharp`` language pair.

Source of truth: ``specs/016-codeconv-init-scaffold-langpair/contracts/langpair_plugin_contract.md``
behavioural requirement 1 — these hooks MUST be **byte-faithful** to the
pre-016 Dart path so the feature-012/014/015 discover suites stay green
(FR-023 / SC-005). They are the regression oracle.

Per research R2 + DISCIPLINE §1.3 (single source of truth, never
duplicate a parser): this module **delegates** to the existing,
already-tested implementations in
``codeconv.tools.discover.{walker,parse,pubspec}`` rather than copying
them. Copying would guarantee drift between two inventories of "the same"
logic — the exact anti-pattern §1.3 forbids. ``tools/discover`` becomes
pair-generic by calling these hooks; the implementations physically stay
where they are proven, re-exported here so the langpair surface is the
single import point for a stage tool.
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional

# Byte-faithful delegation to the proven feature-012/014 implementations.
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


# Dart source extension(s). A single ``.dart`` extension today.
SOURCE_EXTENSIONS: tuple[str, ...] = (".dart",)

# Recommended tool-exclusion globs for the Dart side, derived from the
# discover walker's own exclusion semantics so init's recorded exclusions
# and discover's pruning agree exactly (FR-007). ``<seg>/`` denotes a
# directory segment the walker prunes (``.dart_tool``, ``build``);
# ``*.<suf>`` the generated-artefact filename suffixes the walker skips.
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
