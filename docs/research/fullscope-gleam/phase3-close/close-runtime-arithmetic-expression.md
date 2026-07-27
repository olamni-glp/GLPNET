<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T088 close-runtime-arithmetic-expression` (b3-c2-026)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Closes**: `verify-runtime-arithmetic-expression` (b3-c1-001) — 8 detail_ids re-filed with glp_gleam code testimony
**Backing detail_ids**: `arithmetic-expression`, `external-io`, `heap-value-copy-semantics`, `scheduler-fairness`, `stream-mutual-reference`, `suspension-abandonment`, `suspension-diagnostics`, `system-predicate-registry`

## Headline: one genuine code fix; two verify-verdict items corrected to matched-parity

The wave-2 verify verdict listed five activation items. Verifying each against the **actual Dart/C#
reference** (DISCIPLINE §1.5) reclassified two of them: they are not Gleam divergences but behaviours
the Dart reference **shares**, so Gleam is already at parity and "fixing" only Gleam would *introduce* a
divergence. The genuine, delivered code change is the unknown-guard `[WARN]` diagnostic (#3). Gabi's
disposition (2026-07-27): implement #3, record the rest.

## The eight detail_ids — re-filed with glp_gleam evidence

| # | detail_id | disposition | glp_gleam evidence |
|---|---|---|---|
| 1 | `arithmetic-expression` | **DELIVERED (parity)** — full kernel set + `eval_num`; `:=`/guard arithmetic works. The `X := 10-4` "defect" is **matched behaviour**, not a gap (see below). | `src/glp/engine/kernels.gleam` (`_add/_sub/_mul/_div/_idiv/_mod/_neg`, `eval_num`); differential `X := 2+3` → all three `X=5` |
| 2 | `external-io` | **PARTIAL — host-bridge I/O deferred to T061** (recorded scope). `_output/1` delivered. | `src/glp/engine/kernels.gleam:242` (`_output/1`); `_now`/`_send` registration → T061 |
| 3 | `heap-value-copy-semantics` (`_copy/2`) | **DELIVERED** — value-copy IS the immutable threaded heap; every mutating op returns a new `Heap`, role from cell tag only, lookups copy not alias. | `src/glp/runtime/heap.gleam:1-9` + `deref`/`bind_writer`/`unify` all return a fresh `Heap` |
| 4 | `scheduler-fairness` | **PARTIAL — host-event-loop yield N/A in the synchronous standalone engine** (recorded scope decision). FIFO + per-reduction yield + fuel/budget delivered. | `src/glp/engine/scheduler.gleam` FIFO `RunQueue` + `run`'s `fuel`/`reduction_budget` bounds |
| 5 | `stream-mutual-reference` | **DELIVERED** | `src/glp/engine/kernels.gleam:171-238` (`_allocate_mutual_reference/2`, `_stream_append/3`, `_close_mutual_reference/1`, `is_mutual_ref/1`) |
| 6 | `suspension-abandonment` | **matched-stub parity** — the Dart reference is itself unimplemented; no capability to port (see below). | Dart `glp_runtime/lib/runtime/abandon.dart` = `UnimplementedError`; bytecode §10.2:736 — no opcode |
| 7 | `suspension-diagnostics` | **DELIVERED + `[WARN]` sub-gap FIXED** (this WP) | `src/glp/engine/scheduler.gleam` `RunStatus`; NEW `[WARN]` in `runner.gleam` (see #3-fix) |
| 8 | `system-predicate-registry` | **DELIVERED (comparison/type/order) + `[WARN]` FIXED**; host-side I/O predicates → T061 | `src/glp/engine/runner.gleam` `eval_guard` three-valued registry + the new `GUnknown` warning arm |

## The genuine fix — item 3: unknown-guard `[WARN]` (Dart/C# parity)

**Divergence (verified, still real at close time):** `bash test/parity/run_differential.sh
programs/tests/test_time_guard.glp 'test_time(T).'` reported `2 divergent pair(s): dart/gleam
csharp/gleam` — identical outcome (`T = <unbound>, → failed`) but Dart/C# print `[WARN] Unknown guard
predicate: time` (Dart `runner.dart:5284`) and Gleam was silent.

**Fix:** `src/glp/engine/runner.gleam` — `eval_guard`'s default arm now returns a distinct
`GUnknown(name)` verdict; `guard_generic_builtin` appends `[WARN] Unknown guard predicate: <name>` to
`ctx.output` and soft-fails (the Dart default arm's behaviour). Because the diagnostic is emitted on a
**failing** clause, the reduce outcomes `Failed`/`Suspended`/`BudgetExhausted` were given an `output`
field (like `Reduced` already had) so the line survives the non-committing path (`clear_clause`
preserves `output`; `no_more_clauses` carries it into `Failed`/`Suspended`); `scheduler.gleam`'s three
`step` variants append that output to `engine.output`. (stderr was not an option — the harness discards
it via `2>/dev/null`.)

**After:** the same differential now reports **all runtimes AGREE** — Gleam emits `[WARN] Unknown guard
predicate: time` identically.

## The two corrected items (matched-parity, not defects)

**#1 `:=` binary minus.** The verdict called `X := 10-4` a "`:=` RHS operator-table gap." In fact the
Gleam operator table is complete (`is_operator`/`precedence`/`operator_functor` all include `Minus`,
`parse_term = parse_expression(ps,0)`). The real cause is the **lexer** treating `-<digit>` as a
negative-number literal (`lexer.gleam:285`) — and the **Dart lexer does exactly the same**
(`glp_runtime/lib/compiler/lexer.dart:171`). Differential proof: `run_differential.sh - 'X := 10-4.'`
→ **all three runtimes `→ failed` and AGREE**; `X := 2+3` → all three `X=5`. So Gleam is at parity;
accepting `10-4` in Gleam alone would make it the *only* runtime that does. Left as matched behaviour.

**#6 suspension-abandonment.** The verdict said "port the missing capability (Dart `abandon.dart`)."
But `glp_runtime/lib/runtime/abandon.dart` is an `UnimplementedError` stub ("Abandon operation not
implemented in FCP design"), and bytecode §10.2:736 states abandonment is *not* a bytecode instruction
and "No explicit reactivate/abandon bytecode instructions exist or are needed." There is no implemented
reference to port; Gleam matches the reference by absence. Recorded as matched-stub parity (a
beyond-parity FCP-exact implementation would be a new decision, and would need Dart changed too to keep
parity).

## Runnable evidence (fresh-session reproducible)

| Check | Command | Result |
|---|---|---|
| Full Gleam suite (grow-only) + new regression test | `cd glp_gleam && gleam test` | **602 passed, no failures** (was 601; +`unknown_guard_warning_test`) |
| Item 3 parity restored | `bash test/parity/run_differential.sh programs/tests/test_time_guard.glp 'test_time(T).'` | **all runtimes AGREE** (all emit the `[WARN]` line) |
| #1 matched-parity | `bash test/parity/run_differential.sh - 'X := 10-4.'` | **all runtimes `→ failed`, AGREE** |
| #6 Dart stub | `glp_runtime/lib/runtime/abandon.dart` | `throw UnimplementedError(...)` |

Regression test: `glp_gleam/test/glp/engine/unknown_guard_warning_test.gleam` — asserts an unknown
guard predicate fails the goal AND emits the `[WARN]` line into captured output (surviving the failing
path).

## Disposition

**Close status: CLOSED — clean.** All eight detail_ids re-filed with glp_gleam code testimony; the one
genuine divergence (#3 unknown-guard `[WARN]`) is fixed to Dart/C# parity with a regression test;
`external-io`/`scheduler-fairness` host-loop items are recorded scope decisions (I/O → T061); #1 and #6
are corrected to matched-parity with primary-source evidence (a verify-before-confabulate catch).
