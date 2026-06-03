"""codeconv-equiv tool — the deterministic, LM-free differential
trace-equivalence oracle (feature 020).

Per ``specs/020-trace-equivalence-fidelity/contracts/equiv_cli.md``:

- Exports ``app: typer.Typer`` (feature 012 FR-006 auto-discovery — no
  ``runner.py``/``cli.py`` edit needed; the runner discovers this
  subpackage by its exported ``app``).
- Will export ``register_workflows(dbos_app)`` once the durable ``equiv``
  step lands (US2 / T024–T025). The step is a pure verdict ingest of
  recorded traces — it NEVER spawns a REPL or reads wall-clock (R12).

Subcommand surface (equiv_cli.md) — added incrementally:

- ``status``  (default — bare ``codeconv equiv`` invokes it).        [skeleton here; full in T026]
- ``capture <key> <source>``                                          [US1 / T018]
- ``compare`` / ``bytecode-diff``                                     [US1 / T019]
- ``next`` / ``ingest`` / ``retry`` / ``aggregate-escalations``       [US2 / T026–T027]
- ``fidelity <key>`` / ``promote <subsystem>`` / ``mark-stale``       [US5 / T043–T044]

This Python CLI owns ALL deterministic equivalence state and imports NO
dspy/litellm/openai (SC-008 — guarded by ``test_no_lm_on_production_path``).
Trace CAPTURE (the nondeterministic REPL spawn) lives in the CLI/agent
layer here, never inside a DBOS step (R12). Top-level flags
(``--repo-root``, ``--data-dir``, ``--quiet``, ``--json``) propagate from
the ``codeconv`` console script via ``typer.Context``.

T002 scope: skeleton only — the Typer app + a minimal ``status`` that does
not require migration ``0008`` to be applied. Real status (frontier in
curriculum order, per-subsystem fidelity rollup ≤5 s warm) is T026.
"""

from __future__ import annotations

import json as _json
import sys as _sys
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Optional

import typer

from codeconv.tools.equiv import corpus as _corpus
from codeconv.tools.equiv import manifest as _manifest
from codeconv.tools.equiv import recorded as _recorded
from codeconv.tools.equiv import verdict as _verdict

# Exit codes (equiv_cli.md): 0 ok/equivalent · 2 divergent · 3 needs_agent_work
# · 64 bad data / config. A divergent verdict / needs_agent_work are VERDICTS,
# never uncaught exceptions (DISCIPLINE §1.7).
EXIT_OK = 0
EXIT_DIVERGENT = 2
EXIT_NEEDS_AGENT_WORK = 3
EXIT_BAD_DATA = 64

# Reserved pseudo-key for the source-keyed bytecode-emission checkpoint
# (FR-004 is per-source compiler output, not per-converted-file).
_EMIT_KEY = "_emit"

_SUBSYS_PARTS = (".codeconv", "equiv-manifest", "subsystems.yml")


app = typer.Typer(
    add_completion=False,
    help=(
        "Differential trace-equivalence oracle "
        "(Dart golden vs converted-C# candidate; tiered fidelity gate)."
    ),
    invoke_without_command=True,
    no_args_is_help=False,
)


def _ctx_repo_root(ctx: typer.Context) -> Path:
    return Path(ctx.obj["repo_root"]) if ctx.obj else Path.cwd()


def _ctx_data_dir(ctx: typer.Context) -> Optional[Path]:
    return ctx.obj.get("data_dir") if ctx.obj else None


@app.callback(invoke_without_command=True)
def _default(ctx: typer.Context) -> None:
    """Bare ``codeconv equiv`` ⇒ ``status`` (spawns nothing, writes nothing).

    NB: we call ``_run_status`` directly rather than ``ctx.invoke(status)``
    — under this Click/Typer version ``ctx.invoke`` does not inject the
    ``typer.Context`` first parameter, so the indirect form raises
    ``TypeError: status() missing 1 required positional argument: 'ctx'``
    (the same latent bug the 019 codegen tool's bare path exhibits).
    """
    if ctx.invoked_subcommand is None:
        _run_status(json_summary=False)


@app.command("status")
def status(
    ctx: typer.Context,
    json_summary: bool = typer.Option(
        False, "--json", help="Emit JSON summary on stdout."
    ),
) -> None:
    """Equivalence frontier + per-subsystem fidelity rollup; no agents, no writes.

    T002 skeleton: reports that the oracle is scaffolded and which build
    stages remain. The bridge-backed rollup (querying ``dart_equivalence``)
    is implemented in T026 — kept out of the skeleton so this command works
    before migration ``0008`` is applied.
    """
    _run_status(json_summary=json_summary)


