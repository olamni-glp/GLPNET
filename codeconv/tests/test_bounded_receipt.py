"""FR-005 — receipts are bounded but honest: totals survive, truncation self-declares. T038."""

from __future__ import annotations

from codeconv.receipts import Target, bind, emit


def test_large_enumeration_capped_but_totals_preserved(tmp_path):
    n = bind.MAX_ENUM + 50
    items = [f"item{i}" for i in range(n)]
    r = emit(check_id="big", area="reference", target=Target("path", "/t"),
             examined_count=n, total_count=n, examined=items, run_id="r", root=tmp_path)
    assert len(r.examined) == bind.MAX_ENUM            # enumeration capped
    assert r.examined_count == n and r.total_count == n  # true totals preserved (FR-010)
    assert r.truncated.enumerations and r.truncated.dropped == 50  # truncation self-declared


def test_oversized_single_field_is_byte_backstopped(tmp_path):
    huge = "z" * (bind.MAX_FIELD_BYTES + 100)
    r = emit(check_id="bf", area="reference", target=Target("path", "/t"),
             examined_count=1, total_count=1, examined=[huge], run_id="r", root=tmp_path)
    assert r.truncated.byte_capped  # a single pathological field is capped, and it says so
