<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-module-system-scope-chain` (b2-c2-021)

**Feature**: 059 · **Wave**: 2 (verify) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27 · **Backing detail_ids**: `module-system`

## Reference capability under test

The Dart reference `glp_runtime/lib/runtime/module_hierarchy.dart` (present in-tree) delivers a
**directory-based `self.glp` scope chain**: ancestor scoping across nested `self.glp` files, name
shadowing (nearer wins), and sibling-directory isolation.

## Runnable evidence (fresh-session reproducible)

| Check | Command | Result |
|---|---|---|
| Gleam module-system source | `find glp_gleam/src -iname '*module*'` | **none** |
| Scope-chain / hierarchy sweep | `rg -i 'self\.glp|scope.?chain|module_hierarchy|ancestor|sibling|shadow' glp_gleam/src` | only **single-root-prelude** handling + unrelated hits |

**Interpretation of the hits:** every `self.glp` reference in Gleam is the **one root prelude**
(`programs/self.glp`), read once by the engine facade (`engine.gleam:45,99`) and threaded into the
loader (`compiler/loader.gleam`). The `ancestor` matches are (a) **type**-scope for parameterized
templates (`analysis/type_ast.gleam:221,281`) and (b) **heap-cycle** traversal
(`codec/result_envelope_builder.gleam`) — neither is the directory module scope chain. There is no
per-directory ancestor resolution, no shadowing, no sibling isolation, and no `#` cross-module
dispatch table.

## Verdict

| detail_id | verdict | basis |
|---|---|---|
| `module-system` | **ABSENT** (directory scope chain) / **PARTIAL** at the system level | Single root prelude is delivered; the reference nested-`self.glp` ancestor/shadow/sibling scope chain is not implemented in Gleam. |

## Decision this surfaces (per WP risk — routed, not buried)

The reference `self.glp` scope-chain parity is **absent**. A scope ruling is required: does it join
**`close-module-system-runtime-rpc`** (build the directory scope chain to Dart parity), or is it filed
as a **deferral rule-request** (single-root-prelude accepted for the Gleam instance, multi-directory
scoping out of scope)? This verify records the gap and the decision; it does not resolve scope.
