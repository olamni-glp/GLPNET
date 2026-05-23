"""T028 — optimized-prompt artifact (de)serialize round-trip + load (pure).

Contract codegen_artifact_format.md § B: a fenced ```yaml provenance
block + markdown instructions; ``prompt.load()`` reads it for production,
falling back to the shipped baseline (with a warning) when absent. No
bridge, no LM.
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.codegen.prompt import (
    BASELINE_INSTRUCTIONS,
    SCHEMA_VERSION,
    load,
    optimized_prompt_path,
    parse,
    serialize,
    write_optimized,
)


def test_serialize_parse_roundtrip() -> None:
    instr = "Generate real C#.\nHonor idioms.\n"
    prov = {
        "optimizer": "gepa",
        "metric_score": 0.83,
        "dataset_hash": "abc123",
        "generated_at": "2026-05-23T00:00:00Z",
        "model": "openai/gpt-5.1",
    }
    text = serialize(instr, prov)
    art = parse(text)
    assert art.instructions.strip() == instr.strip()
    assert art.provenance["optimizer"] == "gepa"
    assert art.provenance["metric_score"] == 0.83
    # serialize injects schema_version.
    assert art.provenance["schema_version"] == SCHEMA_VERSION


def test_load_absent_uses_baseline(tmp_path: Path) -> None:
    art = load(tmp_path)
    assert art.is_baseline is True
    assert art.instructions == BASELINE_INSTRUCTIONS
    assert art.provenance == {}


def test_write_then_load_production(tmp_path: Path) -> None:
    instr = "Optimized: emit only C#.\n"
    prov = {"optimizer": "gepa", "metric_score": 0.9, "model": "m"}
    target = write_optimized(tmp_path, instr, prov)
    assert target == optimized_prompt_path(tmp_path)
    assert target.is_file()
    art = load(tmp_path)
    assert art.is_baseline is False
    assert art.instructions.strip() == instr.strip()
    assert art.provenance["metric_score"] == 0.9


def test_empty_instructions_falls_back_to_baseline(tmp_path: Path) -> None:
    # An artifact with only a provenance block (empty instructions) must
    # not be used as production instructions.
    write_optimized(tmp_path, "", {"optimizer": "gepa"})
    art = load(tmp_path)
    assert art.is_baseline is True
    assert art.instructions == BASELINE_INSTRUCTIONS


def test_parse_without_yaml_block_is_lenient() -> None:
    art = parse("just instructions, no provenance block\n")
    assert "just instructions" in art.instructions
    assert art.provenance == {}
