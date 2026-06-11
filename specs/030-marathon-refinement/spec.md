# Feature Specification: Marathon Refinement

**Feature Branch**: `030-marathon-refinement`
**Created**: 2026-06-11
**Status**: Draft
**Input**: User description: "Refine glpnet's marathon-stage-harness (feature 024, `codeconv.marathon`) by adopting-and-reconciling the capabilities of the sibling crucible-xyz `crucible_marathon` package, plus a key extension: capture emergent mid-marathon work as first-class items that each run through an in-marathon mini-pipeline."

## User Scenarios & Testing *(mandatory)*

The "user" throughout is an **engineer driving a long, multi-session feature** through the buildkit
pipeline with the marathon harness, across session ends, context compaction, and machine restarts.
Feature 024 delivered a working harness for THIS repo's fixed pipeline; this feature generalises and
extends it to match (and exceed, on emergent work) the sibling `crucible_marathon` package.

### User Story 1 - Run any stage shape, and grow it mid-run (Priority: P1) 🎯 MVP

The engineer registers a marathon with an **arbitrary ordered list of named stages** for the work at
hand (not a fixed, hard-coded seven-stage pipeline), and — when new stages are discovered partway
through — **appends them to the in-progress run**. The resume position always reports progress
against the *current* known total, which may have grown since the run started.

**Why this priority**: The current harness hard-codes one fixed stage vocabulary and cadence, so it
only fits this repo's exact pipeline. Making stages registrable and growable is the foundational
refinement every other capability builds on, and is independently valuable on its own (a second
workload could adopt the harness unchanged).

**Independent Test**: Register a run with stages `[a, b, c]`; complete `a`; append stage `d`; ask for
the resume position — it reports `done=1/4` (not `1/3`) and names `b` as the next action. No code
change to the harness is needed to use a different stage list.

**Acceptance Scenarios**:

1. **Given** a new marathon, **When** the engineer registers it with an ordered list of named stages,
   **Then** the harness records the run with exactly those stages and reports `done=0/N`.
2. **Given** an in-progress run with N stages, **When** the engineer appends a newly-discovered stage,
   **Then** the total becomes N+1 and the resume position reports progress against N+1.
3. **Given** a run whose stage list differs entirely from this repo's pipeline, **When** stages are
   completed and checkpointed, **Then** durability, resume, and status behave identically — the
   harness is not coupled to any particular stage vocabulary.

---

### User Story 2 - Capture emergent work and route it through a mini-pipeline (Priority: P1)

While running a marathon, the engineer (or the driving agent) discovers work that wasn't in the
original plan — a newly-surfaced user story or acceptance criterion, a latent requirement, an issue,
a bug, or a missing prerequisite. They **capture it as a first-class, typed item attached to the
marathon**. Each captured item is then **routed through an in-marathon mini-pipeline**
(`mini-specify → mini-clarify → mini-plan → mini-tasks → mini-analyze`) whose output **feeds the
marathon's implement stage**. A captured item that is a **blocking missing prerequisite** is routed
**ahead of** the stage it blocks; non-blocking items follow the current stage. Routing is
**advisory / default-deny**: the harness names the item's next incomplete mini-stage in the resume
position but never auto-advances it.

