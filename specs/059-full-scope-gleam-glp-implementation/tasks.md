<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: Full-scope Gleam GLP implementation

**Branch**: `059-full-scope-gleam-glp-implementation` | **Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), contracts/

**Authoritative WP inventory**: `docs/research/fullscope-gleam/feature-outline-plan-FINAL-2026-07-20.md` (90 WPs). This tasks list maps each WP 1:1 in **wave order** (the plan's dependency spine, FR-001), tagging each with the user story it serves. Each WP's restart-safe acceptance evidence in the FINAL plan is its completion bar (FR-013).

**Structure decision**: Phases follow the 5 waves (not one-phase-per-user-story) because the 6 user stories span waves and wave order IS the execution dependency order. `[US*]` tags preserve story traceability. `[P]` = parallel-safe within its wave.

---

## Phase 1: Setup

- [x] T001 Confirm BEAM toolchain + Windows/WSL topology and both parity oracles available (Gleam winget + Erlang/OTP 29 on PATH; `DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart.exe`) per specs/059-full-scope-gleam-glp-implementation/quickstart.md
- [x] T002 [P] Establish green baselines on synced branch: `cd glp_gleam && gleam test` (>=508/0) and `bash test/run_all_tests.sh` (0 fail) — record as the freeze baseline
- [x] T003 [P] Confirm escalation register state in specs/059-full-scope-gleam-glp-implementation/research.md: `rule-quic-sideprocess-relay` OPEN (blocks wave-4 QUIC dependents); `rule-embeddability-api-yngenios-wiring` RESOLVED
- [x] T004 Confirm coverage/traceability union (154 detail_ids + open-items) is loaded from docs/research/fullscope-gleam/gap-inventory-2026-07-19.md for terminal-disposition tracking (SC-003)

## Phase 2: Foundational — Wave 1 FREEZE + GUARD (blocks all later waves)

**Goal**: US1 — drift-proof foundation. **Gate**: see plan.md Wave→pipeline mapping.

- [x] T005 [P] [US1] `freeze-body-kernel` (freeze, S, b2-c2-015) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T006 [P] [US1] `freeze-bytecode-isa` (freeze, S, b1-c2-005) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T007 [P] [US1] `freeze-bytecode-runner` (freeze, S, b2-c2-016) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T008 [P] [US1] `freeze-codec-envelope` (freeze, M, b1-c2-006) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T009 [P] [US1] `freeze-compiler-pipeline` (freeze, M, b1-c2-004) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T010 [P] [US6] `freeze-embeddability-api` (freeze, S, b2-c2-020) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T011 [P] [US1] `freeze-engine-execution` (freeze, M, b1-c2-002) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T012 [P] [US1] `freeze-engine-facade` (freeze, S, b1-c2-003) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T013 [P] [US1] `freeze-guard-kernel` (freeze, S, b2-c2-017) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T014 [P] [US4] `freeze-link-layer` (freeze, S, b2-c2-019) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T015 [P] [US4] `freeze-link-transport-seam` (freeze, S, b1-c2-008) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T016 [P] [US4] `freeze-link-wire` (freeze, S, b1-c2-007) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T017 [P] [US1] `freeze-module-system` (freeze, S, b2-c2-018) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T018 [P] [US1] `freeze-platform-atomvm-policy` (freeze, S, b1-c2-010) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T019 [P] [US1] `freeze-repl-surface` (freeze, S, b1-c2-009) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T020 [P] [US1] `freeze-runtime-term-heap` (freeze, S, b1-c2-001) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T021 [P] [US1] `guard-atomvm-gated-probe` (guard, S, b1-c2-014) — acceptance per FINAL plan ✓ COMPLETE 2026-07-27: manual AtomVM probe RUN on OLAMNIT (release v0.7.0-alpha.1 wrapper, sha256-verified) → ATOMVM GATED PASS, byte-identical (int64 max/min + float π, 3× round-trip true); src unmodified vs baseline (item 3)
- [x] T022 [P] [US5] `guard-fe-be-envelope-seam` (guard, S, b2-c1-001) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T023 [P] [US1] `guard-suite-csharp-reference` (guard, S, b1-c2-013) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T024 [P] [US1] `guard-suite-dart-reference` (guard, S, b1-c2-012) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)
- [x] T025 [P] [US1] `guard-suite-gleam` (guard, S, b1-c2-011) — acceptance per FINAL plan ✓ VERIFIED 2026-07-27 (register@49b52342 + Gleam 508/0, REPL 0-fail, C# glp_link 152/0)

## Phase 3: Wave 2 — VERIFY + RULE-REQUESTS

**Goal**: US2 — every promised capability verified. **Gate**: see plan.md Wave→pipeline mapping.

- [x] T026 [P] [US2] `rule-bytecode-runner-unifyconstant-divergence` (rule-request, S, b2-c2-003) — acceptance per FINAL plan ✓ RULED (docs/research/fullscope-gleam/phase2-verify/rulings.md)
- [x] T027 [P] [US2] `rule-codec-compiled-il-on-the-wire` (rule-request, S, b3-c2-024) — acceptance per FINAL plan ✓ RULED (docs/research/fullscope-gleam/phase2-verify/rulings.md)
- [x] T028 [P] [US2] `rule-compiler-antlr-shared-grammar-spike` (rule-request, S, b3-c2-021) — acceptance per FINAL plan ✓ RULED (docs/research/fullscope-gleam/phase2-verify/rulings.md)
- [x] T029 [P] [US6] `rule-embeddability-api-yngenios-wiring` (rule-request, S, b2-c2-022) **[RESOLVED 2026-07-20 — Option C full wiring, see spec Clarifications]** — acceptance per FINAL plan ✓ RULED (docs/research/fullscope-gleam/phase2-verify/rulings.md)
- [x] T030 [P] [US4] `rule-mesh-ring-escalation` (rule-request, S, b2-c2-002) — acceptance per FINAL plan ✓ RULED (docs/research/fullscope-gleam/phase2-verify/rulings.md)
- [x] T031 [P] [US3] `rule-multiagent-runtime-escalation` (rule-request, S, b2-c2-001) — acceptance per FINAL plan ✓ RULED (docs/research/fullscope-gleam/phase2-verify/rulings.md)
- [x] T032 [P] [US2] `rule-open-items-cycle2-residual` (rule-request, S, b2-c2-023) — acceptance per FINAL plan ✓ RULED (docs/research/fullscope-gleam/phase2-verify/rulings.md)
- [x] T033 [P] [US2] `rule-open-items-merge-candidates` (rule-request, S, b2-c2-024) — acceptance per FINAL plan ✓ RULED (docs/research/fullscope-gleam/phase2-verify/rulings.md)
- [x] T034 [P] [US2] `rule-open-items-unswept-areas` (rule-request, S, b2-c2-025) — acceptance per FINAL plan ✓ RULED (docs/research/fullscope-gleam/phase2-verify/rulings.md)
- [x] T035 [P] [US2] `rule-process-engine-instances-scaling-research` (rule-request, S, b3-c2-023) — acceptance per FINAL plan ✓ RULED (docs/research/fullscope-gleam/phase2-verify/rulings.md)
- [x] T036 [P] [US4] `rule-quic-sideprocess-relay` (rule-request, S, b1-c2-015) **[RULED 2026-07-27 — Disposition 2: minimal relay smoke test required; enforced by T098 `close-quic-sideprocess-relay-smoketest`; escalation-register.md]** — acceptance per FINAL plan
- [x] T037 [P] [US4] `rule-quicws-mesh-full-mesh-native-quic` (rule-request, S, b3-c2-025) — acceptance per FINAL plan ✓ RULED (docs/research/fullscope-gleam/phase2-verify/rulings.md)
- [x] T038 [P] [US4] `rule-transports-zmq-comm-base` (rule-request, S, b3-c2-022) — acceptance per FINAL plan ✓ RULED (docs/research/fullscope-gleam/phase2-verify/rulings.md)
- [x] T039 [P] [US2] `verify-acceptance-acceptance-sweep-and-polish` (verify, S, b3-c1-017) — acceptance per FINAL plan ✓ VERDICT 2026-07-27 (phase2-verify/verify-acceptance-acceptance-sweep-and-polish.md): DELIVERED-as-verify — capstone unstarted as expected (no run_link_tests_cross_gleam.sh, no acceptance.md)
- [x] T040 [P] [US2] `verify-bytecode-bytecode-instruction-set` (verify, M, b3-c1-005) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-bytecode-bytecode-instruction-set.md)
- [x] T041 [P] [US2] `verify-codec-compiled-il-on-the-wire` (verify, M, b3-c1-011) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-codec-compiled-il-on-the-wire.md)
- [x] T042 [P] [US2] `verify-compiler-antlr-shared-grammar-spike` (verify, M, b3-c1-004) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-compiler-antlr-shared-grammar-spike.md)
- [x] T043 [P] [US6] `verify-embed-embeddability-service-box` (verify, S, b3-c1-014) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-embed-embeddability-service-box.md)
- [x] T044 [P] [US5] `verify-engine-engine-composition-root` (verify, S, b3-c1-015) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-engine-engine-composition-root.md)
- [x] T045 [P] [US5] `verify-febe-embedded-switch-role-framing` (verify, M, b3-c1-013) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-febe-embedded-switch-role-framing.md)
- [x] T046 [P] [US2] `verify-guards-guard-defined` (verify, S, b3-c1-002) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-guards-guard-defined.md)
- [x] T047 [P] [US2] `verify-langsurface-channel-convention` (verify, M, b3-c1-003) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-langsurface-channel-convention.md)
- [x] T048 [P] [US4] `verify-link-inbound-pump` (verify, M, b3-c1-008) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-link-inbound-pump.md)
- [x] T049 [P] [US2] `verify-module-system-scope-chain` (verify, S, b2-c2-021) — acceptance per FINAL plan ✓ VERDICT 2026-07-27 (phase2-verify/verify-module-system-scope-chain.md): ABSENT — directory self.glp scope-chain not in Gleam (single root prelude only); scope decision surfaced
- [x] T050 [P] [US3] `verify-multiagent-multiagent-boot-loader` (verify, S, b3-c1-007) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-multiagent-multiagent-boot-loader.md)
- [x] T051 [P] [US2] `verify-parity-differential-harness` (verify, M, b3-c2-016) — acceptance per FINAL plan ✓ VERDICT 2026-07-27 (phase2-verify/verify-parity-differential-harness.md): harness re-run OK (differential X:=2+3 all-3 AGREE; gleam 508/0 grew from 443; 10x bound PASS) BUT ⚠ HALT/ESCALATE — corpus rc=44: 162 agree, 44 MISSING-golden (evidence-reproducibility drift, NOT codec divergence); recorded 201/206 not fresh-reproducible on branch → engineer decision needed
- [x] T052 [P] [US2] `verify-platform-atomvm-compatibility-by-construction` (verify, S, b3-c1-018) — acceptance per FINAL plan ✓ VERDICT 2026-07-27 (phase2-verify/verify-platform-atomvm-compatibility-by-construction.md): DELIVERED by-construction — deps_policy no-OTP (gleam_erlang not gleam_otp), 13 subsystems filled, specs 039/031/032 present; AtomVM probe RUN supplementary
- [x] T053 [P] [US2] `verify-process-baseline-program-dossier` (verify, S, b3-c1-019) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-process-baseline-program-dossier.md)
- [x] T054 [P] [US2] `verify-proofs-proof-dist-deref-convergence` (verify, S, b3-c1-020) — acceptance per FINAL plan ✓ VERDICT 2026-07-27 (phase2-verify/verify-proofs-proof-dist-deref-convergence.md): PI:14 DISCHARGED (Lean sorry-free + lake build green + prose + suite), PI:17 UNDISCHARGED as expected (scaffold; 050 T057/T058 unchecked)
- [x] T055 [P] [US4] `verify-quicws-link-completion-live-repl-bridge` (verify, M, b3-c1-009) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-quicws-link-completion-live-repl-bridge.md)
- [x] T056 [P] [US2] `verify-repl-repl-boot-command` (verify, S, b3-c1-006) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-repl-repl-boot-command.md)
- [x] T057 [P] [US2] `verify-runtime-arithmetic-expression` (verify, M, b3-c1-001) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-runtime-arithmetic-expression.md)
- [x] T058 [P] [US4] `verify-transports-multi-accept-transport-extension` (verify, M, b3-c1-010) — acceptance per FINAL plan ✓ VERDICT 2026-07-27 (phase2-verify/verify-transports-multi-accept-transport-extension.md): PARTIAL — multi-accept/quiescence ABSENT, frame-hardening DELIVERED; zmq-comm-base premise SUPERSEDED (ZMQ now in-contract)
- [x] T059 [P] [US4] `verify-wireproto-crdt-convergence` (verify, M, b3-c1-012) — acceptance per FINAL plan ✓ VERDICT COMMITTED (docs/research/fullscope-gleam/phase2-verify\verify-wireproto-crdt-convergence.md)

