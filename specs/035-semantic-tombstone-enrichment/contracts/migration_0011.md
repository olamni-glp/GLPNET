# Contract: migration `0011_enrich_provenance` (Constitution VI-a)

**Feature**: 035 | File:
`codeconv/src/codeconv/db/migrations/versions/0011_enrich_provenance.py`

## Header
```python
revision: str = "0011"
down_revision: Union[str, None] = "0010"      # chains off marathon_schema (current head)
branch_labels = None
depends_on = None
```

## `upgrade()`
```python
op.execute("ALTER TABLE codeconv.dart_files ADD COLUMN IF NOT EXISTS purpose_source  text NOT NULL DEFAULT 'absent'")
op.execute("ALTER TABLE codeconv.dart_files ADD COLUMN IF NOT EXISTS key_idea_source text NOT NULL DEFAULT 'absent'")
op.execute("""
    UPDATE codeconv.dart_files
       SET purpose_source  = CASE WHEN purpose  = '' THEN 'absent' ELSE 'doc' END,
           key_idea_source = CASE WHEN key_idea = '' THEN 'absent' ELSE 'doc' END
""")
```
Additive + idempotent (`IF NOT EXISTS`). Backfill is exact: mechanical seeding
is the only current source of non-blank values, so non-blank ⇒ `doc`, blank ⇒
`absent` (research R-005).

## `downgrade()`
```python
op.execute("ALTER TABLE codeconv.dart_files DROP COLUMN IF EXISTS key_idea_source")
op.execute("ALTER TABLE codeconv.dart_files DROP COLUMN IF EXISTS purpose_source")
```

## Head-assertion test (VI-a is machine-checkable)
Add `codeconv/tests/test_migration_0011_single_head.py`, mirroring
`test_migration_0010_single_head.py`:
```python
def test_exactly_one_head_offline() -> None:
    assert _script_dir().get_heads() == ["0011"]

def test_linear_chain_through_0011_offline() -> None:
    chain = {r.revision: r.down_revision for r in _script_dir().walk_revisions()}
    assert chain["0011"] == "0010" and chain["0010"] == "0009"  # …→0001: None
```
After this feature, the constitution's "current head" reference advances
`0010 → 0011`; the single linear head discipline is preserved (no branch/merge).
The pre-existing `test_migration_0010_single_head.py` asserting `["0010"]` will
need updating to `["0011"]` (or superseding) — flag for `/bk-tasks`.
