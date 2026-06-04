# Feature Specification: /glptutorial-run — run & explain a single GLP tutorial example

**Feature Branch**: `023-glptutorial-run`  
**Created**: 2026-06-04  
**Status**: Draft  
**Input**: User description: "/glptutorial-run — a skill AND a python tool to select, run, and explain a single GLP REPL tutorial example. Sits downstream of the shipped /glptutorial-list (feature 022) and reuses its corpus-discovery layer (codeconv/src/codeconv/tutorials/) as the selection front-end. Corpus lives in the sibling repo at D:/bstdev/research/glp/GLP/olamni/tutorial/. Target user: an engineer/learner working through the GLP tutorial corpus. Flow: (1) select a specific tutorial script; (2) PREVIEW the intended behaviour before running; (3) RUN it — default backend is the C#-based REPL, with the Dart REPL available on demand; (4) post-run, analyse and EXPLAIN the actual outcome, referring back to the tutorial's .md. Hard requirement — a UNIFIED run-model across BOTH chapter shapes: (a) section-driven chapters ch01–ch06 use the chNN/exercise-MM/*.glp layout; (b) use-case-driven chapters ch07+ keep exercise-MM dirs as step-through .md guides only, with their .glp living in module-project dirs under the {self,agent,boot,network,actors,mediator}.glp shape. MUST intensively analyse ch07 AND all chapters ch01..ch13; build one model for running individual examples across both shapes; MAY restructure/improve per-chapter examples for consistency. Known dependency/risk: the default C# REPL path depends on a functional C# runner — runner.cs is currently a stub (NotImplementedException); full Dart→C# conversion is in-flight under feature 020. repl-trace is OUTCOME-only per 020 FR-006."

## Open Decisions (all resolved 2026-06-04)

Three load-bearing decisions were deliberately surfaced rather than guessed (each materially changes scope, self-containment, or safety). **All three are now RESOLVED** in the Clarifications below (Session 2026-06-04); no `[NEEDS CLARIFICATION]` markers remain.

1. **Corpus source for *running*** (FR-012) — vendored snapshot vs. sibling in place vs. hybrid. **RESOLVED 2026-06-04: hybrid** (select from vendored, execute against the sibling in place).
2. **Backend policy** (FR-006/FR-007) — **RESOLVED 2026-06-04: C# is the mandated default and MUST always run (it is fully implemented, not a stub); Dart on demand; a non-working C# backend is a critical P1 defect.**
3. **Scope of "MAY restructure/improve per-chapter examples"** (FR-013) — **RESOLVED 2026-06-04: A+B hybrid with gated C** — read-only by default, may emit improvement *proposals*, and *applies* them only with engineer/operator approval on a justified improvement.

> All three open decisions are **RESOLVED** — see Clarifications 2026-06-04.

## Clarifications

### Session 2026-06-04

- Q: Corpus source when running an example (FR-012) — vendored snapshot, sibling in place, or hybrid? → A: **Hybrid** — selection/discovery uses the vendored snapshot (`tutorials/olamni/`, the feature-022 default); execution resolves and runs the actual load target from the **sibling repo in place** (`D:/bstdev/research/glp/GLP/olamni/tutorial/`, plus the corresponding sibling project location for ch07+ module-projects). A drift guard (`codeconv tutorials sync --check`) keeps the snapshot aligned with the sibling so the selected example and the executed example are the same.
- Q: Backend policy (FR-006/FR-007) — fail fast, graceful fallback, or Dart-effective-default? → A: **C# is the default backend and MUST always be run; the Dart REPL is available on demand.** The C# REPL is fully implemented — NOT a stub (this supersedes the Input's "runner.cs is a stub / feature-020-pending" statement). A non-working or wrong-result C# backend is a **critical P1 defect** requiring immediate, thorough fix — surfaced loudly as P1, never silently tolerated; the tool MAY fall back to the Dart REPL with a prominent P1 notice to keep the learner unblocked, but MUST NOT mask the C# failure.
- Q: Scope of "MAY restructure/improve per-chapter examples" (FR-013) — read-only, proposal-only, or mutating? → A: **A+B hybrid with gated C** — read-only by default (A); the tool MAY emit restructuring **proposals** (B — a normalization report/map) without mutating; **applying** a proposal (C) is permitted **only with explicit engineer/operator approval and a justified improvement** — never automatic. When applied, it targets the sibling source of truth (then re-vendor), is layout/metadata-level (preserves program semantics and book-exact clause text), and is revertible.
- Q: In the use-case-driven shape (ch07+), what is the selectable/runnable "single example"? → A: **The exercise** (`exercise-MM`) is the uniform selectable unit across both shapes (matching ch01–06 and the 022 lister). In the use-case shape an exercise resolves to *(its backing module-project + the play/goal it documents)*; the play is the goal-within-the-exercise, mirroring a section-driven exercise's goal.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run a section-driven example end-to-end and see its outcome (Priority: P1)

