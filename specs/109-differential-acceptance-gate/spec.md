<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature Specification: Differential acceptance, an enforcing gate, and an audit that cannot report a confident zero

**Feature Branch**: `109-differential-acceptance-gate`
**Created**: 2026-09-06
**Status**: Draft
**Input**: Engineer ruling `Q-olg16-01..03` (2026-09-06) composed the next era from THREE workstreams, all
three taken, not one of them; ruling `Q-olg17-01` (2026-09-06) packaged those three as this single feature
and `[03]` as its own.

---

## Why these three are one feature

Each of the three workstreams removes a different way for a **check to answer a question nobody asked**:

| workstream | the question asked | the question actually answered today |
|---|---|---|
| **A** differential gate | "do all N runtimes agree?" | "does runtime 1 pass?" |
| **B** FR-006 wiring | "does the consumer refuse a non-conforming signal?" | "does the harness simulate a consumer that would?" |
| **C** audit scope | "is this surface clean?" | "is this surface one of the 29 we chose to look at?" |

The shared invariant, and the one sentence this feature exists to enforce:

> **A criterion is discharged only by an instrument that could have failed. A check that cannot
> distinguish the passing case from the failing case has measured nothing, whatever it printed.**

This is feature 078's invariant (a check must prove it ran) and feature 108's invariant (a signal must
not report completion before the work completes) applied one level up — to the **criterion** rather than
to the check or the signal.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — A cross-runtime criterion refuses to be discharged from one runtime (Priority: P1)

A lane declares an acceptance criterion that spans runtimes or hosts — "Dart, C# and Gleam agree on
goal-term acceptance", "all four hosts count the same election tally". Today the suite reports that
criterion green when a single runtime passes. This story makes the suite **refuse** to report such a
criterion at all unless every declared participant was actually started, and their outputs compared.

**Why this priority**: It is the top-ranked item on the board (WSJF 19.50) and it is the workstream that
audits the other two. Measured on 2026-09-04 in feature 101: the feature was recorded implemented,
`CLAUDE.md` and `docs/known-issues.md` both named the exact C# lines the fix had landed at, **those lines
were still defective**, C# still returned a silent wrong answer for an improper list tail, and the Gleam
half had shipped **with no test file at all**. None of it was visible because nothing in the 566-check
suite had ever started a second runtime.

**Independent Test**: Declare a criterion spanning `{dart, csharp}`, run the suite with the C# REPL binary
absent, and confirm the criterion is reported **NOT-MEASURED** with the missing participant named — never
green, and never silently skipped.

**Acceptance Scenarios**:

1. **Given** a criterion declared over `{dart, csharp}` and both runtimes available, **When** the suite
   runs the same script through both and the transcripts are byte-identical after chrome normalisation,
   **Then** the criterion is reported MEASURED-AGREE.
2. **Given** the same criterion, **When** one runtime's transcript differs, **Then** the criterion is
   reported MEASURED-DIVERGE, the divergence is printed, and the suite fails.
3. **Given** the same criterion, **When** both transcripts are **empty**, **Then** the criterion is
   reported NOT-MEASURED — never MEASURED-AGREE. Two empty transcripts also compare equal, and that
   equality is the vacuous pass this story exists to prevent.
4. **Given** the same criterion, **When** a declared participant cannot be started, **Then** the criterion
   is reported NOT-MEASURED **naming the participant and the reason**, and this is distinguishable in the
   report from both agreement and divergence.
5. **Given** a criterion whose declared participant list has one entry, **When** the harness loads it,
   **Then** it is **refused at declaration time** — a one-participant "differential" is a category error,
   not a degenerate case to be tolerated.

---

### User Story 2 — A non-conforming signal is refused by its consumer, not merely reported (Priority: P1)

Feature 108 shipped an audit that **names** non-conforming evidence signals. It does not stop anything.
`/bk-codexreview` finding 8 recorded this plainly: the classifier, the size detector and the override
logic are **simulators in the test harness, not enforcement in the audit**. This story makes the audit an
enforcing gate, phased by declared adoption, with feature 078's informed-consent override.

**Why this priority**: A report that names a defect and permits it is the same shape as the defects it
names — it answers "did we notice?" while the reader hears "are we safe?". It is P1 alongside US1 because
until the gate binds, workstream C's widening only grows a list nobody must act on.

**Independent Test**: Point the audit at an area declared `adopted` that contains a known non-conforming
signal, and confirm it exits non-zero and refuses; then record a valid 078 override and confirm it
proceeds while the refusal remains permanently visible in the receipt.

**Acceptance Scenarios**:

1. **Given** an area declared `adopted` in `.specify/receipts/adoption.json` containing a non-conforming
   signal, **When** the audit runs, **Then** it REFUSES with a non-zero exit naming the area, the signal,
   and which of FR-004/FR-007/FR-012 it fails.
