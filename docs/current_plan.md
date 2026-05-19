# Current Plan: 018 facet-3 remediation — Option 4 (agent-gate split)

Started: 2026-05-19

## Decision
Gabi approved Option 4 (split per-file child: deterministic pre-agent wf
+ out-of-DBOS agent gate + content-addressed post-agent wf). Post key =
sha8 of the artifact file (artifact-sha). Amendment written into the
canonical spec/contract (spec-first).

## Steps
- [x] 0. Diagnose 3-facet defect; facets 1&2 fixed+validated; Option 4 approved
- [x] T060 durable/__init__.py: pre_* + content-addressed post_* id helpers
- [x] T061 durable/workflows.py: split child → pre_agent/post_agent; outer gates post on artifact presence; SCC one unit; +_resume_epoch new-epoch-on-needs_agent_work
- [x] T062 pure regression (rewritten test_builder_needs_agent_propagation.py + id determinism + stage-order)
- [x] T063 @needs_bridge acceptance gate test_agent_gate_traversal.py — GREEN (plain re-drive ingests agent spec, idempotent)
- [x] T064 spec.md Amendment 2 + FR-044; dbos_workflow_model.md taxonomy+protocol; plan.md complexity note; memory
- [~] V1 full builder bridge re-validation — RUNNING (bg btzh8l65z); pure 17/17 already green
- [ ] V2 resume genuine 128-file canonical live pass; spawn real sub-agents; drive to complete (gated on V1 green)
- [ ] V3 final report; commit boundary + out/csharp decision (Gabi)

## Context
D2: steps.py step bodies reused VERBATIM; only durable/workflows.py +
durable/__init__.py change. Constraints preserved: R3, FR-003/004,
SC-002, R9-in-spirit. Live state on canonical PG17 cluster has dead
terminal epochs from buggy runs — V2 starts a fresh epoch.
Uncommitted: facet-1/2 fix + 0006 + test re-baseline (validated, green).
