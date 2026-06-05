# Feature Specification: Marathon Stage Harness

**Feature Branch**: `024-marathon-stage-harness`  
**Created**: 2026-06-05  
**Status**: Draft  
**Input**: Roadmap promotion (epic `distributed-glp-connectivity`): "Marathon stage harness — durable, restart-safe workflow backing for long multi-stage features"

## Clarifications

### Session 2026-06-05

- Q: Scope boundary that keeps the harness from becoming its own marathon — what is IN vs OUT? → A: IN = exactly the 7 specced user stories, sized as a robust-but-minimal prototype that can drive *only* `multi-protocol-link-layer` through the 7 buildkit stages. OUT = the experiment→verify→refine optimizer/loop, any general-purpose or multi-marathon generalization, and anything not required to run that one marathon.
- Q: What is a "logical block" (the checkpoint + commit/push boundary)? → A: One logical block = one orchestrated Workflow run, mapped 1:1 to the cadence — specify, clarify, and plan+task+analyze are each one block; in implement, each subagent session is one block. Checkpoint and commit/push boundaries are identical to Workflow-run boundaries, so resume granularity and git granularity never drift apart.
- Q: In auto-mode, what blocks for Gabi vs proceeds automatically? → A: The harness blocks for Gabi at exactly two kinds of point — (1) each stage-block's plan-approval gate, and (2) escalations: a failure that can't be auto-retried, a store divergence, a blocked/non-fast-forward push, or anything a stage explicitly flags as needing Gabi. Everything else proceeds automatically within an already-approved block. When unattended, reaching a gate or escalation durably checkpoints and waits; it never auto-approves.
- Q: When the primary (DBOS+PGLite) and JSON-fallback stores diverge, which is authoritative? → A: Each checkpoint carries a monotonically increasing sequence number. On reconciliation the store with the strictly higher sequence is authoritative and the stale store is fast-forwarded to it (the JSON fallback normally holds the newer work done during a primary outage). If both advanced past their last common checkpoint (a true fork, not a clean fast-forward), the harness stops and escalates to Gabi rather than silently picking. The primary is the default home; this rule only governs reconciliation after a fallback episode.
- Q: How is the Workflow-tool opt-in carried in unattended auto-mode? → A: The opt-in is a standing, marathon-scoped preauthorization granted once at marathon start and recorded durably alongside the commit/push preauthorization, so every stage-block run is authorized without a per-run prompt. It is revocable by Gabi and is the only standing grant beyond commit/push; it does not relax the plan-approval gate or any FR-022 escalation.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are Gabi and Claude jointly driving a long, multi-stage buildkit feature (a "marathon") through the full pipeline — specify → clarify → plan → task → analyze → implement → review — across many sessions, partly in auto-mode, with escalation to Gabi at decision points. The first marathon this harness must carry is `multi-protocol-link-layer`.

### User Story 1 - Restart-safe resume with no context loss and no re-instruction (Priority: P1)

A marathon block is interrupted mid-flight — a session ends, the context window is compacted, or the process crashes. On the next start, the harness locates the exact stage and the work-in-progress position objectively (not from a possibly-stale summary), recovers from the last durable checkpoint, skips already-completed work, and continues — without Gabi re-explaining anything and without losing prior decisions.

**Why this priority**: This is the keystone value. Without durable cross-session resume, a marathon spanning many sessions cannot complete reliably; every interruption risks redone work or lost decisions. It is the single capability that distinguishes this harness from running the buildkit stages by hand.

**Independent Test**: Start a marathon block, perform some work, induce an interruption (end the session / simulate compaction / kill the process) partway through, then start again and confirm the harness reports the correct stage + WIP position, resumes from the last checkpoint, and re-executes none of the already-completed units — with zero re-instruction.

**Acceptance Scenarios**:

1. **Given** a marathon block partway through with several completed units and one in-progress unit, **When** the session is interrupted and a new session starts, **Then** the harness reports the exact stage and WIP position and resumes from the last durable checkpoint without Gabi providing any restart instructions.
2. **Given** a resumed block, **When** execution continues, **Then** already-completed units are not re-executed and previously recorded decisions/approvals remain in effect.
3. **Given** a context compaction occurs mid-block, **When** the harness re-locates the work, **Then** it derives position from durable state rather than the compaction summary.

---

### User Story 2 - Per-stage plan → engineer review/approval → durably stored gate (Priority: P2)

Before a stage-block runs its mutating work, the harness presents the plan for that block to Gabi for review and approval (or change). The decision is recorded durably and tied to the block. On any later resume, the stored approval is honored and not re-requested.