2. **Given** an area declared `non-adopted`, **When** the audit runs, **Then** it does NOT refuse, and the
   surface carries a **visible non-adoption marker** in the report.
3. **Given** an area with **no declaration at all**, **When** the audit runs, **Then** it is an ERROR —
   never treated as non-adoption, never a pass. (Mirrors 078 FR-019/FR-020 exactly.)
4. **Given** a refusal and a recorded override carrying briefing, acknowledgement, rationale, scope and a
   **future** `expires_on`, **When** the audit runs, **Then** it proceeds, and the receipt records a
   *recorded, expiring, scoped proceed* — never a pass.
5. **Given** an override whose `expires_on` has passed, **When** the audit runs, **Then** refusal
   RESUMES.
6. **Given** an override recorded with **no** `expires_on`, **When** it is recorded, **Then** it is
   rejected at the point of recording — not at the point of reliance.
7. **Given** the adoption and override rules, **When** they are evaluated by the audit and by
   `codeconv.receipts`, **Then** both evaluate the **same implementation** — a divergence between the two
   is impossible by construction, not by discipline.

---

### User Story 3 — The audit cannot report a confident zero over a surface it never opened (Priority: P1)

The audit reports `regions UNREAD 0` and `scope boundary 1329`. Both numbers are true and neither means
what a reader takes it to mean. Measured this session:

| measured fact | consequence |
|---|---|
| `SCAN_SUFFIXES = (.py, .cs, .dart, .sh, .ps1)` | **223 `.gleam`**, **1416 `.glp`** and **12 `.mjs`** files are never opened, in regions the report calls *examined* |
| `glp_gleam/src` scanned → `examined=0`, `sites=0` | reads as **clean**; means **never looked at** |
| `test/run_all_tests.sh` scanned → **0 hits** | the repo's single largest exit-status consumer, a ~2900-line suite whose whole job is deciding on exit statuses |
| the suite's actual idiom is `MAD_EXIT=$?` … `if [ $MAD_EXIT -eq 0 ]` | neither bash pattern (`$? -eq`, `if [ $?`) matches it — **zero of them** |
| `codeconv/tests` | **387 sites, all `exit-status`, in 67 files**; `codeconv/src` has only **11** |

**Why this priority**: P1 because it is the difference between this feature widening a real denominator
and widening a number. Adding regions while the suffix list and the bash patterns stay as they are would
produce a *larger* confident zero, which is worse than the honest small one it replaces.

**Independent Test**: Add a file in a scanned region containing a decision site written in the suite's
own `RC=$?` / `if [ $RC -eq 0 ]` idiom, and in a `.gleam` file, and confirm both are found; then remove
the fix and confirm both are missed. The negative control is the test.

**Acceptance Scenarios**:

1. **Given** a region declared in scope, **When** it contains files whose suffix is not scannable,
   **Then** the report states the **count of unopened files by suffix** for that region — a region is
   never called examined on the strength of the subset the scanner happens to read.
2. **Given** the two-step bash idiom `RC=$?` followed by a branch on `$RC`, **When** the scan runs,
   **Then** the decision site is found.
3. **Given** a newly scoped region, **When** each site is declared, **Then** every site carries a
   `disposition` of exactly one of `owned` / `not-a-signal` / `disclosed`; only `owned` requires a
   `conformance_check` and a `negative_control`; `not-a-signal` requires a one-line rationale;
   `disclosed` requires a named owner. (Ruling `Q-olg17-03`.)
4. **Given** a site with no `disposition`, **When** the manifest loads, **Then** it is REFUSED — absence
   is an error, never a default, in the same shape as 078 FR-020.
5. **Given** the report, **When** a reader looks for coverage, **Then** the per-disposition counts are the
   coverage statement, and a single aggregate percentage that mixes them is not published.

---

### Edge Cases

- **A runtime that starts and immediately exits 0 with no output.** Its transcript is empty; US1
  scenario 3 forces NOT-MEASURED. This is the instance-4 shape (exit 0 while refusing) at criterion level.
- **A criterion whose participants agree because both are broken identically.** Differential testing
  cannot detect this and MUST NOT claim to. The report says AGREE, which is a statement about agreement,
  not correctness — and the spec says so rather than letting the reader over-read it.
- **Chrome normalisation that normalises away the difference.** A normaliser is a claim about what is
  irrelevant. Each normalisation rule needs its own negative control proving it does not erase a real
  divergence.
- **An override recorded for a scope broader than the refusal.** It applies only within its recorded
  scope; a broader scope does not silently authorise a narrower future refusal of a different kind.
