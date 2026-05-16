# Current Plan: 017-conversion-plan-agents (codeconv-planagents)

Started: 2026-05-16

## Steps
- [x] 0. Read all spec/plan/tasks/contracts + feature-015 reference code
- [ ] 1. Phase 1 Setup: baseline pytest, snapshots ← CURRENT
- [ ] 2. Phase 2 Foundational: 0003 migration, _FIELD_ORDER ext (done), subpackage skeleton, readiness.py
- [ ] 3. Phase 2 tests: readiness unit tests, schema isolation test, run migration
- [ ] 4. Phase 3 US1: workflow status/next/plan-started/plan-completed, tombstone_writer, artefact, SKILL.md, US1 tests
- [ ] 5. Phase 4 US2: frontier tests + readiness verify
- [ ] 6. Phase 5 US3: SCC fixture + batch tests + SKILL SCC protocol
- [ ] 7. Phase 6 US4: aggregate-escalations + escalation tests + SKILL escalate-don't-guess
- [ ] 8. Phase 7 US5: research-agent SKILL contract + research tests
- [ ] 9. Phase 8 Polish: stale, dry-run, stamp/rebuild tests, full suite, SC-009, docs

## Context
Deterministic Python tool `codeconv planagents` + `/codeconv-planagents` skill orchestration
loop. New table codeconv.dart_plans (parallel to feature-015 dart_conversions). Mirrors
feature-015 depgraph tool structure. Alembic 0003 chains off 0002. Data-dir on this exFAT
checkout = C:/pglite/research/glpnet-017 (fresh PG17 cluster, created by first migrate).
