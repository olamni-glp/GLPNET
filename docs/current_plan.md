# Current Plan: 012-codeconv-runner — speckit chain to /speckit-implement

Started: 2026-05-09
Branch: `012-codeconv-runner`
Spec: `specs/012-codeconv-runner/spec.md` (clarified Session 2026-05-09)

## 🔴 Branch Instructions

Work on the existing `012-codeconv-runner` branch. Do NOT create a new `claude/...` branch.

```
git checkout 012-codeconv-runner
git pull origin 012-codeconv-runner
```

All commits go on this branch. When done, Gabi merges into `main`.

## Steps

- [x] 1. /speckit-plan → wrote `specs/012-codeconv-runner/plan.md`, `research.md`, `data-model.md`, `contracts/` (7 files), `quickstart.md`. Updated `CLAUDE.md` SPECKIT marker to point at the new plan.
- [x] 2. /speckit-tasks → wrote `specs/012-codeconv-runner/tasks.md` (T001–T092, organised by 4 user stories US1–US4 plus shared phases).
- [x] 3. /speckit-analyze → wrote `specs/012-codeconv-runner/analysis.md`. 6 top remediations identified.
- [x] 4. Apply top remediations (in-document only) → applied R1 portalocker pin, R2 FR-027 .NET flag preservation in T038, R3 added T091 (.NET pooling grep), R4 added T092 (COPY FROM STDIN grep), R5 added T010a (D2NET schema discovery), R6 amended T057 with Alembic-then-DBOS order.
- [ ] 5. /speckit-implement (in NEW session) ← CURRENT — this session ends here per Gabi's request.

## Context

Feature 012-codeconv-runner consolidates PGLite into a single repo-wide deployment at `.pgdb/` with OS-level cross-process locking; migrates `.D2NET/pgdb/` data into the unified location; converts D2NET .NET tools to bridge clients; ships `/codeconv-runner` (Python CLI on DBOS-over-PGLite) plus first registered tool `/codeconv-discover` (walks `glp_runtime_net/`, populates `codeconv` schema, writes `.codeconv/tombstones/`).

Spec was clarified in Session 2026-05-09 (16 questions answered). Plan + tasks + analysis are locked in.

## How to resume in a fresh session

1. New Claude Code session in this repo. CLAUDE.md mandatory reading auto-loads.
2. Claude reads this `current_plan.md` per CLAUDE.md "Multi-Stage Task Persistence" rule.
3. Read `specs/012-codeconv-runner/plan.md` (technical context) and `tasks.md` (T001–T092). Optionally read `analysis.md` for the remediation history.
4. Type `/speckit-implement` to begin Phase 1 (Setup) of `tasks.md`.

The `/speckit-implement` skill processes tasks in dependency order:
Phase 1 (Setup) → Phase 2 (Foundational, blocks all stories) → Phase 3 (US1 = MVP precondition: bridge with cross-process exclusion) → Phase 4 (US2: D2NET migration) → Phase 5 (US3: codeconv runner) → Phase 6 (US4: discover tool) → Phase 7 (Polish + cross-cutting verification).

## Files in scope (writing or modifying during /speckit-implement)

NEW directories / files:
- `codeconv/` — Python package (runner + first tool + tests + vendored libs).
- `tools/d2net/src/D2Net.BridgeClient/` — shared lock+sidecar lib for .NET clients.
- `tools/d2net/src/D2Net.PgdbMigrate/` — one-shot migration CLI.
- `prereq-patterns/pglite/tests/` — bridge unit tests.
- `.claude/skills/codeconv-runner/SKILL.md`, `.claude/skills/codeconv-discover/SKILL.md`, `.claude/skills/D2NET-pgdb-migrate/SKILL.md`.
- `.codeconv/tombstones/.orphaned/` — checked-in (with `.gitkeep`).
- `specs/012-codeconv-runner/scripts/` — SC-003 harness (Python + .NET).
- `.pgdb/` — runtime data, gitignored; populated by bridge.

MODIFIED:
- `prereq-patterns/pglite/pglite_bridge.mjs` (add lock + sidecar + log rotation; preserve all FR-005 invariants).
- `prereq-patterns/pglite/package.json` (add `proper-lockfile`).
- `prereq-patterns/pglite/description.md` (FR-012 amendment).
- `tools/d2net/src/D2Net.Init/PgBridgeProcess.cs` (delegate to BridgeClient; preserve FR-027 connection-string flags).
- `tools/d2net/src/D2Net.Scaffold/` (verify whether it self-launches a bridge — if so, same change as Init).
- `tools/d2net/D2Net.sln` (add new projects).
- `.claude/skills/D2NET-init/SKILL.md`, `.claude/skills/D2NET-scaffold/SKILL.md` (point at unified bridge).
- `.gitignore` (add `.pgdb/` and `.D2NET/pgdb.bak.*/`; do NOT ignore `.codeconv/tombstones/`).
- `CLAUDE.md` SPECKIT marker (already done; T089 adds a brief migration note elsewhere).
- `docs/known-issues.md` (T090).

## Implementation cautions

- **CLAUDE.md baseline-then-change-then-test discipline** applies. The bridge changes touch a live file used by feature 011's catalog. Run any existing tests + the existing bridge smoke (`prereq-patterns/pglite/`) before T024; commit a baseline; modify; re-run.
- **No COPY FROM STDIN** against PGLite (FR-026). T092 verifies.
- **No client-side prepared-statement caching** (FR-027). T091 verifies on .NET side; T054 enforces on Python side.
- **D2NET schema unchanged** (FR-015). T010a documents what it currently is; do NOT rewrite.
- **`proper-lockfile` Windows behaviour** is the validation criterion for research R1. If it does not honour kernel release on Windows for the chosen call shape, STOP and escalate to Gabi before lowering the lock guarantee.
- **Spec-First Development**: spec + plan + tasks + contracts are the source of truth. Implementation MUST match. Any deviation → STOP and discuss.

## Optional auto-commit hooks (not yet executed)

The repo's `.specify/extensions.yml` defines optional `after_plan` / `after_tasks` / `after_analyze` hooks that run `speckit.git.commit`. These were NOT auto-executed during this session. Before `/speckit-implement` in the new session, the resuming session may run:

```
/speckit-git-commit
```

…to land the spec-kit artefacts as a baseline commit on `012-codeconv-runner`. (Optional but recommended — keeps the implementation diff scoped.)

## Files added or modified in this session (uncommitted)

```
modified:   CLAUDE.md  (SPECKIT marker → 012)
modified:   docs/current_plan.md  (this file)
new file:   specs/012-codeconv-runner/plan.md
new file:   specs/012-codeconv-runner/research.md
new file:   specs/012-codeconv-runner/data-model.md
new file:   specs/012-codeconv-runner/quickstart.md
new file:   specs/012-codeconv-runner/tasks.md
new file:   specs/012-codeconv-runner/analysis.md
new file:   specs/012-codeconv-runner/contracts/bridge_lifecycle.md
new file:   specs/012-codeconv-runner/contracts/bridge_cli.md
new file:   specs/012-codeconv-runner/contracts/codeconv_runner_cli.md
new file:   specs/012-codeconv-runner/contracts/codeconv_tool_contract.md
new file:   specs/012-codeconv-runner/contracts/codeconv_discover_cli.md
new file:   specs/012-codeconv-runner/contracts/tombstone_format.md
new file:   specs/012-codeconv-runner/contracts/d2net_pgdb_migration_cli.md
```

(Verify with `git status --short`.)

## Resume one-liner

```
/speckit-implement
```

Each task in tasks.md lists exact file paths; the implementer follows them in order, marking each complete as it lands.
