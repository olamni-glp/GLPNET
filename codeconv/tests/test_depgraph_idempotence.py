"""Idempotence tests for ``codeconv depgraph compute`` (SC-002, SC-008)."""

from __future__ import annotations

import json
import re
from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


def _mk_subtree(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "a.dart").write_text(
        "/// A.\nclass A {}\n", encoding="utf-8"
    )
    (sub / "lib" / "b.dart").write_text(
        "/// B.\nimport 'a.dart';\nclass B {}\n", encoding="utf-8"
    )
    return sub


def _migrate_discover_compute(repo_root: Path, sub: Path) -> None:
    proc = run_codeconv(repo_root, "migrate", timeout=180.0)
    assert proc.returncode == 0, proc.stderr
    proc = run_codeconv(
        repo_root, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc.returncode == 0, proc.stderr


_GENERATED_AT_RE = re.compile(r'"generated_at":\s*"[^"]+"')


def _strip_volatile(payload_text: str) -> str:
    """Strip the wall-clock fields that are expected to vary across runs."""
    return _GENERATED_AT_RE.sub('"generated_at":""', payload_text)


@needs_bridge
def test_two_consecutive_computes_byte_identical_modulo_generated_at(
    discover_repo: Path,
) -> None:
    """SC-002: re-running compute on unchanged state produces byte-identical
    JSON output (modulo generated_at)."""
    sub = _mk_subtree(discover_repo)
    _migrate_discover_compute(discover_repo, sub)
    proc = run_codeconv(discover_repo, "depgraph", "compute")
    assert proc.returncode == 0, proc.stderr
    text1 = (discover_repo / ".codeconv" / "depgraph.json").read_text(
        encoding="utf-8"
    )
    proc = run_codeconv(discover_repo, "depgraph", "compute")
    assert proc.returncode == 0, proc.stderr
    text2 = (discover_repo / ".codeconv" / "depgraph.json").read_text(
        encoding="utf-8"
    )
    assert _strip_volatile(text1) == _strip_volatile(text2), (
        "SC-002 violated: depgraph.json differs across idempotent re-run"
    )


@needs_bridge
def test_dry_run_writes_nothing(discover_repo: Path) -> None:
    """SC-008: ``--dry-run`` does not write the JSON artefact nor populate
    ``codeconv.dart_depgraph``."""
    sub = _mk_subtree(discover_repo)
    _migrate_discover_compute(discover_repo, sub)
    # Wipe state.
    json_path = discover_repo / ".codeconv" / "depgraph.json"
    if json_path.is_file():
        json_path.unlink()

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    with engine.begin() as conn:
        conn.execute(text("DELETE FROM codeconv.dart_depgraph"))

    proc = run_codeconv(discover_repo, "depgraph", "compute", "--dry-run")
    assert proc.returncode == 0, proc.stderr
    assert not json_path.is_file(), "dry-run wrote depgraph.json"
    with engine.connect() as conn:
        n = conn.execute(
            text("SELECT COUNT(*) FROM codeconv.dart_depgraph")
        ).scalar()
    assert n == 0, f"dry-run wrote {n} rows to dart_depgraph"
