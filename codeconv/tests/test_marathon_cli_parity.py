"""Bridge-free CLI↔library parity guard (T051, FR-025).

``contracts/cli.md`` declares each ``codeconv marathon`` subcommand a thin
wrapper over exactly one public library function (documented multi-mode rows —
``status``, ``gate``, ``rerun`` — name the one function per mode; ``position``
is the one documented alias of ``resume``). This test pins that table:

1. the registered Typer surface equals the contract's subcommand set —
   a new subcommand cannot land without a contract row, a removed one
   cannot linger in the contract;
2. every declared library function is importable and callable —
   a rename breaks parity loudly;
3. each subcommand's callback actually references its declared function(s) —
   the table cannot drift from the wiring;
4. vice-versa: after folding the documented ``position``→``resume`` alias,
   no library function is wired to two different subcommands.
"""

from __future__ import annotations

import importlib
import inspect

import codeconv.marathon as marathon_cli
from codeconv.marathon import keeper_app, marathon_app

# subcommand -> public library call(s), verbatim from contracts/cli.md (FR-025).
PARITY: dict[str, tuple[str, ...]] = {
    "register": ("codeconv.marathon.stages:register_run",),
    "append-stage": ("codeconv.marathon.stages:append_stage",),
    "stage-start": ("codeconv.marathon.checkpoint:start_stage",),
    "checkpoint": ("codeconv.marathon.checkpoint:checkpoint",),
    "capture": ("codeconv.marathon.intake:capture_item",),
    "resume": ("codeconv.marathon.position:resume_position",),
    # cli.md: `position` is the documented alias of `resume` (same call).
    "position": ("codeconv.marathon.position:resume_position",),
    "status": (
        "codeconv.marathon.status:status_line",
        "codeconv.marathon.status:emit_status",
    ),
    "gate": (
        "codeconv.marathon.gate:present_gate",
        "codeconv.marathon.gate:record_decision",
    ),
    "rerun": (
        "codeconv.marathon.orchestrate:rerun_block",
        "codeconv.marathon.orchestrate:rerun_subagent",
    ),
    "trace": ("codeconv.marathon.trace:write_trace",),
    "reconcile": ("codeconv.marathon.store:reconcile",),
    "finalize": ("codeconv.marathon.stages:finalize",),
    "doctor": ("codeconv.marathon.keeper:doctor",),
    "keeper start": ("codeconv.marathon.keeper:start_keeper",),
    "keeper stop": ("codeconv.marathon.keeper:stop_keeper",),
    "keeper recover": ("codeconv.marathon.keeper:recover_keeper",),
}

ALIAS_OF: dict[str, str] = {"position": "resume"}


def _registered_subcommands() -> dict[str, object]:
    """name -> Typer callback for every registered marathon subcommand."""
    commands: dict[str, object] = {}
    for info in marathon_app.registered_commands:
        commands[info.name] = info.callback
    for info in keeper_app.registered_commands:
        commands[f"keeper {info.name}"] = info.callback
    return commands


def _resolve(target: str):
    module_name, func_name = target.split(":")
    module = importlib.import_module(module_name)
    return getattr(module, func_name)


def test_cli_surface_equals_contract_table():
    """1:1 — no unmapped subcommand, no stale contract row (FR-025)."""
    assert set(_registered_subcommands()) == set(PARITY)
    # `keeper` is registered as a sub-app, not a plain command.
    group_names = {g.name for g in marathon_app.registered_groups}
    assert "keeper" in group_names


def test_declared_library_functions_exist_and_are_callable():
    for subcommand, targets in PARITY.items():
        for target in targets:
            func = _resolve(target)
            assert callable(func), f"{subcommand}: {target} is not callable"


def test_each_subcommand_wires_its_declared_function():
    """The callback body references the declared function — table ≡ wiring.

    ``resume``/``position`` share the module-level ``_cmd_resume_impl``
    helper; their effective source includes it.
    """
    for subcommand, callback in _registered_subcommands().items():
        source = inspect.getsource(callback)
        if "_cmd_resume_impl" in source:
            source += inspect.getsource(marathon_cli._cmd_resume_impl)
        for target in PARITY[subcommand]:
            func_name = target.split(":")[1]
            assert func_name in source, (
                f"`{subcommand}` does not reference its declared library "
                f"function `{func_name}` (contracts/cli.md drift)"
            )


def test_no_library_function_serves_two_subcommands():
    """Vice-versa parity: one function, one subcommand (alias folded)."""
    owners: dict[str, str] = {}
    for subcommand, targets in PARITY.items():
        if subcommand in ALIAS_OF:
            continue  # documented alias — not a second owner
        for target in targets:
            assert target not in owners, (
                f"{target} is wired to both `{owners[target]}` and "
                f"`{subcommand}` — parity requires exactly one owner"
            )
            owners[target] = subcommand