**Why this priority**: Marathons run partly in auto-mode; the approval gate is what keeps the work collaborative and prevents auto-mode from charging through decisions Gabi must own. Durable storage of the decision is what makes resume non-repetitive.

**Independent Test**: Reach a stage-block boundary, confirm the harness presents the block plan and waits for a decision, record an approval, interrupt and resume, and confirm the approval is not requested again.

**Acceptance Scenarios**:

1. **Given** a stage-block ready to execute mutating work, **When** the block boundary is reached, **Then** the harness presents the plan and waits for an explicit engineer decision (approve / change) before proceeding.
2. **Given** an approval has been recorded for a block, **When** the block is resumed after an interruption, **Then** the harness proceeds without re-asking for approval.
3. **Given** Gabi requests changes instead of approving, **When** the plan is revised, **Then** the revised plan is re-presented and the prior (superseded) decision is retained in history.

---

### User Story 3 - Re-runnable per-stage and per-subagent execution on failure (Priority: P2)

When a stage fails, or a single subagent within a stage fails, that unit can be re-run in isolation without redoing the units that already succeeded.

**Why this priority**: The implement stage in particular runs as a series of subagent sessions; a single failed subagent should not force the whole stage to repeat. Cheap, targeted re-runs are what make a long marathon affordable and robust.

**Independent Test**: Run a stage that fans out to multiple subagents, force one subagent to fail, trigger a re-run, and confirm only the failed subagent re-executes while succeeded siblings are left intact.

**Acceptance Scenarios**:

1. **Given** a stage with several completed subagents and one failed subagent, **When** the stage is re-run, **Then** only the failed subagent re-executes and succeeded subagents are not repeated.
2. **Given** a whole stage failed, **When** it is re-run, **Then** the harness restarts that stage from its last durable checkpoint, not from the beginning of the marathon.
3. **Given** a re-run succeeds, **When** the harness records the result, **Then** the failure history for that unit is preserved alongside the successful outcome.

---

### User Story 4 - Compose the Workflow tool and verify resumability + budget tracking (Priority: P2)

As the first harness task, the harness composes the Claude Code dynamic Workflow tool to run each stage-block as a single Workflow run, and verifies — by smoke test — that `resumeFromRunId` cached-prefix resume and token-budget tracking behave as required for safe restart of small chunks. The harness reinvents none of the orchestration the Workflow tool already provides; it adds only the cross-session durable checkpoint, the approval gate, and the JSON fallback that the Workflow tool lacks.

**Why this priority**: Gabi wants a *verified* restart method, not an assumed one. This de-risking spike establishes that the chosen substrate (Workflow tool + harness additions) actually delivers safe, resumable, budget-bounded chunks before the marathon relies on it.

**Independent Test**: Run a small multi-step Workflow as a stage-block, re-invoke it with an unchanged prefix and confirm the unchanged prefix returns cached results while only the first changed/new step re-executes; confirm spent/remaining token figures are reported throughout.

**Acceptance Scenarios**:

1. **Given** a stage-block executed as one Workflow run, **When** it is re-invoked with an unchanged leading sequence of steps, **Then** the unchanged steps return cached results and execution resumes at the first changed or new step.
2. **Given** a Workflow run, **When** it executes, **Then** spent and remaining token figures are observable throughout the run.
3. **Given** the same-session-only limitation of Workflow resume, **When** a session boundary is crossed, **Then** the harness's own cross-session durable checkpoint (not Workflow resume) restores position.

---

### User Story 5 - Token budget + periodic standardized status (Priority: P3)

While a marathon block runs, the harness tracks token spend against a budget ceiling and emits a standardized status report on a periodic cadence (about every 5 minutes) containing: what is done, current issues, tokens spent and remaining, and what is still to do. When the budget ceiling is reached, work halts or escalates rather than overrunning.

**Why this priority**: Standardized periodic status keeps a long, partly-autonomous run legible and interruptible; the budget ceiling prevents runaway spend. Valuable but dependent on the core resume/approval machinery being in place first.

**Independent Test**: Run a block long enough to cross the status cadence and confirm at least one status report appears with all four fields; set a low budget ceiling and confirm work halts/escalates at the ceiling instead of overrunning.

**Acceptance Scenarios**:

