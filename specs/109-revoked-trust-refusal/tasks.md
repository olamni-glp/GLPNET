# Tasks: Revoked trust material is refused at load

**Spec**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md)

## Phase 1 — Baseline (before any change)

- [x] T001 Confirm the working tree is clean and the branch is `109-revoked-trust-refusal`.
- [x] T002 Record the pre-change green baseline: `dotnet test csharp/glp_link.tests` (or the
      solution's link-test project) passes. A change without a baseline cannot be attributed.

## Phase 2 — The guard (FR-001..FR-008)

- [x] T003 Add `CurrentPin` and `RevokedPins` as compiled-in constants on `SharedCertMaterial`
      (FR-002). No file read, no environment variable.
- [x] T004 Add the **revoked** check after the consistency check, before `return` (FR-001, FR-003).
      Message names the pin, the rule, and the remedy — *obtain current material from a peer host;
      do NOT check it out of git history* (FR-005).
- [x] T005 Add the **not-current-generation** check immediately after T004, with a distinct message
      naming its own rule (FR-004, FR-005).
- [x] T006 Verify both throw on the existing fail-closed path — no degraded mode, no warning-and-
      continue (FR-006).
- [x] T007 Confirm FR-008 holds via the existing `.Trim()`; do not re-implement it.

## Phase 3 — Controls (FR-009 / SC-004) — *both required; either alone is insufficient*

- [x] T008 New `csharp/glp_link.tests/SharedCertMaterialGenerationTests.cs`.
- [x] T009 **Positive control** `RevokedPin_IsRefused` — the guard FIRES.
- [x] T010 **Negative control** `CurrentPin_IsAccepted` — the guard does NOT over-fire.
- [x] T011 `RevokedPin_MessageNamesPinRuleAndRemedy` (SC-003 — all three strings present).
- [x] T012 `UnknownGeneration_IsRefusedAsNotCurrent` (FR-004).
- [x] T013 `RevokedBeatsNotCurrent_OrderIsPinned` (FR-007 ordering).
- [x] T014 `ExistingChecks_StillFireFirst_MissingPfx` + `_MissingFingerprint` — the pre-existing
      checks still fire, and still fire FIRST, and are NOT reported as generation problems
      (FR-007 regression). *Initially marked done in error before the test existed; caught on
      review and written properly — 9 tests, not 7.*
- [x] T015 `TrailingNewline_DoesNotEvadeTheGuard` (FR-008).
- [x] T016 Fixtures generated in-test; **no key material is committed** (FR-010).

## Phase 4 — Prove it, don't assert it

- [x] T017 Run the new tests: all green (SC-001, SC-004).
- [x] T018 **Mutation check**: neuter the guard (make the revoked check a no-op) and confirm
      `RevokedPin_IsRefused` FAILS. A control that cannot fail is not a control. Restore after.
- [x] T019 **Inverse mutation**: set `CurrentPin` to the revoked value and confirm
      `CurrentPin_IsAccepted` FAILS — proves the negative control is live, and is the exact guard
      against the "guard's own constant is wrong" edge case.
- [x] T020 Run the 064 service-box drills (`resume_drill.sh`, `history_drill.sh`) — they load real
      material through this path and are the true SC-002 negative control. Expect 7/7 and 4/4.
- [x] T021 Run the full repo suite `bash test/run_all_tests.sh` — expect 595/595, no new failures.

## Phase 5 — Close out

- [x] T022 `/bk-codexreview` — adversarial review; fix findings.
- [ ] T023 `/bk-ship` — full GitFlow.
- [ ] T024 `/bk-close` + marathon discharge + slot release + branch tidy.

## Notes

- **T018 and T019 are not optional.** The repo's recorded history (wave-26, wave-28) is that a
  green self-written suite is not evidence: nine guard suites were green while six false-green
  holes existed, and mutation testing found the *tests* wrong roughly eight times. A guard nobody
  has watched fail is a guard nobody has tested.
- T020 is the strongest check here and it is not self-written — it is an existing drill that
  exercises the real load path with real material.

## Phase 6 — codexreview remediation (2 cycles, 6 findings, all real)

- [x] T025 Cycle 1 [P1] glp_quick_host bypassed the guard entirely — guarded after the branch.
- [x] T026 Cycle 1 [P1] my change broke QuicRegistrationTests; I compared failure COUNTS not SETS.
- [x] T027 Cycle 2 [P1] gleam_quic/src/glpq_quic.erl is a THIRD seam — guarded, 3 controls green.
- [x] T028 Cycle 2 [P1] FR-004 broke  + 6 test files → **engineer ruling G-05**:
      revoked list unconditional on every path; current-generation assertion only on the walk-up
      SHARED material. Load(dir) defaults revoked-only; LoadFromRepo() opts into both.
- [x] T029 Cycle 2 [P2] CurrentPin_IsAccepted was TAUTOLOGICAL — passing the production constant into
      a comparison against itself. Proven: an arbitrary wrong non-revoked constant left all 9 tests
      GREEN. Fixed with an independently specified ExpectedGen3Pin; the same mutation now fails 2
      tests, and on cert-less CI too.
- [x] T030 Cycle 2 [P2] plan.md 'every consumer goes through Load' was FALSE — corrected in the plan
      with all three seams enumerated.
