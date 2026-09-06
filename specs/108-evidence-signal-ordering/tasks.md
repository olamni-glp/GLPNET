<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: Evidence-signal ordering (feature 108)

**Feature**: `108-evidence-signal-ordering` · **Branch**: `108-evidence-signal-ordering`
**Spec**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md)

**Tests are REQUIRED for this feature and are not optional.** The whole subject is that an unproven
check is not evidence (FR-016), and a check never shown capable of failing scores zero (FR-018a).
A test-free implementation of this feature would be self-refuting.

---

## Phase 1 — Setup

- [ ] T001 Create the manifest directory and an empty, schema-valid manifest at `.specify/evidence-signals/manifest.json` (`version: 1`, `lane: "olamnit-glpnet"`, `surfaces: []`)
- [ ] T002 [P] Add `.specify/evidence-signals/report.json` and `.specify/evidence-signals/receipt-*.json` to `.gitignore` — the report is a run artefact, the manifest is the checked-in truth
- [ ] T003 [P] Create `scripts/tests/__init__.py` so the new pytest module is importable alongside the existing `scripts/tests/` directory

## Phase 2 — Foundational (blocks every user story)

- [ ] T004 Implement manifest load + schema validation in `scripts/evidence_signal_audit.py`, refusing with exit 2 and naming the offending field — never defaulting, never skipping (contracts/audit-cli.md)
- [ ] T005 Implement the FR-004-without-`negative_control` refusal in `scripts/evidence_signal_audit.py` (exit 2): a contention claim with no way to be wrong is worse than an absent entry
- [ ] T006 Implement region walking with explicit `regions_examined` / `regions_unexamined` accounting in `scripts/evidence_signal_audit.py`, so an unreadable region is recorded and exits 4, never dropped from the denominator (FR-020)
- [ ] T007 Implement the exit-code contract in `scripts/evidence_signal_audit.py` — 0 clean, 1 non-conforming/unproven, 2 usage, 3 manifest/scan disagreement, 4 unexamined region — and the not-a-terminal stderr reminder that piping replaces `$?`
- [ ] T008 Implement the feature-078-conforming receipt writer in `scripts/evidence_signal_audit.py`, recording the repo root **as resolved**, manifest sha256, examined/skipped counts with reasons, outcome and timestamp (FR-017)
- [ ] T009 [P] Write `scripts/tests/test_evidence_signal_audit.py` covering T004–T008: each refusal path, each exit code, and a receipt-shape assertion

## Phase 3 — User Story 1: a wait means the work happened (P1)

**Goal**: no caller can observe completion for work that has been accepted but not begun.
**Independent test**: drive the surface under contention 40× and observe a correct result 40/40, with the harness demonstrated to fail against the pre-fix behaviour.

- [ ] T010 [P] [US1] Implement the `wait` / `idle-predicate` scan patterns in `scripts/evidence_signal_audit.py` across C#, Dart, Python and Bash (`WaitForIdle`, `WaitFor*`, `*Idle`, `IsIdle`, `drain`, `quiesce`, `join`)
- [ ] T011 [US1] Declare `hook-notifier-wait-for-idle` in `.specify/evidence-signals/manifest.json` with `governed_by: ["FR-004"]`, `iterations: 40`, `contention: "concurrent enqueue during drain"`, `owner: "olamnit-glpnet"`, `disposition: "owned"`
- [ ] T012 [US1] Add the 40-iteration contention conformance test for `HookNotifier.WaitForIdle` in `csharp/ynet_transport.tests/`, asserting a correct result on all 40 iterations (FR-004, FR-005)
- [ ] T013 [US1] Add the **negative control** for T012 — a test that reproduces the pre-fix ordering (signal observable between accept and begin) and asserts the harness FAILS it (FR-018a); without this T012 scores zero
- [ ] T014 [US1] Wire `conformance_check` and `negative_control` for `hook-notifier-wait-for-idle` in the manifest, and re-run the audit to confirm the surface moves `unproven → conforming`
- [ ] T015 [P] [US1] Add `scripts/tests/test_evidence_signal_conformance.py::test_wait_class_negative_control_fails` — a Python-side early-wait simulator plus its negative control, so the mechanism is covered even in lanes with no C# (FR-018a)

