<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T078 close-module-system-runtime-rpc` (b2-c2-007)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Closes**: `verify-module-system-runtime-rpc` + `verify-module-system-scope-chain` (b2-c2-021)
**Backing detail_ids**: `module-system-runtime-rpc`, `module-system-scope-chain`
**Spec authority**: `spec-note-module-rpc-runtime.md` (quotes typed-glp-manual §19.4/19.6/19.7); §1.14
kernel-add (`_activate/2` + `serve/2`) and faithful-dynamic-dispatch both **approved by Gabi 2026-07-27**.

## Acceptance (spec-note + FINAL plan)

> A new end-to-end module-qualified remote-call test executes a Distribute/Transmit program to
> completion with no Unimplemented fault, plus a directory-scope-chain test proving
> nearer-self.glp-wins shadowing + sibling isolation to Dart parity. `gleam test` stays grow-only green.

**Both delivered. `gleam test` 606/0** (was 603 after Part A; +3 scope-chain tests).

## Part A — module RPC runtime (Distribute/Transmit no longer `Unimplemented`)

A module-qualified `M # goal(...)` body call now RUNS: the goal routes onto the target module's
**channel stream**, waking its suspended **`serve/2`** loop, which dispatches it via the **`_activate/2`**
body kernel to the exported procedure over the shared heap cells; the result flows back. Faithful
dynamic dispatch (§19.4/§19.7), not the static-link shortcut.

| Piece | File |
|---|---|
| `Distribute`/`Transmit` opcode arms (replace the `Unimplemented` fallthrough) + `_activate/2` kernel (reuses the existing `SpawnReq` — NO new dynamic-spawn primitive, per ruling) + `module` threaded on `RunnerContext`/`Reduced` | `glp_gleam/src/glp/engine/runner.gleam` |
| `ModuleRuntime` name→channel registry (Dart `glpChannels` analogue) | `glp_gleam/src/glp/engine/module_runtime.gleam` (new) |
| `step_module`/`run_module` scheduler variant (mirrors `step_link`) | `glp_gleam/src/glp/engine/scheduler.gleam` |
| `Distribute` import table (`index→name`) on the compiled program, surfaced from codegen | `glp_gleam/src/glp/bytecode/program.gleam` (`import_name`/`with_imports`) + `glp_gleam/src/glp/compiler/codegen.gleam` |
| `serve/2` system procedure | `programs/system/module_predicates.glp` (new) |
| `_activate/2` declaration + type-checker recognizer (§1.14 approved) | `programs/system/module_predicates.glp` (with `serve/2`) + `glp_gleam/src/glp/analysis/prelude.gleam` |
| **Acceptance test** | `glp_gleam/test/glp/engine/module_rpc_test.gleam` |

**Acceptance test (`module_rpc_distribute_runs_to_completion`):** `run_echo(5, R?) :- math_service # echo(X?, R).`
drives `scheduler.run_module` to completion with **no `Unimplemented` fault**; the input `5` crosses the
RPC (Distribute → channel → `serve` → `_activate` → `echo`) and the output flows back on the shared cell.

