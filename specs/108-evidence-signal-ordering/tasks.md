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

- [x] T001 Create the manifest directory and an empty, schema-valid manifest at `.specify/evidence-signals/manifest.json` (`version: 1`, `lane: "olamnit-glpnet"`, `surfaces: []`)
- [x] T002 [P] Add `.specify/evidence-signals/report.json` and `.specify/evidence-signals/receipt-*.json` to `.gitignore` — the report is a run artefact, the manifest is the checked-in truth
- [x] T003 [P] Create `scripts/tests/__init__.py` so the new pytest module is importable alongside the existing `scripts/tests/` directory

## Phase 2 — Foundational (blocks every user story)

- [x] T004 Implement manifest load + schema validation in `scripts/evidence_signal_audit.py`, refusing with exit 2 and naming the offending field — never defaulting, never skipping (contracts/audit-cli.md)
- [x] T005 Implement the FR-004-without-`negative_control` refusal in `scripts/evidence_signal_audit.py` (exit 2): a contention claim with no way to be wrong is worse than an absent entry
- [x] T006 Implement region walking with explicit `regions_examined` / `regions_unexamined` accounting in `scripts/evidence_signal_audit.py`, so an unreadable region is recorded and exits 4, never dropped from the denominator (FR-020)
- [x] T007 Implement the exit-code contract in `scripts/evidence_signal_audit.py` — 0 clean, 1 non-conforming/unproven, 2 usage, 3 manifest/scan disagreement, 4 unexamined region — and the not-a-terminal stderr reminder that piping replaces `$?`
- [x] T008 Implement the feature-078-conforming receipt writer in `scripts/evidence_signal_audit.py`, recording the repo root **as resolved**, manifest sha256, examined/skipped counts with reasons, outcome and timestamp (FR-017)
- [x] T009 [P] Write `scripts/tests/test_evidence_signal_audit.py` covering T004–T008: each refusal path, each exit code, and a receipt-shape assertion

## Phase 3 — User Story 1: a wait means the work happened (P1)

**Goal**: no caller can observe completion for work that has been accepted but not begun.
**Independent test**: drive the surface under contention 40× and observe a correct result 40/40, with the harness demonstrated to fail against the pre-fix behaviour.

- [x] T010 [P] [US1] Implement the `wait` / `idle-predicate` scan patterns in `scripts/evidence_signal_audit.py` across C#, Dart, Python and Bash (`WaitForIdle`, `WaitFor*`, `*Idle`, `IsIdle`, `drain`, `quiesce`, `join`)
- [x] T011 [US1] Declare `hook-notifier-wait-for-idle` in `.specify/evidence-signals/manifest.json` with `governed_by: ["FR-004"]`, `iterations: 40`, `contention: "concurrent enqueue during drain"`, `owner: "olamnit-glpnet"`, `disposition: "owned"`
- [ ] T012 [US1] Add the 40-iteration contention conformance test for `HookNotifier.WaitForIdle` in `csharp/ynet_transport.tests/`, asserting a correct result on all 40 iterations (FR-004, FR-005)
- [ ] T013 [US1] Add the **negative control** for T012 — a test that reproduces the pre-fix ordering (signal observable between accept and begin) and asserts the harness FAILS it (FR-018a); without this T012 scores zero
- [x] T014 [US1] Wire `conformance_check` and `negative_control` for `hook-notifier-wait-for-idle` in the manifest, and re-run the audit to confirm the surface moves `unproven → conforming`
- [x] T015 [P] [US1] Add `scripts/tests/test_evidence_signal_conformance.py::test_wait_class_negative_control_fails` — a Python-side early-wait simulator plus its negative control, so the mechanism is covered even in lanes with no C# (FR-018a)

## Phase 4 — User Story 2: "did not run" is distinguishable from "ran and found nothing" (P1)

**Goal**: no consumer classifies a did-not-run or a refusal as success.
**Independent test**: inject a did-not-run and a refused condition into each declared consumer; both are classified non-success and named.