A learner has used `/glptutorial-list` to find a tutorial script in a section-driven chapter (ch01–ch06, where each `exercise-MM/` directory holds a single runnable `.glp`). They select that one example and ask the tool to run it. Without hand-loading files or hand-typing goals at a REPL prompt, they get back the **actual outcome** of running the example's goal — the bindings and the `→ succeeds` / `→ suspended` result.

**Why this priority**: This is the core value and the minimum viable product — turning a discovered tutorial script into an executed result with one command. ch01–ch06 are fully implemented today, so this slice is runnable and testable on its own and already delivers the headline "run a tutorial example" capability.

**Independent Test**: Pick one fully-implemented section-driven example (e.g. `ch01/exercise-01`), run it through the tool, and confirm the tool loads the single `.glp`, runs the documented goal, and reports the actual outcome — verifiable in isolation against the example's known-good outcome.

**Acceptance Scenarios**:

1. **Given** a selected section-driven example with a single `.glp` and a documented goal, **When** the user runs it, **Then** the tool loads that `.glp`, runs the goal, and reports the actual outcome (final bindings and `→ succeeds` / `→ suspended`).
2. **Given** an example whose documented goal succeeds, **When** the user runs it, **Then** the reported outcome states success and shows the resulting bindings.
3. **Given** an example whose documented behaviour is to suspend (a normal outcome for some examples), **When** the user runs it, **Then** the tool reports `→ suspended` as a valid outcome, not an error.

---

### User Story 2 - Run a use-case-driven example with the SAME model (the unification) (Priority: P1)

A learner selects an example from a use-case-driven chapter (ch07+, where `exercise-MM/` directories are step-through `.md` guides only and the runnable `.glp` live in module-project directories such as `cssg-modules/` and `simple-multimodule/` under the `{self,agent,boot,network,actors,mediator}.glp` shape). Using the **same command and the same select→run flow** as a section-driven example, they run the chosen example (e.g. a specific play such as `fplay1`) and get its actual outcome — even though the underlying layout is a multi-file project rather than a single file.

**Why this priority**: This is the **hard requirement** — one unified run-model across both chapter shapes — and the reason this feature exists rather than a ch01–ch06-only runner. It directly closes the gap where the 022 lister shows ch07 as "(no scripts)" by design: those examples become runnable here. It is co-equal P1 with US1 because the unification, not single-file running, is the point.

**Independent Test**: Pick one implemented use-case-driven example (a ch07 play backed by `cssg-modules/` or `simple-multimodule/`), run it through the identical tool command used in US1, and confirm the tool resolves the project, loads its module `.glp` in the right order, runs the documented goal/play, and reports the actual outcome — with no shape-specific command or extra step demanded of the user.

**Acceptance Scenarios**:

1. **Given** a use-case-driven example whose `exercise-MM/` guide maps to a module project and a play/goal, **When** the user runs it with the same command used for a section-driven example, **Then** the tool resolves the project, loads its module files together, runs the goal, and reports the actual outcome.
2. **Given** the module project requires its files in a specific load order, **When** the tool runs the example, **Then** it loads the modules in the correct order and, if a module is missing or fails to load, reports which module and why rather than producing a misleading result.
3. **Given** a ch07 example that the 022 lister shows under "(no scripts)", **When** the user runs it through this tool, **Then** it executes — demonstrating that the unified model reaches examples the lister could not surface as scripts.

