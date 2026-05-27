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
from pathlib import Path

import typer


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
