"""Shared helpers for the US1 / T054-gate end-to-end builder tests."""

from __future__ import annotations

import json
import time
from pathlib import Path

from .conftest import run_codeconv


def mk_nfile_subtree(repo_root: Path, n: int = 22) -> Path:
    """An ``n``-file linear dependency chain under ``glp_runtime_net/``
    (f0 ← f1 ← … ← f{n-1}), enough to exercise sustained throughput
    (T054 requires ≥20)."""
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    for i in range(n):
        imp = f"import 'f{i - 1}.dart';\n" if i else ""
        (sub / "lib" / f"f{i}.dart").write_text(
            f"/// File {i}.\n{imp}class F{i} {{}}\n", encoding="utf-8"
        )
    return sub


def migrate_discover_depgraph(repo_root: Path, sub: Path) -> None:
    """Full workspace setup for the durable builder pipeline, matching
    quickstart Flow B: migrate → **init** (configures the workspace +
    delegates discovery) → discover (idempotent) → depgraph compute.

    ``init`` is REQUIRED: the builder's scaffold stage (``run_scaffold``)
    refuses to run without an initialised workspace (FR-006) — omitting
    it makes scaffold a silent no-op (no ``target_path``)."""
    assert run_codeconv(repo_root, "migrate", timeout=180.0).returncode == 0
    assert (
        run_codeconv(
            repo_root,
            "init",
            "run",
            "--source",
            "glp_runtime_net",
            "--target",
            "out/csharp",
            "--source-lang",
            "dart",
            "--target-lang",
            "csharp",
            "--accept-suggested-exclusions",
            "--non-interactive",
            "--json",
            timeout=180.0,
        ).returncode
        == 0
    )
    assert (
        run_codeconv(
            repo_root, "discover", "run", "--root", str(sub), "--json"
        ).returncode
        == 0
    )
    assert (
        run_codeconv(repo_root, "depgraph", "compute", "--json").returncode == 0
    )


def builder_run(repo_root: Path, *extra: str, timeout: float = 300.0):
    return run_codeconv(
        repo_root, "builder", "run", "--json", *extra, timeout=timeout
    )


def last_json(proc) -> dict:
    return json.loads(proc.stdout.strip().splitlines()[-1])


def timed_status(repo_root: Path) -> tuple[dict, float]:
    t0 = time.monotonic()
    p = run_codeconv(repo_root, "builder", "status", "--json", timeout=60.0)
    dt = time.monotonic() - t0
    assert p.returncode == 0, p.stderr
    return last_json(p), dt
