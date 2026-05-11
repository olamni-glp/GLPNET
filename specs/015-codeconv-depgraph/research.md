# Phase 0 Research: 015-codeconv-depgraph

Resolves the technical unknowns of the plan. Every section follows Decision / Rationale / Alternatives considered. The spec was clarified in Session 2026-05-11 (2 questions; see `spec.md` § Clarifications); items already settled there are referenced, not re-litigated.

This feature does NOT reopen feature 012's or feature 014's research notes. The new tool consumes their outputs as a downstream client.

---

## R1. Tarjan SCC algorithm — pure stdlib, iterative, deterministic

**Decision**: Implement Tarjan's strongly-connected-components algorithm directly in `codeconv/src/codeconv/tools/depgraph/algorithm.py`, using only the Python standard library. The implementation is **iterative** (explicit stack) rather than recursive — at ~128 nodes this is academic on Windows Python 3.11 (default recursion limit 1000), but iterative form is the convention in production scientific code and avoids future surprise on larger graphs. The algorithm input is a `dict[str, list[str]]` adjacency list (node → list of nodes-it-imports). Output is `dict[str, int]` mapping every node to its SCC id, plus an SCC-id → level mapping computed via a reverse topological walk of the condensation DAG.

**Determinism** (FR-015 / SC-002): tiebreak rules at every choice point:

1. Adjacency lists are sorted lexicographically before the Tarjan loop starts.
2. Outer iteration over nodes visits nodes in lexicographic order.
3. When an SCC is popped, its `cycle_group_id` is assigned in the order SCCs are popped (Tarjan completion order); members within an SCC are stored sorted lexicographically by `path`.
4. The condensation-level assignment uses Kahn's algorithm with the worklist seeded by lexicographic order of zero-out-degree SCCs (i.e. SCCs with no edges to other SCCs — these are the leaves, level 0).
5. The final per-file ordering is `(topo_level ASC, cycle_group_id ASC, path ASC)`.

Two consecutive runs on the same input produce byte-identical assignments by construction.

**Rationale**:

- **Why stdlib only**: `networkx` would add a dependency for a 50-line algorithm; `scipy.sparse.csgraph` requires a numpy build. Neither is currently in the codeconv venv (per `codeconv/pyproject.toml`). The cost of vendoring Tarjan is ~50 lines of Python; the cost of adding a 30 MB transitive dependency is high.
- **Why Tarjan over Kosaraju**: Tarjan is single-pass DFS; Kosaraju is two-pass DFS + transpose. Both are O(V+E). For 128 nodes / 443 edges the constant factor is irrelevant, but Tarjan's single-pass shape is the standard reference and makes the determinism rules above simpler to enforce.
- **Why iterative**: defensive — if the inventory grows to many thousands of files (future work), recursion depth becomes a real concern. Iterative form has no runtime cost at this scale.

**Alternatives considered**:

- **`graphlib.TopologicalSorter`** (stdlib): rejected — does not handle cycles. Raises `graphlib.CycleError` if any cycle exists. The spec requires graceful cycle handling (US3); we cannot use it as the primary sorter.
- **`scipy.sparse.csgraph.connected_components`**: rejected — adds scipy + numpy as dependencies. Worth ~30 lines of saved code at the cost of ~30 MB of wheels.
- **Recursive Tarjan**: rejected — would work fine today but is fragile under future scale-up; the iterative form has no real cost.

**Validation criterion**: SC-006 — synthetic 3-file cycle fixture (A→B→C→A) produces one `cycle_group_id` shared by all three, all three at the same `topo_level`. Single-file (self-loop) test produces a singleton `cycle_group_id`. Acyclic fixture matches a hand-computed expected ordering byte-for-byte.

---

## R2. Auto-recompute after `mark-*` — NO

**Decision**: `codeconv depgraph mark-started <path>` and `codeconv depgraph mark-completed <path>` write only to `codeconv.dart_conversions` (and the corresponding tombstone YAML, per FR-006a). They do NOT trigger a re-run of `compute`. The `codeconv.dart_depgraph` table's `status` column may therefore be stale after a `mark-*` call until the user explicitly invokes `codeconv depgraph compute` (or `codeconv depgraph` with no subcommand, which defaults to compute). The user-facing artefact `.codeconv/depgraph.json` is similarly stale until recomputed.

