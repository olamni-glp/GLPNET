<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T068 close-embeddability-host-api` (b2-c2-013)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-28
**Closes**: `embeddability-api` — the dedicated host-embedding API over the pure engine value.
**Backing detail_ids**: `embeddability-api`

## Acceptance (FINAL plan, line 483)

> `gleam test` in glp_gleam passes a host-harness test that embeds and drives the engine exclusively
> through the new API (no repl module imports), and the surface is documented in
> specs/050-full-gleam-combined/contracts/gleam-instance-surface.md.

Both met: `gleam test` **629 passed, 0 failures**; the host-harness test imports NO `glp/repl/*` module;
the surface is documented in the contract (new "Host-embedding API" section).

## What was delivered

The `load/run/step/Event` surface over the opaque `Engine` value already existed (T029). This close adds
the two missing halves of the Dart `glp_engine.dart` embeddability baseline — **`configure`** and the
**prelude-injection seam** wired to host kernels — and proves the whole surface drives an embedded engine
with no UI/REPL coupling.

| Piece | Change (`glp/engine.gleam`) |
|---|---|
| **`EngineConfig`** | NEW record: `reduction_budget` (per-goal budget), `fuel` (total-reduction backstop; Dart `maxCycles`), `trace` (reduction-trace default), `host_kernels`. `default_config()` = the historical defaults (1M / 1M / off / none). |
| **`configure` / `config`** | `configure(engine, cfg)` replaces the config on the pure value (no global state, FR-009); `config(engine)` reads it. The config-driven `run` honours `fuel` + `trace`; `run_with_limit*` keep an ad-hoc fuel for the REPL `:limit`. Every run path threads `config.reduction_budget` + `config.host_kernels` into the scheduler. |
| **`register_kernel`** | Adds a host body kernel to the config (the T069 composition-root seam; see that evidence). |
| **`HostKernel` re-export** | `pub type HostKernel = runner.HostKernel`, so a host wires kernels through `glp/engine` alone. |
| **Prelude-injection seam** | `new_with_prelude(source)` (pre-existing) is the host prelude hook; because `compile_prelude` elides the user-style type check, an injected prelude may call host kernels (like self.glp's `'_now'`/`'_add'`). |

### Host-harness test — `glp/engine/engine_host_api_test.gleam`

`host_harness_embeds_and_drives_engine_test` builds an engine over a host prelude, `configure`s it (a
custom `fuel`) + `register_kernel`s `_host_double/2`, asserts `config(eng).fuel` round-trips, then
`run`s `double(7, R)` → `R = 14` — driven ENTIRELY through `glp/engine`. `plain_goal_drives_through_the_api_test`
drives an ordinary `X := 2 + 3` the same way. The file's imports are `glp/engine`, `glp/engine/runner`
(the `HostKernel` value type), `glp/runtime/{heap,terms}`, and the codec/envelope for assertions — **no
`glp/repl/*`**, satisfying the decoupled-from-UI requirement.

## Documentation

`specs/050-full-gleam-combined/contracts/gleam-instance-surface.md` gains a "Host-embedding API
(T068 · T069)" section enumerating construct/prelude-injection, configure, and kernel-injection.

## Discipline

Gleam-side only (`engine.gleam` + shared runner/scheduler seam from T069 + one NEW test) + the contract
doc. No `self.glp`, no language change. yngenios-side wiring stays a wave-4 concern
(`build-yngenios-embeddability`); the in-repo acceptance proves the host API + harness. `gleam test` 629/0.
