"""Tests for ``codeconv discover --verify-tombstones`` — feature 015 #17.

Maps to ``specs/012-codeconv-runner/contracts/codeconv_discover_cli.md``
§ Steps (--verify-tombstones mode) + § Exit codes.

``--verify-tombstones`` is a read-only source-truth audit: it reads
``.dart`` sources but acquires NO bridge and writes NO DB / tombstones.
So every test here runs WITHOUT ``@needs_bridge`` — fast + deterministic.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from .conftest import run_codeconv
from .test_discover_idempotence import _extract_json


def _sub(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True, exist_ok=True)
    return sub


def _write_tombstone(
    repo_root: Path,
    rel_path: str,
    *,
    sha256: str,
    dependencies: list[str] | None = None,
    callers: list[str] | None = None,
) -> None:
    troot = repo_root / ".codeconv" / "tombstones"
    tp = troot / (rel_path + ".md")
    tp.parent.mkdir(parents=True, exist_ok=True)
    deps = "[]" if not dependencies else "[" + ", ".join(dependencies) + "]"
    clrs = "[]" if not callers else "[" + ", ".join(callers) + "]"
    tp.write_text(
        "---\n"
        f"path: {rel_path}\n"
        f"name: {Path(rel_path).name}\n"
        "purpose: ''\n"
        "key_idea: ''\n"
        f"dependencies: {deps}\n"
        f"callers: {clrs}\n"
        "mtime: '2026-05-16T00:00:00.000Z'\n"
        f"sha256: {sha256}\n"
        "---\n\n",
        encoding="utf-8",
    )


def _sha(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def test_verify_clean(tmp_path: Path) -> None:
    sub = _sub(tmp_path)
    f = sub / "lib" / "a.dart"
    f.write_text("class A {}\n", encoding="utf-8")
    _write_tombstone(tmp_path, "lib/a.dart", sha256=_sha(f))

    proc = run_codeconv(
        tmp_path, "discover", "run", "--root", str(sub),
        "--verify-tombstones", "--json",
    )
    assert proc.returncode == 0, proc.stderr
    summary = json.loads(_extract_json(proc.stdout))
    assert summary["mode"] == "verify_tombstones"
    assert summary["verified_clean"] == 1
    assert summary["stale"] == 0
    assert summary["missing_source"] == 0
    assert summary["missing_tombstone"] == 0
    # NO bridge acquired in verify mode.
    assert not (tmp_path / ".pgdb" / "bridge.json").exists()


def test_verify_stale_sha256(tmp_path: Path) -> None:
    sub = _sub(tmp_path)
    f = sub / "lib" / "a.dart"
    f.write_text("class A {}\n", encoding="utf-8")
    _write_tombstone(tmp_path, "lib/a.dart", sha256="deadbeef" * 8)

    proc = run_codeconv(
        tmp_path, "discover", "run", "--root", str(sub),
        "--verify-tombstones", "--json",
    )
    assert proc.returncode == 0, proc.stderr
    summary = json.loads(_extract_json(proc.stdout))
    assert summary["stale"] == 1
    assert summary["verified_clean"] == 0
    kinds = {w["kind"] for w in summary["warnings"]}
    assert "stale_tombstone" in kinds


def test_verify_missing_source(tmp_path: Path) -> None:
    sub = _sub(tmp_path)
    # One real source so we are NOT in the zero-sources exit-1 path.
    real = sub / "lib" / "real.dart"
    real.write_text("class R {}\n", encoding="utf-8")
    _write_tombstone(tmp_path, "lib/real.dart", sha256=_sha(real))
    # A tombstone whose .dart is absent.
    _write_tombstone(tmp_path, "lib/gone.dart", sha256="ab" * 32)

    proc = run_codeconv(
        tmp_path, "discover", "run", "--root", str(sub),
        "--verify-tombstones", "--json",
    )
    assert proc.returncode == 0, proc.stderr
    summary = json.loads(_extract_json(proc.stdout))
    assert summary["missing_source"] == 1
    assert any(
        w["kind"] == "missing_source" and w["path"] == "lib/gone.dart"
        for w in summary["warnings"]
    )


def test_verify_missing_tombstone(tmp_path: Path) -> None:
    sub = _sub(tmp_path)
    f = sub / "lib" / "untracked.dart"
    f.write_text("class U {}\n", encoding="utf-8")
    # No tombstone written at all.

    proc = run_codeconv(
        tmp_path, "discover", "run", "--root", str(sub),
        "--verify-tombstones", "--json",
    )
    assert proc.returncode == 0, proc.stderr
    summary = json.loads(_extract_json(proc.stdout))
    assert summary["missing_tombstone"] == 1
    assert any(
        w["kind"] == "missing_tombstone" and w["path"] == "lib/untracked.dart"
        for w in summary["warnings"]
    )


def test_verify_zero_sources_exits_1(tmp_path: Path) -> None:
    sub = _sub(tmp_path)  # empty subtree (no .dart)
    _write_tombstone(tmp_path, "lib/a.dart", sha256="cd" * 32)

    proc = run_codeconv(
        tmp_path, "discover", "run", "--root", str(sub),
        "--verify-tombstones",
    )
    assert proc.returncode == 1, (
        f"zero .dart sources must exit 1 (not 2); got {proc.returncode} "
        f"stderr={proc.stderr!r}"
    )
    assert "no .dart sources" in proc.stderr


def test_verify_malformed_tombstone_exits_65(tmp_path: Path) -> None:
    sub = _sub(tmp_path)
    (sub / "lib" / "a.dart").write_text("class A {}\n", encoding="utf-8")
    troot = tmp_path / ".codeconv" / "tombstones" / "lib"
    troot.mkdir(parents=True, exist_ok=True)
    # No frontmatter delimiter → read_tombstone raises.
    (troot / "a.dart.md").write_text("not a tombstone\n", encoding="utf-8")

    proc = run_codeconv(
        tmp_path, "discover", "run", "--root", str(sub),
        "--verify-tombstones",
    )
    assert proc.returncode == 65, (
        f"malformed tombstone must exit 65; got {proc.returncode} "
        f"stderr={proc.stderr!r}"
    )
    assert "ABORT" in proc.stderr


def test_verify_missing_sha256_exits_65(tmp_path: Path) -> None:
    """Codex P2 regression: a tombstone with a valid path but NO sha256
    is format-invalid for the audit → abort exit 65 (NOT a stale warning
    with exit 0)."""
    sub = _sub(tmp_path)
    (sub / "lib" / "a.dart").write_text("class A {}\n", encoding="utf-8")
    troot = tmp_path / ".codeconv" / "tombstones" / "lib"
    troot.mkdir(parents=True, exist_ok=True)
    (troot / "a.dart.md").write_text(
        "---\n"
        "path: lib/a.dart\n"
        "name: a.dart\n"
        "dependencies: []\n"
        "callers: []\n"
        "mtime: '2026-05-16T00:00:00.000Z'\n"
        "---\n\n",  # NB: no sha256 key
        encoding="utf-8",
    )
    proc = run_codeconv(
        tmp_path, "discover", "run", "--root", str(sub),
        "--verify-tombstones",
    )
    assert proc.returncode == 65, (
        f"missing sha256 must abort exit 65; got {proc.returncode} "
        f"{proc.stdout}{proc.stderr}"
    )
    assert "ABORT" in proc.stderr


def test_verify_does_not_create_tombstones_dir(tmp_path: Path) -> None:
    """Codex P3 regression: verify mode is read-only — it must NOT create
    `.codeconv/tombstones/` in a clean checkout."""
    sub = _sub(tmp_path)
    (sub / "lib" / "x.dart").write_text("class X {}\n", encoding="utf-8")
    troot = tmp_path / ".codeconv" / "tombstones"
    assert not troot.exists()
    proc = run_codeconv(
        tmp_path, "discover", "run", "--root", str(sub),
        "--verify-tombstones", "--json",
    )
    assert proc.returncode == 0, proc.stderr
    summary = json.loads(_extract_json(proc.stdout))
    assert summary["missing_tombstone"] == 1
    assert not troot.exists(), (
        "verify mode must not create .codeconv/tombstones/ (read-only)"
    )


def test_verify_and_from_tombstones_mutually_exclusive(tmp_path: Path) -> None:
    sub = _sub(tmp_path)
    proc = run_codeconv(
        tmp_path, "discover", "run", "--root", str(sub),
        "--from-tombstones", "--verify-tombstones",
    )
    assert proc.returncode == 2  # click usage error
    # Typer's Rich error box hard-wraps the message across │ borders;
    # collapse all whitespace/box-drawing before substring-checking.
    flat = "".join(
        ch for ch in (proc.stderr + proc.stdout) if ch not in "│┌┐└┘─"
    )
    flat = " ".join(flat.split())
    assert "mutually exclusive" in flat
