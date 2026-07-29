# Tasks: Wave 5 consolidated: captured triad

**Input**: Design documents from `/specs/063-wave-5-consolidated-captured-triad/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included where the spec makes them the acceptance instrument (the
US1 regression/re-verify gates FR-003/FR-004 and the US2 drill SC-004); other
work follows the existing suite conventions.

**Organization**: By user story, in spec priority order. FR-015: no task below
consumes wave-4 output (research R10); if one emerges it is appended LAST and
board-flagged before work starts.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 Scaffold the `ms_message/` Python package (pyproject.toml, src/ms_message/, tests/) mirroring the glp_quick layout, per plan.md structure
- [X] T002 [P] Scaffold `.claude/skills/ms-message/SKILL.md` (originator/recipient/status/dlq command surface per contracts/mesh-messaging-protocol.md)
- [X] T003 [P] Create `docs/three-role-orchestration/` with an empty engagements/ dir and a stub PROTOCOL.md header

## Phase 2: Foundational (blocking prerequisites)

- [X] T004 Verify `csharp/glp_quick_host` + `glp_quick` build/run baseline on this tree (dotnet build + pytest) and record the pre-change verdict counts in specs/063-wave-5-consolidated-captured-triad/baseline.md
- [X] T005 Add additive migration `codeconv/src/codeconv/db/migrations/versions/0011_msmesh_schema.py` (tables per data-model.md: station, mailbox, message, delivery_position, dlq, gap_event) + single-head test `codeconv/tests/test_migration_0011_single_head.py` (heads == [0011])
  — DEVIATION (recorded): feature 035 (`0011_enrich_provenance`) had already advanced the head to 0011 before this wave, so the msmesh migration landed as `0012_msmesh_schema.py` (head 0011→0012) with authoritative test `test_migration_0012_single_head.py` (heads == [0012]); the 0011 test was repurposed to the interior-link assertion, the same convention 035 applied to 0010's. The task's binding intent — additive, single linear head (Constitution VI-a) — is unchanged.
- [X] T006 [P] Implement `ms_message/src/ms_message/protocol.py` — signal/fetch/fetch_batch/friend_lookup/friend_reply message shapes (contracts/mesh-messaging-protocol.md), transport-agnostic payload encode/decode

## Phase 3: User Story 1 — QUIC+WS link completion (P1) — MVP

**Goal**: live REPL over the link; mesh dup-id defect proven fixed; suite reproducible.
**Independent test**: quickstart.md US1 — two REPLs linked, goal evaluated cross-host; `mesh_dup_id` scenario green; 0 dll-skips.

- [ ] T007 [US1] In-tree build path for the C# host library the 9 skipped integration tests load: wire `csharp/glp_quick_host` output into `out/csharp/` (dotnet build) and satisfy the tests' load condition (remove an explicit skip attribute ONLY if one is hard-coded; a dll-presence gate resolves by the build alone) (audit gap (c), contract C3)
- [ ] T008 [US1] Run the un-skipped integration suite; per Bug-Protocol report (not fix) any failure the dll exposes; record verdicts in baseline.md (contract C3)
- [ ] T009 [US1] Write the `mesh_dup_id` regression scenario in `csharp/glp_link.tests/MeshDupIdRegressionTests.cs`: A/B live, C announces B's id — incumbent survives, traffic keeps flowing to B, refresh visible (contract C2; R1: the scenario decides whether the audited symptom still reproduces)
- [ ] T010 [US1] If T009 reproduces the audited defect: fix `Mesh` id learn/evict in `csharp/glp_quick_host/Program.cs` so a newcomer never evicts a live incumbent; re-run T009 green. If T009 passes as-is: record the provenance finding in baseline.md (guard already landed) and keep the scenario as the closure witness
- [ ] T011 [US1] Complete the `--repl` live bridge in `csharp/glp_quick_host/Program.cs`: spawn the REPL child, bridge stdio ⇄ link-message envelopes (one line/result per message), surface child death as a link fault (contract C1, R2)
- [ ] T012 [P] [US1] Plumb `--repl` through the Python CLI in `glp_quick/src/` (server + client roles) with the REPL exe path argument (contract C1)
- [ ] T013 [US1] End-to-end scenario: two instances, `--repl` both ends, goal evaluated remotely, result returned — scripted in `glp_quick/tests/test_repl_bridge.py`, asserting the SC-001 envelope (link-up + first result within the 5-minute wall-clock bound; scripted runs land in seconds)
- [ ] T014 [US1] Re-run the full 036 demo suite; produce the per-scenario verdict table superseding the 18/104 claim; append to baseline.md (contract C3, SC-003)
- [ ] T015 [P] [US1] Correct the stack-profile wording (relay profile relays; reference stack terminates QUIC) at its single authoritative doc site (contract C4, FR-005a)

## Phase 4: User Story 2 — durable first-hop mesh messaging (P2)

**Goal**: signal-then-fetch with WAL durability, exactly-once observation, DLQ.
**Independent test**: quickstart.md US2 drill — N=1,000 offline+restart, exactly-once, in order; unknown target → DLQ.

- [ ] T016 [P] [US2] Implement `ms_message/src/ms_message/wal.py`: append-only WAL + size-policy message files (shared/own/split per R4), replay-on-restart, dense-sequence assertion
- [ ] T017 [P] [US2] Implement `ms_message/src/ms_message/store.py`: msmesh hot-tier access via the shared `codeconv.bridge_client` (stations, mailboxes, messages, delivery_position, dlq, gap_event; R5)
- [ ] T018 [US2] Implement `ms_message/src/ms_message/dlq.py`: park-with-reason, list, re-drive (contract guarantees 5; R8)
- [ ] T019 [US2] Implement originator role in `ms_message/src/ms_message/cli.py`: accept→WAL→store→signal reachable targets; friend-lookup then DLQ for unresolvables (FR-006, FR-007, FR-011a)
- [ ] T020 [US2] Implement recipient role in `ms_message/src/ms_message/cli.py`: signal handling, resumable fetch from position, exactly-once observation via delivery_position, gap_event recording (FR-007..FR-010, R7)
- [ ] T021 [US2] Implement `ms_message/src/ms_message/lake.py`: DuckLake aging + hot∪lake catch-up query behind the seam, LOUD PGlite-only degradation (R6)
- [ ] T022 [US2] Retention sweep (ephemeral/time-windowed/permanent) in store.py + CLI `status` summary (FR-011b, contract guarantees 6)
- [ ] T023 [US2] Unit tests: WAL replay, size policy, dedup across restart, gap detection, AND the FR-011 failure paths (store unwritable / journal corrupt ⇒ explicit refusal or named fault, never silent loss) in `ms_message/tests/` (contract guarantees 1–3 + 5)
- [ ] T024 [US2] The SC-004 drill script `ms_message/tests/drill_disconnect.py` (N=1,000, recipient offline, originator restart, exactly-once in order, bounded waits) + wire into `test/run_all_tests.sh` as an explicit section or documented standalone gate (SC-004/SC-005)
- [ ] T025 [US2] QUIC-leg evidence (after US1): one drill pass with the link over QUIC+WS; TCP remains the default evidence path (R3)

## Phase 5: User Story 3 — 3-role orchestration operationalized (P3)

**Goal**: written protocol + two recorded engagements on real wave-5 gates.
**Independent test**: an engagement runs with every operator step named in PROTOCOL.md; conflicts escalate visibly.

- [ ] T026 [P] [US3] Write `docs/three-role-orchestration/PROTOCOL.md` from the recorded method doc + installed capability contract (role charters, blind-then-cross-verify, false-consensus guard, authority order, convergence caps, evidence/attribution, engineer gates; contract deliverable 1)
- [ ] T027 [US3] Engagement E1: plan-review triad over this wave's plan artifacts; record in `docs/three-role-orchestration/engagements/E1-plan-review.md` (attributed claims, critic verdicts, escalations, engineer decisions)
- [ ] T028 [US3] Engagement E2: code-review triad over the US1 completion diff; record in `docs/three-role-orchestration/engagements/E2-us1-code-review.md`
- [ ] T029 [US3] Closure evidence: link E1/E2 + PROTOCOL.md into the roadmap item (notes update), per contract deliverable 3

## Phase 6: User Story 4 — wave close roadmap advance (P4)

- [ ] T030 [US4] At wave close: advance `http3-quic-ws-link-completion`, `durable-mesh-messaging-protocol`, `three-role-agent-team-orchestration` to delivered/closed with receipts; export + publish per fleet sync protocol (FR-014, SC-007)

## Phase 7: Polish & cross-cutting

- [ ] T031 [P] Gitignore entries for `ms_message/.data/` (WAL/lake) and any new build outputs
- [ ] T032 [P] Update quickstart.md with any CLI-surface drift discovered during implementation
- [ ] T033 Full-suite re-verify on the branch (REPL suite + dotnet + pytest + the drill) and stage-seam UPDATE with verdict counts

## Dependencies

- Phase 2 blocks all stories (T004 baselines; T005/T006 block US2).
- US1 (T007–T015): T007→T008; T009→T010; T011→T012→T013; T014 after T008/T010/T013; T015 parallel.
- US2 (T016–T025): T016/T017 parallel after T005/T006; T018–T020 after T016/T017; T021/T022 after T017; T023 after T016–T020; T024 after T023; T025 after T024 AND US1 T013.
- US3: T026 parallel anytime; T027 after plan ships (now); T028 after US1 diff exists; T029 last.
- US4 T030 strictly last. Polish after all stories.

## Parallel examples

- Setup: T002 ∥ T003 while T001 runs.
- US1: T009 ∥ T011 (different files); T012/T015 ∥ most of the phase.
- US2: T016 ∥ T017; T021 ∥ T022.
- Cross-story: US3 T026 ∥ US1/US2 phases; E1 (T027) can run while US1 codes.

## Implementation strategy

MVP = Phase 1–3 (US1): the wave's headline payoff, independently shippable.
Then US2 as the second increment (TCP evidence first, QUIC leg last), US3
engagements riding the real gates as they occur, US4 + polish at close.
