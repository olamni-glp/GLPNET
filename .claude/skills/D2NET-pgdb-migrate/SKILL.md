---
name: "D2NET-pgdb-migrate"
description: "One-shot migration of legacy .D2NET/pgdb/ to the unified .pgdb/ at repo root. Idempotent (no-op after first success). Confirms before destructive --force overwrites."
argument-hint: "Empty runs the migration with auto-detected paths. Use --dry-run to preview, --force to overwrite a non-empty .pgdb/, --no-backup to skip the source backup, --json for machine-readable output."
compatibility: "Requires tools/d2net/src/D2Net.PgdbMigrate/ in the repo and a built or buildable d2net-pgdb-migrate binary. .NET 8 SDK on PATH for the dotnet-run fallback."
metadata:
  author: "GLPNET"
  source: "specs/012-codeconv-runner/contracts/d2net_pgdb_migration_cli.md"
user-invocable: true
disable-model-invocation: false
---


## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Goal

Run the `d2net-pgdb-migrate` CLI on behalf of the user. This is the one-shot migration that moves `.D2NET/pgdb/` → `.pgdb/` for feature 012's unified-bridge layout.

The procedure below mirrors the `/D2NET-init` shape. Follow each step in order.

## Operating Constraints

- **NEVER pass `--force`** unless the destructive-operation gate (Step 5) was completed affirmatively in this conversation.
- **NEVER walk up** to find `.git/` or change the working directory the user invoked from.
- **NEVER modify** `.D2NET/D2NET-Settings.json` or any file outside `.D2NET/pgdb/` (source) and `.pgdb/` (target). The migration is scope-limited to those two directories plus the backup `.D2NET/pgdb.bak.<UTC-stamp>/`.
- **AT MOST ONE** `dotnet build` per skill invocation. If the build fails, surface its stderr and stop.

## Procedure

### Step 1 — Read user input

The user's freeform argument string is in the `$ARGUMENTS` block above. Empty input runs the migration with default settings (auto-detected `--repo-root`, no `--force`, backup enabled, human-readable output).

### Step 2 — Locate the binary

Search in this exact order. On Windows the binary is `d2net-pgdb-migrate.exe`; on macOS / Linux it is `d2net-pgdb-migrate`.

1. `tools/d2net/src/D2Net.PgdbMigrate/bin/Release/net8.0/d2net-pgdb-migrate[.exe]`.
2. `tools/d2net/src/D2Net.PgdbMigrate/bin/Debug/net8.0/d2net-pgdb-migrate[.exe]`.
3. Fallback to `dotnet run --project tools/d2net/src/D2Net.PgdbMigrate -- <args>`. If selecting this path, emit a one-line note: "Using `dotnet run` fallback (slower). Run `dotnet build tools/d2net/D2Net.sln` once to enable the direct-binary path."

If neither Release nor Debug binary exists AND `dotnet --version` fails (no .NET SDK on PATH), emit a single message naming all three missing prerequisites and stop.

### Step 3 — Detect missing or stale binary

If Step 2 selected the fallback path because **no binary exists**, the binary is **missing**. Otherwise check **staleness**: `mtime(binary)` vs `max(mtime(.cs file under tools/d2net/src/D2Net.PgdbMigrate))`.

- Missing: emit `d2net-pgdb-migrate binary is missing at <path> — build now? (yes/no)` and wait for affirmative.
- Stale: emit `d2net-pgdb-migrate binary may be stale (newest source: <path> @ <time>; binary @ <time>) — rebuild now? (yes/no)` and wait for affirmative.

On affirmative, run `dotnet build tools/d2net/D2Net.sln`, surface output, continue. On non-affirmative, stop without invoking the binary.

### Step 4 — Parse user intent

Branch on `$ARGUMENTS`:

- **Empty** → resolved flag set is `[]` (default migration: backup + move). Proceed to Step 5.
- **`help` / `--help` / `-h`** → resolved flag set = `[--help]`. Skip Steps 5–6. Proceed to Step 7.
- **`dry-run` / phrases like `preview` / `plan only`** → add `--dry-run`.
- **`force` / phrases like `force overwrite` / `--force` literal** → add `--force`. Triggers Step 5 destructive gate.
- **`no-backup` / `--no-backup` literal / `skip backup`** → add `--no-backup`. Triggers Step 5 (because skipping backup is destructive of the source on failure).
- **`json` / `--json` literal / `as json`** → add `--json`.
- **`--repo-root <path>` literal** → preserve verbatim.

Pure pass-through (every token is `--<flag>` or a value following one) is also accepted — pass tokens verbatim.

### Step 5 — Destructive-operation gate

Triggered when the resolved flag set contains `--force` OR `--no-backup`.

For `--force`: emit
```
You are about to OVERWRITE the existing .pgdb/ directory. Any content there will be backed up to .D2NET/pgdb-target.bak.<UTC-stamp>/, then deleted before the migration source is moved into place.

Proceed? (yes/no)
```

For `--no-backup` (and not also `--force`): emit
```
You are about to MOVE .D2NET/pgdb → .pgdb without taking a backup of the source. If the move is interrupted mid-flight, recovery may require manual file-system surgery.

Proceed? (yes/no)
```

For BOTH `--force` AND `--no-backup`: emit a single combined warning and require ONE affirmative reply.

Wait for one of `yes` / `y` / `confirmed` / `proceed`. On any non-affirmative reply, stop without invoking the binary.

### Step 6 — Pre-invocation summary

Echo the planned invocation (binary path + resolved flag set) verbatim. Example:
```
Running: tools/d2net/src/D2Net.PgdbMigrate/bin/Debug/net8.0/d2net-pgdb-migrate.exe --force
```

### Step 7 — Invoke the binary

Run via Bash with the CWD as the user's invocation directory (NEVER `cd` away). Capture stdout and stderr.

### Step 8 — Surface results

Print the binary's stdout and stderr verbatim. Map exit codes:

| Code | User-facing summary |
|---|---|
| 0 | "Migration succeeded (or no-op)." |
| 1 | "Migration failed (filesystem error). See stderr above." |
| 64 | "Argument error. Re-issue with valid flags." |
| 73 | "Backup verification failed. Migration aborted; source untouched." |
| 78 | "Migration refused: both .D2NET/pgdb and .pgdb exist with content. Inspect manually, then re-run with `--force` after taking your own backup of `.pgdb/` if needed." |

Do NOT re-format the binary's output beyond this exit-code summary line.

### Step 9 — Post-success follow-up (informational)

After exit 0 (success, not no-op), suggest:
```
Next: any D2NET command (/D2NET-init, /D2NET-scaffold) and any codeconv command (/codeconv-runner, /codeconv-discover) will now share the unified bridge at .pgdb/.
```

## Examples

### Empty input — default migration

```
User: /D2NET-pgdb-migrate
Skill: (no destructive flags) → invokes d2net-pgdb-migrate with no args → reports SUCCESS.
```

### Dry-run preview

```
User: /D2NET-pgdb-migrate dry-run
Skill: invokes d2net-pgdb-migrate --dry-run → prints plan, exits 0.
```

### Force overwrite

```
User: /D2NET-pgdb-migrate force
Skill: emits Step 5 confirmation → on affirmative → invokes d2net-pgdb-migrate --force.
```

### Conflict resolution

```
User: /D2NET-pgdb-migrate
Skill: binary exits 78 (both dirs present non-empty). Skill summarises:
       "Migration refused: both .D2NET/pgdb and .pgdb exist with content.
        Inspect manually, then re-run with `--force` after taking your own
        backup of `.pgdb/` if needed."
```