1. **Given** an active marathon block, **When** the status cadence elapses, **Then** a standardized status report is emitted containing done / issues / tokens (spent + remaining) / to-do.
2. **Given** a configured token budget ceiling, **When** spend reaches the ceiling, **Then** the harness halts or escalates rather than continuing past the ceiling.
3. **Given** a status report, **When** Gabi reads it, **Then** he can determine progress, problems, and remaining work without inspecting internal state by hand.

---

### User Story 6 - Preauthorized commit + push per logical block (Priority: P3)

Each completed logical block is committed and pushed under a standing (preauthorized) authorization, so that durable checkpoints are also captured in version control without prompting at every block.

**Why this priority**: Git-level checkpoints reinforce the durable state and make a marathon's progress recoverable and reviewable; the preauthorization removes per-block friction in auto-mode. It rides on top of the checkpoint machinery.

**Independent Test**: Complete a logical block and confirm an automatic commit + push occurs for that block under the standing authorization, scoped to the files the block touched.

**Acceptance Scenarios**:

1. **Given** a logical block completes, **When** the harness checkpoints it, **Then** the block's changes are committed and pushed automatically under the preauthorization.
2. **Given** preauthorized commit/push, **When** a commit is made, **Then** it stages only the files that block produced (no sweeping of unrelated work).
3. **Given** a push is blocked (e.g., conflict or non-fast-forward), **When** the harness detects it, **Then** it stops and escalates rather than forcing the push.

---

### User Story 7 - Durable verification-trace substrate (Priority: P3)

Per stage or per primitive, the harness durably records a generic, restart-safe iteration-and-verification trace: experiment inputs, metric scores, accept/reject decisions, and refine history. This substrate is what later enables an experiment → verify → refine loop, but the harness provides only the trace substrate — not the optimizer/loop itself.

**Why this priority**: The marathon needs an experiment/verify/refine capability, but baking the full optimizer into the harness would let it balloon into its own marathon. Providing only the durable trace substrate keeps the harness focused while still enabling the loop to be built (as implementation methodology) on top of it later.

**Independent Test**: Record an experiment input, a metric score, and an accept/reject decision for a stage; interrupt and resume; confirm the trace (including refine history) is durably recoverable and append-only.

**Acceptance Scenarios**:

1. **Given** a stage iteration, **When** an experiment input, metric score, and accept/reject decision are recorded, **Then** they persist durably and survive a restart.
2. **Given** multiple iterations of the same primitive, **When** they are recorded, **Then** the refine history is preserved in order and earlier iterations are not overwritten.
3. **Given** the trace substrate, **When** an external optimizer (out of scope here) reads it, **Then** it can reconstruct the iteration/verification history without harness-internal knowledge.

### Edge Cases

