"""Pure tests for the codegen artefact module (supports T011).

``validate_generated`` is the INVERSE of the convspec rule: it REQUIRES
real C# and REJECTS prose-only files, leftover Dart, and empty stubs.
``produced_cs_rel`` maps a source ``.dart`` path to its
``out/csharp/<rel>.cs`` target via the langpair ``target_for``.
"""

from __future__ import annotations

from codeconv.tools.codegen.artefact import (
    DEFAULT_TARGET_ROOT,
    is_real_csharp,
    produced_cs_rel,
    validate_generated,
)


def test_produced_cs_rel_maps_dart_to_csharp() -> None:
    assert produced_cs_rel("runtime/cell.dart") == "out/csharp/runtime/cell.cs"
    assert produced_cs_rel("bin/main.dart") == "out/csharp/bin/main.cs"
    assert (
        produced_cs_rel("a/b.dart", "target/net")
        == "target/net/a/b.cs"
    )
    assert DEFAULT_TARGET_ROOT == "out/csharp"


def test_valid_real_csharp_accepted() -> None:
    src = (
        "namespace Demo;\n\n"
        "public sealed class Cell\n{\n"
        "    public int Value { get; init; }\n}\n"
    )
    assert validate_generated(src) == []
    assert is_real_csharp(src)


def test_empty_file_rejected() -> None:
    errs = validate_generated("   \n  \n")
    assert errs and "empty" in errs[0].lower()


def test_prose_only_rejected() -> None:
    md = (
        "# Conversion Spec for cell.dart\n\n"
        "This file describes the conversion.\n\n"
        "```csharp\npublic class Cell {}\n```\n"
    )
    errs = validate_generated(md)
    assert any("prose" in e for e in errs)


def test_leftover_dart_rejected() -> None:
    dart = (
        "import 'package:meta/meta.dart';\n\n"
        "class Cell { int value = 0; }\n"
    )
    errs = validate_generated(dart)
    assert any("Dart" in e for e in errs)


def test_lowercase_main_rejected_as_dart() -> None:
    dart = "void main() {\n  print('hi');\n}\n"
    errs = validate_generated(dart)
    assert any("main" in e for e in errs)


def test_no_construct_rejected_as_stub() -> None:
    stub = "// TODO: generate Cell\nusing System;\n"
    errs = validate_generated(stub)
    assert any("construct" in e for e in errs)


def test_expected_unit_must_be_present() -> None:
    src = "namespace Demo;\npublic class Cell {}\n"
    assert validate_generated(src, expected_units=["Cell"]) == []
    errs = validate_generated(src, expected_units=["Heap"])
    assert any("Heap" in e for e in errs)
