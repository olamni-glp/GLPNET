---
description: "Task list for prereq-patterns catalog (glpnet) — branch 011-prereq-patterns-catalog"
---

# Tasks: prereq-patterns catalog (glpnet)

**Input**: Design documents from `specs/011-prereq-patterns-catalog/` — `plan.md`, `spec.md`, `research.md`, `data-model.md`, `quickstart.md`, `contracts/README.md`.
**Prerequisites**: `plan.md` (PASS), `spec.md` (PASS — clarifications resolved), `research.md` (PASS), `data-model.md` (PASS), `contracts/README.md` (placeholder; concrete contracts imported in Phase 2).

**Tests**: This is a documentation-catalog feature. No code-test tasks. The conformance script (Phase 5) verifies structural rules; the SC-003 / SC-004 pglite regression checks are deferred to the first glpnet feature that adopts the merged bridge (documented in `prereq-patterns/pglite/sources.md`).

**Organization**: Tasks are grouped by user story per `spec.md`. US1 + US2 are both P1 (joint MVP). US3 (P2) and US4 (P3) are verification/attribution polish.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks).
- **[Story]**: `[US1]`, `[US2]`, `[US3]`, `[US4]` for user-story-phase tasks; absent in Setup, Foundational, and Polish phases.
- File paths are absolute or repo-relative from `D:/BSTDEV/research/GLP/GLPNET/`.

## Path Conventions

This is a documentation-catalog feature. Catalog content lives at the new top-level peer `prereq-patterns/`; speckit artefacts (plan/research/data-model/quickstart/contracts/tasks/handover) live under `specs/011-prereq-patterns-catalog/`. The cited pglite bridge code is JavaScript (`.mjs`) — Node ≥ 18 implicit. No `src/` or `tests/` directories are created.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the catalog root directory and confirm the AIGRID source is reachable for the import work.

- [ ] T001 Create the catalog root directory at `prereq-patterns/` (top-level peer of `specs/`, `docs/`, `programs/`, `glp_runtime/`, `glp_multiagent/`, `test/`) per FR-001
- [ ] T002 Verify the AIGRID source repo is reachable at `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/` and at `D:/BREENDEV/aigrid/AWS-Infra/specs/`; if unreachable, surface as a blocker and stop (per `spec.md` Assumption 1, `research.md` D)
- [ ] T003 Capture the AIGRID branch name + commit SHA at the time of import; record in `specs/011-prereq-patterns-catalog/contracts/README.md` for use throughout Phase 2 and pattern `sources.md` `@<branch>` pinning

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Import + scrub the six format contracts. Every governance file and every per-pattern file references one of these contracts, so no story work can begin until they exist and are scrubbed.

**⚠️ CRITICAL**: No user-story task may begin until T009 passes.

