# Quickstart: Semantic Tombstone Enrichment

**Feature**: `035-semantic-tombstone-enrichment`

## Prerequisites
- `discover` has already run (tombstones exist under `.codeconv/tombstones/`).
- The unified bridge over `D:/bstdev/research/glp/glpnet/.pgdb` is reachable
  (auto-spawned by the CLI).
- Migrations applied through `0011` (`codeconv migrate`).
- Enrichment is driven through the `/codeconv-enrich` skill, which injects the
  Claude-backed `infer_fn`. A bare CLI call (no injection) exits 2 by design.

## Apply the migration
```
codeconv --data-dir D:/bstdev/research/glp/glpnet/.pgdb migrate
```

## Dry-run (see candidates, infer nothing destructive)
Through the skill loop (which supplies `infer_fn`):
```
/codeconv-enrich --dry-run --json
```
Reports `candidates` / would-be `enriched` counts; writes nothing.

## Enrich blank tombstones
```
/codeconv-enrich --json
```
Scoped:
```
/codeconv-enrich --path lib/compiler --json
```

## Verify (US1 independent test)
Pick the canonical blank-doc file and confirm enrichment filled it:
```
# before: purpose: '' / key_idea: ''
codeconv ...   # run enrich
# after:  .codeconv/tombstones/lib/compiler/codegen.dart.md shows
#   purpose: <non-blank, source-grounded>
#   key_idea: <distinct from purpose>
#   purpose_source: inferred
#   key_idea_source: inferred
#   sha256: <unchanged>
```
DB agreement:
```sql
SELECT purpose, key_idea, purpose_source, key_idea_source
FROM codeconv.dart_files WHERE path = 'lib/compiler/codegen.dart';
```

## Idempotence (US2) & clobber-resistance (SC-003)
```
# run enrich twice → second run: 0 inferences, byte-identical tombstones
# then run discover on unchanged files → inferred values preserved (not blanked)
```

## Tests
```
codeconv/.venv/Scripts/python.exe -m pytest codeconv/tests/test_enrich_*.py codeconv/tests/test_discover_*.py codeconv/tests/test_migration_0011_single_head.py -q
```
