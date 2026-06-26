# Contract: discover-side provenance preservation (FR-008)

**Feature**: 035 | Touches: `tools/discover/workflow.py`,
`tools/discover/tombstone.py` (scoped edits to an existing tool — NOT the
runner/CLI registry).

## Background (verified)
- Idempotence short-circuit `workflow.py:512-519`: if the `dart_files` row's
  `sha256` == current file hash, discover `return "skipped"` **before any
  write**. Unchanged files on the incremental path are already safe.
- Mechanical seed `workflow.py:527-528`: `purpose = extract_leading_doc(...)`,
  `key_idea = purpose`. UPSERT at `workflow.py:547-569`.
- Append-only round-trip machinery: `_PRESERVED_APPENDED_KEYS` +
  `merge_preserving_feature015` (`tombstone.py:165-177`).

## Required changes

### C1 — frontmatter keys (`tombstone.py`)
- Append `purpose_source`, `key_idea_source` to `_FIELD_ORDER` (END).
- Add `_FEATURE_035_KEYS = ("purpose_source","key_idea_source")` to
  `_PRESERVED_APPENDED_KEYS` (provenance round-trips through any re-write).

### C2 — seed sets provenance (`workflow.py` seed step)
When discover seeds a file it writes:
- `purpose_source = 'doc' if extract_leading_doc(...) != '' else 'absent'`
- `key_idea_source = same as purpose_source` (key_idea is the doc copy today).

### C3 — conditional preservation on re-write (`workflow.py` + UPSERT)
Before seeding, read the existing **tombstone**'s `purpose_source` /
`key_idea_source` and its recorded `sha256`. For each value field:
- IF existing `*_source == 'inferred'` AND existing tombstone `sha256` ==
  current file hash → **carry forward** the existing value + `*_source:
  inferred` (do NOT overwrite). [FR-008 / SC-003]
- ELSE seed mechanically per C2 (stale inference discarded on a real source
  change — FR-007).

The `dart_files` UPSERT column list + `ON CONFLICT DO UPDATE SET` gain
`purpose_source`, `key_idea_source`; the DB-rebuilt case (row absent, tombstone
present with `inferred`, unchanged sha256) restores the inferred values from the
tombstone into the new row.

## Tests
- `test_discover_preserves_inferred_on_unchanged` — enrich, then `discover`
  re-run on unchanged file ⇒ inferred `purpose`/`key_idea`/`*_source` intact
  (SC-003, 100%).
- `test_discover_reblanks_on_source_change` — change source, `discover` ⇒
  values re-seeded, `*_source` reset (FR-007).
- `test_discover_restores_inferred_from_tombstone_when_row_absent` — drop the
  `dart_files` row (simulate rebuilt inventory), `discover` ⇒ inferred values
  restored from tombstone, not blanked.
- Existing `test_discover_idempotence.py` must stay green (no byte change to
  unchanged-file path beyond the new appended keys).