---

### User Story 3 - Preview the intended behaviour before running (Priority: P2)

Before committing to a run, the learner asks the tool to preview the selected example: what goal(s) it will run and what outcome the tutorial says to expect. The preview is drawn from the tutorial's own documentation (the `ex-MM-tutorial.md` guide and the outcome-only known-good capture) and shows the intended behaviour **without executing anything**.

**Why this priority**: Previewing lets a learner understand and confirm what they are about to run, and is the natural step-2 of the select→preview→run→explain flow. It is a strong enhancement to the core run (US1/US2) but the run is valuable without it, so it is P2.

**Independent Test**: Select an example with a documented goal and expected outcome, request a preview, and confirm the tool shows the intended goal(s) and expected outcome sourced from the tutorial `.md`, with no execution performed.

**Acceptance Scenarios**:

1. **Given** a selected example with a documented goal and expected outcome, **When** the user requests a preview, **Then** the tool shows the goal(s) it would run and the expected outcome, attributing them to the tutorial documentation, without running anything.
2. **Given** an example with more than one documented goal, **When** the user requests a preview, **Then** all documented goals are shown so the user can choose which to run.
3. **Given** an example for which no goal can be resolved from the documentation, **When** the user requests a preview, **Then** the tool says so clearly and indicates that a goal must be supplied to run.

---

### User Story 4 - Explain the actual outcome, referring back to the tutorial (Priority: P2)

After a run, the learner asks the tool to analyse and explain what happened: it compares the actual outcome to the tutorial's intended outcome (the outcome-only golden) and explains the result with reference to the tutorial's `.md`. A difference between actual and intended is surfaced and explained, never silently passed over.

**Why this priority**: Explanation turns a raw outcome into a learning aid and completes the select→preview→run→explain flow. It depends on a completed run (US1/US2) and is therefore P2, alongside preview.

**Independent Test**: Run an example whose outcome matches its golden and confirm the tool explains the match with reference to the `.md`; then run (or simulate) an example whose outcome differs and confirm the difference is surfaced and explained rather than hidden.

**Acceptance Scenarios**:

1. **Given** a completed run whose actual outcome matches the example's outcome-only golden, **When** the user requests an explanation, **Then** the tool reports a match and explains the outcome with reference to the tutorial `.md`.
2. **Given** a completed run whose actual outcome differs from the golden, **When** the user requests an explanation, **Then** the tool surfaces the difference explicitly and explains it, never reporting a silent pass.
3. **Given** a suspended outcome, **When** the user requests an explanation, **Then** the tool explains the suspension as the example's expected behaviour where the documentation says so.

---

### User Story 5 - Choose the run backend (C# default, Dart on demand) (Priority: P3)

