"""T010 — workflow-id determinism (pure; no bridge, no DBOS).

Asserts the R9 / dbos_workflow_model.md invariant that underpins
FR-004 / SC-002: a re-run recovers the *same* workflow id, so DBOS
dedups and resumed state == uninterrupted state. The hash must be
stable across processes (SHA-256, not salted ``hash()``).
"""

from __future__ import annotations

import subprocess
import sys

from codeconv.durable import (
    file_workflow_id,
    outer_workflow_id,
    scc_workflow_id,
)


def test_outer_id_deterministic_and_grammar():
    a = outer_workflow_id("ws1", 1779000000)
    b = outer_workflow_id("ws1", 1779000000)
    assert a == b == "builder:ws1:1779000000"
    # different epoch ⇒ different run (explicit --restart-run path, R13)
    assert outer_workflow_id("ws1", 1779000001) != a
    assert outer_workflow_id("ws2", 1779000000) != a
    # float epoch coerced to int (stable)
    assert outer_workflow_id("ws1", 1779000000.9) == a


def test_file_id_stable_and_distinct():
    p = "runtime/cell.dart"
    assert file_workflow_id(p) == file_workflow_id(p)
    assert file_workflow_id(p).startswith("file:")
    assert file_workflow_id("runtime/heap.dart") != file_workflow_id(p)
    # separator / affix normalisation ⇒ same id
    assert file_workflow_id("runtime\\cell.dart") == file_workflow_id(p)
    assert file_workflow_id("/runtime/cell.dart/") == file_workflow_id(p)


def test_scc_id_order_invariant():
    m1 = ["b/y.dart", "a/x.dart", "c/z.dart"]
    m2 = ["c/z.dart", "a/x.dart", "b/y.dart"]  # different order, same set
    assert scc_workflow_id(m1) == scc_workflow_id(m2)
    assert scc_workflow_id(m1).startswith("scc:")
    # a different member set ⇒ a different id
    assert scc_workflow_id(["a/x.dart", "b/y.dart"]) != scc_workflow_id(m1)
    # an SCC of one member is NOT the same as that file's per-file id
    assert scc_workflow_id(["a/x.dart"]) != file_workflow_id("a/x.dart")


def test_ids_stable_across_processes():
    """Cross-process: a fresh interpreter must derive byte-identical ids
    (guards against accidental use of salted ``hash()``)."""
    code = (
        "from codeconv.durable import outer_workflow_id, file_workflow_id, "
        "scc_workflow_id;"
        "print(outer_workflow_id('ws','7'));"
        "print(file_workflow_id('runtime/cell.dart'));"
        "print(scc_workflow_id(['b.dart','a.dart']))"
    )
    out = subprocess.run(
        [sys.executable, "-c", code],
        capture_output=True,
        text=True,
        check=True,
    ).stdout.splitlines()
    assert out[0] == outer_workflow_id("ws", 7)
    assert out[1] == file_workflow_id("runtime/cell.dart")
    assert out[2] == scc_workflow_id(["b.dart", "a.dart"])
