"""Filesystem walker for `/codeconv-discover` — Phase 6 / US4 / T069.

Per ``specs/012-codeconv-runner/contracts/codeconv_discover_cli.md``
§ Subtree scope (FR-018):

- Walks ``<root>/**/*.dart``.
- Excludes paths containing a ``.dart_tool/`` or ``build/`` segment.
- Excludes ``*.g.dart``, ``*.freezed.dart``, ``*.gen.dart``.
- Does NOT follow symlinks pointing outside the subtree.

Yields ``(absolute_path, posix_relative_path)`` tuples. The relative
path is normalised to POSIX-style separators (R7) so it can be stored
in the DB and tombstone YAML without per-platform divergence.
"""

from __future__ import annotations

import os
from pathlib import Path
from typing import Iterator, Tuple

# Filename suffixes that mark generated Dart artefacts (FR-018).
_GENERATED_SUFFIXES: tuple[str, ...] = (
    ".g.dart",
    ".freezed.dart",
    ".gen.dart",
)

# Directory segments that mark "do not enter" subtrees (FR-018). Note
# segment-level matching: a directory literally named ``buildkit`` is
# fine; only ``build`` (exact) is excluded.
_EXCLUDED_SEGMENTS: frozenset[str] = frozenset({".dart_tool", "build"})


def walk_dart_files(
    root: Path,
) -> Iterator[Tuple[Path, str]]:
    """Yield each in-scope ``.dart`` file under ``root``.

    Returns ``(absolute_path, posix_relative_path)`` where the relative
    path is rooted at ``root`` and uses ``/`` as separator.
    """
    root = root.resolve()
    if not root.is_dir():
        return
    real_root = os.path.realpath(root)

    for dirpath, dirnames, filenames in os.walk(root, followlinks=False):
        # Prune excluded directories at this level so we don't descend.
        dirnames[:] = [d for d in dirnames if d not in _EXCLUDED_SEGMENTS]

        # Symlink dirs whose target lies outside the subtree must not be
        # followed. ``followlinks=False`` already prevents descent for
        # symlinks, but a symlinked DIR shows up in dirnames as a normal
        # name; we filter it explicitly.
        kept: list[str] = []
        for d in dirnames:
            full = Path(dirpath) / d
            if full.is_symlink():
                target = os.path.realpath(full)
                if not _is_under(target, real_root):
                    continue  # outward symlink — skip
            kept.append(d)
        dirnames[:] = kept

        for fname in filenames:
            if not fname.endswith(".dart"):
                continue
            if any(fname.endswith(suf) for suf in _GENERATED_SUFFIXES):
                continue
            abs_path = Path(dirpath) / fname
            # Skip symlink files whose target is outside the subtree.
            if abs_path.is_symlink():
                target = os.path.realpath(abs_path)
                if not _is_under(target, real_root):
                    continue
            try:
                rel = abs_path.resolve().relative_to(root)
            except ValueError:
                continue
            yield abs_path, rel.as_posix()


def _is_under(path: str, root: str) -> bool:
    """Return True iff ``path`` is the same as or a descendant of ``root``."""
    try:
        common = os.path.commonpath([os.path.normcase(path), os.path.normcase(root)])
    except ValueError:
        return False
    return common == os.path.normcase(root)


__all__ = ["walk_dart_files"]
