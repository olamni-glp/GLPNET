---
description: "Task list for feature 034 — glp_gleam core terms + heap + unification (F4)"
---

# Tasks: glp_gleam core terms + heap + unification (F4)

**Input**: Design documents from `/specs/034-glp-gleam-core-terms-and-heap/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/runtime-api.md ✓, quickstart.md ✓

**Tests**: INCLUDED — the spec mandates automated verification (each user story's *Independent Test*
is a gleeunit suite; FR-009/SC-005 require a parity corpus). The kernel's correctness *is* its tests.

**Organization**: by user story (US1 P1 → US2 P2 → US3 P3), each an independently testable increment.

**Port basis (source of truth)**: Dart `glp_runtime/lib/runtime/{terms,heap_fcp,suspension}.dart`.
**Landing site**: the F3 `glp_gleam/` subtree (additive only). **Mechanism (R-001)**: immutable threaded
binding store — NOT process-cells. **Toolchain**: Gleam 1.17.0 / OTP 25.3.2.8 on WSL Ubuntu; `gleam test`
+ `glp_gleam/smoke.sh`; no `gleam_otp`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no incomplete-task dependency)
- **[Story]**: US1 / US2 / US3 (setup, foundational, polish carry no story label)
- All paths are repo-relative under `glp_gleam/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: scaffold the new module + test files; confirm the F3 subtree baseline is green before any change.

- [X] T001 Confirm baseline: from `glp_gleam/` run `gleam build --target erlang` + `gleam test` + `./smoke.sh` (WSL) — all green (F3 baseline) before touching anything (Constitution VII).
- [X] T002 [P] Create empty module files `glp_gleam/src/glp/runtime/terms.gleam`, `glp_gleam/src/glp/runtime/suspension.gleam`, `glp_gleam/src/glp/runtime/heap.gleam`, `glp_gleam/src/glp/runtime/unify.gleam` (compile-clean placeholders).
- [X] T003 [P] Create empty test files `glp_gleam/test/glp/runtime/terms_test.gleam`, `heap_test.gleam`, `unify_test.gleam`, `suspension_test.gleam`, `parity_test.gleam` (compile-clean placeholders) under `glp_gleam/test/glp/runtime/`.

**Checkpoint**: subtree still builds + tests green with empty new files (additivity preserved — FR-011).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the shared data definitions every story builds on (the ADTs + the heap container + the
shared result/error types). **Models before services** — types here, behaviour in the story phases.

**⚠️ CRITICAL**: no user-story work begins until this phase is complete.

- [X] T004 [P] Define the term model in `glp_gleam/src/glp/runtime/terms.gleam`: `Constant { ConstAtom(String) ConstInt(Int) ConstReal(Float) ConstString(String) }`, `Term { ConstTerm(Constant) StructTerm(functor: String, args: List(Term)) VarRef(addr: Int) }`, plus `nil()`/`cons(Term, Term)` helpers (← `terms.dart`; data-model §1; R-002/R-003). NO `MutualRefTerm`/`ModuleTerm` (R-008).
- [X] T005 [P] Define suspension types in `glp_gleam/src/glp/runtime/suspension.gleam`: `Suspension(goal_id: Int, resume_pc: Int, armed: Bool)` and `GoalRef(goal_id: Int, resume_pc: Int)` (← `suspension.dart`; data-model §4).
- [X] T006 Define the heap container in `glp_gleam/src/glp/runtime/heap.gleam`: `opaque Heap`, `Cell { WriterCell(reader_addr, suspensions) WriterBound(target) ReaderCell(writer_addr) ValueCell(term) }`, `CellTag` + a **derived** `tag(Cell) -> CellTag` (NOT a stored field — F2/data-model §2), `DerefResult { Bound(Term) Unbound(writer) }`, `HeapError { WriterToWriter AlreadyBound NotAWriter Cycle }`, `UnifyOutcome { Success(Heap) Suspend(Heap, on) Fail }`, and `new() -> Heap` (← `heap_fcp.dart`; data-model §2,3,5,7,8). Depends on T004, T005.
- [X] T007 Implement `allocate_variable(Heap) -> #(Heap, Int, Int)` and tag predicates `is_writer`/`is_reader`/`is_value` (role from cell tag ONLY, never address arithmetic — FR-002) in `heap.gleam` (← `allocateVariable`; data-model §3). Depends on T006.
- [X] T008 Wire the `glp_gleam/src/glp/runtime.gleam` umbrella to re-export the public surface from `terms`/`suspension`/`heap` (type aliases + thin wrappers; replaces the F3 doc-only placeholder — R-004). Extended for `unify` in T015. Depends on T004–T007.

