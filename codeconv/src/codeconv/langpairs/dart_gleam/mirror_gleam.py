"""Dart→Gleam mirror-side hooks for the ``dart_gleam`` language pair.

Source of truth:
``specs/032-codeconv-gleam-langpair/contracts/dart_gleam_hooks.md`` +
``data-model.md`` (mirror side) over the 016 base contract
(``langpair_plugin_contract.md`` behavioural requirement 5), which itself
reproduces spec ``001-d2net-scaffold`` FR-002/FR-004/FR-005/FR-006/FR-007.

These values mirror the Dart→C# pair (``dart_csharp.mirror_dart``) with
the Gleam target substituted (FR-004 / spec Assumptions):

- **prune set**: identical to ``dart_csharp`` — these prune the *Dart
  source* tree (a source-side concern, the same for either target).
- **preserved-source suffix**: ``""`` (verbatim mirror) — same reason as
  ``dart_csharp``: codeconv ``discover`` detects source by the ``.dart``
  extension, so a renamed copy would be invisible (016 Amendment 1 /
  FR-032 Option 1).
- **companion set**: the nine companions with ``.gleam`` in place of the
  C# ``.cs`` (order fixed for deterministic tracker records — FR-010).
- **companion stub comment**: a Gleam ``//`` line comment (Gleam has no
  block comment; the ``// TODO:`` one-line form is already Gleam-legal —
  only the ``.gleam`` category label differs from ``dart_csharp``).
- **tracker filename**: ``codeconv-gleam-tracker.json`` (pair-defined; the
  C# pair keeps the legacy ``d2net-tracker.json`` literal for fidelity, so
  the Gleam pair chooses its own — spec Assumptions / research.md R-002).

Pure / side-effect-free (no filesystem, DB, bridge, network) so the unit
tests need no ``@needs_bridge`` (FR-009).
"""

from __future__ import annotations

# Prune set for the MIRROR walk — IDENTICAL to dart_csharp (these prune
# the Dart source tree: a source-side concern, the same for either
# target). ``build``/``archive``/``backup`` are pruned as standard so the
# feature-015 depgraph referential check does not reject dangling archive
# imports. NOT discover's walker ``_EXCLUDED_SEGMENTS`` (different purpose).
MIRROR_PRUNE_SEGMENTS: tuple[str, ...] = (
    ".dart_tool",
    "build",
    "archive",
    "backup",
    ".git",
    ".idea",
    ".vscode",
)

# Verbatim mirror (empty suffix) — same Option-1 reason as dart_csharp:
# the preserved copy keeps its original ``.dart`` name so codeconv
# ``discover`` (``.dart``-based) inventories it (016 Amendment 1 / FR-032).
PRESERVED_SOURCE_SUFFIX: str = ""

# The nine companion artefacts — the dart_csharp set with ``.gleam`` in
# place of ``.cs``. Order is fixed so tracker records are deterministic
# (FR-010).
COMPANION_EXTENSIONS: tuple[str, ...] = (
    ".gleam",
    ".ana",
    ".tst",
    ".con",
    ".dep",
    ".cgn",
    ".iss",
    ".sta",
    ".ver",
)

# Pair-defined root tracker filename (de-branded provenance prefix). The
# C# pair keeps the legacy ``d2net-tracker.json`` literal for fidelity;
# the Gleam pair chooses its own (spec Assumptions / research.md R-002).
TRACKER_FILENAME: str = "codeconv-gleam-tracker.json"

# Human-readable category per companion extension (for the FR-006 stub
# comment). Keys are the extensions WITHOUT the leading dot. Identical to
# dart_csharp except the target companion is ``gleam`` -> "Gleam source".
_CATEGORY: dict[str, str] = {
    "gleam": "Gleam source",
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
    """Return the mirror-walk prune set (same as dart_csharp)."""
    return MIRROR_PRUNE_SEGMENTS


def preserved_source_suffix() -> str:
    """Return the preserved-source suffix (``""`` — verbatim mirror)."""
    return PRESERVED_SOURCE_SUFFIX


def companion_extensions() -> tuple[str, ...]:
    """Return the nine companion-artifact extensions (``.gleam`` lead)."""
    return COMPANION_EXTENSIONS


def companion_stub_comment(companion_ext: str, source_basename: str) -> str:
    """Return the single-line Gleam ``//`` stub body (spec-001 FR-006).

    Names the artefact category and the originating source file, e.g.
    ``// TODO: Gleam source (gleam) — port from runner.dart``. ``//`` is
    the Gleam line comment (Gleam has no block comment), so the
    dart_csharp one-line form is reused verbatim — only the ``.gleam``
    category label differs.
    """
    ext = companion_ext[1:] if companion_ext.startswith(".") else companion_ext
    category = _CATEGORY.get(ext, ext)
    return f"// TODO: {category} ({ext}) — port from {source_basename}"


def tracker_filename() -> str:
    """Return the pair-defined root tracker filename."""
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
