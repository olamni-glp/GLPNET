# Cross-artefact analysis: 012-codeconv-runner

**Date**: 2026-05-09
**Inputs**: `spec.md` (clarified Session 2026-05-09; 30 FRs; 13 SCs; 4 user stories; 16 clarifications) · `plan.md` · `research.md` (R1–R16) · `data-model.md` · `contracts/` (7 files) · `quickstart.md` (6 flows) · `tasks.md` (T001–T090).

**Mode**: Non-destructive consistency + quality scan. No code changes; documentation amendments only where explicitly applied below.

---

## 1. Coverage matrix — spec FRs → plan / tasks

| FR | Spec subject | Tasks |
|----|---|---|
| FR-001 unified data dir | `.pgdb/` | T024, T037, T053 |
| FR-002 single bridge | OS lock, exit 5 | T024, T020, T035 |
| FR-003 lock auto-release | kernel-managed | T024 (proper-lockfile), T022 |
| FR-004 sidecar JSON | bridge.json | T024, T036 |
| FR-005 in-process serialisation | globalWorkChain etc. preserved | T024 (preservation clause) |
| FR-006 auto-spawn lifecycle | acquire→spawn→READY | T024, T035, T053 |
| FR-007 .D2NET/pgdb migration | move + backup | T037 |
| FR-008 migration refuse on conflict | exit 78 | T037, T033 |
| FR-009 migration idempotent | re-run no-op | T037, T032 |
| FR-010 D2NET → unified-bridge client | replace own bridge | T038, T039 |
| FR-011 D2NET regression-free | observable behaviour | T040 |
| FR-012 canonical bridge in-place | modify, no copy | T024 + T013 + T038 (remove old `pgbridge/`) |
| FR-013 `/codeconv-runner` slash + CLI | thin wrapper | T057, T059 |
| FR-014 DBOS over PGLite | engine + patch sequence | T054, T052 |
| FR-015 schema isolation | dbos / codeconv / D2NET | T054, T055, T086 |
| FR-016 file-system tool registry | no runner edits | T056, T051 |
| FR-017 durable workflows | DBOS resume | T072, T067 |
| FR-018 discover scope | glp_runtime_net only, generated excluded | T069, T060 |
| FR-019 inventory schema + UNIQUE | dart_files / _imports / _callers | T055, T072, T061 |
| FR-020 mechanical-only purpose | leading doc-comment verbatim | T070, T061 |
| FR-021 tombstones | YAML frontmatter + body | T071, T072 |
| FR-022 rebuild from tombstones | --from-tombstones | T072, T065 |
| FR-023 caller-graph inside-only | warn on outside | T072, T066 |
| FR-024 idempotence | zero diff on re-run | T063, T072 |
| FR-025 orphan + revival | move row + tombstone, refresh | T072, T064 |
| FR-026 no COPY FROM STDIN | invariant | **GAP — see § 4.1** |
| FR-027 no prepared-statement caching | psycopg + Npgsql + ODBC | T054 (Python); **GAP for .NET — see § 4.2** |
| FR-028 target language C# / .NET | future scope | (informational; no task) |
| FR-029 gitignore policy | `.pgdb/`, backups, but not tombstones | T005 |
| FR-030 bridge log destination + rotation | bridge.log 5MB×3 | T024, T023, T088 |

## 2. Coverage matrix — spec SCs → verification

| SC | Subject | Verification |
|----|---|---|
| SC-001 single-bridge race exit 5 in 1 s | T020, T080 (Flow A step 4) |
| SC-002 lock auto-release post-kill in 1 s | T022, T080 (Flow A step 5) |
| SC-003 concurrent two-stack 100 cycles | T026 (bridge-side smoke), T084 (full Python+.NET) |
| SC-004 D2NET migration row counts preserved | T031, T081 (Flow B step 3) |
| SC-005 D2NET regression-free | T040, T081 (Flow B step 4) |
| SC-006 discover 128 rows + 128 tombstones | T072, T082 (Flow C step 4–5) |
| SC-007 rebuild bit-for-bit | T065, T083 (Flow D step 5) |
| SC-008 idempotence zero diff | T063, T082 (Flow C step 6) |
| SC-009 resume after kill | T067, T085 (Flow F) |
| SC-010 slash discoverability | T082 (Flow C step 7; manual via Claude UI) |
| SC-011 caller-graph inside-only | T087 (DB query) + T072 enforcement |
| SC-012 schema isolation | T086 |
| SC-013 perf SLO | T068, T076, T082 (Flow C step 4 wallclock) |

## 3. Coverage matrix — clarifications → research / contracts

All 16 Session 2026-05-09 clarifications are addressed.