- [ ] T004 [P] Copy AIGRID `specs/001-prereq-patterns-pglite/contracts/description_md_format.md` verbatim to `specs/011-prereq-patterns-catalog/contracts/description_md_format.md` (FR-005)
- [ ] T005 [P] Copy AIGRID `specs/001-prereq-patterns-pglite/contracts/applicability_md_format.md` verbatim to `specs/011-prereq-patterns-catalog/contracts/applicability_md_format.md` (FR-005)
- [ ] T006 [P] Copy AIGRID `specs/001-prereq-patterns-pglite/contracts/sources_md_format.md` verbatim to `specs/011-prereq-patterns-catalog/contracts/sources_md_format.md` (FR-005)
- [ ] T007 [P] Copy AIGRID `specs/001-prereq-patterns-pglite/contracts/directory_md_format.md` verbatim to `specs/011-prereq-patterns-catalog/contracts/directory_md_format.md` (FR-005)
- [ ] T008 [P] Copy AIGRID `specs/001-prereq-patterns-pglite/contracts/howto_md_format.md` verbatim to `specs/011-prereq-patterns-catalog/contracts/howto_md_format.md` (FR-005)
- [ ] T009 [P] Copy AIGRID `specs/002-add-prereq-patterns-batch/contracts/policies_md_format.md` verbatim to `specs/011-prereq-patterns-catalog/contracts/policies_md_format.md` (FR-005)
- [ ] T010 Apply the FR-011 scrubbing rules listed in `specs/011-prereq-patterns-catalog/contracts/README.md` to all six contracts: replace `BreenLake`/`breenlake` with footnote-only references, replace `~/.aigrid/...` with `D:/BSTDEV/research/glpnet-datalake/...`, replace AIGRID `specs/00[1-9]-...` cross-refs with `specs/011-prereq-patterns-catalog/contracts/...`, remove `opskit feature 004`. The single allowed retention: `sources_md_format.md` may describe the upstream-citation convention referencing AIGRID upstream paths abstractly (FR-011 exception)
- [ ] T011 Verify each scrubbed contract still parses as Markdown; verify all internal references point to expected glpnet-local path *shapes* (regex-level: under `prereq-patterns/`, under `specs/011-prereq-patterns-catalog/`, or under `D:/BSTDEV/research/glpnet-datalake/...`; no AIGRID paths, no `~/.aigrid/`); verify no live `BreenLake`/`aigrid`/`opskit` cross-references remain outside footnotes (advance gate for SC-008). NOTE: target-existence is intentionally NOT checked here — Phase 3 hasn't authored the referenced governance / per-pattern files yet; T030 (C5) enforces target-existence post-Phase-3.
- [ ] T012 Promote `specs/011-prereq-patterns-catalog/contracts/README.md` from "expected files" placeholder to "what was imported" record: replace every `<branch>` placeholder with the concrete branch+SHA captured in T003

**Checkpoint**: All six format contracts exist, scrubbed, link-verified, and `contracts/README.md` records branch+SHA. User stories may now proceed.

---

## Phase 3: User Story 1 — Future glpnet feature finds and copies a prerequisite (Priority: P1) 🎯 MVP

**Goal**: A glpnet developer can locate a curated prerequisite pattern via `directory.md`, read its `description.md`, follow `sources.md`, and consult `applicability.md` — all from inside the glpnet repo, no AIGRID lookup required.

**Independent Test**: Pick any non-pglite pattern (e.g., `dbos`). Read its three files end-to-end. Following only files inside glpnet, locate the cited upstream artefacts via `sources.md` `Upstream` column with `@<branch>` pinning. (The pglite-specific test in spec US1 Independent Test is achievable only after US2 completes.)

### Implementation for User Story 1

