---
description: "Task list for glp_gleam subtree scaffold (feature 033)"
---

# Tasks: glp_gleam subtree scaffold

**Input**: Design documents from `/specs/033-glp-gleam-subtree-scaffold/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓, quickstart.md ✓

**Tests**: No TDD/test-first was requested. The single gleeunit **smoke test** is a feature
*deliverable* (FR-003), authored as an implementation task in US1 — not a red-first test phase.

**Environment**: every `gleam`/`erl` command runs **under WSL Ubuntu** with the F1-pinned toolchain
(Gleam 1.17.0 · OTP 25.3.2.8 · rebar3 3.19.0), verified reachable. Invoke as:
`wsl.exe -e bash -lc 'cd glp_gleam && <command>'`.

**Format**: `[ID] [P?] [Story?] Description with exact path`. [P] = different file, no dependency on
incomplete work.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the project skeleton and static config (no toolchain needed yet).

- [X] T001 Create the `glp_gleam/` directory tree at the repo root (`glp_gleam/src/glp/`, `glp_gleam/test/`) per `specs/033-glp-gleam-subtree-scaffold/contracts/project-layout.md` — sibling to `glp_runtime/` and `glp_runtime_net/` (FR-001).
- [X] T002 Author `glp_gleam/gleam.toml` — `name = "glp_gleam"`, version/description/licences, `[dependencies] gleam_stdlib`, `gleam_erlang`, `[dev-dependencies] gleeunit`, **no `gleam_otp`**, ranges per `contracts/dependency-lock.md` (FR-001, FR-005, FR-006).
- [X] T003 [P] Author `glp_gleam/.gitignore` with `*.beam`, `*.ez`, `/build`, `erl_crash.dump` (FR-010).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Lock dependencies so any build is reproducible. **⚠️ BLOCKS all build/test tasks.**

- [X] T004 Verify the WSL toolchain: `gleam --version` == `1.17.0`, `erl` OTP release == `25`, `rebar3` present; **fail loudly** with the required versions if not (edge case "toolchain absent/wrong version").
- [X] T005 Generate and **commit** `glp_gleam/manifest.toml` by running `gleam deps download` under WSL (cwd `glp_gleam/`), then verify it locks `gleam_stdlib` 1.0.3 / `gleam_erlang` 1.3.0 / `gleeunit` 1.11.0 and `grep -c gleam_otp manifest.toml` == `0` (FR-005, FR-010, SC-004). Manifest is git-tracked; `build/` is not.

**Checkpoint**: deps locked → builds are reproducible; user stories can begin.

---

## Phase 3: User Story 1 — A buildable, testable Gleam subtree exists (Priority: P1) 🎯 MVP

**Goal**: From a fresh checkout on the pinned toolchain, the subtree **builds to Erlang/BEAM and
its test suite passes**, with no ported runtime code present.

**Independent Test**: `gleam build --target erlang` (0 errors) then `gleam test --target erlang`
(≥1 test, 0 failures), under WSL — the SC-001/SC-002 gate.

- [X] T006 [US1] Author `glp_gleam/test/glp_gleam_test.gleam` — a gleeunit smoke test with ≥1 assertion exercising the build-and-run path (FR-003, SC-002).
- [X] T007 [US1] Build green: `gleam build --target erlang` under WSL → 0 errors (FR-002, SC-001). Here resolve the R-006 empty-src question against the **real compiler**: if `gleam build` requires ≥1 `src/` module, add a single minimal `////`-doc module (formalized into the full 8 in US2) — decided by the compiler, not guessed.
- [X] T008 [US1] Test green: `gleam test --target erlang` under WSL → ≥1 test, 0 failures (SC-002).

**Checkpoint**: MVP delivered — a buildable, testable empty-but-building Gleam subtree. Deployable.

---

## Phase 4: User Story 2 — Skeleton mirrors the authoritative Dart subsystem structure (Priority: P2)

**Goal**: One empty-but-building placeholder module per authoritative Dart subsystem under
`src/glp/`, 1:1 with `glp_runtime/lib/`, so downstream ports map with no restructuring.

**Independent Test**: enumerate `src/glp/*.gleam` → exactly the 8 subsystems; rebuild → all compile.