| Clarification | Where reflected |
|---|---|
| Q1 location `.pgdb/` | plan § Project Structure; data-model § 1; contracts/bridge_lifecycle |
| Q2 single-bridge granularity | research R1; contracts/bridge_lifecycle |
| Q3 OS-level lock proper-lockfile | research R1; contracts/bridge_lifecycle § Lock semantics |
| Q4 D2NET = bridge client | plan; contracts/d2net_pgdb_migration_cli; tasks T038/T039 |
| Q5 migration is move-with-backup | research R8; contracts/d2net_pgdb_migration_cli; tasks T037 |
| Q6 tombstones format `.codeconv/tombstones/<rel>.dart.md` + YAML | contracts/tombstone_format; data-model § 4 |
| Q7 scope = `glp_runtime_net/` only | contracts/codeconv_discover_cli § Subtree scope; tasks T060 |
| Q8 target = Dart→C#/.NET | spec FR-028; informational |
| Q9 mechanical purpose only (single block) | contracts/tombstone_format; research R11 |
| Q10 auto-spawn primary | contracts/bridge_lifecycle; research R2 |
| Q11 schema isolation triple | contracts/codeconv_runner_cli § Engine sequence; data-model § 1 (header table) |
| Q12 D2NET migration boundary (only `pgdb/` moves) | contracts/d2net_pgdb_migration_cli; data-model § 3 |
| Q13 caller-graph inside-only | research R12; contracts/codeconv_discover_cli; tasks T066 |
| Q14 perf SLO 60s/5s | research R15; contracts/codeconv_discover_cli § Performance; tasks T068 |
| Q15 row uniqueness on edge tables | data-model § 1.2/1.3 (UNIQUE); tasks T055 |
| Q16 gitignore policy | tasks T005 |
| Q17 orphan revival | data-model § 1.4; contracts/codeconv_discover_cli step 5; tasks T064 |
| Q18 bridge log destination | contracts/bridge_cli; research R9; tasks T023, T088 |

## 4. Gaps identified

### 4.1 FR-026 (`no COPY FROM STDIN`) lacks a verification task

**Severity**: Low. The requirement is a "MUST NOT generate" invariant — no task could produce it. Still, the absence of a guard means a future tool author could add COPY-FROM-STDIN code without anything catching it.

**Remediation (applied below)**: Add a Phase 7 task that greps the introduced source trees (`codeconv/`, `tools/d2net/src/D2Net.BridgeClient/`, `tools/d2net/src/D2Net.PgdbMigrate/`) for `COPY ... FROM STDIN` patterns and fails CI if any match.

### 4.2 FR-027 .NET coverage (Pooling=false on Npgsql) is missing from D2NET tasks

**Severity**: Medium. Spec FR-027 mandates `Pooling=false` for Npgsql and the documented connection-string flags for psqlODBC. Plan/tasks cover the Python side (T054 via `pglite_engine_kwargs`) but no task explicitly verifies the .NET connection strings carry `Pooling=false` post-conversion.

D2NET pre-existing code uses `OdbcConnectionStringBuilder.cs` for psqlODBC. Conversion to BridgeClient must preserve the existing `Pooling=false` (and `UseDeclareFetch=0`) flags AND add them to any new Npgsql usage if introduced.

**Remediation (applied below)**: Amend T038 description to call out preservation of FR-027 connection-string flags; add Phase 7 task T091 to grep the .NET source for any `Pooling=true` or absent-Pooling Npgsql connection strings.

### 4.3 Python lock library is not yet pinned

**Severity**: Medium. Research R1 picks `proper-lockfile` for the Node bridge but defers Python's library to "use portalocker or fcntl+msvcrt". T053 mirrors that ambiguity. T002's pyproject.toml deps list does not include the lock library.

**Remediation (applied below)**: Pin `portalocker>=2.8` (cross-platform, kernel-managed, the de-facto Python equivalent of `proper-lockfile`). Amend T002 + T053 + research R1 to record the choice.

### 4.4 D2NET schema discovery is documentation-only, no task

**Severity**: Low. Research R14 says "task is documentation, not code change" but no task exists to write the documentation. data-model.md mentions D2NET schemas in the header table but does not list them.

**Remediation (applied below)**: Add T010a — inspect `tools/d2net/src/D2Net.Init/SchemaInitializer.cs` and `Schema/` and document the actual schema(s) used in `data-model.md` § D2NET schemas (new subsection). One-shot read-only task.

### 4.5 `codeconv migrate` glue between Alembic + DBOS is implicit

**Severity**: Low. T054 implements `setup_dbos()`; T055 implements Alembic migrations; T057 implements the `migrate` CLI command. No task explicitly says "the `migrate` CLI calls `Alembic.upgrade('head')` THEN `dbos.migrate()`". An implementer might run them in the wrong order or skip one.

