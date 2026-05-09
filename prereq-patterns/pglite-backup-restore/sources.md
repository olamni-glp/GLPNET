# Sources — pglite-backup-restore

The AIGRID prereq-patterns catalog is glpnet's consolidating upstream for this pattern. Its `prereq-patterns/pglite-backup-restore/` index reaches into hatzinor's `ulpani_dbbackup.py` (a single ~1976-line module covering both directions), two integration tests (round-trip and atomic-swap-under-failure), one representative on-disk dump as evidence of the snapshot-before-development discipline, and one simpler multi-DB backup script for the small-single-purpose shape. Glpnet has no own implementation today.

## Index

| Path | Upstream | Action | Summary |
|---|---|---|---|
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/pglite-backup-restore/description.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's pattern description — what backup/restore produces, the snapshot-before-development discipline, and the atomic-swap restore semantics. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/pglite-backup-restore/applicability.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's consumer-class notes for the four DB topology cases (idle, under-DBOS-load, serving-Flask-API, inter-version-upgrade). |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/pglite-backup-restore/sources.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's upstream-source citations into hatzinor: `ulpani_dbbackup.py`, the two integration tests, one representative on-disk dump, and the simpler LMS-bundled `backup.py` reference. |

## Per-source notes

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/pglite-backup-restore/description.md`

- The two non-obvious correctness rules (snapshot-before-development; atomic-swap-not-drop-and-recreate) are the load-bearing claims a glpnet adopter MUST internalise before iterating on the tool itself. The AIGRID file's "Why it matters" section is the rationale plus the operator-history precedent.
- Recovery-point granularity is whole-database, not per-table or per-row. Glpnet adopters with stricter recovery-point-objectives need to layer their own delta-strategy on top.

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/pglite-backup-restore/applicability.md`

- Per-topology H3s cover four cases and what changes from the idle path. The DBOS-load case carries the at-most-once-after-restore semantics for non-idempotent steps; the Flask-API case relies on the bridge's `globalWorkChain` for backup-vs-API serialisation; the inter-version case requires manifest version validation before applying.

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/pglite-backup-restore/sources.md`

- Cites `D:/BSTDEV/lang/hatzinor_ai-ddp/src/ulpani_dbbackup.py@develop` (`olamni-research/hatzinor_ai-data-driven-publishing@develop`, Action: Model) — the single-module backup AND restore tool. Key entry points: `backup_db()`, `backup_all()`, `restore()`, `_take_pre_restore_snapshot()`, `_apply_atomic_swap()`, `_verify_fingerprint()`, `_self_verify()`.
- Cites two integration tests — `test_dbbackup_roundtrip.py` (Action: Read) and `test_dbrestore_atomic_swap.py` (Action: Read). The first asserts backup → wipe → restore roundtrip invariance; the second arranges a mid-apply failure and asserts the live database is untouched.
- Cites a representative on-disk dump (`ulpani/dbbackup/20260507-094857/postgres/dump.sql`) as operational evidence. The companion `NOTES.md` records a real story: dump succeeded, post-dump dry-run failed for an FK reason, `BACKUP-INCOMPLETE.txt` was written for operator review — but the dump itself is consistent and usable for emergency manual restore.
- Cites the simpler `now_lms/db/backup.py` (~154 lines) as the smaller-purpose shape — useful when a Flask-LMS-style consumer wants `pg_dump`/`psql`/`mysqldump`-based backup rather than the pglite-specific path.
