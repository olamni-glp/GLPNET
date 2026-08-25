<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: glptutorial corpus-golden reconciliation (stale goldens + drift-guard vendoring)

**Input**: Design documents from `/specs/083-glptutorial-corpus-goldens/`
**Prerequisites**: `plan.md` ✅, `spec.md` ✅, `research.md` ✅, `data-model.md` ✅, `contracts/tutorials-cli.md` ✅
**Branch**: `083-glptutorial-corpus-goldens` · **Lane**: `gavriella` · **Marathon**: `mrun-20d9230f767b` · **Z-series step**: `Z01`
**Generated**: 2026-08-25

**Tests**: Test tasks ARE included, and only where a requirement *is itself a detection claim*
(FR-004/SC-003 "a modification MUST cause it to fail"; FR-007 "exactly the unrepaired remainder";
C-4.4 "recapture only with a cited cause"). A requirement of the form "X must be detected" cannot
be delivered without a test that proves detection — the test is the deliverable, not extra scope.
No speculative unit tests are added beyond those.

**Organization**: Tasks are grouped by user story so each story is independently implementable and
testable. Slice ids (S1–S7) are the plan's; they are carried on each phase heading.

---

## 🔴 GATES — read before starting any task

Four items where the spec/plan, as written, **cannot be satisfied**. Two are already recorded in
`plan.md` → Complexity Tracking (A-1, A-2). **Three more (A-3, A-4, A-5) were measured during task
generation on 2026-08-25 and are recorded here.** Per the plan's own discipline they are **raised,
not silently redefined**.

| # | item | measured state | binding effect on tasks |
|---|---|---|---|
| **A-1** | SC-003 / FR-004 — "unmodified tree reports OK 100% of the time" | `sync --check` exits 1 with **67 drift lines** on an unmodified tree | **T039/T040 must not claim SC-003 until A-1 is ruled.** Delivered per-chapter via C-2.3. |
| **A-2** | SC-007 — "remains green (baseline 546 / 0 / 1)" | measured **561 total / 559 pass / 2 fail / 0 skip** | **No task may claim SC-007 until A-2 is ruled.** T055 asserts *no new failure* against 561/559/2/0. |
| **A-3** | Spec Assumption + research R-6: *"the existing approval-gated propose/apply flow is the delivery mechanism"* | **False as measured.** `propose.apply_proposal()` enforces the gate and then **mutates nothing** (its own docstring: *"the actual mutation … is performed by the caller"*) — and `cli.cmd_propose` discards its return value. `--apply` also applies `proposals[0]` **regardless of what `--approve` names**. No provenance is persisted anywhere. | The apply + record path must be **built** (T011–T015), not merely used. This is larger than "no new mechanism" implies. |
| **A-4** | FR-007 / SC-001 — "re-running the report shows exactly the unrepaired remainder" / "propose returns zero" | **All four proposals are unconditional literals** in `propose.generate_proposals()` — repairing a golden would not remove its proposal. And `cmd_propose` **always exits 0**, even with 4 outstanding, contradicting contract C-1's exit-code table. | `generate_proposals` must become **derived from corpus state** (T006–T008) and the exit code fixed (T010) **before** any repair can be verified. This is Phase 2 and blocks both stories. |
| **A-5** | Delivery target — repairs write to the **sibling** repo | `propose`'s `target_sibling_path` is `olamni/tutorial/ch04/exercise-0N`, and `sync()` vendors *from* `D:/bstdev/research/glp/GLP/olamni/tutorial`. That sibling is a **separate git repository** (`olamni-glp/GLP`), currently on branch `upgrade/buildkit-migration-20260727T173724Z` with **45 dirty files**. | **T024 / T027 write outside this repo, into a dirty tree on a non-default branch.** Cross-repo write authority and branch/commit discipline are an engineer decision. Do not start T024 until A-5 is ruled. |

🔴 **A-4 and A-3 are the same defect class as R-2 and as feature 078's NO-GO: a check whose result
does not depend on what it checks.** A proposal list that is a constant cannot fall to zero; an
apply that mutates nothing cannot be verified. Delivering US1's goldens without Phase 2 would
produce a corpus that *looks* repaired to `propose` exactly as much before as after.

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: `US1` / `US2` / `US3` — Setup, Foundational and Polish tasks carry no story label

