"""T034 — sampled review recording + promotion gate (US3 / FR-006).

@needs_bridge. Seeds built ``dart_codegen`` rows, records human reviews
via ``record-review``, and asserts ``promote-batch`` enforces the gate
(100% build + median ≥ 4/5): a low median or an unbuilt file blocks
promotion; a passing batch sets ``promoted=true`` on every file.
"""

from __future__ import annotations

import json
from pathlib import Path

from codeconv.tools.codegen.review import run_promote_batch, run_record_review

from .conftest import needs_bridge
from .test_discover_idempotence import _extract_json
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


def _setup(discover_repo: Path):
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)


def _seed_built(
    repo_root: Path,
    rel: str,
    build_status: str = "pass",
    batch_id: str | None = None,
) -> None:
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(acquire_or_discover(repo_root, ready_timeout=30.0))
    completed = "NOW()" if build_status == "pass" else "NULL"
    with eng.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_codegen "
                "(path, codegen_started_at, codegen_completed_at, "
                " sha256_of_dart_at_codegen_start, target_cs_path, "
                " build_status, open_escalation_count, batch_id) "
                f"VALUES (:p, NOW(), {completed}, 'x', :tp, :bs, 0, :b) "
                "ON CONFLICT (path) DO UPDATE SET build_status = :bs, "
                "batch_id = COALESCE(:b, codeconv.dart_codegen.batch_id)"
            ),
            {"p": rel, "tp": f"out/csharp/{rel[:-5]}.cs", "bs": build_status,
             "b": batch_id},
        )


@needs_bridge
def test_promotion_passes_with_good_reviews(discover_repo: Path) -> None:
    _setup(discover_repo)
    for rel in ("lib/a.dart", "lib/b.dart"):
        _seed_built(discover_repo, rel, "pass")
        run_record_review(
            repo_root=discover_repo, batch_id="batch1",
            file_path=rel, score=5, note="clean",
        )
    res = run_promote_batch(repo_root=discover_repo, batch_id="batch1")
    assert res["promoted"] is True, res
    assert res["files_promoted"] == 2

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    eng = build_engine(acquire_or_discover(discover_repo, ready_timeout=30.0))
    with eng.connect() as conn:
        n = conn.execute(
            text(
                "SELECT COUNT(*) FROM codeconv.dart_codegen "
                "WHERE batch_id = 'batch1' AND promoted"
            )
        ).scalar()
    assert n == 2


@needs_bridge
def test_promotion_blocked_by_low_median(discover_repo: Path) -> None:
    _setup(discover_repo)
    _seed_built(discover_repo, "lib/a.dart", "pass")
    _seed_built(discover_repo, "lib/b.dart", "pass")
    run_record_review(repo_root=discover_repo, batch_id="b2",
                      file_path="lib/a.dart", score=2)
    run_record_review(repo_root=discover_repo, batch_id="b2",
                      file_path="lib/b.dart", score=3)
    res = run_promote_batch(repo_root=discover_repo, batch_id="b2")
    assert res["promoted"] is False
    assert any("median" in b for b in res["blockers"])


@needs_bridge
def test_promotion_blocked_by_unbuilt_file(discover_repo: Path) -> None:
    _setup(discover_repo)
    # Both files tagged into batch b3 at "ingest" (the unbuilt one too —
    # the gate must see ALL batch members, not only the reviewed sample).
    _seed_built(discover_repo, "lib/a.dart", "pass", batch_id="b3")
    _seed_built(discover_repo, "lib/b.dart", "fail", batch_id="b3")
    run_record_review(repo_root=discover_repo, batch_id="b3",
                      file_path="lib/a.dart", score=5)
    res = run_promote_batch(repo_root=discover_repo, batch_id="b3")
    assert res["promoted"] is False
    assert res["all_built"] is False
