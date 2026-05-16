# Current Plan: 017-conversion-plan-agents (codeconv-planagents)

Started: 2026-05-16

## Steps
- [x] 0. Read all spec/plan/tasks/contracts + feature-015 reference code
- [x] 1. Phase 1 Setup: venv+npm install, baseline running
- [x] 2. Phase 2 Foundational: 0003 migration (applied PG17), _FIELD_ORDER+round-trip, subpackage, readiness.py
- [x] 3. Phase 2 tests: readiness 17 green, schema-isolation 6 green (incl FR-020 C2), migration verified
- [x] 4. Phase 3 US1: workflow/CLI/artefact/tombstone_writer/SKILL.md; US1 bridge 13 green + artefact-val 11 green
- [x] 5. Phase 4 US2: frontier+SC-002 tests authored (bridge run pending)
- [x] 6. Phase 5 US3: SCC fixture + batch tests authored (bridge run pending)
- [x] 7. Phase 6 US4: aggregate-escalations impl + tests + SKILL escalate-don't-guess
- [x] 8. Phase 7 US5: research-agent SKILL contract + research tests
- [x] 9a. Bug fixes: 2 real SCC-batch bugs found by bridge tests, fixed + unit-locked + re-green serial
- [ ] 9. Phase 8 Polish: final serial run b11c9ywbl in flight; then finalize ← CURRENT

## Test results (serial, uncontended — authoritative)
- readiness 18/18, artefact-val 11/11 (no bridge)
- schema-isolation 6/6 (+downgrade re-confirm pending b11c9ywbl)
- US1: next 6/6, orchestration-mock 1/1, lifecycle (re-confirm pending)
- US3 SCC-batch 4/4, US4 escalations 3/3 (post-fix, serial)
- pre-feature baseline: 5 of 6 "fails" are known bridge-concurrency
  flakes (green in isolation per memory); 1 = test_sc003_two_stack_
  concurrent = pre-existing .NET-binary-absent (Sc003NpgsqlLoop.exe not
  built; feature-012 .NET interop, unrelated to 017). ZERO regressions.

## BLOCKED (for Gabi)
- T003/T004/T005 (live snapshots) + T045 (SC-009 live full pass):
  glp_runtime_net/ has 0 .dart files in this worktree (128 checked-in
  tombstones from a prior populated state) AND a real pass needs LLM
  planning sub-agents (brief forbids running them). Mirrors feature-015's
  parked live-cluster tasks. Deterministic engine + mocked-agent harness
  cover every FR/SC mechanism.

## Context
Deterministic Python tool `codeconv planagents` + `/codeconv-planagents` skill orchestration
loop. New table codeconv.dart_plans (parallel to feature-015 dart_conversions). Mirrors
feature-015 depgraph tool structure. Alembic 0003 chains off 0002. Data-dir on this exFAT
checkout = C:/pglite/research/glpnet-017 (fresh PG17 cluster, created by first migrate).
