"""T026 [US1] — empty/again-complete subtree exits cleanly (FR-020).

A builder run with no in-scope work MUST exit 0 with an explicit
"nothing to convert" outcome, NOT an error. The decision is the pure
``orchestrate.is_empty_subtree`` predicate over the read-only
``codeconv.dart_depgraph``; the @needs_bridge case drives the real CLI
on a freshly-migrated cluster with an empty depgraph.
"""

from __future__ import annotations

import json
from pathlib import Path

from .conftest import needs_bridge, run_codeconv


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    return build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))


@needs_bridge
def test_empty_subtree_nothing_to_convert(discover_repo: Path) -> None:
    """Fresh migrate, NO discover/depgraph ⇒ empty dart_depgraph ⇒
    ``builder run`` exits 0 with outcome 'nothing_to_convert' (FR-020)."""
    assert run_codeconv(discover_repo, "migrate", timeout=180.0).returncode == 0

    # Sanity: the pure predicate agrees the subtree is empty.
    from codeconv.tools.builder.orchestrate import is_empty_subtree

    assert is_empty_subtree(_engine(discover_repo)) is True

    proc = run_codeconv(discover_repo, "builder", "run", "--json", timeout=120.0)
    assert proc.returncode == 0, f"expected exit 0, got {proc.returncode}: {proc.stderr}"
    payload = json.loads(proc.stdout.strip().splitlines()[-1])
    assert payload["outcome"] == "nothing_to_convert", payload
    assert payload["units"] == 0, payload


@needs_bridge
def test_status_on_empty_subtree_is_clean(discover_repo: Path) -> None:
    """``builder status`` on an empty subtree is a clean read (exit 0,
    zero counts) — never an error."""
    assert run_codeconv(discover_repo, "migrate", timeout=180.0).returncode == 0
    proc = run_codeconv(
        discover_repo, "builder", "status", "--json", timeout=60.0
    )
    assert proc.returncode == 0, proc.stderr
    payload = json.loads(proc.stdout.strip().splitlines()[-1])
    assert payload["counts"]["total"] == 0, payload
