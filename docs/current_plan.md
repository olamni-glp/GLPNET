# Current Plan: Parallel /speckit-implement of 016 + 017 (worktree-isolated)

Started: 2026-05-16

## Steps
- [x] 1. Mandatory reading (CLAUDE.md / DISCIPLINE / typed-glp-manual / cheat-sheet)
- [x] 2. Resolve scope: 016 ⊥ 017 independent; git-worktree isolation; normal DBOS only; fresh PG17 side clusters
- [x] 3. Create worktrees — ../GLPNET-016 (016-codeconv-init-scaffold-langpair@177a33f8), ../GLPNET-017 (new 017-conversion-plan-agents off 20bf5130)
- [x] 4. Verify spec artifacts present in both worktrees
- [x] 5. Verify PGLite 0.4.5 merged on origin/main+015/016/017; live PG16 data-migration gated → use fresh side clusters
- [ ] 6. Spawn 2 background agents (speckit-implement 016 & 017) <- CURRENT
- [ ] 7. Monitor both to completion (auto-notified; no polling)
- [ ] 8. Reconcile: review diffs, confirm per-feature tests green, collect BLOCKED tasks
- [ ] 9. Report + hand Gabi per-branch merge templates (Claude does NOT merge to main)

## Context
Parallel, worktree-isolated `/speckit-implement` of feature 016 (codeconv init+scaffold behind a langpair registry; removes tools/d2net) and feature 017 (codeconv-planagents; 46 tasks, MVP=US1). Independent per Gabi (msg "1 both are truly independent").

Each background agent: its own branch + worktree + dedicated fresh PG17 side cluster
(016 -> C:/pglite/research/glpnet-016, 017 -> C:/pglite/research/glpnet-017),
`codeconv --data-dir <side> migrate` before any bridge test. Canonical C:/pglite/research/glpnet is PG16 + gated D2NET cluster — OFF LIMITS.

NOTE (Gabi 2026-05-16): the real live PG16->PG17 data migration (canonical-cluster dump/restore) is DEFERRED — no spec/plan/tasks/implementation; the fresh per-feature side-cluster interim is the only approach in scope. Each feature's own Alembic `migrate` on its fresh PG17 side cluster still runs (schema setup, not the deferred live-data migration).

Discipline baked into agent prompts: spec-first (block+report, never guess/workaround), baseline+retest, commit-per-task by name (no `git add -A`), NO push/merge, do not modify GLP core, normal DBOS only (no new tracking infra — not in spec). Reconciliation + merge templates handled by orchestrator; only Gabi merges main.