**Why this priority**: This is the headline differentiator beyond the sibling package — it lets a
marathon absorb mid-flight discoveries without abandoning the run or smuggling unplanned work in
untracked. It depends on US1 (a captured item grows the run's stage total).

**Independent Test**: Mid-run, capture a `missing-prerequisite` item that blocks stage `c`; the
resume position now names `mini-specify` for that item as the next action and shows the item's
mini-stages ordered *before* `c`; the run total grew by the mini-pipeline's stage count. The harness
does not advance any mini-stage on its own.

**Acceptance Scenarios**:

1. **Given** an in-progress run, **When** the engineer captures a typed item (latent-requirement,
   issue, bug, or missing-prerequisite), **Then** the item is durably recorded with its type and a
   mini-pipeline of ordered mini-stages is appended to the run (growing the total).
2. **Given** a captured **blocking** missing-prerequisite, **When** the resume position is computed,
   **Then** the item's next incomplete mini-stage is named as the next action *before* the blocked
   stage.
3. **Given** a captured **non-blocking** item, **When** the resume position is computed, **Then** the
   item's mini-stages are ordered *after* the current stage.
4. **Given** an item whose mini-pipeline is complete, **When** its planning output exists, **Then**
   that output feeds the marathon's implement stage and the item is marked done.
5. **Given** any captured item, **When** it is inspected, **Then** its mini-artifacts live inside the
   marathon's own store — **no** top-level `specs/NNN` feature directory or shared project-pipeline
   row is created for it.
6. **Given** any captured item and its mini-stages, **When** the run is interrupted and resumed,
   **Then** they enjoy the same durability, isolation, commit-boundary, and resume guarantees as
   ordinary stages.

---

### User Story 3 - The durable store survives crashes and stale locks on its own (Priority: P2)

The engineer starts a marathon and the durable store comes up as a **background service** that
publishes a connection endpoint subsequent operations reuse. When they're done for the day they stop
it **gracefully** (pending state flushed, so the next start needs no recovery). If a session is
killed abruptly and leaves **stale lock / lifecycle residue**, the next start **recovers
automatically** — no manual file deletion. A **second concurrent writer** is refused rather than
allowed to corrupt state.

**Why this priority**: The current harness runs in-process with no background lifecycle, no explicit
single-writer enforcement, and no automatic stale-lock recovery — so a crashed session can wedge the
store and require manual cleanup. A self-healing keeper makes long marathons robust to the messy
reality of multi-session work.

**Independent Test**: Start a run (keeper comes up, endpoint published); kill the process abruptly;
start again — the harness clears the stale residue and resumes with no manual intervention. Attempt a
second concurrent writer against the same store — it is refused with a clear message.

**Acceptance Scenarios**:

1. **Given** a fresh run, **When** the store is started, **Then** a background store service is running
   and its connection endpoint is published for reuse.
2. **Given** a running store, **When** it is stopped gracefully, **Then** pending state is flushed and
   the next start finds a consistent store requiring no recovery.
3. **Given** a store left with stale lock/lifecycle residue after an abrupt kill, **When** the next
   operation runs, **Then** the harness clears the stale residue and restarts the store automatically.
4. **Given** a live store with one active writer, **When** a second writer attempts to attach, **Then**
   it is refused (or serialised) with a message distinct from a recoverable stale-residue condition.

---

### User Story 4 - Commit boundaries and status cadence work with the new stage model (Priority: P2)

Every completed stage (ordinary, dynamically-appended, or a mini-stage) is a **commit boundary**: the
harness commits **only that block's artifacts** (explicit paths, never a blanket add, force-push,
history rewrite, or hook bypass), and re-drives a scoped commit on resume if a durably-complete block
was left uncommitted. At each stage boundary and on demand, the harness emits a **standardised,
mechanically-parseable status line** (work done vs current total, open issues, budget spent, single
next action).

**Why this priority**: glpnet already has commit-boundary auto-commit (`gitblock`) and a status line;
this story ensures they are **reconciled with** the refined stage model (dynamic + mini-stages) rather
than duplicated, so the existing discipline holds for the new stage shapes.

**Independent Test**: Complete a dynamically-appended stage and a mini-stage; each produces a scoped
commit of only its paths; the status line after each reports `done=k/N` against the current total and
a single next action. Interrupt right after a block is marked complete but before commit; on resume,
the harness re-drives that one scoped commit.

**Acceptance Scenarios**:

1. **Given** any completed block, **When** the checkpoint commit runs, **Then** only that block's
   explicitly-named paths are staged and committed (no blanket add / force / hook bypass).
2. **Given** a durably-complete-but-uncommitted block, **When** the run resumes, **Then** the scoped
   checkpoint commit is re-driven before new work begins.
3. **Given** any stage boundary, **When** status is emitted, **Then** a single mechanically-parseable
   line reports done/total, open issues, budget spent, and one next action.

---

### User Story 5 - Existing harness strengths are preserved under the new model (Priority: P3)

