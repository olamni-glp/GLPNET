<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: Post-wave consolidation — verified gap closure (REPL/engine + Full-Gleam)

**Feature**: 064-post-wave-gap-closure · **Branch**: `064-post-wave-gap-closure` · **MVP gate**: US1+US2 (clarify Q4)
**Inputs**: spec.md (5 stories, 12 FRs), plan.md, research.md (D1–D6), data-model.md, contracts/{dist-unify,quiescence,il-request-kind,febe-split}.md

## Phase 1 — Setup

- [X] T001 Record baselines green and commit checkpoint: bash test/run_all_tests.sh; glp_gleam/smoke.sh; dotnet test csharp suites; test/parity/run_gleam_corpus.sh; test/parity/cross_runtime/run_all.sh — counts recorded in specs/064-post-wave-gap-closure/baseline.md
- [X] T002 [P] Read the C# link remote-binding + quiescence sources end-to-end and record the parity checklist (message kinds, ordering, MGU checks) in specs/064-post-wave-gap-closure/parity-checklist.md (D1/D2 evidence anchor)

## Phase 2 — Foundational (blocking)

- [ ] T003 Extend the Gleam link seam with the four dist-unify message kinds (VAR_EXPORT/DIST_BIND/DIST_SUSPEND/DIST_FAULT) as frame types in glp_gleam/src/glp/link/reliability/frame_codec.gleam + the C# mirror in csharp/glp_link/seam (byte-parity table updated; no behavior yet)
- [ ] T004 [P] Add CENSUS_REQ/CENSUS_REP frame kinds to the same seam files (quiescence contract §Protocol)

## Phase 3 — US1: Gleam link tail (P1)

- [ ] T005 [US1] Implement glp_gleam/src/glp/link/primitives/dist_unify.gleam per contracts/dist-unify.md (writer-MGU rules 1–5, RemoteVarRef/DistBindMsg/RemoteSuspension from data-model.md)
- [ ] T006 [US1] Wire dist_unify into the engine suspension path (glp_gleam/src/glp/engine/runner.gleam additive dispatch arm; remote suspension reactivation on DIST_BIND)
- [ ] T007 [P] [US1] gleeunit suite glp_gleam/test/glp/link/dist_unify_test.gleam: bind/chained/suspend-reactivate/writer-writer-fault/malformed-fault cases
- [ ] T008 [US1] Implement glp_gleam/src/glp/link/primitives/quiescence.gleam per contracts/quiescence.md (census rounds, verdict rules, bounded-silence fault)
- [ ] T009 [P] [US1] gleeunit suite glp_gleam/test/glp/link/quiescence_test.gleam incl. the adversarial delayed-DIST_BIND safety case
- [X] T010 [US1] Implement glp_gleam/src/glp/link/transports/multi_accept.gleam (N concurrent inbound links, none dropped; {exit_on_close, false} + D-9 barrier per FR-012)
- [X] T011 [P] [US1] gleeunit suite glp_gleam/test/glp/link/multi_accept_test.gleam (two concurrent dials, half-close at establishment)
- [X] T012 [US1] Implement the C# bridge acceptor in csharp/glp_quick_host (Gleam-facing TCP acceptor relaying FrameCodec frames to the QUIC-WS mesh) + glp_gleam/src/glp/link/transports/bridge_client.gleam dial helper (D3)
- [ ] T013 [US1] Extend test/parity/cross_runtime with dist-unify/quiescence/multi-link/bridge scenarios, both directions, committed .out results; run ×10 loops (SC-001)
- [ ] T014 [US1] US1 checkpoint: all suites green (zero regression vs T001), scoped commit+push, marathon checkpoint

## Phase 4 — US2: C# multi-client serve path (P2)

- [X] T015 [US2] Extend csharp/glp_link/transports/TcpTransport.cs ListenAsync to a continuous multi-accept loop (remove the one-accept-then-Stop deferral; keep single-accept behavior available for existing callers)
- [X] T016 [US2] Create csharp/glp_engine_host/ClientSession.cs (per-client channel pair, session_id, active|draining|closed lifecycle, RoutedReply routing per data-model.md)
- [X] T017 [US2] Rework csharp/glp_engine_host/EngineServer.cs: accept N clients, register each session with the A31 control program merge tree, route replies per session, discard pending replies on client crash without wedging the merge loop
- [X] T018 [P] [US2] xUnit suite csharp/glp_engine_host.tests/MultiClientServeTests.cs: 3 concurrent clients, interleaved goals, per-client reply isolation, disconnect-one survives
- [X] T019 [US2] US2 checkpoint: engine-host + REPL suites green, scoped commit+push, marathon checkpoint
- [ ] T020 [US2] MVP GATE (Anchor review): record specs/064-post-wave-gap-closure/mvp-gate-review.md over US1+US2 evidence (clarify Q4)

## Phase 5 — US3: IL on the wire (P3)