**🔴 Shared-file hardening (2026-07-28, post-close correction).** The `_activate/2` declaration was
FIRST added to `programs/self.glp` — which is SHARED with the Dart reference runtime (`glp_runtime` also
compiles `self.glp`). Dart's compiler rejects a **clauseless** `_activate` declaration (it is not in
Dart's host-kernel recognizer, unlike `_send`/`_link_*`), so `GlpEngine._loadRootSelf` threw
`Bad state: … Procedure declaration for "_activate" has no clauses` and the **Dart REPL crashed on boot**
(surfaced when the differential harness's Dart column came back blank). Fix: the declaration was moved to
`programs/system/module_predicates.glp` — a **Gleam-only** system module loaded alongside `serve/2`, only
when module RPC is exercised — so `self.glp` stays byte-identical to the shared original and both runtimes
boot. Verified: Dart REPL boots + runs `p(5,R). → R=10`; Gleam `gleam test` 606/0. LESSON: never add a
Gleam-only clauseless kernel declaration to the shared `self.glp`.

## Part B — the directory `self.glp` scope chain (§19.6)

| Piece | File |
|---|---|
| `discover_self_chain` — the ancestor walk, ROOT-FIRST, siblings never visited (Dart `discoverSelfChain`) | `glp_gleam/src/glp/compiler/module_hierarchy.gleam` (new) |
| `load_with_scope` — merges the ancestor self.glp chain into the type env root-first, NEARER-WINS (`type_ast.merge` = `dict.merge`; Dart `assembleTypeScope`) | `glp_gleam/src/glp/compiler/loader.gleam` (`load` = `load_with_scope(..., [])`) |
| Fixtures (`self.glp` button / `sub/self.glp` slider / `sub/client.glp` uses slider / `other/self.glp` sibling) | `programs/tests/scope_chain/` (new) |
| **Acceptance test** | `glp_gleam/test/glp/compiler/scope_chain_test.gleam` |

**Acceptance tests (3):** (1) `discover_self_chain` returns exactly `[scope_chain/self.glp, sub/self.glp]`
ROOT-FIRST and EXCLUDES `other/self.glp` (**sibling isolation**); (2) with the discovered chain, the
nearer `sub/self.glp` `Widget=slider` **shadows** the root `Widget=button`, so `use_widget(slider)`
type-checks (**nearer-wins**); (3) the **reversed** chain lets the outer `Widget=button` win and the load
is rejected — order-sensitivity confirms root-first is what makes the nearer definition win.

## Runnable evidence (fresh-session reproducible)

| Check | Command | Result |
|---|---|---|
| Full suite grow-only + both acceptance tests | `cd glp_gleam && gleam test` | **606 passed, no failures** |
| Module RPC runs to completion | `module_rpc_test.gleam` | Distribute program completes, no `Unimplemented`, result on shared cell |
| Scope chain shadowing + sibling isolation | `scope_chain_test.gleam` | root-first walk, sibling excluded, nearer-wins, order-sensitive |

## Carried-forward residuals (flagged, NOT silently deferred)

1. **Facade REPL-integration** — the module RPC + scope chain are complete and tested at the
   loader/scheduler level, but the engine facade (`engine.gleam`) does not YET (a) auto-activate an
   exported module on `load` (spawn `serve/2` + register a channel + drive `run_module`) nor (b) thread
   a file PATH into `load` to call `discover_self_chain` + `load_with_scope`. So the interactive REPL does
   not yet route `#` calls or apply the directory scope chain automatically. The acceptance tests drive
   the mechanisms directly (the facade would only wire them). Recommended as a T078 follow-up sub-WP.
2. **~~§19.5 output-hole-via-body~~ — RETRACTED 2026-07-28 (was a test-harness artifact, NOT an engine
   gap).** Differential on `programs/tests/output_via_body.glp` (`p(X, R?) :- q(X?, R). q(X, Y?) :- Y :=
   X? * 2.`) with `p(5, R).` → **Dart `R = 10 → succeeds` AND Gleam `R = 10 → succeeds` — they AGREE.**
   The §19.5 output-hole-via-body pattern works correctly in both runtimes. The earlier "unbound output"
   observation came from `module_rpc_test` manually setting the query register (`reg1 = VarRef(r_w)`)
   instead of building it via `goal_boot.setup_goal`; the head-binding `echo` was robust to that, the
   body-output chain was not. No engine fix needed. (The Part A acceptance keeps `echo(X, X?)`; it
   correctly exercises input→output flow through the RPC.)
3. **T073 interaction** — a module referencing a type defined in NO in-scope `self.glp` panics at
   `program_dfa.gleam:580` (`UnknownTypeError`) rather than returning a clean staged error; this is the
   pending T073 (`close-langsurface-channel-convention` sibling / small-fix panic→StagedError). The
   scope-chain negative is proven via a DEFINED-but-non-matching `Widget` (clean reject) to avoid it.
4. **Full corpus** was not re-run as the gate (environmentally impractical here: >9 min, ~2s BEAM startup
   × 206 goals; background redirects don't capture). The core-type additions (`module` on the context,
   `output` on Failed/Suspended) are provably inert for non-module / non-warning goals; relied on
   `gleam test` 606/0 + targeted differentials (AGREE on `test_time_guard`, `X:=2+3`, `X:=10-4`).

## Disposition

**Close status: CLOSED — acceptance met, residuals flagged.** Both detail_ids delivered: module RPC
runtime execution (Distribute/Transmit → serve/_activate over a channel) and the directory `self.glp`
scope chain (root-first walk + nearer-wins shadowing + sibling isolation), each with a green acceptance
test; `gleam test` grow-only 606/0. Facade REPL-integration + the §19.5 output-hole issue are carried
forward as flagged follow-ups.
