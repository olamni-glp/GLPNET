<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Spec amendment note — module RPC runtime semantics (T078 prerequisite)

**Feature**: 059 · **Wave**: 3 · **Date**: 2026-07-27 · **WP**: `T078 close-module-system-runtime-rpc`

## Why this note exists (Spec-First, DISCIPLINE §1.1/§1.10)

The 050 spec is silent on module runtime execution (`promise = absent`), so the T078 close lacks
inline spec backing. Per the WP risk note and CLAUDE.md's Spec-First rule, the governing semantics are
quoted here **before** any runner code is written. The authority is `docs/typed-glp-manual.md §19`
(Modules) — the single source of truth for module semantics.

## Governing spec (verbatim, `typed-glp-manual.md §19`)

§19.4 Cross-Module Calls:
> To call an exported procedure from another module, use the `#` operator:
> `test_double(X, Y?) :- math_service # double(X?, Y).`
> This sends the goal `double(X?, Y)` to the `math_service` module for execution. The runtime routes
> the goal through a GLP channel to the target module's service loop, which dispatches it to the
> exported procedure.

§19.7 Compilation Modes — Dynamic dispatch:
> **Dynamic dispatch** (for independently loaded modules): Each module is loaded and activated
> separately. Cross-module calls route through GLP channels at runtime.

§19.8:
> Modules with `exported procedure` declarations are automatically activated when loaded — a GLP
> channel is created, and a service loop is spawned to dispatch incoming goals.

## What T078 must implement (runtime obligation)

The compiler already emits the module surface; the runner faults `Unimplemented` on two opcodes
(`opcodes.gleam` `Distribute(import_index, functor, arity)` / `Transmit(module_var_index, functor,
arity)`) that reach the `runner.gleam` catch-all (`_ -> Stop(RunnerError(Unimplemented(...)))`). T078
adds runner arms that execute the §19.4/§19.7 semantics: resolve the target module (by import index /
module var), route the goal to the target module's activated service loop over a GLP channel, so a
module-qualified `M # goal(...)` call **runs to completion instead of faulting Unimplemented**.

Faithful to the delivered link/mad channel machinery: the routing is a GLP-channel send to the
service loop (the same shape as the delivered `_send`/link kernels).

**§1.14 ruling (Gabi, 2026-07-27) — KERNEL ADD APPROVED.** An earlier draft of this note claimed "no
new kernel/guard/directive is introduced." That was **inaccurate about the mechanism**: the faithful
Dart-parity port routes through the `_activate/2` **body kernel** (Dart `body_kernels.dart:820-881`
`activateKernel`) driven by the `serve/2` **system procedure** (Dart `glp_engine.dart:71-82`) — neither
exists in Gleam yet. Gabi, as Language Authority (§1.14), **expressly approved adding `_activate/2` +
`serve/2` to Gleam** as a parity port (system-internal, underscore-prefixed, `-mode(system)`; they are
existing language surface in the Dart reference, not a new invention). Routing model = the faithful
channel/`serve`/`_activate` dynamic dispatch (NOT the direct-spawn static-link shortcut). This resolves
the spec-vs-mechanism inconsistency before any runner code is written.

## Scope boundary — the directory `self.glp` scope chain

`verify-module-system-scope-chain` (b2-c2-021) reports the ancestor/nearer-wins/sibling-isolation
directory scope chain (Dart `module_hierarchy.dart`) ABSENT (single root prelude only). Per the
engineer directive **full build-to-parity (no deferrals)**, the scope chain is **in scope for T078**
(built to Dart parity), not filed as a deferral rule-request. §19.6 is its authority:
> Each directory may contain a `self.glp` file that defines types and procedures visible to all
> modules in that directory and its subdirectories. … Every module sees root self.glp automatically.

## Acceptance (per FINAL plan)

A new end-to-end module-qualified remote-call test (`glp_gleam/test/glp/engine/module_rpc_test.gleam`)
executes a `Distribute`/`Transmit` program to completion with no `Unimplemented` fault, plus a
directory-scope-chain test proving nearer-`self.glp`-wins shadowing + sibling isolation to Dart parity.
`gleam test` stays grow-only green.
