"""Unit tests for the language-pair registry + dart_csharp hooks.

Feature 016 / US3. Source of truth:
``specs/016-codeconv-init-scaffold-langpair/contracts/langpair_plugin_contract.md``
§ "Test obligations" 1–5 (spec FR-001..FR-005, FR-024, SC-003).

Pure unit — **NO bridge** (the hooks read the filesystem at most). The
registry is a process-wide singleton; tests that register a throwaway
pair clean up after themselves so ordering is irrelevant.
"""

from __future__ import annotations

import subprocess
from pathlib import Path

import pytest

from codeconv import langpairs
from codeconv.langpairs.base import LangPair, UnknownLangPair


REPO_ROOT = Path(__file__).resolve().parent.parent.parent


# ---------------------------------------------------------------------------
# Obligation 5 — a test-only second pair proving SC-003 (zero stage-tool edits)
# ---------------------------------------------------------------------------


class _FakeKotlinSwift(LangPair):
    """A trivial test-only second pair. Its existence + registration must
    require ZERO edits to any stage tool (init/discover/depgraph/scaffold)
    — the registry indirection from Phase 2 is the only seam."""

    def key(self) -> tuple[str, str]:
        return ("kotlin", "swift")

    def source_extensions(self) -> tuple[str, ...]:
        return (".kt",)

    def tool_exclusion_globs(self) -> tuple[str, ...]:
        return ("build/", ".gradle/")

    def read_package_name(self, subtree_root):
        return None, None

    def extract_imports(self, file_path, subtree_root, package_name):
        return []

    def extract_leading_doc(self, file_path) -> str:
        return ""

    def target_extension(self) -> str:
        return ".swift"

    def target_for(self, source_rel: str) -> str:
        rel = source_rel.replace("\\", "/")
        dot = rel.rfind(".")
        stem = rel[:dot] if dot > rel.rfind("/") else rel
        return stem + ".swift"

    def workdir_name(self, source_rel: str):
        return None


@pytest.fixture
def fake_pair_registered():
    """Register the test-only pair; unregister on teardown."""
    pair = _FakeKotlinSwift()
    langpairs.register(pair)
    try:
        yield pair
    finally:
        # Surgical cleanup of the process-wide registry.
        langpairs._REGISTRY.pop(("kotlin", "swift"), None)


# ---------------------------------------------------------------------------
# Obligation 1 — register / get / list_pairs happy path + (idempotent /
# conflicting) re-register
# ---------------------------------------------------------------------------


def test_registry_happy_path(fake_pair_registered) -> None:
    assert ("kotlin", "swift") in langpairs.list_pairs()
    got = langpairs.get("kotlin", "swift")
    assert got.key() == ("kotlin", "swift")
    # The production pair is always present (auto-imported).
    assert ("dart", "csharp") in langpairs.list_pairs()


def test_register_idempotent_same_type(fake_pair_registered) -> None:
    """Re-registering the same implementation type is a no-op (no raise)."""
    langpairs.register(_FakeKotlinSwift())
    assert langpairs.get("kotlin", "swift").key() == ("kotlin", "swift")


def test_register_conflicting_raises(fake_pair_registered) -> None:
    """A conflicting re-register (same key, different impl) raises."""

    class _OtherKotlinSwift(LangPair):
        def key(self):
            return ("kotlin", "swift")

        def source_extensions(self):
            return (".kt",)

        def tool_exclusion_globs(self):
            return ()

        def read_package_name(self, subtree_root):
            return None, None

        def extract_imports(self, file_path, subtree_root, package_name):
            return []

        def extract_leading_doc(self, file_path):
            return ""

        def target_extension(self):
            return ".swift"

        def target_for(self, source_rel):
            return source_rel

        def workdir_name(self, source_rel):
            return None

    with pytest.raises(ValueError, match="conflicting re-register"):
        langpairs.register(_OtherKotlinSwift())


def test_list_pairs_sorted(fake_pair_registered) -> None:
    pairs = langpairs.list_pairs()
    assert pairs == sorted(pairs)


# ---------------------------------------------------------------------------
# Obligation 2 — unregistered pair → UnknownLangPair (message names known)
# ---------------------------------------------------------------------------


def test_get_unregistered_raises_with_known() -> None:
    with pytest.raises(UnknownLangPair) as ei:
        langpairs.get("dart", "rust")
    msg = str(ei.value)
    assert "dart" in msg and "rust" in msg
    # The message must name the registered production pair.
    assert "dart->csharp" in msg


def test_unknown_langpair_lists_known_pairs() -> None:
    exc = UnknownLangPair("x", "y", [("dart", "csharp")])
    assert "x" in str(exc) and "y" in str(exc)
    assert "dart->csharp" in str(exc)


# ---------------------------------------------------------------------------
# Obligation 3 — dart_csharp hook exact values (positive) + negative controls
# ---------------------------------------------------------------------------


def test_dart_csharp_identity_and_extensions() -> None:
    p = langpairs.get("dart", "csharp")
    assert p.key() == ("dart", "csharp")
    assert p.source_extensions() == (".dart",)
    assert p.target_extension() == ".cs"


