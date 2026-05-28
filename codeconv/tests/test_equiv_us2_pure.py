"""US2 pure layer — readiness (T023) + durable-step pure core (T024).

No bridge, no LM, no REPL: ``readiness`` is pure by design; the durable step's
PURE half (``compute_step_result``) is exercised here, while the bridge-write
half (``step_equiv``) is wired and tested at T025+.
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools.equiv import readiness as R
from codeconv.tools.equiv import recorded
from codeconv.tools.equiv import workflow as W


# --------------------------------------------------------------------------- #
# T023 — readiness                                                            #
# --------------------------------------------------------------------------- #
_NODES = {
    "lib/runtime/heap_fcp.dart": R.DepNode(topo_level=0, cycle_group_id=1),
    "lib/bytecode/runner.dart": R.DepNode(topo_level=1, cycle_group_id=2),
    "lib/compiler/parser.dart": R.DepNode(topo_level=2, cycle_group_id=3),
    "lib/multiagent/isolate.dart": R.DepNode(topo_level=3, cycle_group_id=4),
}
_DEPS = {
    "lib/runtime/heap_fcp.dart": frozenset(),
    "lib/bytecode/runner.dart": frozenset({"lib/runtime/heap_fcp.dart"}),
    "lib/compiler/parser.dart": frozenset({"lib/bytecode/runner.dart"}),
    "lib/multiagent/isolate.dart": frozenset({"lib/runtime/heap_fcp.dart"}),
}


def _subsystem_of(path: str):
    if path.startswith("lib/runtime/heap_fcp"):
        return "heap"
    if path.startswith("lib/bytecode/"):
        return "bytecode"
    if path.startswith("lib/compiler/"):
        return "compiler"
    if path.startswith("lib/multiagent/"):
        return "multiagent"
    if path.startswith("lib/runtime/"):
        return "runtime-core"
    return None


_HEAP = "lib/runtime/heap_fcp.dart"
_BC = "lib/bytecode/runner.dart"
_COMPILER = "lib/compiler/parser.dart"
_MULTI = "lib/multiagent/isolate.dart"


def test_not_ready_when_codegen_incomplete() -> None:
    assert R.classify(
        _HEAP, nodes=_NODES, cross_scc_deps=_DEPS,
        codegen={}, equiv={}, subsystem_of=_subsystem_of,
    ) == R.EQUIV_NOT_READY


def test_not_ready_when_unclassified() -> None:
    nodes = {"lib/zzz/x.dart": R.DepNode(0, 1)}
    assert R.classify(
        "lib/zzz/x.dart", nodes=nodes, cross_scc_deps={"lib/zzz/x.dart": frozenset()},
        codegen={"lib/zzz/x.dart": R.CodegenStatus(completed=True)},
        equiv={}, subsystem_of=_subsystem_of,
    ) == R.EQUIV_NOT_READY


def test_ready_when_no_deps_and_no_rows() -> None:
    cg = {_HEAP: R.CodegenStatus(completed=True)}
    assert R.classify(
        _HEAP, nodes=_NODES, cross_scc_deps=_DEPS,
        codegen=cg, equiv={}, subsystem_of=_subsystem_of,
    ) == R.EQUIV_READY


def test_not_ready_when_dep_not_equiv_done() -> None:
    # bytecode depends on heap; heap codegen-done but not equiv-done ⇒ blocked.
    cg = {_HEAP: R.CodegenStatus(True), _BC: R.CodegenStatus(True)}
    eq = {_HEAP: R.EquivStatus(total_in_scope=2, equivalent=1)}
    assert R.classify(
        _BC, nodes=_NODES, cross_scc_deps=_DEPS,
        codegen=cg, equiv=eq, subsystem_of=_subsystem_of,
    ) == R.EQUIV_NOT_READY


def test_done_when_all_equivalent() -> None:
    cg = {_HEAP: R.CodegenStatus(True)}
    eq = {_HEAP: R.EquivStatus(total_in_scope=3, equivalent=3)}
    assert R.classify(
        _HEAP, nodes=_NODES, cross_scc_deps=_DEPS,
        codegen=cg, equiv=eq, subsystem_of=_subsystem_of,
    ) == R.EQUIV_DONE


def test_in_progress_when_partial() -> None:
    cg = {_HEAP: R.CodegenStatus(True)}
    eq = {_HEAP: R.EquivStatus(total_in_scope=3, equivalent=1)}
    assert R.classify(
        _HEAP, nodes=_NODES, cross_scc_deps=_DEPS,
        codegen=cg, equiv=eq, subsystem_of=_subsystem_of,
    ) == R.EQUIV_IN_PROGRESS


def test_divergent_keeps_done_false() -> None:
    cg = {_HEAP: R.CodegenStatus(True)}
    eq = {_HEAP: R.EquivStatus(total_in_scope=3, equivalent=2, divergent=1)}
    assert R.classify(
        _HEAP, nodes=_NODES, cross_scc_deps=_DEPS,
        codegen=cg, equiv=eq, subsystem_of=_subsystem_of,
    ) == R.EQUIV_IN_PROGRESS


def test_select_next_curriculum_order_multiagent_last() -> None:
    # All four files codegen-done; their deps don't matter for ordering once
    # READY — what matters is the subsystem rank then topo_level.
    cg = {p: R.CodegenStatus(True) for p in _NODES}
    eq = {
        # Heap, bytecode, compiler done so their dependents are ready.
        _HEAP: R.EquivStatus(total_in_scope=1, equivalent=1),
        _BC: R.EquivStatus(total_in_scope=1, equivalent=1),
        _COMPILER: R.EquivStatus(total_in_scope=1, equivalent=1),
    }
    order = R.select_next(
        nodes=_NODES, cross_scc_deps=_DEPS,
        codegen=cg, equiv=eq, subsystem_of=_subsystem_of, limit=10,
    )
    # _HEAP/_BC/_COMPILER are DONE (not candidates); _MULTI is READY (deps done).
    assert order == [_MULTI]


def test_select_next_orders_strict_before_multiagent() -> None:
    # Fresh: heap ready (no deps); bytecode ready iff heap equiv-done; compiler
    # ready iff bytecode equiv-done; multiagent ready iff heap equiv-done.
    cg = {p: R.CodegenStatus(True) for p in _NODES}
    eq: dict[str, R.EquivStatus] = {}  # no rows yet ⇒ only files with no equiv-blocking deps are READY
    order = R.select_next(
        nodes=_NODES, cross_scc_deps=_DEPS,
        codegen=cg, equiv=eq, subsystem_of=_subsystem_of, limit=10,
    )
    # Only heap is READY (no deps); bytecode/compiler/multiagent gated.
    assert order == [_HEAP]


# --------------------------------------------------------------------------- #
# T024 — durable step PURE core (compute_step_result)                         #
# --------------------------------------------------------------------------- #
_GOLDEN = (
    "EV 1 BYTECODE_OP opcode=push pc=0\n"
    "EV 2 WRITER_BIND writer=0x10 shape=foo\n"
    "OUT succeed X=foo\n"
)
_DIVERGENT = (
    "EV 1 BYTECODE_OP opcode=pop pc=0\n"
    "EV 2 WRITER_BIND writer=0x99 shape=foo\n"
    "OUT succeed X=foo\n"
)

_SRC = "programs/tests/typed/p.glp"
_KEY = "lib/runtime/heap_fcp.dart"


def _repo(tmp_path: Path) -> Path:
    man = tmp_path / ".codeconv" / "equiv-manifest"
    man.mkdir(parents=True)
    (man / "corpus.yml").write_text(
        "version: 1\nsources:\n"
        f"  - {{ path: {_SRC}, suite: unified, compare_mode: trace, tier: strict, kind: file }}\n",
        encoding="utf-8",
    )
    (man / "subsystems.yml").write_text(
        "version: 1\nsubsystems:\n  heap:\n    tier: strict\n"
        '    path_prefixes:\n      - "lib/runtime/heap_fcp"\n'
        "split:\n  ratio: { train: 0.70, held_out: 0.30 }\n  assignments: {}\n",
        encoding="utf-8",
    )
    return tmp_path


def test_compute_step_result_bad_data_unknown_source(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    r = W.compute_step_result(repo, _KEY, "programs/not/in/corpus.glp")
    assert r.kind == W.KIND_BAD_DATA


def test_compute_step_result_bad_data_unclassified_key(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    r = W.compute_step_result(repo, "lib/zzz/unknown.dart", _SRC)
    assert r.kind == W.KIND_BAD_DATA


def test_compute_step_result_needs_agent_work_when_no_traces(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    r = W.compute_step_result(repo, _KEY, _SRC)
    assert r.kind == W.KIND_NEEDS_AGENT_WORK
    assert r.subsystem == "heap"
    assert r.tier == "strict"
    assert r.compare_mode == "trace"


def test_compute_step_result_equivalent(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    recorded.write_entry(repo, _KEY, _SRC, golden_trace=_GOLDEN, candidate_trace=_GOLDEN)
    r = W.compute_step_result(repo, _KEY, _SRC)
    assert r.kind == W.KIND_COMPARED
    assert r.verdict == "equivalent"
    assert r.subsystem == "heap"
    assert r.builds is True and r.trace_captured is True
    assert r.divergence is None


def test_compute_step_result_divergent(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    recorded.write_entry(repo, _KEY, _SRC, golden_trace=_GOLDEN, candidate_trace=_DIVERGENT)
    r = W.compute_step_result(repo, _KEY, _SRC)
    assert r.kind == W.KIND_COMPARED
    assert r.verdict == "divergent"
    assert isinstance(r.divergence, dict)
    assert r.divergence["event_kind"]  # the first divergence is identified


def test_compute_step_result_picks_up_bytecode_diff(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    recorded.write_entry(repo, _KEY, _SRC, golden_trace=_GOLDEN, candidate_trace=_GOLDEN,
                         golden_bc="0 push\n1 pop\n", candidate_bc="0 push\n1 push\n")
    r = W.compute_step_result(repo, _KEY, _SRC)
    assert r.kind == W.KIND_COMPARED
    assert r.bytecode_diff_empty is False  # bc differ ⇒ empty=False