- [ ] T013 [US1] Author `prereq-patterns/howto.md` per `specs/011-prereq-patterns-catalog/contracts/howto_md_format.md`; link the format-contract references to `specs/011-prereq-patterns-catalog/contracts/`, NOT to AIGRID `specs/` (FR-002, FR-005 link target)
- [ ] T014 [US1] Author `prereq-patterns/policies.md` per `specs/011-prereq-patterns-catalog/contracts/policies_md_format.md`. Include Policy 1 verbatim from AIGRID (no cleartext auth tokens; secret-material hashes restricted to `{Argon2id, scrypt, bcrypt}`; `Applies to.` = `dbos`, `flask-sqlalchemy-alembic-api`, `background-task-manager`, `local-secrets-store`) per FR-CC-1 / FR-015. Add Policy 2 with `D:/BSTDEV/research/glpnet-datalake/<pattern-or-app>/<data-class>/<partition>.parquet` as the destination convention per FR-CC-2 / FR-010. Mention BreenLake only as an "external sibling, may share host" footnote (FR-011)
- [ ] T015 [US1] Author `prereq-patterns/directory.md` per `specs/011-prereq-patterns-catalog/contracts/directory_md_format.md`. List 8 patterns: `pglite` (no suffix — active), then `dbos (draft)`, `flask-sqlalchemy-alembic-api (draft)`, `pglite-backup-restore (draft)`, `blazor-spa-bg-api (draft)`, `background-task-manager (draft)`, `local-secrets-store (draft)`, `secure-signatures (draft)` — in that source order (FR-013, FR-012)
- [ ] T016 [P] [US1] Author `prereq-patterns/dbos/description.md`, `prereq-patterns/dbos/applicability.md`, `prereq-patterns/dbos/sources.md` — `Status: draft`; substantive H3 from AIGRID OR FR-016 triviality line in `applicability.md`; 4-column table in `sources.md` with AIGRID upstream pinned `@<branch>` (FR-003, FR-004, FR-012, FR-016, FR-017)
- [ ] T017 [P] [US1] Author `prereq-patterns/flask-sqlalchemy-alembic-api/{description.md,applicability.md,sources.md}` — same rules; `Status: draft` (FR-003, FR-004, FR-012, FR-016, FR-017)
- [ ] T018 [P] [US1] Author `prereq-patterns/pglite-backup-restore/{description.md,applicability.md,sources.md}` — same rules; `Status: draft` (FR-003, FR-004, FR-012, FR-016, FR-017)
- [ ] T019 [P] [US1] Author `prereq-patterns/blazor-spa-bg-api/{description.md,applicability.md,sources.md}` — same rules; `Status: draft` (FR-003, FR-004, FR-012, FR-016, FR-017)
- [ ] T020 [P] [US1] Author `prereq-patterns/background-task-manager/{description.md,applicability.md,sources.md}` — same rules; `Status: draft` (FR-003, FR-004, FR-012, FR-016, FR-017)
- [ ] T021 [P] [US1] Author `prereq-patterns/local-secrets-store/{description.md,applicability.md,sources.md}` — same rules; `Status: draft` (FR-003, FR-004, FR-012, FR-016, FR-017)
- [ ] T022 [P] [US1] Author `prereq-patterns/secure-signatures/{description.md,applicability.md,sources.md}` — same rules; `Status: draft` (FR-003, FR-004, FR-012, FR-016, FR-017)
- [ ] T023 [US1] Verify each authored pattern dir contains exactly three files; none collapses to its H1 header; each `applicability.md` has at minimum one substantive `### <consumer-name>` H3 OR the FR-016 triviality line; each `sources.md` has the 4-column header (FR-004, FR-016, FR-017 — advance gate for SC-006)

**Checkpoint**: All 7 non-pglite patterns + governance + directory.md exist. `directory.md` lists pglite first but `prereq-patterns/pglite/` is still empty until US2 completes — note this as expected interim state. US1's independent test on any non-pglite pattern works now.

---

## Phase 4: User Story 2 — pglite migration preserves glpnet's distinctive learnings (Priority: P1) 🎯 MVP (joint with US1)

**Goal**: Glpnet's pre-existing pglite learnings (no-pg-gateway hand-rolled bridge, two diagnosed bugs fixed, Npgsql/psqlODBC compat) are preserved AND AIGRID's serialization/lifecycle/Python-consumer learnings are incorporated, with full traceability via the migration analysis document.

**Independent Test**: Read `pglite-merge-analysis.md` end-to-end. Every distinguishing feature of either pre-merge bridge is classified as `present-in-merged` / `superseded-with-rationale` / `dropped-with-rationale`. Zero unclassified.

### Implementation for User Story 2

