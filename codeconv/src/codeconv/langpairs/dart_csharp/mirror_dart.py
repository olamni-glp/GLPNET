"""Dart→C# mirror-side hooks for the ``dart_csharp`` language pair.

Source of truth: ``specs/016-codeconv-init-scaffold-langpair/spec.md``
Amendment 1 (FR-027..FR-041, D7) + ``contracts/langpair_plugin_contract.md``
behavioural requirement 5, reproducing spec ``001-d2net-scaffold``
FR-002/FR-004/FR-005/FR-006/FR-007 values for the Dart→C# pair.

Pure / side-effect-free (no filesystem, DB, bridge, network) so the
registry unit tests need no ``@needs_bridge`` (FR-039).
"""

from __future__ import annotations

# spec-001 FR-002 prune set for the MIRROR walk, EXTENDED per spec
# Amendment 1 (owner decision 2026-05-17): build / archive / backup
# directories are pruned as standard (the old curated glp_runtime_net
# excluded bin/archive etc.; mirroring them carries dangling archive
# imports that the feature-015 depgraph referential check rejects). Not
# discover's walker ``_EXCLUDED_SEGMENTS`` (different purpose). This is
# the pair's STANDARD set; `codeconv init` can force-include a standard
# dir or add gitignore-style exclusions on top (Amendment 1 / FR-042..).
MIRROR_PRUNE_SEGMENTS: tuple[str, ...] = (
    ".dart_tool",
    "build",
    "archive",
    "backup",
    ".git",
    ".idea",
    ".vscode",
)

# spec Amendment 1 Option 1 (the ONE deliberate spec-001 deviation):
# spec-001 FR-004 renames ``foo.dart`` -> ``foo.dart.src``, but unlike
# standalone D2NET the codeconv pipeline runs ``discover`` AFTER mirror,
# and ``discover`` detects Dart source by the ``.dart`` extension. A
# ``.dart.src`` file would be invisible to ``discover`` (empty inventory
# -> dead pipeline). So for ``dart_csharp`` the preserved copy keeps the
# original ``.dart`` name (empty suffix = verbatim mirror); the 9
# companion stubs + the tracker are still produced (full substantive
# spec-001 behaviour). This deviation is documented in spec Amendment 1
# (FR-032) and contracts/codeconv_mirror_cli.md.
PRESERVED_SOURCE_SUFFIX: str = ""

# spec-001 FR-005: the nine companion artefacts. Order is fixed so
# tracker records (FR-034) are deterministic.
COMPANION_EXTENSIONS: tuple[str, ...] = (
    ".cs",
    ".ana",
    ".tst",
    ".con",
    ".dep",
    ".cgn",
    ".iss",
    ".sta",
    ".ver",
)

# spec-001 FR-007: literal name kept for behavioural fidelity even
# though the toolchain is otherwise de-branded (pair-defined hook).
TRACKER_FILENAME: str = "d2net-tracker.json"

# Human-readable category per companion extension (for the FR-006 stub
# comment). Keys are the extensions WITHOUT the leading dot.
_CATEGORY: dict[str, str] = {
    "cs": "C# source",
    "ana": "analysis",
    "tst": "tests",
    "con": "conversion notes",
    "dep": "dependencies",
    "cgn": "code-generation",
    "iss": "issues",
    "sta": "status",
    "ver": "verification",
}


def mirror_prune_segments() -> tuple[str, ...]:
    """Return the spec-001 FR-002 mirror-walk prune set."""
    return MIRROR_PRUNE_SEGMENTS


def preserved_source_suffix() -> str:
    """Return the preserved-source suffix.

    ``""`` for ``dart_csharp`` — Option 1: the source is mirrored
    verbatim under its original ``.dart`` name so codeconv ``discover``
    (``.dart``-based) inventories it. This is the single documented
    spec-001 FR-004 deviation (spec Amendment 1 / FR-032).
    """
    return PRESERVED_SOURCE_SUFFIX


def companion_extensions() -> tuple[str, ...]:
    """Return the nine companion-artifact extensions (spec-001 FR-005)."""
    return COMPANION_EXTENSIONS


def companion_stub_comment(companion_ext: str, source_basename: str) -> str:
    """Return the single-line ``// TODO:`` stub body (spec-001 FR-006).

    Names the artefact category and the originating source file, e.g.
    ``// TODO: C# source — port from runner.dart``.
    """
    ext = companion_ext[1:] if companion_ext.startswith(".") else companion_ext
    category = _CATEGORY.get(ext, ext)
    return f"// TODO: {category} ({ext}) — port from {source_basename}"


def tracker_filename() -> str:
    """Return the root tracker filename (spec-001 FR-007)."""
    return TRACKER_FILENAME


__all__ = [
    "COMPANION_EXTENSIONS",
    "MIRROR_PRUNE_SEGMENTS",
    "PRESERVED_SOURCE_SUFFIX",
    "TRACKER_FILENAME",
    "companion_extensions",
    "companion_stub_comment",
    "mirror_prune_segments",
    "preserved_source_suffix",
    "tracker_filename",
]
