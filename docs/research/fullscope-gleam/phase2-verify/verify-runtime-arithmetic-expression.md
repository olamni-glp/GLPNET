<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-runtime-arithmetic-expression` (WP b3-c1-001, wave 2)

**Date**: 2026-07-21
**Method**: source-verification + Gleam-side execution + 3-runtime differential harness (engineer-approved wave-2 depth: source + Gleam runs, harness where cheap).
**Paired close (all 8 detail_ids)**: `close-runtime-arithmetic-expression` — every non-DELIVERED row below activates it.

## Environment / commands run

- `gleam test` in `glp_gleam` (Windows OTP-29 clean build) → **465 passed, no failures** (the pinned floor; note the shared `build/` needs `gleam clean` when Windows/WSL toolchains alternate — the `gleam_stdlib:percent_encode/2` OTP-load error).
- `rg -n 'abandon|fairness|external_io|system_predicate|mutual|drain' glp_gleam/src` (WP's prescribed sweep).
- `test/parity/run_differential.sh <prog> <goal>` for the three named corpus programs — **all three runtimes ran** (Dart + C# + Gleam; the C# REPL is built at `out/csharp/glp_repl/...`).

## Verdict table

| # | detail_id | verdict | basis |
|---|-----------|---------|-------|
| 1 | `arithmetic-expression` | **DELIVERED** (runtime) — parser defect flagged | full kernel set + recursive Exp eval; `-` works in guards; **`:=` RHS parser rejects binary `-`** |
| 2 | `external-io` | **PARTIAL** | `_output/1` registered; `_now`/`_send` deliberately unregistered in the standalone engine |
| 3 | `heap-value-copy-semantics` | **DELIVERED** | immutable threaded heap — every mutating op returns a new `Heap`; role from cell tag only |
| 4 | `scheduler-fairness` | **PARTIAL** | FIFO RunQueue + per-reduction yield + fuel/budget bounds delivered; host-event-loop yield N/A in a synchronous standalone engine |
| 5 | `stream-mutual-reference` | **DELIVERED** | allocate/append/close kernels present; harness parity confirmed |
| 6 | `suspension-abandonment` | **ABSENT** | no explicit abandonment op anywhere in `glp_gleam/src`; only automatic reactivation |
| 7 | `suspension-diagnostics` | **DELIVERED** — diagnostic sub-gap | three-valued `RunStatus` + blocking-reader table; **missing the `[WARN] Unknown guard predicate` line** |
| 8 | `system-predicate-registry` | **PARTIAL** | three-valued comparison/type/order guard registry + suspend-on-unbound delivered; I/O predicates (`time`/`now`, `send`) + unknown-predicate warning absent |

**Tally**: DELIVERED 4 · PARTIAL 3 · ABSENT 1.

## Per-capability evidence

### 1. `arithmetic-expression` — DELIVERED (runtime); `:=` parser defect
Full native kernel set in `glp_gleam/src/glp/engine/kernels.gleam`: `_add/_sub/_mul/_div/_idiv/_mod/_neg` (+ `_pow`, unary math, type conversions, univ). `eval_num` (kernels.gleam:552) recursively evaluates `ConstTerm`/`VarRef`/`StructTerm` Exp trees. `self.glp` decomposes `:=`/2 to these.
- **Runnable**: `expr_eq(5,7,R)` (uses `X?+1 =:= Y?-1`) → Dart/C#/Gleam **all `R = equal, → succeeds`** — binary `-` and `+` both evaluate correctly **in guard arithmetic**.
- **Defect (routed to close, NOT fixed here)**: `X := 10-4` and `X := 3-9` → `ParseError("Expected \".\" at end of clause")` at the `-` column, whereas `X := 2+3`→5 and `X := 6*7`→42 succeed. So the `:=` RHS-expression parser handles `+`/`*` but **not binary `-`** — a parser operator-table gap, not a runtime one (the `_sub` kernel and guard `-` both work). This is the concrete arithmetic work item for `close-runtime-arithmetic-expression`.

### 2. `external-io` — PARTIAL
`_output/1` is registered and threads captured output as data (kernels.gleam:242, T034). `_now`/`_send` are **intentionally unregistered** in the standalone engine (kernels.gleam:24-27: "need wall-clock/IO the standalone engine does not have"); a BODY spawn to them surfaces a loud runner error. Host-bridge I/O is the gap.

### 3. `heap-value-copy-semantics` — DELIVERED
`glp_gleam/src/glp/runtime/heap.gleam:1-9`: "the WAM mutable heap is re-expressed as an IMMUTABLE value threaded through deref/bind/unify; every mutating op returns a new `Heap`." Cell role is derived from the tag only, never address arithmetic. Matches inventory b1-c1-013 (value-copy, lookups copy not alias).

### 4. `scheduler-fairness` — PARTIAL
`scheduler.gleam`: FIFO `RunQueue` (enqueue-tail/dequeue-head), each reduced goal drops and its spawns/wakes re-enqueue at the tail (per-reduction yield), and `run` is bounded by `fuel` (total reductions) + `reduction_budget` (instructions/reduction). FIFO fairness + bounded budget = delivered. The inventory's "yields to the **host event loop** to avoid starving I/O/timers" (b3-c1-009) is **N/A in the synchronous standalone engine** (no host loop) — flagged for the close so it is a recorded scope decision, not a silent pass.

### 5. `stream-mutual-reference` — DELIVERED
`_allocate_mutual_reference/2`, `_stream_append/3`, `_close_mutual_reference/1`, `is_mutual_ref/1` all present (kernels.gleam:171-238), immutable `$mutual_ref(addr)` sentinel walking the cons chain to the open tail.
- **Runnable**: `build_stream(Xs)` on `test_mutual_ref.glp` → Dart/C#/Gleam **all `→ failed` identically** (harness AGREE). The probe fails uniformly on every runtime (source-level predicate naming, not a Gleam divergence); Gleam is at parity.

### 6. `suspension-abandonment` — ABSENT
`rg 'abandon' glp_gleam/src` → **no matches**. The Gleam scheduler has automatic reactivation (`heap.suspend_on_writer` armed → `heap.bind_writer` wakes) but **no explicit abandonment operation**, whereas the inventory (b3-c1-008) defines "explicit suspension abandonment ... a defined runtime operation" (Dart `abandon.dart`, bytecode §10.2). No corpus program exercises abandonment (inventory: "no in-slice test program identified") — so the close WP must **add a reproducer** as well as the operation. ABSENT is by source-absence; recorded here rather than confirmed by a failing program because none exists yet.

### 7. `suspension-diagnostics` — DELIVERED; diagnostic sub-gap
`scheduler.gleam` `RunStatus` = `Success | Suspended(blocking_readers) | Failed | OutOfFuel | Errored` — three-valued status + a `blocking` table (reader addr → suspended goal ids, a mirror of Dart `rt.suspended`) feeding the envelope's `blocking_readers`. Matches inventory b3-c1-010.
- **Sub-gap (runnable)**: `test_time(T)` → **2 divergent pairs** (`dart/gleam`, `csharp/gleam`): Dart and C# emit `[WARN] Unknown guard predicate: time`; **Gleam omits the warning** (same `T = <unbound>, → failed` outcome). The unknown-predicate diagnostic is missing in Gleam.

### 8. `system-predicate-registry` — PARTIAL
`runner.gleam` implements a three-valued guard registry: `eval_guard` (runner.gleam:2370) for the comparison ops (`=:= =\= < > =< >=`), standard-order comparators, ground/known/unknown/no_readers, and `defined_guard_table` for spec-defined guards, with `guard_suspend` on unbound readers (runner.gleam:2150). The **comparison/type/order** half is delivered. **Absent**: the host-side I/O system predicates (`time`/`now`, `send`) and the unknown-predicate warning (see #7). Matches inventory b3-c1-015 partial delivery.

## Activation

`close-runtime-arithmetic-expression` is **activated** with these concrete work items (from the PARTIAL/ABSENT/defect rows):
1. `:=` RHS-expression parser: accept binary `-` (operator-table gap) — #1.
2. Host-bridge I/O: register `_now`/`_send` + the `time`/`now`/`send` system predicates — #2, #8.
3. Explicit suspension-abandonment operation **and** a corpus reproducer — #6.
4. `[WARN] Unknown guard predicate` diagnostic to match Dart/C# — #7, #8.
5. Recorded scope decision on host-event-loop yield (N/A in standalone) — #4.

DELIVERED rows (#3 heap-value-copy, #5 stream-mutual-reference, and the runtime evaluation of #1, #7 status core) need no close work beyond the sub-gaps noted.
