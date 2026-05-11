"""Tests for ``codeconv.tools.discover.walker`` — Phase 6 / US4 / T060.

Maps to ``specs/012-codeconv-runner/contracts/codeconv_discover_cli.md``
§ Subtree scope (FR-018):

- Walks ``<root>/**/*.dart``.
- Excludes paths containing ``.dart_tool/`` or ``build/`` segments.
- Excludes ``*.g.dart``, ``*.freezed.dart``, ``*.gen.dart``.
- Does NOT follow symlinks pointing outside the subtree.

Walker yields ``(absolute_path, posix_relative_path)`` tuples.
"""

from __future__ import annotations

import os
import sys
from pathlib import Path

import pytest

from codeconv.tools.discover.walker import walk_dart_files


def _touch(p: Path, content: str = "") -> None:
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(content, encoding="utf-8")


def test_walks_dart_files_only(tmp_path: Path) -> None:
    """Only ``*.dart`` files are yielded; ``*.py``/``*.txt`` are ignored."""
    root = tmp_path / "src"
    _touch(root / "a.dart")
    _touch(root / "b.py", "print('not dart')")
    _touch(root / "notes.txt")
    _touch(root / "sub" / "c.dart")

    rels = sorted(rel for _, rel in walk_dart_files(root))
    assert rels == ["a.dart", "sub/c.dart"]


def test_excludes_generated(tmp_path: Path) -> None:
    """``*.g.dart``, ``*.freezed.dart``, ``*.gen.dart`` are excluded."""
    root = tmp_path / "src"
    _touch(root / "model.dart")
    _touch(root / "model.g.dart")
    _touch(root / "model.freezed.dart")
    _touch(root / "model.gen.dart")
    _touch(root / "thing.dart")

    rels = sorted(rel for _, rel in walk_dart_files(root))
    assert rels == ["model.dart", "thing.dart"]


def test_excludes_dart_tool_and_build(tmp_path: Path) -> None:
    """Any path with a ``.dart_tool/`` or ``build/`` segment is excluded."""
    root = tmp_path / "src"
    _touch(root / "lib" / "ok.dart")
    _touch(root / ".dart_tool" / "package_config.dart")
    _touch(root / "build" / "out.dart")
    _touch(root / "lib" / ".dart_tool" / "nested.dart")
    _touch(root / "lib" / "build" / "thing.dart")
    _touch(root / "lib" / "buildkit" / "kept.dart")  # NOT a 'build/' segment

    rels = sorted(rel for _, rel in walk_dart_files(root))
    assert rels == ["lib/buildkit/kept.dart", "lib/ok.dart"]


@pytest.mark.skipif(
    sys.platform == "win32" and not os.environ.get("CODECONV_ENABLE_SYMLINK_TESTS"),
    reason="symlink creation on Windows requires admin or developer mode; "
    "set CODECONV_ENABLE_SYMLINK_TESTS=1 to opt in.",
)
def test_does_not_follow_outward_symlinks(tmp_path: Path) -> None:
    """A symlink whose target lies outside the subtree is not followed."""
    root = tmp_path / "src"
    outside = tmp_path / "elsewhere"
    _touch(root / "inside.dart")
    _touch(outside / "outside.dart")

    link = root / "outward_link"
    try:
        link.symlink_to(outside, target_is_directory=True)
    except (OSError, NotImplementedError):
        pytest.skip("symlink creation unsupported on this platform")

    rels = sorted(rel for _, rel in walk_dart_files(root))
    assert rels == ["inside.dart"]
    # outside.dart MUST NOT be reached via the symlink.
    assert all("outside" not in r for r in rels)
