# Phase 1 Data Model: Semantic Tombstone Enrichment

**Feature**: `035-semantic-tombstone-enrichment` | **Date**: 2026-06-25
Derives from `spec.md` Key Entities + `research.md` decisions. Cites the
authoritative codeconv source so the implementation matches exactly.

---

## 1. Provenance value domain

`purpose_source`, `key_idea_source` ∈ **{`doc`, `inferred`, `absent`}** (FR-005).

| Value | Meaning | Set by |
|---|---|---|
| `doc` | Value derived from a real leading doc-comment (or human-authored) | `discover` mechanical seed when `extract_leading_doc` ≠ `''`; migration `0011` backfill for non-blank existing rows |
| `inferred` | Value produced by the Claude/Agent seam from source | `enrich` on a successful, grounded inference |
| `absent` | No value (blank); no doc-comment and not yet/never inferred | `discover` seed when `extract_leading_doc` == `''`; migration `0011` backfill for blank existing rows; `enrich` low-confidence/failed outcome (value stays blank) |

**Invariant.** `purpose == '' ⟺ purpose_source == 'absent'` (and same for
`key_idea`). A non-blank value is always `doc` or `inferred`, never `absent`.

---

## 2. Tombstone frontmatter — `_FIELD_ORDER` extension (append-only)

Authoritative order: `tools/discover/tombstone.py:31-76`. This feature appends
**two** keys at the END (preserving every existing key's emission position —
FR-004, the convention used by features 015/017/018/019/020):

```python
# --- feature 035 (semantic-tombstone-enrichment) appended fields ---
"purpose_source",
"key_idea_source",
```

And adds them to the round-trip preservation set
(`tools/discover/tombstone.py:165-177`):

```python
_FEATURE_035_KEYS: tuple[str, ...] = ("purpose_source", "key_idea_source")
_PRESERVED_APPENDED_KEYS = ( … + _FEATURE_020_NO_EMIT_KEYS + _FEATURE_035_KEYS )
```

`purpose`/`key_idea` remain among the original feature-012 eight (positions 3–4)
and are NOT added to `_PRESERVED_APPENDED_KEYS` (their preservation is
**conditional** — see §4, not unconditional carry-forward).

### Tombstone before / after (blank-doc candidate `lib/compiler/codegen.dart`)

Before (verified on disk today):
```yaml
purpose: ''
key_idea: ''
…
sha256: fdeeb685…
```
After a successful enrichment:
```yaml
purpose: 'Lowers analyzed GLP clauses to bytecode for the FCP runner.'
key_idea: 'Single-pass walk over the analyzed AST emitting HEAD/GUARD/BODY opcode blocks via asm.dart.'
…
sha256: fdeeb685…            # unchanged — source not modified
purpose_source: inferred
key_idea_source: inferred
```
The markdown **body** (= `purpose`, `write_tombstone` tombstone.py:203-226)
also becomes the inferred purpose, so it appears in the git diff (FR-014).

---

## 3. `codeconv.dart_files` — additive columns (migration `0011`)

Existing DDL (`db/migrations/versions/0001_codeconv_schema.py:36-48`):
`path PK, name, purpose, key_idea, mtime, sha256, discovered_at`.

Migration `0011_enrich_provenance.py` (revision `0011`, down_revision `0010`):

```sql
ALTER TABLE codeconv.dart_files ADD COLUMN purpose_source  text NOT NULL DEFAULT 'absent';
ALTER TABLE codeconv.dart_files ADD COLUMN key_idea_source text NOT NULL DEFAULT 'absent';
UPDATE codeconv.dart_files
   SET purpose_source  = CASE WHEN purpose  = '' THEN 'absent' ELSE 'doc' END,
       key_idea_source = CASE WHEN key_idea = '' THEN 'absent' ELSE 'doc' END;
```
`downgrade()` drops both columns. Backfill is exact because mechanical seeding
is the only current source of non-blank values (research R-005).

