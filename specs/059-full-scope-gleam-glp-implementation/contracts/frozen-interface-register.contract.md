<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract — Frozen-Interface Register (wave 1)

**Governs**: spec FR-002, FR-003; SC-001; US1. Live register:
`docs/research/fullscope-gleam/frozen-interface-register.md`.

## What "frozen" means

An interface listed in the register is a **delivered, pinned** contract of the Gleam instance
(runtime terms/heap/unification, compiler pipeline, engine execution, bytecode runner, REPL surface,
result/IL codecs, link wire formats, link transport seam, body/guard kernels, module system,
AtomVM policy). While frozen:

1. Its observable behavior MUST NOT change.
2. Its **protected test files** MUST NOT be modified or removed, and the suite MUST NOT shrink.
3. Any WP that needs to change it MUST file an **unfreeze rule-request** and wait for an engineer
   ruling before touching it. Silent drift is a **guard failure** that fails the feature checkpoint.

## The three pinned suites (grow-only tripwires)

| Suite | Baseline | Command | Rule |
|---|---|---|---|
| Gleam gleeunit | 463-test freeze baseline (now **508** green) | `cd glp_gleam && gleam test` | grow-only; never red |
| Dart unified REPL | reference oracle | `DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart.exe bash test/run_all_tests.sh` | grow-only; never red |
| C# reference | glp_link / glp_quick_host / result-codec / crdtmsg xUnit | per-project `dotnet test` | grow-only; never red |

The **AtomVM gated probe** retains its recorded manual procedure (`guard-atomvm-gated-probe`) — not in
the automatic tripwire set.

## Unfreeze protocol

```text
WP needs a frozen-interface change
  → file rule-request (append to the escalation register, status=open, due_before=this WP)
  → BLOCK the WP until an engineer ruling is recorded
  → on ruling: update the register entry, then proceed under the ruling
  → NEVER change the interface or shrink a protected suite ahead of the ruling
```

## Checkpoint gate

At every feature checkpoint: the register enumerates every frozen interface with its protected test
list, and all three pinned suites pass with their protected files unmodified against the freeze
baseline. A shrunk suite, a modified protected file, or a red pinned suite **fails the checkpoint**.
