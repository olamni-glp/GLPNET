---
description: "Task list — Gleam Port Source & Toolchain / AtomVM Feasibility Spike"
---

# Tasks: Gleam Port — Source & Toolchain / AtomVM Feasibility Spike

**Input**: Design documents from `/specs/031-gleam-port-spike/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: This is a research/decision spike. The spec requests **reproducible command+observed-output evidence** (FR-009), **not** an automated test suite or TDD. No separate automated-test tasks are generated; verification is the reproducibility re-runs (T015, T020) and the contract self-checks (T021).

**Organization**: Tasks are grouped by the four user stories so each is independently deliverable. All durable outputs live under `docs/research/gleam-atomvm/`. The spike creates **no** `glp_gleam/` subtree and modifies **no** GLP runtime / programs / roadmap (FR-011).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1–US4 (maps to spec.md user stories)
- Include exact file paths in descriptions

## Path Conventions

Spike deliverables (created during implementation):
- `docs/research/gleam-atomvm/dossier.md`
- `docs/research/gleam-atomvm/toolchain-inventory.md`
- `docs/research/gleam-atomvm/hello-glp-term/` (self-contained Gleam project)

Contracts the deliverables satisfy live in `specs/031-gleam-port-spike/contracts/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the deliverable scaffolding and the evidence convention.

- [ ] T001 Create `docs/research/gleam-atomvm/` and a section-skeleton `docs/research/gleam-atomvm/dossier.md` keyed to `specs/031-gleam-port-spike/contracts/dossier-outline.md` (all 7 required sections as headed placeholders)
- [ ] T002 [P] Create `docs/research/gleam-atomvm/toolchain-inventory.md` skeleton with all fields from `specs/031-gleam-port-spike/contracts/toolchain-inventory.schema.md`, including the evidence-recording convention header (command + observed output + exact version — FR-009)
- [ ] T003 [P] Scaffold the Gleam project skeleton at `docs/research/gleam-atomvm/hello-glp-term/` (`gleam.toml`, `src/`, `test/`, `README.md` stub) per `specs/031-gleam-port-spike/contracts/hello-glp-term.contract.md` — placeholder only, not yet implemented

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: A working, version-pinned toolchain — the cross-cutting prerequisite for every "run" story (US2 smoke, US3 AtomVM, US4 JS) and for US1's smoke-backed architectural-fit finding.

**⚠️ CRITICAL**: No smoke/run task can begin until this phase is complete.

- [ ] T004 Stand up the Gleam + Erlang/OTP toolchain on the primary **Windows** environment; if infeasible, fall back to **WSL/Linux** then sibling **Mac**, and record which environment was used as a first-class finding (research R1; spec Edge case "toolchain will not install on Windows")
- [ ] T005 Pin and record **exact** versions (`gleam --version`, `erl +V`, `rebar3 version`) and the reproducible setup commands into `docs/research/gleam-atomvm/toolchain-inventory.md` (FR-003; SC-002; contract `toolchain-inventory.schema.md`) — depends on T002, T004

**Checkpoint**: Toolchain proven and pinned — smoke and runtime attempts can begin.

---

## Phase 3: User Story 1 — Ratifiable source-language decision & go/no-go (Priority: P1) 🎯 MVP

