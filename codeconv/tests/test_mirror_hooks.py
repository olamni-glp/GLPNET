"""Unit tests for the Dart→C# mirror hooks — Feature 016 / spec
Amendment 1 (FR-039).

Pure / no-bridge: the mirror hooks (``mirror_prune_segments``,
``preserved_source_suffix``, ``companion_extensions``,
``companion_stub_comment``, ``tracker_filename``) are filesystem-free,
so these assert exact values + negative controls without ``@needs_bridge``
(langpair_plugin_contract.md behavioural requirement 5).
"""

from __future__ import annotations

from codeconv import langpairs


def _pair():
    return langpairs.get("dart", "csharp")


def test_mirror_prune_segments_standard_set() -> None:
    # spec-001 set EXTENDED (owner 2026-05-17): build/archive/backup
    # pruned as standard. Order is fixed.
    assert _pair().mirror_prune_segments() == (
        ".dart_tool",
        "build",
        "archive",
        "backup",
        ".git",
        ".idea",
        ".vscode",
    )
    for seg in ("build", "archive", "backup"):
        assert seg in _pair().mirror_prune_segments()


def test_preserved_source_suffix_is_empty_option1() -> None:
    # FR-032 Option 1: dart_csharp mirrors the source verbatim as .dart
    # (no .src rename) so codeconv `discover` inventories it.
    assert _pair().preserved_source_suffix() == ""


def test_companion_extensions_are_the_nine_in_fixed_order() -> None:
    # Order is contractually fixed for deterministic tracker records.
    assert _pair().companion_extensions() == (
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


def test_tracker_filename_literal_for_spec001_fidelity() -> None:
    assert _pair().tracker_filename() == "d2net-tracker.json"


def test_companion_stub_comment_is_single_cstyle_todo_line() -> None:
    p = _pair()
    c = p.companion_stub_comment(".cs", "runner.dart")
    assert c.startswith("// TODO:")
    assert "\n" not in c
    assert "runner.dart" in c
    # Category named for the .cs artefact.
    assert "cs" in c


def test_companion_stub_comment_covers_every_companion_ext() -> None:
    p = _pair()
    for ext in p.companion_extensions():
        c = p.companion_stub_comment(ext, "foo.bar.dart")
        assert c.startswith("// TODO:")
        assert "foo.bar.dart" in c
        assert ext.lstrip(".") in c


def test_companion_stub_comment_negative_unknown_ext_is_still_wellformed() -> (
    None
):
    # A non-registered ext must not crash and must still be a single
    # C-style TODO line (negative control).
    c = _pair().companion_stub_comment(".zzz", "x.dart")
    assert c.startswith("// TODO:")
    assert "\n" not in c
    assert "x.dart" in c


def test_source_extensions_still_dart_only() -> None:
    # The mirror reuses the existing source-side hook; negative control:
    # a C# file name is NOT a source file.
    exts = _pair().source_extensions()
    assert exts == (".dart",)
    assert any("heap.dart".endswith(e) for e in exts)
    assert not any("heap.cs".endswith(e) for e in exts)
