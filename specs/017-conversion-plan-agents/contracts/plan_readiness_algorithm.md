# Contract: plan-readiness predicate + topo/SCC batch selection

Implements spec FR-003, FR-004, FR-011, FR-021; SC-002, SC-006. Implemented in `codeconv/src/codeconv/tools/planagents/readiness.py` (pure; unit-testable with no bridge).

## Source of truth references

- Spec FRs: FR-003 (read depgraph only, never recompute), FR-004 (four-state classification), FR-011 (SCC coordinated batch), FR-021 (deterministic ordering).
- Feature 015: `specs/015-codeconv-depgraph/contracts/depgraph_algorithm.md` (`topo_level`, `cycle_group_id` semantics) and `codeconv.dart_depgraph` (canonical — this feature consumes it read-only and MUST NOT redefine it).
- Research: R2 (predicate), R3 (≤7 cap), R4 (SCC batch).

## Inputs (all read-only; no `.dart` parse — FR-003)

| Input | Source | Used for |
|---|---|---|
| `nodes` | `codeconv.dart_files.path` minus `codeconv.dart_files_orphaned.path` | Universe of plannable files (orphans excluded — FR-020). |
| `depgraph[path]` | `codeconv.dart_depgraph` rows: `(topo_level, cycle_group_id, status)` | Canonical ordering + SCC membership (feature 015). |
| `cross_scc_deps[path]` | the set `{ dep : (path → dep) is an in-subtree dependency AND cycle_group_id(dep) ≠ cycle_group_id(path) }` | SCC-external dependency set. **`codeconv.dart_depgraph` stores only per-file `(topo_level, cycle_group_id, status)` — no edge targets** — so the edges are read **read-only** from `codeconv.dart_imports` (the canonical edge table, feature 012/014) and each endpoint's `cycle_group_id` is looked up in `dart_depgraph`; a pair is cross-SCC iff the two `cycle_group_id`s differ (intra-SCC edges excluded, mirrors feature-015 FR-006). Reading `dart_imports` is read-only — FR-020 forbids *writes* to the protected tables, not reads (the SC-002 self-join below reads the same table). |
| `plans[path]` | `codeconv.dart_plans` rows: `(plan_started_at, plan_completed_at)` | Plan lifecycle state. |

## State classification (FR-004 — exactly one per non-orphaned node)

```
classify(path):
  if path has a dart_plans row:
      if plan_completed_at IS NOT NULL:  return PLANNED
      else:                              return PLAN_IN_PROGRESS
  else:  # no row
      if every d in cross_scc_deps[path] is PLANNED:  return PLAN_READY
      else:                                            return PLAN_PENDING
```

- `cross_scc_deps[path] == ∅` (leaf / isolated, or SCC with no external deps) ⇒ immediately `PLAN_READY`.
- "every d is PLANNED" means `plans[d].plan_completed_at IS NOT NULL`. A `PLAN_IN_PROGRESS` dependency does **NOT** satisfy this (FR-004: in-progress plans do not unblock downstream).
- Orphaned files are not classified (excluded from `nodes`).

## SCC coordinated-batch rule (FR-011 / R4 / SC-006)

A file's SCC is the set `S = { p : cycle_group_id(p) == cycle_group_id(path) }`.

- **SCC plan-readiness**: an SCC `S` is plan-ready iff **no** member has a `dart_plans` row yet (none `PLAN_IN_PROGRESS`/`PLANNED`) AND every SCC-external dependency of *any* member is `PLANNED`. When ready, **all** members of `S` are emitted together as one batch unit with shared `cycle_group_id` and the full member list.
- **Partial-batch resume**: if some members have rows and others do not (interrupted batch), the SCC is **not** newly `PLAN_READY`; the un-started members **and any member still `plan_completed_at IS NULL` (crashed mid-batch)** are still selectable so the batch can be completed/resumed (edge case "SCC member subset already planned"; T030 "C resumable"). Downstream stays blocked until every member is `PLANNED`.
- **Downstream gating**: a file with an SCC-external dependency on any member of `S` is `PLAN_READY` only when **every** member of `S` is `PLANNED`.

## Selection order (FR-021 / SC-002 / R3)

`select_next(limit=7)` returns the next batch of plan-ready units:

1. Compute `classify` for all nodes.
2. Candidate set = all `PLAN_READY` files, **plus** any member (un-started **or** in-progress `plan_completed_at IS NULL`) of a partially-started SCC whose external deps are all `PLANNED` (SCC resume — covers a member whose agent crashed mid-batch; T030), **plus** any singleton with a `dart_plans` row, `plan_completed_at IS NULL`, whose external deps are all `PLANNED` (crashed-agent resume — FR-014). In both resume cases within-run double-spawn is prevented by the skill's active-set tracking, not by excluding the row here — see `agent_orchestration.md` §Concurrency-cap.
3. Group candidates into **units**: a singleton file is one unit; all members of one `cycle_group_id` form one unit (SCC batch).
4. Order units by `(min(topo_level) of unit ASC, min(path) of unit ASC)`; within an SCC unit order members lexicographically by `path`.
5. Flatten units to a tombstone list in that order; take files until `limit` tombstones are accumulated **without splitting an SCC unit** (an SCC unit is taken whole even if it pushes the count above `limit` — coordinated batch integrity wins over the soft cap; the skill still runs ≤7 Agent calls concurrently, draining the over-count batch across loop iterations — see R3).
6. A file with a `dart_plans` row, `plan_completed_at IS NULL`, is included **only** as a resume (SCC partial-batch member, or crashed singleton from a prior interrupted run — step 2), never as a fresh plan. Within a single orchestrator run the skill MUST NOT re-issue `next` for a file still active in its batch (active-set tracking — `agent_orchestration.md` §Concurrency-cap); that, not blanket exclusion by `next`, is what prevents double-spawn (FR-014 idempotent recovery).

`limit` default = 7 (FR-005). `select_next` is pure given its inputs and returns the same list for the same inputs (FR-021 determinism).

## Correctness invariant (SC-002)

For every in-subtree dependency edge `(A → B)` that crosses SCCs (`cycle_group_id(A) ≠ cycle_group_id(B)`):

> A is never selected for planning before B has `dart_plans.plan_completed_at IS NOT NULL`.

Equivalently: `select_next` never emits `A` while `classify(B) ≠ PLANNED` for any cross-SCC dependency `B` of `A`. Verifiable by an SQL self-join over `codeconv.dart_imports` × `codeconv.dart_plans` × `codeconv.dart_depgraph` (SC-002).

Intra-SCC edges are exempt (members plan as a batch — FR-011).

## What this algorithm does NOT do

- Does NOT read `.dart` source and does NOT *re-derive* topo order / SCC membership — those are consumed verbatim from feature-015's `codeconv.dart_depgraph` (FR-003). It DOES read `codeconv.dart_imports` **read-only** for the dependency edges themselves (the edges are not in `dart_depgraph`), classifying each edge cross- vs intra-SCC via the consumed `cycle_group_id` — no graph recomputation, no writes (FR-020).
- Does NOT recompute `topo_level` / `cycle_group_id` / `status` (feature 015 owns these).
- Does NOT write anything (pure function; the workflow layer performs DB writes).
- Does NOT decide conversion order — only **planning** order (conversion is a separate future tool).