- [X] T021 [US3] Add LOAD_IL/RUN_GOAL_IL request kinds to csharp/glp_split_protocol/WireProtocol.cs per contracts/il-request-kind.md (062 CompiledIlEnvelope unchanged; typed refusal taxonomy)
- [X] T022 [US3] Client-side compile+ship in csharp/glp_repl_client (compiler reference moves to the client project; per-session path choice, never mixed per module)
- [X] T023 [US3] Engine-side IL execute path in csharp/glp_engine_host with no compiler reference on the execute path (project-file assertion + build check)
- [X] T024 [P] [US3] Create the csharp/glp_split_protocol.tests xUnit project (does not exist yet — add to solution) with il-request round-trip + refusal taxonomy suites; corpus equivalence sweep (IL path vs text path diff empty, SC-003)
- [X] T025 [US3] US3 checkpoint + incremental review note; scoped commit+push, marathon checkpoint

## Phase 6 — US4: FE/BE split + embeddability builds, then 059 sweep (P4)

- [X] T026 [US4] Implement the Gleam BE process entrypoint glp_gleam/src/glp/be/server.gleam (engine+scheduler behind the split protocol over the Gleam TCP transport; exit taxonomy mirrors C# engine host; FR-012 socket norms)
- [X] T027 [US4] Implement the Gleam FE thin REPL loop glp_gleam/src/glp/fe/client.gleam (existing command surface over the split protocol)
- [X] T028 [P] [US4] Two-process split test: standard REPL scenarios through FE↔BE equal single-process results for the regression corpus (contracts/febe-split.md acceptance 1)
- [X] T029 [US4] Cross-runtime FE/BE smoke both directions (Gleam FE ↔ C# BE; C# thin client ↔ Gleam BE, text kinds)
- [X] T030 [US4] Implement glp_gleam/src/glp_embed.gleam (load/run/observe surface, G3-A) + minimal host program test glp_gleam/test/glp_embed_host_test.gleam
- [ ] T031 [US4] Discharge the 059 acceptance sweep: QUIC gate (bridge evidence), T094 full-scope regression accept across Dart/C#/Gleam, SC-sweep rows — evidence recorded in specs/059-full-scope-gleam-glp-implementation/tasks.md checkboxes + a close-out note; remaining true residuals recorded as explicit deferrals
- [ ] T032 [US4] US4 checkpoint + incremental review note; scoped commit+push, marathon checkpoint

## Phase 7 — US5: small residuals (P5)

- [X] T033 [P] [US5] Implement :boot in glp_gleam/src/glp/repl/commands.gleam (multi-isolate plays, G9) + scenario test
- [X] T034 [P] [US5] Complete glp_gleam/src/glp/bytecode/lint.gleam checks per docs/glp-bytecode-v216-complete.md (well-formedness of the v2.16 instruction stream: opcode validity, operand arity, HEAD/GUARD/BODY placement) + known-bad-program test
- [X] T035 [P] [US5] Fix the param_arity panic (reported error, REPL survives) + regression test in glp_gleam/test
- [X] T036 [US5] US5 checkpoint; scoped commit+push, marathon checkpoint

## Phase 8 — Polish & close

- [ ] T037 Full zero-regression sweep across every suite (FR-010) + record final counts vs T001 baseline in baseline.md
- [ ] T038 [P] Record the two gated deferrals durably (native BEAM QUIC-WS leaf; any §1.14-blocked sub-scope if one arose) in specs/064-post-wave-gap-closure/DEFERRALS.md
- [ ] T039 [P] Roadmap bookkeeping rider: advance the six delivered-but-open Full-Gleam rows + antlr4 supersession per the 3rtask verdicts (buildkit-roadmap advance/supersede), export+publish sync
- [ ] T040 /bk-codexreview over the full diff (plan-first, adversarial); apply confirmed fixes; re-run sweep
- [ ] T041 Ship via buildkit GitFlow (announce CalVer to the fleet BEFORE cutting; buildkit ship --skip-preflight) then /bk-close (retro + roadmap advance 064)

## Dependencies

- Phase 2 (T003–T004) blocks US1 (T005+) — frame kinds first.
- US1 → MVP gate needs US2; US2 is independent of US1 code but gate-coupled (T020 needs T014+T019).
- US3 independent of US1/US2 (different projects); may run parallel after Phase 2.
- US4 depends on US1 (link semantics for the split transport) and benefits from US3 (IL kinds optional — text kinds sufficient per contract).
- US5 fully parallel-safe after Phase 1.
- T037–T041 strictly last, in order.

## Parallel opportunities

- T002 ∥ T001; T004 ∥ T003; T007/T009/T011 ∥ their impl siblings' next steps; US3 (T021–T024) ∥ US1 phase; US5 (T033–T035) ∥ anything after Phase 1; T038/T039 ∥ T037.

## Implementation strategy

MVP first (US1+US2 → Anchor gate T020), then US3/US4/US5 under incremental reviews, polish, codexreview, ship, close. Every checkpoint: zero regression + scoped commit + push + marathon checkpoint. STOP-and-report on any discovered core-GLP or reference-semantics bug (Constitution II); §1.14 propose-only if new language surface turns out to be needed (FR-011).