The `compute` output is the authoritative oracle; the `mark-*` calls are pure bookkeeping operations whose effect on readiness is observable only by re-running `compute`.

**Rationale**:

- Spec FR-006a treats `mark-*` as bookkeeping: "MUST validate `path` exists in `codeconv.dart_files`; MUST be idempotent on re-run". No clause demands auto-recompute.
- Coupling `mark-*` to `compute` creates surprise: a developer who marks 10 files in a script would pay 10× the compute cost. Even at the sub-second scale of 128 files, the principle matters at future scale.
- The explicit two-step (mark, then compute) is testable in isolation. `test_depgraph_mark.py` can verify the row-shape behaviour of `mark-*` without spinning up the full Tarjan + JSON-write pipeline.
- A future enhancement can add `--recompute` as an opt-in flag to `mark-*`. It is cheaper to add the flag later than to remove auto-recompute later.

**Alternatives considered**:

- **Always auto-recompute after `mark-*`**: rejected per the rationale above. Implicit coupling and surprise cost.
- **Auto-recompute only after `mark-completed`** (since only completion can advance the frontier): considered. Rejected because `mark-started` also changes the `status` column (`pending` → `in_progress`), so a `compute` run after `mark-started` is the only way to keep `dart_depgraph.status` consistent. Asymmetric semantics are worse than uniform "you must explicitly compute".
- **A `--mark-and-recompute` convenience subcommand**: deferred. If user behaviour demands it after v1 ships, add it as `codeconv depgraph mark-completed --then-recompute`.

**Validation criterion**: spec acceptance scenarios US2 #2 and #3 explicitly walk through "mark-started, then run depgraph; mark-completed, then run depgraph" — i.e. two-step. Tests in `test_depgraph_mark.py` will check exactly this state machine.

---

## R3. `--from-tombstones` rebuild surface — confined to conversions only

**Decision**: Do NOT add a `--from-tombstones` mode to `codeconv depgraph compute`. The depgraph is **always recomputed** from `codeconv.dart_imports` (which is the canonical edge graph; rebuildable from tombstones via `/codeconv-discover --from-tombstones` per feature 012 FR-022). What this feature DOES add is a separate subcommand `codeconv depgraph rebuild-conversions-from-tombstones` that scans every `.codeconv/tombstones/*.dart.md` for the new `conversion_started_at` / `conversion_completed_at` keys (R4 / contracts/tombstone_format_delta.md) and writes them into `codeconv.dart_conversions`. This satisfies FR-006a's "schema can be rebuilt from tombstones" requirement.

**Rationale**:

- The depgraph table (`codeconv.dart_depgraph`) is a pure function of (`codeconv.dart_imports`, `codeconv.dart_conversions`). It carries no information beyond its inputs. Therefore the question of "rebuild depgraph from tombstones" reduces to "rebuild its inputs from tombstones": `dart_imports` is rebuilt via `/codeconv-discover --from-tombstones` (already in place); `dart_conversions` needs its own rebuild path (this feature, this subcommand).
- Separating the rebuild surface — `discover` rebuilds the inventory and edges; `depgraph rebuild-conversions-from-tombstones` rebuilds only the conversion state — keeps each tool responsible for the data it owns. The depgraph compute step then re-computes the table as it normally would.
- FR-008 mandates atomic-per-run writes to `dart_depgraph`; idempotent compute on unchanged inputs is the cleaner contract than "compute can run from two different input sources".

**Alternatives considered**:

- **A single `--from-tombstones` flag on `compute` that does everything**: rejected. Conflates two responsibilities (depgraph compute vs conversions rebuild) into one CLI surface; harder to test in isolation; obscures which artefact is being read from where.
- **No conversions-rebuild subcommand at all (rely on `mark-*` to repopulate)**: rejected. After a DB wipe, the conversion history would be unrecoverable except by re-running every `mark-*` call by hand. The tombstone YAML carries the durable record per FR-006a; the subcommand simply formalises the inverse operation.