## Phase 4: Wave 3 — CLOSE (paired to ABSENT verdicts)

**Goal**: US2/US3/US4/US5/US6 — close gaps to parity. **Gate**: see plan.md Wave→pipeline mapping.

- [ ] T060 [P] [US2] `close-acceptance-acceptance-sweep-and-polish` (close, L, b3-c2-042) — acceptance per FINAL plan
- [ ] T061 [P] [US2] `close-body-kernel-now-send` (close, M, b2-c2-004) — acceptance per FINAL plan
- [x] T062 [P] [US2] `close-bytecode-bytecode-instruction-set` (close, M, b3-c1-030) — CLOSED 2026-07-27: Op union discriminant-complete (44) + runtime WxW (unify.gleam:88-89) CONFIRMED, opcodes_test/writer_mgu_adversarial green; bytecode-lint recorded auxiliary-optional; evidence phase3-close/close-bytecode-bytecode-instruction-set.md
- [ ] T063 [P] [US2] `close-bytecode-runner-missing-opcodes` (close, M, b2-c2-005) — acceptance per FINAL plan
- [x] T064 [P] [US2] `close-codec-compiled-il-on-the-wire` (close, M, b3-c2-036) — CLOSED 2026-07-27: term/envelope/builder byte-parity CONFIRMED (golden_corpus_test reads corpus.hex live, gleam test 465+); IL-codec family ABSENT-by-design/owner-deferred, held pending rule-codec-compiled-il-on-the-wire (b3-c2-024); evidence phase3-close/close-codec-compiled-il-on-the-wire.md
- [ ] T065 [P] [US2] `close-compiler-antlr-shared-grammar-spike` (close, L, b3-c2-029) — acceptance per FINAL plan
- [ ] T066 [P] [US5] `close-distribution-engine-sessions` (close, L, b2-c2-014) — acceptance per FINAL plan
- [x] T067 [P] [US6] `close-embed-embeddability-service-box` (close, S, b3-c1-039) — CLOSED 2026-07-27: requirements-level close; service-box.contract.md RATIFIED (A1 done), 21 P7 items dispositioned & build-bound to b3-c2-047; A5 store-kernel scope + C3/D1-D6 gates CARRIED FORWARD to engineer (FR-010 never-team-resolved; recommendation=host-owned log); no code; evidence phase3-close/close-embed-embeddability-service-box.md
- [ ] T068 [P] [US6] `close-embeddability-host-api` (close, M, b2-c2-013) — acceptance per FINAL plan
- [ ] T069 [P] [US5] `close-engine-engine-composition-root` (close, M, b3-c1-040) — acceptance per FINAL plan
- [x] T070 [P] [US5] `close-febe-embedded-switch-role-framing` (close, M, b3-c1-038) — CLOSED 2026-07-27: single-process shape confirmed + 026 design authority DELIVERED; all 8 detail_ids dispositioned & bound to build-fe-be-process-split (b3-c2-046) acceptances; forks F1 (snapshot API) + F2 (liveness-vs-BEAM-supervision) carried to engineer, NOT resolved; evidence phase3-close/close-febe-embedded-switch-role-framing.md
- [ ] T071 [P] [US2] `close-guard-kernel-wait-guards` (close, M, b2-c2-006) — acceptance per FINAL plan
- [x] T072 [P] [US2] `close-guards-guard-defined` (close, M, b3-c1-027) — CLOSED 2026-07-27: guard-defined (guard_defs.gleam three-valued Guard-opcode; test_defined_guards.glp R=ok/R=not_channel agree Dart/C#/Gleam) + guard-purity (compile-time enforced; test_time_guard.glp negative agrees) both DELIVERED, clean; evidence phase3-close/close-guards-guard-defined.md
- [ ] T073 [P] [US2] `close-langsurface-channel-convention` (close, M, b3-c1-028) — acceptance per FINAL plan
- [x] T074 [P] [US4] `close-link-inbound-pump` (close, L, b3-c1-033) — CLOSED 2026-07-27: link_driver+link_pump (egress-as-goal, no heap onBind; step_link mirrors step_mad, pure run untouched); 6 link programs round-trip 2-proc real-TCP ≡ Dart oracle (harness 7/7 PASS), gleam 544/0; ConstString renderer-parity gap flagged for follow-up; evidence phase3-close/close-link-inbound-pump.md
- [x] T075 [P] [US4] `close-link-layer-fault-decoration` (close, S, b2-c2-009) — CLOSED 2026-07-27: fault-as-data decoration parity to C# LinkFaults — bounded-silence heuristic (tempFail/permFail), fencing→permFail (T077 fencing_registry wired), establishment-failure→permFail on monitor (not bare LinkAbort); gleam 587/0, harness 7/7 (mon.glp observes decoration); evidence phase3-close/close-link-layer-fault-decoration.md
- [x] T076 [P] [US4] `close-link-layer-glp-primitives` (close, L, b2-c2-008) — CLOSED 2026-07-27: 8-module primitives layer (link_terms/transport_registry/handle/registry/runtime/egress/faults/kernels) faithful Dart port, +36 tests; shares acceptance with T074 (6 link programs round-trip, gleam 544/0); evidence phase3-close/close-link-inbound-pump.md
- [x] T077 [P] [US4] `close-link-layer-sequence-dedup` (close, M, b2-c2-010) — CLOSED 2026-07-27: 9-module reliability sublayer (sequencer/inbound_ordering/frame_reassembler/send_window/fencing/cycle_guard/reclaimer/snapshot/frame_exception) faithful Dart port; ordering+reassembly on live pump path, +27 tests gleam 571/0, harness 7/7 (sr.glp passes); evidence phase3-close/close-link-layer-sequence-dedup.md
- [ ] T078 [P] [US2] `close-module-system-runtime-rpc` (close, M, b2-c2-007) — acceptance per FINAL plan
- [ ] T079 [P] [US3] `close-multiagent-multiagent-boot-loader` (close, L, b3-c2-032) — acceptance per FINAL plan
- [x] T080 [P] [US2] `close-parity-differential-harness` (close, M, b3-c1-041) — CLOSED 2026-07-27: CRLF root cause fixed (9bf9d02f/8627f7f3); corpus rc=0 206 agree/0 diverge/0 missing, 10x PASS; evidence phase3-close/close-parity-differential-harness.md
- [x] T081 [P] [US2] `close-platform-atomvm-compatibility-by-construction` (close, S, b3-c1-043) — CLOSED 2026-07-27: by-construction DELIVERED — no-OTP policy (deps_policy_test green; gleam.toml gleam_erlang only), Windows-native gleam build+test 508/0, 13 subsystems filled, 6 records dispositioned; AtomVM probe PASS supplementary only; evidence phase3-close/close-platform-atomvm-compatibility-by-construction.md
- [x] T082 [P] [US2] `close-process-baseline-program-dossier` (close, S, b3-c2-044) — CLOSED 2026-07-27: 6 process/decision records reconciled MATCH, 0 drift; conditional-drift trigger recorded (constituent rows understate M1 by protocol, become drift only when combined-full-gleam ships per 050:12,191); scaling-research held pending rule (b3-c2-023); evidence phase3-close/close-process-baseline-program-dossier.md
- [ ] T083 [P] [US2] `close-proofs-proof-dist-deref-convergence` (close, L, b3-c2-045) — acceptance per FINAL plan
- [x] T098 [US4] `close-quic-sideprocess-relay-smoketest` (close, S, ruling-2026-07-27) — CLOSED 2026-07-27: reassembly gate green (gleam_quic/smoke.sh rc=0, 2 MiB envelope whole on stdout / control on stderr); live-C# byte-identity ENVIRONMENT-blocked (msquic; glp_quick_host requires real QUIC) — recorded not waived; T084/T085/T086 inherit same msquic block; evidence phase3-close/close-quic-sideprocess-relay-smoketest.md
- [ ] T084 [P] [US4] `close-quic-client-inprocess-tests` (close, M, b2-c2-012) — acceptance per FINAL plan
- [ ] T085 [P] [US4] `close-quic-transport-leaf` (close, L, b2-c2-011) — acceptance per FINAL plan
- [ ] T086 [P] [US4] `close-quicws-link-completion-live-repl-bridge` (close, L, b3-c2-034) — acceptance per FINAL plan
- [ ] T087 [P] [US2] `close-repl-repl-boot-command` (close, M, b3-c1-031) — acceptance per FINAL plan
- [ ] T088 [P] [US2] `close-runtime-arithmetic-expression` (close, L, b3-c2-026) — acceptance per FINAL plan
- [x] T089 [P] [US4] `close-transports-multi-accept-transport-extension` (close, L, b3-c2-035) — CLOSED 2026-07-27: quiescence oracle (quiescence.gleam, GAP-G6, public API for T083/T066) + multi-accept capability (N distinct links, per-link reliability state); gleam 587/0, harness 7/7. 🔻RESIDUAL flagged (not silent-deferred): TCP single-persistent-socket accept-loop (re-listen has port-release window) needs FFI/seam, no acceptance program exercises it — engineer-directed follow-up; evidence phase3-close/close-transports-multi-accept-transport-extension.md
- [ ] T090 [P] [US4] `close-wireproto-crdt-convergence` (close, L, b3-c1-037) — acceptance per FINAL plan

## Phase 5: Wave 4 — BUILD (FE/BE split + yngenios)

**Goal**: US5/US6 — absent subsystems. **Gate**: see plan.md Wave→pipeline mapping.

- [ ] T091 [US5] `build-fe-be-process-split` (build, L, b3-c2-046) — acceptance per FINAL plan
- [ ] T092 [US6] `build-yngenios-embeddability` (build, L, b3-c2-047) — acceptance per FINAL plan

## Phase 6: Wave 5 — ACCEPT (fresh-session sweep)

**Goal**: US1/US6 — terminal acceptance. **Gate**: see plan.md Wave→pipeline mapping.

- [ ] T093 [US6] `accept-febe-embeddability` (accept, M, b3-c1-049) — acceptance per FINAL plan
- [ ] T094 [US1] `accept-full-scope-regression` (accept, L, b3-c2-048) — acceptance per FINAL plan

## Phase 7: Polish & Cross-Cutting

- [ ] T095 Verify SC-001..SC-009 each has a committed evidence row (quickstart.md map); zero open escalation-register entries (SC-009)
- [ ] T096 [P] Confirm all 3 pinned suites still green + grow-only against the freeze baseline (frozen-interface register unmodified)
- [ ] T097 Marathon discharge gate: every WP checkpointed/accepted or ruled-out; run ships via buildkit GitFlow (feature->develop->release/*->main)

---

## Dependencies & Execution Order

- **Wave order is strict** (FR-001): Wave 1 (freeze/guard) -> Wave 2 (verify/rule) -> Wave 3 (close) -> Wave 4 (build) -> Wave 5 (accept). A later wave never starts before its predecessor's gate.
- **verify -> close activation**: a Wave-2 `verify-*` WP emitting ABSENT activates its paired Wave-3 `close-*` WP (data-model.md).
- **QUIC-relay gate**: `rule-quic-sideprocess-relay` RULED 2026-07-27 (Disposition 2). The QUIC OS-port relay may not be depended on until T098 `close-quic-sideprocess-relay-smoketest` passes; that WP gates the Wave-3 QUIC closes (T084–T086) and all Wave-4 QUIC dependents (escalation-register.md; FR-011).
- **Within a wave**: `[P]`-tagged WPs are parallel-safe (independent files / distinct builders in the 3rtask split).

## Implementation Strategy (MVP-first)

- **MVP = Wave 1 (US1)**: the frozen-interface register + grow-only tripwire suites. Delivers the drift-proof foundation independently testable (SC-001).
- Then Wave 2 (verify, US2) makes the gap map concrete; Wave 3 (close) drives gaps to parity; Wave 4 builds the absent subsystems (US3-US6); Wave 5 accepts.
- Marathon-scale: each session ships green via buildkit GitFlow from this branch; main only via `buildkit release`.

