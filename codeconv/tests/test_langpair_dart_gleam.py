"""Unit tests for the ``dart_gleam`` language pair (feature 032).

Source of truth:
``specs/032-codeconv-gleam-langpair/contracts/dart_gleam_hooks.md``
§ "Test obligations" 1–8 (spec FR-001..FR-011, SC-002..SC-005) over the
016 base contract.

Pure unit — **NO bridge** (the hooks read the filesystem at most; the
registry/planner fakes below stand in for the DB). The registry is a
process-wide singleton; the production ``(dart, gleam)`` pair is
auto-imported, so no registration/cleanup is needed here.

R-003 owner ruling: **R3-b** — identity-preserving Gleam-segment
normalization PLUS one generic, pair-agnostic target-uniqueness assertion
in ``tools/scaffold/planner.py``. This module covers both the per-file
normalization legality (obligation 5) + the corpus no-collision guarantee
(obligation 8, R3-a half) AND the planner raising on a synthetic
colliding source set (obligation 8, R3-b half).
"""

from __future__ import annotations

import re
from pathlib import Path

import pytest

from codeconv import langpairs
from codeconv.langpairs.base import UnknownLangPair
from codeconv.langpairs.dart_gleam import target_gleam
from codeconv.tools.scaffold.planner import (
    TargetCollisionError,
    plan_target_tree,
)


REPO_ROOT = Path(__file__).resolve().parent.parent.parent


# ---------------------------------------------------------------------------
# DB-free fakes — stand in for a SQLAlchemy engine (keeps the suite pure /
# no @needs_bridge). resolve_workspace_pair + plan_target_tree are the only
# pair-touching call sites that read the engine.
# ---------------------------------------------------------------------------


class _Result:
    def __init__(self, rows: list[tuple]) -> None:
        self._rows = rows

    def all(self) -> list[tuple]:
        return self._rows

    def scalar(self):
        return self._rows[0][0] if self._rows else None


class _Conn:
    def __init__(self, routes: dict[str, list[tuple]]) -> None:
        self._routes = routes

    def __enter__(self) -> "_Conn":
        return self

    def __exit__(self, *exc) -> bool:
        return False

    def execute(self, stmt, *args, **kwargs) -> _Result:
        s = str(stmt)
        for needle, rows in self._routes.items():
            if needle in s:
                return _Result(rows)
        return _Result([])


class _Engine:
    """Minimal engine: ``connect()`` yields a conn that routes a SQL
    statement to canned rows by substring match."""

    def __init__(self, routes: dict[str, list[tuple]]) -> None:
        self._routes = routes

    def connect(self) -> _Conn:
        return _Conn(self._routes)


def _settings_engine(source: str, target: str) -> _Engine:
    return _Engine(
        {
            "workspace_settings": [
                ("source_lang", source),
                ("target_lang", target),
            ]
        }
    )


def _inventory_engine(source_rels: list[str], excluded: list[str] = []) -> _Engine:
    return _Engine(
        {
            "dart_files": [(r,) for r in source_rels],
            "excluded_directories": [(d,) for d in excluded],
        }
    )


# ---------------------------------------------------------------------------
# Obligation 1 — identity + extensions  (T008)
# ---------------------------------------------------------------------------


def test_identity_and_extensions() -> None:
    p = langpairs.get("dart", "gleam")
    assert p.key() == ("dart", "gleam")
    assert p.source_extensions() == (".dart",)
    assert p.target_extension() == ".gleam"


# ---------------------------------------------------------------------------
# Obligation 2 — registry / selectability / refusal  (T008, FR-007/SC-005)
# ---------------------------------------------------------------------------


def test_registry_lists_both_pairs() -> None:
    pairs = langpairs.list_pairs()
    assert ("dart", "csharp") in pairs
    assert ("dart", "gleam") in pairs
    assert pairs == sorted(pairs)


def test_get_returns_gleam_pair() -> None:
    assert langpairs.get("dart", "gleam").key() == ("dart", "gleam")


def test_unknown_pair_names_both_registered() -> None:
    with pytest.raises(UnknownLangPair) as ei:
        langpairs.get("dart", "rust")
    msg = str(ei.value)
    assert "dart" in msg and "rust" in msg
    # The message must name BOTH registered production pairs now.
    assert "dart->csharp" in msg
    assert "dart->gleam" in msg