The capabilities feature 024 already delivered that the sibling package does **not** have continue to
work against the refined stage model: the **per-stage plan-approval gate**, **per-block and
per-subagent re-run**, **budget-ceiling escalation**, the **durable verification-trace substrate**,
and **dual-store reconciliation**. None of these is dropped or regressed by the refinement.

**Why this priority**: This is a refinement of a shipped, working harness — not a rewrite. The
existing, valuable behaviours must survive. It is P3 because it is preservation/regression-guarding
rather than new value.

**Independent Test**: Run an existing-style marathon end to end and confirm the approval gate,
re-run, budget ceiling, trace substrate, and reconciliation all behave as before, now over
registrable/dynamic/mini stages.

**Acceptance Scenarios**:

1. **Given** a stage requiring approval, **When** the gate is presented and decided, **Then** the
   decision is durably recorded and resume short-circuits an already-approved gate — unchanged by the
   refinement.
2. **Given** a failed block or subagent, **When** a re-run is requested, **Then** it resumes from the
   block's last checkpoint (not the marathon start) and reports untouched siblings — unchanged.
3. **Given** a budget ceiling, **When** advancing would exceed it, **Then** the harness halts and
   escalates rather than overrunning — unchanged.
4. **Given** the dual-store, **When** the primary and fallback diverge, **Then** reconciliation
   fast-forwards the stale store or escalates a true fork (never silently picks) — unchanged.

---

### Edge Cases

- **Stacked blocking prerequisites**: two blocking missing-prerequisite items both target the same
  stage — their mini-stages must order deterministically ahead of the blocked stage without collision.
- **Item captured against an already-completed stage**: capturing a prerequisite that blocks a stage
  already done — the harness must surface this clearly rather than reorder finished work.
- **Append during finalisation**: a stage appended (or item captured) after all prior stages are
  complete but before the run is finalised — the run must un-finalise / re-open cleanly.
- **Resume mid-mini-pipeline**: interruption between two mini-stages of an item — resume names the
  exact next incomplete mini-stage, never re-running a completed one.
- **Keeper endpoint stale but process dead**: the published endpoint points at a dead store — treated
  as recoverable stale residue, not a hard failure.
- **Concurrent capture**: two captures racing on the same run — single-writer enforcement serialises
  them; the stage total reflects both deterministically.
- **Empty stage list**: registering a run with zero stages — resume reports "register stages" rather
  than "finalise".

## Requirements *(mandatory)*

### Functional Requirements

**Stage model (US1)**
- **FR-001**: The harness MUST let a workload register a run with an explicit ordered list of named
  stages, without modifying harness internals or depending on any fixed stage vocabulary.
- **FR-002**: The harness MUST let a workload append additional, dynamically-discovered stages to an
  in-progress run; the total expected stages MAY grow during execution.
- **FR-003**: The resume position MUST report progress against the *current* known total (FR-002),
  not a count fixed at registration.
- **FR-004**: A stage that started but did not complete MUST NOT be counted as complete in the resume
  position (carried-forward 024 guarantee, now over registrable stages).

**Emergent work + mini-pipeline (US2)**
- **FR-005**: The harness MUST let the engineer/agent capture, during a run, an arising work-item as a
  first-class, durably-recorded item carrying its **type**: latent-requirement, issue, bug, or
  missing-prerequisite.
- **FR-006**: Each captured item MUST expand into an ordered **mini-pipeline**
  (`mini-specify → mini-clarify → mini-plan → mini-tasks → mini-analyze`) appended to the current run
  as ordinary checkpointed stages (extending FR-002).
- **FR-007**: The mini-pipeline's output MUST feed the **marathon's implement stage**; when an item's
  mini-pipeline is complete its planning artifacts are available to implement and the item is marked
  done.
- **FR-008**: The mini-pipeline MUST be **in-marathon and lightweight**: each mini-stage produces a
  compact artifact stored inside the marathon's own store under the item; it MUST NOT create a
  top-level `specs/NNN` feature directory or a shared project-pipeline row.