- [ ] T024 [US2] Author `specs/011-prereq-patterns-catalog/pglite-merge-analysis.md` (FR-009). Enumerate every distinguishing feature of glpnet's `docs/research/pgbridge-reference/bridge-direct.mjs` (floor: hand-rolled minimal Postgres-wire server, implicit-Sync-after-execProtocolRaw fix, pg-gateway 0.3.0-beta.4 response-corruption avoidance, Npgsql/psqlODBC compat — FR-007) and AIGRID's `pglite_bridge.mjs` (floor: `globalWorkChain`, per-conn `workChain`, `endsAtFlushBoundary()`, synthetic `ROLLBACK` startup handshake, Windows `DETACHED_PROCESS` lifecycle, `sidecar.json` discovery, `@electric-sql/pglite@0.2.17` pin — FR-008). Classify each feature as `present-in-merged` / `superseded-with-rationale` / `dropped-with-rationale`. Zero unclassified (SC-005)
- [ ] T025 [US2] Implement the merged pglite bridge file at `prereq-patterns/pglite/<bridge-filename>.mjs` per the structural-skeleton decision in `research.md` § B2: AIGRID `pglite_bridge.mjs` skeleton + grafted glpnet no-pg-gateway startup path + the two glpnet bug fixes (implicit-Sync, response-corruption). Filename per `research.md` § D1 — decide by least-surprise (likely `pglite_bridge.mjs` to match AIGRID convention)
- [ ] T026 [P] [US2] Author `prereq-patterns/pglite/description.md` — `Status: active`; what the pattern produces, why it matters, how a feature uses it (FR-004, FR-012)
- [ ] T027 [P] [US2] Author `prereq-patterns/pglite/applicability.md` SUPERSET — `### DBOS`, `### SQLAlchemy`, `### Alembic`, `### psycopg` (carried verbatim from AIGRID where content describes consumer-class behaviour, scrubbed of AIGRID-internal call sites), `### Npgsql`, `### psqlODBC` (new — `Pooling=false` / queue-of-one connection discipline, no prepared-statement caching equivalent, behaviour against the merged hand-rolled wire-protocol bridge), `### Other consumers` (asyncpg, psycopg2, ORM wrappers — partial-fit notes carried from AIGRID) (FR-018)
- [ ] T028 [US2] Author `prereq-patterns/pglite/sources.md` — 4-column `Path | Upstream | Action | Summary` table citing BOTH (a) AIGRID `prereq-patterns/pglite/pglite_bridge.mjs@<branch>` and the rest of AIGRID's pglite cluster, AND (b) glpnet `docs/research/pgbridge-reference/bridge-direct.mjs`, `docs/research/pgbridge-reference/README.md`, `docs/research/pgbridge-reference/package.json`. Per-row sub-section explains what each contributed to the merged bridge (FR-017, US4 Acceptance Scenario 2). Document SC-003 (Npgsql/psqlODBC connectivity) and SC-004 (psycopg-style invariant) regression-check procedures here for future glpnet adopters (`quickstart.md` Flow D)
- [ ] T029 [US2] Decide `docs/research/pgbridge-reference/` disposition per FR-014 (recommendation in `research.md` § B1: retain with `MIGRATED.md`). If retained: author `docs/research/pgbridge-reference/MIGRATED.md` with a short prose note pointing at `prereq-patterns/pglite/` as the new canonical home. If removed: author one-file forwarding stub at the same location

**Checkpoint**: pglite/ pattern dir is fully populated, the merged bridge exists, and the migration-analysis document classifies every feature with zero unclassified. US1's independent test using pglite (spec US1 Independent Test) is now runnable once a future feature actually launches the bridge.

---

## Phase 5: User Story 3 — Catalog governance is fully glpnet-local (Priority: P2)

**Goal**: A maintainer reviewing a PR can verify the PR against glpnet-local governance + format-contract files only, no AIGRID lookup required. The conformance script enforces this property mechanically.

**Independent Test**: Run `conformance-check.ps1` against the catalog. C3 (link self-containment), C4 (no-AIGRID grep), and C5 (format-contract reachability) all pass. Manual spot-check: `grep -i 'breenlake\|aigrid\|opskit'` over `prereq-patterns/` returns matches only inside explicit "external sibling reference" notes.

### Implementation for User Story 3

- [ ] T030 [US3] Author `specs/011-prereq-patterns-catalog/conformance-check.ps1` implementing checks C1 (three-files-per-pattern), C2 (lifecycle agreement), C3 (link self-containment), C4 (no-AIGRID grep), C5 (format-contract reachability), C6 (migration-analysis completeness) per `quickstart.md` Flow C. PowerShell-only, no third-party dependency (per `research.md` § B4)
- [ ] T031 [US3] Run `conformance-check.ps1` from repo root; capture output to `specs/011-prereq-patterns-catalog/conformance-output.txt`
- [ ] T032 [US3] Fix any C1–C6 failures revealed by T031 (likely candidates: missed scrubbing in T010, broken internal links in T013/T014, lifecycle drift between `description.md` and `directory.md` in T015 / T026); re-run `conformance-check.ps1` until every check passes
- [ ] T033 [US3] Author `specs/011-prereq-patterns-catalog/handover.md` with C1–C6 results, the FR-014 disposition decision recorded in T029, and any deviations from the spec / plan flagged for review

