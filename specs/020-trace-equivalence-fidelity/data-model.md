# Phase 1 Data Model — Trace-Equivalence-Driven Codegen Fidelity (020)

Extends 019's persistence additively (FR-018). One new table in the `codeconv` schema, one new migration chained after `0007`, append-only tombstone keys, and two checked-in artifact families (the per-subsystem prompts + the manifest). No `public`/`dbos` objects authored. Upstream tables (`dart_depgraph`, `dart_convspecs`, `dart_plans`, `dart_codegen`, `conversion_idioms`) are **read-only inputs**.

---

## Entity: `codeconv.dart_equivalence` (NEW)

Two-phase row per (converted file × in-scope GLP source), mirroring `dart_codegen`'s write discipline. Holds the verdict, the divergence record, and the inputs the tiered scorer consumes.

| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `tombstone_key` | text NOT NULL | the converted file's tombstone key (the C# unit under test) |
| `source_path` | text NOT NULL | the GLP `.glp` source executed (relative to `programs/`, in place) |
| `subsystem` | text NOT NULL | `heap`\|`bytecode`\|`compiler`\|`runtime-core`\|`multiagent` |
| `tier` | text NOT NULL | `strict`\|`dynamic` |
| `compare_mode` | text NOT NULL | `trace` (total-order on strict, partial-order on dynamic) \| `outcome` (bonds) |
| `split` | text NOT NULL | `train`\|`held-out` (from the manifest; for SC-003 auditability) |
| `phase` | text NOT NULL | `pending`\|`captured`\|`compared` (two-phase: capture then deterministic verdict) |
| `verdict` | text NULL | `equivalent`\|`divergent`\|`stale`\|NULL(pending) |
| `golden_trace_hash` | text NULL | hash of the normalized Dart trace (or outcome) |
| `candidate_trace_hash` | text NULL | hash of the normalized C# trace (or outcome) |
| `divergence` | jsonb NULL | trace-divergence record (event kind, causal position, expected, actual) — NULL when equivalent |
| `bytecode_diff_empty` | boolean NULL | early-checkpoint result (FR-004); NULL until compiler subsystem converted |
| `builds` | boolean NOT NULL DEFAULT false | `dotnet build` gate input to the scorer (0.0 floor when false) |
| `back_tested` | boolean NOT NULL DEFAULT false | ported-unit back-test ran (high-band eligibility) |
| `trace_captured` | boolean NOT NULL DEFAULT false | a normalized trace was captured (high-band eligibility) |
| `dart_source_hash` | text NULL | hash of the Dart source at verdict time → drift/stale detection (FR-016) |
| `created_at` / `updated_at` | timestamptz | |

**Indexes**: unique `(tombstone_key, source_path)`; `(subsystem, tier)`; `(verdict)`; partial index on `verdict = 'stale'`.

**Derived (not stored)**: the **fidelity score** is computed on read by `tools/equiv/fidelity.py` from `builds` / `back_tested` / `trace_captured` / per-source `verdict` aggregated over a file's in-scope corpus — never persisted, so the gate and GEPA always recompute identically (R4, SC-004).

**State machine** (`phase` × `verdict`):
```
pending ──capture──▶ captured ──compare(deterministic)──▶ compared{equivalent|divergent}
                                                              │
  (Dart source drift, FR-016) ◀──mark-stale─────────────────┘
        │
        └──▶ stale ──recapture──▶ captured ──▶ compared …
```

---

## Subsystem manifest: `.codeconv/equiv-manifest/subsystems.yml` (NEW, checked in, versioned)

Authoritative classification + corpus split (R8, R9). Validated by `manifest.py` against `dart_depgraph` (read-only).

```yaml
version: 1
subsystems:
  heap:        { tier: strict,  path_prefixes: ["lib/runtime/heap_fcp", ...] }
  bytecode:    { tier: strict,  path_prefixes: ["lib/bytecode/", ...] }
  compiler:    { tier: strict,  path_prefixes: ["lib/compiler/", ...] }   # incl. type-check, partial-eval, SRSW
  runtime-core:{ tier: strict,  path_prefixes: ["lib/runtime/", ...] }   # single-computation
  multiagent:  { tier: dynamic, path_prefixes: ["lib/multiagent/"] }
corpus:
  trace:   { suites: ["unified", "book"] }      # 384 + 141, trace-compared
  outcome: { suites: ["bonds"] }                # outcome-only
  back_test: { unit_tests_ported: true }        # 374 ported C# unit tests
split:                                          # deterministic, recorded per source (no run-to-run wobble)
  ratio: { train: 0.70, held_out: 0.30 }
  assignments:                                  # stable: source_path -> train|held-out
    "programs/tests/typed/foo.glp": train
    # … full enumeration, regenerated only by an explicit, reviewed step
```