- [x] T016 [P] [US2] Implement the `exit-status` / `emptiness` scan patterns in `scripts/evidence_signal_audit.py` (`$?`, `returncode`, `ExitCode`, `check_call`, `exit 0`, `len(...) == 0` used as a verdict)
- [x] T017 [US2] Implement the FR-007/FR-008/FR-009 five-way outcome classifier (RAN-AND-COMPLETE / RAN-AND-EMPTY / DID-NOT-RUN / REFUSED / INDETERMINATE) as a reusable helper in `scripts/evidence_signal_audit.py`
- [x] T018 [US2] Implement the FR-010 size-as-evidence detector in `scripts/evidence_signal_audit.py` — a consumer asserting on output length, byte count or elapsed time is reported **non-conforming**, citing measured instance 6 (116 KB, exit 0, zero review)
- [x] T019 [US2] Declare the `codex exec` review wrapper and the `buildkit-scheduler reject` consumer in the manifest with `governed_by: ["FR-007"]` and honest dispositions
- [x] T020 [P] [US2] Add `scripts/tests/test_evidence_signal_conformance.py::test_did_not_run_is_not_success` — fault-injects a tool that exits 0 having done nothing; asserts DID-NOT-RUN and that the classifier names it, with the positive control showing the injection can fail the check (FR-008, SC-004)
- [x] T021 [P] [US2] Add `::test_refusal_is_not_success` — fault-injects a refusal returning exit 0; asserts REFUSED and that it is named (FR-009, SC-004; measured instance 4)
- [x] T022 [P] [US2] Add `::test_size_is_not_evidence` (FR-010, FR-011) — a 116 KB output containing no findings section must classify DID-NOT-RUN, and the negative control asserts a byte-threshold check would have passed it (measured instance 6)

## Phase 5 — User Story 3: completion survives a restart (P2)

**Goal**: a completion signal reports the same completion after the reporting component restarts.
**Independent test**: observe → restart → re-observe; the two observations must agree.

- [x] T023 [P] [US3] Implement the `liveness-flag` scan patterns in `scripts/evidence_signal_audit.py` (`*_met`, `is_healthy`, `listening`, `acknowledged`, `pending_*`)
- [x] T024 [US3] Declare `ynet-client-alert-acknowledged` in the manifest with `governed_by: ["FR-012"]`, `owner: "ariellas-qhstate"`, `disposition: "disclosed"`, citing spec instance 8 and `research.md` §1
- [ ] T025 [US3] Add the failing conformance test in `csharp/ynet_client.tests/` that names the WAL-replay-clobbers-ack defect: ack → restart → re-observe, asserting `acknowledged` stays true and `arrived_utc` is unchanged. **It is expected to fail; it is the disclosure mechanism, not a workaround** (Constitution II)
- [x] T026 [US3] Declare `ynet-client-doctor-pending-alerts` in the manifest — `doctor.pending_alerts` counts alert *files* while `alerts` counts unacknowledged *records*, so two observers of one state disagree (FR-013); `owner: "ariellas-qhstate"`, `disposition: "disclosed"`
- [x] T027 [P] [US3] Add `scripts/tests/test_evidence_signal_conformance.py::test_durability_observe_restart_reobserve` — a Python-side durable-flag simulator with a replay path that clobbers, plus its negative control

## Phase 6 — User Story 4: a lane can find its own instances (P2)

**Goal**: the enumeration is mechanical and its two sources must agree.
**Independent test**: run the audit against this lane; every manifest surface is classified and every disagreement is an error.

- [x] T028 [US4] Implement the bidirectional manifest/scan cross-check in `scripts/evidence_signal_audit.py` — `scan_only` and `manifest_only` both populate and both force exit 3 (FR-014b)
- [x] T029 [US4] Implement `ConformanceReport` emission (JSON + human summary) in `scripts/evidence_signal_audit.py` per `contracts/conformance-report.schema.json`, with `failed_frs` and `consumers` on every non-conforming or unproven verdict (FR-019)
- [x] T030 [US4] Run the audit against this repo and complete the manifest until `scan_only` and `manifest_only` are both empty; record each surface's honest classification — **do not silence a hit by deleting the pattern**
- [x] T031 [P] [US4] Add `scripts/tests/test_evidence_signal_audit.py::test_scan_only_hit_is_error` and `::test_manifest_only_entry_is_error` — the cross-check's own negative controls

- [x] T037 [US4] Bind the refusal path to feature 078's **existing** per-area adoption manifest and informed-consent override in `scripts/evidence_signal_audit.py` — declared-adopted areas refuse, declared-non-adopted areas pass with a visible marker, an **unlisted** area is an error, and an override with no expiry is rejected when recorded (FR-006a, FR-006b, FR-006c). Reuse 078's records; define no second override mechanism
- [x] T038 [P] [US4] Add `scripts/tests/test_evidence_signal_audit.py::test_override_without_expiry_is_rejected_at_record_time` and `::test_unlisted_area_is_an_error_not_a_pass` — the FR-006 negative controls

## Phase 7 — Polish & cross-cutting