def test_resolve_workspace_pair_bound_to_gleam() -> None:
    """A workspace bound to (dart, gleam) resolves to the Gleam pair."""
    eng = _settings_engine("dart", "gleam")
    p = langpairs.resolve_workspace_pair(eng, require_workspace=True)
    assert p.key() == ("dart", "gleam")


def test_pair_mismatch_when_override_disagrees() -> None:
    """FR-007/SC-005 mismatch half: bound to (dart, gleam) but an override
    requests a different pair -> PairMismatch, no mixed-pair output."""
    eng = _settings_engine("dart", "gleam")
    with pytest.raises(langpairs.PairMismatch):
        langpairs.resolve_workspace_pair(
            eng, require_workspace=True, override=("dart", "csharp")
        )


# ---------------------------------------------------------------------------
# Obligation 3 — source-side parity vs dart_csharp AND legacy discover (T009)
# ---------------------------------------------------------------------------


def test_source_extensions_and_globs_equal_dart_csharp() -> None:
    g = langpairs.get("dart", "gleam")
    c = langpairs.get("dart", "csharp")
    assert g.source_extensions() == c.source_extensions()
    assert g.tool_exclusion_globs() == c.tool_exclusion_globs()


def test_extract_imports_parity(tmp_path: Path) -> None:
    """dart_gleam.extract_imports == dart_csharp == legacy discover.parse
    on the same fixture (FR-002 — shared Dart source side)."""
    from codeconv.tools.discover.parse import (
        extract_imports as legacy_extract_imports,
    )

    sub = tmp_path / "glp_runtime"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "a.dart").write_text("class A {}\n", encoding="utf-8")
    target = sub / "lib" / "b.dart"
    target.write_text(
        "import 'a.dart';\nimport 'dart:core';\n"
        "import 'package:flutter/x.dart';\nclass B {}\n",
        encoding="utf-8",
    )

    g = langpairs.get("dart", "gleam")
    c = langpairs.get("dart", "csharp")
    via_gleam = g.extract_imports(target, sub, None)
    assert via_gleam == c.extract_imports(target, sub, None)
    assert via_gleam == legacy_extract_imports(target, sub, None)
    assert via_gleam == ["lib/a.dart"]


def test_extract_leading_doc_parity(tmp_path: Path) -> None:
    from codeconv.tools.discover.parse import (
        extract_leading_doc as legacy_doc,
    )

    f = tmp_path / "c.dart"
    f.write_text(
        "/// Heap kernel.\n/// Second line.\nclass C {}\n",
        encoding="utf-8",
    )
    g = langpairs.get("dart", "gleam")
    c = langpairs.get("dart", "csharp")
    assert g.extract_leading_doc(f) == c.extract_leading_doc(f)
    assert g.extract_leading_doc(f) == legacy_doc(f)
    assert g.extract_leading_doc(f).startswith("Heap kernel.")


# ---------------------------------------------------------------------------
# Obligation 4 — target_for positive: identity + ext swap, Windows sep (T011)
# ---------------------------------------------------------------------------


def test_target_for_legal_paths_identity_plus_ext() -> None:
    p = langpairs.get("dart", "gleam")
    assert p.target_for("lib/runtime/heap_fcp.dart") == "lib/runtime/heap_fcp.gleam"
    assert p.target_for("bin/main.dart") == "bin/main.gleam"
    # Already-legal nested dirs + basename preserved (FR-003 AS-2).
    assert p.target_for("lib/a.dart") == "lib/a.gleam"


def test_target_for_windows_sep_to_posix() -> None:
    p = langpairs.get("dart", "gleam")
    assert p.target_for("lib\\a.dart") == "lib/a.gleam"
    assert p.target_for("lib\\runtime\\x.dart") == "lib/runtime/x.gleam"


def test_target_for_no_source_extension() -> None:
    p = langpairs.get("dart", "gleam")
    # No source ext -> ext appended; basename normalized (lowercased).
    assert p.target_for("README") == "readme.gleam"


def test_workdir_name() -> None:
    p = langpairs.get("dart", "gleam")
    assert p.workdir_name("lib/runtime/heap_fcp.dart") == "__heap_fcp"
    assert p.workdir_name("bin/main.dart") == "__main"
    assert p.workdir_name("x.dart") is not None