The learner runs an example on the default backend (the C#-based REPL) or, on demand, selects the Dart REPL instead. When the chosen backend is unavailable, the tool states why and what to do rather than hanging or crashing.

**Why this priority**: Backend selection is a real requirement of the feature, but the select→run→explain value is delivered through whichever backend is functional; the choice is a refinement layered on top, hence P3. It is also where the known C#-runner dependency lands (see Dependencies and FR-007).

**Independent Test**: Run the same example on each available backend and confirm the outcome is reported from the selected backend; request the unavailable backend and confirm the tool explains the unavailability and the alternative.

**Acceptance Scenarios**:

1. **Given** a functional backend is selected, **When** the user runs an example, **Then** the outcome is produced by that backend and the report names which backend ran it.
2. **Given** the user selects the Dart backend on demand, **When** they run an example, **Then** the Dart REPL is used and the outcome is reported.
3. **Given** the selected default backend is not yet functional, **When** the user runs an example, **Then** the tool responds per the resolved default-backend behaviour (FR-007) — never an unexplained hang or crash.

### Edge Cases

- **C# backend fails or returns a wrong result**: the C# REPL is the mandated default and is expected to always run; any failure or incorrect outcome is a **critical P1 defect** (FR-007/FR-018) — surfaced loudly for immediate fix, optionally falling back to Dart with a prominent P1 notice, never a silent hang/crash/pass.
- **Chapter not yet implemented**: ch08–ch13 are mostly planned stubs (only `spec-rev-eng-input/` + `chNN-sources.md`, no runnable examples) → the tool reports "not yet available" for those chapters rather than failing.
- **Use-case example with no resolvable project/goal mapping**: a ch07+ `exercise-MM/` guide that does not map cleanly to a module project or a goal → clear message naming what is missing, no misleading result.
- **Multiple goals in one example**: an exercise documents several goals → the user can choose one (or run them in sequence); the choice is explicit.
- **Goal not resolvable from documentation**: no goal extractable from the `.md` → the tool says so and lets the user supply a goal.
- **Goal hits a known REPL limitation**: e.g. a goal containing a struct inside a list, or `=..` in a clause body (documented REPL limitations) → surfaced clearly as a known limitation, not a crash and not silently swallowed.
- **Module load order / missing module (use-case shape)**: a project whose modules must load in a particular order, or a missing module file → the tool reports which module and why.
- **Outcome differs from golden**: actual ≠ intended → surfaced and explained (US4 #2), never a silent pass.
- **Suspended outcome**: `→ suspended` (normal for plays with escrow timers) → treated as a valid outcome, not an error.
- **Corpus unreachable / unknown identifier**: the corpus source cannot be read, or the selected tutorial/example identifier matches nothing → clear, actionable error naming what was tried (consistent with the 022 selection front-end).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The tool MUST let the user select a single runnable example from within a chosen tutorial, reusing the feature-022 corpus-discovery layer (`codeconv/src/codeconv/tutorials/`) as the selection front-end so selection is consistent with `/glptutorial-list`.
- **FR-002**: The tool MUST provide ONE unified run-model that runs an individual example under BOTH chapter shapes with the same command and flow: (a) section-driven (ch01–ch06) where a single `.glp` lives directly in `exercise-MM/`, and (b) use-case-driven (ch07+) where `exercise-MM/` holds `.md` guides only and the runnable `.glp` live in module-project directories (e.g. `cssg-modules/`, `simple-multimodule/`). No shape-specific command or extra user step may be required.
- **FR-003**: The tool MUST resolve the load target for a selected example regardless of shape — a single `.glp` file for the section-driven shape; the module-project (the set of `{self,agent,boot,network,actors,mediator}.glp` files, loaded together as a project) for the use-case-driven shape. The **exercise** (`exercise-MM`) is the uniform selectable unit in both shapes; in the use-case shape the selected exercise resolves to its backing module-project plus the play/goal its guide documents (that play is the exercise's goal).
- **FR-004**: The tool MUST resolve the goal(s) to run for a selected example from the tutorial documentation; when an example documents multiple goals it MUST let the user choose one or run them in sequence; when no goal is resolvable it MUST report so and allow the user to supply a goal.
- **FR-005**: Before running, the tool MUST be able to PREVIEW the selected example — showing the goal(s) it would run and the expected outcome drawn from the tutorial `.md` / outcome-only golden — without executing anything.
- **FR-006**: The tool MUST RUN the selected example+goal through a REPL backend, defaulting to the C#-based REPL, with the Dart REPL selectable on demand.
- **FR-007**: The C#-based REPL is the default backend and MUST always be the backend used unless the user explicitly selects Dart on demand. A non-working C# backend, or one that produces a wrong result, is a **critical P1 defect** that MUST be surfaced loudly as such (demanding immediate fix) — never an unexplained hang, crash, or silent pass. The tool MAY fall back to the Dart REPL with a prominent P1 notice so the learner stays unblocked, but MUST NOT mask or downgrade the C# failure.
- **FR-008**: The tool MUST capture the ACTUAL outcome of a run as OUTCOME-only — final bindings and the `→ succeeds` / `→ suspended` result — consistent with feature-020 FR-006 (repl-trace is outcome-only); it MUST NOT depend on a full step-by-step execution trace.
- **FR-009**: After running, the tool MUST be able to EXPLAIN the actual outcome — comparing it to the example's intended outcome (the outcome-only golden) and referring back to the tutorial `.md`; a difference from the golden MUST be surfaced and explained, never silently passed.
- **FR-010**: The tool MUST treat a suspended result (`→ suspended`) as a valid outcome (normal for some examples, e.g. plays with escrow timers), not a failure.
- **FR-011**: The feature MUST be built on an intensive analysis of all chapters ch01..ch13 (with particular attention to ch07) so the single run-model accommodates both shapes; for chapters or examples not yet implemented (ch08–ch13 stubs at spec time), the tool MUST report "not yet available" rather than failing.
- **FR-012**: The tool MUST resolve examples via a **hybrid** corpus model: **selection/discovery** reads the vendored snapshot (`tutorials/olamni/`, the feature-022 default), while **execution** loads and runs the actual target from the **sibling repo in place** (`D:/bstdev/research/glp/GLP/olamni/tutorial/`, plus the corresponding sibling project location for ch07+ module-projects). The tool MUST guard against snapshot/sibling drift (e.g. via `codeconv tutorials sync --check`) so the example selected is the example executed; on detected drift it MUST warn rather than run a mismatched example.
- **FR-013**: The feature is **read-only over the corpus by default**. It MAY surface per-chapter restructuring **proposals** — a normalization report/map of inconsistencies and suggested improvements (e.g. explicit ch07+ exercise→project/goal mappings, layout normalisation, run-manifests) — **without mutating any file**. **Applying** a proposal (actually restructuring the corpus) is permitted only as an explicitly engineer/operator-approved, justified exception (FR-019); when applied it is layout/metadata-level and MUST preserve each program's semantics and any book-exact clause text the corpus charter mandates.
- **FR-014**: The capability MUST be delivered as BOTH a `/glptutorial-run` skill (a thin front-end) and an underlying command-line tool (the engine), and the two entry points MUST produce equivalent behaviour (mirroring 022 FR-009).
- **FR-015**: The select / preview / run / explain actions, and the emission of restructuring proposals, MUST be read-only — they never mutate the corpus; the only execution is loading and running the example in the REPL backend. Only the approval-gated *apply* step (FR-013/FR-019) may mutate the corpus.
- **FR-016**: The tool MUST emit clear, actionable messages for: corpus unreachable; unknown tutorial/example identifier; example with no resolvable load target; no resolvable goal; selected backend unavailable; chapter not yet implemented; and a goal that hits a documented REPL limitation.
- **FR-017**: When loading a use-case-driven project, the tool MUST load the module files in the order required for the project to compile/run, and MUST report a missing or failed module clearly (which module and why).
- **FR-018**: The C# REPL backend is fully implemented and is the mandated default (per Gabi's 2026-06-04 directive, superseding the Input's "stub" claim). Should the C# backend ever be found non-functional or incorrect, the tool MUST treat it as a critical P1 defect (see FR-007); the feature does NOT accept a stub or non-functional C# runner as a tolerated condition.
- **FR-019**: Applying a restructuring proposal (FR-013) MUST: require explicit **engineer/operator approval per example before any `.glp` edit**, plus a recorded improvement rationale (CLAUDE.md spec-first discipline); target the **sibling source of truth** and then re-vendor the snapshot (`codeconv tutorials sync`); preserve program semantics and book-exact clause text; and be revertible (each example's change independently undoable). Absent approval, the feature does NOT modify the corpus.

### Key Entities *(include if feature involves data)*

- **Runnable example**: the unit this feature runs, anchored on the **exercise** (`exercise-MM`) uniformly across both shapes — a load target + one or more goals (the documented play/goal in the use-case shape) + an expected outcome. It is the bridge between the 022 "script/exercise" view and an executable run.
- **Load target**: what the backend loads — a single `.glp` file (section-driven shape) OR a module-project: the ordered set of module `.glp` files loaded together (use-case-driven shape).
- **Goal**: a REPL goal to run against the loaded program; sourced from the tutorial documentation (or user-supplied). An example may have several goals.
- **Expected outcome (golden)**: the outcome-only known-good result for a goal (from the example's `ex-MM-repl-trace.md`), per feature-020 FR-006 — bindings and `→ succeeds` / `→ suspended`, not a full trace.
- **REPL backend**: the engine that loads and runs an example — the C#-based REPL (default) or the Dart REPL (on demand).
- **Run result**: the actual outcome captured from a run (outcome-only), plus the comparison verdict against the golden (match / explained difference) used by the explain step.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a single command, a user selects and runs any fully-implemented section-driven example (ch01–ch06) and sees its actual outcome — without manually loading files or typing goals at a REPL.
- **SC-002**: The identical command runs an implemented use-case-driven example (ch07) through the same select→run flow, with no shape-specific step — making runnable the very examples the 022 lister shows as "(no scripts)".
- **SC-003**: For at least 90% of implemented examples (ch01–ch07) that have a documented goal and an outcome-only golden, running the example yields an outcome the tool reports as matching the golden, or explicitly explains the difference.
- **SC-004**: Before running, a user can see an example's intended goal(s) and expected outcome without anything being executed.
- **SC-005**: After running, a user receives an explanation of the actual outcome that references the tutorial `.md` and states match-or-difference versus the golden.
- **SC-006**: The C# backend is the default and runs by default; a user can select the Dart backend on demand. A C# backend failure is reported as a critical P1 defect (never a silent hang/crash/pass), optionally with a flagged Dart fallback.
- **SC-007**: Every implemented example across ch01..ch07 is reachable by the unified run-model (100% coverage of implemented examples), and not-yet-implemented chapters (ch08–ch13) are reported as "not yet available" rather than crashing.

## Assumptions

- **Reuse + extension of 022**: selection reuses the feature-022 corpus-discovery layer; the run feature *extends* discovery so the use-case-driven (project-dir) shape — which 022 surfaces as "(no scripts)" — becomes selectable and runnable.
- **Delivery split**: the command-line tool is the engine; the `/glptutorial-run` skill is a thin front-end over it; both produce equivalent behaviour (FR-014), mirroring 022.
- **Goal & golden source**: goals and expected outcomes are sourced from the example's tutorial `.md` (`ex-MM-tutorial.md`) and its outcome-only known-good capture (`ex-MM-repl-trace.md`); outcomes are compared outcome-only per feature-020 FR-006.
- **Suspended is valid**: `→ suspended` is an expected, non-error outcome for examples documented to suspend (e.g. plays with escrow timers).
- **Implementation status at spec time**: ch01–ch06 (section-driven) and ch07 (use-case-driven, plays backed by `cssg-modules/` / `simple-multimodule/`) are implemented; ch08–ch13 are planned stubs (sources only). The model must still be designed to cover both shapes for the planned chapters.
- **Known REPL limitations**: documented REPL limitations (e.g. struct inside a list in a REPL goal; `=..` in a clause body) may block specific goals; the tool surfaces them as known limitations rather than working around them.
- **Both backends functional; C# is the mandated default**: the C#-based REPL is fully implemented and is the default that MUST always run; the Dart REPL (`dart run glp_repl.dart` / `glp_repl.exe`) is available on demand. A non-functional C# backend is a P1 defect, not an expected state (FR-007/FR-018).
- **Restructuring = read-only proposals + approval-gated apply**: per FR-013/FR-019 the feature is read-only by default and may *propose* improvements, but only *applies* them with explicit engineer/operator approval and a justified reason; `/buildkit-plan` should weigh whether the propose/apply capability warrants its own prioritised (low) user story.

## Dependencies

- **Feature 022 corpus-discovery layer** (`codeconv/src/codeconv/tutorials/`) — the selection front-end this feature reuses and extends. It is not yet merged to `develop`; this feature branches off `022-glptutorial-list`, which carries it.
- **REPL backends** — the C#-based REPL (fully implemented) is the mandated default and MUST always run; the Dart REPL (`dart run glp_repl.dart` / `glp_repl.exe`) is available on demand. A non-functional C# backend is a P1 defect to fix immediately (FR-007/FR-018), not an accepted dependency risk.
- **Feature 020 (trace-equivalence/fidelity)** — supplies the outcome-only golden convention (its FR-006) that FR-008 follows. (The C# runner is implemented; it is no longer a pending dependency of this feature.)
- **The tutorial corpus** — read from the source resolved in FR-012 (vendored snapshot vs. sibling repo in place).
