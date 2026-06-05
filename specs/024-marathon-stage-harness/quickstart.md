# Quickstart: Marathon Stage Harness

**Feature**: 024-marathon-stage-harness | **Date**: 2026-06-05

This walkthrough proves the keystone value (US1: restart-safe resume) and the FR-011
verification spike. It assumes the `codeconv` venv and the shared PGLite bridge.

## Prerequisites

```
codeconv/.venv/Scripts/python.exe -m pip install -e codeconv   # if not already
codeconv --data-dir C:/pglite/research/glpnet doctor           # expect OVERALL OK
codeconv --data-dir C:/pglite/research/glpnet migrate          # applies 0010_marathon_schema
```

## 1. Start the marathon (records the two standing grants)

```
codeconv --data-dir C:/pglite/research/glpnet marathon start \
  --feature multi-protocol-link-layer --branch 0NN-multi-protocol-link-layer \
  --budget 2000000 --auto --preauth-commit-push --preauth-workflow
```
Expect: `{marathon_id, preauth_commit_push:true, preauth_workflow_optin:true, budget_ceiling:2000000}`.

## 2. FR-011 verification spike (run FIRST, before relying on the substrate)

```
codeconv --data-dir C:/pglite/research/glpnet marathon verify-spike
```
Expect: a small Workflow run; re-invocation with an unchanged prefix returns **cached**
results and resumes at the first changed/new step; `spent`/`remaining` observed
throughout; a `verification_traces` row `subject=workflow-spike` recorded (SC-008).

## 3. Run a stage-block with the approval gate

```
codeconv ... marathon gate --block <id>            # presents the plan, blocks
codeconv ... marathon gate --block <id> --approve --by gabi
# block now runs as one Workflow run; checkpoints accumulate
```

## 4. Induce an interruption and resume (US1 — the keystone)

```
# ... start the block, let a few units complete, then kill the process / end the session ...
codeconv --data-dir C:/pglite/research/glpnet marathon resume --feature multi-protocol-link-layer
```
Expect (`--json`): correct `stage`, `block_id`, `wip_unit`, `completed_units`,
`remaining_units`, and the recorded approval honored — **zero re-instruction** (SC-001),
**0 completed units re-executed** (SC-002), approval **not re-asked** (SC-004).

## 5. Per-subagent re-run on failure (US3)

```
codeconv ... marathon rerun --block <id> --subagent <label>
```
Expect: only the failed subagent re-executes; succeeded siblings untouched (SC-003);
failure history retained alongside the success (FR-008).

## 6. Fallback + reconciliation (US1 edge / SC-007)

```
# make the bridge unreachable, do work (writes land in JSON), restore the bridge:
codeconv ... marathon doctor      # shows active_store=fallback during the outage
codeconv ... marathon reconcile --feature multi-protocol-link-layer
```
Expect: strictly-higher sequence fast-forwards the stale store; a true fork stops and
escalates (exit `2`) — never a silent pick.

## 7. Status cadence + budget ceiling (US5)

```
codeconv ... marathon status --feature multi-protocol-link-layer --emit
```
Expect: a report with all four fields (done / issues / tokens spent+remaining / to-do)
at least once per 5-min interval (SC-005); at the ceiling, work halts/escalates with
0 overrun (SC-006).

## 8. Preauthorized commit/push per block (US6 / SC-010)

On block completion the harness commits **only the block's files** and pushes under the
standing grant; a blocked push escalates (`push_blocked`) instead of forcing.

## Acceptance mapping

| Step | User story | Success criteria |
|---|---|---|
| 2 | US4 | SC-008 |
| 4 | US1 | SC-001, SC-002, SC-009 |
| 3 | US2 | SC-004 |
| 5 | US3 | SC-003 |
| 6 | US1 (fallback) | SC-007 |
| 7 | US5 | SC-005, SC-006 |
| 8 | US6 | SC-010 |
| trace | US7 | (append-only, refine-history order) |