def _run_status(*, json_summary: bool) -> None:
    summary = {
        "tool": "equiv",
        "phase": "scaffolded",
        "note": "oracle skeleton (T002); subcommands land in US1+ (T018/T019/T026)",
    }
    if json_summary:
        typer.echo(_json.dumps(summary, indent=2, sort_keys=True))
    else:
        typer.echo("equiv: scaffolded (T002) — capture/compare/next/promote pending US1+.")


# =========================================================================== #
# US1 — capture / compare / bytecode-diff                                     #
#                                                                             #
# capture (T018) is the nondeterministic REPL-spawn layer (lives HERE, never  #
# in a DBOS step — R12). compare / bytecode-diff (T019) are STANDALONE         #
# deterministic verdict-writers over the recorded artifacts — no DBOS         #
# dependency, so US1 is an independently-testable conformance harness. The     #
# durable ``equiv`` step that WRAPS compare lands in US2 (T024).               #
# =========================================================================== #


def _err(msg: str) -> None:
    print(f"equiv: {msg}", file=_sys.stderr)


def _corpus_index(repo_root: Path) -> dict[str, "_corpus.CorpusSource"]:
    return {s.path: s for s in _corpus.load(repo_root)}


def _resolve_source(repo_root: Path, source_path: str) -> Optional["_corpus.CorpusSource"]:
    return _corpus_index(repo_root).get(source_path)


def _resolve_subsystem(repo_root: Path, tombstone_key: str) -> Optional[str]:
    """File-derived subsystem (g2=a): classify the CONVERTED file's Dart key via
    the manifest prefixes. ``tombstone_key`` is the converted file's inventory
    key (its Dart rel path)."""
    m = _manifest.load(repo_root.joinpath(*_SUBSYS_PARTS))
    return m.subsystem_for(tombstone_key)


# --- capture backend (injectable so orchestration is unit-testable) -------- #
@dataclass(frozen=True)
class CaptureOutput:
    """What a capture backend produces from one (golden, candidate) REPL run."""

    golden_trace: Optional[str] = None
    candidate_trace: Optional[str] = None
    golden_bc: Optional[str] = None
    candidate_bc: Optional[str] = None
    builds: bool = False
    reason: Optional[str] = None  # why a side is missing (needs_agent_work)


CaptureBackend = Callable[[Path, "_corpus.CorpusSource"], CaptureOutput]


def _default_capture_backend(repo_root: Path, source: "_corpus.CorpusSource") -> CaptureOutput:
    """Live capture is gated on the converted C# REPL + the Dart ``:trace``→wire
    adapter (T017/T022, B1). Until that instrumentation exists this honestly
    yields ``needs_agent_work`` rather than fabricating a trace (CLAUDE.md: no
    groping in the dark). The REPL spawn (nondeterministic) belongs here, not in
    any DBOS step (R12)."""
    return CaptureOutput(
        reason=(
            "candidate C# REPL trace instrumentation not yet available "
            "(T017); no recorded trace captured"
        )
    )


def _run_capture(
    *,
    repo_root: Path,
    tombstone_key: str,
    source_path: str,
    backend: CaptureBackend = _default_capture_backend,
) -> int:
    """Write the recorded trace artifacts for one (key × source). No DB write —
    persistence (``phase='captured'`` in ``dart_equivalence``) is the durable
    ``equiv`` step's job (T024); capture only produces the recorded inputs."""
    source = _resolve_source(repo_root, source_path)
    if source is None:
        _err(f"source {source_path!r} not in corpus (.codeconv/equiv-manifest/corpus.yml)")
        return EXIT_BAD_DATA
    subsystem = _resolve_subsystem(repo_root, tombstone_key)
    if subsystem is None:
        _err(f"tombstone_key {tombstone_key!r} classifies to no subsystem")
        return EXIT_BAD_DATA

    out = backend(repo_root, source)
    if out.golden_trace is None or out.candidate_trace is None:
        # Typed sentinel — a verdict, not a crash (R12 / DISCIPLINE §1.7).
        print(_json.dumps({"phase": "needs_agent_work", "reason": out.reason}))
        return EXIT_NEEDS_AGENT_WORK

    _recorded.write_entry(
        repo_root,
        tombstone_key,
        source_path,
        golden_trace=out.golden_trace,
        candidate_trace=out.candidate_trace,
        golden_bc=out.golden_bc,
        candidate_bc=out.candidate_bc,
        meta={
            "subsystem": subsystem,
            "tier": source.tier,
            "compare_mode": source.compare_mode,
            "split": source.split,
            "builds": out.builds,
        },
    )
    print(_json.dumps({"phase": "captured", "tombstone_key": tombstone_key,
                       "source_path": source_path, "subsystem": subsystem}))
    return EXIT_OK


