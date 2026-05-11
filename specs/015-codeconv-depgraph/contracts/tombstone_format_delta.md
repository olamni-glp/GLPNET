# Contract: tombstone YAML frontmatter — feature 015 delta (five new keys, appended)

This document specifies the exact change to `codeconv/src/codeconv/tools/discover/tombstone.py` made by this feature, and the round-trip semantics. The implementation follows this contract; any deviation is a bug.

## Source of truth references

- Spec FRs covered: FR-006a (tombstone update on `mark-*`), FR-014 (`stamp-tombstones`), FR-022 (carry-forward of tombstone-round-trip property from feature 012)
- Research notes: R3 (rebuild surface), R8 (idempotence preservation), R10 (durable round-trip via tombstones)
- Sibling format spec: `specs/012-codeconv-runner/contracts/tombstone_format.md`

## Change to `_FIELD_ORDER`

Before (feature 012/014):

```python
_FIELD_ORDER: tuple[str, ...] = (
    "path",
    "name",
    "purpose",
    "key_idea",
    "dependencies",
    "callers",
    "mtime",
    "sha256",
)
```

After (feature 015):

```python
_FIELD_ORDER: tuple[str, ...] = (
    "path",
    "name",
    "purpose",
    "key_idea",
    "dependencies",
    "callers",
    "mtime",
    "sha256",
    # --- feature 015 (codeconv-depgraph) appended fields ---
    "topo_level",
    "cycle_group_id",
    "status",
    "conversion_started_at",
    "conversion_completed_at",
)
```

The five new keys are APPENDED to the tuple — the position of every existing key is unchanged. This is the critical idempotence property: a pre-feature tombstone and the feature-012 / -014 idempotence tests continue to produce identical YAML for the first eight keys.

## Per-key semantics

| Key | Type | Source | Written by |
|---|---|---|---|
| `topo_level` | integer | `codeconv.dart_depgraph.topo_level` | `stamp-tombstones` |
| `cycle_group_id` | integer | `codeconv.dart_depgraph.cycle_group_id` | `stamp-tombstones` |
| `status` | string ('pending'/'ready'/'in_progress'/'converted') | `codeconv.dart_depgraph.status` | `stamp-tombstones` |
| `conversion_started_at` | ISO8601 string `2026-05-11T14:32:08Z` | `codeconv.dart_conversions.started_at` | `mark-started` AND `stamp-tombstones` |
| `conversion_completed_at` | ISO8601 string or YAML-null | `codeconv.dart_conversions.completed_at` | `mark-completed` AND `stamp-tombstones` |

## Null vs missing convention

The five new keys distinguish between THREE states:

1. **Key absent from frontmatter**: pre-feature tombstone (never re-written by `stamp-tombstones` or `mark-*`). Reader-side default: treat as "depgraph state unknown" (NOT zero-or-default).
2. **Key present with non-null value**: stamped with a known value from the DB.
3. **Key present with `null`**: stamped, but the source column is NULL. Today only `conversion_completed_at` can legitimately be `null` (in-progress conversions; the row exists in `dart_conversions` with `completed_at IS NULL`).

This three-state distinction is what makes `rebuild-conversions-from-tombstones` work: it can tell "no conversion record" (missing key) from "conversion started but not completed" (key present, value null). See `depgraph_cli.md` § `rebuild-conversions-from-tombstones`.

## Writer behaviour by subcommand

### `mark-started`

Reads existing tombstone; merges in **two keys**: `conversion_started_at` (set to the new ISO8601 timestamp). Other depgraph keys (`topo_level`, `cycle_group_id`, `status`) are left UNCHANGED — if they exist in the file, they stay; if they don't, they remain absent. The reason: `mark-started` knows about conversion state but not about depgraph state; only `compute` + `stamp-tombstones` writes those.

### `mark-completed`

Reads existing tombstone; merges in `conversion_completed_at` (and, indirectly via the row state, any future writer might also update `status` to 'converted' here — but per R2, status updates are deferred to the next `compute` + `stamp-tombstones` cycle; `mark-completed` does NOT touch `status`).

### `stamp-tombstones`

For every file in `dart_depgraph`:

1. Read existing tombstone.
2. Set ALL FIVE new keys to their current DB values:
   - `topo_level`, `cycle_group_id`, `status` from `dart_depgraph`.
   - `conversion_started_at`, `conversion_completed_at` from `dart_conversions` (or absent if no row).
3. Re-emit the tombstone via the canonical YAML writer.