def test_dart_csharp_tool_exclusion_globs() -> None:
    p = langpairs.get("dart", "csharp")
    globs = p.tool_exclusion_globs()
    # Derived from discover's walker semantics (byte-faithful).
    assert ".dart_tool/" in globs
    assert "build/" in globs
    assert "*.g.dart" in globs
    assert "*.freezed.dart" in globs
    assert "*.gen.dart" in globs


def test_dart_csharp_target_for_positive() -> None:
    p = langpairs.get("dart", "csharp")
    assert p.target_for("lib/runtime/heap_fcp.dart") == "lib/runtime/heap_fcp.cs"
    assert p.target_for("bin/main.dart") == "bin/main.cs"
    # POSIX in / POSIX out even from a Windows-style input.
    assert p.target_for("lib\\a.dart") == "lib/a.cs"


def test_dart_csharp_target_for_negative_controls() -> None:
    p = langpairs.get("dart", "csharp")
    # A non-Dart extension is NOT special-cased — only the final
    # extension is swapped; a dotless name gets the extension appended.
    assert p.target_for("README") == "README.cs"
    # Directory structure is mirrored verbatim — no `..` collapsing,
    # no escaping the mirrored tree.
    assert p.target_for("a/../b.dart") == "a/../b.cs"


def test_dart_csharp_workdir_name() -> None:
    p = langpairs.get("dart", "csharp")
    assert p.workdir_name("lib/runtime/heap_fcp.dart") == "__heap_fcp"
    assert p.workdir_name("bin/main.dart") == "__main"
    # Never None for dart_csharp (always has a workdir convention).
    assert p.workdir_name("x.dart") is not None


# ---------------------------------------------------------------------------
# Obligation 4 — extract_imports / extract_leading_doc parity vs legacy
# tools/discover on a fixture
# ---------------------------------------------------------------------------


def test_extract_imports_parity_with_legacy(tmp_path: Path) -> None:
    """dart_csharp.extract_imports MUST equal the legacy
    tools/discover.parse.extract_imports on the same fixture
    (byte-faithful regression oracle — FR-023/SC-005)."""
    from codeconv.tools.discover.parse import (
        extract_imports as legacy_extract_imports,
    )

    sub = tmp_path / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "a.dart").write_text("class A {}\n", encoding="utf-8")
    target = sub / "lib" / "b.dart"
    target.write_text(
        "import 'a.dart';\nimport 'dart:core';\n"
        "import 'package:flutter/x.dart';\nclass B {}\n",
        encoding="utf-8",
    )

    p = langpairs.get("dart", "csharp")
    via_pair = p.extract_imports(target, sub, None)
    via_legacy = legacy_extract_imports(target, sub, None)
    assert via_pair == via_legacy
    # Sanity: only the in-subtree relative import is captured.
    assert via_pair == ["lib/a.dart"]


def test_extract_leading_doc_parity_with_legacy(tmp_path: Path) -> None:
    """dart_csharp.extract_leading_doc MUST equal the legacy
    tools/discover.parse.extract_leading_doc on the same fixture."""
    from codeconv.tools.discover.parse import (
        extract_leading_doc as legacy_doc,
    )

    f = tmp_path / "c.dart"
    f.write_text(
        "/// Heap kernel.\n/// Second line.\nclass C {}\n",
        encoding="utf-8",
    )
    p = langpairs.get("dart", "csharp")
    assert p.extract_leading_doc(f) == legacy_doc(f)
    assert p.extract_leading_doc(f).startswith("Heap kernel.")


# ---------------------------------------------------------------------------
# Obligation 5 — SC-003: a new pair = new files, ZERO stage-tool source edits
# ---------------------------------------------------------------------------


def test_second_pair_selectable_without_stage_tool_edit(
    fake_pair_registered,
) -> None:
    """The test-only pair is registered + selectable purely via the
    registry; no stage-tool module had to be imported or edited to make
    it work (the indirection is the only seam)."""
    assert ("kotlin", "swift") in langpairs.list_pairs()
    sel = langpairs.get("kotlin", "swift")
    assert sel.target_extension() == ".swift"
    assert sel.target_for("src/Main.kt") == "src/Main.swift"
    # Resolvable through the same registry helper the stages use.
    assert langpairs.get(*sel.key()) is sel


def test_sc003_no_stage_tool_source_modified_by_this_feature() -> None:
    """SC-003: adding a pair edits only ``langpairs/`` (+ the one
    auto-import line). This feature's diff against the feature-015 base
    must not have *rewritten* any stage tool's core logic. We assert the
    stage-tool packages still import cleanly and expose their ``app`` —
    a structural proxy that no stage tool was broken/forked to add the
    pair mechanism (the registry is the only seam)."""
    import importlib

    for mod_name in (
        "codeconv.tools.init",
        "codeconv.tools.discover",
        "codeconv.tools.depgraph",
        "codeconv.tools.scaffold",
    ):
        mod = importlib.import_module(mod_name)
        assert hasattr(mod, "app"), f"{mod_name} lost its Typer app"

    # The discover tool sources its Dart-specifics via the registry
    # (Phase-2 genericisation), NOT by importing parse/pubspec directly
    # at module scope — proving the pair seam is the registry. (The
    # per-file parse hooks now come from the resolved pair.)
    import codeconv.tools.discover.workflow as dw

    assert hasattr(dw, "_active_source_pair"), (
        "discover must resolve its source hooks via the langpair registry"
    )