## Phase 4 — User Story 2: "did not run" is distinguishable from "ran and found nothing" (P1)

**Goal**: no consumer classifies a did-not-run or a refusal as success.
**Independent test**: inject a did-not-run and a refused condition into each declared consumer; both are classified non-success and named.

- [ ] T016 [P] [US2] Implement the `exit-status` / `emptiness` scan patterns in `scripts/evidence_signal_audit.py` (`$?`, `returncode`, `ExitCode`, `check_call`, `exit 0`, `len(...) == 0` used as a verdict)
- [ ] T017 [US2] Implement the FR-007 five-way outcome classifier (RAN-AND-COMPLETE / RAN-AND-EMPTY / DID-NOT-RUN / REFUSED / INDETERMINATE) as a reusable helper in `scripts/evidence_signal_audit.py`
- [ ] T018 [US2] Implement the FR-010 size-as-evidence detector in `scripts/evidence_signal_audit.py` — a consumer asserting on output length, byte count or elapsed time is reported **non-conforming**, citing measured instance 6 (116 KB, exit 0, zero review)
- [ ] T019 [US2] Declare the `codex exec` review wrapper and the `buildkit-scheduler reject` consumer in the manifest with `governed_by: ["FR-007"]` and honest dispositions
- [ ] T020 [P] [US2] Add `scripts/tests/test_evidence_signal_conformance.py::test_did_not_run_is_not_success` — fault-injects a tool that exits 0 having done nothing; asserts DID-NOT-RUN and that the classifier names it
- [ ] T021 [P] [US2] Add `::test_refusal_is_not_success` — fault-injects a refusal returning exit 0; asserts REFUSED and that it is named (measured instance 4)
- [ ] T022 [P] [US2] Add `::test_size_is_not_evidence` — a 116 KB output containing no findings section must classify DID-NOT-RUN, and the negative control asserts a byte-threshold check would have passed it (measured instance 6)

## Phase 5 — User Story 3: completion survives a restart (P2)

**Goal**: a completion signal reports the same completion after the reporting component restarts.
**Independent test**: observe → restart → re-observe; the two observations must agree.

- [ ] T023 [P] [US3] Implement the `liveness-flag` scan patterns in `scripts/evidence_signal_audit.py` (`*_met`, `is_healthy`, `listening`, `acknowledged`, `pending_*`)
- [ ] T024 [US3] Declare `ynet-client-alert-acknowledged` in the manifest with `governed_by: ["FR-012"]`, `owner: "ariellas-qhstate"`, `disposition: "disclosed"`, citing spec instance 8 and `research.md` §1
- [ ] T025 [US3] Add the failing conformance test in `csharp/ynet_client.tests/` that names the WAL-replay-clobbers-ack defect: ack → restart → re-observe, asserting `acknowledged` stays true and `arrived_utc` is unchanged. **It is expected to fail; it is the disclosure mechanism, not a workaround** (Constitution II)
- [ ] T026 [US3] Declare `ynet-client-doctor-pending-alerts` in the manifest — `doctor.pending_alerts` counts alert *files* while `alerts` counts unacknowledged *records*, so two observers of one state disagree (FR-013); `owner: "ariellas-qhstate"`, `disposition: "disclosed"`
- [ ] T027 [P] [US3] Add `scripts/tests/test_evidence_signal_conformance.py::test_durability_observe_restart_reobserve` — a Python-side durable-flag simulator with a replay path that clobbers, plus its negative control

## Phase 6 — User Story 4: a lane can find its own instances (P2)

**Goal**: the enumeration is mechanical and its two sources must agree.
**Independent test**: run the audit against this lane; every manifest surface is classified and every disagreement is an error.

