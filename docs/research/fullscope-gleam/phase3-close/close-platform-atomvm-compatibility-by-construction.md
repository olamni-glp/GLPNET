<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T081 close-platform-atomvm-compatibility-by-construction` (b3-c1-043)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Closes**: `verify-platform-atomvm-compatibility-by-construction` (b3-c1-018) — DELIVERED by-construction
**Backing detail_ids**: `atomvm-compatibility-by-construction`, `build-test-topology-windows`, `langpair-dart-gleam`, `monitor-primitive-verification`, `port-source-basis-dart`, `subtree-scaffold`

## What was verified DELIVERED (by-construction)

AtomVM **execution** is excluded from 050 acceptance (per the WP risk); the verdict rests on
**by-construction** constraints only. The no-OTP policy is machine-enforced:
`glp_gleam/test/glp/deps_policy_test.gleam` (green in the build/test gate) asserts the dependency
shape, and `glp_gleam/gleam.toml` declares `gleam_stdlib` + `gleam_erlang` (Subjects) **only** — no
`gleam_otp` — with the comment "The Gleam OTP actor package is intentionally NOT a dependency: its
`proc_lib` use is outside AtomVM's BEAM/OTP subset (F1 dossier §3), so the downstream port stays
AtomVM-runnable by spawning the raw erlang way + gleam_erlang Subjects." The 033 subtree scaffold is
filled — 13 subsystems under `src/glp` (analysis/bytecode/codec/compiler/engine/link/mad/multiagent/
parser/repl/runtime/lint/diagnostics), no empty-placeholder stubs remaining on the runtime path
(`glp/lint.gleam` is a documented intentional placeholder, not a scaffold hole).

## Runnable evidence (fresh-session reproducible)

| Check | Command | Result |
|---|---|---|
| No-OTP-abstraction guard | `find glp_gleam/test -iname '*deps_policy*'` → in the `gleam test` suite | `deps_policy_test.gleam` present, **green** (part of 508/0) |
| Dependency policy | `rg 'gleam_otp\|gleam_erlang' glp_gleam/gleam.toml` | `gleam_erlang` only; **no `gleam_otp`** |
| Windows-native build+test topology | `gleam build && gleam test` (Windows-native) | **green, 508/0**; WSL reserved for AtomVM/quicer |
| Subtree scaffold filled | `ls glp_gleam/src/glp` | 13 subsystems, no empty placeholders on the runtime path |
| Decision records present | `ls specs/{039,031,032}/spec.md` | all present (monitor / port-basis / langpair) |

## Disposition (per detail_id)

| detail_id | disposition |
|---|---|
| `atomvm-compatibility-by-construction` | **CONFIRMED-DELIVERED** — `deps_policy_test` enforces no-OTP; `gleam_erlang` (Subjects), not `gleam_otp` |
| `build-test-topology-windows` | **CONFIRMED-DELIVERED** — Windows-native build+test green (508/0); WSL for AtomVM/quicer |
| `langpair-dart-gleam` | **CONFIRMED-DELIVERED (record)** — `specs/032-codeconv-gleam-langpair/spec.md` present |
| `monitor-primitive-verification` | **CONFIRMED-DELIVERED (record)** — `specs/039-m2-0-verify-erlang-monitor-atomvm/spec.md` present |
| `port-source-basis-dart` | **CONFIRMED-DELIVERED (record)** — `specs/031-gleam-port-spike/spec.md` present (Dart-basis decision) |
| `subtree-scaffold` | **CONFIRMED-DELIVERED** — 033 scaffold subsystems all filled, no placeholder stubs on the runtime path |

## Supplementary (out-of-050-evidence — NOT the basis of this close)

The gated AtomVM probe was independently **RUN on this host 2026-07-27** (release
`v0.7.0-alpha.1` wrapper, sha256-verified) → `ATOMVM GATED: PASS`, byte-identical
(see `phase2-plan/atomvm-probe-runbook.md`). This is the third independent AtomVM confirmation and
strengthens confidence, but AtomVM execution is excluded from 050 acceptance — the close rests on
the by-construction constraints above, not on this probe.

**Close status: CLOSED (by-construction).** No-OTP policy machine-enforced, Windows-native
build/test green (508/0), scaffold complete, all decision records present; no drift escalated. The
supplementary AtomVM probe PASS is recorded as corroboration only.