- **Primary durable store unavailable**: when the primary durable store cannot be reached, the harness falls back to the JSON store with no loss of resume capability, and surfaces that it is operating in fallback mode.
- **Conflicting state between primary store and JSON fallback**: the harness must resolve to a single authoritative position and report any divergence rather than silently picking one.
- **Interruption exactly at a stage/block boundary**: resume must not double-execute the boundary unit nor skip it.
- **Resume after compaction with a misleading summary**: position must be derived from durable state, never from the summary text.
- **Auto-mode reaching a decision that requires Gabi**: the harness must escalate and block, not auto-approve.
- **Budget ceiling reached mid-subagent**: the in-flight unit must end at a safe checkpoint, not be abandoned in a partial state.
- **Re-run of a unit whose inputs changed**: the harness must treat changed-input units as new work rather than returning stale cached results.
- **Push rejected / merge conflict during preauthorized push**: stop and escalate; never force.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The harness MUST persist per-stage and per-block state durably so that it survives session end, context compaction, and process crash.
- **FR-002**: On start, the harness MUST objectively locate the active marathon's current stage and WIP position from durable state (following the roadmap → pipeline-state → tasks order), independent of any conversation summary.
- **FR-003**: The harness MUST resume from the last durable checkpoint, skipping already-completed units and requiring no re-instruction from Gabi.
- **FR-004**: The harness MUST present each stage-block's plan for engineer review and record the approve/change decision durably, tied to that block.
- **FR-005**: On resume, the harness MUST honor a previously recorded approval and MUST NOT re-request it.
- **FR-006**: The harness MUST support re-running a failed stage in isolation, restarting it from its last checkpoint rather than from the marathon's start.
- **FR-007**: The harness MUST support re-running a single failed subagent within a stage without re-executing subagents that already succeeded.
- **FR-008**: The harness MUST preserve failure history for a re-run unit alongside its eventual successful outcome (append-only, not overwrite).
- **FR-009**: The harness MUST run each stage-block as a single orchestrated run that composes the existing Claude Code dynamic Workflow tool, and MUST NOT reimplement orchestration the Workflow tool already provides (fan-out, per-agent journaling, same-session cached-prefix resume, in-run budget tracking).
- **FR-010**: The harness MUST add the capabilities the Workflow tool lacks: cross-session durable checkpointing, the per-stage engineer-approval gate, and a JSON-store fallback.
- **FR-011**: As the first implementation task, the harness MUST smoke-test and verify that cached-prefix resume (`resumeFromRunId`) and token-budget tracking behave as required for safe restart of small chunks, and MUST record the verification result.
- **FR-012**: The harness MUST track token spend against a configurable budget ceiling and MUST halt or escalate when the ceiling is reached rather than overrunning it.
- **FR-013**: The harness MUST emit a standardized status report on a periodic cadence (target every ~5 minutes during active work) containing: done, issues, tokens (spent + remaining), and to-do.
- **FR-014**: The harness MUST commit and push each completed logical block automatically under a standing preauthorization, staging only the files that block produced.
- **FR-015**: When a preauthorized push cannot proceed (conflict, non-fast-forward, or unexpected divergence), the harness MUST stop and escalate rather than forcing the push.
- **FR-016**: The harness MUST durably record, per stage/primitive, a verification trace consisting of experiment inputs, metric scores, accept/reject decisions, and refine history, append-only and restart-safe.
- **FR-017**: The harness MUST provide the verification trace as a generic substrate only; it MUST NOT include the experiment→verify→refine optimizer/loop itself.
- **FR-018**: The harness MUST integrate as hooks into each buildkit stage (specify → clarify → plan → task → analyze → implement → review) and into the memory chain rooted at CLAUDE.md.
- **FR-019**: The harness MUST map the stage cadence one-to-one to orchestrated runs: specify = one block (then restart), clarify = one block, plan + task + analyze (including applied top remediations) = one block, and implement = a series of subagent sessions (fewest practical).
- **FR-020**: When the primary durable store is unavailable, the harness MUST fall back to the JSON store with no loss of resume capability, and MUST surface that it is in fallback mode.
- **FR-021**: Every checkpoint MUST carry a monotonically increasing sequence number. When the primary (durable) store and the JSON fallback diverge, the harness MUST treat the store holding the strictly higher sequence as authoritative and fast-forward the stale store to it; if both have advanced past their last common checkpoint (a true fork rather than a clean fast-forward), the harness MUST stop and escalate to Gabi rather than silently choosing. The primary store is the default home; this rule governs only reconciliation after a fallback episode.
- **FR-022**: In auto-mode, the harness MUST block and wait for Gabi at exactly two kinds of point, never auto-deciding either: (a) each stage-block's plan-approval gate (FR-004); and (b) escalations — a failure that cannot be auto-retried, a store divergence (FR-021), a blocked/non-fast-forward push (FR-015), or any decision a stage explicitly flags as requiring Gabi. All other work within an already-approved block MUST proceed automatically. On reaching a gate or escalation while unattended, the harness MUST durably checkpoint and wait.
- **FR-023**: The harness MUST treat the Workflow-tool opt-in as a standing, marathon-scoped preauthorization — granted once at marathon start, recorded durably alongside the commit/push preauthorization, and applied to every stage-block run without a per-run prompt. It MUST be revocable by Gabi, MUST be the only standing grant beyond commit/push, and MUST NOT relax the plan-approval gate (FR-004) or any escalation (FR-022).

### Key Entities *(include if feature involves data)*

