<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: Durable listener service box (gavri variant)

**Feature**: 064-durable-listener-service-box | **Branch**: `064-durable-listener-service-box`
**Input**: spec.md (US1-US3), plan.md, research.md (R1-R7), data-model.md, contracts/ (3), quickstart.md
**Tests**: included where a spec SC demands a drill (SC-001/002/003); no blanket TDD.

## Phase 1: Setup

- [x] T001 Add `glpservice/` runtime hygiene: gitignore `glpservice/resume.json` + `glpservice/wal/` in `.gitignore`, and commit the operator sample `glpservice/resume.json.sample` (contract schema v1, quickstart values)

## Phase 2: Foundational

- [x] T002 Record the green baseline before any code change (Constitution VII): `dotnet test` for `csharp/glp_link.tests` + `csharp/glp_crdtmsg.tests`, and `bash test/run_all_tests.sh`; note counts in the commit message. ALSO capture the pre-feature no-registration REPL startup transcript to `test/service_box/baseline_startup.txt` (piped `:quit`) — T005 scenario 3 and T014's SC-005 check diff against it

## Phase 3: User Story 1 — Listener survives a REPL restart (P1, MVP)

**Goal**: registered service re-arms on launch with zero operator input.
**Independent test**: quickstart flow — register, restart, peer connects; plus the three US1 acceptance scenarios.

- [x] T003 [US1] Registration reader in the shim project: new hand-authored `out/csharp/glp_repl/ResumeConfig.cs` — walk-up discovery of `glpservice/resume.json` (SharedCertMaterial idiom), JSON parse + v1 validation + `enabled`/`replay` defaults, diagnostics exactly per `contracts/resume-registration.md` (invalid ⇒ named line, absent ⇒ silent)
- [x] T004 [US1] Resume execution in `out/csharp/glp_repl/Program.cs` (`AfterEngineCreated` closure): print `resume: arming <goal> from <program>`, load the registered program (interactive-load semantics), run the goal synchronously (typed-input semantics, R2); load/goal failure ⇒ named diagnostic and fall through to the prompt (FR-009); zero-registration path byte-identical (SC-005)
- [x] T005 [US1] Restart drill script `test/service_box/resume_drill.sh` (piped-stdin, loopback QUIC): scenario 1 register+restart+peer-connect (SC-001 ≤10 s), scenario 2 missing-program diagnostic, scenario 3 no-file transcript identical to pre-feature capture

**Checkpoint**: US1 alone = viable MVP (self-healing service without history).

## Phase 4: User Story 2 — Message history survives restarts (P2)

**Goal**: every received message durable before the program acts on it; boot replay history-before-live, idempotent.
**Independent test**: SC-002 drill (100 messages → restart → complete/ordered/no-dup → second restart byte-identical).

- [x] T006 [P] [US2] Additive delivery hook in `csharp/glp_link/primitives/LinkPump.cs`: nullable `OnDelivered(LinkId, Term)` invoked in `TryApplyNext`'s data case immediately before the heap bind; null ⇒ byte-identical; unit test `csharp/glp_link.tests/LinkPumpDeliveryHookTests.cs` (fires once per delivered term, ordered, not on close/fault items)
- [x] T007 [US2] WAL composition in the shim: new `out/csharp/glp_repl/ServiceWal.cs` — `PgliteOpWal` primary (existing `COLAB_PG_CONN` convention, the single `.pgdb/` cluster) with file `OpWal` fallback under `glpservice/wal/`, primary-then-loud-degrade (061 composer discipline); appender registered on the T006 hook only when an enabled registration exists; append completes before the hook returns (FR-004); both-backends-fail ⇒ deliver anyway + the contract's named diagnostic on every affected delivery (analyze U1 policy)
- [x] T008 [US2] Boot replay per `contracts/message-log-and-replay.md`: after program load, before goal arm — read `Ops` ascending, present to the service through the same inbound delivery shape as live traffic (history precedes live, program-indistinguishable); observer registration deferred until replay completes (no re-append); `resume: replayed <N> message(s)` line; replay failure ⇒ named diagnostic then arm with the replayed prefix. NOTE: the delivery-shape mechanism (pre-seeded pump delivery vs. replay source ahead of the link stream) is the one open implementation choice — resolve inside the contract's constraints, record the choice in the code comment + PR notes
- [x] T009 [US2] SC-002 drill: `test/service_box/history_drill.sh` (N=100 restart drill + double-restart byte-identity) and `csharp/glp_crdtmsg.tests/ReplayIdempotenceTests.cs` (replay reads never append; dot-key dedup on crash-replay overlap)

**Checkpoint**: US1+US2 = the durable chat service.

## Phase 5: User Story 3 — Dialing peers insulated from restarts (P3)

**Goal**: QUIC connector retries until budget, TCP parity.
**Independent test**: dial-before-listen drill; budget-exhaustion behavior unchanged.

- [x] T010 [P] [US3] Retry loop in `csharp/glp_link/transports/QuicTransport.cs` `ConnectAsync` per `contracts/quic-connect-retry.md`: `while(true)` + catch-refused/unreachable + `Task.Delay(100, ct)`, pre-establishment failures only, budget = existing kernel ct; mirror the TCP comment ("listener not up yet — back off and retry")
- [x] T011 [US3] Retry tests `csharp/glp_link.tests/QuicConnectRetryTests.cs`: dial-before-listen succeeds once listener arms (SC-003), never-arms fails only at ct exhaustion with existing fault surface (US3 scenario 2)

## Phase 6: Polish & cross-cutting

- [x] T012 Wire the two drills into `test/run_all_tests.sh` as a new explicit-skip section (never a silent pass; skip line names the standalone gates), following Section I/S conventions
- [x] T013 [P] Verify `quickstart.md` end-to-end as written (fresh clone semantics: sample → rename → run → restart → history back); fix any drifted line
- [x] T014 Full non-regression gate: `dotnet test` (glp_link.tests, glp_crdtmsg.tests) + `bash test/run_all_tests.sh` green vs T002 baseline; SC-004 check = zero language-surface diff (suites pass unmodified); SC-005 check = no-registration startup transcript identical

## Dependencies

```
T001 → T002 → US1 (T003 → T004 → T005)
                US2: T006 [P, may start after T002] → T007 → T008 → T009 (T007/T008 need US1's resume seam)
                US3: T010 [P, may start after T002] → T011   (independent of US1/US2)
US1 + US2 + US3 → Polish (T012 → T013/T014)
```

- Story order: US1 → US2 (replay rides the resume seam); US3 fully parallel to both.
- Parallel opportunities: T006 ∥ US1 phase; T010/T011 ∥ everything after T002; T013 ∥ T012.

## Implementation strategy

MVP first: ship-worthy after US1 (checkpoint above). Then US2 (the durable-history heart of the feature), US3 last (smallest, isolated). Commit per task (Constitution VII); baseline T002 attributes any regression. The single flagged risk is T008's delivery-shape mechanism — bounded by the contract, resolved in implementation, escalated per Bug-Protocol if it can't be met without touching converted files or GLP language surface.
