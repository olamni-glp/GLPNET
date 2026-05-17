"""T017 — append-only ``_FIELD_ORDER`` round-trip is a fixed point.

FR-021 / data-model §3: the feature-018 tombstone keys are appended
AFTER feature-017's, so write→read→write is byte-identical and a
``/codeconv-discover`` re-write (which does NOT author the appended
keys) carries them forward via ``merge_preserving_feature015`` rather
than dropping them. This is the data-model §3 idempotence proof. Pure
(no bridge): the invariant is in the tombstone canonicaliser itself.
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.discover.tombstone import (
    _FEATURE_018_KEYS,
    merge_preserving_feature015,
    read_tombstone,
    tombstone_path,
    write_tombstone,
)

_BASE = {
    "path": "lib/cell.dart",
    "name": "cell.dart",
    "purpose": "A cell.",
    "key_idea": "",
    "dependencies": ["lib/a.dart"],
    "callers": ["lib/b.dart"],
    "mtime": "2026-05-17T00:00:00Z",
    "sha256": "deadbeef",
}

_F018 = {
    "convspec_started_at": "2026-05-17T01:00:00Z",
    "convspec_completed_at": None,  # present-but-null ⇒ reached, not done
    "spec_path": ".codeconv/conversion-specs/lib/cell.dart.md",
    "convspec_open_escalation_count": 0,
    "builder_outer_workflow_id": "builder:ws:1779000000",
    "builder_file_state": "specced",
}


def test_stamp_rebuild_stamp_is_fixed_point(tmp_path: Path) -> None:
    root = tmp_path / "tomb"
    fields = {**_BASE, **_F018}

    p1 = write_tombstone(root, _BASE["path"], fields)
    bytes1 = p1.read_bytes()
    parsed1 = read_tombstone(p1)

    # Re-stamp from the parsed frontmatter ⇒ byte-identical (fixed point).
    p2 = write_tombstone(root, _BASE["path"], parsed1)
    assert p2.read_bytes() == bytes1, "stamp→rebuild→stamp not a fixed point"

    # Every feature-018 key survived the round-trip with value intact
    # (incl. YAML-null for convspec_completed_at).
    for k in _FEATURE_018_KEYS:
        assert parsed1[k] == _F018[k], f"{k} not preserved: {parsed1.get(k)!r}"


def test_discover_rewrite_preserves_f018(tmp_path: Path) -> None:
    """A discover-style re-write (8-key dict, does NOT author 018 keys)
    must carry the on-disk 018 state forward (merge_preserving_*),
    otherwise a re-discover after a builder stamp would erase it."""
    root = tmp_path / "tomb"
    existing = write_tombstone(root, _BASE["path"], {**_BASE, **_F018})

    merged = merge_preserving_feature015(dict(_BASE), existing)
    for k, v in _F018.items():
        assert merged[k] == v, f"discover re-write dropped {k}"

    write_tombstone(root, _BASE["path"], merged)
    again = read_tombstone(tombstone_path(root, _BASE["path"]))
    for k, v in _F018.items():
        assert again[k] == v, f"{k} lost after discover-style rewrite"


def test_absent_f018_keys_are_byte_invariant(tmp_path: Path) -> None:
    """Append-only proof: a tombstone with NO 018 keys is byte-identical
    to what feature-017 would have written (no spurious null keys)."""
    root = tmp_path / "tomb"
    p = write_tombstone(root, _BASE["path"], dict(_BASE))
    text = p.read_text(encoding="utf-8")
    for k in _FEATURE_018_KEYS:
        assert k not in text, f"absent key {k} leaked into tombstone"
