# US1 final parity + gate audit (T013)

**Date**: 2026-07-09 · **Host**: gavri · **Commits audited**: feature history through `06aaec6e`

## SC-003 — 100% guard-vs-matcher parity on shared vectors
- **Criterion**: guard decisions agree with the shipped C# PolicyMatcher on every
  `guard_only=false` vector (deliver ↔ success, no_route ↔ fail); Suspend is guard-only.
- **Evidence**: both sides assert against the SAME SSOT `contracts/vectors.json`.
  Matcher side: `csharp/glp_crdtmsg.tests/PolicyVectorParityTests.cs` green vs `expected_matcher`
  (124/124 at T006 commit `13ec67c4`; re-confirmed in the T030 final baselines —
  `evidence/final-baselines.md`). Guard side: 12/12 vs `expected_guard` under BOTH forms
  (`form-a.md`, `form-b.md`). For every shared vector `expected_matcher`/`expected_guard` encode
  the deliver↔success, no_route↔fail correspondence, so green-on-both ⇒ 100% agreement:
  wx1/v05/v07/v08 deliver=success; wx2/wx3/v06/v09/v10 no_route=fail. Zero silent fallbacks or
  silent drops observed in any test (suspend cases print `→ suspended`, fail cases `→ failed`).
- **Verdict**: PASS

## FR-006 — shipped matcher untouched
- **Criterion**: `csharp/glp_crdtmsg/route/PolicyMatcher.cs` MUST NOT be modified by this feature.
- **Evidence**: `git diff origin/develop...HEAD -- csharp/glp_crdtmsg/route/PolicyMatcher.cs`
  → EMPTY; `git log --follow` shows its last change is feature 041's `99b7aaff`.
- **Verdict**: PASS

## SC-001 — zero guard code preceding the T003 addendum
- **Criterion**: feature history shows no guard implementation/compile/run event before the
  §1.14 realization + vector addenda were recorded in spec.md Clarifications.
- **Evidence**: `git merge-base --is-ancestor` confirms `3767b082` (realization addendum) and
  `ab0e46b7` (vector addendum) both PRECEDE `13ec67c4` (first guard sources, T005–T008); the
  guard first COMPILED/RAN only at the (a1) seat `7884fbbb` (2026-07-09, this session) — later
  still. The T007-era sources carry the explicit "expected not to load until T009" marker.
- **Verdict**: PASS

## Gate summary
All three audit criteria PASS. Form (a) reference behavior is preserved and permanently
regression-covered in `test/run_all_tests.sh` (A29 form-(a) env reference + form-(b) defaults +
A30 pure-(b) probe).