# ---------------------------------------------------------------------------
# Obligation 5 — segment normalization legality (T012, FR-003/FR-008/SC-004)
# ---------------------------------------------------------------------------


@pytest.mark.parametrize(
    "src, expected",
    [
        ("lib/Foo.dart", "lib/foo.gleam"),        # uppercase
        ("lib/2d_grid.dart", "lib/g_2d_grid.gleam"),  # leading digit
        ("lib/my-mod.dart", "lib/my_mod.gleam"),  # hyphen
        ("lib/a.b.dart", "lib/a_b.gleam"),        # dotted stem -> '_'
        ("lib/type.dart", "lib/type_.gleam"),     # reserved word
        ("lib/case.dart", "lib/case_.gleam"),     # reserved word
        ("lib/import.dart", "lib/import_.gleam"),  # reserved word
        ("lib/fn.dart", "lib/fn_.gleam"),          # reserved word
    ],
)
def test_target_for_normalization_examples(src: str, expected: str) -> None:
    p = langpairs.get("dart", "gleam")
    assert p.target_for(src) == expected


@pytest.mark.parametrize(
    "seg",
    [
        "Foo", "FOO", "2d", "9", "my-mod", "a.b", "a b", "café",
        "type", "case", "import", "fn", "pub", "use", "todo", "",
        "__init__", "X__Y", "-leading", "trailing-",
    ],
)
def test_normalize_segment_always_legal_and_non_reserved(seg: str) -> None:
    """SC-004: every normalized segment matches ^[a-z][a-z0-9_]*$ and is
    not a Gleam reserved word."""
    out = target_gleam.normalize_segment(seg)
    assert re.match(r"^[a-z][a-z0-9_]*$", out), (seg, out)
    assert out not in target_gleam._GLEAM_RESERVED, (seg, out)


def test_normalize_segment_identity_on_legal() -> None:
    """FR-003 AS-2: an already-legal, non-reserved segment is unchanged."""
    for seg in ("a", "heap_fcp", "runtime", "x9", "g_2"):
        assert target_gleam.normalize_segment(seg) == seg


def test_normalize_segment_is_deterministic() -> None:
    for seg in ("Foo", "my-mod", "type", "2d"):
        assert target_gleam.normalize_segment(seg) == target_gleam.normalize_segment(seg)


# ---------------------------------------------------------------------------
# Obligation 6 — mirror-hook exact values (T013, FR-004/FR-010)
# ---------------------------------------------------------------------------


def test_mirror_prune_segments_exact() -> None:
    p = langpairs.get("dart", "gleam")
    # Identical to dart_csharp (Dart-tree concern, same for either target).
    assert p.mirror_prune_segments() == (
        ".dart_tool",
        "build",
        "archive",
        "backup",
        ".git",
        ".idea",
        ".vscode",
    )
    assert p.mirror_prune_segments() == langpairs.get(
        "dart", "csharp"
    ).mirror_prune_segments()


def test_preserved_source_suffix_is_verbatim() -> None:
    assert langpairs.get("dart", "gleam").preserved_source_suffix() == ""