**Remediation (applied below)**: Amend T057 description to specify the order (Alembic first → DBOS second) and the idempotence requirement (re-running `migrate` on already-migrated DB is a no-op).

### 4.6 `--no-orphan-revival` flag was introduced in contracts/codeconv_discover_cli but lacks a test

**Severity**: Very low. Edge case for testing. The contracts document the flag; no test exercises it.

**Remediation**: Not applied — orphan revival is the default per FR-025; testing the negation is a robustness concern, not a spec requirement. Documented here for awareness; can be added later if regression-prone.

### 4.7 Plan's "Project Structure" lists `D2Net.BridgeClient` and `D2Net.PgdbMigrate` but solution-file integration is implicit

**Severity**: Very low. T004 says "Add both to `tools/d2net/D2Net.sln`." No explicit task verifies `dotnet sln add` succeeded before T035/T037 start. T014 implicitly catches it via build verification.

**Remediation**: Not applied — T014's build check is sufficient evidence.

### 4.8 Performance test (T068) needs a baseline measurement before SC-013 is enforceable

**Severity**: Low. The 60s / 5s budgets in SC-013 are upper bounds. Without a baseline measurement at first implementation, regressions can creep in undetected.

**Remediation**: Not applied as a task addition (over-engineering). Documented for awareness: T068 implementations should record baseline timings in a side-file for trend tracking.

### 4.9 No task exercises bridge `--no-lock` flag explicitly

**Severity**: Very low. The `--no-lock` flag was introduced for testing in T024. Tests like T020 use it implicitly (need separate data-dirs to spawn parallel bridges). Verification that `--no-lock` truly skips the lock is implicit in the lock unit tests passing.

**Remediation**: Not applied — implicit coverage is sufficient.

## 5. Internal-consistency findings

- **No contradictions** found between spec, plan, contracts, and tasks. Spec FR-005 invariants (preserved bridge behaviours) are explicitly listed in T024's MUST-NOT-regress clause.
- **Spec → contracts → tasks** trace verified for every SC.
- **Tombstone schema** in `data-model.md § 4` is consistent with `contracts/tombstone_format.md`.
- **Bridge sidecar shape** in `data-model.md § 2` is consistent with `contracts/bridge_lifecycle.md` and `contracts/bridge_cli.md`.
- **Discover step ordering** in `contracts/codeconv_discover_cli.md` matches `tasks.md` T072 sub-bullets.

## 6. Quality observations (not gaps, just notes)

- The 16 spec clarifications dramatically reduced the implementation risk for this feature. Without them, the plan would have had ≥ 6 NEEDS CLARIFICATION items.
- Test coverage is generous (≥ 35 test tasks across 4 user stories). Per CLAUDE.md "baseline before changing", this is the right scale for a feature that touches bridge protocol + .NET + Python + the existing live D2NET workflow.
- Quickstart flows A–F map 1:1 to user stories US1–US4, with E and F covering cross-cutting properties (concurrent safety, durability). Good independent-test coverage.

## 7. Top remediations applied (in this order)

| # | What | Why | Where applied |
|---|------|-----|--------------|
| R1 | Pin Python lock lib to `portalocker>=2.8` | Closes § 4.3 | `tasks.md` T002, T053; `research.md` R1 |
| R2 | Amend T038 to call out FR-027 connection-string flag preservation | Closes § 4.2 | `tasks.md` T038 |
| R3 | Add T091 (grep .NET source for Pooling=true / missing Pooling) | Closes § 4.2 | `tasks.md` Phase 7 |
| R4 | Add T092 (grep all introduced source for COPY FROM STDIN) | Closes § 4.1 | `tasks.md` Phase 7 |
| R5 | Add T010a (inspect + document D2NET schemas) | Closes § 4.4 | `tasks.md` Phase 2 |
| R6 | Amend T057 to specify Alembic-then-DBOS order in `codeconv migrate` | Closes § 4.5 | `tasks.md` T057 |

## 8. NOT applied (left for /speckit-implement to decide)

- § 4.6 — `--no-orphan-revival` test. Robustness; not in spec.
- § 4.7 — sln-add explicit verification task. T014's build check covers it.
- § 4.8 — perf baseline trend file. Over-engineering for one feature.
- § 4.9 — `--no-lock` standalone test. Implicit coverage in T020.

## 9. Follow-ups for Gabi (none)

No remediations require spec amendment or Gabi approval. All applied edits are in-document refinements that close documentation gaps; none change semantics, timelines, or the feature's contract surface.

If Gabi disagrees with any of R1–R6, they are revertible with `git diff`; nothing has been deleted.
