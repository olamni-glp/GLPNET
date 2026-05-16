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
- [ ] 9. Phase 8 Polish: stale/dry-run/stamp-rebuild tests authored; US2-5 + full-suite bridge runs in flight; SC-009 ← CURRENT

## Context
Deterministic Python tool `codeconv planagents` + `/codeconv-planagents` skill orchestration
loop. New table codeconv.dart_plans (parallel to feature-015 dart_conversions). Mirrors
feature-015 depgraph tool structure. Alembic 0003 chains off 0002. Data-dir on this exFAT
checkout = C:/pglite/research/glpnet-017 (fresh PG17 cluster, created by first migrate).