- **The adoption manifest itself under-declaring.** `GLPNET_AREAS` is the denominator; an area missing
  from the manifest is an error. This feature adds areas to neither without declaring them.
- **A `not-a-signal` disposition used to silence a real signal.** The rationale is required and is
  reviewed; the count of `not-a-signal` is published so a lane that disposes of its way to a clean report
  is visible in the number.
- **This feature's own harness.** It is subject to its own invariant (108 FR-017). Its negative controls
  must fail when the fix is reverted, and that reversion must be executed, not asserted.

---

## Requirements *(mandatory)*

### Differential acceptance (US1)

- **FR-001**: A criterion that spans more than one runtime, host or implementation MUST be **declared**
  with its participant set. An undeclared multi-participant criterion is an error, not a single-participant
  criterion.
- **FR-002**: A declared criterion MUST be reported as exactly one of **MEASURED-AGREE**,
  **MEASURED-DIVERGE**, or **NOT-MEASURED**. Only MEASURED-AGREE may be treated as discharged.
- **FR-003**: NOT-MEASURED MUST name the participant that was not started and the reason. A criterion
  whose participants could not all be started MUST NOT be reported green and MUST NOT be silently skipped.
- **FR-004**: Agreement MUST be established over **non-empty** outputs. Two empty outputs MUST yield
  NOT-MEASURED. The non-emptiness guard MUST be asserted before the comparison, not after.
- **FR-005**: A declaration with fewer than two participants MUST be refused at load time.
- **FR-006**: Every normalisation applied before comparison MUST be individually declared, and each MUST
  carry a negative control demonstrating that it does not erase a real divergence.
- **FR-007**: The harness MUST be proven a real detector by **executing** a reversion of a known fix and
  confirming the criterion reports MEASURED-DIVERGE. An unfalsifiable 100% scores zero (`Q-olg15` SC-003
  ruling, carried forward).
- **FR-008**: MEASURED-AGREE MUST be reported as *agreement*, never as *correctness*. Identical failure in
  all participants is agreement, and the report MUST NOT imply otherwise.

### The enforcing gate (US2)

- **FR-009**: The audit MUST refuse — non-zero exit — when a signal in an area with declared adoption is
  non-conforming. Reporting without refusing does not satisfy this requirement.
- **FR-010**: Refusal MUST bind only where the producing area has **declared adoption**; an area declared
  non-adopted keeps working behind a visible marker; an area with **no declaration** is an error.
- **FR-011**: Refusal MUST be overridable only through feature 078's informed-consent override —
  briefing, acknowledgement, rationale, declared scope and a **mandatory expiry**. This feature MUST NOT
  introduce a second override mechanism.
- **FR-012**: An expired override MUST resume refusing. An override with no expiry MUST be rejected **at
  the point it is recorded**.
- **FR-013**: The adoption and override rules MUST have exactly **one** implementation, reachable both
  from `codeconv.receipts` and from the stdlib-only audit. (Ruling `Q-olg17-02`.)
- **FR-014**: The audit MUST retain its stdlib-only property. Satisfying FR-013 MUST NOT make the audit
  unable to run where the `codeconv` virtual environment is absent — "the tool did not run" being read as
  "nothing to report" is measured instance 4.
- **FR-015**: An override MUST remain permanently visible in the receipt. It converts a refusal into a
  recorded, expiring, scoped proceed — never into a pass.

### The denominator (US3)

- **FR-016**: For every region reported as examined, the report MUST state the number of files it did
  **not open**, broken down by suffix. A region MUST NOT be reported examined on the strength of a subset.
- **FR-017**: The scan MUST find the two-step status idiom (`VAR=$?` followed by a decision on `VAR`),
  which is the dominant form in this repo's own suite and is currently found **zero** times.
- **FR-018**: The scanned-suffix set MUST be **declared** with a rationale per included and per excluded
  suffix. A language present in the repository and absent from the set is a declared, visible gap — never
  an implicit one.
- **FR-019**: Every manifest surface MUST carry a `disposition` of exactly one of `owned`,
  `not-a-signal`, `disclosed`. `owned` MUST carry a `conformance_check` and a `negative_control`;
  `not-a-signal` MUST carry a rationale; `disclosed` MUST carry a named owner.
- **FR-020**: A surface with no `disposition` MUST be refused at manifest load.
- **FR-021**: Coverage MUST be reported as per-disposition counts. A single blended percentage MUST NOT
  be published, because it makes `not-a-signal` and `owned` indistinguishable to a reader.
- **FR-022**: Widening MUST NOT reduce what is reported. The out-of-scope boundary count MUST continue to
  be reported for whatever remains outside scope.

### Cross-cutting

- **FR-023**: This feature's own harness is subject to its own invariant. Each negative control MUST be
  demonstrated failing by an executed reversion, recorded in the run.
