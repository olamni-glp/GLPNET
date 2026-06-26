"""Feature 035 / US1 (T007) — blank tombstones get inferred semantics.

Acceptance 1: a blank-doc candidate gains a non-blank ``purpose``, a
DISTINCT ``key_idea`` (SC-005), ``*_source: inferred``, with ``sha256``
unchanged (source not modified). Acceptance 2: a doc'd file's
``purpose``/``key_idea`` TEXT is left unchanged. Acceptance 3 / SC-006:
provenance distinguishes ``inferred`` from ``doc``. Markdown ⇔ DB agree
(FR-004). Uses the deterministic fake ``infer_fn`` (no network — SC-004).
"""

from __future__ import annotations

from pathlib import Path

from sqlalchemy import text

from .conftest import BRIDGE_SCRIPT, fake_infer_fn, needs_bridge, run_codeconv
from codeconv.tools.discover.tombstone import read_tombstone, tombstone_path


def _mk_subtree(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    # Blank-doc candidate: NO leading doc-comment → discover seeds purpose=''.
    (sub / "lib" / "blank.dart").write_text(
        "class Blank {\n  int compute() => 41 + 1;\n}\n", encoding="utf-8"
    )
    # Doc'd non-candidate: leading /// doc-comment → discover seeds purpose=doc.
    (sub / "lib" / "docced.dart").write_text(
        "/// Already documented unit.\nclass Docced {}\n", encoding="utf-8"
    )
    return sub


@needs_bridge
def test_blank_inferred_docced_text_untouched(discover_repo: Path) -> None:
    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    p = run_codeconv(discover_repo, "discover", "run", "--root", str(sub), "--json")
    assert p.returncode == 0, p.stderr

    troot = discover_repo / ".codeconv" / "tombstones"
    blank_tomb = tombstone_path(troot, "lib/blank.dart")
    docced_tomb = tombstone_path(troot, "lib/docced.dart")

    pre_blank = read_tombstone(blank_tomb)
    assert pre_blank["purpose"] == "" and pre_blank["key_idea"] == ""
    pre_sha = pre_blank["sha256"]
    pre_docced = read_tombstone(docced_tomb)
    assert pre_docced["purpose"].strip() == "Already documented unit."

    # In-process enrich with the fake seam — discovers the running bridge.
    from codeconv.tools.enrich.workflow import run_enrich

    summary = run_enrich(
        discover_repo, infer_fn=fake_infer_fn, bridge_script=BRIDGE_SCRIPT
    )
    assert summary["ok"] is True, summary
    assert summary["enriched"] >= 1

    # Acceptance 1: blank tombstone filled, distinct, inferred, sha unchanged.
    post_blank = read_tombstone(blank_tomb)
    assert post_blank["purpose"].strip() != ""
    assert post_blank["key_idea"].strip() != ""
    assert post_blank["key_idea"] != post_blank["purpose"]  # SC-005 distinct
    assert post_blank["purpose_source"] == "inferred"
    assert post_blank["key_idea_source"] == "inferred"
    assert post_blank["sha256"] == pre_sha  # source NOT modified

    # Acceptance 2: doc'd file's purpose/key_idea TEXT unchanged (FR-006).
    post_docced = read_tombstone(docced_tomb)
    assert post_docced["purpose"] == pre_docced["purpose"]
    assert post_docced["key_idea"] == pre_docced["key_idea"]
    # Acceptance 3 / SC-006: provenance distinguishes doc from inferred.
    assert post_docced["purpose_source"] == "doc"

    # Markdown ⇔ DB agreement (FR-004).
    from codeconv.db.engine import connect

    engine = connect(discover_repo)
    with engine.begin() as conn:
        row = conn.execute(
            text(
                "SELECT purpose, key_idea, purpose_source, key_idea_source "
                "FROM codeconv.dart_files WHERE path = :p"
            ),
            {"p": "lib/blank.dart"},
        ).first()
    assert row is not None
    assert row[0] == post_blank["purpose"]
    assert row[1] == post_blank["key_idea"]
    assert row[2] == "inferred" and row[3] == "inferred"
