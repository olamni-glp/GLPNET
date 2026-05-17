# Quickstart: Flow I — `/codeconv-planagents` end-to-end

Prerequisite flows (feature 012 Flow F / feature 015 Flow H): `/codeconv-discover` then `/codeconv-depgraph` have run; `codeconv.dart_depgraph` is populated. On this exFAT checkout every command needs `--data-dir C:/pglite/research/glpnet`.

## 0. One-time

```
python -m venv codeconv/.venv
codeconv/.venv/Scripts/python.exe -m pip install -e codeconv[dev]
codeconv/.venv/Scripts/codeconv.exe migrate --data-dir C:/pglite/research/glpnet   # applies 0003_dart_plans
```

## 1. See what is plan-ready (no agents, no writes)

```
/codeconv-planagents status --data-dir C:/pglite/research/glpnet
```

Expected on a fresh baseline: every depgraph leaf / isolated file is `plan_ready`; everything else `plan_pending`; `planned=0`, `plan_in_progress=0`, `open_escalations_total=0`. If `dart_depgraph` is empty → exit 2 with `"No depgraph. Run /codeconv-depgraph first."` (FR-018).

## 2. Generate the first wave (US1)

```
/codeconv-planagents --data-dir C:/pglite/research/glpnet
```

The skill loops: `next --limit 7` → for each tombstone records `plan-started`, spawns a planning sub-agent (≤7 concurrent) that inspects the real `.dart`, writes `.codeconv/conversion-plans/<rel>.dart.md`, then records `plan-completed`. Repeats until `next` returns an empty batch, then runs `aggregate-escalations`.

Verify (SC-001/SC-004):

```
git status .codeconv/conversion-plans/        # one <rel>.dart.md per leaf, all checked in
/codeconv-planagents status --data-dir C:/pglite/research/glpnet   # leaves now `planned`
```

Each artefact must contain sections 1–6 (analysis, plan, tasks, research, consistency, escalations) in order; SCC members additionally section 7 (`conversion_plan_artefact_format.md`).

## 3. Advance the frontier (US2)

Re-invoke. Files at the next `topo_level` whose every SCC-external dependency is now `planned` become `plan_ready` and are planned next. A chain A→B→C with empty `dart_plans`: run 1 plans only A; after A completes, run 2 plans B; after B, run 3 plans C (US2 Independent Test).

## 4. SCC batch (US3)

For a 3-file SCC A↔B↔C with downstream D→A: one run plans A,B,C as a batch (three artefacts, each §7 cross-referencing the other two with the same `cycle_group_id`); D is NOT plan-ready until all of A,B,C are `plan_completed` (SC-006).

## 5. Escalations (US4)

A file whose source uses a Dart construct with no pre-specified C#/.NET mapping: its artefact §6 has an `### E1` escalation (open), the file is still `planned` for the planning frontier, `dart_plans.open_escalation_count > 0`, and `.codeconv/conversion-plans/_escalations-report.md` lists it. The agent did NOT guess a mapping (SC-005).

```
/codeconv-planagents aggregate-escalations --data-dir C:/pglite/research/glpnet
```

## 6. Idempotence & dry-run (SC-003 / SC-008)

```
/codeconv-planagents --dry-run --data-dir C:/pglite/research/glpnet
git status            # no artefact/tombstone change
# SELECT count(*) FROM codeconv.dart_plans  → unchanged
```

A re-run on unchanged source + plan state re-plans zero files, creates zero duplicate rows/artefacts, and yields zero artefact diff except each artefact's `generated_at` front-matter field (SC-003).

## 7. Source drift / replan (FR-015)

If a planned file's `.dart` changed since `plan-started` (`dart_files.sha256` ≠ `sha256_of_dart_at_plan_start`), `status` reports it **stale**. It is re-planned only under explicit `--replan <selection>`; the new artefact carries forward prior open escalations with a "carried from <prior generated_at>" note (never silently dropped).

## 8. Round-trip (FR-013)

```
/codeconv-planagents stamp-tombstones --data-dir C:/pglite/research/glpnet
/codeconv-planagents rebuild-plans-from-tombstones --data-dir C:/pglite/research/glpnet
```

`stamp` embeds the four plan-state keys into every tombstone (idempotent, byte-identical re-stamp). `rebuild` repopulates `codeconv.dart_plans` from tombstone YAML (DB-wipe recovery) — `sha256_of_dart_at_plan_start` is re-snapshotted from current `dart_files.sha256` (same caveat as feature-015 `rebuild-conversions-from-tombstones`).