def test_companion_extensions_exact_order() -> None:
    p = langpairs.get("dart", "gleam")
    assert p.companion_extensions() == (
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
    # Exactly the dart_csharp nine with .gleam in place of .cs.
    csharp = langpairs.get("dart", "csharp").companion_extensions()
    assert p.companion_extensions()[1:] == csharp[1:]
    assert p.companion_extensions()[0] == ".gleam"
    assert csharp[0] == ".cs"


def test_companion_stub_comment_gleam_syntax_and_category() -> None:
    p = langpairs.get("dart", "gleam")
    stub = p.companion_stub_comment(".gleam", "runner.dart")
    assert stub == "// TODO: Gleam source (gleam) — port from runner.dart"
    # Gleam line comment, not C-style block; category names "Gleam source".
    assert stub.startswith("// ")
    assert "Gleam source" in stub
    # A non-target companion keeps the dart_csharp category label.
    assert "analysis" in p.companion_stub_comment(".ana", "runner.dart")


def test_tracker_filename_pair_defined() -> None:
    assert langpairs.get("dart", "gleam").tracker_filename() == (
        "codeconv-gleam-tracker.json"
    )
    # Pair-defined: distinct from the C# pair's legacy literal.
    assert (
        langpairs.get("dart", "gleam").tracker_filename()
        != langpairs.get("dart", "csharp").tracker_filename()
    )


# ---------------------------------------------------------------------------
# Obligation 7 — SC-003 structural proxy: stage tools intact (T015)
# ---------------------------------------------------------------------------


def test_sc003_stage_tools_still_import_and_expose_app() -> None:
    """SC-003 (restated for R3-b — zero *pair-specific* stage edits): the
    stage tools still import cleanly and expose their Typer ``app``; no
    stage tool was forked/broken to add the Gleam pair (the registry is
    the only seam; the one R3-b planner assertion is generic)."""
    import importlib

    for mod_name in (
        "codeconv.tools.init",
        "codeconv.tools.discover",
        "codeconv.tools.depgraph",
        "codeconv.tools.scaffold",
    ):
        mod = importlib.import_module(mod_name)
        assert hasattr(mod, "app"), f"{mod_name} lost its Typer app"

    import codeconv.tools.discover.workflow as dw

    assert hasattr(dw, "_active_source_pair"), (
        "discover must resolve its source hooks via the langpair registry"
    )


# ---------------------------------------------------------------------------
# Obligation 8a — corpus no-collision over authoritative glp_runtime/ (T016)
# ---------------------------------------------------------------------------


def test_corpus_no_collision_over_glp_runtime() -> None:
    """R3-a guarantee: over the authoritative ``glp_runtime/`` Dart source
    set, ``target_for`` produces no two equal targets — FR-008's operative
    promise ("never silently merged or overwritten") holds for the
    production corpus the downstream F3/F4 features run against."""
    p = langpairs.get("dart", "gleam")
    corpus = REPO_ROOT / "glp_runtime"
    if not corpus.is_dir():
        pytest.skip("glp_runtime corpus not present")
    prune = set(p.mirror_prune_segments())

    rels: list[str] = []
    for f in corpus.rglob("*.dart"):
        parts = f.relative_to(corpus).parts
        if any(seg in prune for seg in parts):
            continue
        rels.append("/".join(parts))
    assert rels, "expected .dart files under glp_runtime/"

    seen: dict[str, str] = {}
    collisions: list[tuple[str, str, str]] = []
    for r in rels:
        t = p.target_for(r)
        if t in seen:
            collisions.append((seen[t], r, t))
        else:
            seen[t] = r
    assert not collisions, f"normalization collisions in glp_runtime/: {collisions}"


# ---------------------------------------------------------------------------
# Obligation 8b — R3-b: the planner raises on a synthetic colliding pair
# ---------------------------------------------------------------------------


def test_planner_raises_on_synthetic_collision() -> None:
    """R3-b: two distinct Dart sources that normalize to the same Gleam
    target make the scaffold planner raise (FR-008 runtime detection),
    naming both colliding sources — and, being pre-write, produce nothing."""
    p = langpairs.get("dart", "gleam")
    eng = _inventory_engine(["lib/Runner.dart", "lib/runner.dart"])
    with pytest.raises(TargetCollisionError) as ei:
        plan_target_tree(eng, p)
    msg = str(ei.value)
    assert "lib/runner.gleam" in msg
    assert "Runner.dart" in msg and "runner.dart" in msg


def test_planner_no_collision_for_distinct_targets() -> None:
    """The generic guard does NOT false-positive on a normal source set."""
    p = langpairs.get("dart", "gleam")
    eng = _inventory_engine(["lib/a.dart", "lib/b.dart", "bin/main.dart"])
    plan = plan_target_tree(eng, p)
    assert {pf.target_rel for pf in plan} == {
        "lib/a.gleam",
        "lib/b.gleam",
        "bin/main.gleam",
    }


def test_planner_dart_csharp_injective_no_collision_same_input() -> None:
    """The default pair's target_for is injective on .dart inputs, so the
    new generic guard never fires for dart_csharp (FR-006/SC-002 — default
    path unchanged)."""
    c = langpairs.get("dart", "csharp")
    eng = _inventory_engine(["lib/Runner.dart", "lib/runner.dart"])
    plan = plan_target_tree(eng, c)
    assert {pf.target_rel for pf in plan} == {"lib/Runner.cs", "lib/runner.cs"}
