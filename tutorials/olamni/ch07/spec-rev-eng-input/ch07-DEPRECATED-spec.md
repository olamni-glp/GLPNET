# Feature Specification: Chapter 7 — Module System

**Feature Branch**: `008-tutorial-ch07`
**Created**: 2026-04-28
**Status**: Draft (raw — produced by deep PDF scan)
**Input**: `olamni/tutorial/ch07/ch07-sources.md` + `GLP_ART.pdf` book pp 55–62 (PDF pp 67–74).
**Constitution**: `.specify/memory/constitution.md` — Principle VI; Principle I.
**Tutorial Mode**: multi-actor-distillation (TRANSITION chapter — first project-subdir + Flutter chapter per charter §1)

## Clarifications
- Ch 7 is the **transition** from REPL-only to project-subdir-with-Flutter format (charter §2.2). The chapter itself describes the module system; §7.7 Validation: Child-Safe Social Graph is the example project that exercises every feature.
- The full CSSG protocol code lives in Ch 8 (cold-call, friend-mediated intro). For Ch 7, the tutorial should focus on the **module-system mechanics**: project structure, exported/imported declarations, ancestor scoping, and the renaming/call-resolution that project compilation performs.

## Source Programs (verified against PDF)
- §7.2 project-tree example (file-system layout) — p 56.
- §7.3 Social Agent module: `-module(agent).` + `exported procedure agent/4` + private `merge/3`, `lookup_send/4` decls + agent/4 clause — p 56–57.
- §7.3 Boot's imported declaration + cross-module call site — p 57.
- §7.3 Ancestor `self.glp` types: `Response`, `AgentContent` — p 57.
- §7.5 procedure-renaming table + entry-point aliases — p 59.
- §7.7 CSSG project tree — p 61.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Walk the CSSG project structure (Priority: P1)

Learner reads §7.1–§7.3 and opens `ch07/module-system-walkthrough/` — a project subdirectory mirroring the `cssg_modules/` shape from §7.7, but reduced to a **minimal** working example: just the `agent.glp` skeleton + `boot.glp` + `self.glp` types, no full CSSG protocol clauses (those land in Ch 8).

**Why this priority**: P1 because module structure is the chapter's central concept; everything else (cross-module type checking, project compilation, dynamic linking) depends on it.

**Independent Test**: load the project as a directory in the REPL (project mode). The full pipeline (SRSW → PE → type-check → compile → renaming → entry-point aliasing) MUST complete without errors. Run a tiny demo goal that exercises `boot:play1` calling `agent:agent/4`.

**Acceptance Scenarios**:
1. Project tree exactly matches §7.2 layout: `social/{self.glp, agent.glp, ui/{self.glp, mediator.glp, actors.glp}, boot.glp}` (or the simplified `cssg_modules/` shape from §7.7).
2. Type identity is structural (Formal 7.2): `Response` defined in `social/self.glp` is visible to `agent.glp` and `ui/mediator.glp` without any import directive.
3. `agent.glp` declares `exported procedure agent/4` and PRIVATE `procedure merge/3`, `lookup_send/4`.
4. `boot.glp` declares `imported procedure agent#agent/4` and calls it as `agent # agent(alice, …)`.
5. Procedure-renaming after project compilation matches §7.5 table.

### User Story 2 — Companion Flutter project for the module-system walkthrough (Priority: P2)

Per charter §2.2, every project subdir for chs 7–13 pairs with a Flutter `main_olamni_chNN_<use-case>.dart` cloned from `glp_multiagent/lib/main_cssg_mad_modules.dart`.

**Independent Test**: build `glp_multiagent/lib/main_olamni_ch07_module_system.dart` (cloned from the canonical template, with `_projectDir` retargeted to `olamni/tutorial/ch07/module-system-walkthrough/`); launch the Flutter app; the play executes and the UI shows `boot:play1` succeeding.

**Acceptance Scenarios**:
1. Flutter file compiles after `flutter clean && flutter pub get && flutter build macos`.
2. Trace log at `/private/tmp/glp_multiagent_trace.log` shows the module-renamed call `boot:play1 → agent:agent/4` resolved correctly.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001** Output: project subdir `olamni/tutorial/ch07/module-system-walkthrough/{self.glp, agent.glp, ui/{self.glp, mediator.glp, actors.glp}, boot.glp}` plus paired `glp_multiagent/lib/main_olamni_ch07_module_system.dart`.
- **FR-002** `agent.glp` exposes EXACTLY the §7.3 example: `exported procedure agent/4` + private merge + lookup_send. Body clause is the §7.3 connect-target snippet.
- **FR-003** `self.glp` files contain ONLY type definitions per §7.2.
- **FR-004** Type identity is verified to be structural (Formal 7.2): `Response`/`AgentContent` defined in `social/self.glp` are accessible from descendants without imports.
- **FR-005** `boot.glp` MUST use `imported procedure` for cross-module calls per §7.3.
- **FR-006** Project loads cleanly in the REPL via project-directory loading; post-compilation procedure names follow the §7.5 renaming convention.
- **FR-007** Flutter file MUST be a clone of `main_cssg_mad_modules.dart` per charter §2.2; verify after change with `flutter clean && flutter build macos`.
- **FR-008** §7.4 (Cross-Module Type Checking, Formal 7.2) and §7.6 (Dynamic Linking) are theoretical and referenced in file headers only.
- **FR-009** §7.7 full validation set is OUT OF SCOPE for this chapter — it is the subject of Ch 8 with full protocol code.
- **FR-010** Exercises (§7.7-end) out of scope per charter.
