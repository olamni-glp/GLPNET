"""T008 — codegen tombstone keys are append-only & round-trip idempotent.

Pure (no bridge): exercises ``codeconv.tools.codegen.tombstone_writer``
against on-disk tombstones written by feature-012's canonical emitter.
Verifies (contract codegen_schema.md § "Tombstone keys (append-only)"):

1. Stamping the five codegen keys preserves every prior key (features
   012/015/017/018) verbatim and appends the codegen keys AFTER them in
   ``_FIELD_ORDER``.
2. A re-stamp with identical values is byte-identical (idempotent).
3. ``read_codegen_keys`` recovers exactly the stamped keys
   (null-preserving).
4. ``stamp_keys(row_exists=False)`` omits all five keys (byte-identical
   to a never-codegen'd tombstone).
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.codegen.tombstone_writer import (
    codegen_completed_keys,
    codegen_started_keys,
    read_codegen_keys,
    stamp_keys,
    write_tombstone_with_codegen_keys,
)
from codeconv.tools.discover.tombstone import (
    _FIELD_ORDER,
    read_tombstone,
    tombstone_path,
    write_tombstone,
)


def _seed_tombstone(root: Path, rel: str) -> Path:
    """Write a tombstone carrying base + prior-feature appended keys."""
    fields = {
        "path": rel,
        "name": Path(rel).name,
        "purpose": "seed",
        "key_idea": "",
        "dependencies": ["x/dep.dart"],
        "callers": [],
        "mtime": "2026-05-23T00:00:00Z",
        "sha256": "abc123",
        # prior appended state (015 + 017 + 018) that MUST survive
        "topo_level": 2,
        "cycle_group_id": 5,
        "status": "converted",
        "plan_started_at": "2026-05-23T01:00:00Z",
        "plan_completed_at": "2026-05-23T02:00:00Z",
        "plan_path": ".codeconv/conversion-plans/" + rel + ".md",
        "open_escalation_count": 0,
        "convspec_started_at": "2026-05-23T00:30:00Z",
        "spec_path": ".codeconv/conversion-specs/" + rel + ".md",
    }
    return write_tombstone(root, rel, fields)


def test_codegen_stamp_preserves_prior_keys_and_appends(tmp_path: Path) -> None:
    root = tmp_path / "tombs"
    rel = "runtime/cell.dart"
    _seed_tombstone(root, rel)

    write_tombstone_with_codegen_keys(
        root,
        rel,
        codegen_completed_keys(
            completed_at="2026-05-23T03:00:00Z",
            target_cs_path="out/csharp/runtime/Cell.cs",
            build_status="pass",
            open_escalation_count=0,
        ),
    )
    fm = read_tombstone(tombstone_path(root, rel))
    # Prior keys preserved verbatim.
    assert fm["plan_path"] == ".codeconv/conversion-plans/" + rel + ".md"
    assert fm["spec_path"] == ".codeconv/conversion-specs/" + rel + ".md"
    assert fm["topo_level"] == 2 and fm["cycle_group_id"] == 5
    # Codegen keys appended.
    assert fm["codegen_completed_at"] == "2026-05-23T03:00:00Z"
    assert fm["target_cs_path"] == "out/csharp/runtime/Cell.cs"
    assert fm["build_status"] == "pass"
    assert fm["codegen_open_escalation_count"] == 0

    # Codegen keys appear AFTER the feature-018 keys in _FIELD_ORDER.
    order = list(_FIELD_ORDER)
    assert order.index("codegen_started_at") > order.index("builder_file_state")


def test_codegen_stamp_idempotent_byte_identical(tmp_path: Path) -> None:
    root = tmp_path / "tombs"
    rel = "runtime/heap.dart"
    _seed_tombstone(root, rel)

    extras = codegen_completed_keys(
        completed_at="2026-05-23T03:00:00Z",
        target_cs_path="out/csharp/runtime/Heap.cs",
        build_status="pass",
        open_escalation_count=1,
    )
    write_tombstone_with_codegen_keys(root, rel, extras)
    first = tombstone_path(root, rel).read_bytes()
    write_tombstone_with_codegen_keys(root, rel, extras)
    second = tombstone_path(root, rel).read_bytes()
    assert first == second, "re-stamp not byte-identical (not idempotent)"


def test_read_codegen_keys_roundtrip(tmp_path: Path) -> None:
    root = tmp_path / "tombs"
    rel = "a.dart"
    _seed_tombstone(root, rel)
    write_tombstone_with_codegen_keys(
        root, rel, codegen_started_keys(codegen_started_at="2026-05-23T03:00:00Z")
    )
    keys = read_codegen_keys(root, rel)
    assert keys == {"codegen_started_at": "2026-05-23T03:00:00Z"}


def test_stamp_keys_row_absent_omits_all(tmp_path: Path) -> None:
    root = tmp_path / "tombs"
    rel = "b.dart"
    _seed_tombstone(root, rel)
    baseline = tombstone_path(root, rel).read_bytes()

    extras = stamp_keys(
        codegen_started_at=None,
        codegen_completed_at=None,
        target_cs_path=None,
        build_status=None,
        open_escalation_count=None,
        row_exists=False,
    )
    assert extras == {}
    write_tombstone_with_codegen_keys(root, rel, extras)
    after = tombstone_path(root, rel).read_bytes()
    assert after == baseline, "row_exists=False must leave the tombstone unchanged"
