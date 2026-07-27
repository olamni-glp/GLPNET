<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-engine-engine-composition-root` (WP b3-c1-015, wave 2)

**Date**: 2026-07-21
**Method**: source-verification of the engine facade (`glp/engine.gleam`) + `gleam test` (8 T034 capture tests, part of the 465-green floor) + scripted Gleam-run probe.
**Paired close**: `close-engine-engine-composition-root` — **activated** for the one PARTIAL row.

## Verdict table

| # | detail_id | verdict | basis |
|---|-----------|---------|-------|
| 1 | `engine-composition-root` | **PARTIAL** | composition-root injection delivered for the **prelude** (`new_with_prelude`, engine a passive value); **kernels compiled into the runner** (referenced, not injected) and **transports not engine-wired** (no injection seam) |
| 2 | `output-capture-seam` | **DELIVERED** | output-as-data flow kernels→runner→scheduler→engine→REPL; 8 T034 capture tests green |
| 3 | `reference-envelope-and-capture-seam` | **DELIVERED** | self-contained ED-1 `ResultEnvelope` + server-side deep-resolve (`result_envelope_builder`); frozen `codec-envelope` |

**Tally**: DELIVERED 2 · PARTIAL 1.

## Evidence

### 1. `engine-composition-root` — PARTIAL
The detail (b3-c1-038) is "kernels **and transports** injected onto a live engine, never referenced by it."
- **Delivered (prelude injection)**: `engine.gleam` `Engine` is an opaque value threaded `new()`→`load()`→`run()`→`step()` with **zero global state** (FR-009), and `new_with_prelude(source)` is an explicit CWD-independent injection seam (engine.gleam:107-126) — the engine holds injected state and references no host. This is the composition-root discipline, honored for the prelude (and it is the frozen `embeddability-api` half).
- **Gap (kernels/transports)**: `rg 'inject|kernel|transport'` over `glp_gleam/src` finds **no kernel/transport injection seam** onto the engine. Native kernels are **compiled into the runner** (`runner.gleam` imports `engine/kernels` and dispatches inline at a BODY Spawn) — i.e. **referenced**, the opposite of the "never referenced by it" discipline. Transports (`glp/link/*`) exist as standalone modules but are **not wired to the engine** at all. So a host cannot inject its own kernels/transports onto the standalone engine today.
- **Disposition**: this is the wave-4 embeddability requirement — `build-yngenios-embeddability` / `build-fe-be-process-split` must add the host-side kernel/transport injection seam so the four spec-056 services drive the engine through their own composition root. Recorded here, routed to `close-engine-engine-composition-root`.

### 2. `output-capture-seam` — DELIVERED
Output-as-data flow: `kernels.gleam:242` `_output/1` → `KSuccess(heap, [], [line])` → `runner.Reduced(output:)` → `scheduler.captured_output` → `engine.run_with_limit_capturing` returns the lines → the REPL prints them **ahead of** the outcome block (`commands.gleam:143-149`). `glp_gleam/test/glp/engine/output_capture_test.gleam` has **8** `_test` functions (the recorded T034 set), all green in the 465 floor.
- **Probe finding**: `_output/1` is a **body kernel**, not a goal predicate — a bare goal `_output(hello).` correctly returns `→ failed / predicate _output/1 not found` (kernels dispatch at a BODY Spawn, never via `label_pc`; matches Dart `bodyKernels`). Capture is therefore exercised from a clause body, which the 8 unit tests do.
- Note: the envelope's `captured` **field** stays `<<>>` by the frozen owner-approved deferral (R4, engine.gleam:177-178) — this is distinct from the output-capture **seam** (the data flow), which is delivered.

### 3. `reference-envelope-and-capture-seam` — DELIVERED
The self-contained ED-1 result envelope with server-side deep-resolve is `glp/codec/result_envelope` + `result_envelope_builder` (imported by the facade as `builder`, engine.gleam:32; `finish_run` calls `builder.build_result_envelope` which deep-resolves the query vars, engine.gleam:263-285). This is the shipped 038 work, already pinned by the frozen `codec-envelope` register entry. DELIVERED.

## Activation

`close-engine-engine-composition-root` is **activated** for #1 only: add the host-side **kernel/transport injection seam** onto the engine value (the composition-root extension the four spec-056 services need), without disturbing the delivered prelude-injection seam. #2 and #3 are DELIVERED and need no close work.
