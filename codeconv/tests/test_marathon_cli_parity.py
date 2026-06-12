"""Polish — CLI ↔ library 1:1 parity (T051).

FR-025: the harness is drivable as a library and via a thin CLI kept in
one-to-one correspondence. ``contracts/cli.md`` is the normative table: each
subcommand is a thin wrapper over exactly the library function(s) its row
names (flag-dispatched rows like ``gate`` name the per-variant function).

Mechanically enforced, bridge-free: the Typer apps are introspected for the
registered subcommand set, and each command callback's source is scanned for
its ``from codeconv.marathon.<mod> import <fn>`` lines (the wiring keeps
library imports inside command bodies — T018). Direction 1: every subcommand
imports exactly its contracted function(s), nothing more. Direction 2: every
contracted library function is wired by exactly one subcommand (``position``
shares ``resume``'s impl — the documented alias row), exists in its module,
and is callable.
"""

from __future__ import annotations

import importlib
import inspect
import re

from codeconv.marathon import keeper_app, marathon_app

# contracts/cli.md "Library call" column, by implementation name (cli.md's
# `resume(run)` is the library-api name; the wiring imports the canonical
# `resume_position` — position.py exports `resume` as its alias, T016).
PARITY: dict[str, set[tuple[str, str]]] = {
    "register": {("stages", "register_run")},
    "append-stage": {("stages", "append_stage")},
    "stage-start": {("checkpoint", "start_stage")},
    "checkpoint": {("checkpoint", "checkpoint")},
    "capture": {("intake", "capture_item")},
    "resume": {("position", "resume_position")},
    "position": {("position", "resume_position")},  # alias of `resume` (cli.md)
    "status": {("status", "status_line"), ("status", "emit_status")},
    "gate": {("gate", "present_gate"), ("gate", "record_decision")},
    "rerun": {("orchestrate", "rerun_block"), ("orchestrate", "rerun_subagent")},
    "trace": {("trace", "write_trace")},
    "reconcile": {("store", "reconcile")},
    "finalize": {("stages", "finalize")},
    "keeper start": {("keeper", "start_keeper")},
    "keeper stop": {("keeper", "stop_keeper")},
    "keeper recover": {("keeper", "recover_keeper")},
    "doctor": {("keeper", "doctor")},
}

# Shared glue, not part of the parity surface: env resolution, the store
# accessor, and the escalation exception types handled by `_execute`.
INFRA_NAMES = {
    "Repository",
    "resolve_env",
    "StoreRootInsideRepoError",
    "PrereqAgainstCompletedStage",
    "ConcurrentWriter",
}

_IMPORT_RE = re.compile(r"from codeconv\.marathon\.(\w+) import ([\w, ]+)")


def _command_callbacks() -> dict[str, object]:
    """The registered subcommand set, keyed as in PARITY (`keeper <sub>`)."""
    commands: dict[str, object] = {}
    for info in marathon_app.registered_commands:
        commands[info.name] = info.callback
    group_names = [g.name for g in marathon_app.registered_groups]
    assert group_names == ["keeper"], group_names
    for info in keeper_app.registered_commands:
        commands[f"keeper {info.name}"] = info.callback
    return commands


def _library_imports(callback) -> set[tuple[str, str]]:
    """(module, name) pairs the callback's body imports, infra filtered out.

    `resume` and `position` are thin shims over `_cmd_resume_impl`; their
    effective source includes the shared impl (the alias row in cli.md).
    """
    source = inspect.getsource(callback)
    if "_cmd_resume_impl" in source:
        from codeconv.marathon import _cmd_resume_impl

        source += inspect.getsource(_cmd_resume_impl)
    found: set[tuple[str, str]] = set()
    for module, names in _IMPORT_RE.findall(source):
        for raw in names.split(","):
            name = raw.strip().split(" as ")[0].strip()  # `checkpoint as _checkpoint`
            if name and name not in INFRA_NAMES:
                found.add((module, name))
    return found


def test_every_subcommand_is_contracted() -> None:
    """The registered CLI surface equals the cli.md table — both ways."""
    assert set(_command_callbacks()) == set(PARITY)


def test_cli_to_library_exactly_the_contracted_functions() -> None:
    """Direction 1: each subcommand imports exactly its contracted set."""
    for name, callback in _command_callbacks().items():
        assert _library_imports(callback) == PARITY[name], name


def test_library_to_cli_exactly_one_subcommand() -> None:
    """Direction 2: each contracted function is wired by exactly one
    subcommand and exists as a public callable in its module."""
    wired: dict[tuple[str, str], list[str]] = {}
    for name, callback in _command_callbacks().items():
        for pair in _library_imports(callback):
            wired.setdefault(pair, []).append(name)
    for pair, subcommands in sorted(wired.items()):
        if pair == ("position", "resume_position"):
            # The one documented alias: `position` surfaces `resume`'s row.
            assert sorted(subcommands) == ["position", "resume"], pair
        else:
            assert len(subcommands) == 1, (pair, subcommands)
    for module_name, fn_name in {p for pairs in PARITY.values() for p in pairs}:
        module = importlib.import_module(f"codeconv.marathon.{module_name}")
        fn = getattr(module, fn_name)
        assert callable(fn), (module_name, fn_name)
        assert not fn_name.startswith("_"), (module_name, fn_name)
