"""FR-013/023/016 — a vanished expected check and an undeclared run are both loud. T020/T028."""

from __future__ import annotations

import pytest

from codeconv.receipts import UndeclaredRun, declare_expected, load_expected, missing_checks

from .reference_check import run_reference_check


def test_missing_expected_check_is_reported(tmp_path):
    root = tmp_path / "receipts"
    target = tmp_path / "t"
    target.mkdir()
    (target / "a").write_text("x", encoding="utf-8")
    run_reference_check(root=root, run_id="r", target_dir=target, check_id="ran.check")
    declare_expected(root, "r", ["ran.check", "vanished.check"])
    assert missing_checks(root, "r") == ["vanished.check"]  # a check that did not run is loud


def test_undeclared_run_refuses(tmp_path):
    with pytest.raises(UndeclaredRun):
        load_expected(tmp_path / "receipts", "no-decl-run")


def test_fixture_non_execution_is_loud(tmp_path):
    # FR-016 — the fault-injection suite is subject to its own invariant: declared
    # expected but never run ⇒ a missing check.
    root = tmp_path / "receipts"
    declare_expected(root, "r", ["receipts.conformance-fixture"])
    assert missing_checks(root, "r") == ["receipts.conformance-fixture"]