def _run_compare(
    *,
    repo_root: Path,
    tombstone_key: str,
    source_path: str,
) -> int:
    """Standalone deterministic verdict from recorded traces (no DB write).

    The durable ``equiv`` step (T024) is the sole ``dart_equivalence`` writer;
    this CLI is the independently-testable conformance harness — verdict + exit
    code + the divergence record (which is ALSO the GEPA reflective feedback)."""
    source = _resolve_source(repo_root, source_path)
    if source is None:
        _err(f"source {source_path!r} not in corpus (.codeconv/equiv-manifest/corpus.yml)")
        return EXIT_BAD_DATA
    subsystem = _resolve_subsystem(repo_root, tombstone_key)
    if subsystem is None:
        _err(f"tombstone_key {tombstone_key!r} classifies to no subsystem")
        return EXIT_BAD_DATA

    entry = _recorded.load_entry(repo_root, tombstone_key, source_path)
    if not entry.have_traces:
        print(_json.dumps({"phase": "needs_agent_work",
                           "reason": "no recorded golden+candidate trace (run `equiv capture`)"}))
        return EXIT_NEEDS_AGENT_WORK

    result = _verdict.compare_recorded(
        entry.golden_trace,  # type: ignore[arg-type]
        entry.candidate_trace,  # type: ignore[arg-type]
        compare_mode=source.compare_mode,
        tier=source.tier,
    )
    divergence = (
        _verdict.divergence_to_dict(result.verdict.divergence)
        if result.verdict.divergence is not None
        else None
    )
    out = {
        "verdict": "equivalent" if result.equivalent else "divergent",
        "tombstone_key": tombstone_key,
        "source_path": source_path,
        "subsystem": subsystem,
        "compare_mode": source.compare_mode,
        "tier": source.tier,
        "divergence": divergence,
    }
    print(_json.dumps(out, indent=2, sort_keys=True))
    return EXIT_OK if result.equivalent else EXIT_DIVERGENT


def _run_bytecode_diff(*, repo_root: Path, source_path: str) -> int:
    if _resolve_source(repo_root, source_path) is None:
        _err(f"source {source_path!r} not in corpus")
        return EXIT_BAD_DATA
    entry = _recorded.load_entry(repo_root, _EMIT_KEY, source_path)
    if not entry.have_bytecode:
        print(_json.dumps({"phase": "needs_agent_work",
                           "reason": "no recorded golden+candidate emitted bytecode"}))
        return EXIT_NEEDS_AGENT_WORK
    bd = _verdict.compare_bytecode(entry.golden_bc, entry.candidate_bc)  # type: ignore[arg-type]
    print(_json.dumps(_verdict.bytecode_diff_to_dict(bd), indent=2, sort_keys=True))
    return EXIT_OK if bd.empty else EXIT_DIVERGENT


@app.command("capture")
def capture(
    ctx: typer.Context,
    tombstone_key: str = typer.Argument(..., help="Converted file key (Dart rel path)."),
    source_path: str = typer.Argument(..., help="In-scope GLP source (under programs/)."),
) -> None:
    """Run Dart (golden) + C# (candidate) REPLs, normalize, record traces.
    Nondeterministic spawn lives HERE (R12); writes recorded artifacts only —
    the durable equiv step (T024) records phase/verdict in dart_equivalence."""
    raise typer.Exit(
        _run_capture(
            repo_root=_ctx_repo_root(ctx),
            tombstone_key=tombstone_key,
            source_path=source_path,
        )
    )


@app.command("compare")
def compare(
    ctx: typer.Context,
    tombstone_key: str = typer.Argument(..., help="Converted file key (Dart rel path)."),
    source_path: str = typer.Argument(..., help="In-scope GLP source (under programs/)."),
) -> None:
    """Standalone deterministic verdict from recorded traces (relation.py).
    Verdict-only — the durable equiv step (T024) is the sole dart_equivalence
    writer. Exit 0 equivalent / 2 divergent / 3 needs_agent_work."""
    raise typer.Exit(
        _run_compare(
            repo_root=_ctx_repo_root(ctx),
            tombstone_key=tombstone_key,
            source_path=source_path,
        )
    )


@app.command("bytecode-diff")
def bytecode_diff(
    ctx: typer.Context,
    source_path: str = typer.Argument(..., help="In-scope GLP source (under programs/)."),
) -> None:
    """Early checkpoint (FR-004): C#-emitted vs Dart-emitted bytecode for a
    source. Exit 0 empty / 2 differs / 3 needs_agent_work."""
    raise typer.Exit(
        _run_bytecode_diff(repo_root=_ctx_repo_root(ctx), source_path=source_path)
    )
