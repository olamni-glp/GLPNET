<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 57a8b9fc-2569-4312-9cb0-17b9be75ff62
-->

# Implementation Plan: Full-scope Gleam GLP implementation

**Branch**: `059-full-scope-gleam-glp-implementation` | **Date**: 2026-07-27 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/059-full-scope-gleam-glp-implementation/spec.md`

**Note**: This plan **composes** the already-adjudicated authoritative inputs; it does not re-derive
them (spec Assumptions, FR-001/FR-013). The Phase-2 FINAL outline plan is the authoritative WP
inventory; this file maps its 5 waves onto the buildkit pipeline artifacts and records the gate check.

## Summary

Bring a second, independent **Gleam/BEAM** GLP runtime to full parity with the Dart/C# reference
(v2.16), then deliver it *inside the yngenios architecture* as the embedded controller of the frozen
spec-056 four-service fabric. The work is already decomposed into **90 work packages across 5 waves**
(freeze/guard → verify/rule-request → close → build → accept) in
`docs/research/fullscope-gleam/feature-outline-plan-FINAL-2026-07-20.md`, backed by a 154-capability
gap inventory (44 delivered / 9 partial / 99 gap-class) and binding engineer rulings G1–G5 + G3-A.
Technical approach: **freeze the delivered 44 behind a pinned-interface register + grow-only tripwire
suites**, **verify-before-close** every unconfirmed gap against named reference programs, **build** the
wholly-absent subsystems (multiagent runtime, QUIC-WS mesh, FE/BE split, embeddability service-box) to
reference parity, and **accept** with a single fresh-session sweep. Execution is marathon-scale
(`mrun-8bda036d9e9b` scoping-discharged; execution tracked under the active run) and ships session by
session via buildkit GitFlow from this branch.

## Technical Context

**Language/Version**: Gleam 1.17 on Erlang/OTP 29 (BEAM); reference oracles in Dart 3.9+ (REPL runtime)
and C#/.NET 8 (glp_link, glp_quick_host, result-codec, crdtmsg suites).
**Primary Dependencies**: gleeunit (Gleam test), rebar3 3.27, `gleam_erlang`/`gleam_otp`; Erlang
`gen_tcp`/`gen_udp`, quicer (Profile-C QUIC, WSL-only NIF), ZMQ leaf via `glp_link_zmq_ffi.erl` behind
the T045 transport seam; AtomVM (gated manual probe only).
**Storage**: N/A for the engine (in-heap terms). Cross-repo wave-4 wiring binds to the yngenios spec-056
data plane (S1 storage / S3 kv) via the shared mailbox binding — integration only, no sources imported.
**Testing**: Gleam gleeunit (frozen 463-test baseline, now 508 green, grow-only); Dart unified REPL
suite (`test/run_all_tests.sh`, `DART=<win-dart>`); C# reference xUnit suites; a differential
parity harness against the reference program corpus (byte-identical where the plan pins bytes).
**Target Platform**: Windows-native build + WSL test topology (recorded); Profile-C QUIC WSL-only and
environment-fragile; AtomVM per its recorded manual procedure.
**Project Type**: Multi-runtime language implementation (compiler + engine + REPL + link/transport +
multiagent + embeddability) with cross-runtime parity oracles — not a single-src greenfield layout.
**Performance Goals**: Parity-governed, not throughput-governed (G4): outcomes identical to the Dart
oracle, byte-identical where pinned. No independent latency/throughput target is in scope.
**Constraints**: Reference v2.16 behavior governs on any divergence (FR-005, incl. the UnifyConstant
ground-struct-literal golden pin); no frozen interface changes without a recorded unfreeze ruling
(FR-002); pinned suites grow-only and always green (FR-003, SC-001); every scope exit only by recorded
engineer ruling (FR-012).
**Scale/Scope**: 154 inventory detail_ids + open-items; 90 WPs; 5 waves; 6 prioritized user stories
(P1 freeze/guard → P6 yngenios fabric on the embedded engine). Multi-session marathon effort.

**Open items carried into execution** (NEEDS-CLARIFICATION-equivalents, tracked in research.md):
- `rule-quic-sideprocess-relay` — **OPEN escalation**; must be ruled before its wave-4 gate; dependent
  WPs blocked until then (never re-scoped). *(spec Assumptions; FR-011.)*
- `rule-embeddability-api-yngenios-wiring` — **RESOLVED 2026-07-20** (Option C, full wiring).
- Store-kernel scope (`store_put`/`store_get` kernels vs host-owned log) — escalated to the engineer,
  never resolved by the team (FR-010).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Verdict | Basis |
|---|---|---|
| I. Spec-First — code never source of truth | **PASS** | Feature composes adjudicated rulings G1–G5/G3-A + gap inventory; no code-first. Verify-before-close (FR-004) is spec-first made mechanical. |
| II. Bug-Protocol / No-Workarounds | **PASS** | Edge cases mandate halt+escalate on divergence, never inline patch (spec Edge Cases; FR-011). |
| III. SRSW inviolable (SRSW-escape-token scan) | **PASS** | Zero SRSW-escape tokens in spec/plan; Gleam engine enforces SRSW (writer-MGU). |
| IV-a. Language Authority | **PASS** | No language extension proposed. S4 mint/policy kernel and store-kernel scope remain owner-gated/escalated (FR-008/FR-010); prior ZMQ scope change was owner-approved (DISCIPLINE 1.14). |
| IV-b. Preserve Working Internals | **PASS** | Wave-1 freeze register *protects* delivered internals; changes require an unfreeze ruling (FR-002). |
| V. Claude-Only LM / No External API (external-LM-key/provider-token scan) | **PASS** | Zero external-LM API-key or non-Claude-provider tokens in artifacts. Any codex-CLI review degrades to Claude; no external-provider LM on any path. |
| VI-a. Additive-only, idempotent, single-head migrations | **PASS** (N/A) | Feature adds no DB migration; marathon/roadmap rows are additive by construction. |
| VI-b. Single OS-lock-guarded PGLite cluster | **PASS** | No new working-data cluster; marathon per-run store is the permitted out-of-repo exemption. |
| VII. Test-gated, commit-scoped shipping | **PASS** | Grow-only pinned suites (FR-003); ships feature→develop→release/*→main via GitFlow; main only via `buildkit release` (never hand-merged). |
| VIII. Single source of truth & traceability | **PASS** | roadmap(059) → this pipeline → WPs; FINAL plan is the single authoritative WP inventory; this plan references, never duplicates it. |

**Result: GATE PASS — no violations.** Complexity Tracking below is empty (no justified violations).

## Project Structure

### Documentation (this feature)

```text
specs/059-full-scope-gleam-glp-implementation/
├── spec.md              # Feature spec (done)
├── plan.md              # This file (/bk-plan output)
├── research.md          # Phase 0 output — composes gap inventory + rulings + open escalations
├── data-model.md        # Phase 1 output — WP, registers, coverage table, gate ruling entities
├── quickstart.md        # Phase 1 output — the fresh-session acceptance sweep (SC-001..SC-009)
├── contracts/           # Phase 1 output — frozen-interface register, service-box, parity contracts
├── checklists/
│   └── requirements.md  # (existing)
└── tasks.md             # Phase 2 output (/bk-tasks) — the 90 WPs in wave/dependency order
```

**Authoritative design inputs (composed, not duplicated):**

```text
docs/research/fullscope-gleam/
├── gap-inventory-2026-07-19.md                 # 154 capabilities: 44 delivered / 9 partial / 99 gap-class
├── feature-outline-plan-FINAL-2026-07-20.md    # 90 WPs, waves 1–5, deps, restart-safe acceptance evidence
├── frozen-interface-register.md                # pinned delivered interfaces + protected test files + unfreeze path
├── phase2-verify/rulings.md                    # binding engineer rulings G1–G5, G3-A
├── phase2-verify/*.md                           # per-detail_id DELIVERED/ABSENT/PARTIAL verdicts
├── phase2-plan/                                 # phase-2 planning evidence
└── roadmap-snapshot-2026-07-19.md
```

### Source Code (repository root — real layout this feature touches)

```text
glp_gleam/                     # the Gleam/BEAM GLP instance (primary build surface)
├── src/glp/
│   ├── runtime/               # terms, heap, unification (delivered — frozen wave 1)
│   ├── compiler/              # parser/compile/mode-conversion (partial — wave 3 close)
│   ├── engine/                # bytecode runner, engine execution (delivered/partial)
│   ├── mad/                   # multiagent: mad_engine, kernels, globalize/localize, message (build wave 3–4)
│   ├── link/                  # link seam, wire, transport (loopback/TCP done; QUIC-WS/pump build wave 4)
│   └── repl/                  # REPL surface (delivered — frozen)
└── test/glp/**                # gleeunit tripwire suites (grow-only)

csharp/                        # C#/.NET reference peers + parity oracles (glp_link, glp_quick_host, crdtmsg)
programs/                      # GLP source corpus + multiagent plays (parity acceptance set)
test/run_all_tests.sh          # Dart unified REPL reference suite (parity oracle)
```

**Structure Decision**: No new top-level tree is introduced. Execution mutates `glp_gleam/` (the Gleam
instance under build), grows the three pinned suites, and writes evidence + registers under
`docs/research/fullscope-gleam/`. Wave-4 yngenios wiring is **cross-repo integration** against
`D:\bstdev\research\yngenios-003` (frozen spec-056) — no yngenios sources enter this repo (FR-008).

## Wave → pipeline mapping

The FINAL plan's 5 waves become the tasks.md phase spine (Phase 2, `/bk-tasks`). Each WP's
**restart-safe acceptance evidence** is its completion bar (FR-001/FR-013):

| Wave | Kind | WPs | Gate into next wave |
|---|---|---|---|
| 1 | freeze + guard | 21 | Frozen-interface register complete; 3 pinned suites green + grow-only tripwire armed. |
| 2 | verify + rule-request | 34 | Every unconfirmed-gap capability has a committed DELIVERED/ABSENT/PARTIAL verdict; open escalations queued for ruling. |
| 3 | close | *(paired to ABSENT verdicts)* | Each confirmed gap closed to named-reference parity, or ruled out of scope. |
| 4 | build | *(absent subsystems)* | Multiagent runtime, QUIC-WS mesh, FE/BE split, embeddability service-box built to parity; yngenios wiring live. |
| 5 | accept | sweep + polish | One fresh-session acceptance sweep: all SCs have committed evidence rows. |

Blocking rule (FR-011): an open escalation (`rule-quic-sideprocess-relay`) blocks its dependent wave-4
WPs — never worked around. A verify re-run diverging from recorded M1 evidence halts as a drift finding.

## Complexity Tracking

*No Constitution Check violations — table intentionally empty.*

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