**Checkpoint**: Conformance script lands, all 6 checks PASS, handover doc records the implementation decisions. US3's independent test passes mechanically.

---

## Phase 6: User Story 4 — Source attribution and bidirectional traceability (Priority: P3)

**Goal**: Every cited AIGRID upstream path is `@<branch>`-pinned and resolves at the AIGRID repo root; every cited glpnet path resolves inside glpnet. The closed `Action` vocabulary `{Read, Copy, Model}` is honoured throughout.

**Independent Test**: For each pattern's `sources.md`, every `Path` resolves to a real file (in glpnet or in AIGRID) and every `Upstream` includes `@<branch>` pinning. Action column entries are exactly `Read`, `Copy`, or `Model`.

### Implementation for User Story 4

- [ ] T034 [US4] Verify every `prereq-patterns/*/sources.md` `Action` column uses ONLY `Read`, `Copy`, or `Model` (US4 Independent Test); fix any deviations introduced in Phase 3 / Phase 4
- [ ] T035 [US4] Verify every AIGRID `Upstream` cell across all `prereq-patterns/*/sources.md` includes `@<branch>` pinning matching the branch+SHA captured in T003 (FR-017); fix any missing pins
- [ ] T036 [US4] Verify `prereq-patterns/pglite/sources.md` cites BOTH AIGRID's `pglite_bridge.mjs` (under AIGRID upstream) AND glpnet's `docs/research/pgbridge-reference/bridge-direct.mjs` (under glpnet upstream), with summaries explaining what each contributed (US4 Acceptance Scenario 2)

**Checkpoint**: Every citation across the catalog conforms to attribution discipline. US4's independent test passes.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final gates, version bookkeeping, scope-confinement audit.

- [ ] T037 Run `conformance-check.ps1` once more as the final pre-merge gate; append result to `specs/011-prereq-patterns-catalog/handover.md`
- [ ] T038 [P] Record the intended CalVer slot (`vYYYY.MM.DD[-N]`) per `docs/VERSIONING.md` in `specs/011-prereq-patterns-catalog/handover.md` for Gabi to apply at merge time. Do NOT bump `VERSION` on the feature branch — CalVer applies on `main` after merge per CLAUDE.md / `docs/VERSIONING.md`. If `VERSION` does not exist at the repo root, also include in the handover note that creating it is part of the merge step (not this feature).
- [ ] T039 [P] Append a CHANGELOG entry covering this feature; reference `specs/011-prereq-patterns-catalog/spec.md`, `plan.md`, `tasks.md`. Pre-flight: read `CHANGELOG.md` first; if it does not exist at the repo root, create it with a single H1 (`# Changelog`) and the new entry as the first H2 section. If it exists, append the new H2 section under the existing H1.
- [ ] T040 Run `git status` and verify no files outside `prereq-patterns/`, `specs/011-prereq-patterns-catalog/`, `docs/research/pgbridge-reference/` (FR-014 disposition only), `CLAUDE.md` (SPECKIT block), and `CHANGELOG.md` have changed (per `plan.md` Assumption — no `programs/`, `glp_runtime/`, `glp_multiagent/`, or `test/` modifications). NOTE: `VERSION` is intentionally NOT bumped in this feature (see T038); pre-existing modifications to `.claude/settings.local.json`, `.specify/feature.json`, and untracked `.D2NET/` predate this feature and are expected noise.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup, T001–T003)**: No dependencies — start immediately.
- **Phase 2 (Foundational, T004–T012)**: Requires Phase 1 complete. T010 depends on T004–T009 (the verbatim copies). T011 depends on T010. T012 depends on T011 + T003.
- **Phase 3 (US1, T013–T023)**: Requires Phase 2 complete (all six format contracts exist + scrubbed).
- **Phase 4 (US2, T024–T029)**: Requires Phase 2 complete. Largely independent of Phase 3, EXCEPT: T028 (`pglite/sources.md`) cites the merged bridge produced by T025; T015 (`directory.md` in Phase 3) lists `pglite` first — T015 can be authored before T026 lands `pglite/description.md`, but the SC-007 lifecycle-drift check in T032 requires both files to exist with agreeing `Status:` lines.
- **Phase 5 (US3, T030–T033)**: Requires Phase 3 + Phase 4 complete (conformance check operates on the populated catalog).
- **Phase 6 (US4, T034–T036)**: Requires Phase 4 + relevant Phase 3 tasks complete (`sources.md` files exist for all 8 patterns).
- **Phase 7 (Polish, T037–T040)**: Requires Phases 5 + 6 complete.

