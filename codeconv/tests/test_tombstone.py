"""Tests for ``codeconv.tools.discover.tombstone`` — Phase 6 / US4 / T062.

Maps to ``specs/012-codeconv-runner/contracts/tombstone_format.md``:

- Field ordering: ``path, name, purpose, key_idea, dependencies,
  callers, mtime, sha256``.
- POSIX path separators in YAML always (R7).
- Lists sorted lexically.
- Round-trip: write → read returns identical fields.
"""

from __future__ import annotations

from pathlib import Path

import pytest

from codeconv.tools.discover.tombstone import (
    read_tombstone,
    write_tombstone,
)


def _fields(**overrides):
    """Default field set — every required key present."""
    base = dict(
        path="runtime/cell.dart",
        name="cell.dart",
        purpose="Heap cell.\n\nBidirectional pair.\n",
        key_idea="Heap cell.\n\nBidirectional pair.\n",
        dependencies=["runtime/tag.dart", "bytecode/opcode.dart"],
        callers=["runtime/runner.dart"],
        mtime="2026-04-30T11:14:22.000Z",
        sha256="7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b",
    )
    base.update(overrides)
    return base


def test_write_then_read_roundtrip(tmp_path: Path) -> None:
    fields = _fields()
    write_tombstone(tmp_path, "runtime/cell.dart", fields)

    tomb_path = tmp_path / "runtime" / "cell.dart.md"
    assert tomb_path.is_file()

    parsed = read_tombstone(tomb_path)
    # Keys present + values equal (lists must be sorted by writer).
    expected = dict(fields)
    expected["dependencies"] = sorted(expected["dependencies"])
    expected["callers"] = sorted(expected["callers"])
    for key in (
        "path",
        "name",
        "purpose",
        "key_idea",
        "dependencies",
        "callers",
        "mtime",
        "sha256",
    ):
        assert parsed[key] == expected[key], f"mismatch on {key}"


def test_yaml_field_ordering_stable(tmp_path: Path) -> None:
    """Frontmatter keys are emitted in the contract-specified order."""
    fields = _fields()
    write_tombstone(tmp_path, "runtime/cell.dart", fields)

    text = (tmp_path / "runtime" / "cell.dart.md").read_text(encoding="utf-8")
    # Capture frontmatter between the first two '---' lines.
    parts = text.split("---", 2)
    assert len(parts) >= 3, "expected '---' frontmatter delimiters"
    yaml_block = parts[1]

    expected_order = [
        "path:",
        "name:",
        "purpose:",
        "key_idea:",
        "dependencies:",
        "callers:",
        "mtime:",
        "sha256:",
    ]
    positions = [yaml_block.find(k) for k in expected_order]
    assert all(p >= 0 for p in positions), (
        f"missing top-level keys: "
        f"{[k for k, p in zip(expected_order, positions) if p < 0]}"
    )
    assert positions == sorted(positions), (
        f"frontmatter keys out of order: "
        f"{list(zip(expected_order, positions))}"
    )


def test_path_uses_posix_separators(tmp_path: Path) -> None:
    """Even on Windows, path values in YAML must be POSIX (R7)."""
    fields = _fields(
        path="runtime/sub/deep/cell.dart",
        dependencies=["runtime/tag.dart", "bytecode/opcode.dart"],
        callers=["runtime/sub/x.dart"],
    )
    write_tombstone(tmp_path, "runtime/sub/deep/cell.dart", fields)

    text = (tmp_path / "runtime" / "sub" / "deep" / "cell.dart.md").read_text(
        encoding="utf-8"
    )
    # The YAML must contain forward slashes in path values; no backslashes
    # in any path-like field.
    assert "runtime/sub/deep/cell.dart" in text
    assert "\\" not in text, (
        "tombstone YAML must not contain backslashes (R7)"
    )


def test_dependencies_sorted_lexically(tmp_path: Path) -> None:
    """Even if caller passes unsorted lists, the writer sorts them — diff
    stability across runs (R7 / contracts/tombstone_format.md § Diff
    stability)."""
    fields = _fields(
        dependencies=["z/last.dart", "a/first.dart", "m/mid.dart"],
        callers=["z/caller_z.dart", "a/caller_a.dart"],
    )
    write_tombstone(tmp_path, "runtime/cell.dart", fields)
    parsed = read_tombstone(tmp_path / "runtime" / "cell.dart.md")
    assert parsed["dependencies"] == [
        "a/first.dart",
        "m/mid.dart",
        "z/last.dart",
    ]
    assert parsed["callers"] == ["a/caller_a.dart", "z/caller_z.dart"]
