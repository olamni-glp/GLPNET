"""FR-019/020 — an area absent from the adoption manifest is an error, not a pass. T021."""

from __future__ import annotations

import json

import pytest

from codeconv.receipts import GLPNET_AREAS, MissingDeclaration, Verdict, VerdictRefused, load_adoption, read


def _write_manifest(path, areas):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(
        {"areas": [{"area": a, "state": s, "since": "2026-08-18"} for a, s in areas]}),
        encoding="utf-8")


def test_manifest_omitting_an_area_is_an_error(tmp_path):
    m = tmp_path / "adoption.json"
    _write_manifest(m, [("build-gate", "non-adopted")])  # omits the rest
    with pytest.raises(MissingDeclaration):
        load_adoption(m)


def test_full_manifest_loads(tmp_path):
    m = tmp_path / "adoption.json"
    _write_manifest(m, [(a, "non-adopted") for a in GLPNET_AREAS])
    assert set(load_adoption(m)) == set(GLPNET_AREAS)


def test_consumer_refuses_verdict_from_unlisted_area(tmp_path):
    manifest = {a: "non-adopted" for a in GLPNET_AREAS}  # 'mystery' is unlisted
    with pytest.raises(VerdictRefused):
        read(Verdict(check_id="x", area="mystery", receipt_pointer=None), adoption=manifest)


def test_non_adopted_area_is_usable_behind_a_marker(tmp_path):
    manifest = {a: "non-adopted" for a in GLPNET_AREAS}
    reading = read(Verdict(check_id="x", area="build-gate", receipt_pointer=None), adoption=manifest)
    assert reading.non_adoption is True and reading.successful is False
