"""Corpus vendoring + drift tests — supporting D3 (T031).

Build-time helper; exercised entirely against tmp trees (never touches the real
``tutorials/olamni/``). Covers the sync round-trip + provenance manifest and
``--check`` drift detection (tampering, missing, sibling staleness).
"""

from __future__ import annotations

import json
from pathlib import Path

from codeconv.tutorials import sync as S


def _make_sibling(root: Path) -> Path:
    src = root / "sibling" / "tutorial"
    (src / "ch01" / "exercise-01").mkdir(parents=True)
    (src / "tutorial.md").write_text("# top\n", encoding="utf-8")
    (src / "ch01" / "exercise-01" / "a.glp").write_text("% a.glp\nfoo(a).\n", encoding="utf-8")
    (src / "ch01" / "exercise-01" / "ex-01-tutorial.md").write_text("# Ex 1 — demo\n", encoding="utf-8")
    return src


def test_sync_round_trip_and_manifest(tmp_path: Path) -> None:
    src = _make_sibling(tmp_path)
    dest = tmp_path / "vendored" / "olamni"

    result = S.sync(tmp_path, source=src, dest=dest)

    # Content copied verbatim.
    assert (dest / "tutorial.md").read_text(encoding="utf-8") == "# top\n"
    assert (dest / "ch01" / "exercise-01" / "a.glp").is_file()
    # Provenance written.
    assert (dest / S.SNAPSHOT_MD).is_file()
    manifest = json.loads((dest / S.MANIFEST_NAME).read_text(encoding="utf-8"))
    assert manifest["source"] == src.as_posix()
    assert "vendored_at" in manifest
    assert result.files == len(manifest["files"]) == 3  # 3 content files, manifest excluded
    assert set(manifest["files"]) == {
        "tutorial.md",
        "ch01/exercise-01/a.glp",
        "ch01/exercise-01/ex-01-tutorial.md",
    }


def test_check_clean_after_sync(tmp_path: Path) -> None:
    src = _make_sibling(tmp_path)
    dest = tmp_path / "vendored" / "olamni"
    S.sync(tmp_path, source=src, dest=dest)

    result = S.check(tmp_path, source=src, dest=dest)
    assert result.ok is True
    assert not result.tampered and not result.missing and not result.sibling_drift


def test_check_detects_local_tampering(tmp_path: Path) -> None:
    src = _make_sibling(tmp_path)
    dest = tmp_path / "vendored" / "olamni"
    S.sync(tmp_path, source=src, dest=dest)

    # Tamper a vendored file; --check (manifest only) must catch it.
    (dest / "tutorial.md").write_text("# tampered\n", encoding="utf-8")
    result = S.check(tmp_path, source=None, dest=dest)  # source absent → manifest-only
    assert result.ok is False
    assert "tutorial.md" in result.tampered


def test_check_detects_sibling_drift(tmp_path: Path) -> None:
    src = _make_sibling(tmp_path)
    dest = tmp_path / "vendored" / "olamni"
    S.sync(tmp_path, source=src, dest=dest)

    # Sibling advances; vendored snapshot is now stale.
    (src / "tutorial.md").write_text("# changed upstream\n", encoding="utf-8")
    result = S.check(tmp_path, source=src, dest=dest)
    assert result.ok is False
    assert "tutorial.md" in result.sibling_drift


def test_check_missing_manifest_is_drift(tmp_path: Path) -> None:
    dest = tmp_path / "empty"
    dest.mkdir()
    result = S.check(tmp_path, source=None, dest=dest)
    assert result.ok is False
    assert S.MANIFEST_NAME in result.missing
