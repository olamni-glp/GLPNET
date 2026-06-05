# Contract: Workflow-Tool Composition

The harness **composes** the Claude Code dynamic Workflow tool for in-session
orchestration and adds only cross-session durability, the approval gate, and the JSON
fallback (FR-009/010). It MUST NOT re-implement fan-out, per-agent JSONL journaling,
`resumeFromRunId` cached-prefix resume, or `budget.spent()/remaining()`.

## Mapping

| Marathon concept | Workflow-tool concept |
|---|---|
| One **stage-block** (FR-019) | One **Workflow run** (one `Workflow({script})` invocation) |
| In-stage subagents (e.g. implement) | `agent()` / `parallel()` / `pipeline()` inside the script |
| Run-linkage (`stage_blocks.workflow_run_id`) | the returned `runId` |
| In-session retry of a block | `Workflow({scriptPath, resumeFromRunId})` (cached prefix) |
| Cross-session resume | **harness durable checkpoint** (NOT Workflow resume — same-session only) |
| Budget ceiling + spent/remaining | `budget.total` / `budget.spent()` / `budget.remaining()` |

## Run lifecycle (per stage-block)

1. Gate must be `approve` (else block; FR-004/022).
2. Verify the Workflow-tool opt-in preauthorization is granted and not revoked (FR-023).
3. Launch the block's Workflow script; record `runId` as run-linkage.
4. As the run progresses, the harness `write_checkpoint`s at meaningful sub-steps,
   snapshotting `budget.spent()` and the completed/remaining units.
5. On block completion → final checkpoint → preauthorized commit/push (gitblock).
6. On in-session failure → `resumeFromRunId` retry (cached prefix). On cross-session
   failure → durable-checkpoint resume.

## FR-011 verification spike (FIRST implementation task)

A standalone smoke test, run before the marathon relies on the substrate (US4/SC-008):

- **Cached-prefix**: run a small multi-step Workflow; re-invoke with an unchanged leading
  sequence and assert the unchanged prefix returns **cached** results while execution
  resumes at the **first changed/new** step. (US4-AS1)
- **Budget**: assert `spent`/`remaining` are observable throughout the run. (US4-AS2)
- **Record the verification result durably** (a `verification_traces` row, subject
  `workflow-spike`). (FR-011)

## Budget enforcement (FR-012 / SC-006)

- Persist `budget_ceiling` on the marathon; read live `budget.spent()/remaining()`.
- When spend reaches the ceiling, end the in-flight unit at a **safe checkpoint**, then
  halt or escalate — **0 overruns past the ceiling** (SC-006), no abandoned partial unit
  (edge case "budget ceiling reached mid-subagent").

## Non-reinvention guard (FR-009)

The harness code MUST call the Workflow tool for orchestration. A reviewer check: there is
no harness-local fan-out scheduler, no harness-local per-agent journal writer, and no
harness-local cached-prefix differ — those come from the Workflow tool. The harness owns
only: checkpoints, approvals, status, trace, gitblock, reconciliation, escalation.
