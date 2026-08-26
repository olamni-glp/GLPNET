"""067 US3 — the published decode vectors must be self-conformant (FR-011, SC-002).

This is the producer's half of the consumer contract: if these pass, an independent
implementation (olamnit-assistant `android-quick-link-endpoints`) can be written against
`contracts/payload-contract.md` + the vector files alone, with **no live mesh**.

A failure here means the published contract and the shipped producer have diverged — which would
strand the out-of-repo consumer, so it is a hard failure, never a skip.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from glp_quick.provision.bundle import AssemblyError, Bundle, assemble
from glp_quick.provision.crypto import BadPassphrase, open_envelope

VECTOR_DIR = Path(__file__).resolve().parents[1] / "vectors" / "provision"


def _decode(chunks, passphrase):
    """Mirror of what a consumer does: assemble → decrypt → parse. Returns (token, bundle)."""
    try:
        envelope = assemble(chunks)
    except AssemblyError as exc:
        return exc.token, exc.detail, None
    try:
        plaintext = open_envelope(envelope, passphrase)
    except BadPassphrase:
        return "bad_passphrase", "", None
    try:
        return "ok", "", Bundle.from_json(plaintext)
    except AssemblyError as exc:
        return exc.token, exc.detail, None


def _load(name):
    path = VECTOR_DIR / name
    if not path.exists():
        pytest.fail(
            f"vector {name} is missing — regenerate with "
            f"`python -m tests.vectors.provision.generate_vectors` from glp_quick/"
        )
    return json.loads(path.read_text(encoding="utf-8"))


@pytest.fixture(scope="module", autouse=True)
def _ensure_vectors():
    """Generate the vectors if absent, so the suite is self-sufficient on a fresh clone."""
    if not (VECTOR_DIR / "v1_single_chunk.json").exists():
        from tests.vectors.provision.generate_vectors import build_all

        build_all()


@pytest.mark.parametrize(
    "name",
    [
        "v1_single_chunk.json",
        "v1_multi_chunk.json",
        "v1_corrupt_chunk.json",
        "v1_missing_chunk.json",
        "v1_bad_version.json",
        "v1_bad_passphrase.json",
    ],
)
def test_vector_decodes_to_its_declared_expectation(name):
    v = _load(name)
    token, detail, bundle = _decode(v["chunks"], v["passphrase"])
    assert token == v["expect"]["result"], f"{name}: expected {v['expect']['result']}, got {token}"
    if "detail" in v["expect"]:
        assert v["expect"]["detail"] in detail, f"{name}: detail {detail!r}"
    if token == "ok":
        assert bundle is not None
        assert bundle.session_id == v["expect"]["session_id"]


def test_multi_chunk_assembles_identically_in_any_order():
    v = _load("v1_multi_chunk.json")
    _, _, straight = _decode(v["chunks"], v["passphrase"])
    _, _, shuffled = _decode(v["chunks_shuffled"], v["passphrase"])
    assert straight is not None and shuffled is not None
    assert straight == shuffled, "order-independent assembly is a contract MUST (FR-007)"


def test_multi_chunk_vector_is_actually_multi_chunk():
    """Guards the vector itself: a single-chunk 'multi' vector would silently test nothing."""
    v = _load("v1_multi_chunk.json")
    assert len(v["chunks"]) > 1
    assert len(v["chunks"]) == v["expect"]["chunk_count"]


def test_bad_passphrase_vector_yields_no_plaintext():
    v = _load("v1_bad_passphrase.json")
    token, _, bundle = _decode(v["chunks"], v["passphrase"])
    assert token == "bad_passphrase"
    assert bundle is None  # FR-004: no partial plaintext on the failure path


def test_vectors_carry_no_real_trust_material():
    """The vectors are as public as documentation — they must contain nothing that ever protected
    a real endpoint. Guards against a future regeneration accidentally sourcing live material."""
    live_pin = None
    live_cert = Path(__file__).resolve().parents[3] / "glpquick-cert" / "glpquick.fingerprint"
    if live_cert.exists():
        live_pin = live_cert.read_text(encoding="ascii").strip()

    for path in VECTOR_DIR.glob("v1_*.json"):
        raw = path.read_text(encoding="utf-8")
        assert "-----BEGIN" not in raw, f"{path.name} carries PEM material in the clear"
        if live_pin:
            assert live_pin not in raw, f"{path.name} carries the LIVE trunk pin"