## Path conventions

Single project (plan.md → Structure Decision). Real paths, verified 2026-08-25:

| what | path |
|---|---|
| tutorials tool | `codeconv/src/codeconv/tutorials/` (`cli.py` · `propose.py` · `sync.py` · `outcome.py` · `resolve.py` · `corpus.py` · `render_run.py`) |
| tests | `codeconv/tests/test_tutorials_*.py` |
| vendored corpus | `tutorials/olamni/` (13 chapters + `SNAPSHOT.md` + `.snapshot.json`) |
| sibling source-of-truth | `D:/bstdev/research/glp/GLP/olamni/tutorial/` (separate repo — see A-5) |
| ch07 substrate | `programs/cssg_modules/` (5 entries — **not** `_v2`, 6 entries) |
| REPL gate | `test/run_all_tests.sh` |

🔴 **`sync()` replaces the vendored tree wholesale** (`shutil.rmtree(dest)` then `copytree`,
`sync.py`). Anything written into `tutorials/olamni/` that is not present in the sibling is
**destroyed on the next `sync`**. Every new artefact this feature creates therefore lives *outside*
that tree — `tutorials/cssg_modules/`, `tutorials/ch07-run-manifest.json`,
`tutorials/golden-provenance.jsonl`.

---

## Phase 1: Setup — record the baselines this feature is measured against

**Purpose**: every success criterion here is a delta against a measured number. A criterion quoted
from a stale baseline is how SC-007 became unmeetable (R-5). All five land in one new file.

