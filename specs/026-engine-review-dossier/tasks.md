---
description: "Task list for Engine Review + Refactoring Design Dossier"
---

# Tasks: Engine Review + Refactoring Design Dossier

**Input**: Design documents from `/specs/026-engine-review-dossier/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/dossier-outline.md, contracts/roadmap-candidate.md, quickstart.md

**Tests**: NONE. This feature ships no executable code (FR-015, SC-006); acceptance is **checklist-against-spec** (Success Criteria SC-001..SC-010). No test tasks are generated.

**Organization**: Tasks are grouped by the four user stories. The single deliverable is `docs/research/repl-engine-separation/design-dossier.md`; each authoring task targets a distinct **section anchor** of that file (the single-doc analog of "different files"), so `[P]`-marked section tasks are content-independent and may be drafted concurrently then assembled.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Content-independent (distinct section / distinct source tree) — may proceed concurrently
- **[Story]**: US1 / US2 / US3 / US4 (maps to spec.md user stories)
- Exact paths included. The dossier path is `docs/research/repl-engine-separation/design-dossier.md` (abbreviated **DOSSIER** below).

## Path Conventions

Documentation deliverable — no `src/`/`tests/` trees. Artifacts:
- **DOSSIER** = `docs/research/repl-engine-separation/design-dossier.md` (net-new, authored here)
- Read-only inputs: `docs/research/repl-engine-separation/{investigation,requirements,feature-definition,llvm-feasibility,research-programme}.md`
- Read-only subject code (cited, never modified): `out/csharp/`, `csharp/glp_link/`, `codeconv/src/codeconv/marathon/`, `glp_runtime/`, `programs/self.glp`

---

## Phase 1: Setup

**Purpose**: Establish the dossier skeleton and confirm inputs.

- [X] T001 Create **DOSSIER** skeleton with the 13 section anchors (§0–§12) exactly per `specs/026-engine-review-dossier/contracts/dossier-outline.md`, plus a "Source Inputs (read-only)" block and a placeholder for the consolidated classification table.
- [X] T002 [P] Verify every read-only source-input path exists (`investigation.md`, `requirements.md`, `feature-definition.md`, `llvm-feasibility.md`, `research-programme.md`) and list them in the **DOSSIER** "Source Inputs" block with one-line roles.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build the shared evidence base every section cites. Re-verify all step-1 claims against current code (FR-016) and produce the reuse/refactor/net-new classification (FR-014, SC-008).

**⚠️ CRITICAL**: No user-story section can be authored until the citations behind it are re-verified here.

- [X] T003 [P] Re-verify **seam / result / wire** citations in `out/csharp` (`lib/engine/glp_engine.cs`, `bin/glp_repl.cs`, `lib/runtime/scheduler.cs`, `runner.cs`, `opcodes.cs`/`opcodes_v2.cs`, `codegen.cs`, `terms.cs`, `result.cs`) — record current `file:line` for each claim used by §1–§3, §9.
- [X] T004 [P] Re-verify **feature-025 link-layer** citations in `csharp/glp_link` (`reliability/FrameCodec.cs`, `transports/TcpTransport.cs`, `lib/multiagent/payload_serializer.cs`, `primitives/LinkKernels.cs`/`LinkPump.cs`, `LinkEstablish.cs`, `LinkRegistry.cs`, `seam/*`, `ResourceSnapshot.cs`) — `file:line` for §3, §4, §6.
- [X] T005 [P] Re-verify **persistence/state** citations: live-state inventory in `out/csharp/lib/runtime/runtime.cs` + `heap_fcp.cs`, and the durable-store template `codeconv/src/codeconv/marathon/{store.py,checkpoint.py}` — `file:line` for §5, §6.
- [X] T006 [P] Re-verify **Dart parity + GLP** citations: `glp_runtime/lib/engine/glp_engine.dart` (`ExecutionResult`) and `programs/self.glp` link wrappers (`serve/2`, `request_listener`, `Link`, `link_send`/`link_recv`, `mwm`) — `file:line` for §2.6, §4, §7.
- [X] T007 Consolidate the **reuse / refactor / net-new classification table** (seed from `research.md` "net-new vs reuse map") against the T003–T006 re-verified citations; write it into the **DOSSIER** as the shared table §1–§8 reference (FR-014, SC-008, INV-2).

**Checkpoint**: Evidence base ready — user-story authoring can begin.

---

## Phase 3: User Story 1 — Decision-final design for successor authors (Priority: P1) 🎯 MVP

**Goal**: All seven named design areas present, each covered by a forced design or fully-researched options, tagged reuse/refactor/net-new with `file:line`.

**Independent Test**: Pick any design area — its **DOSSIER** section gives the contract shape, the code locations it affects, and either a forced design or options-with-consequences; the wire-crossing design is locatable without opening engine source (SC-001, SC-005, SC-008).

- [X] T008 [P] [US1] Author **DOSSIER** §1 **Seam contract** — what crosses each direction; the components computed-but-dropped at the `ExecutionResult` boundary (var→writer map, suspended-goal detail, captured output) with `file:line`; classification tag (FR-002).
- [X] T009 [P] [US1] Author **DOSSIER** §2 **Binary wire shapes** — client→engine payload and the net-new engine→client **result envelope** with the complete field set (status, bindings, var-name→writer map, suspended-goal detail, captured/streamed output, errors) and the **unbound-variable-in-suspended-result** encoding (FR-003; US1 scenario 1).
- [X] T010 [P] [US1] Author **DOSSIER** §3 **Wire reuse decision** — `FrameCodec`/`TcpTransport`/`ILinkTransport` reused as-is; dedicated IL + result codecs net-new; rationale for each; why not extend `PayloadSerializer` (FR-004).
- [X] T011 [P] [US1] Author **DOSSIER** §4 **Control-program startup + client model** — `AfterEngineCreated` insertion seam; front-end as one client; single-engine/multi-client implications; the **multi-accept** hard prerequisite (FR-005).
- [X] T012 [P] [US1] Author **DOSSIER** §5 **Liveness / crash-signal / restart model** — OS liveness signal, unrecoverable-state crash signal, supervision/restart, host-layer placement under FR-057 (FR-006).
- [X] T013 [P] [US1] Author **DOSSIER** §6 **Persistent-vs-ephemeral state model** — per-component classification table, re-establish-from-definition rule, DB-abstraction API shape (MarathonStore-mirrored), bootstrap behaviour, restore-and-resume behaviour (FR-007).
- [X] T014 [P] [US1] Author **DOSSIER** §7 **Mailbox decision** — OS-level vs GLP-language, for MVP and long-term target, with rationale (FR-008).
- [X] T015 [P] [US1] Author **DOSSIER** §8 **MVP slice(s)** — each naming exactly the net-new capabilities it depends on and what it defers; mark one as advisory recommendation (owner decides) (FR-009, SC-007).
- [X] T016 [US1] Verify US1: 7/7 design areas present; each forced-design-or-options; each tagged + cites ≥1 `file:line`; §2 wire design locatable from the dossier alone (SC-001, SC-005, SC-008).

**Checkpoint**: MVP — the design areas are decision-ready.

---

## Phase 4: User Story 2 — Premise/code mismatches reconciled (Priority: P1)

**Goal**: Both requirement/code premise mismatches reconciled with as-built code locations.

**Independent Test**: For each premise, the dossier states (i) the requirement assumption, (ii) the as-built reality + `file:line`, (iii) the resolving decision, (iv) the downstream consequence (SC-002, US2).

- [X] T017 [P] [US2] Author **DOSSIER** §9 reconciliation: **compiler location** — assumption (parser-in-front-end / compiled-IL-on-wire) vs as-built (Lexer/Parser/TypeChecker/Compiler engine-internal, `glp_engine.cs` `file:line`); decision (MVP carries source text; compiler relocation a deliberate follow-up); which successor features it splits (FR-010).
- [X] T018 [P] [US2] Author **DOSSIER** §9 reconciliation: **runtime-IL generation** — assumption (engine generates new IL at runtime) vs as-built (no bytecode synthesised; runtime goal-term assembly + dispatch via `_activate` against `ModuleTerm`-wrapped `BytecodeProgram`, `file:line`); consequence for the persistence design (FR-010).
- [X] T019 [US2] Verify US2: 2/2 premises reconciled, each with as-built `file:line` and a downstream consequence (SC-002).

---

## Phase 5: User Story 3 — Every open question presented as owner-decision options (Priority: P2)

**Goal**: 100% of step-1 open questions presented as 2–5 mutually-exclusive, evidence-grounded, concise options; none recorded as settled.

**Independent Test**: Enumerate `investigation.md` §8.3 open questions; each appears in §10 as an option set with consequences + evidence + optional advisory recommendation, framed for an owner decision (SC-003, SC-009, US3).

- [X] T020 [US3] Enumerate the step-1 open questions (`investigation.md` §8.3) into the **DOSSIER** §10 question index. **`investigation.md` §8.3 is the authoritative open-question set** against which SC-003's "100%" is measured (the checklist §10 must fully cover it).
- [X] T021 [P] [US3] Author **DOSSIER** §10 option sets for the **wire/result** questions — output streaming vs terminal envelope; encoding of unbound `VarRef`/`MutualRefTerm`/`ModuleTerm` + whether suspended-goal/blocking-reader detail round-trips; var-name→writer identity scheme (`GlobalVarId` vs local heap ints) (FR-011, FR-018).
- [X] T022 [P] [US3] Author **DOSSIER** §10 option sets for the **persistence/resume** questions — which DB underlies the store + what counts as "full current state"; snapshot granularity + consistency point; resume-driver location under FR-057; store-as-source-of-truth-for-code vs reload-`.glp`; in-flight-request loss vs replay (FR-011, FR-018).
- [X] T023 [P] [US3] Author **DOSSIER** §10 option set for the **compiler-location** question (cross-linked to §9) (FR-011, FR-018).
- [X] T024 [US3] Verify US3: 100% of the authoritative §8.3 open-question set (per T020) present as 2–5 evidence-grounded (`file:line`/prior-art), concise, mutually-exclusive options; recommendations marked advisory; no fork recorded settled (SC-003, SC-009, INV-4).

---

## Phase 6: User Story 4 — Feature breakdown authored + (post-approval) seeded (Priority: P2)

**Goal**: An ordered, topologically-valid successor-feature breakdown authored in the dossier; after owner approval, features 2–16 seeded into `buildkit-roadmap` as candidates.

**Independent Test**: Every breakdown entry has kind/scope/why/depends-on + a section ref; the order has zero forward dependencies; after approval each successor exists as a roadmap candidate and none is specified (SC-004, SC-010, US4).

- [X] T025 [US4] Author **DOSSIER** §11 **epic feature breakdown** — the 16 ordered entries (from `investigation.md` §7), each with `kind` (prep/experiment/mvp/follow-up), one-line `scope`, `why`, explicit `depends_on[]`, and a citing **dossier section ref** (FR-012, FR-013).
- [X] T026 [US4] Validate **DOSSIER** §11: topological order (no entry depends on a later entry) and every entry cites a motivating section; **if any entry has no citable section, add that section to the dossier and re-validate** (closes the FR-013 gap edge case); MVP entries enumerate net-new deps + defers (FR-013, SC-004, SC-007).
- [X] T027 [US4] ⛔ **OWNER-GATED (autonomous-action boundary)** — **only after the owner approves the dossier** at the marathon gate: seed successors **2–16** into `buildkit-roadmap` as one candidate each (kind/scope/why/depends-on; `state=candidate`) per `contracts/roadmap-candidate.md`. Do **not** run `/buildkit-specify` or any later stage on any successor (FR-019).
- [X] T028 [US4] Verify post-seed: features 2–16 present as roadmap candidates carrying all four fields; **zero** successors specified/planned/implemented (SC-010).

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Risk register, executive summary, and the cross-cutting invariants.

- [X] T029 [P] Author **DOSSIER** §12 **risk register** — the top design risks (from `investigation.md` §8.2) each with a mitigation reflected in the design or in the breakdown ordering (FR-017).
- [X] T030 Finalize **DOSSIER** §0 **executive summary + how-to-cite** once §1–§12 exist (FR-001, INV-5).
- [X] T031 Cross-cutting verification: INV-1 read-only (`git diff` touches only `docs/` + `specs/` — SC-006); INV-3 re-verified reality where code contradicts step 1 (FR-016); INV-4 present-options (no fork settled); INV-5 self-containment (a reviewer locates any wire-crossing design from the dossier alone — SC-005).

---

## Dependencies & Execution Order

- **Setup (Phase 1)** → **Foundational (Phase 2)** must complete before any authoring.
- **US1 (Phase 3)** is the MVP; **US2/US3/US4-authoring** all depend only on the Phase-2 evidence base, so they are independent of US1 and of each other (different sections).
- **T027 (seeding)** depends on **owner approval of the whole dossier** — i.e. after US1–US3 + §11 authored (T025/T026) + Polish (T029/T030). It is the only mutation outside the dossier and is strictly owner-gated.
- **Polish (Phase 7)** depends on all section content (T030 needs §1–§12; T031 is the final gate).

## Parallel Opportunities

- Phase 2: **T003, T004, T005, T006** run in parallel (distinct code trees).
- Phase 3: **T008–T015** run in parallel (distinct dossier sections), then T016 verifies.
- Phase 4: **T017, T018** in parallel. Phase 5: **T021, T022, T023** in parallel (after T020).
- Phase 7: **T029** parallel with US-phase work; T030/T031 are final.

## Implementation Strategy

- **MVP scope** = Phase 1 + Phase 2 + **User Story 1** (T001–T016): the seven design areas decision-ready. Delivers value the moment any one area is decision-ready.
- **Incremental delivery**: add US2 (premise reconciliations) → US3 (open-question options) → US4 authoring (§11) → Polish → **then** the owner-gated seeding (T027/T028).
- **Hard stop before T027** for owner approval — do not seed the roadmap or run any successor pipeline stage without it.

## Task Count

- Total: **31 tasks**. Setup 2 · Foundational 5 · US1 9 · US2 3 · US3 5 · US4 4 · Polish 3.
- Per story: US1 = 9 (incl. verify), US2 = 3, US3 = 5, US4 = 4.