`manifest.py` asserts: every in-scope source assigned exactly once; ratios within tolerance; every `path_prefix` resolves to ≥1 inventoried Dart file in `dart_depgraph`.

---

## Per-subsystem prompt artifacts (NEW, checked in)

```
.codeconv/codegen-prompt/_base.md            # shared optimized base (carry-forward seed)
.codeconv/codegen-prompt/heap.md
.codeconv/codegen-prompt/bytecode.md
.codeconv/codegen-prompt/compiler.md
.codeconv/codegen-prompt/runtime-core.md
.codeconv/codegen-prompt/multiagent.md
```

Each carries provenance front-matter (optimizer = `dspy.GEPA`, metric_score on held-out, dataset/manifest hash, model, generated-at, base-prompt hash it descends from). `prompt.load(subsystem)` (production, in `tools/codegen/`) selects by subsystem and imports no LM/dspy.

---

## Tombstone `_FIELD_ORDER` extension (append-only, after codegen keys)

`tools/discover/tombstone.py` appends, in order:
`equiv_subsystem`, `equiv_tier`, `equiv_verdict`, `equiv_fidelity`, `equiv_bytecode_diff_empty`, `equiv_stale`, `equiv_last_verified_at`.

> **`equiv_fidelity` is a cached denormalized snapshot** written for tombstone round-trip durability — NOT a competing source of truth. `tools/equiv/fidelity.py` remains the sole authoritative computation (recomputed on read by both the gate and GEPA); the tombstone value is the last-stamped result and is overwritten on each re-verify. This reconciles "fidelity is computed-on-read, never persisted in `dart_equivalence`" (the relational table holds only the inputs) with the tombstone carrying a stamped snapshot.

Round-trips through tombstones (012 contract) so equiv state survives a DB rebuild. Appended strictly AFTER the 019 codegen keys (no reordering — 012 stability rule).

---

## Migration `0008_equivalence.py` linearization (single head)

```
0001 → 0002 → 0003_d2net_into_codeconv → 0003_dart_plans → 0005 → 0006 → 0007 → 0008
```

The historical `0003` split (two revisions sharing the `0003` ordinal) was already linearized downstream; `0007` is the **single current head**. `0008` sets `down_revision = '0007'`, `revision = '0008'`. Single-head proof obligation (verified in tasks): `alembic heads` (or the codeconv migration runner equivalent) returns exactly `0008` after this migration is added. `CREATE TABLE IF NOT EXISTS codeconv.dart_equivalence`; no `public`/`dbos` DDL (012 schema isolation, FR-018).

---

## Read-only upstream (consumed, never written)

| Source | Used for | Feature |
|---|---|---|
| `codeconv.dart_depgraph` | curriculum/subsystem topo + SCC order; manifest validation | 015 |
| `codeconv.dart_convspecs` + artifacts | codegen sub-agent context (unchanged from 019) | 018 |
| `codeconv.dart_plans` + artifacts | codegen sub-agent context (unchanged from 019) | 017/018 |
| `codeconv.dart_codegen` | the converted `.cs` to verify; build state | 019 |
| `codeconv.conversion_idioms` | idiom KB for the codegen prompt | 016+ |

---

## Validation rules (enforced in code/tests)

- A row may reach `verdict = equivalent` only if `builds = true` AND (`trace_captured` for trace mode / outcome recorded for outcome mode) AND the divergence is empty under the relation.
- A file's fidelity reaches `1.0` only when **every** in-scope source row is `equivalent` (outcome-equivalent for bonds) — `fidelity.py` enforces the clamp (SC-004).
- A `dart_source_hash` mismatch on the current Dart source flips affected rows to `verdict = stale` (FR-016); stale rows do not count toward `frac`.
- The durable `equiv` step writes only after a deterministic compare of already-captured traces (R12) — never spawns a REPL.
