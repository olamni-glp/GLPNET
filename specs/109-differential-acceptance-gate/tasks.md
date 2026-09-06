<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: 109 — Differential acceptance, an enforcing gate, and an honest denominator

**Ordering rule.** US3 first: it is self-contained, it produces the measurable improvement the era
was ruled for, and it does not depend on the other two. US2 second (it needs Piece A). US1 last (it
is the largest and its participants may be unavailable, which must not block the other two).

Legend — `[ ]` open · `[x]` done · `[!]` blocked, with the blocker named.

---

## Phase 0 — Baseline (mandatory, before any change)

- [x] **T001** Record the audit baseline bare (never through a pipe; `$?` of a pipe is the pipe's).
      → exit 1 · 91 examined · 1329 boundary · 7 checks · 2 conforming / 1 non-conforming / 26 unproven / 0 errors.
- [x] **T002** Record the suite baseline from feature 108's close → 595/595 executed, 0 failures, 2 named not-run.
- [x] **T003** Measure the blind spots so the fix has a number to beat:
      `.gleam` 223 · `.glp` 1416 · `.mjs` 12 files never opened; `test/run_all_tests.sh` → **0** hits
      against ≥ 6 real two-step decision sites; `codeconv/tests` 387 sites (all exit-status),
      `codeconv/src` 11, `csharp` 79.

## Phase 1 — US3: the denominator (P1)

### Tests first
- [ ] **T010** Test: a region containing an unscannable suffix reports a non-zero unopened count for
      that suffix (FR-016). Negative control: a region of only `.py` reports zero.
- [ ] **T011** Test: the two-step bash idiom `RC=$?` + `if [ $RC -eq 0 ]` is found (FR-017).
      Negative control: a bare mention `echo "rc=$?"` is **not** a decision site and is not found.
- [ ] **T012** Test: a manifest surface with no `disposition` is REFUSED at load (FR-020).
- [ ] **T013** Test: `owned` without `conformance_check` or without `negative_control` is refused;
      `not-a-signal` without a rationale is refused; `disclosed` without an owner is refused (FR-019).
- [ ] **T014** Test: the report prints per-disposition counts and **contains no blended percentage** (FR-021).

### Implementation
- [ ] **T015** Replace `SCAN_SUFFIXES` with a declared table `(suffix, scanned, rationale)` covering
      every suffix present in scoped regions, `.gleam`/`.glp`/`.mjs` included and marked
      `scanned=False` with their rationale (FR-018).
- [ ] **T016** Count rejected files by suffix per region inside `scan()`; carry into the report (FR-016).
- [ ] **T017** Add the two assignment/decision patterns for `$?` (FR-017).
- [ ] **T018** Enforce `disposition` at manifest load with the per-disposition required fields (FR-019/020).
- [ ] **T019** Backfill `disposition` onto all 29 existing surfaces. Existing entries carrying a
      `conformance_check` **and** a `negative_control` become `owned`; the `ynet-client-alert-acknowledged`
      surface whose defect is owned by `@ariellas-qhstate` becomes `disclosed` with that owner.
- [ ] **T020** Report per-disposition counts; remove any blended coverage figure (FR-021).
- [ ] **T021** Re-run the audit bare; record the new numbers against T001/T003.

## Phase 2 — US2: the enforcing gate (P1)

### Tests first
- [ ] **T030** Test: `codeconv.receipts.override.applies` and `adoption_gate.override_applies` are the
      **same function object** (SC-004 — a second implementation cannot be added silently).
- [ ] **T031** Test: one override record, driven through both call paths, yields identical verdicts.
- [ ] **T032** Test: an adopted area with a non-conforming signal ⇒ audit REFUSES, non-zero exit.
- [ ] **T033** Test: a non-adopted area ⇒ no refusal, and a non-adoption marker is present.
- [ ] **T034** Test: an area with no declaration ⇒ ERROR, not a pass (FR-010).
- [ ] **T035** Test: an expired override resumes refusing (FR-012); an override with no expiry is
      rejected **at record time** (FR-012).
- [ ] **T036** Test: the audit still runs with `codeconv` **absent from `sys.path`** (FR-014).

### Implementation
- [ ] **T037** Create `scripts/lib/adoption_gate.py`, stdlib-only.
- [ ] **T038** Delegate `codeconv.receipts.override` and `.manifest` to it, signatures unchanged.
- [ ] **T039** Run 078's existing test suite as the regression proof of the move.
- [ ] **T040** Wire refusal into the audit: new `EXIT_REFUSED`, area resolution, override consultation.
- [ ] **T041** Record every refusal and every override permanently in the receipt (FR-015).

## Phase 3 — US1: the differential harness (P1)

### Tests first
- [ ] **T050** Test: two empty transcripts ⇒ `NOT-MEASURED`, never `MEASURED-AGREE` (FR-004).
- [ ] **T051** Test: a missing participant ⇒ `NOT-MEASURED` **naming the participant and reason** (FR-003).
- [ ] **T052** Test: a one-participant declaration is refused at load (FR-005).
- [ ] **T053** Test: differing transcripts ⇒ `MEASURED-DIVERGE` and the divergence is printed (FR-002).
- [ ] **T054** Test: each declared normalisation has a negative control proving it does not erase a
      real divergence (FR-006).

### Implementation
- [ ] **T055** `scripts/differential_gate.py` — declaration loader, runner, three-outcome reporter.
- [ ] **T056** `.specify/differential/criteria.json` — declare the Dart-vs-C# goal-term criterion that
      V-18..V-23 measures by hand.
- [ ] **T057** Suite **Section Y**, invoking the runner; NOT-MEASURED is reported, never skipped.
- [ ] **T058** **Execute** a reversion to prove the harness is a real detector (FR-007, SC-002) —
      executed, not asserted.
- [ ] **T059** Assert Section Y and V-18..V-23 agree on the same criterion.

## Phase 4 — Close

- [ ] **T070** Rebuild the Debug C# REPL (`dotnet build out/csharp/glp_repl/glp_repl.csproj`) — the
      freshness gate reads Debug, and a stale binary silently suppresses Sections I, T, U and V-18..23.
- [ ] **T071** Full suite run; compare to the 595/595 baseline; the 2 named not-run groups stay named.
- [ ] **T072** `/bk-codexreview`; fix every finding; **no deferrals**.
- [ ] **T073** Update `docs/evidence-signal-invariant.md` and `docs/known-issues.md` with the measured
      blind-spot findings.
- [ ] **T074** `/bk-ship`, `/bk-close`, marathon discharge.

---

## Blocked / disclosed, carried openly

- [!] **T080** `[03]`'s Postgres conformance evidence (feature 110) is **blocked on an admin-gated
      host fact**, not on this lane's code: `com.docker.service` is Stopped (Manual) and the running
      user `Olamnit\smbuser` is **not a member of `docker-users`** (only `Olamnit\gavri` is). Both
      fixes need administrator rights. Recorded, not worked around.