- [X] T009 [P] [US2] Create `glp_gleam/src/glp/analysis.gleam` — a `////` module-doc naming the subsystem + its Dart source-of-truth path (`glp_runtime/lib/analysis/`); no exported definitions (R-006).
- [X] T010 [P] [US2] Create `glp_gleam/src/glp/bytecode.gleam` (same discipline; Dart `glp_runtime/lib/bytecode/`).
- [X] T011 [P] [US2] Create `glp_gleam/src/glp/compiler.gleam` (Dart `glp_runtime/lib/compiler/`).
- [X] T012 [P] [US2] Create `glp_gleam/src/glp/engine.gleam` (Dart `glp_runtime/lib/engine/`).
- [X] T013 [P] [US2] Create `glp_gleam/src/glp/link.gleam` (Dart `glp_runtime/lib/link/`).
- [X] T014 [P] [US2] Create `glp_gleam/src/glp/lint.gleam` (Dart `glp_runtime/lib/lint/`).
- [X] T015 [P] [US2] Create `glp_gleam/src/glp/multiagent.gleam` (Dart `glp_runtime/lib/multiagent/`).
- [X] T016 [P] [US2] Create `glp_gleam/src/glp/runtime.gleam` (Dart `glp_runtime/lib/runtime/`).
- [X] T017 [US2] Rebuild green with all 8 placeholders: `gleam build --target erlang` under WSL → every placeholder compiles even though nothing imports it (edge case "imported but unused must still compile") (SC-003).
- [X] T018 [US2] Verify the subsystem set is **exactly** the 8 and equals `glp_runtime/lib/` dir names (set equality both ways) — no 9th, none missing (FR-004, SC-003); and assert every module path segment matches `^[a-z][a-z0-9_]*$` and is non-reserved — the skeleton contains no illegal segment (FR-006, edge case "illegal module/namespace segment").

**Checkpoint**: US1 + US2 — a building skeleton 1:1 with the Dart source-of-truth.

---

## Phase 5: User Story 3 — Smoke gate exists + conversion tooling recognizes the subtree (Priority: P3)

**Goal**: A local WSL-runnable `smoke.sh` gates build+test green; the subtree is recognized by the
Dart→Gleam conversion data flow **without any stage-tool source change**.

**Independent Test**: run `smoke.sh` under WSL (gates green); confirm `git diff` shows zero edits
under `codeconv/src/codeconv/tools/` and zero new `langpairs/` files (FR-008/SC-006).

