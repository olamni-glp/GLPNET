"""Tests for ``codeconv.tools.discover.parse`` — Phase 6 / US4 / T061.

Maps to research R11 (leading doc-comment) and R12 (import resolution):

- ``extract_leading_doc(path) -> str``: captures verbatim ``///`` block or
  ``/** */`` block at the top of the file (after blank lines / shebang).
  No doc-comment → empty string. 200-line cap (R11).
- ``extract_imports(path, subtree_root) -> list[str]``: regex over
  ``import 'X';`` / ``import "X";`` directives. Skips ``package:`` and
  ``dart:`` targets. Resolves relative paths against the file's dir.
  Records only imports that resolve INSIDE the subtree. Deduplicates
  with a warning on repeats (FR-019).
"""

from __future__ import annotations

import warnings
from pathlib import Path

import pytest

from codeconv.tools.discover.parse import extract_imports, extract_leading_doc


# ---------------------------------------------------------------------------
# Leading doc-comment extraction (R11)
# ---------------------------------------------------------------------------


def test_extracts_leading_doc_comment_triple_slash(tmp_path: Path) -> None:
    src = tmp_path / "a.dart"
    src.write_text(
        "/// First line of doc.\n"
        "///\n"
        "/// Second paragraph.\n"
        "library a;\n"
        "\n"
        "import 'b.dart';\n",
        encoding="utf-8",
    )
    doc = extract_leading_doc(src)
    assert "First line of doc." in doc
    assert "Second paragraph." in doc
    # Marker stripped per Dart convention.
    assert "///" not in doc


def test_extracts_leading_doc_comment_block(tmp_path: Path) -> None:
    src = tmp_path / "b.dart"
    src.write_text(
        "/**\n"
        " * Block-style doc.\n"
        " * Continues here.\n"
        " */\n"
        "class Foo {}\n",
        encoding="utf-8",
    )
    doc = extract_leading_doc(src)
    assert "Block-style doc." in doc
    assert "Continues here." in doc
    assert "/*" not in doc and "*/" not in doc


def test_no_doc_comment_returns_empty(tmp_path: Path) -> None:
    src = tmp_path / "c.dart"
    src.write_text(
        "library c;\n"
        "\n"
        "class Bar {}\n",
        encoding="utf-8",
    )
    assert extract_leading_doc(src) == ""


# ---------------------------------------------------------------------------
# Import resolution (R12)
# ---------------------------------------------------------------------------


def _mk_subtree(tmp_path: Path) -> Path:
    """Create a minimal in-subtree tree for import-resolution tests."""
    sub = tmp_path / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "a.dart").write_text("class A {}\n", encoding="utf-8")
    (sub / "lib" / "sub" / "b.dart").parent.mkdir(parents=True, exist_ok=True)
    (sub / "lib" / "sub" / "b.dart").write_text("class B {}\n", encoding="utf-8")
    return sub


def test_extracts_imports_relative(tmp_path: Path) -> None:
    sub = _mk_subtree(tmp_path)
    src = sub / "lib" / "main.dart"
    src.write_text(
        "import 'a.dart';\n"
        "import 'sub/b.dart';\n"
        "\n"
        "void main() {}\n",
        encoding="utf-8",
    )
    edges = extract_imports(src, sub)
    assert sorted(edges) == ["lib/a.dart", "lib/sub/b.dart"]


def test_skips_package_and_dart_imports(tmp_path: Path) -> None:
    sub = _mk_subtree(tmp_path)
    src = sub / "lib" / "main.dart"
    src.write_text(
        "import 'package:meta/meta.dart';\n"
        "import 'dart:io';\n"
        "import 'dart-ext:vm/process';\n"
        "import 'a.dart';\n",
        encoding="utf-8",
    )
    edges = extract_imports(src, sub)
    assert edges == ["lib/a.dart"]


def test_dedupes_duplicate_imports(tmp_path: Path) -> None:
    """Repeated ``import 'a.dart';`` is deduped with a warning (FR-019)."""
    sub = _mk_subtree(tmp_path)
    src = sub / "lib" / "main.dart"
    src.write_text(
        "import 'a.dart';\n"
        "import 'a.dart';\n",
        encoding="utf-8",
    )
    with warnings.catch_warnings(record=True) as caught:
        warnings.simplefilter("always")
        edges = extract_imports(src, sub)
    assert edges == ["lib/a.dart"]
    msgs = [str(w.message) for w in caught]
    assert any("duplicate" in m.lower() and "a.dart" in m for m in msgs), (
        f"expected a duplicate-import warning; got {msgs}"
    )


def test_outside_subtree_imports_dropped(tmp_path: Path) -> None:
    """A relative import that resolves OUTSIDE the subtree is silently
    dropped (callers query that case via the warning channel — see T066)."""
    sub = _mk_subtree(tmp_path)
    outside = tmp_path / "outside.dart"
    outside.write_text("class Outside {}\n", encoding="utf-8")
    src = sub / "lib" / "main.dart"
    src.write_text("import '../../outside.dart';\n", encoding="utf-8")

    edges = extract_imports(src, sub)
    assert edges == []
