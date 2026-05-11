# Contract: D2NET pgdb migration CLI (`d2net-pgdb-migrate`)

Source: spec FR-007, FR-008, FR-009, FR-010; clarifications Q4, Q5, Q12; research R8.

This is the one-shot migration that moves `.D2NET/pgdb/` to `.pgdb/`. It runs once per checkout (idempotent; no-op after first success).

## Invocation

Direct CLI: `d2net-pgdb-migrate [flags]` (built from `tools/d2net/src/D2Net.PgdbMigrate/`).

Slash skill: `/D2NET-pgdb-migrate [flags]` (thin wrapper at `.claude/skills/D2NET-pgdb-migrate/SKILL.md`).

## Flags

| Flag | Type | Default | Semantics |
|---|---|---|---|
| `--repo-root <path>` | path | cwd | Locates `.D2NET/pgdb/` (source) and `.pgdb/` (target). |
| `--dry-run` | bool | false | Compute plan; do not move or back up anything. |
| `--no-backup` | bool | false | Skip backup step. **NOT RECOMMENDED**; only for CI environments where the source is throw-away. |
| `--force` | bool | false | Override the FR-008 refusal (target exists non-empty). The operator confirms they have manually preserved or accepted loss of `.pgdb/` content. |
| `--json` | bool | false | Emit machine-readable summary. |

## State machine (R8)

```
read source presence: src_exists  ← Test-Path(.D2NET/pgdb)
read target presence: tgt_exists  ← Test-Path(.pgdb)
read target non-empty: tgt_nonempty ← Get-ChildItem(.pgdb).Count > 0

CASE (src_exists, tgt_exists, tgt_nonempty):
  (false, *, *):
    PRINT "no-op: .D2NET/pgdb absent (already migrated or never present)"
    EXIT 0
  (true, false, *) OR (true, true, false):
    plan: backup → move
    IF --dry-run: print plan, EXIT 0
    backup_path ← .D2NET/pgdb.bak.<UTC-stamp>
    IF NOT --no-backup:
      copy-recursive .D2NET/pgdb → backup_path  (robocopy /MIR on Windows; cp -r on POSIX)
      verify backup checksum (file count, sizes match)
    move .D2NET/pgdb → .pgdb  (atomic rename if same volume; otherwise copy+delete)
    write .pgdb/.migration-record.json (data-model.md § 3)
    PRINT summary
    EXIT 0
  (true, true, true) AND NOT --force:
    PRINT "REFUSED: both .D2NET/pgdb and .pgdb exist with content. Resolve manually."
    PRINT "  source .D2NET/pgdb: <N1> tables, <N2> bytes"
    PRINT "  target .pgdb:       <M1> tables, <M2> bytes"
    EXIT 78  # configuration-conflict
  (true, true, true) AND --force:
    plan: backup .pgdb → backup .D2NET/pgdb → move
    [as above, with extra backup of .pgdb taken first]
```

## Idempotence (FR-009)

The migration MUST be safe to re-run. Re-runs after a successful migration enter case `(false, *, *)` and exit 0 with a no-op message.

Migration MUST NOT depend on a "migrated" flag file. The presence/absence of `.D2NET/pgdb/` IS the source of truth.

## Crash recovery

If the migration is killed mid-rename:

- If `.D2NET/pgdb/` is partially moved (e.g., some files in `.pgdb/`, some still in `.D2NET/pgdb/`): retry. Robocopy/cp reconciliation is idempotent.
- If `.D2NET/pgdb/` is gone but `.pgdb/` is incomplete: restore from backup (`mv .D2NET/pgdb.bak.<stamp>/* .pgdb/`).
- If both `.D2NET/pgdb/` and `.pgdb/` exist (partial rename): re-run enters case `(true, true, *)` and refuses without `--force` — operator inspects.

## Side-effects on file system

After successful run:

- `.D2NET/pgdb/` is GONE (moved into `.pgdb/`).
- `.D2NET/pgdb.bak.<UTC-stamp>/` exists (unless `--no-backup`).
- `.pgdb/` exists with the same content as the source.
- `.pgdb/.migration-record.json` exists.
- `.D2NET/D2NET-Settings.json` UNCHANGED — FR-007 says only `pgdb/` moves; the rest of `.D2NET/` is untouched.

## Backups and gitignore (FR-029)

The backup dirs `.D2NET/pgdb.bak.*/` MUST be matched by a `.gitignore` rule so they don't accidentally get committed. The migration logs this rule's presence and warns if missing.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success (including no-op) |
| 1 | Generic error |
| 64 | Usage error (bad flag combination) |
| 73 | Backup verification failed |
| 78 | Refused due to non-empty target without `--force` |

## Output (human-readable)

```
d2net-pgdb-migrate: planning…
  source .D2NET/pgdb:  present (12.4 MB, 47 files)
  target .pgdb:        absent
  plan: backup → move

  → taking backup: .D2NET/pgdb.bak.20260509T143211Z
  → backup verified: 47 files, 12.4 MB
  → moving .D2NET/pgdb → .pgdb
  → wrote .pgdb/.migration-record.json

d2net-pgdb-migrate: SUCCESS in 1.2s
  next: run any D2NET command to verify connectivity to the unified bridge.
```

## Acceptance tests

- `tools/d2net/tests/D2Net.PgdbMigrate.Tests/HappyPath.cs` — fresh `.D2NET/pgdb/` present, `.pgdb/` absent → SC-004 row counts preserved.
- `.../Idempotent.cs` — second invocation after success is no-op (FR-009).
- `.../RefuseOnConflict.cs` — both dirs present non-empty → exit 78 without `--force`.
- `.../CrashRecovery.cs` — interrupt mid-move (mock); re-run completes cleanly.
- `.../NoBackupAfterRun.cs` — `--no-backup` flag respected (and warned).

## Slash skill behaviour

`.claude/skills/D2NET-pgdb-migrate/SKILL.md` follows the `/D2NET-init` shape:

- YAML frontmatter declares `name`, `description`, `argument-hint: "[--dry-run] [--force] [--no-backup]"`, `compatibility`.
- Forwards arguments verbatim.
- Inserts a confirmation gate when `--force` is in the resolved args (mirroring `/D2NET-init` Step 6 pattern).
- Surfaces stdout / stderr verbatim.