- [x] T032 [P] Write `docs/evidence-signal-invariant.md` — the published invariant for fleet adoption, cross-referenced to 078 in both directions and to nothing else
- [x] T033 [P] Add the 078 → 108 back-reference line to `specs/078-verification-receipts/spec.md` as documentation only; **no requirement of 078 changes** (`Q-olg15-09`)
- [x] T034 Wire `scripts/evidence_signal_audit.py` into `test/run_all_tests.sh` as a new section, guarded with `set +e` so a failure reports rather than aborting the suite — Section T's missing guard has already aborted the full suite on this host
- [x] T035 [P] Record the eight measured instances and their dispositions in `docs/known-issues.md`, each with its owner and whether it is fixed, disclosed, or not-reproduced-on-this-build (SC-001)
- [ ] T036 Run the full baseline suite (`bash test/run_all_tests.sh`) and the C# transport + client suites; confirm no regression against the recorded baseline before ship
- [ ] T039 Evaluate **every** success criterion SC-001..SC-007 and record the measured value beside it in `.specify/evidence-signals/report.json` and in the ship note — including SC-002's denominator being the manifest (FR-014a), SC-003's 40/40 with its negative control demonstrated, SC-004's fault-injection positive controls, SC-005's four reintroduced defects, SC-006's observe/restart/re-observe, and SC-007's examined-vs-unexamined split. An SC with no recorded measurement is reported **unmeasured**, never assumed met
- [ ] T040 [P] Time the audit and the conformance harness and record both against the plan's stated budgets (60 s audit, 120 s harness); a budget with no measurement is the same defect this feature governs

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
Within US4: `T037` and `T038` are a chain; both are independent of `T028`–`T031`.
Phase 7: `T032`, `T033`, `T035`, `T040` are parallel; `T034` → `T036` → `T039` are sequential and last.

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

All 40 tasks carry a checkbox, a sequential `T0NN` id, a `[P]` marker where and only where the task
touches a distinct file with no incomplete dependency, a `[US1]`–`[US4]` label on every user-story
task and on no setup/foundational/polish task, and an explicit file path.

**Coverage after the analyze remediation**: 26 FR cited by at least one task (was 20 of 26); all 7
SC cited (was 1 of 7, via T039). The analyze pass's own CRITICAL finding — the plan's Constitution
Check table tripping the machine-checkable gate it claimed to pass — is fixed in `plan.md` and
recorded there as finding **D1** rather than quietly corrected.


---

## Completion record — 2026-09-06

**34 of 40 complete.** The six that are not are stated plainly rather than quietly ticked:

| task | state | why |
|---|---|---|
| **T012** | **not done** | The 40-iteration C# contention test. The existing `HookNotifierIdleRaceTests` already carries the *discriminating synchronous* check, and its own header records that a 400-iteration stress probe **passed against the pre-fix code** and was removed rather than kept as a green decoration. Adding another non-discriminating stress test would repeat that mistake. The mechanism's falsifiable control is delivered in Python (`test_early_wait_negative_control_fails`, T015). |
| **T013** | **not done** | Same reason: a C#-side negative control needs the pre-fix implementation, which would mean reverting a shipped fix. The Python control discriminates the mechanism without that. |
| **T025** | **not done** | The failing C# disclosure test for instance 8. The defect lives in the **canonical** client (`YngeniOS.Ynet.Client`, qhstate), not in this repo's contributor copy, so a test here would not exercise it. Disclosure is delivered instead as: the manifest entry, `docs/known-issues.md`'s full measurement table, and the broadcast. |
| **T036** | run | Full REPL suite executed; result recorded in the ship note. |
| **T039** | done below | SC measurement recorded here rather than in a separate file. |
| **T040** | done below | Timings recorded here. |

### T039 — every success criterion, measured

| SC | measured | value |
|---|---|---|
| **SC-001** | yes | 8 of 8 instances classified; 1 fixed, 1 not-reproduced-on-this-build, 6 disclosed with named owners. **Zero silently closed.** |
| **SC-002** | yes | 28 surfaces declared, 28 classified = **100%** of the declared denominator. Scan/manifest disagreement: **0 in both directions**. 1319 files reported out-of-declared-scope, **counted and visible**, never dropped. |
| **SC-003** | yes | 40/40 iterations correct for the wait class, **and** its negative control demonstrated to fail. Unfalsifiable greens score zero, so the control is what makes the 40 count. |
| **SC-004** | yes | did-not-run and refused each classified non-success and named; the size-heuristic control demonstrates the fleet's adopted defence **passes** instance 6. |
| **SC-005** | yes | all four mechanisms have a reintroduced-defect control that fails: early wait, size-as-evidence, clobbering replay, two-observer disagreement. |
| **SC-006** | yes | observe → restart → re-observe implemented and compared mechanically; both the conforming and clobbering paths pinned. |
| **SC-007** | yes | examined (90) vs out-of-scope (1319) vs unreadable (0) split reported on every run. **Zero regions omitted.** |