**Goal**: A dossier that names exactly one source basis (Dart / C# / file-by-file), backs it with a criteria table and a smoke-backed architectural-fit assessment, names downstream re-scopes, and ends with a single go/no-go verdict.

**Independent Test**: A reviewer reads **only** `dossier.md` and can state the recommended source basis + one-sentence rationale, see the criteria table, see the architectural-fit risk findings, and act on the go/no-go — without any other document. (US1 Independent Test)

> The source-decision core (T006–T008) is the true MVP and is independent of the smoke. The architectural-fit (T009) and final verdict (T012) **integrate** US2's smoke and US3/US4's findings — see Dependencies.

- [ ] T006 [US1] Inventory every source-candidate root and record per-criterion evidence into the dossier's source-decision section: Dart `glp_runtime/` (single coherent tree, ~151 files, current 2026-06-08) vs C# `glp_runtime_net/` (hand-port + own REPL) + `csharp/` (feature modules) + `out/csharp/` (regenerable scaffold mirror) — over {health & currency, structural fit to Gleam, conversion effort, divergence} (FR-001; research R5; data-model E4)
- [ ] T007 [US1] Write the source-criteria table in `dossier.md` (rows = {Dart, C#, file-by-file replication}; columns = the four criteria), surfacing Dart↔C# divergence **explicitly as a criterion**, not assumed parity (FR-001; spec Edge case; contract `dossier-outline.md` §2.1–2.2) — depends on T006
- [ ] T008 [US1] State exactly one source recommendation + a one-sentence ratifiable rationale in `dossier.md`, confirming or overturning the roadmap's initial C#-lean with evidence (FR-001; SC-001; contract §2.3) — depends on T007
- [ ] T009 [US1] Write the architectural-fit section in `dossier.md`: the mutable-heap/immutability mismatch (**backed by the running unbound→bound demonstration from T013/T014**, citing the smoke's observed output — not analysis alone) and the FCP-concurrency/BEAM-process opportunity, each with its bearing on the recommendation (FR-006; SC-006; contract §4) — depends on T008, T014
- [ ] T010 [US1] Write the downstream re-scope notes in `dossier.md` naming F5 bytecode runner, F6 compiler/loader, and F9 link layer, each with a recommended re-scope **or** "confirmed unchanged" (FR-007; SC-005; contract §5) — depends on T009
- [ ] T011 [US1] Write the downstream-handoff block in `dossier.md` (chosen source basis + assumed `glp_gleam/` layout/conventions + toolchain versions for F2/F3) (FR-008; SC-004; contract §6) — depends on T008, T005
- [ ] T012 [US1] Write the executive summary + the single **go / no-go / go-with-revisions** verdict (revisions enumerated if applicable) in `dossier.md` §1/§7, ensuring the dossier is self-sufficient (FR-010; SC-001; SC-005; contract §1,§7) — depends on T009, T017, T019

**Checkpoint**: The dossier settles the source question and carries a go/no-go — independently reviewable (MVP).

---

## Phase 4: User Story 2 — Toolchain stood up + hello-GLP-term on Erlang/BEAM (Priority: P1)

**Goal**: A minimal Gleam module constructs a representative GLP term and demonstrates one unbound→bound transition, compiles to BEAM, and runs on Erlang with observed, reproducible output.

**Independent Test**: On a clean checkout, a second person follows the recorded commands; the module compiles to BEAM, runs on Erlang, and emits the same expected term result. (US2 Independent Test)

- [ ] T013 [US2] Implement `docs/research/gleam-atomvm/hello-glp-term/src/hello_glp_term.gleam`: construct a representative GLP term (≥1 compound/structure term + 1 unbound-variable analogue) and exactly **one** unbound→bound transition with a reader observing the bound value — primary **process/state-holder** model (Gleam `gleam_otp`/`gleam_erlang` actor) plus a **functional sibling** for contrast; do NOT implement full unification, suspension/reactivation scheduling, bytecode execution, or any perf measurement (FR-004; research R4; contract `hello-glp-term.contract.md`) — depends on T003, T004
- [ ] T014 [US2] Compile to BEAM and run on Erlang; capture the verbatim observed output (the documented term representation + the observed bound value); record the exact build+run commands and output into `docs/research/gleam-atomvm/hello-glp-term/README.md` (FR-004; FR-009; SC-002; US2 acceptance #2) — depends on T013
- [ ] T015 [US2] Re-run from a clean state to confirm an identical observed result and fill the Erlang/BEAM row evidence for the build-target matrix (US2 acceptance #3; SC-002) — depends on T014

**Checkpoint**: Toolchain "observed working," not merely "claimed feasible"; BEAM matrix row evidenced.

---

## Phase 5: User Story 3 — AtomVM BEAM-subset feasibility verdict (Priority: P2)

**Goal**: An evidence-backed verdict for whether the smoke runs on an AtomVM host build, or the specific subset limitation / bring-up blocker that prevents it.

**Independent Test**: The matrix's AtomVM row carries a verdict (viable / partially / not viable) plus evidence — an observed smoke result on an AtomVM host build, the named BEAM/OTP-subset limitation, or the recorded bring-up blocker. (US3 Independent Test)

- [ ] T016 [US3] Attempt the smoke on an AtomVM host build — **effort-bounded**: prefer a prebuilt/generic host release → only a time-boxed source build of `generic_unix` if no prebuilt runs → else record the **bring-up blocker** (record the budget used); capture outcome/output or the named subset limitation; record the AtomVM build identity into `toolchain-inventory.md` (FR-005; research R3; US3 acceptance #1) — depends on T014, T004
- [ ] T017 [US3] Write the AtomVM build-target-matrix row in `dossier.md` (verdict + evidence + `host build` caveat) and state the implication for the heavy downstream features (bytecode runner, compiler/loader, link layer) (FR-005; FR-007; US3 acceptance #2; contract `build-target-matrix.schema.md`) — depends on T016

**Checkpoint**: AtomVM row decided with evidence; epic can proceed on BEAM regardless.

---

## Phase 6: User Story 4 — Complete build-target matrix incl. JavaScript fallback (Priority: P3)

**Goal**: The full {Erlang/BEAM, AtomVM, JavaScript} matrix — every row a verdict + evidence; JS evaluated as a fallback.

**Independent Test**: Every target row has a verdict and ≥1 supporting evidence; no cell is "unknown" without a recorded reason; the JS row states whether JS is a viable fallback and its cost vs the BEAM path. (US4 Independent Test)

- [ ] T018 [US4] Evaluate the Gleam **JavaScript** backend (`gleam build --target javascript` + run, or authoritative citation) and record whether JS is a viable fallback for GLP and its cost relative to the BEAM path (US4 acceptance #2; research R7) — depends on T013, T004
- [ ] T019 [US4] Assemble the complete 3-row build-target matrix and embed it in `dossier.md` — every cell a verdict + ≥1 evidence + host-vs-hardware caveat, **no unexplained "unknown"** (FR-002; SC-003; contract `build-target-matrix.schema.md`) — depends on T015, T017, T018

**Checkpoint**: Matrix complete; dossier's verdict (T012) can be finalized.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Reproducibility and contract conformance.

- [ ] T020 [P] Run the `specs/031-gleam-port-spike/quickstart.md` validation end-to-end as the second-person reproducibility check; fix any gap in the recorded commands/versions (SC-002; quickstart Done-when)
- [ ] T021 [P] Self-check `dossier.md` against the binary acceptance checklist in `contracts/dossier-outline.md` and the smoke against `contracts/hello-glp-term.contract.md` (SC-001; SC-005; SC-006)
- [ ] T022 Final consistency & scope pass: confirm exactly one source recommendation + one verdict; every "it works" claim has command+output or citation; **no `glp_gleam/` subtree created**; **no GLP runtime / programs / roadmap modified** (FR-009; FR-011)

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: after Setup; **blocks** all run/smoke tasks (T013–T019) and US1's T009/T011/T012.
- **User stories (Phase 3–6)**: after Foundational. US1's MVP core (T006–T008) needs only the source inventory; US1's T009/T012 integrate later stories (see below).
- **Polish (Phase 7)**: after the desired stories.

### Cross-story dependencies (real, by design — a single incremental dossier)

- **T009** (US1 arch-fit, mutable-heap finding) ⟵ **T014** (US2 smoke run) — SC-006 requires the finding be smoke-backed.
- **T012** (US1 final verdict) ⟵ **T009**, **T017** (US3 AtomVM verdict), **T019** (US4 matrix) — the single go/no-go integrates all findings.
- **T011** (US1 handoff) ⟵ **T005** (pinned versions), **T008** (source basis).
- **T016** (US3 AtomVM attempt) ⟵ **T014** (built BEAM artifact).
- **T019** (US4 matrix assembly) ⟵ **T015** (BEAM), **T017** (AtomVM), **T018** (JS).

### Recommended execution order

Setup → Foundational → **US2 smoke (T013–T015)** → **US1 source core (T006–T008)** → **US3 (T016–T017)** → **US4 (T018–T019)** → **US1 finalization (T009–T012)** → Polish. (US1 is presented first by priority, but its smoke-backed sections finalize after US2–US4 produce evidence.)

### Parallel opportunities

- **T002, T003** (Setup) — different files, run in parallel.
- **T020, T021** (Polish) — independent checks, run in parallel.
- Cross-story: once the toolchain (T004/T005) is up, **US2 smoke (T013)** and **US1 source inventory (T006)** can proceed in parallel (different files: the Gleam project vs the dossier source section).

---

## Parallel Example: Setup

```text
# T002 and T003 touch different files with no interdependency:
Task: "Create toolchain-inventory.md skeleton (contract toolchain-inventory.schema.md)"
Task: "Scaffold hello-glp-term/ Gleam project skeleton (contract hello-glp-term.contract.md)"
```

## Parallel Example: after Foundational

```text
# Independent files — the dossier vs the Gleam project:
Task: "T006 [US1] Source-candidate inventory into dossier.md source section"
Task: "T013 [US2] Implement hello_glp_term.gleam term + one unbound→bound bind"
```

---

## Implementation Strategy

### MVP First (User Story 1, smoke-backed)

1. Phase 1 Setup → Phase 2 Foundational (toolchain pinned).
2. US2 smoke (T013–T015) — produces the running unbound→bound evidence.
3. US1 source core (T006–T008) + arch-fit (T009) + a preliminary go/no-go.
4. **STOP and VALIDATE**: a reviewer can state the source basis + rationale + arch-fit risk + go/no-go from `dossier.md` alone (US1 Independent Test).

### Incremental Delivery

1. Setup + Foundational → toolchain ready.
2. + US2 → BEAM smoke observed working (independently valuable even if AtomVM is infeasible).
3. + US1 core → source decision + go/no-go (MVP dossier).
4. + US3 → AtomVM verdict; refine downstream re-scope + final verdict.
5. + US4 → complete matrix incl. JS fallback.
6. Polish → reproducibility + contract conformance.

---

## Notes

- [P] = different files, no incomplete-task dependency.
- This is a spike: deliverables are **documents + one throwaway-grade Gleam smoke**, not production code. Honor FR-011 — no `glp_gleam/` subtree, no runtime/programs/roadmap edits.
- Commit per task or logical group; commit only spike files (Constitution VII).
- Every verdict/"it works" claim carries command+output or an authoritative citation (FR-009); no matrix cell "unknown" without a recorded reason (SC-003).