**Checkpoint**: types compile; `allocate_variable` usable; subtree green. User stories can begin.

---

## Phase 3: User Story 1 - Term model + variable store (Priority: P1) 🎯 MVP

**Goal**: construct GLP terms; allocate a logic variable; deref unbound; bind a writer to a ground value;
deref back to it with path compression — on BEAM, matching the Dart source.

**Independent Test**: `gleam test` runs `terms_test` + `heap_test`: build/inspect/compare all 9 term
kinds (SC-001); allocate → deref `Unbound` → `bind_writer` → deref value → re-deref O(1) (SC-002);
WxW-during-deref → `Error(WriterToWriter)`.

### Tests for User Story 1 (write FIRST, ensure they FAIL)

- [X] T009 [P] [US1] `terms_test.gleam`: construct + structurally inspect + equality-compare all 9 kinds (atom, int, real, string, compound struct, empty list `nil()`, non-empty list `cons`, nested struct, `VarRef`) — SC-001 — in `glp_gleam/test/glp/runtime/terms_test.gleam`.
- [X] T010 [P] [US1] `heap_test.gleam`: fresh var → `deref` = `Unbound`; after `bind_writer` → `deref` = the value; repeated `deref` on the returned heap does not re-traverse (compression); role read from tag (AS US1#4); WxW chain → `Error(WriterToWriter)` — SC-002 + SC-004(deref) — in `glp_gleam/test/glp/runtime/heap_test.gleam`.

### Implementation for User Story 1

- [X] T011 [US1] Implement `deref(Heap, Int) -> Result(#(Heap, DerefResult), HeapError)` with **path compression threaded into the returned heap**, cycle detection, and WxW detection during traversal in `glp_gleam/src/glp/runtime/heap.gleam` (← `derefAddr`; data-model §5; FR-003/FR-004/R-006). Depends on T007.
- [X] T012 [US1] Implement `bind_writer(Heap, Int, Term) -> Result(#(Heap, List(GoalRef)), HeapError)`: unbound `WriterCell` → `ValueCell(value)`; single-assignment (`AlreadyBound`); returns `[]` activations for now (suspension production lands in US3) in `heap.gleam` (← `bindWriter`; FR-005). Depends on T007.
- [X] T013 [US1] Implement `bind_writer_to_var(Heap, Int, Int) -> Result(#(Heap, List(GoalRef)), HeapError)`: writer→target-reader chain (`WriterBound`); target-resolves-to-writer → `Error(WriterToWriter)`; returns `[]` (forwarding lands in US3) in `heap.gleam` (← `bindWriterToReader`; FR-004/FR-006). Depends on T007.
- [X] T014 [US1] Run `gleam test` + `./smoke.sh`; make `terms_test` + `heap_test` green; confirm additivity (no other-subtree change).

**Checkpoint**: US1 is a fully functional, demonstrable MVP — GLP data + a bound/read-back logic variable.

---

## Phase 4: User Story 2 - Writer-MGU three-valued unification (Priority: P2)

**Goal**: unify two terms → exactly one of success / suspend / fail, binding only writers (never readers,
never writer-to-writer), suspending (not failing) on a needed unbound reader.

**Independent Test**: `gleam test` runs `unify_test`: the full SC-003 truth table returns the correct
verdict; every WxW attempt → `Error(WriterToWriter)` (0 silent — SC-004).

### Tests for User Story 2 (write FIRST, ensure they FAIL)

- [X] T015 [P] [US2] `unify_test.gleam`: SC-003 truth table — const/const (match & mismatch), struct/struct (match, functor-mismatch, arity-mismatch), var/value, value/var, var/var, **unbound-reader-needed → `Suspend`** (never `Fail`); assert the suspend case yields the **verdict + `on` address only** (NOT a stored `SuspensionRecord` — `unify` has no goal context, F1); assert binding touches only writers; WxW → `Error` not `Fail` (SC-004); **no occurs-check** (F4) — unify of a writer with a struct containing that writer's own reader succeeds without diverging (and a subsequent `deref` of the resulting cyclic term yields `Error(Cycle)`, faithful to Dart) — in `glp_gleam/test/glp/runtime/unify_test.gleam`.

### Implementation for User Story 2

- [X] T016 [US2] Implement `unify(Heap, Term, Term) -> Result(UnifyOutcome, HeapError)` in `glp_gleam/src/glp/runtime/unify.gleam`: deref both (thread heap); ground-equal→`Success`, mismatch→`Fail`; writer vs ground→`bind_writer`; writer vs var→`bind_writer_to_var`; struct/N same functor→unify args pairwise (first non-`Success` short-circuits); needed unbound reader→`Suspend(on:)`; bind **only writers**; **no occurs-check** (FR-007; data-model §7; R-008). Depends on T011–T013.
- [X] T017 [US2] Extend the `glp/runtime.gleam` umbrella (T008) to re-export `unify` + `UnifyOutcome`. Depends on T016.
- [X] T018 [US2] Run `gleam test`; make `unify_test` green; confirm additivity.

**Checkpoint**: US1 + US2 both independently functional — the distinctive GLP unification semantic works.

---

## Phase 5: User Story 3 - Suspension storage, activation & Dart parity corpus (Priority: P3)

**Goal**: record a suspension on an unbound writer; on binding that writer produce the exact activation
list; on binding to a variable forward suspensions; and pin observable outcomes to the Dart source-of-truth.

**Independent Test**: `gleam test` runs `suspension_test` + `parity_test`: suspend → bind-to-value →
activation list = the armed suspension(s); bind-to-variable forwards + fires nothing; the micro-scenario
corpus matches Dart observable outcomes (SC-005).

### Tests for User Story 3 (write FIRST, ensure they FAIL)

- [X] T019 [P] [US3] `suspension_test.gleam`: suspend on unbound writer (reader pairing preserved) → `bind_writer` returns activation list containing exactly the armed suspension; `bind_writer_to_var` forwards suspensions to the target writer and returns `[]` (no activation yet) — FR-008 / US3 AS#1,#2 — in `glp_gleam/test/glp/runtime/suspension_test.gleam`.
- [X] T020 [P] [US3] `parity_test.gleam`: the fixed micro-scenario corpus (allocate · deref · bind-to-value · bind-to-variable · the unify truth table · suspend-and-activate), each asserting the **observable** outcome (deref result · unify verdict · activation set) — internal heap layout EXCLUDED — against the Dart-derived expected values (SC-005; R-010) — in `glp_gleam/test/glp/runtime/parity_test.gleam`.

### Implementation for User Story 3

- [X] T021 [US3] Implement `suspend_on_writer(Heap, Int, Suspension) -> Result(Heap, HeapError)`: attach to the `WriterCell` suspension list, preserving the reader pairing; `NotAWriter` otherwise (← `suspendOnWriter`; FR-008) in `glp_gleam/src/glp/runtime/heap.gleam`. Depends on T007.
- [X] T022 [US3] Add activation production to `bind_writer` (armed suspensions → `GoalRef` list, then disarm — guarding double-activation) and suspension **forwarding** to `bind_writer_to_var` (armed → target writer) in `heap.gleam` (← `_walkAndActivate` / `_forwardSuspensions`; FR-008). Depends on T012, T013, T021.
- [X] T023 [US3] Author the Dart-derived parity corpus expected-values and **cross-validate each scenario against the Dart `runtime` source-of-truth, recording the command + observed output** per scenario (F1-dossier evidence convention) into a checked-in evidence note (e.g. `specs/034-glp-gleam-core-terms-and-heap/parity-evidence.md`) — **REQUIRED**, not a soft SHOULD: it is the auditable basis for the FR-009/SC-005 "matches Dart" claim (F3). `gleam test` stays hermetic (expected values are baked in; no Dart dependency in the suite — R-010). Feeds T020.
- [X] T024 [US3] Run `gleam test` + `./smoke.sh`; make `suspension_test` + `parity_test` green; confirm additivity.

**Checkpoint**: all three stories independently functional; observable parity to Dart pinned.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T025 [P] Verify additivity (SC-007): `git diff --stat -- glp_runtime/ glp_runtime_net/ out/csharp/ codeconv/` is EMPTY; `git status --porcelain glp_gleam/` shows only new `src/glp/runtime/**` + `test/glp/runtime/**` (+ filled `runtime.gleam`); `glp_gleam/build/` is gitignored (no BEAM/build artifacts staged).
- [X] T026 [P] Verify `gleam_otp` absent from `glp_gleam/manifest.toml` (0 occurrences — SC-006) and the only runtime dep terms/heap/unify use is `gleam_stdlib`.
- [X] T027 [P] Module-doc each `glp/runtime/*.gleam` with its Dart source-of-truth pointer; run the `quickstart.md` walkthrough end-to-end.
- [X] T028 Final clean-checkout gate (SC-006): in `glp_gleam/` `rm -rf build && gleam build --target erlang && gleam test && ./smoke.sh` — zero errors, suite green (≥1 test, 0 failures).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (P1)**: no deps — start immediately.
- **Foundational (P2)**: after Setup — **BLOCKS all user stories**. (T004,T005 ∥; T006 needs T004,T005; T007 needs T006; T008 needs T004–T007.)
- **US1 (P3)**: after Foundational. T011/T012/T013 each need T007; tests T009,T010 ∥ first.
- **US2 (P4)**: after US1 (unify calls `deref`/`bind_*`). T016 needs T011–T013; T017 needs T016.
- **US3 (P5)**: after US1 (extends `bind_*`). T021 needs T007; T022 needs T012,T013,T021. *Independent of US2.*
- **Polish (P6)**: after the desired stories.

### User-story independence

- **US1** stands alone (MVP). **US2** builds on US1's heap but is independently testable. **US3** extends
  US1's `bind_*` and is independent of US2 (US3 can proceed right after US1). US2 and US3 may proceed in
  parallel once US1 is done.

### Within each story

- Tests written first and FAIL → then implementation → re-test green. Models (Phase 2) before operations.

### Parallel opportunities

- Setup: T002 ∥ T003. Foundational: T004 ∥ T005. Per-story tests: T009 ∥ T010; T019 ∥ T020. Polish: T025 ∥ T026 ∥ T027.
- After US1, a second pair of hands can take US2 (unify) while another extends US3 (suspension).

---

## Parallel Example: User Story 1

```text
# Write US1 tests together (they must fail first):
Task: "terms_test.gleam — all 9 term kinds (SC-001)"        # T009
Task: "heap_test.gleam — allocate/deref/bind/compress (SC-002)" # T010
```

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (CRITICAL — blocks all) → 3. Phase 3 US1 →
4. **STOP & VALIDATE**: `gleam test` + `smoke.sh` green; GLP data representable + a logic variable
   bound and read back on BEAM. Demoable MVP.

### Incremental delivery

US1 (MVP) → US2 (three-valued unification) → US3 (suspension + Dart parity corpus). Each adds value
without breaking the previous; additivity (FR-011) re-verified at each story's final task.

---

## Notes

- [P] = different files, no incomplete-task dependency. [Story] label = traceability to spec user stories.
- Faithful port only — any gap in the authoritative specs is **reported, not invented around** (FR-012; Bug-Protocol).
- Commit only `glp_gleam/**` files this session (Constitution VII); never `git add -A`.
- All build/test on WSL Ubuntu with the pinned toolchain; `glp_gleam/build/` never committed.
