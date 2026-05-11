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


# ---------------------------------------------------------------------------
# Feature 014: self-package rewrite (T005-T010)
# ---------------------------------------------------------------------------


def test_resolves_self_package_imports(tmp_path: Path) -> None:
    """package:<name>/<rest> rewrites to lib/<rest> when package_name is set."""
    sub = _mk_subtree(tmp_path)
    src = sub / "lib" / "main.dart"
    src.write_text(
        "import 'package:glp_runtime/sub/b.dart';\n",
        encoding="utf-8",
    )
    edges = extract_imports(src, sub, package_name="glp_runtime")
    assert edges == ["lib/sub/b.dart"]


def test_external_package_imports_still_skipped(tmp_path: Path) -> None:
    """External package: targets (different name) and dart:/dart-ext: are
    still skipped silently when package_name is set."""
    sub = _mk_subtree(tmp_path)
    src = sub / "lib" / "main.dart"
    src.write_text(
        "import 'package:meta/meta.dart';\n"
        "import 'package:json_annotation/json_annotation.dart';\n"
        "import 'dart:io';\n"
        "import 'dart-ext:vm/process';\n",
        encoding="utf-8",
    )
    edges = extract_imports(src, sub, package_name="glp_runtime")
    assert edges == []


def test_self_package_when_package_name_none(tmp_path: Path) -> None:
    """With package_name=None (feature-012 fallback), every package: target
    is skipped — even one whose name 'looks like' the self package."""
    sub = _mk_subtree(tmp_path)
    src = sub / "lib" / "main.dart"
    src.write_text(
        "import 'package:glp_runtime/sub/b.dart';\n",
        encoding="utf-8",
    )
    edges = extract_imports(src, sub, package_name=None)
    assert edges == []
    # And the two-positional-arg form (existing callers) must also skip.
    edges2 = extract_imports(src, sub)
    assert edges2 == []


def test_self_package_dedup_against_relative(tmp_path: Path) -> None:
    """If a file imports the same target via both package: and relative
    forms, the dedup set collapses them to one row and the FR-019
    duplicate-import warning fires (FR-007)."""
    sub = _mk_subtree(tmp_path)
    src = sub / "lib" / "main.dart"
    # Both forms point at lib/sub/b.dart.
    src.write_text(
        "import 'package:glp_runtime/sub/b.dart';\n"
        "import 'sub/b.dart';\n",
        encoding="utf-8",
    )
    with warnings.catch_warnings(record=True) as caught:
        warnings.simplefilter("always")
        edges = extract_imports(src, sub, package_name="glp_runtime")
    assert edges == ["lib/sub/b.dart"]
    msgs = [str(w.message) for w in caught]
    assert any("duplicate" in m.lower() for m in msgs), (
        f"expected a duplicate-import warning when package: and relative "
        f"forms collide; got {msgs}"
    )


def test_malformed_self_package_skipped(tmp_path: Path) -> None:
    """`package:<name>/` (no rest) and `package:<name>` (no slash) are
    BOTH silently skipped — they cannot be rewritten."""
    sub = _mk_subtree(tmp_path)
    src = sub / "lib" / "main.dart"
    src.write_text(
        "import 'package:glp_runtime/';\n"
        "import 'package:glp_runtime';\n",
        encoding="utf-8",
    )
    with warnings.catch_warnings(record=True) as caught:
        warnings.simplefilter("always")
        edges = extract_imports(src, sub, package_name="glp_runtime")
    assert edges == []
    # And no warning is emitted for these malformed targets.
    assert [str(w.message) for w in caught] == []


def test_self_package_outside_lib_skipped(tmp_path: Path) -> None:
    """A self-package target that resolves outside the package's lib/
    root (e.g. via a `..` traversal in <rest>) is silently dropped — Dart
    convention says package: paths are always under <package>/lib/."""
    sub = _mk_subtree(tmp_path)
    src = sub / "lib" / "main.dart"
    # `..` escapes lib/ — resolves to <sub>/escape.dart which is inside
    # the subtree but OUTSIDE lib/. Per the parser_contract this is
    # treated as out-of-subtree-style and skipped silently.
    (sub / "escape.dart").write_text("class E {}\n", encoding="utf-8")
    src.write_text(
        "import 'package:glp_runtime/../escape.dart';\n",
        encoding="utf-8",
    )
    edges = extract_imports(src, sub, package_name="glp_runtime")
    # The rewritten resolved path is <sub>/escape.dart, which is NOT under
    # <sub>/lib/. Per the contract: "anchored at <subtree>/lib/<rest>".
    # `..` traversal that escapes lib/ produces a path outside lib/ and
    # therefore is silently skipped.
    assert "lib/escape.dart" not in edges
    # And the (only) edge candidate is escape.dart which is outside lib/,
    # so the result is empty.
    assert edges == []
