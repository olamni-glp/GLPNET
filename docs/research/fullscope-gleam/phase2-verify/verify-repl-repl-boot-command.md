<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-repl-repl-boot-command` (WP b3-c1-006, wave 2)

**Date**: 2026-07-21
**Method**: source-verification (`glp/repl/commands.gleam`, `glp/repl/repl.gleam`) + scripted drive of the Gleam entry point (`gleam run`).
**Paired close**: `close-repl-repl-boot-command` — **activated** for the two ABSENT commands (with an engineer scope question, below).

## Verdict table

| # | detail_id | verdict | basis |
|---|-----------|---------|-------|
| 1 | `repl-trace-command` | **DELIVERED** | `:trace`/`:t` toggles; run threads trace → reduction-trace lines. Sub-gap: no separate `:debug` toggle |
| 2 | `repl-limit-command` | **DELIVERED** | `:limit <n>` sets reduction fuel; malformed → usage line; exhaustion → OutOfFuel/Failed |
| 3 | `repl-boot-command` | **ABSENT** (capability-absent) | no `:boot`; multiagent-boot subsystem not ported (US3/G2) |
| 4 | `repl-bytecode-command` | **ABSENT as REPL command** (capability relocated to lib) | no `:bytecode`; disassembly exists in `bytecode/program.gleam`, not exposed interactively |

**Tally**: DELIVERED 2 · ABSENT 2.

## Evidence

The complete Gleam REPL `Command` set (`glp/repl/commands.gleam:21-33`) is: `Quit` (`:quit`/`:q`), `ToggleTrace` (`:trace`/`:t`), `SetLimit`/`LimitUsage` (`:limit <n>`), `Load`, `Goal`, `Blank`. There is **no** `:boot` and **no** `:bytecode`; `repl.gleam:1-8` states the surface is deliberately "Scripted, newline-fed stdin with a non-interactive EOF exit (the corpus runner pipes `load …` + goals)".

**Scripted drive** (`printf ':trace\n:limit 500\n:boot\n:bytecode foo\n:quit\n' | gleam run`):
```
GLP> Trace enabled                 # :trace  → repl-trace-command
GLP> Goal reduction limit set to 500   # :limit → repl-limit-command
GLP> → failed
     Error: parse: LexError("Unexpected character :", 1, 1)   # :boot → not a command, lexed as a goal
GLP> → failed
     Error: parse: LexError("Unexpected character :", 1, 1)   # :bytecode → same
GLP> Goodbye!
```

- **`repl-trace-command` DELIVERED**: `ToggleTrace` flips `Session.trace`; `Goal` execution calls `engine.run_with_limit_traced(.., session.trace)` and prepends the reduction-trace lines (commands.gleam:120-149). Missing vs Dart: a separate `:debug` output toggle (Dart b3-c1-033 pairs trace + debug) — Gleam has only `:trace`.
- **`repl-limit-command` DELIVERED**: `SetLimit(n)` threads as the run's `fuel`; a bad value returns the reference usage line; exhaustion surfaces as `OutOfFuel → Failed` ("reduction fuel exhausted"). Full parity with Dart b3-c1-035.
- **`repl-boot-command` ABSENT**: `:boot` is not recognized (lexes as an invalid goal). It drives multiagent boot (Dart `glp_repl.dart:189-201`, test `play_alice_bob.glp`); the multiagent runtime is a wholly-absent subsystem in Gleam (US3/G2), so this is capability-absent, not a REPL-surface omission.
- **`repl-bytecode-command` ABSENT as a command / relocated**: `:bytecode` is not recognized. But the *disassembly capability* exists at the library level (`bytecode/program.gleam` disassembly, frozen register entry `bytecode-isa`). So the capability is present; only the interactive `:bytecode` command is missing.

## Activation & engineer question

`close-repl-repl-boot-command` is **activated** for #3 and #4 — but the risk note (absent-capability vs relocated-capability vs intentionally-narrower-surface) forces an engineer scope decision rather than automatic add:

1. `:bytecode` — the disassembly lib exists; exposing it as a REPL command is small **if** the narrow corpus-runner surface is to grow. Otherwise it is a deliberate omission.
2. `:boot` — cannot be added until the multiagent runtime is ported (US3/G2); its close is **subsumed by the multiagent build**, not a standalone REPL fix.
3. The frozen `repl-surface` register deliberately lists only `load`/goal/`:trace`/`:limit`/`:quit`. If `:boot`/`:bytecode` are intended to stay out-of-surface, the close should be a **rule-request** recording that, not an implementation.

`:trace` and `:limit` are DELIVERED and need no close work (aside from the optional `:debug` toggle).