- **FR-009**: The mini-pipeline MUST be **advisory / default-deny**: the harness captures and routes
  the item but MUST NOT auto-advance any mini-stage; the resume position MUST name the item's next
  incomplete mini-stage so the driving agent advances it explicitly.
- **FR-010**: A captured item MUST carry whether it **blocks** an existing stage; a blocking
  missing-prerequisite's mini-stages MUST be ordered in the resume position **before** the blocked
  stage (priority routing), while a non-blocking item's mini-stages follow the current stage.
- **FR-011**: Arising items and their mini-artifacts MUST be covered by the same durability,
  isolation, commit-boundary, and resume guarantees as ordinary stages.

**Durable store + keeper lifecycle (US3)**
- **FR-012**: The harness MUST start its durable store as a background service and publish a current
  connection endpoint that subsequent operations reuse.
- **FR-013**: The harness MUST support graceful shutdown that flushes pending state, so the next start
  finds a consistent store requiring no recovery.
- **FR-014**: After abrupt termination leaving stale lock/lifecycle residue, the harness MUST recover
  the store automatically (clear stale residue and restart) without manual file deletion.
- **FR-015**: The harness MUST enforce a single active writer per store, refusing or serialising a
  second concurrent writer rather than risking state corruption.
- **FR-016**: The harness MUST surface store-unavailable and integrity failures as clear, actionable
  messages **distinct** from recoverable stale-residue conditions.

**Commit boundary + status cadence (US4) — reconcile, don't duplicate**
- **FR-017**: The harness MUST treat each completed block (ordinary, dynamic, or mini) as a commit
  boundary and commit **only** that block's explicitly-named paths — never a blanket add, force-push,
  history rewrite, or hook bypass — reusing the project's existing scoped-commit mechanism.
- **FR-018**: On resume, if the last block is durably complete but uncommitted, the harness MUST
  re-drive that scoped checkpoint commit before beginning new work.
- **FR-019**: The harness MUST emit a standardised, mechanically-parseable status line (work done vs
  current total, open issues, budget spent, single next action) on demand and at every stage boundary.

**Preserve existing strengths (US5)**
- **FR-020**: The per-stage plan-approval gate (durable decision recording, resume short-circuit of an
  approved gate) MUST continue to function over the refined stage model.
- **FR-021**: Per-block and per-subagent re-run (resuming from a block's last checkpoint, reporting
  untouched siblings) MUST continue to function.
- **FR-022**: Budget-ceiling tracking with halt-and-escalate on would-exceed MUST continue to function.
- **FR-023**: The durable verification-trace substrate (append-only, ordered by refinement sequence)
  MUST continue to function.
- **FR-024**: Dual-store reconciliation (fast-forward the stale store, escalate a true fork, never
  silently pick) MUST continue to function under the refined model.

**Interface & adoption**
- **FR-025**: The refined harness MUST be drivable both as a library (importable interface) and via a
  thin command-line surface, with the two kept in one-to-one correspondence (parity), so a workload
  adopts it without copying or forking harness code.
- **FR-026**: The refinement MUST NOT require modifying already-shipped, unrelated features to adopt
  it (no regressions forced on existing adopters).

**Open scope decisions** (resolve in `/buildkit-clarify`)
- **FR-027**: The harness MUST persist marathon state in a store that is isolated from the working
  repository and from unrelated project state. [NEEDS CLARIFICATION: store model — adopt the sibling's
  **per-run isolated store outside the repo**, or keep glpnet's current **shared embedded cluster +
  on-disk JSON fallback with reconciliation**? The two differ in isolation guarantees vs. reuse of the
  already-running shared bridge.]
- **FR-028**: The refined harness MUST be packaged for reuse. [NEEDS CLARIFICATION: packaging — extract
  it as a **truly standalone, separately-installable package** (sibling model), or keep it a
  **workload-agnostic module that resides within the existing toolchain** but no longer hard-codes a
  stage vocabulary?]
- **FR-029**: The refinement MUST define what happens to any **in-flight state created under the
  current (024) model**. [NEEDS CLARIFICATION: migration — must existing live marathon state migrate
  into the refined model, or is the refined model greenfield with the 024 model retired once no run is
  in flight?]