`discover`'s UPSERT (`tools/discover/workflow.py:547-569`) extends its column
list + `ON CONFLICT DO UPDATE SET` to include `purpose_source`,
`key_idea_source` (with the conditional-preservation logic of §4).
`enrich`'s write is an `UPDATE … SET purpose=:p, key_idea=:k,
purpose_source='inferred', key_idea_source='inferred' WHERE path=:path`.

---

## 4. Entities

### Enrichment candidate
- **Definition**: in-scope (under `glp_runtime_net/`, Dart→C# pair), non-orphan
  (`tombstones/.orphaned/` excluded — FR-013), tombstone `purpose` and/or
  `key_idea` blank (`*_source == absent`).
- **Source of truth**: the `dart_files` rows joined to their tombstones;
  optional `--path` filter narrows the set (FR-012). Default scope = all blank
  candidates.

### Inference result
- Fields: `rel_path`, `purpose`, `key_idea`, `grounded: bool`, `reason: str`.
- Outcome status ∈ {`enriched`, `low_confidence`, `failed`} (drives the §6
  summary counts).
- Provenance of accepted values = `inferred`.

### Run report / summary
- Counts: `candidates`, `enriched`, `skipped`, `failed` (+ `low_confidence`)
  — FR-011 / SC-001. Plus a durable run log (table or `--json` artifact,
  mirroring `discover_runs`).

### Provenance carry-forward rule (discover side, FR-008)
State transition for a value field `f ∈ {purpose, key_idea}` when discover
re-writes a file (i.e. NOT short-circuited by the idempotence skip,
workflow.py:512-519):

| Existing tombstone `f_source` | file `sha256` vs tombstone `sha256` | Action |
|---|---|---|
| `inferred` | unchanged | **preserve**: carry forward existing `f` + `f_source: inferred` (FR-008) |
| `inferred` | changed | re-seed mechanically; `f_source` ← `doc`/`absent` (stale inference discarded, FR-007) |
| `doc` / `absent` | any | re-seed mechanically; `f_source` ← `doc` if leading-doc else `absent` |

---

## 5. State machine — a field's provenance over the pipeline

```
        discover (no doc-comment)              enrich (grounded infer)
absent ───────────────────────────▶ absent ───────────────────────────▶ inferred
   │                                   ▲                                    │
   │ discover (doc-comment present)    │  source changes → discover         │ source changes →
   ▼                                   │  re-seeds (R-002 case b)           │ discover re-blanks
  doc ◀────────────────────────────────┴────────────────────────────────────┘
  (doc never transitions to inferred — FR-006 blank-only scope)
```

`inferred → inferred` on an unchanged-source discover re-run is the FR-008
preservation edge (no transition, value kept).

---

## 6. Run summary contract (shape)

The FR-011 four counts (`candidates`, `enriched`, `skipped`, `failed`) are
always present; `low_confidence` and `skipped_non_candidate` are finer
sub-counts. `skipped` = candidates skipped because already enriched/unchanged
(idempotence); `skipped_non_candidate` = in-scope files that were never blank.

```jsonc
{
  "ok": true,
  "tool": "enrich",
  "scope": "glp_runtime_net/ (path filter: <none|…>)",
  "candidates": 37,            // FR-011
  "enriched": 35,              // FR-011
  "skipped": 0,                // FR-011 — already-enriched candidates skipped this run
  "failed": 1,                 // FR-011
  "low_confidence": 1,         // sub-count: grounded=False / over-cap → tombstone unchanged
  "skipped_non_candidate": 142,// sub-count: in-scope but never blank (provenance-stamped only)
  "run_log": ".codeconv/enrich-runs/<run-id>.json",  // C1: durable file log (no DB table)
  "failures": [ { "path": "lib/…/x.dart", "reason": "seam error: …" } ]
}
```
`candidates == enriched + skipped + low_confidence + failed` (SC-001: none
silently blank). `failed`/`low_confidence` files have **unchanged** tombstones
(SC-007).

**Durable run log (C1 resolution).** Each run writes its full summary +
per-file outcomes to `.codeconv/enrich-runs/<run-id>.json` — a file artifact,
**not** a new DB table (keeps migration `0011` to the two provenance columns
only). This is the FR-011 "durable run log"; `discover`'s `discover_runs` table
is its DB-side analog but is **not** mirrored here (simplest design — research
guidance).