- [ ] T028 [US4] Implement the bidirectional manifest/scan cross-check in `scripts/evidence_signal_audit.py` — `scan_only` and `manifest_only` both populate and both force exit 3 (FR-014b)
- [ ] T029 [US4] Implement `ConformanceReport` emission (JSON + human summary) in `scripts/evidence_signal_audit.py` per `contracts/conformance-report.schema.json`, with `failed_frs` and `consumers` on every non-conforming or unproven verdict (FR-019)
- [ ] T030 [US4] Run the audit against this repo and complete the manifest until `scan_only` and `manifest_only` are both empty; record each surface's honest classification — **do not silence a hit by deleting the pattern**
- [ ] T031 [P] [US4] Add `scripts/tests/test_evidence_signal_audit.py::test_scan_only_hit_is_error` and `::test_manifest_only_entry_is_error` — the cross-check's own negative controls

## Phase 7 — Polish & cross-cutting

- [ ] T032 [P] Write `docs/evidence-signal-invariant.md` — the published invariant for fleet adoption, cross-referenced to 078 in both directions and to nothing else
- [ ] T033 [P] Add the 078 → 108 back-reference line to `specs/078-verification-receipts/spec.md` as documentation only; **no requirement of 078 changes** (`Q-olg15-09`)
- [ ] T034 Wire `scripts/evidence_signal_audit.py` into `test/run_all_tests.sh` as a new section, guarded with `set +e` so a failure reports rather than aborting the suite — Section T's missing guard has already aborted the full suite on this host
- [ ] T035 [P] Record the eight measured instances and their dispositions in `docs/known-issues.md`, each with its owner and whether it is fixed, disclosed, or not-reproduced-on-this-build (SC-001)
- [ ] T036 Run the full baseline suite (`bash test/run_all_tests.sh`) and the C# transport + client suites; confirm no regression against the recorded baseline before ship

---

## Dependencies

```
Phase 1 (T001-T003)
      ↓
Phase 2 (T004-T009)  ← blocks everything; the audit skeleton and its refusals
      ↓
  ┌───┴───────────────┬───────────────┬───────────────┐
  ↓                   ↓               ↓               ↓
US1 (T010-T015)   US2 (T016-T022)  US3 (T023-T027)  US4 (T028-T031)
  P1                  P1              P2              P2
  └───────────────────┴───────────────┴───────────────┘
                      ↓
              Phase 7 (T032-T036)
```

- **US1, US2, US3 are independent of each other** and may be built in any order once Phase 2 lands.
- **US4 depends on the scan patterns from US1/US2/US3** (T010, T016, T023) because the cross-check
  has nothing to cross-check without them. T028–T029 can be written first; T030 cannot be completed
  until the patterns exist.
- **T014 depends on T012 + T013** — the manifest may not claim a check that is not yet demonstrated
  to be capable of failing.

## Parallel execution

Within Phase 2: `T009` runs alongside T004–T008 authoring.
Within US1: `T010` and `T015` are parallel; `T011 → T012 → T013 → T014` is a strict chain.
Within US2: `T020`, `T021`, `T022` are fully parallel once `T017` lands.
Within US3: `T023` and `T027` are parallel; `T024 → T025` and `T026` are independent chains.
Phase 7: `T032`, `T033`, `T035` are parallel; `T034` then `T036` are sequential and last.

## Implementation strategy

**MVP = Phase 1 + Phase 2 + US1.** That alone delivers a runnable audit, a declared manifest, and
one surface moved from unproven to conforming with a demonstrated negative control — a complete,
independently valuable slice.

**Second increment = US2**, because it covers the highest-consequence measured instance (a false
green on a security review) and needs no C# at all.

**US3 is disclosure, not repair.** T025 and T026 land tests that are expected to fail against a
component this lane does not own. That is deliberate and is the Bug-Protocol's reporting mechanism
(Constitution II); silently passing them, or patching the peer's client, are both violations.

**US4 last**, because the cross-check is only meaningful once the patterns it cross-checks exist.

## Format validation

All 36 tasks carry a checkbox, a sequential `T0NN` id, a `[P]` marker where and only where the task
touches a distinct file with no incomplete dependency, a `[US1]`–`[US4]` label on every user-story
task and on no setup/foundational/polish task, and an explicit file path.
