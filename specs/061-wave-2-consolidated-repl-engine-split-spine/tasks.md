<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 04cf9466-9a14-4ffc-b1b4-967065e4eb0f
-->

# Tasks: Wave 2 Consolidated — REPL Engine Split Spine

**Input**: Design documents from `/specs/061-wave-2-consolidated-repl-engine-split-spine/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/{wire-protocol,snapshot-store,supervision}.md

**Tests**: included — the spec explicitly requires them (FR-033 kill-and-restart in the permanent suite; SC-001 parity corpus; SC-004 round-trip probes; FR-040 model-check verdicts).

**Organization**: grouped by user story (US1 split MVP → US2 snapshot → US3 supervision → US4 restore-resume), each an independently testable increment.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 Create `csharp/glp_split_protocol/GlpSplitProtocol.csproj` (net8.0, refs `csharp/glp_link/GlpLink.csproj` only — the tiny shared wire-protocol library both sides use without the client touching the runtime) and `csharp/glp_engine_host/GlpEngineHost.csproj` (net8.0, refs `out/csharp/glp_runtime_net.csproj`, `csharp/glp_link/GlpLink.csproj`, `csharp/glp_result_codec/GlpResultCodec.csproj`, `csharp/glp_split_protocol/GlpSplitProtocol.csproj`) with empty `Program.cs`; `dotnet build` green
- [X] T002 [P] Create `csharp/glp_repl_client/GlpReplClient.csproj` (net8.0, refs `csharp/glp_link/GlpLink.csproj` + `csharp/glp_split_protocol/GlpSplitProtocol.csproj` only — R7 thin client, no runtime ref) with empty `Program.cs`; `dotnet build` green
- [X] T003 [P] Create `csharp/glp_supervisor/GlpSupervisor.csproj` (net8.0, Microsoft.Extensions.Hosting for BackgroundService) with empty `Program.cs`; `dotnet build` green
- [X] T004 [P] Create `csharp/glp_engine_host.tests/GlpEngineHost.Tests.csproj` (xUnit, refs the three new projects) with one placeholder test; `dotnet test` green
- [X] T005 Record suite baseline: run `bash test/run_all_tests.sh` + `dotnet test` across `csharp/*.tests`; commit baseline note in `specs/061-wave-2-consolidated-repl-engine-split-spine/baseline.md` (Constitution VII)

## Phase 2: Foundational (blocking prerequisites for all stories)

- [X] T006 Define REQUEST/RESPONSE payload types + kind bytes (contracts/wire-protocol.md table) in `csharp/glp_split_protocol/WireProtocol.cs` (shared constants project — both host and client reference it)
- [X] T007 Implement request/response frame encode/decode over `FrameCodec` in `csharp/glp_split_protocol/RequestResponseCodec.cs` — request_id u64 echo, UTF-8 bodies, loud-fail on unknown kind/trailing bytes (wire-protocol rules 3)
- [X] T008 [P] xUnit round-trip + loud-fail tests for the frame codec in `csharp/glp_engine_host.tests/RequestResponseCodecTests.cs`

## Phase 3: US1 — Run GLP goals from a separate client process (P1) 🎯 MVP

**Goal**: thin client ↔ engine host over TCP loopback; results parity with the single-process REPL; engine survives client exit.
**Independent test**: quickstart.md "Run the split" — load + goal through the client matches `out/csharp/glp_repl` output; reconnect keeps loaded programs.

- [X] T009 [US1] Implement `csharp/glp_engine_host/EngineServer.cs`: one-accept TCP loopback listener on `--listen`, single client (FR-002), loud PROTOCOL_ERROR + close on second accept (wire rule 1), port-in-use refusal (edge case)
- [X] T010 [US1] Implement `csharp/glp_engine_host/RequestDispatcher.cs`: LOAD_SOURCE → engine full pipeline; RUN_GOAL → execute, build 038 `ResultEnvelope` (ground-only subset, engine-side pre-rendered bindings — R6) + length-prefixed UTF-8 output blob (R3); STATUS; errors → structured RESULT/PROTOCOL_ERROR leaving engine serving (FR-006)
- [X] T011 [US1] Engine bootstrap in `csharp/glp_engine_host/Program.cs`: load root `programs/self.glp` prelude at start (FR-003), `--listen` arg, EngineSession state machine `starting→serving` (data-model.md)
- [X] T012 [P] [US1] Implement thin client `csharp/glp_repl_client/Program.cs` + `ClientChannel.cs`: REPL loop (load/goal/`:status`/`:quit`), renders envelope results, distinguishes transport failure from goal failure (FR-007), no local self.glp (R7)
- [X] T013 [P] [US1] xUnit server tests in `csharp/glp_engine_host.tests/EngineServerTests.cs`: single-accept, second-client refusal, compile-error keeps serving, client-exit + reconnect keeps loaded program (US1 AS-3/AS-4)
- [X] T014 [US1] Parity harness in `csharp/glp_engine_host.tests/ParityCorpusTests.cs`: run the C#-runtime-compatible subset of `test/run_all_tests.sh` Section-A programs through the split client, diff rendered results vs single-process REPL (SC-001, research D11)
- [X] T015 [P] [US1] SPIN full wire-protocol model in `docs/research/repl-engine-separation/models/spin/` (extends `spikes/spin/front_back.pml`; all six request kinds + DEFERRED/ENGINE_BUSY; deadlock-freedom, no unspecified receptions, `request_eventually_answered`; `run.sh`+`run.ps1`+`RESULT.md`+`tool-versions.txt` — FR-040/DEF-A3)

**Checkpoint**: US1 = the ratified MVP cut; Anchor-A MVP-gate review fires after this story ships (T040).

## Phase 4: US2 — Snapshot and persist the engine state (P2)

**Goal**: quiescence-gated snapshot of the full FR-010 state set behind the two-backend store; engine startable `--from-snapshot`.
**Independent test**: quickstart.md "Snapshot" — snapshot, start second engine from it, state-revealing probes answer identically (SC-004).

- [X] T016 [US2] Implement `csharp/glp_engine_host/Quiescence.cs`: quiescent ⇔ goal queue empty ∧ no in-flight reduction ∧ transport drained (clarified FR-014), over `out/csharp/lib/runtime/scheduler.cs` drain state; pending-snapshot parking + DEFERRED reporting
- [X] T017 [US2] Implement `csharp/glp_engine_host/snapshot/SnapshotBlob.cs`: format_version-1 layout per contracts/snapshot-store.md (GSNP header, sections 0x01–0x09, ByteIo conventions, loud-fail on unknown tag/trailing bytes)
- [X] T018 [US2] Implement `csharp/glp_engine_host/snapshot/SnapshotCapture.cs`: capture heap verbatim (FR-011), suspended goals + per-goal tables, next `_goalId`, loaded IL units, `_waitReaders` as remaining-duration entries (FR-015), `InfrastructureGoalIds`, `GlpChannels`, link definitions + cursor positions (DEF-D1 complete set) — additive accessors only on the runtime (IV-b)
- [X] T019 [US2] Implement `csharp/glp_engine_host/snapshot/SnapshotRestore.cs`: rebuild runtime from blob, re-arm timers with remaining durations, verify section completeness before leaving `restoring` (FR-030); `--from-snapshot latest|<seq>` in `Program.cs`; ENGINE_BUSY during restore (wire rule 4)
- [X] T020 [P] [US2] Implement `csharp/glp_engine_host/store/ISnapshotStore.cs` + `FileSnapshotStore.cs`: Write/Latest/BySeq/List, atomic temp-write→fsync→rename, manifest-last, monotonic seq (FR-012/013)
- [X] T021 [US2] Implement `csharp/glp_engine_host/store/PgliteSnapshotStore.cs`: additive tables on the single repo cluster via the `csharp/glp_crdtmsg/store/PgliteOpWal.cs` access pattern (VI-b); loud fallback engagement report (US2 AS-4); shared seq namespace = max(both)+1
- [X] T022 [US2] Wire SNAPSHOT + SHUTDOWN(graceful final snapshot) request kinds through `RequestDispatcher.cs`; client `:snapshot` command in `csharp/glp_repl_client/Program.cs`
- [X] T023 [P] [US2] xUnit tests in `csharp/glp_engine_host.tests/SnapshotTests.cs`: blob round-trip `decode(encode(state))==state`, non-quiescent deferral, empty-engine snapshot/restore, in-flight-snapshot coalescing (edge cases)
- [X] T024 [P] [US2] xUnit store tests in `csharp/glp_engine_host.tests/SnapshotStoreTests.cs`: torn-write never listed (kill during Write → Latest()==previous seq), seq monotonic across backends, fallback loud-report
- [X] T024a [US2] Restore-equivalence probe test in `csharp/glp_engine_host.tests/RestoreEquivalenceTests.cs`: load programs + run goals to a known state, snapshot, boot a SECOND engine `--from-snapshot`, run the state-revealing probe set against both and assert identical answers (SC-004, US2 independent test)

**Checkpoint**: US1+US2 = durable save-points; probes identical pre/post restore.

## Phase 5: US3 — Supervised engine with crash detection and restart (P3)

**Goal**: supervisor pings, records crashes, restarts from latest snapshot, stops on unrecoverable states.
**Independent test**: quickstart.md "Supervised run" — kill engine PID; supervisor detects within ping interval, restarts, engine healthy.

- [X] T025 [US3] Implement `csharp/glp_supervisor/Supervisor.cs`: BackgroundService hosting engine child process; PING over the wire every `ping_interval`; death = exit OR ping_timeout (contracts/supervision.md); restart via `--from-snapshot latest`; backoff initial/multiplier/max (SupervisorConfig)
- [X] T026 [P] [US3] Implement `csharp/glp_supervisor/CrashLog.cs`: append-only CrashRecord persistence + `--history`/`--status` operator queries (FR-024, data-model.md CrashRecord)
- [X] T027 [US3] Implement `csharp/glp_supervisor/UnrecoverableTaxonomy.cs`: repeated_immediate_crash / corrupt_latest_snapshot (previous-seq fallback once) / store_unavailable / explicit_poison → stop restarting, persist classification, loud operator surface (FR-023, DEF-F2)
- [X] T028 [P] [US3] Write the DEF-F1 proposal memo `docs/research/repl-engine-separation/self-prove-liveness-proposal.md`: the self-prove GLP liveness goal as a language-authority proposal to Gabi — PROPOSAL ONLY, zero implementation (FR-021, §1.14)
- [X] T029 [P] [US3] xUnit tests in `csharp/glp_engine_host.tests/SupervisorTests.cs`: kill→detect→restart within ping budget, backoff progression, taxonomy stop (restart-storm edge case), crash-record completeness
- [ ] T030 [P] [US3] UPPAAL timed model in `docs/research/repl-engine-separation/models/uppaal/`: ping interval/timeout/backoff automata, detect-restart bound (SC-003); `run.ps1`+`RESULT.md`+`tool-versions.txt` (FR-040)

**Checkpoint**: unattended engine service; no silent death, no restart loops.

## Phase 6: US4 — Restore, re-establish links, resume (P4)

**Goal**: restore reloads state, re-wires links through the new adopt path, resumes drain; kill-and-restart test passes deterministically.
**Independent test**: `dotnet test csharp/glp_engine_host.tests --filter KillAndRestart` (FR-033).

- [X] T031 [US4] Implement `csharp/glp_link/primitives/RewireHandle.cs` (NEW, additive — DEF-E1): adopt restored possibly-bound In/Out/Faults cells, register idempotently via `LinkRegistry.GetOrEstablish`, wire cursors at restored positions, arm drainer/pump; the `LinkEstablish.cs:38-43` unbound guards stay untouched for the normal path
- [X] T032 [US4] Restore-order gating in `csharp/glp_engine_host/snapshot/SnapshotRestore.cs`: persistent constructs → links re-established from definitions (peer-unreachable ⇒ local work proceeds, link drain waits — edge case) → cursors re-wired → drain resumes; discard post-snapshot in-flight work (at-most-once, FR-032)
- [X] T033 [P] [US4] xUnit tests in `csharp/glp_engine_host.tests/RewireTests.cs`: adopt pre-bound cells (the post-restore state that aborts `WireEstablishedLink` today), idempotent re-registration, cursor-position resume, and mid-restore client behaviour — only STATUS/PING answered, ENGINE_BUSY for the rest until restore completes (wire rule 4, spec edge case)
- [X] T034 [US4] Kill-and-restart correctness test in `csharp/glp_engine_host.tests/KillAndRestartTests.cs`: stream committed results to a peer over an established link, kill engine mid-stream, supervisor restarts, assert peer-observable committed stream ≡ uninterrupted run (no loss/duplication of committed work; in-flight = transport failure + resubmit) — FR-033/SC-002, deterministic
- [X] T035 [P] [US4] TLA+ model in `docs/research/repl-engine-separation/models/tla/`: crash/restore/resume state machine, at-most-once committed-stream consistency over all crash points (FR-040); `run.ps1`+`RESULT.md`+`tool-versions.txt`

**Checkpoint**: full wave headline outcome — split + snapshot + supervision + restore-resume all green.

## Phase 7: Polish & Cross-Cutting

- [X] T036 [P] Produce the R8 metric tables (name | kind | tool | threshold) for the four seeds in `specs/061-wave-2-consolidated-repl-engine-split-spine/metrics.md` with the R14 protocol-verification row mandatory (FR-041)
- [X] T037 [P] Machine-check scan: 0 `skipSRSW`, 0 `OPENAI_API_KEY`/`litellm`/`openai` in all 061 artifacts + new code (Constitution III/V); record in metrics.md
- [X] T038 Re-run full suites (`bash test/run_all_tests.sh` + all `dotnet test`) and diff against T005 baseline — zero regression (SC-005, Constitution VII)
- [X] T039 Update `docs/research/repl-engine-separation/reconciliation/DEFERRALS.md` statuses: DEF-A3 → done(→061), DEF-D1/D2 → done(→061), DEF-E1/E2 → done(→061), DEF-F2 → done(→061); DEF-F1 stays open (proposal delivered, approval pending)
- [X] T040 Run the Anchor-A MVP-gate review (re-read Deferral Register Anchor A + full rescan) after US1 ships; record outcomes in `specs/061-wave-2-consolidated-repl-engine-split-spine/mvp-gate-review.md` (FR-043)
- [X] T041 At wave close: advance the four consolidated roadmap features (`repl-engine-process-split-mvp`, `engine-state-snapshot-and-persistence-api`, `liveness-crash-restart-host`, `restore-and-resume-with-link-reestablish`) shipped→closed with evidence refs (FR-043); quickstart.md verified end-to-end

## Dependencies

- Phase 1 → Phase 2 → all stories. US1 (P3) blocks US2 (needs the wire + dispatcher). US2 blocks US3 (restart needs snapshots) and US4 (restore needs blob+store). US3 blocks US4's kill-and-restart test (needs the supervisor), but T031/T033/T035 can start after US2.
- Story order = priority order: US1 → US2 → US3 → US4. Polish last (T040 fires as soon as US1 ships).

## Parallel Examples

- Setup: T002, T003, T004 in parallel after T001.
- US1: T012 (client), T013 (tests), T015 (SPIN) parallel to each other once T009–T011 land.
- US2: T020 (file store) parallel with T017/T018; T023/T024 parallel after their targets.
- US3: T026, T028, T029, T030 all parallel around T025/T027.
- US4: T033/T035 parallel after T031/T032.

## Implementation Strategy

MVP = Phase 1+2+US1 (the ratified smallest end-to-end split) — ship-reviewable at the US1 checkpoint (Anchor A). Then US2 → US3 → US4 as independent increments, each with its own green-suite checkpoint and marathon step checkpoint; wave close runs T038–T041.