### User Story Dependencies

- **US1 (P1)**: Depends on Phase 2. Independent of US2 except that US1's directory.md (T015) lists pglite, which is populated by US2.
- **US2 (P1)**: Depends on Phase 2. Independent of US1.
- **US3 (P2)**: Depends on US1 + US2 (conformance script needs the catalog to exist).
- **US4 (P3)**: Depends on US1 + US2 (`sources.md` files must exist to verify attribution).

### Within Each User Story

- Models before files that reference them — i.e., format contracts (Phase 2) before any governance / per-pattern file (Phases 3–4).
- Lifecycle agreement: `description.md` `Status:` line and `directory.md` suffix MUST be authored together for each pattern, or the conformance check (T032 C2) will fail.
- pglite sources.md (T028) depends on the merged-bridge filename being settled (T025).

### Parallel Opportunities

- **Phase 1**: T001 + T002 + T003 are independent and can be done in any order or in parallel.
- **Phase 2**: T004–T009 are all `[P]` — six file copies are independent. T010 must wait for all six. T011 must wait for T010. T012 must wait for T011.
- **Phase 3**: T016–T022 are all `[P]` — seven independent pattern dirs. T013, T014, T015 can be done before, after, or interleaved with the seven (different files).
- **Phase 4**: T026 + T027 are `[P]` against each other; T028 depends on T025 + T024; T029 depends on T024 (informs the disposition rationale).
- **Phase 7**: T038 + T039 are `[P]` against each other.

---

## Parallel Example: Phase 2 (foundational format-contract import)

```bash
# Six AIGRID format-contract copies in parallel:
Task: "Copy AIGRID specs/001-prereq-patterns-pglite/contracts/description_md_format.md → specs/011-prereq-patterns-catalog/contracts/description_md_format.md"
Task: "Copy AIGRID specs/001-prereq-patterns-pglite/contracts/applicability_md_format.md → ..."
Task: "Copy AIGRID specs/001-prereq-patterns-pglite/contracts/sources_md_format.md → ..."
Task: "Copy AIGRID specs/001-prereq-patterns-pglite/contracts/directory_md_format.md → ..."
Task: "Copy AIGRID specs/001-prereq-patterns-pglite/contracts/howto_md_format.md → ..."
Task: "Copy AIGRID specs/002-add-prereq-patterns-batch/contracts/policies_md_format.md → ..."

# Then sequentially: T010 (scrub) → T011 (verify) → T012 (record branch+SHA in README)
```

## Parallel Example: Phase 3 (User Story 1, non-pglite patterns)

```bash
# Seven non-pglite pattern dirs in parallel:
Task: "Author prereq-patterns/dbos/{description.md,applicability.md,sources.md}"
Task: "Author prereq-patterns/flask-sqlalchemy-alembic-api/{description.md,applicability.md,sources.md}"
Task: "Author prereq-patterns/pglite-backup-restore/{description.md,applicability.md,sources.md}"
Task: "Author prereq-patterns/blazor-spa-bg-api/{description.md,applicability.md,sources.md}"
Task: "Author prereq-patterns/background-task-manager/{description.md,applicability.md,sources.md}"
Task: "Author prereq-patterns/local-secrets-store/{description.md,applicability.md,sources.md}"
Task: "Author prereq-patterns/secure-signatures/{description.md,applicability.md,sources.md}"
```

