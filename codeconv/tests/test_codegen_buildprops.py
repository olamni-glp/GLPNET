"""Pure unit tests for ``codegen.buildprops`` (Converted.props maintenance).

The codegen workflow appends ``<Compile Include="lib/<rel>.cs" />`` to
``out/csharp/Converted.props`` before the build gate (because
``EnableDefaultCompileItems=false`` excludes everything else) and reverts
on fail so a failed file cannot poison subsequent files' builds.

These tests are PURE (no bridge, no dotnet) so they run in milliseconds.
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.codegen import buildprops


_INITIAL = """\
<!-- header comment -->
<Project>
  <ItemGroup>
    <!-- codeconv-codegen append point — keep one entry per line, sorted. -->
  </ItemGroup>
</Project>
"""


def _write_initial(tmp_path: Path) -> Path:
    p = tmp_path / "Converted.props"
    p.write_text(_INITIAL, encoding="utf-8")
    return p


def test_converted_props_path_under_target_root(tmp_path: Path) -> None:
    p = buildprops.converted_props_path(tmp_path, "out/csharp")
    assert p == tmp_path / "out" / "csharp" / "Converted.props"


def test_converted_props_path_normalises_separators(tmp_path: Path) -> None:
    p = buildprops.converted_props_path(tmp_path, "out\\csharp\\")
    assert p == tmp_path / "out" / "csharp" / "Converted.props"


def test_include_relpath_strips_target_root() -> None:
    assert (
        buildprops.include_relpath("out/csharp/lib/runtime/cell.cs", "out/csharp")
        == "lib/runtime/cell.cs"
    )


def test_include_relpath_idempotent_on_already_relative() -> None:
    # If someone passes a path already relative to the target_root, leave it.
    assert (
        buildprops.include_relpath("lib/runtime/cell.cs", "out/csharp")
        == "lib/runtime/cell.cs"
    )


def test_update_absent_file_is_noop(tmp_path: Path) -> None:
    p = tmp_path / "Converted.props"  # not created
    assert buildprops.update(p, add="lib/a.cs") is False
    assert not p.exists()


def test_update_add_inserts_sorted_include(tmp_path: Path) -> None:
    p = _write_initial(tmp_path)
    assert buildprops.update(p, add="lib/b.cs") is True
    text = p.read_text(encoding="utf-8")
    assert '<Compile Include="lib/b.cs" />' in text


def test_update_add_is_idempotent(tmp_path: Path) -> None:
    p = _write_initial(tmp_path)
    assert buildprops.update(p, add="lib/a.cs") is True
    # Second add of the same include changes nothing.
    assert buildprops.update(p, add="lib/a.cs") is False
    text = p.read_text(encoding="utf-8")
    # Exactly one Include entry for lib/a.cs.
    assert text.count('Include="lib/a.cs"') == 1


def test_update_keeps_sorted_order(tmp_path: Path) -> None:
    p = _write_initial(tmp_path)
    for inc in ("lib/z.cs", "lib/a.cs", "lib/m.cs"):
        buildprops.update(p, add=inc)
    text = p.read_text(encoding="utf-8")
    idx_a = text.index("lib/a.cs")
    idx_m = text.index("lib/m.cs")
    idx_z = text.index("lib/z.cs")
    assert idx_a < idx_m < idx_z


def test_update_remove_drops_include(tmp_path: Path) -> None:
    p = _write_initial(tmp_path)
    buildprops.update(p, add="lib/a.cs")
    buildprops.update(p, add="lib/b.cs")
    assert buildprops.update(p, remove="lib/a.cs") is True
    text = p.read_text(encoding="utf-8")
    assert "lib/a.cs" not in text
    assert "lib/b.cs" in text


def test_update_remove_absent_is_noop(tmp_path: Path) -> None:
    p = _write_initial(tmp_path)
    assert buildprops.update(p, remove="lib/never.cs") is False
    assert p.read_text(encoding="utf-8") == _INITIAL


def test_update_preserves_anchor_comment(tmp_path: Path) -> None:
    p = _write_initial(tmp_path)
    buildprops.update(p, add="lib/a.cs")
    text = p.read_text(encoding="utf-8")
    assert "codeconv-codegen append point" in text


def test_update_ignores_example_include_inside_header_comment(tmp_path: Path) -> None:
    # Regression for the bulk-codegen first-batch bug: the production
    # Converted.props header comment contains an example ``<Compile
    # Include="..."/>`` line. An earlier version of ``update`` scanned the
    # whole file and treated the example's ``...`` as a real existing
    # entry, re-inserting it into the ItemGroup body on every rewrite —
    # then ``dotnet build`` crashed trying to GitInfo a path of "...".
    p = tmp_path / "Converted.props"
    p.write_text(
        "<!--\n"
        '  Each conversion appends one <Compile Include="..."/> here.\n'
        "-->\n"
        "<Project>\n"
        "  <ItemGroup>\n"
        f"    {buildprops._ANCHOR}\n"  # type: ignore[attr-defined]
        "  </ItemGroup>\n"
        "</Project>\n",
        encoding="utf-8",
    )
    assert buildprops.update(p, add="lib/a.cs") is True
    text = p.read_text(encoding="utf-8")
    # Body has the real entry only — no "..." entry leaked from the comment.
    assert '<Compile Include="lib/a.cs" />' in text
    # The literal ellipsis Include must not appear in the ItemGroup body
    # (it remains only inside the header comment).
    body = text.split("<ItemGroup>")[1].split("</ItemGroup>")[0]
    assert 'Include="..."' not in body


def test_update_returns_false_on_malformed_props(tmp_path: Path) -> None:
    p = tmp_path / "Converted.props"
    # No <ItemGroup> at all — malformed; update declines to touch it.
    p.write_text("<Project></Project>\n", encoding="utf-8")
    assert buildprops.update(p, add="lib/a.cs") is False
    assert p.read_text(encoding="utf-8") == "<Project></Project>\n"