- [X] T019 [US3] Author `glp_gleam/smoke.sh` per `contracts/build-test-smoke.md`: resolve own dir + `cd`; **loud toolchain check** (Gleam 1.17.0 · OTP 25) failing with required versions; `gleam build --target erlang`; `gleam test --target erlang`; exit 0 iff both green, non-zero otherwise; Erlang/BEAM target only (FR-007).
- [X] T020 [US3] Run `bash glp_gleam/smoke.sh` under WSL → exit 0 on green; confirm it fails non-zero if the toolchain check is tripped (SC-005, edge case).
- [X] T021 [US3] **Establish + verify** FR-008 recognition (config-only). (a) *Establish/document* the lightweight recognition path: record — in `glp_gleam/README.md` and against `contracts/conversion-recognition.md` — how the Dart→Gleam data flow sees `glp_gleam/` purely via `codeconv init` → `codeconv.workspace_settings` (active pair `dart_gleam`; `target_rel_root`/`output_rel` roots), mirroring how `glp_runtime_net`/`out/csharp` participate for the C# pipeline — no stage-tool source. (b) *Verify the negative*: `git diff --name-only` is empty under `codeconv/src/codeconv/tools/{init,discover,scaffold,mirror}/` and adds no `codeconv/src/codeconv/langpairs/` file (FR-008, SC-006; R-004). No code change.
- [X] T022 [US3] Author `glp_gleam/README.md` — one screen: purpose (F3 skeleton; where F4+ land), build/test commands (SC-001/SC-002), the `smoke.sh` gate, and a pointer to `docs/research/gleam-atomvm/dossier.md` §6 (reference, don't duplicate — Principle VIII).

**Checkpoint**: all three stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T023 [P] Verify **additive-only** (FR-009, SC-006): the only tracked changes are under `glp_gleam/` (plus the `specs/033-...` docs); no existing subtree's source changed; existing gates' outcomes unchanged.
- [ ] T024 Run the `quickstart.md` walkthrough end-to-end under WSL — steps 1–6 all green (SC-001..SC-006 spot-check).
- [ ] T025 [P] Final artifact-hygiene check: `git status --porcelain glp_gleam/build` empty (ignored); `manifest.toml`, `gleam.toml`, `src/**`, `test/**`, `smoke.sh`, `README.md` tracked (FR-010).

---

## Dependencies & Execution Order

### Phase dependencies
- **Setup (P1: T001–T003)**: no deps — start immediately. T003 is [P] with T002.
- **Foundational (P2: T004–T005)**: needs `gleam.toml` (T002). **Blocks all build/test** (T007, T008, T017, T020, T024).
- **US1 (P3: T006–T008)**: needs Foundational. The MVP.
- **US2 (P4: T009–T018)**: needs Foundational; T009–T016 are independent of US1 (different files) and may proceed in parallel with US1, but the **rebuild** T017 should follow US1's first green build for a clean attribution. T018 after T017.
- **US3 (P5: T019–T022)**: smoke (T019–T020) needs a building project (US1). T021/T022 are doc/verify — independent.
- **Polish (P6)**: after the desired stories.

### User-story independence
- **US1** is self-contained (build+test green). **US2** adds the 8 modules (compile-only; no US1 dep beyond the project). **US3** adds gate + recognition (needs a building project for the smoke run; the recognition check is independent).

### Within a story
- Author file → build → verify. US2's 8 creations (T009–T016) are mutually parallel.

---

## Parallel Opportunities

- **Setup**: T002 ∥ T003.
- **US2**: T009–T016 (8 placeholder modules, distinct files) all run in parallel; then T017 (single build) → T018 (verify).
- **US1 ∥ US2 authoring**: T006 (smoke test) and T009–T016 (placeholders) touch different files and can be authored together; serialize only at the shared `gleam build` steps.
- **Polish**: T023 ∥ T025.

### Parallel example: US2 placeholder modules
```bash
# Author all 8 subsystem placeholders together (distinct files):
Task: "Create glp_gleam/src/glp/analysis.gleam"
Task: "Create glp_gleam/src/glp/bytecode.gleam"
Task: "Create glp_gleam/src/glp/compiler.gleam"
Task: "Create glp_gleam/src/glp/engine.gleam"
Task: "Create glp_gleam/src/glp/link.gleam"
Task: "Create glp_gleam/src/glp/lint.gleam"
Task: "Create glp_gleam/src/glp/multiagent.gleam"
Task: "Create glp_gleam/src/glp/runtime.gleam"
```

---

## Implementation Strategy

### MVP first (US1 only)
1. Setup (T001–T003) → 2. Foundational (T004–T005) → 3. US1 (T006–T008) → **STOP & VALIDATE**:
build + test green on BEAM under WSL. This is the literal roadmap acceptance gate.

### Incremental delivery
1. Setup + Foundational → reproducible project.
2. US1 → buildable + green smoke (MVP).
3. US2 → 8 placeholders, 1:1 with Dart, all compile.
4. US3 → smoke gate + config-only conversion recognition.
5. Polish → additive-only + artifact hygiene + quickstart validation.

---

## Notes
- Every build/test runs **under WSL**; Gleam is not Windows-native here.
- `manifest.toml` is generated by `gleam` and committed; never hand-edited.
- FR-008 recognition requires **zero** codeconv stage-tool edits — it is config (`workspace_settings`)
  + the existing F2 `dart_gleam` pair. If any task seems to require editing a stage tool, STOP — that
  violates FR-008/SC-006 and the F2 boundary.
- Commit after each story/logical group, scoped by name (Principle VII); no `git add -A`.
- The smoke is a **separate** WSL gate, not embedded in `test/run_all_tests.sh` (research.md R-003,
  non-blocking owner-awareness flag).

**Total: 25 tasks** — Setup 3 · Foundational 2 · US1 3 · US2 10 · US3 4 · Polish 3.