---

## Implementation Strategy

### MVP First (US1 + US2 jointly)

1. Complete Phase 1 (Setup): T001–T003.
2. Complete Phase 2 (Foundational): T004–T012 — format contracts are the blocking prerequisite.
3. Complete Phase 3 (US1) and Phase 4 (US2) — these can be interleaved by a single implementer or split between two.
4. **STOP and VALIDATE**: Run `conformance-check.ps1` (which Phase 5 will formalize, but a manual link-check + grep-check satisfies the gate). All 8 patterns exist with three required files; pglite has the merged bridge + analysis doc.
5. MVP is the joint US1 + US2 deliverable.

### Incremental Delivery

1. Phase 1 + Phase 2 → format contracts ready (no user-visible value yet).
2. Phase 3 (US1) → 7 non-pglite patterns + governance browsable; pglite/ directory exists but only has placeholder Status entries until US2 completes.
3. Phase 4 (US2) → pglite merge complete; spec US1 Independent Test using pglite is now runnable.
4. Phase 5 (US3) → conformance script lands; quality gate is mechanical.
5. Phase 6 (US4) → attribution discipline verified.
6. Phase 7 (Polish) → version + changelog + scope-confinement audit; ready for merge to `main` per `docs/BRANCHING.md`.

### Single-Implementer Strategy

Given the catalog-import nature, a single implementer is the realistic case. Recommended order:

1. T001 → T002 → T003 (sequential setup).
2. T004–T009 in a single batch (six file copies); then T010 → T011 → T012.
3. T013 → T014 → T015 (governance + directory).
4. T016–T022 in any order (seven non-pglite pattern dirs).
5. T024 (analysis doc — design ahead of code) → T025 (merged bridge) → T026 + T027 in parallel → T028 → T029 (disposition).
6. T023 (US1 audit) — can run after T013–T022 + T026 + T027.
7. T030 → T031 → T032 → T033 (US3 conformance).
8. T034 → T035 → T036 (US4 attribution).
9. T037 → T038 + T039 → T040 (polish).

---

## Notes

- This is a documentation-catalog feature — the only "code" produced is the merged pglite `.mjs` bridge file (Phase 4) and the `conformance-check.ps1` script (Phase 5). No `programs/`, `glp_runtime/`, `glp_multiagent/`, or `test/` files are added or modified.
- The CLAUDE.md SPECKIT-block plan reference was already updated from `010-scaffold-skill/plan.md` to `011-prereq-patterns-catalog/plan.md` during `/speckit-plan`; no task here re-does that.
- Pre-existing `M` files (`.claude/settings.local.json`, `.specify/feature.json`) and untracked `.D2NET/` are unrelated to this feature; T040's git-status check should treat them as expected-noise (they pre-date Phase 1).
- Commit cadence per `docs/DISCIPLINE.md` §6.3: commit after each task or coherent group; single-line commit messages; stage by name, never `git add -A`.

### Deferred Verification (per /speckit-analyze remediation V1)

- **SC-003 (Npgsql / psqlODBC connectivity, 100 sequential cycles)** and **SC-004 (psycopg-style concurrent-pipeline invariant)** are buildable success criteria that this feature deliberately does NOT verify. Spec, plan, and tasks all defer verification to the first glpnet feature that *adopts* the merged bridge (i.e., starts the bridge as part of its own work). T028 (`prereq-patterns/pglite/sources.md`) documents the regression-check procedures verbatim from `quickstart.md` Flow D so the future adopter has a turn-key script.
- **Risk acknowledged**: between this feature's merge and the first adopter feature's merge, the bridge is unverified end-to-end. Mitigation in this feature: T024's classification of every distinguishing feature in `pglite-merge-analysis.md` (zero unclassified) is a static analogue — every feature called out in FR-007 / FR-008 must appear in the analysis with rationale.
- **Action for next session running `/speckit-implement`**: keep the SC-003 / SC-004 procedures in `prereq-patterns/pglite/sources.md` complete and copy-paste-runnable; do not silently dilute them.