- [ ] T001 Verify both oracles are runnable on this host — `dart --version` and `dotnet --version` — and record the two versions in `specs/083-glptutorial-corpus-goldens/baselines.md`; an absent toolchain MUST abort the feature loudly, never be silently substituted (quickstart Prerequisites; F1/F3 exemplar #5)
- [ ] T002 [P] Record the `codeconv tutorials propose` baseline in `specs/083-glptutorial-corpus-goldens/baselines.md` — 4 proposals (`drift-gap-cssg`, `run-manifest-ch07`, `stale-golden-ch04-ex08`, `spec-violation-ch04-ex07`) **and the observed exit code 0**, which is itself the A-4 defect (SC-001 baseline)
- [ ] T003 [P] Record the `codeconv tutorials sync --check` baseline in `specs/083-glptutorial-corpus-goldens/baselines.md` — exit 1, **67 drift lines = 24 `differs from manifest` + 43 `differs from sibling`**, of which **13 lines over 10 unique in-scope paths** are ch04 (5 paths) + ch07 (5 paths); three paths drift in *both* classes, which is why the line count exceeds the path count (R-2)
- [ ] T004 [P] Record the REPL regression-gate baseline in `specs/083-glptutorial-corpus-goldens/baselines.md` — `bash test/run_all_tests.sh` → **561 total / 559 pass / 2 fail / 0 skip**, naming the two pre-existing `Section T` 064 service-box drills (T-1 US1 resume, T-2 US2 history) as out of scope (R-5, A-2)
- [ ] T005 Confirm the ch07 substrate identity by listing both siblings — `programs/cssg_modules/` (5 entries: `agent.glp`, `boot.glp`, `mad_boot.glp`, `self.glp`, `ui/`) vs `programs/cssg_modules_v2/` (6 entries, adds `child_agent.glp`) — and record the confirmation in `specs/083-glptutorial-corpus-goldens/baselines.md` (R-3, C-5.2)

**Checkpoint**: every number this feature will be judged on is written down and dated.

---

## Phase 2: Foundational (BLOCKING) — make the report and the apply path mean something

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete. **A-3 and A-4 are here.**
Until `propose` is derived and `--apply` actually applies and records, US1's repairs cannot be
verified and US3 has nothing to surface.

### Make the proposal report derived, not constant (A-4 — FR-001, FR-007)

- [ ] T006 Derive `spec-violation-ch04-ex07` from a live-vs-golden comparison of ch04/07 in `codeconv/src/codeconv/tutorials/propose.py`, so the proposal is emitted only while the golden and the runtime actually disagree (today it is an unconditional literal)
- [ ] T007 Derive `stale-golden-ch04-ex08` from a live-vs-golden comparison of ch04/08 in `codeconv/src/codeconv/tutorials/propose.py`, for **both** backends (FR-003)
- [ ] T008 [P] Derive `drift-gap-cssg` from whether the ch07 substrate is actually vendored, and `run-manifest-ch07` from whether the run manifest actually exists, in `codeconv/src/codeconv/tutorials/propose.py`
- [ ] T009 [P] Preserve the four `proposal_id` strings verbatim across the derivation so "this one is fixed" stays citable, and lock them with an id-stability test in `codeconv/tests/test_tutorials_propose.py` (C-1.4)
- [ ] T010 Implement contract C-1's exit codes in `cmd_propose` in `codeconv/src/codeconv/tutorials/cli.py` — exit 0 **iff** zero proposals, non-zero while any proposal is outstanding (today it always raises `typer.Exit(_EXIT_OK)`)

### Make `--apply` apply the approved proposal (A-3 — FR-006, C-1.1)

- [ ] T011 Select the proposal named by `--approve` in `cmd_propose` in `codeconv/src/codeconv/tutorials/cli.py`, and refuse an unknown or ambiguous id, instead of applying `proposals[0]` regardless of what was approved
- [ ] T012 [P] Create `codeconv/src/codeconv/tutorials/provenance.py` implementing entity E5 — `change_class ∈ {repair, recapture}`, `approved`, `rationale`, `cited_cause`
- [ ] T013 Mechanise the C4 rule in `codeconv/src/codeconv/tutorials/provenance.py` — `recapture` is permitted **only** with a non-null `cited_cause` naming a specific runtime change; absent a citation the change is recorded as a `repair` (C-4.4, FR-008)
- [ ] T014 Persist provenance append-only to `tutorials/golden-provenance.jsonl`, deliberately **outside** `tutorials/olamni/` because `sync()` rmtree-replaces that tree and would destroy it (FR-006, C-4.2)
- [ ] T015 Wire `propose.apply_proposal` through to the provenance write in `codeconv/src/codeconv/tutorials/propose.py`, so no repair can be applied without a recorded provenance row, and the caller consumes the result rather than discarding it (FR-006)

### Tests that prove the mechanism conditions on something

- [ ] T016 [P] Add a regression test in `codeconv/tests/test_tutorials_propose.py` proving `propose` reports zero **only** on a clean corpus, and that re-introducing a divergence brings its proposal back — the direct anti-constant-list guard for A-4 (FR-007)
- [ ] T017 [P] Add a test in `codeconv/tests/test_tutorials_propose.py` that `--apply` without `--approve`, or without `--rationale`, refuses **and mutates nothing** (FR-006, C-1.1)
- [ ] T018 [P] Add a test in `codeconv/tests/test_tutorials_propose.py` that a change submitted as a re-capture **without** a cited cause is recorded as `repair`, never `recapture` (C-4.4, FR-008)

**Checkpoint**: `propose` falls when a divergence is repaired, exits non-zero while any remains, and
`--apply` applies exactly the approved proposal and leaves a provenance row. Only now can a repair
be verified.

---

## Phase 3: User Story 1 — The corpus stops asserting falsehoods (Priority: P1) 🎯 MVP · slices S1+S2

**Goal**: no recorded outcome disagrees with the live runtime for a merely historical reason.

**Independent test**: run every ch04 exercise against the live runtime and compare to its golden —
zero unexplained mismatches. Delivers value without touching the drift guard.

### S1 — the golden gains a way to say "correctly refused" (FR-009, C-3)

- [ ] T019 [US1] Add the recorded `outcome_kind ∈ {loaded, rejected, error}` discriminator for entity E2 in `codeconv/src/codeconv/tutorials/outcome.py`, kept distinct from the existing derived `GoldenKind` parse classifier (FR-009, C-3.1)
- [ ] T020 [US1] Define the `rejected` payload as the refusal's **mechanical identity** — diagnostic/rule id plus the offending clause — never free prose, in `codeconv/src/codeconv/tutorials/outcome.py`, so comparison survives rewording (C-3.2, FR-001)
- [ ] T021 [US1] Parse and emit `outcome_kind` in the golden trace format (`ex-MM-repl-trace.md`) in `codeconv/src/codeconv/tutorials/outcome.py`, so an existing golden without the field still loads (backward-compatible read)
- [ ] T022 [P] [US1] Add a test in `codeconv/tests/test_tutorials_corpus.py` that `rejected` and `error` are distinct values and that a genuine runtime breakage can never be recorded as `rejected` (C-3.3, data-model E2)

### S1 — re-record ch04/07 as a rejection, source untouched (FR-002 ruling (b))

- [ ] T023 [US1] Capture the live ch04/07 outcome verbatim via `codeconv tutorials run --chapter ch04 --exercise 07` and save the transcript under `specs/083-glptutorial-corpus-goldens/captures/ch04-ex07-live.txt`
- [ ] T024 [US1] Re-record the ch04/07 golden as `outcome_kind=rejected` in `D:/bstdev/research/glp/GLP/olamni/tutorial/ch04/exercise-07/ex-07-repl-trace.md`, replacing the false `✓ Loaded` — 🔴 **blocked on A-5** (cross-repo write) and 🔴 **`ch-04-ex-07-recursive-numerics.glp` MUST NOT be modified** (FR-002 ruling (b), C-3.5, E1 invariant, Code Modification Protocol)
- [ ] T025 [US1] Record the ch04/07 provenance row in `tutorials/golden-provenance.jsonl` — `change_class=repair`, `cited_cause=null` (no runtime changed; the golden was simply false) (FR-008, C-4.4)

### S2 — re-capture ch04/08 against both live oracles (FR-003)

- [ ] T026 [US1] Capture the live ch04/08 outcome on **both** backends — `codeconv tutorials run --chapter ch04 --exercise 08 --backend csharp` and `--backend dart` — saving both transcripts under `specs/083-glptutorial-corpus-goldens/captures/`
- [ ] T027 [US1] Re-record the ch04/08 golden in `D:/bstdev/research/glp/GLP/olamni/tutorial/ch04/exercise-08/ex-08-repl-trace.md` as `outcome_kind=loaded` with `F=[5,4,3,2,1]` and **no `[WARN] Unknown guard predicate: is_list` lines**, for both `dart` and `csharp` — 🔴 **blocked on A-5** (FR-003, C-3.4)
- [ ] T028 [US1] Record the ch04/08 provenance row in `tutorials/golden-provenance.jsonl` — `change_class=recapture`, `cited_cause` = the C# `is_list` guard fix (`docs/research/csharp-repl-convergence-fixes.md`) (FR-008, C-4.4)

### S1+S2 verification

- [ ] T029 [US1] Re-vendor with `codeconv tutorials sync` so `tutorials/olamni/ch04/exercise-07/` and `.../exercise-08/` carry the re-recorded goldens, and confirm neither exercise appears in `sync --check` output afterwards
- [ ] T030 [US1] Verify `codeconv tutorials propose` now reports **2** proposals — the two ch07 ones only — with a **non-zero** exit, and record the run in `specs/083-glptutorial-corpus-goldens/baselines.md`; a clean report here would be a false green and is itself a defect (FR-007, FR-001, SC-001 partial)
- [ ] T031 [P] [US1] Add an integration test in `codeconv/tests/test_tutorials_corpus.py` comparing every ch04 exercise's golden against the live runtime, asserting zero unexplained mismatches (US1 Independent Test, SC-002)

**Checkpoint**: US1 complete and independently testable. The oracle no longer asserts a falsehood.
**This is the MVP** — stop and validate here.

---

## Phase 4: User Story 2 — The drift guard can see the whole corpus (Priority: P2) · slices S3+S4+S5

**Goal**: a change to `programs/cssg_modules/` is noticed and named.

**Independent test**: modify the ch07 substrate, run `sync --check`, observe a non-zero exit naming
the drifted path — and observe exit 0 on the unmodified in-scope tree.

### S3 — make the guard informative before adding to it (R-2, C-2.3) 🔴 A-1 gate

- [ ] T032 [US2] Add `--chapter <chXX>` scoping to `check()` in `codeconv/src/codeconv/tutorials/sync.py` and to `cmd_sync` in `codeconv/src/codeconv/tutorials/cli.py`, so "ch07 is clean" becomes an expressible statement (C-2.3)
- [ ] T033 [US2] Keep the two drift classes separately reported under scoping — `differs from manifest` vs `differs from sibling` — in `codeconv/src/codeconv/tutorials/sync.py`; both are present today (24 and 43 lines) and collapsing them would hide which side moved (C-2.4)
- [ ] T034 [P] [US2] Add a test in `codeconv/tests/test_tutorials_sync.py` that a `--chapter ch07` verdict is **independent** of drift in every other chapter — the scoping is only useful if out-of-scope noise cannot change the in-scope answer
- [ ] T035 [US2] Drive the in-scope **ch04** drift to zero — 5 unique paths: `ch04-specification-input-prompt.md`, `ch04_tutorial.md`, `exercise-03/ex-03-tutorial.md`, `exercise-05/ex-05-tutorial.md`, `spec-rev-eng-input/ch04-DEPRECATED-spec.md` (the last drifts in **both** classes)
- [ ] T036 [US2] Drive the in-scope **ch07** drift to zero — 5 unique paths: `ch07-sources.md`, `spec-rev-eng-input/ch07-DEPRECATED-spec.md` (both drift in **both** classes), `exercise-06/ex-06-flutter-trace.md`, `exercise-12/ex-12-tutorial.md`, `simple-multimodule/boot.glp`

### S4 — vendor the ch07 substrate (FR-004, E6)

- [ ] T037 [US2] Extend `sync()` in `codeconv/src/codeconv/tutorials/sync.py` to vendor `programs/cssg_modules/` into `tutorials/cssg_modules/` with its own per-file SHA-256 digest manifest — deliberately **outside** `tutorials/olamni/`, which `sync()` rmtree-replaces from the sibling (E6, FR-004, C-2.2)
- [ ] T038 [US2] Record `scope_chapter = ch07` on the vendored substrate manifest in `codeconv/src/codeconv/tutorials/sync.py`, so the substrate's drift is attributable to a chapter and answerable by T032's scoping (E6, R-2)
- [ ] T039 [P] [US2] Add the SC-003 **positive** test in `codeconv/tests/test_tutorials_sync.py` — modifying a vendored `cssg_modules` file causes a non-zero exit **naming the drifted path** (FR-004, C-2.1) — 🔴 may not claim SC-003 until A-1 is ruled
- [ ] T040 [P] [US2] Add the SC-003 **negative** test in `codeconv/tests/test_tutorials_sync.py` — the unmodified in-scope tree exits 0 — and assert the positive test **fails** if the digest comparison is stubbed to a no-op, so the guard cannot stay green while guarding nothing (078's `test_guard_weakening` lesson) — 🔴 may not claim SC-003 until A-1 is ruled

### S5 — the ch07 run manifest (FR-005, E4)

- [ ] T041 [US2] Create `tutorials/ch07-run-manifest.json` mapping each ch07 exercise to exactly one `(program, play, step_limit)` triple, with `program = programs/cssg_modules` (C-5.1, C-5.2, E4)
- [ ] T042 [US2] Read that manifest in `codeconv/src/codeconv/tutorials/resolve.py` in place of the hard-coded `_CSSG_RELPATH` constant and the guide-parsed `fplayMM` goal, so the mapping is recorded rather than inferred (FR-005)
- [ ] T043 [US2] Make a missing mapping a **failure** in `codeconv/src/codeconv/tutorials/resolve.py` — never a silent default to the guide-parsed goal (C-5.3, SC-004)
- [ ] T044 [US2] Include `tutorials/ch07-run-manifest.json` in the digest set checked by `sync --check` so the manifest is itself drift-checkable (C-5.4)
- [ ] T045 [P] [US2] Add a test in `codeconv/tests/test_tutorials_run.py` that every ch07 exercise resolves to exactly one triple, with no ambiguous and no missing mapping (SC-004)

### S3 — report what is out of scope rather than repairing it

- [ ] T046 [US2] Write `specs/083-glptutorial-corpus-goldens/out-of-scope-drift.md` listing the **54 remaining out-of-scope drift lines** (67 total minus the 13 in-scope) across ch01–ch03, ch05, ch06, ch08–ch13 and the root `tutorial.md`, split by class — **reported, not repaired**; note in that file that `plan.md`/`research.md` say "57 out of scope", which counts *unique paths* (10 in scope) where this counts *lines* (13 in scope) — both are right about different things and the file must say which (C-6, spec Out of Scope)
- [ ] T047 [US2] Verify `codeconv tutorials propose` reports **0** proposals for ch04 and ch07 with exit 0, and record the run in `specs/083-glptutorial-corpus-goldens/baselines.md` (SC-001)

**Checkpoint**: US1 and US2 both work independently. The guard's ch07 verdict now depends on the
ch07 substrate.

---

## Phase 5: User Story 3 — Repairs are proposed, approved, and recorded (Priority: P3) · slice S6

**Goal**: every golden change carries a recoverable approval, rationale and classification.

**Independent test**: inspect the corpus after the fact and recover, for each changed golden, who
approved it, why, and whether it was a repair or a re-capture.

- [ ] T048 [US3] Surface the provenance record through a read path — extend `codeconv tutorials explain` in `codeconv/src/codeconv/tutorials/explain.py` and `cli.py` to print `change_class`, `approved`, `rationale` and `cited_cause` for the exercise's golden (FR-006, C-4.2)
- [ ] T049 [P] [US3] Add a test in `codeconv/tests/test_tutorials_propose.py` that every applied repair has a recoverable approval **and** rationale — 100%, no exceptions (SC-005, C-4.1)
- [ ] T050 [P] [US3] Add a test in `codeconv/tests/test_tutorials_propose.py` that each changed golden is classifiable stale-vs-runtime-change from the record alone, **without reading the implementation** (SC-006, C-4.5)
- [ ] T051 [US3] Correct the `drift-gap-cssg` remedy text in `codeconv/src/codeconv/tutorials/propose.py` from *"Vendor cssg_modules/ **or** record a run-manifest"* to a conjunction — the "or" invites delivering half of two separate MUSTs (C-1.5, FR-010, R-4, spec C2)
- [ ] T052 [US3] Correct the Issue-10 headline at `docs/known-issues.md:317` so its *"Status: By design (3-Hybrid scope, Gabi-approved 2026-06-04)"* no longer covers the three **corpus-golden issues** repaired by this feature, which were pending repairs rather than approved scope; leave the deferred run-shapes (two-session, bytecode-dump, Flutter-only) as by-design (FR-010)

**Checkpoint**: all three user stories independently functional.

---

## Phase 6: Polish & cross-cutting concerns

- [ ] T053 Run `specs/083-glptutorial-corpus-goldens/quickstart.md` end to end and correct any step whose quoted output no longer matches (the quickstart quotes 2026-08-24 outputs verbatim)
- [ ] T054 Run the codeconv test suite — `codeconv/.venv/Scripts/python.exe -m pytest codeconv/tests -q` — and confirm no regression in the pre-existing `test_tutorials_*.py` files
- [ ] T055 Run the REPL regression gate `bash test/run_all_tests.sh` and confirm **no new failure against 561 / 559 / 2 / 0**; check `glp_runtime/glp_repl.exe` is newer than its sources before believing a better-than-expected count — 🔴 may not claim SC-007 until A-2 is ruled
- [ ] T056 Write the **B10** report to Udi at `docs/research/book-4-3-1-lesseq-guard-finding.md` — a byte-exact transcription of book §4.3.1 `lesseq` is **rejected** by the typed-GLP guard rules, because `natural_number/1` is a two-clause procedure while manual §8 requires a defined guard to be a single-unit-clause procedure; per the Bug Protocol this is **reported, not fixed** (slice S7, spec C5 consequence 3)
- [ ] T057 Append amendments **A-3, A-4 and A-5** to `specs/083-glptutorial-corpus-goldens/plan.md` → Complexity Tracking, so the plan's own record of "items that cannot stand as written" is complete rather than split across two artefacts

---

## Dependencies & execution order

### Phase dependencies

- **Phase 1 (Setup)** — no dependencies; start immediately
- **Phase 2 (Foundational)** — depends on Phase 1; **BLOCKS both user stories** (A-3, A-4)
- **Phase 3 (US1)** — depends on Phase 2; blocked at T024/T027 by **A-5**
- **Phase 4 (US2)** — depends on Phase 2; T039/T040 gated by **A-1**
- **Phase 5 (US3)** — depends on Phase 2 (T012–T015 create what it surfaces); independent of US2
- **Phase 6 (Polish)** — depends on all desired stories; T055 gated by **A-2**

### User story dependencies

- **US1 (P1)** — after Phase 2. No dependency on US2 or US3. **MVP on its own.**
- **US2 (P2)** — after Phase 2. No ordering dependency on US1 beyond value sequencing (spec Open
  Escalation E2 keeps the split cheap). T037's vendoring is only meaningful after T032's scoping.
- **US3 (P3)** — after Phase 2. Surfaces the records US1's repairs write; testable once US1 has
  produced at least one provenance row.

### Within-story ordering

- T006–T009 (derivation) before T010 (exit code): the exit code is only correct once the count is
- T012 → T013 → T014 → T015: entity, then the C4 rule, then persistence, then the wiring
- T019–T021 (schema) before T024 (the first golden that uses it)
- T023 before T024, T026 before T027: capture the live outcome before recording it
- T029 (re-vendor) before T030 (verify the count fell)
- T032–T033 (scoping) before T037 (vendoring): adding to a saturated guard first is R-2's error
- T041 → T042 → T043 → T044: create the manifest, read it, make absence fatal, then guard it

### Parallel opportunities

- **Phase 1**: T002, T003, T004 in parallel (three independent measurements, one file — merge at the end)
- **Phase 2**: T008 and T009 in parallel with T006/T007; T012 in parallel with T010/T011; T016/T017/T018 in parallel once T015 lands
- **Phase 3**: T022 in parallel with T023/T026 (different files); T031 after T029
- **Phase 4**: T034, T039, T040, T045 in parallel (four independent test files/cases)
- **Phase 5**: T049 and T050 in parallel
- Different user stories can proceed in parallel once Phase 2 is complete

---

## Parallel example: Phase 2 tests

```bash
# Once T015 lands, launch the three mechanism tests together:
Task: "Anti-constant-list regression test in codeconv/tests/test_tutorials_propose.py"
Task: "--apply refusal test (no --approve / no --rationale) in codeconv/tests/test_tutorials_propose.py"
Task: "recapture-without-cited-cause records as repair, in codeconv/tests/test_tutorials_propose.py"
```

---

## Implementation strategy

### MVP first (US1 only)

1. Phase 1 — baselines recorded
2. Phase 2 — **critical**; without it a repair cannot be verified (A-3, A-4)
3. Phase 3 — US1
4. **STOP and VALIDATE**: `propose` reports exactly 2 (the ch07 pair), exit non-zero; every ch04
   golden agrees with the live runtime
5. Ship or demo

### Incremental delivery

1. Setup + Foundational → the report and the apply path condition on real state
2. + US1 → the oracle stops asserting falsehoods (**MVP**) — proposals 4 → 2
3. + US2 → the guard's ch07 verdict depends on the ch07 substrate — proposals 2 → 0
4. + US3 → every change is attributable
5. Polish → quickstart, gates, B10 report

### Gate discipline

🔴 **A-1, A-2, A-3, A-4, A-5 are open.** Tasks may be executed, but **no slice may claim SC-003
(A-1) or SC-007 (A-2)**, T024/T027 must not start before **A-5** is ruled, and the Phase 2 scope
increase implied by **A-3/A-4** should be acknowledged before Phase 2 begins. Advancing past an open
gate is the failure mode this lane has already had to withdraw once.

---

## Notes

- `[P]` = different files, no dependency on an incomplete task
- `[Story]` maps a task to a user story for traceability
- Commit after each task or logical group; **stage by name** — other sessions are live in this repo
- Every count in this file was measured on **2026-08-25**, not inherited
- The two ch07 `stale-ch07-exNN` proposal branches in `propose.generate_proposals()` do not fire on
  today's corpus (the report shows 4, not 9); T006–T008 must not accidentally activate them —
  ch07/08–12 disposition is **out of this feature's scope**
