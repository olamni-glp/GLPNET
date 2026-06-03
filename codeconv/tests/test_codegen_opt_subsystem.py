"""T032 / T034 — per-subsystem dataset split + subsystem prompt selection.

PURE: no bridge, no real ``dotnet``, no LM. Exercises the feature-020
per-subsystem wiring of the offline optimizer:
- ``dataset.classify_examples`` / ``build_subsystem_examples`` /
  ``subsystem_split`` (deterministic, content-free) — T032;
- ``prompt.load(repo_root, subsystem)`` fallback chain
  (``<subsystem>.md`` → ``_base.md`` → ``optimized.md`` → baseline) — T034;
- ``run_optimize(subsystem=…, seed_instructions=…)`` records the subsystem
  in provenance and uses the carry-forward seed.
"""

from __future__ import annotations

import textwrap
from pathlib import Path

from codeconv.tools.codegen.buildgate import BuildResult
from codeconv.tools.codegen.prompt import (
    base_prompt_path,
    load,
    serialize,
    subsystem_prompt_path,
)
from codeconv.tools.codegen_opt import dataset as ds
from codeconv.tools.codegen_opt.optimize import run_optimize


def _manifest(repo_root: Path) -> None:
    p = repo_root / ".codeconv" / "equiv-manifest" / "subsystems.yml"
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(
        textwrap.dedent(
            """\
            version: 1
            subsystems:
              heap:
                tier: strict
                path_prefixes: ["lib/runtime/heap_fcp"]
              bytecode:
                tier: strict
                path_prefixes: ["lib/bytecode/"]
              runtime-core:
                tier: strict
                path_prefixes: ["lib/runtime/"]
            split:
              ratio: {train: 0.70, held_out: 0.30}
              assignments: {}
            """
        ),
        encoding="utf-8",
    )


def _example(repo_root: Path, rel_dart: str, unit: str) -> None:
    plan = repo_root / ".codeconv" / "conversion-plans" / (rel_dart + ".md")
    spec = repo_root / ".codeconv" / "conversion-specs" / (rel_dart + ".md")
    plan.parent.mkdir(parents=True, exist_ok=True)
    spec.parent.mkdir(parents=True, exist_ok=True)
    plan.write_text(
        f"---\npath: {rel_dart}\ntarget_code_unit: {unit}\n---\n## plan\n",
        encoding="utf-8",
    )
    spec.write_text("# spec\n", encoding="utf-8")


def test_classify_examples_groups_by_subsystem(tmp_path: Path) -> None:
    _manifest(tmp_path)
    _example(tmp_path, "lib/bytecode/opcodes.dart", "Opcodes")
    _example(tmp_path, "lib/bytecode/asm.dart", "Asm")
    _example(tmp_path, "lib/runtime/heap_fcp.dart", "HeapFcp")
    _example(tmp_path, "lib/runtime/commit.dart", "Commit")
    groups = ds.classify_examples(tmp_path, ds.build_examples(tmp_path))
    assert {e.rel_path for e in groups["bytecode"]} == {
        "lib/bytecode/opcodes.dart",
        "lib/bytecode/asm.dart",
    }
    # longest-prefix-wins: heap_fcp goes to `heap`, not `runtime-core`.
    assert {e.rel_path for e in groups["heap"]} == {"lib/runtime/heap_fcp.dart"}
    assert {e.rel_path for e in groups["runtime-core"]} == {"lib/runtime/commit.dart"}


def test_build_subsystem_examples_filters(tmp_path: Path) -> None:
    _manifest(tmp_path)
    _example(tmp_path, "lib/bytecode/opcodes.dart", "Opcodes")
    _example(tmp_path, "lib/runtime/commit.dart", "Commit")
    bc = ds.build_subsystem_examples(tmp_path, "bytecode")
    assert [e.rel_path for e in bc] == ["lib/bytecode/opcodes.dart"]


def test_subsystem_split_is_deterministic_and_partitions(tmp_path: Path) -> None:
    _manifest(tmp_path)
    for i in range(20):
        _example(tmp_path, f"lib/bytecode/f{i:02d}.dart", f"F{i}")
    ex = ds.build_subsystem_examples(tmp_path, "bytecode")
    train1, held1 = ds.subsystem_split(ex)
    train2, held2 = ds.subsystem_split(ex)
    assert [e.rel_path for e in train1] == [e.rel_path for e in train2]
    assert [e.rel_path for e in held1] == [e.rel_path for e in held2]
    paths = {e.rel_path for e in ex}
    t, h = {e.rel_path for e in train1}, {e.rel_path for e in held1}
    assert t | h == paths
    assert not (t & h)
    # content-free ~70/30: both buckets non-trivial on n=20.
    assert 0 < len(held1) < len(ex)


def test_load_prefers_subsystem_then_base_then_baseline(tmp_path: Path) -> None:
    # Nothing checked in ⇒ shipped baseline.
    assert load(tmp_path, "bytecode").is_baseline is True
    # _base.md present ⇒ used for ANY subsystem and for the global load.
    bp = base_prompt_path(tmp_path)
    bp.parent.mkdir(parents=True, exist_ok=True)
    bp.write_text(serialize("BASE PROMPT\n", {"optimizer": "seed"}), encoding="utf-8")
    assert load(tmp_path, "bytecode").instructions.strip() == "BASE PROMPT"
    assert load(tmp_path).instructions.strip() == "BASE PROMPT"
    assert load(tmp_path, "bytecode").is_baseline is False
    # A subsystem-specific prompt overrides _base for THAT subsystem only.
    subsystem_prompt_path(tmp_path, "bytecode").write_text(
        serialize("BYTECODE PROMPT\n", {"optimizer": "gepa", "subsystem": "bytecode"}),
        encoding="utf-8",
    )
    assert load(tmp_path, "bytecode").instructions.strip() == "BYTECODE PROMPT"
    assert load(tmp_path, "heap").instructions.strip() == "BASE PROMPT"


def test_run_optimize_subsystem_records_provenance_and_uses_seed(tmp_path: Path) -> None:
    _manifest(tmp_path)
    _example(tmp_path, "lib/bytecode/opcodes.dart", "Opcodes")
    _example(tmp_path, "lib/bytecode/asm.dart", "Asm")

    def gen(instr: str, ex) -> str:
        cls = ex.expected_units[0] if ex.expected_units else "Gen"
        return f"namespace D;\npublic class {cls} {{ public int V; }}\n"

    def build(_proj: Path) -> BuildResult:
        return BuildResult(status="pass")

    def propose(instr: str, _refl: list[str]) -> str:
        return instr  # no improvement ⇒ best stays the seed

    res = run_optimize(
        tmp_path,
        subsystem="bytecode",
        seed_instructions="SEED INSTRUCTIONS\n",
        budget=50,
        generate_fn=gen,
        build_fn=build,
        propose_fn=propose,
        max_rounds=1,
    )
    assert res.subsystem == "bytecode"
    assert res.provenance()["subsystem"] == "bytecode"
    assert res.best_instructions.strip() == "SEED INSTRUCTIONS"
