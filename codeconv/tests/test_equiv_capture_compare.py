"""US1 capture/compare standalone harness (T018/T019) — PURE, no bridge/runtime.

Exercises the recorded-artifact seam + the deterministic verdict path with a
stub capture backend. ``capture``/``compare`` write NO DB row (the durable
equiv step, T024, is the sole dart_equivalence writer); the live REPL spawn is
runtime-gated (B1). This is the only coverage of the standalone conformance
harness until the runtime-coupled e2e (T022).
"""

from __future__ import annotations

from pathlib import Path

from codeconv.tools import equiv
from codeconv.tools.equiv import recorded
from codeconv.tools.equiv.bytecode_diff import EmittedOp

_GOLDEN = (
    "EV 1 BYTECODE_OP opcode=push pc=0\n"
    "EV 2 WRITER_BIND writer=0x10 shape=foo\n"
    "OUT succeed X=foo\n"
)
# Eager writer bind at a different opcode ⇒ divergent.
_DIVERGENT = (
    "EV 1 BYTECODE_OP opcode=pop pc=0\n"
    "EV 2 WRITER_BIND writer=0x99 shape=foo\n"
    "OUT succeed X=foo\n"
)

_SOURCE = "programs/tests/typed/p.glp"
_KEY = "lib/runtime/heap_fcp.dart"  # classifies to subsystem `heap`


def _repo(tmp_path: Path) -> Path:
    """A minimal repo_root with the two equiv-manifest artifacts."""
    man = tmp_path / ".codeconv" / "equiv-manifest"
    man.mkdir(parents=True)
    (man / "corpus.yml").write_text(
        "version: 1\nsources:\n"
        f"  - {{ path: {_SOURCE}, suite: unified, compare_mode: trace, tier: strict, kind: file }}\n",
        encoding="utf-8",
    )
    (man / "subsystems.yml").write_text(
        "version: 1\nsubsystems:\n"
        "  heap:\n    tier: strict\n    path_prefixes:\n"
        '      - "lib/runtime/heap_fcp"\n'
        "split:\n  ratio: { train: 0.70, held_out: 0.30 }\n  assignments: {}\n",
        encoding="utf-8",
    )
    return tmp_path


def _stub_backend(golden: str, candidate: str):
    def backend(repo_root, source):  # noqa: ANN001
        return equiv.CaptureOutput(
            golden_trace=golden, candidate_trace=candidate, builds=True
        )

    return backend


# --------------------------------------------------------------------------- #
# recorded.py serde
# --------------------------------------------------------------------------- #
def test_recorded_round_trip(tmp_path: Path) -> None:
    recorded.write_entry(
        tmp_path, _KEY, _SOURCE, golden_trace=_GOLDEN, candidate_trace=_GOLDEN
    )
    entry = recorded.load_entry(tmp_path, _KEY, _SOURCE)
    assert entry.have_traces
    assert entry.golden_trace == _GOLDEN
    assert not entry.have_bytecode  # none written


def test_emitted_bytecode_serde() -> None:
    ops = [EmittedOp(0, "push", ("a",)), EmittedOp(1, "pop")]
    assert recorded.text_to_emitted(recorded.emitted_to_text(ops)) == ops


# --------------------------------------------------------------------------- #
# capture orchestration (no DB write)
# --------------------------------------------------------------------------- #
def test_default_backend_is_needs_agent_work(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    code = equiv._run_capture(
        repo_root=repo, tombstone_key=_KEY, source_path=_SOURCE
    )
    assert code == equiv.EXIT_NEEDS_AGENT_WORK


def test_capture_unknown_source_is_bad_data(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    code = equiv._run_capture(
        repo_root=repo, tombstone_key=_KEY, source_path="programs/not/in/corpus.glp"
    )
    assert code == equiv.EXIT_BAD_DATA


def test_capture_unclassified_key_is_bad_data(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    code = equiv._run_capture(
        repo_root=repo, tombstone_key="lib/zzz/unknown.dart", source_path=_SOURCE,
        backend=_stub_backend(_GOLDEN, _GOLDEN),
    )
    assert code == equiv.EXIT_BAD_DATA


def test_capture_then_compare_equivalent(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    cap = equiv._run_capture(
        repo_root=repo, tombstone_key=_KEY, source_path=_SOURCE,
        backend=_stub_backend(_GOLDEN, _GOLDEN),
    )
    assert cap == equiv.EXIT_OK
    cmp = equiv._run_compare(
        repo_root=repo, tombstone_key=_KEY, source_path=_SOURCE
    )
    assert cmp == equiv.EXIT_OK


def test_capture_then_compare_divergent(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    equiv._run_capture(
        repo_root=repo, tombstone_key=_KEY, source_path=_SOURCE,
        backend=_stub_backend(_GOLDEN, _DIVERGENT),
    )
    cmp = equiv._run_compare(
        repo_root=repo, tombstone_key=_KEY, source_path=_SOURCE
    )
    assert cmp == equiv.EXIT_DIVERGENT


def test_compare_without_capture_is_needs_agent_work(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    code = equiv._run_compare(
        repo_root=repo, tombstone_key=_KEY, source_path=_SOURCE
    )
    assert code == equiv.EXIT_NEEDS_AGENT_WORK


# --------------------------------------------------------------------------- #
# bytecode-diff checkpoint
# --------------------------------------------------------------------------- #
def test_bytecode_diff_empty_and_differ(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    bc = "0 push\n1 pop\n"
    recorded.write_entry(repo, equiv._EMIT_KEY, _SOURCE, golden_bc=bc, candidate_bc=bc)
    assert equiv._run_bytecode_diff(repo_root=repo, source_path=_SOURCE) == equiv.EXIT_OK

    recorded.write_entry(
        repo, equiv._EMIT_KEY, _SOURCE, golden_bc=bc, candidate_bc="0 push\n1 push\n"
    )
    assert equiv._run_bytecode_diff(repo_root=repo, source_path=_SOURCE) == equiv.EXIT_DIVERGENT


def test_bytecode_diff_missing_is_needs_agent_work(tmp_path: Path) -> None:
    repo = _repo(tmp_path)
    code = equiv._run_bytecode_diff(repo_root=repo, source_path=_SOURCE)
    assert code == equiv.EXIT_NEEDS_AGENT_WORK