**Validation criterion**: `test_depgraph_rebuild_conversions.py` will: (1) seed `dart_conversions` with N rows via `mark-*`; (2) run `stamp-tombstones`; (3) `TRUNCATE codeconv.dart_conversions`; (4) run `rebuild-conversions-from-tombstones`; (5) assert the table content equals step-1's content. Idempotent re-run produces zero diff.

---

## R4. JSON `schema_version` field — `"schema_version": 1`

**Decision**: The top-level JSON metadata block in `.codeconv/depgraph.json` includes the literal key `"schema_version": 1`. The value increments only on breaking changes to the JSON shape (add/remove fields, change types, change array shape). Adding a field is a backwards-compatible operation and does NOT increment the version. Removing or renaming a field is a breaking change and DOES increment the version.

**Rationale**:

- Spec leaves this as an explicit "deferred to planning" item (spec line 137 — "JSON schema-version field"). The choice is forward-looking: this JSON is the developer-facing artefact (SC-005), likely to be consumed by future tooling (a converter, a dashboard, IDE integrations). Stamping a version at v1 is essentially free now and saves a migration headache later.
- The chosen rule (additive = compatible) follows JSON-schema convention (JSON-Schema-Draft-2020 § 8.2 — "Adding new keywords … does not require a new version").
- Tests in `test_depgraph_idempotence.py` will assert `schema_version == 1` to lock the contract.

**Alternatives considered**:

- **No version field**: rejected — costs nothing to add; saves nontrivial future archaeology.
- **Semver-style `"schema_version": "1.0.0"`**: rejected as over-engineered. A single integer matches the precedent used by Alembic (`revision: "0001"`) and by every PGLite migration file in this repo.

---

## R5. `depgraph_runs` traceability table — YES (mirrors `discover_runs`)

**Decision**: Add a third new table `codeconv.depgraph_runs` mirroring `codeconv.discover_runs`'s shape: `id uuid PK, started_at timestamptz NOT NULL, completed_at timestamptz NULL, mode text NOT NULL ('compute' | 'mark-started' | 'mark-completed' | 'stamp-tombstones' | 'rebuild-conversions-from-tombstones'), files_total integer NULL, ready_count integer NULL, in_progress_count integer NULL, converted_count integer NULL, cycle_count integer NULL, warnings jsonb NOT NULL DEFAULT '[]'::jsonb`. Every depgraph CLI invocation that mutates state INSERTS one row at start and UPDATEs `completed_at` + counts at end.

`codeconv.dart_depgraph` rows carry a `depgraph_run_id uuid NULL FK` so each per-file row can be traced to the run that produced it. (Equivalent to `dart_files.discovered_at` — provenance for forensic comparison.)

**Rationale**:

- Spec Key Entities § "depgraph_run (optional, per planning)" explicitly leaves this for planning to decide. The decision: include it. Cost is one tiny table + one column on `dart_depgraph`; benefit is the same forensic property that `discover_runs` provides — answer "when did this file last get marked ready?" with a single JOIN.
- Mirrors `discover_runs` so the developer mental model is "every tool has a `<tool>_runs` table".
- The traceability column `discover_run_id uuid NULL FK` on `dart_depgraph` (also listed in spec data-model line 116) is preserved — it records the most recent inventory state used when this row was computed. Combined with `depgraph_run_id`, every row answers both "what inventory state am I computed against?" and "what compute invocation wrote me?".

**Alternatives considered**:

- **No runs table**: rejected. Saves ~20 lines of DDL and ~30 lines of Python at the cost of losing forensic provenance forever. The asymmetry with `discover_runs` would be jarring.

---

## R6. Table name `codeconv.dart_depgraph` — confirmed

**Decision**: The new ordering table is named `codeconv.dart_depgraph`. The spec FR-008 wording ("a new table `codeconv.dart_depgraph` (or successor name)") is normative for v1. The "(or successor name)" parenthetical is a planning escape hatch; planning chooses the proposed name verbatim.

**Rationale**:

- Consistency with the existing `codeconv.dart_*` family (`dart_files`, `dart_imports`, `dart_callers`, `dart_files_orphaned`).
- "depgraph" is short, unambiguous, and matches the slash-command name `/codeconv-depgraph` and the Python tool subpackage name `codeconv.tools.depgraph` — three things named the same is easier to remember than three things named almost-the-same.

**Alternatives considered**:

- **`codeconv.dart_topo_order`**: rejected. "depgraph" carries the SCC + topological-ordering connotation more naturally than "topo_order" alone.
- **`codeconv.dart_conversion_order`**: rejected — too narrow; the table is also useful as a pure dependency-graph audit (without any conversion intent).

---

## R7. `status` column data integrity — CHECK constraint

**Decision**: `codeconv.dart_depgraph.status` is `text NOT NULL CHECK (status IN ('pending', 'ready', 'in_progress', 'converted'))`. The CHECK constraint is enforced at the database layer; the Python tool emits typed string literals only. The list of values is also encoded as a Python `Literal[...]` type alias in `codeconv/src/codeconv/tools/depgraph/algorithm.py`.

**Rationale**:

- Defense in depth: a bug in the Python code that wrote a typo (`'completed'` instead of `'converted'`) would be caught immediately by PostgreSQL's CHECK rather than producing silently corrupt rows that fail in some downstream query weeks later.
- PGLite supports CHECK constraints natively (verified in feature 012's `dart_files` DDL — no CHECK there because the columns are free-text, but the SQL parser handles CHECK fine).
- Cost: ~10 characters of DDL. Benefit: data integrity invariant by construction.

**Alternatives considered**:

- **Enum type** (`CREATE TYPE codeconv.dart_status AS ENUM (...)` + `ALTER TABLE ... status codeconv.dart_status`): rejected. Adds a schema object and a migration step. The four values are unlikely to change; if they do, the CHECK is two lines to update. The enum type adds friction for inserts from psql / tests.
- **No constraint**: rejected. Cheap to add, expensive to recover from a silent typo.

---

## R8. Idempotence — stable JSON key order + canonical YAML + deterministic Tarjan

**Decision**: Three guarantees, jointly:

1. **Tarjan output is deterministic** (R1 § Determinism rules 1–5).
2. **JSON output emits keys in stable order** — both at the top level (metadata, then arrays, then per-file rows) and within each per-file row (alphabetical). The Python emitter uses `json.dumps(obj, indent=2, sort_keys=True, default=str)`. The top-level shape is hand-laid via an ordered dict so metadata appears first; within each container, keys are sorted.
3. **Tombstone YAML uses the same canonical emitter** as feature-012 (`tombstone.py::_YAML_DUMP_KWARGS` + `_FIELD_ORDER`). The five new keys are appended to `_FIELD_ORDER`, sorted lists stay sorted (per the existing `_canonicalise` helper).

The result: an unchanged inventory + unchanged `dart_conversions` produces byte-identical `.codeconv/depgraph.json` (modulo `generated_at`) and zero diff in `.codeconv/tombstones/*.dart.md`.

**Rationale**:

- SC-002 (idempotence) is the third spec FR explicitly carried forward from feature 012's SC-008. Same mechanism, same proof: deterministic input → deterministic output, byte-canonical encoder, sorted lists/keys throughout.
- The `generated_at` field is the one allowed source of byte-difference (a wall-clock timestamp). The test harness compares JSON after stripping that one field — same convention feature 012 uses for `discover_runs.started_at`.

**Alternatives considered**:

- **Use `pyyaml`'s default emitter without canonicalisation**: rejected — `pyyaml` would unpredictably switch between flow and block style depending on string content, defeating idempotence. The vendored `_YAML_DUMP_KWARGS` already pins block style.

**Validation criterion**: `test_depgraph_idempotence.py` will: (1) run compute; (2) read `.codeconv/depgraph.json` and tombstones; (3) run compute again; (4) re-read; (5) assert byte-identity modulo `generated_at`. Mirror of feature-012's `test_discover_idempotence.py`.

---

## R9. Performance — graph size is trivially under budget

**Decision**: At 128 files / 443 edges, Tarjan SCC is sub-millisecond. The dominant costs of `compute` are: PGLite read of `dart_imports` (~5–50 ms warm), PGLite write of `dart_depgraph` (~50–200 ms for 128 rows), JSON serialisation (~5 ms), JSON write (~5 ms). Total warm-bridge expected: 100–500 ms. SC-001's 5 s warm budget is trivially met.

The cold-bridge first-run budget of 15 s (SC-001) is dominated by the ~7 s PGLite cold-init (per `project_pglite_cold_init_windows.md`), leaving ~8 s for everything else — also trivially met.

**Rationale**:

- The algorithm is O(V+E) at 128 + 443 = 571 — well under any reasonable budget.
- The PGLite writes are the only cost worth profiling. The atomic-per-run protocol (DELETE then bulk INSERT — see contracts/depgraph_schema.md) batches 128 rows in one round-trip.
- No new HTTP, no new disk-scan, no new parse. The tool is a thin transformer on top of an in-memory graph.

**Alternatives considered**:

- **Incremental update of `dart_depgraph` instead of DELETE-and-bulk-INSERT**: rejected for v1. Incremental introduces row-lifecycle complexity (track which rows changed). The DELETE-and-bulk-INSERT shape is simpler and well under budget. Revisit if future scale demands it.

**Validation criterion**: `test_depgraph_compute.py` will time a warm run and assert < 2 s (well inside SC-001's 5 s budget); a cold run is harder to gate cleanly in a unit test, so it is verified manually per `quickstart.md` Flow H.

---

## R10. `.codeconv/depgraph.json` gitignore status — gitignored

**Decision**: Add `.codeconv/depgraph.json` to `.gitignore`. The durable round-trip path for depgraph state is `.codeconv/tombstones/*.dart.md` (per FR-014 / `stamp-tombstones`). The JSON file is a developer-local convenience artefact that is recomputable in seconds from the schema; checking it in would invite merge churn (every developer's run regenerates it; the `generated_at` field guarantees a diff every time).

**Rationale**:

- The tombstone YAML carries `topo_level`, `cycle_group_id`, `status`, `conversion_started_at`, `conversion_completed_at` per FR-014 — that's the durable, version-controlled record.
- The JSON is the *answer to "what should I convert next?"* — a transient question whose answer changes every time `mark-completed` is called. Pinning that to the git index is the opposite of what makes the artefact useful.
- Mirrors the existing pattern: `.codeconv/tombstones/` is checked in (durable inventory record); `.pgdb/bridge.json` is gitignored (transient sidecar); `.codeconv/depgraph.json` follows the latter.

**Alternatives considered**:

- **Check in `depgraph.json`**: rejected per rationale above.
- **Make the artefact opt-in (only written with `--json-out`)**: rejected — spec FR-007 mandates the artefact at a default path. The default path lives outside version control.

**Validation criterion**: SC-008 (`--dry-run` produces no changes) — the JSON-write step is skipped under `--dry-run`. `.gitignore` updated as part of the implementation tasks.

---

## R11. Out of scope (explicit non-decisions)

The following are deferred by spec and NOT resolved here:

- **Actually converting `.dart` files to C# / .NET**: covered by a future codeconv-* tool. This feature surfaces the ordering only.
- **Cross-run diffing of depgraph state**: explicitly out of scope (spec line 142). A future feature could read `depgraph_runs` to render trend reports; not in this delta.
- **Optimal multi-file batch recommendation**: out of scope per spec line 144. SCC membership already surfaces the only natural "convert these together" hint; finer grouping is a future feature.
- **GUI / visualisation**: out of scope (spec line 142).

---

## Open questions for implementation

None. The 2 spec-side clarifications (Session 2026-05-11) plus R1–R10 above constitute the closed set.

If implementation discovers that PGLite's CHECK-constraint behaviour diverges from PostgreSQL's (e.g. CHECK silently accepts disallowed values), STOP and escalate per spec Assumptions before relaxing the constraint or moving validation to the Python layer alone. PGLite is PostgreSQL-WASM (≥0.2.17 per `package.json`) and is expected to honour CHECK; the test suite will confirm.