- **Marathon**: A long multi-stage buildkit feature being driven end-to-end (first instance: `multi-protocol-link-layer`). Has an ordered set of stages and an overall token budget.
- **Stage**: One buildkit pipeline phase (specify, clarify, plan, task, analyze, implement, review).
- **Stage-block (= logical block)**: The cadence grouping that maps one-to-one to a single orchestrated Workflow run (specify=1, clarify=1, plan+task+analyze=1, each implement-session=1). It is the unit of checkpointing and of preauthorized commit/push — these boundaries are identical to the Workflow-run boundary, so resume granularity and git granularity never drift apart.
- **Checkpoint**: A durable snapshot of position — stage, block, WIP unit, completed/remaining units, a monotonically increasing sequence number, and linkage to the orchestrated run — sufficient to resume with no context loss and to arbitrate store divergence.
- **Approval gate**: A stored engineer decision for a block — the presented plan, the approve/change outcome, who decided, and when — with superseded decisions retained in history.
- **Run linkage**: The association between a stage-block and its orchestrated Workflow run (run identifier and per-agent journals) used for same-session cached-prefix resume.
- **Status report**: The standardized periodic snapshot — done / issues / tokens (spent + remaining) / to-do.
- **Verification-trace record**: Per stage/primitive — experiment inputs, metric scores, accept/reject decision, and ordered refine history; append-only.
- **Token budget**: The configurable ceiling and running spent/remaining figures for a marathon.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After an induced interruption (session end, compaction, or crash) at an arbitrary point within a block, the harness resumes at the correct stage and WIP position with **zero** re-instruction from Gabi — verified across all four cadence block types (specify; clarify; plan+task+analyze; an implement session).
- **SC-002**: On resume, **0** already-completed units are re-executed.
- **SC-003**: A re-run of a stage with multiple subagents re-executes **only** the failed subagent(s); **0** succeeded siblings are repeated.
- **SC-004**: An approval recorded once is re-requested **0** times across any number of subsequent resumes.
- **SC-005**: During active work, a standardized status report containing all four fields appears at least once per 5-minute interval.
- **SC-006**: With the budget ceiling set, work halts or escalates at the ceiling in **100%** of trials, with **0** overruns past the ceiling.
- **SC-007**: With the primary durable store made unavailable, the harness completes resume via the JSON fallback with **no** loss of resume capability, and clearly indicates fallback mode.
- **SC-008**: The cached-prefix resume verification passes: re-invoking a run with an unchanged leading sequence returns cached results for the unchanged prefix and re-executes only from the first changed/new step (verification result recorded).
- **SC-009**: The `multi-protocol-link-layer` marathon can be driven through all seven buildkit stages using the harness across at least three deliberate session boundaries without any loss of state or any manual restart instruction.
- **SC-010**: Each completed logical block produces an automatic commit + push under the preauthorization, staging only that block's files; a blocked push escalates rather than forcing in **100%** of trials.

## Assumptions

- **Mandated composition (Gabi, 2026-06-05)**: the harness MUST combine a buildkit skill + Python + PGLite + DBOS + a JSON backing store, as specified in the roadmap brief. This is a hard constraint, not a chosen default.
- **Compose, don't reinvent (Gabi, 2026-06-05)**: the Claude Code dynamic Workflow tool is available here and now and MUST be composed for orchestration (fan-out, per-agent JSONL journaling, `resumeFromRunId` cached-prefix resume, `budget.spent()/remaining()`, background+notify). The harness adds only what Workflow lacks: cross-session durable checkpoint + per-stage approval gate + JSON fallback. Workflow resume is same-session only.
- **Scope discipline (Gabi, 2026-06-05)**: "small/fast" means focused precisely on what THIS marathon (`multi-protocol-link-layer`) needs — a robust working first prototype, neither general-purpose nor corner-cutting. Generalizing into a broader marathon tool is deferred to a later feature. The harness must not become a marathon itself.
- **Scope boundary (confirmed by Gabi, 2026-06-05)**: IN = exactly the 7 specced user stories, sized as a robust-but-minimal prototype that can drive *only* the `multi-protocol-link-layer` marathon through the 7 buildkit stages. OUT = the experiment→verify→refine optimizer/loop, any general-purpose or multi-marathon generalization, and anything not required to run that one marathon. (See Clarifications.)
- **GEPA/DSPy split (confirmed by Gabi, 2026-06-05)**: the harness provides only the durable, restart-safe iteration + verification-trace substrate (experiment inputs, metric scores, accept/reject, refine history). The experiment→verify→refine optimizer/loop is out of scope — it is implementation methodology that reuses the existing codeconv GEPA/DSPy infrastructure and runs Claude-only (no external LLM API, per the project's hard rule).
- **Restart-resume order**: position is located via the established order — roadmap (what feature + stage) → buildkit pipeline state (where in the feature) → spec/plan/tasks (WIP position) — never via a hand-written restart prompt or a conversation summary.
- **Preauthorization scope**: there are exactly two standing grants — (1) commit+push per logical block (stages only that block's files; never force-pushes; never bypasses git hooks), and (2) the marathon-scoped Workflow-tool opt-in. Both are granted at marathon start, recorded durably, and revocable by Gabi. Neither relaxes the plan-approval gate or any escalation.
- **WSJF/RICE skipped**: this feature was promoted by Gabi's direct order; scoring was intentionally skipped.
- **Prerequisite relationship**: this harness is the prerequisite that `multi-protocol-link-layer` is blocked on; it is built first.