- **FR-024**: No requirement here re-opens feature 078 or feature 108 semantically. FR-013's extraction is
  a **move**, behaviour-identical, covered by 078's existing tests (`Q-olg15-09` respected).

### Key Entities

- **Criterion declaration**: `{ id, participants[≥2], script, normalisations[], negative_control }`.
- **Criterion outcome**: `MEASURED-AGREE | MEASURED-DIVERGE | NOT-MEASURED`, plus the reason and the
  participant when NOT-MEASURED.
- **Adoption entry** (078, reused): `{ area, state ∈ {adopted, non-adopted}, since, note }`.
- **Override** (078, reused): `{ briefing, acknowledged_by, rationale, scope{area, check_id, reason},
  expires_on }`.
- **Surface disposition** (new): `owned | not-a-signal | disclosed`, with per-disposition required fields.
- **Suffix declaration** (new): `{ suffix, scanned: bool, rationale }`, enumerated for every suffix
  present in scoped regions.

---

## Success Criteria *(mandatory)*

- **SC-001**: Every criterion in the suite that names more than one runtime or host is declared, and each
  reports one of the three outcomes. **Baseline to beat: the four-host and three-runtime criteria are
  currently carried by claims; the count of declared multi-participant criteria today is 0.**
- **SC-002**: The differential harness is demonstrated a real detector by an **executed** reversion for
  every declared criterion — not by assertion. A criterion whose negative control was not executed is
  reported NOT-MEASURED, exactly as a missing participant is.
- **SC-003**: The audit refuses at least one real, currently-present non-conforming signal in an adopted
  area, and that refusal is cleared only by a recorded override or a fix — never by re-running.
- **SC-004**: The adoption/override rules have one implementation with two callers, demonstrated by a test
  that would fail if a second implementation were introduced.
- **SC-005**: For every examined region, the report states unopened-file counts by suffix. **Baseline:
  1651 files (223 `.gleam` + 1416 `.glp` + 12 `.mjs`) are currently unopened inside regions the report
  calls examined, and the report says nothing about them.**
- **SC-006**: The two-step bash status idiom is found. **Baseline: 0 found in `test/run_all_tests.sh`,
  which contains at least 6 instances.**
- **SC-007**: Every manifest surface carries a disposition; the report publishes per-disposition counts;
  no blended coverage percentage is published anywhere.
- **SC-008**: The full REPL suite is green at or above its recorded baseline (**595/595 executed checks,
  0 failures, 2 honestly-named not-run groups**) after this feature lands, with the two not-run groups
  still named rather than quietly dropped.

---

## Assumptions

- **The suite is the delivery vehicle for US1.** `test/run_all_tests.sh` already contains the reference
  implementation at V-18..V-23 (Dart vs C# byte-identical transcripts with a non-empty guard). This
  feature generalises that into a declared harness; it does not invent a new runner.
- **Promotion to a reusable `bk-guards` capability is OUT of scope here.** The roadmap brief names
  `bk-guards` as the candidate home. That is a buildkit-owned tree; this feature builds the mechanism and
  its declaration format in glpnet, and the promotion is a separate, buildkit-owned successor.
- **078's on-disk formats are stable and are the contract.** `.specify/receipts/adoption.json` and the
  override record shape are reused as-is. FR-013 moves the *code* that reads them; it does not change
  *what* is read.
- **The C# REPL must be rebuilt in Debug before the suite is trusted.** The freshness gate reads
  `bin/Debug/net11.0`; a stale binary silently suppresses Sections I, T, U and V-18..23 — the very
  sections US1 depends on. This is an environment precondition of SC-008, and it is checked, not assumed.
- **Widening targets are `codeconv/tests` (387 sites), `codeconv/src` (11) and `csharp` beyond the two
  already-scoped projects (79).** `test/`, `glp_runtime/`, `glp_gleam/`, `programs/` and
  `prereq-patterns/` currently scan to zero and MUST NOT be added until FR-017 and FR-018 land, or the
  widening would import the confident zero rather than remove it.
- **Gleam and GLP scanning is declared, not necessarily implemented, in this feature.** FR-018 requires
  the gap be *declared with a rationale*; making `.gleam` and `.glp` scannable is a larger piece of work
  and its absence must be visible rather than silent.

## Dependencies

- Feature **078** (verification receipts) — supplies the adoption manifest and the override machinery.
  Reused, not re-opened.
- Feature **108** (evidence-signal ordering) — supplies the audit, the manifest and the conformance
  harness this feature makes enforcing and widens.
- Feature **101** (goal-term acceptance) — supplies the proven differential method and its reference
  implementation.
- The Debug C# REPL build — an environment precondition, checked at run time.
