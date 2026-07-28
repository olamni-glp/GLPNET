<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T069 close-engine-engine-composition-root` (b3-c1-040)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-28
**Closes**: `engine-composition-root` (the PARTIAL row of verify-engine-engine-composition-root) +
confirms `output-capture-seam` + `reference-envelope-and-capture-seam` (both DELIVERED).
**Backing detail_ids**: `engine-composition-root`, `output-capture-seam`,
`reference-envelope-and-capture-seam`

## Acceptance (FINAL plan, line 491)

> The T034 capture tests plus an injection test (kernel registered from the host, never referenced by
> the engine) green under gleam test.

Green under `gleam test` (**629 passed, 0 failures**; baseline 624 + 5 host-API tests).

## What was delivered

The verify verdict left ONE row PARTIAL: composition-root injection existed for the **prelude**
(`new_with_prelude`) but **kernels were compiled into the runner (referenced, not injected)** — a host
could not inject its own kernel onto the engine value. This close adds the **host-side kernel-injection
seam** onto the engine value, threaded but never named.

| Layer | Change |
|---|---|
| **Runner** `glp/engine/runner.gleam` | NEW `HostKernel` = `fn(Heap, List(Term)) -> Result(HostKernelOutcome, String)` + `HostKernelOutcome(heap, woken, output)` (the same three data channels a built-in `KSuccess` threads out). NEW `host_kernels: Dict(#(String,Int), HostKernel)` field on `RunnerContext` (read-only config; NOT threaded out on `Reduced`) + `with_host_kernels`. At a BODY Spawn label-miss `effectful_spawn` consults the table by `(name, arity)` **after** the built-in pure/`_activate`/link/mad seams (a host *extends*, never shadows), dispatching via NEW `host_spawn`; a host abort surfaces as a fatal `Malformed` (the built-in `KAbort` discipline). The runner **never names any host kernel** — it only looks one up (the "injected, never referenced by it" discipline). |
| **Scheduler** `glp/engine/scheduler.gleam` | NEW `host_kernels` field on `Engine` + `with_host_kernels`; applied via `runner.with_host_kernels` at ALL four reduction-context sites (`step` / `step_link` / `step_module` / `step_mad`), so an injected kernel is reachable under any driver mode. |
| **Facade** `glp/engine.gleam` | `register_kernel(engine, name, arity, kernel)` records the kernel in the config; every run threads `engine.config.host_kernels` into the scheduler. |

### Injection test (the acceptance) — `glp/engine/engine_host_api_test.gleam`

- `injected_host_kernel_runs_over_engine_test` — a host registers `_host_double/2` (defined in the TEST,
  never in the engine), injects a wrapper `double(X, Y?) :- '_host_double'(X?, Y).` through the prelude
  (mirroring self.glp's own `now(T?) :- '_now'(T).`), and `run(eng, "double(21, R)")` binds `R = 42`.
- `bare_engine_does_not_know_the_injected_kernel_test` — the SAME prelude on a bare engine (no
  `register_kernel`) → the `_host_double` label misses every built-in seam → non-fatal `Failed`. This is
  the "never referenced by the engine" half proven: the engine knows the kernel only via injection.
- `aborting_host_kernel_surfaces_loudly_test` — a host kernel returning `Error` → a `Failed` run (never a
  silent success).

### Confirmed DELIVERED (no new work)

- `output-capture-seam` — the 8 T034 `output_capture_test` tests stay green in the 629 floor.
- `reference-envelope-and-capture-seam` — `result_envelope` + `result_envelope_builder` deep-resolve,
  unchanged; every host-API run returns a `ResultEnvelope`.

## Scope note — pipeline recognition of host kernels

A host kernel is callable from the injected **prelude** because the prelude is compiled WITHOUT a
user-style type check (`compile_prelude`), exactly as `self.glp` calls `'_now'`/`'_add'`. Making an
arbitrary host kernel callable from a **type-checked user `load`** would require the parser +
type-checker's `is_builtin_procedure` recognition to become instance-configurable — a load-pipeline /
type-surface change deferred to the wave-4 `build-yngenios-embeddability` build (verify verdict #1
routed the full kernel/transport injection there). The wave-3 close delivers the runtime injection seam
on the engine value + the prelude-injection path, which the acceptance exercises end to end.

## Discipline

Gleam-side only — `runner.gleam` / `scheduler.gleam` / `engine.gleam` + one NEW test. No `self.glp`,
no new built-in kernel/guard/directive, no type-system change (the seam is a generic table lookup; the
host supplies the kernel, the language surface is unchanged — §1.14 not engaged). `gleam test` 629/0.
