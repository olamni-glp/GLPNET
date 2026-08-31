"""FR-019/020 — an area absent from the adoption manifest is an error, not a pass. T021."""

from __future__ import annotations

import json

import pytest

from codeconv.receipts import GLPNET_AREAS, MissingDeclaration, Verdict, VerdictRefused, load_adoption, read
from codeconv.receipts.manifest import UndeclaredState


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


# --- FR-019/020: a MALFORMED declaration is an error, exactly like an absent one ---
#
# Found by codexreview 20260828T004446Z: load_adoption accepted any string as a
# state, and consumer.read gated on the single equality `state == "non-adopted"`.
# So one typo did not disable the gate loudly — it handed the area ADOPTED
# semantics and turned an unearned verdict GREEN, through the very manifest that
# authorises the refusal. Both layers are asserted here: the loader, and the gate
# itself (which takes a plain dict any caller may build, bypassing the loader).

def test_manifest_with_an_unknown_state_is_an_error(tmp_path):
    m = tmp_path / "adoption.json"
    _write_manifest(m, [(a, "non-adopted") for a in GLPNET_AREAS[:-1]] + [(GLPNET_AREAS[-1], "adoped")])
    with pytest.raises(UndeclaredState):
        load_adoption(m)


@pytest.mark.parametrize("bogus", ["adoped", "nonadopted", "non_adopted", "pending", "", "ADOPTED"])
def test_no_typo_of_non_adopted_is_accepted(tmp_path, bogus):
    """Near-misses of BOTH legal values must refuse; a near-miss is the realistic typo."""
    m = tmp_path / "adoption.json"
    _write_manifest(m, [(a, "non-adopted") for a in GLPNET_AREAS[:-1]] + [(GLPNET_AREAS[-1], bogus)])
    with pytest.raises(UndeclaredState):
        load_adoption(m)


def test_manifest_declaring_an_area_twice_is_an_error(tmp_path):
    """Two states for one area means one of them is silently discarded."""
    m = tmp_path / "adoption.json"
    _write_manifest(m, [(a, "non-adopted") for a in GLPNET_AREAS] + [("build-gate", "adopted")])
    with pytest.raises(UndeclaredState):
        load_adoption(m)


def test_consumer_refuses_an_unknown_state_rather_than_treating_it_as_adopted(tmp_path):
    """THE LOAD-BEARING ONE. Without the gate's own check this returns a
    successful reading — an unearned green — instead of refusing."""
    manifest = {a: "non-adopted" for a in GLPNET_AREAS}
    manifest["build-gate"] = "adoped"
    with pytest.raises(VerdictRefused):
        read(Verdict(check_id="x", area="build-gate", receipt_pointer=None), adoption=manifest)


def test_both_legal_states_still_pass_through(tmp_path):
    """The guard must not refuse the two values it exists to distinguish."""
    m = tmp_path / "adoption.json"
    _write_manifest(m, [(a, "non-adopted") for a in GLPNET_AREAS[:-1]] + [(GLPNET_AREAS[-1], "adopted")])
    loaded = load_adoption(m)
    assert loaded[GLPNET_AREAS[-1]] == "adopted"
    assert set(loaded) == set(GLPNET_AREAS)