### Key Entities

- **Marathon run**: a long, multi-session unit of work with an id, a title, a status (in-progress /
  finalised), an accumulated budget, and an ordered set of stages that may grow over time.
- **Stage**: a named unit of work within a run, with an order, an origin (registered up-front, appended
  dynamically, or generated as a mini-stage), and — for mini-stages — a link to the item and the
  mini-kind that produced it.
- **Checkpoint**: a durable record of a stage's completion (outcome summary, budget delta, the explicit
  committed paths, the commit reference once committed) — the sole source of truth for resume.
- **Arising item**: an emergent work-item captured mid-run, carrying a type
  (latent-requirement / issue / bug / missing-prerequisite), whether it blocks an existing stage, its
  status (open / done), and a location for its in-marathon mini-artifacts.
- **Issue**: an outstanding concern raised during a stage, open until resolved, surfaced in the resume
  position and status line.
- **Store keeper**: the background lifecycle owner of the durable store — publishes the connection
  endpoint, flushes on graceful stop, recovers stale residue, and enforces single-writer access.
- **Resume position**: the objective, memory-independent view derived solely from durable state —
  work done vs current total, outstanding issues, budget spent, and the single exact next action.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A brand-new workload with a stage list unlike this repo's pipeline can adopt the harness
  and run to completion with **zero changes to harness internals**.
- **SC-002**: After a stage is appended (or an item captured) mid-run, the resume position reports
  progress against the new total in **100%** of cases — never against the stale registration count.
- **SC-003**: An emergent item captured mid-run is routable through its full mini-pipeline and its
  output reaches the marathon's implement stage, with **no** top-level `specs/NNN` directory or shared
  pipeline row created for it.
- **SC-004**: A blocking missing-prerequisite's next mini-stage is surfaced **before** the stage it
  blocks in the resume position in **100%** of blocking-capture cases.
- **SC-005**: After an abrupt kill, the very next operation recovers the store and resumes work with
  **zero** manual file deletions or store surgery.
- **SC-006**: A second concurrent writer is refused (or serialised) in **100%** of attempts — durable
  state is never corrupted by concurrent writes.
- **SC-007**: Every checkpoint commit stages **only** the block's named paths — **zero** blanket
  adds, force-pushes, history rewrites, or hook bypasses across a full run.
- **SC-008**: The resume position is **identical** whether computed with full conversation context or
  after total context loss (compaction) — it depends solely on durable state.
- **SC-009**: All five preserved 024 capabilities (approval gate, re-run, budget ceiling, trace
  substrate, dual-store reconciliation) pass their existing behavioural checks unchanged under the
  refined stage model — **zero** regressions.

## Assumptions

- The refinement **adopts-and-reconciles** the sibling `crucible_marathon` capabilities rather than
  replacing glpnet's harness wholesale; glpnet's existing strengths (US5) are carried forward, not
  dropped.
- The mini-pipeline is the **five planning stages** (`mini-specify … mini-analyze`) whose output feeds
  the **marathon's** implement stage — per the requester's framing. (This intentionally diverges from
  the sibling package, where each item's mini-pipeline includes its own sixth `implement` stage; the
  divergence is called out so `/buildkit-clarify` can confirm it.)
- "Workload-agnostic" means the **stage vocabulary and cadence become data** (a registered list), not
  that the harness must serve multiple concurrent marathons — single-active-marathon-per-store remains
  the operating assumption (single-writer, FR-015).
- The sibling implementation at `D:/bstdev/research/crucible-xyz` (`src/crucible_marathon`,
  `specs/012-work-marathon-stage-harness`) is the **reference** for the ported capabilities; this
  feature depends on **feature 024 (marathon-stage-harness)** as its baseline.
- Telemetry/analytics mirroring, if added, is **fail-safe** — a telemetry failure never blocks or
  breaks a durable operation.
- Resolving FR-027 (store model), FR-028 (packaging), and FR-029 (migration) in `/buildkit-clarify`
  may materially change implementation size; they are held as explicit forks rather than guessed.