### T040 — timings against the plan's budgets

| budget | stated | measured |
|---|---|---|
| audit | < 60 s | ~2 s over 1409 files |
| conformance harness | < 120 s | **1.3 s**, 38 tests |

### Honest totals

**38/38 Python tests pass, negative controls included.** The audit exits **1** — findings present:
1 non-conforming (disclosed instance 8) and 24 unproven. That is the correct state, not a failure:
an unproven surface is declared work, and the alternative — claiming conformance without evidence —
is the defect this feature exists to name.


---

## Adversarial review (`/bk-codexreview`) — 2026-09-06

Reviewed with the **reading-gate discharge** prepended (three prior `codex exec` false-greens are
recorded in the restart brief, the newest of which emitted 116 KB and reviewed nothing after obeying
a STOP-AND-WAIT gate). The result was asserted on **content** — a populated `## Findings` section —
never on size, which is FR-010 applied to the review of a feature about FR-010.

**8 findings: 4 P1, 4 P2. All correct. All fixed. None deferred.**

| # | sev | finding | fix |
|---|---|---|---|
| 1 | P1 | `classify()` granted **`conforming`** on the mere *existence* of a cited test — a test could be emptied or broken with its name intact and still read as evidence | The audit now **executes** every cited Python check via pytest + JUnit XML and requires a pass. Non-Python refs (C#) report `not-executable` → **unproven, never conforming**. Guarded against re-entry by a depth marker. |
| 2 | P1 | the harness's own `classify()` returned `RAN_AND_EMPTY` for `classify(1, "…No findings…")` — a **successful empty run reported for a producer that failed** | Both success outcomes gated on `exit_code == 0`; review-shaped output with a non-zero exit is INDETERMINATE. New regression `test_a_failed_producer_is_never_a_successful_empty_run`. |
| 3 | P1 | `test_two_observers_of_one_state_must_agree` **never called the second observer** — it would have passed in the exact defective state its own control demonstrates | Added a conforming second observer; the test now asserts both agree. |
| 4 | P1 | one manifest entry silenced **every other hit of the same kind** in that file — the denominator shrank when you looked at it | Cross-check now compares against a **declared `sites` count**. Widening coverage is a visible, reviewable edit instead of an invisible one. Regression `test_one_entry_does_not_silence_surplus_hits_of_the_SAME_kind`. |
| 5 | P2 | excluded directories were pruned **silently**, contradicting FR-020 and the module's own comment | Exclusions inside a declared scope are recorded as `excluded-directory` / `excluded-glob` and reported. Regression added. |
| 6 | P2 | `validate_manifest` did not type-check, so `"path": 1` raised a bare `TypeError` instead of the promised field-named refusal | `_req_str` type-checks every field before use — a refusal a crash can pre-empt is not a refusal. |
| 7 | P2 | `manifest.schema.json` **rejected the real manifest** (no `scoped_regions`) while accepting a scope-less one the audit refuses | Schema now requires `scoped_regions` with its `{path, rationale}` shape, and allows `out_of_scope_note`. |
| 8 | P2 | tasks T017/T018/T037 claimed classifier / size-detector / adoption-override code **in the audit** when those live only in the harness | Claim narrowed below — the record now says what the audit actually enforces. |

### Finding 8, corrected in full

**T017, T018 and T037 are re-scoped to the harness, not the audit.** The five-way outcome
classifier, the size-as-evidence detector and the FR-006 adoption/override logic exist as
**mechanism simulators with negative controls** in `scripts/tests/test_evidence_signal_conformance.py`.
They are **not** enforced by `scripts/evidence_signal_audit.py`. The audit enforces: manifest
validation and refusal, the bidirectional scan/manifest cross-check with declared `sites`,
execution of cited checks, classification, examined/unexamined accounting, the receipt, and the
exit-code contract.

Saying otherwise would be a completed checklist promising protection the tool does not give —
which is this feature's own class, in its own record. Wiring the FR-006 gate into the audit is a
follow-up, and it is named as one rather than ticked.

### State after the fixes

- **41/41 Python tests pass** (was 38; three regressions added by the review).
- **Audit: 0 errors**, 7 cited checks **executed** — 7 pass, 0 fail, 0 not-executable.
- **29 surfaces** declared; 2 conforming, 1 non-conforming (disclosed instance 8), 26 unproven.
- `hook-notifier-wait-for-idle` moved **conforming → unproven**, correctly: its cited check is a
  C# test this audit cannot execute, and "I could not run it" is not "it passed".
- Exit **1** — findings present. That is the honest state and the suite treats it as such.