### `compute`

Does NOT touch tombstones (its writes are confined to the DB + `.codeconv/depgraph.json`). The user calls `stamp-tombstones` explicitly after a `compute` cycle if they want the tombstones refreshed.

## Idempotence proof (SC-002 / feature-012 SC-008 carry-forward)

**Claim**: A `stamp-tombstones` re-run on unchanged source state produces zero diff in the tombstones.

**Proof sketch**:

1. The canonical YAML emitter (`_YAML_DUMP_KWARGS`: `default_flow_style=False, sort_keys=False, allow_unicode=True, width=10000`) is byte-deterministic for a given input dict (proved by feature 012's `test_discover_idempotence.py`).
2. The `_canonicalise(fields)` helper enforces `_FIELD_ORDER` and sorts list values lexicographically. The five new keys are appended in fixed order; their values are scalars (int, string, ISO8601 string, or `null`), so sort-order considerations don't apply.
3. The five new values are pure functions of the DB state (`dart_depgraph` and `dart_conversions`) at the moment of read. The DB state is stable between two consecutive `stamp-tombstones` runs if no `mark-*` or `compute` is interposed.
4. Therefore: same DB state → same scalar values → same canonical dict → same YAML bytes → zero diff.

The existing feature 012 / 014 idempotence test (`test_discover_idempotence.py`) was unchanged by feature 014 (which only added content under the existing `dependencies` / `callers` lists). For feature 015 the test continues to work IF the test runs BEFORE any `stamp-tombstones` invocation — i.e. the existing assertion (a `discover` re-run produces zero diff) is unchanged because `discover` doesn't write the new five keys. A separate test `test_depgraph_stamp.py` verifies the new property: a `stamp-tombstones` re-run on unchanged `dart_depgraph` + `dart_conversions` produces zero diff.

## Pre-feature tombstones — backwards compatibility

Tombstones written before feature 015 lacks the five new keys. After feature 015 lands, the FIRST `stamp-tombstones` invocation rewrites every tombstone to add the keys (and to leave existing keys unchanged). This is a one-time refresh; subsequent `stamp-tombstones` calls produce zero diff per the idempotence proof above.

The reader side (`rebuild-conversions-from-tombstones`) handles missing keys gracefully — see § Null vs missing convention above.

## `_canonicalise` adjustment

The existing `_canonicalise(fields)` helper (verify in `tombstone.py`) must handle the case where some of the five new keys are present in the input dict and others are not. The helper's contract: keys present in the input dict appear in the output dict in `_FIELD_ORDER`-mandated order; keys absent are absent. No null-padding; no default-filling.

If the existing helper today panics on `None` values or coerces them silently, this contract requires it to preserve YAML-`null` for `conversion_completed_at` when the source value is `None`. Verify in implementation:

- `_canonicalise({"...", "conversion_completed_at": None})` returns a dict with `"conversion_completed_at": None` preserved.
- `_emit_yaml({"conversion_completed_at": None})` emits `conversion_completed_at: null` in block scalar form.

If the existing emitter omits None-valued keys, the implementation MUST adjust the emitter (only for these new keys; existing keys are not nullable so the change is scoped).

## Test obligations

`test_depgraph_stamp.py` MUST cover:

1. **Initial stamp adds five keys**: a pre-feature tombstone (no new keys) gains all five after `stamp-tombstones`.
2. **Re-stamp idempotence**: a second `stamp-tombstones` call produces zero diff.
3. **Pending status**: a file with no `dart_conversions` row has `status: pending`, `conversion_started_at` absent (NOT null), `conversion_completed_at` absent.
4. **In-progress status**: a file with `dart_conversions` row, `completed_at IS NULL` has `status: in_progress`, `conversion_started_at: <ISO>`, `conversion_completed_at: null` (present with null value).
5. **Converted status**: a file with `dart_conversions` row, `completed_at IS NOT NULL` has `status: converted`, both timestamp keys present and non-null.

`test_depgraph_rebuild_conversions.py` MUST cover:

1. **Round-trip**: seed → stamp → wipe → rebuild → diff == 0 (no rows lost, no rows invented).
2. **Missing-key tolerance**: a tombstone without the five new keys does NOT cause the rebuild to error; the rebuild silently skips that file.
3. **Null-value distinguishability**: a tombstone with `conversion_completed_at: null` produces a `dart_conversions` row with `completed_at IS NULL`; a tombstone without the `conversion_completed_at` key produces NO `dart_conversions` row.
